using System;
using System.Collections.Generic;
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

    /// <summary>What was last registered, to tell whether registering again is needed.</summary>
    [Serializable]
    public class PushRegistrationRecord
    {
        public string token;
        public string language;
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

        public static string RegisterJson(string token, string platform, string language)
        {
            var body = new Dictionary<string, string> { ["token"] = token };
            if (!string.IsNullOrEmpty(platform)) body["platform"] = platform;
            if (!string.IsNullOrEmpty(language)) body["language"] = language;
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
        /// Whether the device should register now: never registered, or the token, the language or the player
        /// changed, or the last registration is older than a week. Keeps the SDK from calling on every launch.
        /// </summary>
        public static bool ShouldRegister(PushRegistrationRecord last, string token, string language, string user,
            long now)
        {
            if (string.IsNullOrEmpty(token) || string.IsNullOrEmpty(user)) return false;
            if (last == null) return true;
            return last.token != token || (last.language ?? "") != (language ?? "") || last.user != user ||
                   now - last.at >= RefreshSeconds || now < last.at;
        }

        /// <summary>
        /// The next step for this device. Opted out: forget the registered token (if any), and never register,
        /// whatever token the provider reports again. A game without the feature: nothing, this session.
        /// </summary>
        public static PushAction Decide(PushRegistrationRecord last, string token, string language, string user,
            long now, bool optedOut, bool featureOff)
        {
            if (optedOut) return last != null && !string.IsNullOrEmpty(last.token) ? PushAction.Unregister : PushAction.None;
            if (featureOff) return PushAction.None;
            return ShouldRegister(last, token, language, user, now) ? PushAction.Register : PushAction.None;
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
