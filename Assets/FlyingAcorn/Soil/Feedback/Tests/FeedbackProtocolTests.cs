using System.Collections.Generic;
using FlyingAcorn.Soil.Feedback.Logic;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace FlyingAcorn.Soil.Feedback.Tests
{
    /// <summary>Bodies are copied from the server's answers (feedback/views.py).</summary>
    public class FeedbackProtocolTests
    {
        private const string Sent =
            "{\"detail\":{\"code\":0,\"message\":\"feedback_sent\"},\"feedback\":{\"id\":\"1688880b-5033-407d-be0f-fb1a85eb9a5c\"," +
            "\"channel\":\"rate_app\",\"target\":\"\",\"rating\":2,\"message\":\"Too many ads\",\"data\":{\"level\":14}," +
            "\"client_id\":\"0b6f2a8e-1c39-4f5e-9a57-2d1c7b3e8f40\",\"status\":\"new\",\"seen\":false," +
            "\"created_at\":\"2026-10-04T18:56:56.481487+00:00\"}}";

        private const string Channels =
            "{\"detail\":{\"code\":3,\"message\":\"channels_listed\"},\"channels\":[{\"key\":\"rate_app\",\"name\":\"App rating\"," +
            "\"rating\":\"required\",\"message_required\":false,\"max_message_length\":1000,\"daily_limit\":3,\"remaining_today\":3}," +
            "{\"key\":\"word_suggestion\",\"name\":\"Word suggestions\",\"rating\":\"none\",\"message_required\":true," +
            "\"max_message_length\":40,\"daily_limit\":20,\"remaining_today\":0}]}";

        private const string Listed =
            "{\"detail\":{\"code\":2,\"message\":\"feedback_listed\"},\"feedback\":[{\"id\":\"1b7a2fcc-dbc8-43b5-8e25-19238efa4397\"," +
            "\"channel\":\"word_suggestion\",\"target\":\"KITE\",\"rating\":null,\"message\":\"kite\",\"data\":{},\"client_id\":null," +
            "\"status\":\"applied\",\"seen\":true,\"created_at\":\"2026-10-04T18:56:56.628674+00:00\"}]}";

        [Test]
        public void Sent_ParsesTheSavedFeedback()
        {
            var result = FeedbackProtocol.ParseSend(201, Sent);
            Assert.AreEqual(FeedbackStatus.FeedbackSent, result.Status);
            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(2, result.feedback.rating);
            Assert.AreEqual(14, result.feedback.data["level"].Value<int>());
            Assert.AreEqual(FeedbackReviewStatus.New, result.feedback.ReviewStatus);
            // Not reformatted into the device's time zone.
            Assert.AreEqual("2026-10-04T18:56:56.481487+00:00", result.feedback.created_at);
        }

        [Test]
        public void AlreadyReceived_IsASuccess()
        {
            var result = FeedbackProtocol.ParseSend(200, Sent.Replace("\"code\":0,\"message\":\"feedback_sent\"",
                "\"code\":1,\"message\":\"already_received\""));
            Assert.AreEqual(FeedbackStatus.AlreadyReceived, result.Status);
            Assert.IsTrue(result.Succeeded);
        }

        [TestCase(404, "{\"detail\":{\"code\":4,\"message\":\"channel_not_found\"}}", FeedbackStatus.ChannelNotFound)]
        [TestCase(400, "{\"detail\":{\"code\":6,\"message\":\"rating_required\"}}", FeedbackStatus.RatingRequired)]
        [TestCase(400, "{\"detail\":{\"code\":10,\"message\":\"message_too_long\"}}", FeedbackStatus.MessageTooLong)]
        public void Refusals_AreAnswersNotFailures(long status, string body, FeedbackStatus expected)
        {
            var result = FeedbackProtocol.ParseSend(status, body);
            Assert.AreEqual(expected, result.Status);
            Assert.IsFalse(result.Succeeded);
            Assert.IsNull(result.feedback);
        }

        [TestCase("{\"detail\":{\"code\":14,\"message\":\"throttled\"}}", FeedbackStatus.Throttled)]
        [TestCase("{\"detail\":{\"code\":13,\"message\":\"daily_limit_reached\"}}", FeedbackStatus.DailyLimitReached)]
        public void TooMany_CarriesRetryAfter(string body, FeedbackStatus expected)
        {
            var result = FeedbackProtocol.ParseSend(429, body, "60");
            Assert.AreEqual(expected, result.Status);
            Assert.AreEqual(60, result.RetryAfterSeconds);
        }

        [TestCase(401, "{\"detail\": \"Authentication credentials were not provided.\"}")]
        [TestCase(403, "{\"detail\": \"This API is not available for you\"}")]
        [TestCase(502, "<html>Bad gateway</html>")]
        [TestCase(200, "")]
        public void BodiesThatAreNotFeedbackAnswersAreNotParsed(long status, string body)
        {
            Assert.IsNull(FeedbackProtocol.ParseSend(status, body));
            Assert.IsNull(FeedbackProtocol.ParseChannels(status, body));
            Assert.IsNull(FeedbackProtocol.ParseList(status, body));
        }

        [Test]
        public void Channels_ParseRules()
        {
            var list = FeedbackProtocol.ParseChannels(200, Channels);
            Assert.AreEqual(2, list.channels.Count);
            Assert.AreEqual(RatingMode.Required, list.Find("rate_app").Rating);
            Assert.AreEqual(RatingMode.None, list.Find("word_suggestion").Rating);
            Assert.AreEqual(40, list.Find("word_suggestion").max_message_length);
            Assert.IsNull(list.Find("support"));
        }

        [Test]
        public void MyFeedback_ParsesStatus()
        {
            var entry = FeedbackProtocol.ParseList(200, Listed).feedback[0];
            Assert.AreEqual(FeedbackReviewStatus.Applied, entry.ReviewStatus);
            Assert.IsTrue(entry.seen);
            Assert.IsNull(entry.rating);
            Assert.AreEqual("KITE", entry.target);
        }

        [Test]
        public void ToJson_SendsOnlyWhatIsSet_AndAlwaysTheClientId()
        {
            var submission = new FeedbackSubmission("rate_app") { Rating = 2 };
            var body = JObject.Parse(FeedbackProtocol.ToJson(submission));
            Assert.AreEqual("rate_app", body["channel"].Value<string>());
            Assert.AreEqual(2, body["rating"].Value<int>());
            Assert.AreEqual(submission.ClientId, body["client_id"].Value<string>());
            Assert.IsNull(body["message"]);
            Assert.IsNull(body["target"]);
            Assert.IsNull(body["data"]);

            submission.Message = "Too many ads";
            submission.Data = new Dictionary<string, object> { ["level"] = 14 };
            body = JObject.Parse(FeedbackProtocol.ToJson(submission));
            Assert.AreEqual(14, body["data"]["level"].Value<int>());
        }

        [Test]
        public void ClientId_StaysTheSameForRetries()
        {
            var submission = new FeedbackSubmission("support");
            Assert.AreEqual(JObject.Parse(FeedbackProtocol.ToJson(submission))["client_id"],
                JObject.Parse(FeedbackProtocol.ToJson(submission))["client_id"]);
            Assert.AreNotEqual(submission.ClientId, new FeedbackSubmission("support").ClientId);
            Assert.AreEqual("kept", new FeedbackSubmission("support", "kept").ClientId);
        }

        [Test]
        public void Check_MirrorsTheServerRules()
        {
            var channels = FeedbackProtocol.ParseChannels(200, Channels);
            var rate = channels.Find("rate_app");
            var words = channels.Find("word_suggestion");
            Assert.IsNull(FeedbackProtocol.Check(rate, new FeedbackSubmission("rate_app") { Rating = 5 }));
            Assert.AreEqual(FeedbackStatus.RatingRequired,
                FeedbackProtocol.Check(rate, new FeedbackSubmission("rate_app") { Message = "meh" }));
            Assert.AreEqual(FeedbackStatus.InvalidRating,
                FeedbackProtocol.Check(rate, new FeedbackSubmission("rate_app") { Rating = 6 }));
            Assert.AreEqual(FeedbackStatus.RatingNotAllowed,
                FeedbackProtocol.Check(words, new FeedbackSubmission("word_suggestion") { Message = "KITE", Rating = 3 }));
            Assert.AreEqual(FeedbackStatus.MessageRequired,
                FeedbackProtocol.Check(words, new FeedbackSubmission("word_suggestion") { Message = "  " }));
            words.remaining_today = 5;
            Assert.AreEqual(FeedbackStatus.MessageTooLong,
                FeedbackProtocol.Check(words, new FeedbackSubmission("word_suggestion") { Message = new string('x', 41) }));
            Assert.AreEqual(FeedbackStatus.TargetTooLong,
                FeedbackProtocol.Check(words, new FeedbackSubmission("word_suggestion") { Message = "x", Target = new string('t', 101) }));
            words.remaining_today = 0;
            Assert.AreEqual(FeedbackStatus.DailyLimitReached,
                FeedbackProtocol.Check(words, new FeedbackSubmission("word_suggestion") { Message = "KITE" }));
            Assert.AreEqual(FeedbackStatus.ChannelNotFound, FeedbackProtocol.Check(null, new FeedbackSubmission("x")));
            // Two problems: the target is named first, as the server does.
            Assert.AreEqual(FeedbackStatus.TargetTooLong, FeedbackProtocol.Check(words,
                new FeedbackSubmission("word_suggestion") { Rating = 3, Target = new string('t', 101) }));
        }

        [Test]
        public void StatusNumbersMatchTheServer()
        {
            // Pinned by feedback/tests.py test_codes_are_unique_and_never_renumbered on the server.
            Assert.AreEqual(0, (int)FeedbackStatus.FeedbackSent);
            Assert.AreEqual(13, (int)FeedbackStatus.DailyLimitReached);
            Assert.AreEqual(15, (int)FeedbackStatus.FeedbackError);
        }
    }
}
