using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FlyingAcorn.Soil.Push.Logic
{
    /// <summary>
    /// Every <c>detail.code</c> the push API answers with (push/return_codes.py on the server). The server only
    /// ever appends, so an unknown number is a newer server, not an error.
    /// </summary>
    public enum PushStatus
    {
        Registered = 0,
        Unregistered = 1,
        InvalidRequest = 2,
        Throttled = 3,
        PushError = 4,
    }

    /// <summary>What a Soil push is about. Unknown covers kinds a newer server sends.</summary>
    public enum PushKind
    {
        Unknown,
        /// <summary>Another player sent this player a friend request. Ref is their public id.</summary>
        FriendRequest,
        /// <summary>A player accepted this player's request. Ref is their public id.</summary>
        FriendAccepted,
        /// <summary>A leaderboard reset paid this player. Ref is the leaderboard identifier.</summary>
        LeaderboardPrize,
        /// <summary>This player was paid for invites. Ref is the last new player's public id.</summary>
        ReferralReward,
        /// <summary>Sent from the dashboard to check the setup.</summary>
        Test,
    }

    [Serializable]
    public class PushStatusDetail
    {
        public int code;
        public string message;

        [JsonIgnore] public PushStatus Status => (PushStatus)code;
    }

    /// <summary>The answer to registering a device.</summary>
    [Serializable]
    public class PushRegisterResult
    {
        public PushStatusDetail detail;
        /// <summary>Whether Soil is sending pushes right now. The token is kept either way.</summary>
        public bool push_enabled;

        [JsonIgnore] public PushStatus Status => detail.Status;
        [JsonIgnore] public bool Succeeded => detail.Status == PushStatus.Registered;
    }

    /// <summary>A push as the game sees it, whether it came from Soil or from something else.</summary>
    public class PushMessage
    {
        /// <summary>True when Soil sent it (its data carries soil = 1).</summary>
        public bool IsSoil { get; set; }
        public PushKind Kind { get; set; }
        /// <summary>The kind as the server named it, for kinds this SDK does not know yet.</summary>
        public string KindName { get; set; }
        /// <summary>What it is about, like fr:123 for a friend request from player 123. One per piece of news.</summary>
        public string GroupKey { get; set; }
        /// <summary>The other player's public id, or the leaderboard identifier.</summary>
        public string Ref { get; set; }
        public string Title { get; set; }
        public string Body { get; set; }
        /// <summary>True when the player tapped the notification to open the game.</summary>
        public bool Opened { get; set; }
        public IReadOnlyDictionary<string, string> Data { get; set; } = new Dictionary<string, string>();
    }

    /// <summary>What the device should do about its registration right now.</summary>
    public enum PushAction
    {
        None,
        Register,
        /// <summary>The player turned pushes off on this device: tell Soil to forget the registered token.</summary>
        Unregister,
    }

    /// <summary>
    /// The kinds a player can turn off on one device, in the game's notification settings. Each is also the Android
    /// notification channel its pushes land in (push/models.py GROUPS and ANDROID_CHANNELS on the server).
    /// </summary>
    public enum PushGroup
    {
        /// <summary>Friend requests received and accepted. Sound and the tray, no pop-up.</summary>
        Friends,
        /// <summary>Leaderboard prizes and invite rewards. Pops up on top.</summary>
        Rewards,
    }

    /// <summary>An Android notification channel the SDK creates for one group.</summary>
    public class PushChannel
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        /// <summary>Android's NotificationManager importance. Fixed by Android once a channel exists on a phone.</summary>
        public int Importance { get; set; }
    }

    /// <summary>What was last registered, to tell whether registering again is needed.</summary>
    [Serializable]
    public class PushRegistrationRecord
    {
        public string token;
        public string language;
        /// <summary>The groups turned off, as <see cref="PushProtocol.MutedText"/> writes them.</summary>
        public string muted;
        public string user;
        /// <summary>Unix seconds.</summary>
        public long at;
    }

    public static class PushProtocol
    {
        public const string DevicesPath = "push/devices/";
        /// <summary>Registering again once a week keeps the server's "last seen" fresh.</summary>
        public const long RefreshSeconds = 7 * 24 * 3600;

        private static readonly JsonSerializerSettings Settings = new JsonSerializerSettings
        {
            DateParseHandling = DateParseHandling.None,
        };

        // Android's NotificationManager.IMPORTANCE_DEFAULT and IMPORTANCE_HIGH.
        public const int ImportanceDefault = 3;
        public const int ImportanceHigh = 4;

        public static readonly IReadOnlyList<PushGroup> Groups = new[] { PushGroup.Friends, PushGroup.Rewards };

        /// <summary>The group's name on the server.</summary>
        public static string GroupName(PushGroup group) => group == PushGroup.Friends ? "friends" : "rewards";

        /// <summary>The groups turned off, in a fixed order, as the device stores and sends them ("friends,rewards").</summary>
        public static string MutedText(IEnumerable<PushGroup> muted)
        {
            var set = new HashSet<PushGroup>(muted ?? Array.Empty<PushGroup>());
            return string.Join(",", Groups.Where(set.Contains).Select(GroupName));
        }

        public static IReadOnlyList<PushGroup> ParseMuted(string text)
        {
            var names = new HashSet<string>((text ?? "").Split(','));
            return Groups.Where(group => names.Contains(GroupName(group))).ToList();
        }

        /// <summary>Every group off: the device then asks Soil to forget it rather than to mute everything.</summary>
        public static bool AllMuted(string text) => ParseMuted(text).Count == Groups.Count;

        /// <summary>"fa" for Persian in any of the ways games name it, else "en".</summary>
        public static string ChannelLanguage(string language)
        {
            var value = (language ?? "").Trim().ToLowerInvariant();
            return value == "fa" || value.StartsWith("fa-") || value.StartsWith("fa_") || value == "persian" ||
                   value == "farsi" || value == "فارسی"
                ? "fa"
                : "en";
        }

        /// <summary>The Android channel for a group, named in the game's language.</summary>
        public static PushChannel Channel(PushGroup group, string language)
        {
            var fa = ChannelLanguage(language) == "fa";
            return group == PushGroup.Friends
                ? new PushChannel
                {
                    Id = "soil_friends",
                    Name = fa ? "دوستان" : "Friends",
                    Description = fa ? "درخواست و پذیرش دوستی" : "Friend requests and accepted requests",
                    Importance = ImportanceDefault,
                }
                : new PushChannel
                {
                    Id = "soil_rewards",
                    Name = fa ? "جوایز" : "Rewards",
                    Description = fa ? "جایزه‌ی جدول و دعوت" : "Leaderboard prizes and invite rewards",
                    Importance = ImportanceHigh,
                };
        }

        /// <summary>The register body. muted: <see cref="MutedText"/>, sent whenever given, even empty, so turning a group
        /// back on reaches the server; null leaves it out.</summary>
        public static string RegisterJson(string token, string platform, string language, string muted = null)
        {
            var body = new Dictionary<string, object> { ["token"] = token };
            if (!string.IsNullOrEmpty(platform)) body["platform"] = platform;
            if (!string.IsNullOrEmpty(language)) body["language"] = language;
            if (muted != null) body["muted"] = ParseMuted(muted).Select(GroupName).ToList();
            return JsonConvert.SerializeObject(body);
        }

        public static string UnregisterJson(string token) =>
            JsonConvert.SerializeObject(new Dictionary<string, string> { ["token"] = token });

        /// <summary>The answer's status, or null when the body is not the push API's (a proxy page, a DRF error).</summary>
        public static PushStatusDetail ParseStatus(string body)
        {
            try
            {
                var detail = JObject.Parse(body ?? "")["detail"];
                return detail is JObject ? detail.ToObject<PushStatusDetail>() : null;
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>The register answer, or null when the request failed outside the API (see the HTTP status).</summary>
        public static PushRegisterResult ParseRegister(long status, string body)
        {
            if (ParseStatus(body) == null) return null;
            try
            {
                return JsonConvert.DeserializeObject<PushRegisterResult>(body, Settings);
            }
            catch (JsonException)
            {
                return null;
            }
        }

        /// <summary>
        /// Whether the device should register now: never registered, or the token, the language, the muted groups or
        /// the player changed, or the last registration is older than a week. Keeps the SDK from calling on every
        /// launch.
        /// </summary>
        public static bool ShouldRegister(PushRegistrationRecord last, string token, string language, string user,
            long now, string muted = "")
        {
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(user)) return false;
            if (last == null) return true;
            return last.token != token || (last.language ?? "") != (language ?? "") || last.user != user ||
                   (last.muted ?? "") != (muted ?? "") || now - last.at >= RefreshSeconds || now < last.at;
        }

        /// <summary>
        /// The next step for this device. Opted out: forget the registered token (if any), and never register,
        /// whatever token the provider reports again. A game without the feature: nothing, this session.
        /// </summary>
        public static PushAction Decide(PushRegistrationRecord last, string token, string language, string user,
            long now, bool optedOut, bool featureOff, string muted = "")
        {
            if (optedOut) return last != null && !string.IsNullOrEmpty(last.token) ? PushAction.Unregister : PushAction.None;
            if (featureOff) return PushAction.None;
            return ShouldRegister(last, token, language, user, now, muted) ? PushAction.Register : PushAction.None;
        }

        public static PushKind ParseKind(string kind) => kind switch
        {
            "friend_request" => PushKind.FriendRequest,
            "friend_accepted" => PushKind.FriendAccepted,
            "leaderboard_prize" => PushKind.LeaderboardPrize,
            "referral_reward" => PushKind.ReferralReward,
            "test" => PushKind.Test,
            _ => PushKind.Unknown,
        };

        /// <summary>Reads a received push's data (all strings, as FCM delivers them).</summary>
        public static PushMessage FromData(IDictionary<string, string> data, string title, string body, bool opened)
        {
            var copy = data == null ? new Dictionary<string, string>() : new Dictionary<string, string>(data);
            copy.TryGetValue("kind", out var kind);
            copy.TryGetValue("group_key", out var groupKey);
            copy.TryGetValue("ref", out var reference);
            copy.TryGetValue("soil", out var soil);
            return new PushMessage
            {
                IsSoil = soil == "1",
                Kind = soil == "1" ? ParseKind(kind) : PushKind.Unknown,
                KindName = kind,
                GroupKey = groupKey,
                Ref = reference,
                Title = title,
                Body = body,
                Opened = opened,
                Data = copy,
            };
        }
    }
}
