using System.Collections.Generic;
using System.Linq;
using FlyingAcorn.Soil.Push.Logic;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace FlyingAcorn.Soil.Push.Tests
{
    /// <summary>Bodies are copied from the server's answers (push/views.py) and FCM's data payload (sender.py).</summary>
    public class PushProtocolTests
    {
        private const string Registered = "{\"detail\":{\"code\":0,\"message\":\"registered\"},\"push_enabled\":true}";
        private const long Now = 1_800_000_000;

        [Test]
        public void Registered_ParsesWhetherPushIsOn()
        {
            var result = PushProtocol.ParseRegister(200, Registered);
            Assert.IsTrue(result.Succeeded);
            Assert.IsTrue(result.push_enabled);
            var off = PushProtocol.ParseRegister(200, Registered.Replace("true", "false"));
            Assert.IsTrue(off.Succeeded);
            Assert.IsFalse(off.push_enabled);
        }

        [TestCase(400, "{\"detail\":{\"code\":2,\"message\":\"invalid_request\"}}", PushStatus.InvalidRequest)]
        [TestCase(429, "{\"detail\":{\"code\":3,\"message\":\"throttled\"}}", PushStatus.Throttled)]
        [TestCase(500, "{\"detail\":{\"code\":4,\"message\":\"push_error\"}}", PushStatus.PushError)]
        public void Refusals_AreAnswersNotSuccesses(long status, string body, PushStatus expected)
        {
            var result = PushProtocol.ParseRegister(status, body);
            Assert.AreEqual(expected, result.Status);
            Assert.IsFalse(result.Succeeded);
        }

        [Test]
        public void CodesMatchTheServer()
        {
            Assert.AreEqual(0, (int)PushStatus.Registered);
            Assert.AreEqual(1, (int)PushStatus.Unregistered);
            Assert.AreEqual(2, (int)PushStatus.InvalidRequest);
            Assert.AreEqual(3, (int)PushStatus.Throttled);
            Assert.AreEqual(4, (int)PushStatus.PushError);
        }

        [TestCase("{\"detail\":\"This API is not available for you\"}")]
        [TestCase("<html>502 Bad Gateway</html>")]
        [TestCase("")]
        [TestCase(null)]
        public void BodiesThatAreNotThePushApi_AreNull(string body)
        {
            Assert.IsNull(PushProtocol.ParseRegister(403, body));
            Assert.IsNull(PushProtocol.ParseStatus(body));
        }

        [Test]
        public void ANewerServersCode_IsKeptNotAnError()
        {
            var result = PushProtocol.ParseRegister(200, "{\"detail\":{\"code\":9,\"message\":\"something_new\"}}");
            Assert.AreEqual(9, (int)result.Status);
            Assert.IsFalse(result.Succeeded);
        }

        [Test]
        public void RegisterJson_LeavesOutWhatIsNotKnown()
        {
            Assert.AreEqual(new JObject { ["token"] = "t", ["platform"] = "Android", ["language"] = "Persian" },
                JObject.Parse(PushProtocol.RegisterJson("t", "Android", "Persian")));
            Assert.AreEqual(new JObject { ["token"] = "t" }, JObject.Parse(PushProtocol.RegisterJson("t", null, "")));
            Assert.AreEqual(new JObject { ["token"] = "t" }, JObject.Parse(PushProtocol.UnregisterJson("t")));
        }

        private static PushRegistrationRecord Record(string token = "t", string language = "fa", string user = "u",
            long at = Now) => new PushRegistrationRecord { token = token, language = language, user = user, at = at };

        [Test]
        public void ShouldRegister_OnlyWhenSomethingChangedOrAWeekPassed()
        {
            Assert.IsTrue(PushProtocol.ShouldRegister(null, "t", "fa", "u", Now), "never registered");
            Assert.IsFalse(PushProtocol.ShouldRegister(Record(), "t", "fa", "u", Now + 60), "nothing changed");
            Assert.IsTrue(PushProtocol.ShouldRegister(Record(), "t2", "fa", "u", Now), "new token");
            Assert.IsTrue(PushProtocol.ShouldRegister(Record(), "t", "en", "u", Now), "new language");
            Assert.IsTrue(PushProtocol.ShouldRegister(Record(), "t", "fa", "u2", Now), "another player");
            Assert.IsTrue(PushProtocol.ShouldRegister(Record(), "t", "fa", "u", Now + PushProtocol.RefreshSeconds),
                "a week later");
            Assert.IsTrue(PushProtocol.ShouldRegister(Record(at: Now + 3600), "t", "fa", "u", Now), "clock went back");
            Assert.IsFalse(PushProtocol.ShouldRegister(Record(language: null), "t", "", "u", Now), "null and empty");
        }

        [Test]
        public void Decide_OptedOutUnregistersOnceAndNeverRegistersAgain()
        {
            Assert.AreEqual(PushAction.Unregister, PushProtocol.Decide(Record(), "t", "fa", "u", Now, true, false));
            Assert.AreEqual(PushAction.None, PushProtocol.Decide(null, "t", "fa", "u", Now, true, false),
                "after the unregister succeeded the record is gone");
            Assert.AreEqual(PushAction.None, PushProtocol.Decide(null, "new-token", "fa", "u", Now, true, false),
                "a token reported again while opted out is not registered");
            Assert.AreEqual(PushAction.Register, PushProtocol.Decide(null, "t", "fa", "u", Now, false, false),
                "after Resume it registers again");
        }

        [Test]
        public void Decide_AGameWithoutTheFeatureDoesNothing()
        {
            Assert.AreEqual(PushAction.None, PushProtocol.Decide(null, "t", "fa", "u", Now, false, true));
            Assert.AreEqual(PushAction.Unregister, PushProtocol.Decide(Record(), "t", "fa", "u", Now, true, true),
                "opting out still forgets the token");
        }

        [Test]
        public void Decide_FollowsTheRegisterRules()
        {
            Assert.AreEqual(PushAction.None, PushProtocol.Decide(Record(), "t", "fa", "u", Now + 60, false, false));
            Assert.AreEqual(PushAction.Register, PushProtocol.Decide(Record(), "t", "fa", "u2", Now, false, false));
        }

        [Test]
        public void ShouldRegister_NeverWithoutATokenOrAPlayer()
        {
            Assert.IsFalse(PushProtocol.ShouldRegister(null, null, "fa", "u", Now));
            Assert.IsFalse(PushProtocol.ShouldRegister(null, "", "fa", "u", Now));
            Assert.IsFalse(PushProtocol.ShouldRegister(null, "t", "fa", null, Now));
        }

        [Test]
        public void FromData_ReadsASoilPush()
        {
            var data = new Dictionary<string, string>
            {
                ["soil"] = "1", ["kind"] = "friend_request", ["group_key"] = "fr:42", ["ref"] = "ABCD2345",
                ["message_id"] = "7",
            };
            var message = PushProtocol.FromData(data, "Friend request", "Ali sent you a friend request", true);
            Assert.IsTrue(message.IsSoil);
            Assert.AreEqual(PushKind.FriendRequest, message.Kind);
            Assert.AreEqual("fr:42", message.GroupKey);
            Assert.AreEqual("ABCD2345", message.Ref);
            Assert.AreEqual("Ali sent you a friend request", message.Body);
            Assert.IsTrue(message.Opened);
            Assert.AreEqual("7", message.Data["message_id"]);
            data["kind"] = "changed later";
            Assert.AreEqual("friend_request", message.Data["kind"], "the data is a copy");
        }

        [TestCase("friend_request", PushKind.FriendRequest)]
        [TestCase("friend_accepted", PushKind.FriendAccepted)]
        [TestCase("leaderboard_prize", PushKind.LeaderboardPrize)]
        [TestCase("referral_reward", PushKind.ReferralReward)]
        [TestCase("test", PushKind.Test)]
        [TestCase("season_ended", PushKind.Unknown)]
        [TestCase(null, PushKind.Unknown)]
        public void Kinds_MapAndAFutureKindIsUnknown(string kind, PushKind expected)
        {
            var message = PushProtocol.FromData(new Dictionary<string, string> { ["soil"] = "1", ["kind"] = kind },
                null, null, false);
            Assert.AreEqual(expected, message.Kind);
            Assert.AreEqual(kind, message.KindName);
        }

        [Test]
        public void FromData_APushFromSomethingElseIsNotSoils()
        {
            var message = PushProtocol.FromData(new Dictionary<string, string> { ["kind"] = "friend_request" },
                "Sale", "50% off", false);
            Assert.IsFalse(message.IsSoil);
            Assert.AreEqual(PushKind.Unknown, message.Kind);
            Assert.IsNotNull(PushProtocol.FromData(null, null, null, false).Data);
        }

        [Test]
        public void Groups_MatchTheServersNamesAndChannels()
        {
            Assert.AreEqual(new[] { "friends", "rewards" }, PushProtocol.Groups.Select(PushProtocol.GroupName).ToArray());
            Assert.AreEqual("soil_friends", PushProtocol.Channel(PushGroup.Friends, "fa").Id);
            Assert.AreEqual("soil_rewards", PushProtocol.Channel(PushGroup.Rewards, "en").Id);
            // Friends never interrupt; rewards pop up on top.
            Assert.AreEqual(PushProtocol.ImportanceDefault, PushProtocol.Channel(PushGroup.Friends, "en").Importance);
            Assert.AreEqual(PushProtocol.ImportanceHigh, PushProtocol.Channel(PushGroup.Rewards, "fa").Importance);
        }

        [TestCase("fa", "دوستان")]
        [TestCase("Persian", "دوستان")]
        [TestCase("fa-IR", "دوستان")]
        [TestCase("English", "Friends")]
        [TestCase("", "Friends")]
        [TestCase(null, "Friends")]
        public void Channels_AreNamedInTheGamesLanguage(string language, string friends)
        {
            Assert.AreEqual(friends, PushProtocol.Channel(PushGroup.Friends, language).Name);
        }

        [Test]
        public void Muted_KeepsOnlyKnownGroupsInAFixedOrder()
        {
            Assert.AreEqual("friends,rewards", PushProtocol.MutedText(new[] { PushGroup.Rewards, PushGroup.Friends }));
            Assert.AreEqual("", PushProtocol.MutedText(null));
            Assert.AreEqual(new[] { PushGroup.Rewards }, PushProtocol.ParseMuted("rewards,ads,").ToArray());
            Assert.IsTrue(PushProtocol.AllMuted("friends,rewards"));
            Assert.IsFalse(PushProtocol.AllMuted("friends"));
            Assert.IsFalse(PushProtocol.AllMuted(null));
        }

        [Test]
        public void RegisterJson_SendsMutedEvenWhenEmpty()
        {
            Assert.AreEqual(new JArray("friends"), JObject.Parse(PushProtocol.RegisterJson("t", null, "", "friends"))["muted"]);
            // Turning a group back on must reach the server.
            Assert.AreEqual(new JArray(), JObject.Parse(PushProtocol.RegisterJson("t", null, "", ""))["muted"]);
        }

        [Test]
        public void ShouldRegister_WhenTheMutedGroupsChange()
        {
            var record = new PushRegistrationRecord { token = "t", language = "fa", user = "u", at = Now };
            Assert.IsFalse(PushProtocol.ShouldRegister(record, "t", "fa", "u", Now, ""), "a record from before groups");
            Assert.IsTrue(PushProtocol.ShouldRegister(record, "t", "fa", "u", Now, "friends"));
            record.muted = "friends";
            Assert.IsFalse(PushProtocol.ShouldRegister(record, "t", "fa", "u", Now, "friends"));
            Assert.AreEqual(PushAction.Register, PushProtocol.Decide(record, "t", "fa", "u", Now, false, false, ""));
        }
    }

    public class PushHubTests
    {
        [SetUp]
        public void SetUp() => PushHub.Reset();

        [TearDown]
        public void TearDown() => PushHub.Reset();

        [Test]
        public void ALateSubscriberGetsTheLatestToken()
        {
            PushHub.ReportToken("first");
            PushHub.ReportToken("second");
            var seen = new List<string>();
            PushHub.TokenReceived += seen.Add;
            CollectionAssert.AreEqual(new[] { "second" }, seen);
            PushHub.ReportToken("second");
            PushHub.ReportToken("third");
            CollectionAssert.AreEqual(new[] { "second", "third" }, seen, "the same token again is not news");
        }

        [Test]
        public void EmptyTokensAreIgnored()
        {
            var seen = new List<string>();
            PushHub.TokenReceived += seen.Add;
            PushHub.ReportToken(null);
            PushHub.ReportToken("");
            Assert.IsEmpty(seen);
            Assert.IsNull(PushHub.Token);
        }

        [Test]
        public void APushThatOpenedTheGameBeforeAnyoneListenedIsKept()
        {
            var tap = new PushMessage { Kind = PushKind.LeaderboardPrize, Opened = true };
            PushHub.ReportMessage(tap);
            var seen = new List<PushMessage>();
            PushHub.MessageReceived += seen.Add;
            CollectionAssert.AreEqual(new[] { tap }, seen);
            var later = new List<PushMessage>();
            PushHub.MessageReceived += later.Add;
            Assert.IsEmpty(later, "delivered once, to the first subscriber");
        }

        [Test]
        public void TheWaitingListIsBounded()
        {
            for (var i = 0; i < 20; i++) PushHub.ReportMessage(new PushMessage { Ref = i.ToString() });
            var seen = new List<PushMessage>();
            PushHub.MessageReceived += seen.Add;
            Assert.AreEqual(5, seen.Count);
            Assert.AreEqual("19", seen[4].Ref);
        }

        [Test]
        public void TheFirebaseStartReachesALateListenerOnce()
        {
            var starts = 0;
            PushHub.RequestFirebaseStart();
            PushHub.RequestFirebaseStart();
            PushHub.FirebaseStartRequested += () => starts++;
            Assert.AreEqual(1, starts);
            PushHub.RequestFirebaseStart();
            Assert.AreEqual(1, starts, "asking again does nothing");
        }

        [Test]
        public void TheFirebaseStartWaitsForTheGame()
        {
            var starts = 0;
            PushHub.FirebaseStartRequested += () => starts++;
            Assert.AreEqual(0, starts, "the bridge never starts on its own");
            PushHub.RequestFirebaseStart();
            Assert.AreEqual(1, starts);
        }
    }
}
