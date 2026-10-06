using System;
using FlyingAcorn.Soil.Socialization.Logic;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace FlyingAcorn.Soil.Socialization.Tests
{
    // Bodies are copied from the server's answers (socialization/views_requests.py).
    public class FriendsProtocolTests
    {
        private const string Sara =
            "{\"uuid\": \"7d829b62-b897-4984-ba1a-bb88948bc980\", \"public_id\": \"LC88VN3T\", \"name\": \"Sara\", " +
            "\"username\": \"sara\", \"avatar_asset\": null}";

        [Test]
        public void RequestSent_IsASuccessWithTheOtherPlayer()
        {
            var result = FriendsProtocol.ParseAction(200,
                "{\"detail\": {\"code\": 7, \"message\": \"request_sent\"}, \"user\": " + Sara + "}");
            Assert.AreEqual(FriendStatus.RequestSent, result.Status);
            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual("LC88VN3T", result.user.public_id);
            Assert.AreEqual("7d829b62-b897-4984-ba1a-bb88948bc980", result.user.uuid);
        }

        [Test]
        public void RefusalsAreAnswersNotFailures()
        {
            var limit = FriendsProtocol.ParseAction(409,
                "{\"detail\": {\"code\": 15, \"message\": \"request_limit_reached\"}, \"user\": " + Sara + "}");
            Assert.AreEqual(FriendStatus.RequestLimitReached, limit.Status);
            Assert.AreEqual(409, limit.HttpStatus);
            Assert.IsFalse(limit.Succeeded);
            Assert.IsNotNull(limit.user);
            Assert.IsNull(limit.RetryAfterSeconds);

            var missing = FriendsProtocol.ParseAction(404, "{\"detail\": {\"code\": 3, \"message\": \"friend_not_found\"}}");
            Assert.AreEqual(FriendStatus.FriendNotFound, missing.Status);
            Assert.AreEqual(404, missing.HttpStatus);
            Assert.IsFalse(missing.Succeeded);
            Assert.IsNull(missing.user);
        }

        [Test]
        public void RequestCooldown_IsARefusalWithTheOtherPlayer()
        {
            // A request within 24 hours of cancelling it, or of the other player declining it.
            var body = "{\"detail\": {\"code\": 20, \"message\": \"request_cooldown\"}, \"user\": " + Sara + "}";
            var result = FriendsProtocol.ParseAction(409, body);
            Assert.IsNotNull(result);
            Assert.AreEqual(FriendStatus.RequestCooldown, result.Status);
            Assert.AreEqual("request_cooldown", result.detail.message);
            Assert.AreEqual(409, result.HttpStatus);
            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual("LC88VN3T", result.user.public_id);
            Assert.IsNull(result.invite);
            Assert.IsNull(result.RetryAfterSeconds);
            Assert.AreEqual(FriendStatus.RequestCooldown, FriendsProtocol.StatusOf(body));
        }

        [Test]
        public void SocializationRestricted_IsARefusalNotAFeatureFailure()
        {
            // A 403 in the code shape is an answer; the feature being off ({"detail": "text"}) is not.
            var result = FriendsProtocol.ParseAction(403,
                "{\"detail\": {\"code\": 19, \"message\": \"socialization_restricted\"}, \"user\": " + Sara + "}");
            Assert.IsNotNull(result);
            Assert.AreEqual(FriendStatus.SocializationRestricted, result.Status);
            Assert.AreEqual(403, result.HttpStatus);
            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual("LC88VN3T", result.user.public_id);
        }

        // Errors keep the code shape: a wrong method (405), malformed JSON (400) and a server error (500).
        [TestCase(405, 16, "invalid_request", FriendStatus.InvalidRequest)]
        [TestCase(400, 16, "invalid_request", FriendStatus.InvalidRequest)]
        [TestCase(500, 5, "friendship_error", FriendStatus.FriendshipError)]
        // Being blocked by enough players pauses new friendships: a refusal, like the other limits.
        [TestCase(409, 19, "socialization_restricted", FriendStatus.SocializationRestricted)]
        public void ErrorsInTheCodeShapeAreAnswers(long http, int code, string message, FriendStatus status)
        {
            var body = $"{{\"detail\": {{\"code\": {code}, \"message\": \"{message}\"}}}}";
            var result = FriendsProtocol.ParseAction(http, body);
            Assert.AreEqual(status, result.Status);
            Assert.AreEqual(message, result.detail.message);
            Assert.AreEqual(http, result.HttpStatus);
            Assert.IsFalse(result.Succeeded);
            Assert.IsNull(result.user);
            Assert.IsNull(result.invite);
            Assert.AreEqual(status, FriendsProtocol.StatusOf(body));
        }

        [Test]
        public void AlreadyFriendsIsASuccess()
        {
            // Retrying a request that already went through: done on an earlier try.
            var result = FriendsProtocol.ParseAction(200,
                "{\"detail\": {\"code\": 0, \"message\": \"friendship_exists\"}, \"user\": " + Sara + "}");
            Assert.AreEqual(FriendStatus.FriendshipExists, result.Status);
            Assert.IsTrue(result.Succeeded);
        }

        [Test]
        public void Throttled_CarriesRetryAfter()
        {
            var result = FriendsProtocol.ParseAction(429, "{\"detail\": {\"code\": 6, \"message\": \"throttled\"}}", "42");
            Assert.AreEqual(FriendStatus.Throttled, result.Status);
            Assert.AreEqual(429, result.HttpStatus);
            Assert.IsFalse(result.Succeeded);
            Assert.IsNull(result.user);
            Assert.AreEqual(42, result.RetryAfterSeconds);
            Assert.IsNull(FriendsProtocol.ParseAction(429, "{\"detail\": {\"code\": 6}}", "soon").RetryAfterSeconds);
            Assert.IsNull(FriendsProtocol.ParseAction(429, "{\"detail\": {\"code\": 6}}").RetryAfterSeconds);
        }

        [TestCase("42", 42)]
        [TestCase("0", 0)]
        [TestCase(" 7 ", 7)]
        [TestCase("-5", null)]
        [TestCase("+5", null)]
        [TestCase("1.5", null)]
        [TestCase("soon", null)]
        [TestCase("Wed, 21 Oct 2026 07:28:00 GMT", null)]
        [TestCase("", null)]
        [TestCase(null, null)]
        public void RetryAfterReadsWholeSecondsOnly(string header, int? expected)
        {
            Assert.AreEqual(expected, FriendsProtocol.RetryAfter(header));
        }

        [TestCase(401, "{\"detail\": \"Authentication credentials were not provided.\"}")]
        [TestCase(403, "{\"detail\": \"This API is not available for you\"}")]
        [TestCase(404, "{\"detail\": \"User not found\"}")]
        [TestCase(429, "<html>Too many requests</html>")]
        [TestCase(502, "<html>Bad gateway</html>")]
        [TestCase(0, null)]
        [TestCase(200, "")]
        [TestCase(200, null)]
        [TestCase(200, "[]")]
        [TestCase(200, "{\"detail\": [1, 2]}")]
        [TestCase(200, "{\"detail\": {\"code\": 7")]
        public void BodiesThatAreNotFriendAnswersAreNotParsed(long status, string body)
        {
            Assert.IsNull(FriendsProtocol.ParseAction(status, body));
            Assert.IsNull(FriendsProtocol.StatusOf(body));
        }

        [Test]
        public void AnUnknownCodeFromANewerServerStillParses()
        {
            var result = FriendsProtocol.ParseAction(200, "{\"detail\": {\"code\": 99, \"message\": \"something_new\"}}");
            Assert.AreEqual(99, (int)result.Status);
            Assert.AreEqual("something_new", result.detail.message);
            Assert.IsTrue(result.Succeeded);

            var refused = FriendsProtocol.ParseAction(409, "{\"detail\": {\"code\": 99, \"message\": \"something_new\"}}");
            Assert.AreEqual(99, (int)refused.Status);
            Assert.IsFalse(refused.Succeeded);
        }

        [Test]
        public void NewFieldsFromANewerServerAreIgnored()
        {
            var result = FriendsProtocol.ParseAction(200,
                "{\"detail\": {\"code\": 7, \"message\": \"request_sent\", \"hint\": \"x\"}, \"user\": " + Sara +
                ", \"something_new\": {\"a\": 1}}");
            Assert.AreEqual(FriendStatus.RequestSent, result.Status);
            Assert.AreEqual("LC88VN3T", result.user.public_id);
        }

        [Test]
        public void List_ReadsUsersAndCounts()
        {
            var list = FriendsProtocol.ParseList(200,
                "{\"detail\": {\"code\": 17, \"message\": \"friends_listed\"}, \"list\": \"incoming\", \"users\": [" +
                Sara.TrimEnd('}') + ", \"since\": \"2026-09-28T12:53:38.512000+00:00\"}], " +
                "\"counts\": {\"friends\": 2, \"incoming\": 1, \"outgoing\": 0, \"blocked\": 3}}");
            Assert.AreEqual("incoming", list.list);
            Assert.AreEqual(1, list.users.Count);
            Assert.AreEqual("Sara", list.users[0].name);
            Assert.AreEqual("2026-09-28T12:53:38.512000+00:00", list.users[0].since);
            Assert.AreEqual(2, list.counts.friends);
            Assert.AreEqual(3, list.counts.blocked);
        }

        [Test]
        public void List_FailuresAndMissingPartsAreSafe()
        {
            Assert.IsNull(FriendsProtocol.ParseList(400, "{\"detail\": {\"code\": 16, \"message\": \"invalid_request\"}}"));
            // A throttled read is not a list: the call throws TooManyRequests with the Retry-After.
            Assert.IsNull(FriendsProtocol.ParseList(429, "{\"detail\": {\"code\": 6, \"message\": \"throttled\"}}"));
            Assert.IsNull(FriendsProtocol.ParseList(404, "{\"detail\": \"User not found\"}"));
            Assert.IsNull(FriendsProtocol.ParseList(200, null));
            Assert.IsNull(FriendsProtocol.ParseList(200, "{\"users\": []}"));
            var bare = FriendsProtocol.ParseList(200, "{\"detail\": {\"code\": 17, \"message\": \"friends_listed\"}}");
            Assert.IsNotNull(bare.users);
            Assert.AreEqual(0, bare.users.Count);
            Assert.IsNotNull(bare.counts);
            var nulls = FriendsProtocol.ParseList(200,
                "{\"detail\": {\"code\": 17, \"message\": \"friends_listed\"}, \"users\": null, \"counts\": null}");
            Assert.IsNotNull(nulls.users);
            Assert.AreEqual(0, nulls.counts.friends);
        }

        [TestCase(FriendListKind.Friends, "friends")]
        [TestCase(FriendListKind.Incoming, "incoming")]
        [TestCase(FriendListKind.Outgoing, "outgoing")]
        [TestCase(FriendListKind.Blocked, "blocked")]
        public void ListQueriesMatchTheServer(FriendListKind kind, string query)
        {
            Assert.AreEqual(query, FriendsProtocol.ListQuery(kind));
        }

        [Test]
        public void ActionPathsAndBodiesMatchTheServer()
        {
            Assert.AreEqual("socialization/friend-requests/request/", FriendsProtocol.ActionPath(FriendsProtocol.Request));
            foreach (var action in new[] { "request", "accept", "decline", "cancel", "remove", "block", "unblock" })
                Assert.AreEqual($"socialization/friend-requests/{action}/", FriendsProtocol.ActionPath(action));
            Assert.AreEqual("abc", (string)JObject.Parse(FriendsProtocol.ByUuid("abc"))["uuid"]);
            // Escaped, never concatenated: whatever a player types stays one string.
            Assert.AreEqual("a\"b", (string)JObject.Parse(FriendsProtocol.ByPublicId("a\"b"))["public_id"]);
        }

        [Test]
        public void StatusCodesMatchTheServer()
        {
            // socialization/return_codes.py; shipped games compare the numbers.
            var expected = new[]
            {
                "friendship_exists", "friendship_created", "friendship_deleted", "friend_not_found",
                "friendship_illegal_self", "friendship_error", "throttled", "request_sent", "request_declined",
                "request_cancelled", "request_not_found", "user_blocked", "user_unblocked", "friend_blocked",
                "friend_limit_reached", "request_limit_reached", "invalid_request", "friends_listed",
                "block_limit_reached", "socialization_restricted", "request_cooldown",
            };
            Assert.AreEqual(expected.Length, Enum.GetValues(typeof(FriendStatus)).Length);
            for (var code = 0; code < expected.Length; code++)
            {
                var name = Enum.GetName(typeof(FriendStatus), code);
                Assert.AreEqual(expected[code].Replace("_", ""), name.ToLowerInvariant(), $"code {code}");
            }
        }
    }
}
