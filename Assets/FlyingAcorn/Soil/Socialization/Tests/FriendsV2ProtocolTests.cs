using System;
using FlyingAcorn.Soil.Socialization.Logic;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace FlyingAcorn.Soil.Socialization.Tests
{
    // Bodies are copied from the server's answers (socialization/views_v2.py).
    public class FriendsV2ProtocolTests
    {
        private const string Sara =
            "{\"uuid\": \"7d829b62-b897-4984-ba1a-bb88948bc980\", \"public_id\": \"LC88VN3T\", \"name\": \"Sara\", " +
            "\"username\": \"sara\", \"avatar_asset\": null}";

        [Test]
        public void RequestSent_IsASuccessWithTheOtherPlayer()
        {
            var result = FriendsV2Protocol.ParseAction(200,
                "{\"detail\": {\"code\": 7, \"message\": \"request_sent\"}, \"user\": " + Sara + "}");
            Assert.AreEqual(FriendStatus.RequestSent, result.Status);
            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual("LC88VN3T", result.user.public_id);
            Assert.AreEqual("7d829b62-b897-4984-ba1a-bb88948bc980", result.user.uuid);
        }

        [Test]
        public void RefusalsAreAnswersNotFailures()
        {
            var limit = FriendsV2Protocol.ParseAction(409,
                "{\"detail\": {\"code\": 15, \"message\": \"request_limit_reached\"}, \"user\": " + Sara + "}");
            Assert.AreEqual(FriendStatus.RequestLimitReached, limit.Status);
            Assert.IsFalse(limit.Succeeded);
            Assert.IsNotNull(limit.user);

            var missing = FriendsV2Protocol.ParseAction(404, "{\"detail\": {\"code\": 3, \"message\": \"friend_not_found\"}}");
            Assert.AreEqual(FriendStatus.FriendNotFound, missing.Status);
            Assert.IsNull(missing.user);
        }

        [Test]
        public void Throttled_CarriesRetryAfter()
        {
            var result = FriendsV2Protocol.ParseAction(429, "{\"detail\": {\"code\": 6, \"message\": \"throttled\"}}", "42");
            Assert.AreEqual(FriendStatus.Throttled, result.Status);
            Assert.AreEqual(42, result.RetryAfterSeconds);
            Assert.IsNull(FriendsV2Protocol.ParseAction(429, "{\"detail\": {\"code\": 6}}", "soon").RetryAfterSeconds);
        }

        [TestCase(401, "{\"detail\": \"Authentication credentials were not provided.\"}")]
        [TestCase(403, "{\"detail\": \"This API is not available for you\"}")]
        [TestCase(502, "<html>Bad gateway</html>")]
        [TestCase(200, "")]
        [TestCase(200, "[]")]
        public void BodiesThatAreNotFriendAnswersAreNotParsed(long status, string body)
        {
            Assert.IsNull(FriendsV2Protocol.ParseAction(status, body));
            Assert.IsNull(FriendsV2Protocol.StatusOf(body));
        }

        [Test]
        public void AnUnknownCodeFromANewerServerStillParses()
        {
            var result = FriendsV2Protocol.ParseAction(200, "{\"detail\": {\"code\": 99, \"message\": \"something_new\"}}");
            Assert.AreEqual(99, (int)result.Status);
            Assert.AreEqual("something_new", result.detail.message);
        }

        [Test]
        public void List_ReadsUsersAndCounts()
        {
            var list = FriendsV2Protocol.ParseList(200,
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
            Assert.IsNull(FriendsV2Protocol.ParseList(400, "{\"detail\": {\"code\": 16, \"message\": \"invalid_request\"}}"));
            var bare = FriendsV2Protocol.ParseList(200, "{\"detail\": {\"code\": 17, \"message\": \"friends_listed\"}}");
            Assert.IsNotNull(bare.users);
            Assert.IsNotNull(bare.counts);
        }

        [TestCase(FriendListKind.Friends, "friends")]
        [TestCase(FriendListKind.Incoming, "incoming")]
        [TestCase(FriendListKind.Outgoing, "outgoing")]
        [TestCase(FriendListKind.Blocked, "blocked")]
        public void ListQueriesMatchTheServer(FriendListKind kind, string query)
        {
            Assert.AreEqual(query, FriendsV2Protocol.ListQuery(kind));
        }

        [Test]
        public void ActionPathsAndBodiesMatchTheServer()
        {
            Assert.AreEqual("socialization/v2/friends/request/", FriendsV2Protocol.ActionPath(FriendsV2Protocol.Request));
            foreach (var action in new[] { "request", "accept", "decline", "cancel", "remove", "block", "unblock" })
                Assert.AreEqual($"socialization/v2/friends/{action}/", FriendsV2Protocol.ActionPath(action));
            Assert.AreEqual("abc", (string)JObject.Parse(FriendsV2Protocol.ByUuid("abc"))["uuid"]);
            // Escaped, never concatenated: whatever a player types stays one string.
            Assert.AreEqual("a\"b", (string)JObject.Parse(FriendsV2Protocol.ByPublicId("a\"b"))["public_id"]);
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
                "block_limit_reached",
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
