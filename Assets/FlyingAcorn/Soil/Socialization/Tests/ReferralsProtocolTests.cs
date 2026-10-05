using System;
using FlyingAcorn.Soil.Socialization.Logic;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace FlyingAcorn.Soil.Socialization.Tests
{
    // Bodies are copied from the server's answers (socialization/views_referrals.py, and the invite block of
    // FriendRequestsActionView in socialization/views_requests.py).
    public class ReferralsProtocolTests
    {
        private const string Sara =
            "{\"uuid\": \"7d829b62-b897-4984-ba1a-bb88948bc980\", \"public_id\": \"LC88VN3T\", \"name\": \"Sara\", " +
            "\"username\": \"sara\", \"avatar_asset\": null}";

        private const string Info = "{\"detail\": {\"code\": 9, \"message\": \"referral_info\"}, ";

        [Test]
        public void Info_ReadsEveryField()
        {
            var info = ReferralsProtocol.ParseInfo(200, Info +
                "\"invited\": true, \"invited_by\": " + Sara + ", \"can_redeem\": false, " +
                "\"redeem_until\": \"2026-10-12T09:30:00.123456+00:00\", \"invited_count\": 3}");
            Assert.AreEqual(ReferralStatus.ReferralInfo, info.detail.Status);
            Assert.AreEqual("referral_info", info.detail.message);
            Assert.IsTrue(info.invited);
            Assert.AreEqual("7d829b62-b897-4984-ba1a-bb88948bc980", info.invited_by.uuid);
            Assert.AreEqual("LC88VN3T", info.invited_by.public_id);
            Assert.AreEqual("Sara", info.invited_by.name);
            Assert.AreEqual("sara", info.invited_by.username);
            Assert.IsNull(info.invited_by.avatar_asset);
            Assert.IsFalse(info.can_redeem);
            // Kept as the server wrote it, not reformatted in the device's culture and time zone.
            Assert.AreEqual("2026-10-12T09:30:00.123456+00:00", info.redeem_until);
            Assert.AreEqual(3, info.invited_count);
        }

        [Test]
        public void Info_RedeemUntilReadsAsUtc()
        {
            var info = ReferralsProtocol.ParseInfo(200, Info +
                "\"invited\": false, \"invited_by\": null, \"can_redeem\": true, " +
                "\"redeem_until\": \"2026-10-12T09:30:00.123456+00:00\", \"invited_count\": 0}");
            var until = info.RedeemUntilUtc.Value;
            Assert.AreEqual(DateTimeKind.Utc, until.Kind);
            Assert.AreEqual(new DateTime(2026, 10, 12, 9, 30, 0, DateTimeKind.Utc).AddTicks(1234560), until);

            info.redeem_until = "2026-10-12T13:00:00+03:30";
            Assert.AreEqual(new DateTime(2026, 10, 12, 9, 30, 0, DateTimeKind.Utc), info.RedeemUntilUtc);
        }

        [Test]
        public void Info_NotInvitedAndNoWindow()
        {
            var info = ReferralsProtocol.ParseInfo(200, Info +
                "\"invited\": false, \"invited_by\": null, \"can_redeem\": true, \"redeem_until\": null, " +
                "\"invited_count\": 0}");
            Assert.IsFalse(info.invited);
            Assert.IsNull(info.invited_by);
            Assert.IsTrue(info.can_redeem);
            Assert.IsNull(info.redeem_until);
            Assert.IsNull(info.RedeemUntilUtc);
            Assert.AreEqual(0, info.invited_count);
        }

        [Test]
        public void Info_InvitedByADeletedAccount()
        {
            var info = ReferralsProtocol.ParseInfo(200, Info +
                "\"invited\": true, \"invited_by\": null, \"can_redeem\": false, \"redeem_until\": null, " +
                "\"invited_count\": 12}");
            Assert.IsTrue(info.invited);
            Assert.IsNull(info.invited_by);
            Assert.IsFalse(info.can_redeem);
            Assert.AreEqual(12, info.invited_count);
        }

        [TestCase(429, "{\"detail\": {\"code\": 8, \"message\": \"throttled\"}}")]
        [TestCase(500, "{\"detail\": {\"code\": 7, \"message\": \"referral_error\"}}")]
        [TestCase(401, "{\"detail\": \"Authentication credentials were not provided.\"}")]
        [TestCase(403, "{\"detail\": \"This API is not available for you\"}")]
        [TestCase(404, "{\"detail\": \"User not found\"}")]
        [TestCase(502, "<html>Bad gateway</html>")]
        [TestCase(200, "")]
        [TestCase(200, "[]")]
        [TestCase(200, "{\"invited\": true}")]
        public void Info_OnlySuccessfulAnswersParse(long status, string body)
        {
            Assert.IsNull(ReferralsProtocol.ParseInfo(status, body));
        }

        [Test]
        public void Redeem_InvitedCarriesTheRewardAndTheInviter()
        {
            var result = ReferralsProtocol.ParseRedeem(200,
                "{\"detail\": {\"code\": 0, \"message\": \"invited\"}, " +
                "\"reward\": {\"currency\": \"ReferralGem\", \"amount\": 50}, \"inviter\": " + Sara + "}");
            Assert.AreEqual(ReferralStatus.Invited, result.Status);
            Assert.AreEqual("invited", result.detail.message);
            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(200, result.HttpStatus);
            Assert.IsNull(result.RetryAfterSeconds);
            Assert.AreEqual("ReferralGem", result.reward.currency);
            Assert.AreEqual(50, result.reward.amount);
            Assert.AreEqual("LC88VN3T", result.inviter.public_id);
            Assert.AreEqual("7d829b62-b897-4984-ba1a-bb88948bc980", result.inviter.uuid);
            Assert.AreEqual("Sara", result.inviter.name);
            Assert.AreEqual("sara", result.inviter.username);
        }

        [TestCase(200, 0, true)]
        [TestCase(200, 3, false)]
        [TestCase(409, 3, false)]
        [TestCase(409, 0, true)]
        public void Redeem_SucceededFollowsTheCodeNotTheHttpStatus(long http, int code, bool succeeded)
        {
            // Unlike FriendActionResult, an invite succeeded when its code says Invited. A retry of the same code
            // answers Invited again (with the first reward), so it reads as a success too.
            var result = ReferralsProtocol.ParseRedeem(http, $"{{\"detail\": {{\"code\": {code}, \"message\": \"x\"}}}}");
            Assert.AreEqual(succeeded, result.Succeeded);
            Assert.AreEqual(http, result.HttpStatus);
        }

        [Test]
        public void Redeem_TheLargestRewardFitsAnInt()
        {
            // The server caps each side's amount at 2^31 - 1.
            var result = ReferralsProtocol.ParseRedeem(200,
                "{\"detail\": {\"code\": 0, \"message\": \"invited\"}, " +
                "\"reward\": {\"currency\": \"gem\", \"amount\": 2147483647}, \"inviter\": " + Sara + "}");
            Assert.AreEqual(int.MaxValue, result.reward.amount);
        }

        [Test]
        public void Redeem_InvitedWithoutAReward()
        {
            var result = ReferralsProtocol.ParseRedeem(200,
                "{\"detail\": {\"code\": 0, \"message\": \"invited\"}, \"reward\": null, \"inviter\": " + Sara + "}");
            Assert.IsTrue(result.Succeeded);
            Assert.IsNull(result.reward);
            Assert.IsNotNull(result.inviter);
        }

        [TestCase(404, 1, "code_not_found", ReferralStatus.CodeNotFound)]
        [TestCase(400, 2, "own_code", ReferralStatus.OwnCode)]
        [TestCase(409, 3, "already_invited", ReferralStatus.AlreadyInvited)]
        [TestCase(409, 4, "window_closed", ReferralStatus.WindowClosed)]
        [TestCase(409, 5, "mutual_invite", ReferralStatus.MutualInvite)]
        [TestCase(400, 6, "invalid_request", ReferralStatus.InvalidRequest)] // also malformed JSON
        [TestCase(405, 6, "invalid_request", ReferralStatus.InvalidRequest)] // a wrong method
        [TestCase(500, 7, "referral_error", ReferralStatus.ReferralError)]
        public void Redeem_RefusalsAreAnswersNotFailures(long http, int code, string message, ReferralStatus status)
        {
            var body = $"{{\"detail\": {{\"code\": {code}, \"message\": \"{message}\"}}}}";
            var result = ReferralsProtocol.ParseRedeem(http, body);
            Assert.AreEqual(status, result.Status);
            Assert.AreEqual(message, result.detail.message);
            Assert.AreEqual(http, result.HttpStatus);
            Assert.IsFalse(result.Succeeded);
            Assert.IsNull(result.reward);
            Assert.IsNull(result.inviter);
            Assert.AreEqual(status, ReferralsProtocol.StatusOf(body));
        }

        [Test]
        public void Redeem_ThrottledCarriesRetryAfter()
        {
            var body = "{\"detail\": {\"code\": 8, \"message\": \"throttled\"}}";
            var result = ReferralsProtocol.ParseRedeem(429, body, "17");
            Assert.AreEqual(ReferralStatus.Throttled, result.Status);
            Assert.IsFalse(result.Succeeded);
            Assert.AreEqual(17, result.RetryAfterSeconds);
            Assert.IsNull(ReferralsProtocol.ParseRedeem(429, body, "soon").RetryAfterSeconds);
            Assert.IsNull(ReferralsProtocol.ParseRedeem(429, body).RetryAfterSeconds);
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
        [TestCase(200, "{\"detail\": {\"code\": 0")]
        public void Redeem_BodiesThatAreNotReferralAnswersAreNotParsed(long status, string body)
        {
            Assert.IsNull(ReferralsProtocol.ParseRedeem(status, body));
            Assert.IsNull(ReferralsProtocol.StatusOf(body));
        }

        [Test]
        public void AnUnknownCodeFromANewerServerStillParses()
        {
            var result = ReferralsProtocol.ParseRedeem(409, "{\"detail\": {\"code\": 42, \"message\": \"something_new\"}}");
            Assert.AreEqual(42, (int)result.Status);
            Assert.AreEqual("something_new", result.detail.message);
            Assert.IsFalse(result.Succeeded);
        }

        [Test]
        public void PathsAndBodyMatchTheServer()
        {
            Assert.AreEqual("socialization/referrals/", ReferralsProtocol.BasePath);
            Assert.AreEqual("socialization/referrals/redeem/", ReferralsProtocol.RedeemPath);
            var body = JObject.Parse(ReferralsProtocol.RedeemBody("LC88VN3T"));
            Assert.AreEqual("LC88VN3T", (string)body["code"]);
            Assert.AreEqual(1, body.Count);
            // Escaped, never concatenated: whatever a player types stays one string.
            Assert.AreEqual("a\"b", (string)JObject.Parse(ReferralsProtocol.RedeemBody("a\"b"))["code"]);
        }

        [Test]
        public void StatusCodesMatchTheServer()
        {
            // REFERRAL_CODES in socialization/return_codes.py; shipped games compare the numbers.
            var expected = new[]
            {
                "invited", "code_not_found", "own_code", "already_invited", "window_closed", "mutual_invite",
                "invalid_request", "referral_error", "throttled", "referral_info",
            };
            Assert.AreEqual(expected.Length, Enum.GetValues(typeof(ReferralStatus)).Length);
            for (var code = 0; code < expected.Length; code++)
            {
                var name = Enum.GetName(typeof(ReferralStatus), code);
                Assert.AreEqual(expected[code].Replace("_", ""), name.ToLowerInvariant(), $"code {code}");
            }
        }

        [Test]
        public void FriendRequest_CarriesTheInviteWhenItCounted()
        {
            var result = FriendsProtocol.ParseAction(200,
                "{\"detail\": {\"code\": 7, \"message\": \"request_sent\"}, \"user\": " + Sara + ", " +
                "\"invite\": {\"detail\": {\"code\": 0, \"message\": \"invited\"}, " +
                "\"reward\": {\"currency\": \"ReferralGem\", \"amount\": 25}}}");
            Assert.AreEqual(FriendStatus.RequestSent, result.Status);
            Assert.AreEqual("LC88VN3T", result.user.public_id);
            Assert.IsNotNull(result.invite);
            Assert.AreEqual(ReferralStatus.Invited, result.invite.Status);
            Assert.AreEqual("invited", result.invite.detail.message);
            Assert.IsTrue(result.invite.Succeeded);
            Assert.AreEqual("ReferralGem", result.invite.reward.currency);
            Assert.AreEqual(25, result.invite.reward.amount);
        }

        [Test]
        public void FriendRequest_TheInviteAndTheRequestStandOnTheirOwn()
        {
            // The server now leaves out an invite that was refused; one from an older server still parses, as a
            // refusal, and the request went through regardless.
            var sent = FriendsProtocol.ParseAction(200,
                "{\"detail\": {\"code\": 7, \"message\": \"request_sent\"}, \"user\": " + Sara + ", " +
                "\"invite\": {\"detail\": {\"code\": 3, \"message\": \"already_invited\"}, \"reward\": null}}");
            Assert.IsTrue(sent.Succeeded);
            Assert.AreEqual(ReferralStatus.AlreadyInvited, sent.invite.Status);
            Assert.IsFalse(sent.invite.Succeeded);
            Assert.IsNull(sent.invite.reward);

            // The invite counted, with no reward for this side, though the request was refused.
            var limit = FriendsProtocol.ParseAction(409,
                "{\"detail\": {\"code\": 15, \"message\": \"request_limit_reached\"}, \"user\": " + Sara + ", " +
                "\"invite\": {\"detail\": {\"code\": 0, \"message\": \"invited\"}, \"reward\": null}}");
            Assert.IsFalse(limit.Succeeded);
            Assert.AreEqual(FriendStatus.RequestLimitReached, limit.Status);
            Assert.IsTrue(limit.invite.Succeeded);
            Assert.IsNull(limit.invite.reward);
        }

        [Test]
        public void FriendRequest_WithoutTheInviteHasNone()
        {
            // No invite made: already invited (even by this player), outside the window, a stopped code, sent by
            // UUID, or the switch off.
            var result = FriendsProtocol.ParseAction(200,
                "{\"detail\": {\"code\": 7, \"message\": \"request_sent\"}, \"user\": " + Sara + "}");
            Assert.IsNull(result.invite);

            // A code that matches nobody: no player, and no invite.
            var missing = FriendsProtocol.ParseAction(404, "{\"detail\": {\"code\": 3, \"message\": \"friend_not_found\"}}");
            Assert.IsNull(missing.user);
            Assert.IsNull(missing.invite);
        }

        [Test]
        public void FriendRequest_AnInviteThatFailedStillSendsTheRequest()
        {
            var result = FriendsProtocol.ParseAction(200,
                "{\"detail\": {\"code\": 7, \"message\": \"request_sent\"}, \"user\": " + Sara + ", " +
                "\"invite\": {\"detail\": {\"code\": 7, \"message\": \"referral_error\"}, \"reward\": null}}");
            Assert.IsTrue(result.Succeeded);
            Assert.AreEqual(FriendStatus.RequestSent, result.Status);
            Assert.AreEqual(ReferralStatus.ReferralError, result.invite.Status);
            Assert.IsFalse(result.invite.Succeeded);
            Assert.IsNull(result.invite.reward);
        }

        [Test]
        public void FriendRequest_AnInviteWithoutADetailIsAnError()
        {
            var result = FriendsProtocol.ParseAction(200,
                "{\"detail\": {\"code\": 7, \"message\": \"request_sent\"}, \"invite\": {\"reward\": null}}");
            Assert.AreEqual(ReferralStatus.ReferralError, result.invite.Status);
            Assert.IsFalse(result.invite.Succeeded);
        }
    }
}
