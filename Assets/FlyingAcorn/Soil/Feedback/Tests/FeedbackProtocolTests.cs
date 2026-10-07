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
        [TestCase(409, "{\"detail\":{\"code\":18,\"message\":\"channel_disabled\"}}", FeedbackStatus.ChannelDisabled)]
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

        private const string GroupedChannels =
            "{\"detail\":{\"code\":3,\"message\":\"channels_listed\"},\"channels\":[{\"key\":\"word_suggestion\"," +
            "\"name\":\"Word suggestions\",\"rating\":\"none\",\"message_required\":false,\"max_message_length\":200," +
            "\"daily_limit\":20,\"remaining_today\":20,\"group_by_target\":true,\"once_per_target\":true}]}";

        [Test]
        public void GroupedChannel_ParsesItsOptions()
        {
            var words = FeedbackProtocol.ParseChannels(200, GroupedChannels).Find("word_suggestion");
            Assert.IsTrue(words.group_by_target);
            Assert.IsTrue(words.once_per_target);
            // Older servers do not send them: off.
            Assert.IsFalse(FeedbackProtocol.ParseChannels(200, Channels).Find("rate_app").group_by_target);
        }

        [Test]
        public void GroupedChannel_TakesTheTargetAlone_AndNeedsOne()
        {
            var words = FeedbackProtocol.ParseChannels(200, GroupedChannels).Find("word_suggestion");
            Assert.IsNull(FeedbackProtocol.Check(words, new FeedbackSubmission("word_suggestion") { Target = "BAR, English" }));
            Assert.AreEqual(FeedbackStatus.TargetRequired,
                FeedbackProtocol.Check(words, new FeedbackSubmission("word_suggestion") { Target = "   " }));
            Assert.AreEqual(FeedbackStatus.TargetTooLong,
                FeedbackProtocol.Check(words, new FeedbackSubmission("word_suggestion") { Target = new string('t', 101) }));
        }

        [Test]
        public void Check_CleansAndCountsLikeTheServer()
        {
            var words = FeedbackProtocol.ParseChannels(200, GroupedChannels).Find("word_suggestion");
            // Only control characters: nothing left, as the server sees it.
            Assert.AreEqual(FeedbackStatus.TargetRequired,
                FeedbackProtocol.Check(words, new FeedbackSubmission("word_suggestion") { Target = "\u0001\u0007 " }));
            // 100 emoji are 200 UTF-16 units but 100 characters: allowed, as on the server.
            var emoji = string.Concat(System.Linq.Enumerable.Repeat("\U0001F600", 100));
            Assert.AreEqual(100, FeedbackProtocol.CountCharacters(emoji));
            Assert.IsNull(FeedbackProtocol.Check(words, new FeedbackSubmission("word_suggestion") { Target = emoji }));
            Assert.AreEqual("a\tb\nc", FeedbackProtocol.Clean(" a\tb\u0000\nc\u007f "));
        }

        [Test]
        public void TargetRequired_IsARefusal()
        {
            var result = FeedbackProtocol.ParseSend(400, "{\"detail\":{\"code\":16,\"message\":\"target_required\"}}");
            Assert.AreEqual(FeedbackStatus.TargetRequired, result.Status);
            Assert.IsFalse(result.Succeeded);
        }

        [Test]
        public void StatusNumbersMatchTheServer()
        {
            // feedback/return_codes.py, in code order; shipped games compare the numbers.
            var expected = new[]
            {
                "feedback_sent", "already_received", "feedback_listed", "channels_listed", "channel_not_found",
                "invalid_request", "rating_required", "rating_not_allowed", "invalid_rating", "message_required",
                "message_too_long", "target_too_long", "data_too_large", "daily_limit_reached", "throttled",
                "feedback_error", "target_required", "too_many_targets", "channel_disabled",
            };
            Assert.AreEqual(expected.Length, System.Enum.GetValues(typeof(FeedbackStatus)).Length);
            for (var code = 0; code < expected.Length; code++)
            {
                var name = System.Enum.GetName(typeof(FeedbackStatus), code);
                Assert.AreEqual(expected[code].Replace("_", ""), name?.ToLowerInvariant(), $"code {code}");
            }
        }

        [Test]
        public void CountCharacters_CountsOnlyRealPairsAsOne()
        {
            // A lone surrogate is sent as U+FFFD, one character on the server.
            Assert.AreEqual(3, FeedbackProtocol.CountCharacters("a\udc00b"));
            Assert.AreEqual(3, FeedbackProtocol.CountCharacters("a\ud800b"));
            Assert.AreEqual(1, FeedbackProtocol.CountCharacters("\ud83d\ude00"));
            // Low before high is two lone ones, not a pair.
            Assert.AreEqual(2, FeedbackProtocol.CountCharacters("\ude00\ud83d"));
            Assert.AreEqual(2, FeedbackProtocol.CountCharacters("\ud83d\ud83d\ude00"));
        }

        [Test]
        public void TargetKey_MatchesTheServer()
        {
            // Expected keys are what the server's target_key (feedback/models.py) gives for the cleaned target.
            Assert.AreEqual("bar, english", FeedbackProtocol.TargetKey("  BAR,\u00a0\u00a0English\u3000"));
            Assert.AreEqual("caf\u00e9 , french", FeedbackProtocol.TargetKey(" Cafe\u0301 ,\t French\n"));
            Assert.AreEqual("ab c", FeedbackProtocol.TargetKey("a\u0001b  \u2003 c"));
            Assert.AreEqual("strasse, german", FeedbackProtocol.TargetKey("Stra\u00dfe, German"));
            Assert.AreEqual(FeedbackProtocol.TargetKey("STRASSE, German"), FeedbackProtocol.TargetKey("Stra\u00dfe, German"));
            Assert.AreEqual("i\u0307stanbul", FeedbackProtocol.TargetKey("\u0130STANBUL"));
            Assert.AreEqual("\u03bf\u03b4\u03bf\u03c3", FeedbackProtocol.TargetKey("\u039f\u0394\u039f\u03a3"));
            Assert.AreEqual("\u03bf\u03b4\u03bf\u03c3", FeedbackProtocol.TargetKey("\u03bf\u03b4\u03bf\u03c2"));
            Assert.AreEqual("final s", FeedbackProtocol.TargetKey("\ufb01nal \u017f"));
            // Persian keeps its zero-width non-joiner: it is not a space.
            Assert.AreEqual("\u0628\u0627\u0631\u200c\u0647\u0627, persian",
                FeedbackProtocol.TargetKey("\u0628\u0627\u0631\u200c\u0647\u0627,\tPersian"));
            Assert.AreEqual("", FeedbackProtocol.TargetKey(null));
        }

        [Test]
        public void TargetKey_MatchesAResultToWhatWasSent()
        {
            var result = FeedbackProtocol.ParseSend(201, SentSeveral);
            var sent = new[] { "\u0628\u0627\u0631,  Persian", "BAR, English", " KITE, English" };
            for (var i = 0; i < sent.Length; i++)
                Assert.AreEqual(FeedbackProtocol.TargetKey(sent[i]), FeedbackProtocol.TargetKey(result.targets[i].target));
        }

        private const string ListChannels =
            "{\"detail\":{\"code\":3,\"message\":\"channels_listed\"},\"channels\":[{\"key\":\"word_suggestion\"," +
            "\"name\":\"Word suggestions\",\"rating\":\"none\",\"message_required\":false,\"max_message_length\":200," +
            "\"daily_limit\":20,\"remaining_today\":20,\"group_by_target\":true,\"once_per_target\":true," +
            "\"max_targets_per_send\":5}]}";

        // A send of three: one saved, one sent before, one past the daily limit (feedback/views.py target_result).
        private const string SentSeveral =
            "{\"detail\":{\"code\":0,\"message\":\"feedback_sent\"},\"targets\":[" +
            "{\"target\":\"\u0628\u0627\u0631, Persian\",\"detail\":{\"code\":0,\"message\":\"feedback_sent\"}," +
            "\"feedback\":{\"id\":\"5e0c1f7a-3b8e-4f43-9a1e-0b8d2a7c6e11\",\"channel\":\"word_suggestion\"," +
            "\"target\":\"\u0628\u0627\u0631, Persian\",\"rating\":null,\"message\":\"\",\"data\":{}," +
            "\"client_id\":\"8d1f2b5c-6a3e-5f71-9c2d-4e8b0a7f3d21\",\"status\":\"new\",\"seen\":false," +
            "\"created_at\":\"2026-10-07T09:12:03.120511+00:00\"},\"retry_after\":null}," +
            "{\"target\":\"bar, english\",\"detail\":{\"code\":1,\"message\":\"already_received\"}," +
            "\"feedback\":{\"id\":\"1b7a9c3e-2d4f-4a6b-8c0e-f1a2b3c4d5e6\",\"channel\":\"word_suggestion\"," +
            "\"target\":\"BAR, English\",\"rating\":null,\"message\":\"\",\"data\":{},\"client_id\":null," +
            "\"status\":\"applied\",\"seen\":true,\"created_at\":\"2026-10-05T10:00:00+00:00\"},\"retry_after\":null}," +
            "{\"target\":\"KITE, English\",\"detail\":{\"code\":13,\"message\":\"daily_limit_reached\"}," +
            "\"feedback\":null,\"retry_after\":41230}]}";

        [Test]
        public void SeveralTargets_ParseEachOnesResult()
        {
            var result = FeedbackProtocol.ParseSend(201, SentSeveral);
            Assert.AreEqual(FeedbackStatus.FeedbackSent, result.Status);
            Assert.IsTrue(result.Succeeded);
            Assert.IsNull(result.feedback);
            Assert.AreEqual(3, result.targets.Count);
            Assert.AreEqual("\u0628\u0627\u0631, Persian", result.targets[0].target);
            Assert.AreEqual(FeedbackStatus.FeedbackSent, result.targets[0].Status);
            Assert.IsTrue(result.targets[0].Accepted);
            Assert.AreEqual(FeedbackStatus.AlreadyReceived, result.targets[1].Status);
            Assert.IsTrue(result.targets[1].Accepted);
            Assert.AreEqual(FeedbackReviewStatus.Applied, result.targets[1].feedback.ReviewStatus);
            Assert.AreEqual(FeedbackStatus.DailyLimitReached, result.targets[2].Status);
            Assert.IsFalse(result.targets[2].Accepted);
            Assert.IsNull(result.targets[2].feedback);
            Assert.AreEqual(41230, result.targets[2].retry_after);
            Assert.IsNull(result.targets[0].retry_after);
            // A single send has none.
            Assert.IsNull(FeedbackProtocol.ParseSend(201, Sent).targets);
        }

        [Test]
        public void SeveralTargets_NothingSavedForTheLimitIsARefusalWithEachResult()
        {
            var body = SentSeveral.Replace("{\"detail\":{\"code\":0,\"message\":\"feedback_sent\"},\"targets\"",
                "{\"detail\":{\"code\":13,\"message\":\"daily_limit_reached\"},\"targets\"");
            var result = FeedbackProtocol.ParseSend(429, body, "41230");
            Assert.AreEqual(FeedbackStatus.DailyLimitReached, result.Status);
            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(41230, result.RetryAfterSeconds);
            Assert.AreEqual(3, result.targets.Count);
        }

        [Test]
        public void SeveralTargets_ToJsonSendsTheList()
        {
            var submission = new FeedbackSubmission("word_suggestion") { Targets = new List<string> { "BAR, English", "\u0628\u0627\u0631, Persian" } };
            var body = JObject.Parse(FeedbackProtocol.ToJson(submission));
            CollectionAssert.AreEqual(new[] { "BAR, English", "\u0628\u0627\u0631, Persian" }, body["targets"].ToObject<string[]>());
            Assert.IsNull(body["target"]);
            Assert.AreEqual(submission.ClientId, body["client_id"].Value<string>());
            Assert.IsNull(JObject.Parse(FeedbackProtocol.ToJson(new FeedbackSubmission("support") { Message = "hi" }))["targets"]);
        }

        [Test]
        public void SeveralTargets_CheckMirrorsTheServer()
        {
            var words = FeedbackProtocol.ParseChannels(200, ListChannels).Find("word_suggestion");
            Assert.AreEqual(5, words.max_targets_per_send);
            FeedbackStatus? Check(FeedbackChannelInfo channel, params string[] targets) =>
                FeedbackProtocol.Check(channel, new FeedbackSubmission(channel.key) { Targets = targets });

            Assert.IsNull(Check(words, "A, English", "B, English", "C, English", "D, English", "E, English"));
            Assert.AreEqual(FeedbackStatus.TooManyTargets, Check(words, "A", "B", "C", "D", "E", "F"));
            Assert.AreEqual(FeedbackStatus.TargetRequired, Check(words));
            Assert.AreEqual(FeedbackStatus.TargetRequired, Check(words, "A, English", " \u0001 "));
            Assert.AreEqual(FeedbackStatus.TargetTooLong, Check(words, "A, English", new string('t', 101)));
            Assert.AreEqual(FeedbackStatus.InvalidRequest, Check(words, "A, English", null));
            Assert.AreEqual(FeedbackStatus.InvalidRequest, FeedbackProtocol.Check(words,
                new FeedbackSubmission("word_suggestion") { Target = "A, English", Targets = new[] { "B, English" } }));
            // Too many is told before an empty item, as the server does.
            Assert.AreEqual(FeedbackStatus.TooManyTargets, Check(words, "", "", "", "", "", ""));

            // A channel that does not group takes no list; one left at a single target takes a list of one.
            var rate = FeedbackProtocol.ParseChannels(200, Channels).Find("rate_app");
            Assert.AreEqual(FeedbackStatus.InvalidRequest, FeedbackProtocol.Check(rate,
                new FeedbackSubmission("rate_app") { Rating = 5, Targets = new[] { "x" } }));
            var single = FeedbackProtocol.ParseChannels(200, GroupedChannels).Find("word_suggestion");
            Assert.AreEqual(1, single.max_targets_per_send);
            Assert.IsNull(Check(single, "A, English"));
            Assert.AreEqual(FeedbackStatus.TooManyTargets, Check(single, "A, English", "B, English"));
        }
    }
}
