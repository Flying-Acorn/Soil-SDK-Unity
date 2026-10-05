using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FlyingAcorn.Soil.Socialization.Logic
{
    /// <summary>
    /// Every <c>detail.code</c> the friends API answers with. 0-6 are shared with the original API;
    /// the server only ever appends, so an unknown number is a newer server, not an error.
    /// </summary>
    public enum FriendStatus
    {
        FriendshipExists = 0,
        FriendshipCreated = 1,
        FriendshipDeleted = 2,
        FriendNotFound = 3,
        FriendshipIllegalSelf = 4,
        FriendshipError = 5,
        Throttled = 6,
        RequestSent = 7,
        RequestDeclined = 8,
        RequestCancelled = 9,
        RequestNotFound = 10,
        UserBlocked = 11,
        UserUnblocked = 12,
        /// <summary>The player blocked this one, and has to unblock them first.</summary>
        FriendBlocked = 13,
        FriendLimitReached = 14,
        RequestLimitReached = 15,
        InvalidRequest = 16,
        FriendsListed = 17,
        BlockLimitReached = 18,
    }

    public enum FriendListKind
    {
        /// <summary>Accepted friends.</summary>
        Friends,
        /// <summary>Requests other players sent to this one.</summary>
        Incoming,
        /// <summary>Requests this player sent, waiting for an answer.</summary>
        Outgoing,
        /// <summary>Players this one blocked.</summary>
        Blocked,
    }

    /// <summary>Another player, as the friends API shows them.</summary>
    [Serializable]
    public class FriendProfile
    {
        public string uuid;
        /// <summary>The short code players can type to find each other.</summary>
        public string public_id;
        public string name;
        public string username;
        public string avatar_asset;
    }

    /// <summary>A player in one of the lists, and when they got there (ISO 8601, UTC).</summary>
    [Serializable]
    public class FriendEntry : FriendProfile
    {
        public string since;
    }

    [Serializable]
    public class FriendCounts
    {
        public int friends;
        public int incoming;
        public int outgoing;
        public int blocked;
    }

    [Serializable]
    public class FriendStatusDetail
    {
        public int code;
        public string message;

        [JsonIgnore] public FriendStatus Status => (FriendStatus)code;
    }

    /// <summary>One list, newest first (capped at 1000), with every list's full count for badges.</summary>
    [Serializable]
    public class FriendList
    {
        public FriendStatusDetail detail;
        public string list;
        public List<FriendEntry> users = new();
        public FriendCounts counts = new();
    }

    /// <summary>
    /// What an action did. A refusal - no such player, a limit, a block, too many requests, a server error - is an
    /// answer, not an exception: check <see cref="Status"/>. Only an answer that is not one of these throws: no
    /// connection or a timeout, an expired sign-in, the app lacking the Friend requests feature, the player's
    /// account not found, or a proxy error page.
    /// </summary>
    [Serializable]
    public class FriendActionResult
    {
        public FriendStatusDetail detail;
        /// <summary>
        /// The other player. Null when they could not be found, and on answers that name nobody: an invalid request,
        /// Throttled, or a server error (FriendshipError).
        /// </summary>
        public FriendProfile user;
        /// <summary>
        /// The invite this request made, as entering the other player's referral code. Only a request sent by code
        /// (<c>SendFriendRequestByCode</c>), in an app with the Referrals feature and its "A friend request counts as
        /// entering the code" switch on (dashboard, Friends → Settings), can carry it, and only when it made a new
        /// invite: Invited, with this player's reward (already in their balance: show it only). Null whenever no
        /// invite was made: already invited (even by the same player), outside the window, own code, mutual, a code
        /// that matches nobody or that staff stopped, a request by UUID or an invalid one, or the switch off. When
        /// counting the invite failed on the server it is ReferralError, while the request still went through: enter
        /// the code with <c>RedeemReferralCode</c>. The invite and the request stand on their own: a request can be
        /// refused while the invite was made, and the other way round.
        /// </summary>
        public ReferralInvite invite;
        /// <summary>The HTTP status the answer came with.</summary>
        [JsonIgnore] public long HttpStatus;
        /// <summary>Seconds to wait before trying again, when <see cref="Status"/> is Throttled.</summary>
        [JsonIgnore] public int? RetryAfterSeconds;

        [JsonIgnore] public FriendStatus Status => detail?.Status ?? FriendStatus.FriendshipError;

        /// <summary>The action did what was asked, now or on an earlier try.</summary>
        [JsonIgnore] public bool Succeeded => HttpStatus >= 200 && HttpStatus < 300;
    }

    /// <summary>The request and answer formats of <c>/api/socialization/friend-requests/</c>.</summary>
    public static class FriendsProtocol
    {
        public const string BasePath = "socialization/friend-requests/";

        public const string Request = "request";
        public const string Accept = "accept";
        public const string Decline = "decline";
        public const string Cancel = "cancel";
        public const string Remove = "remove";
        public const string Block = "block";
        public const string Unblock = "unblock";

        public static string ListQuery(FriendListKind kind) => kind switch
        {
            FriendListKind.Incoming => "incoming",
            FriendListKind.Outgoing => "outgoing",
            FriendListKind.Blocked => "blocked",
            _ => "friends",
        };

        public static string ActionPath(string action) => BasePath + action + "/";

        public static string ByUuid(string uuid) => new JObject { ["uuid"] = uuid }.ToString(Formatting.None);

        public static string ByPublicId(string publicId) =>
            new JObject { ["public_id"] = publicId }.ToString(Formatting.None);

        /// <summary>
        /// The answer of an action, or null when the body is not one: a sign-in or feature refusal, a proxy
        /// error page. Refusals the friends API makes itself carry a status code and are returned.
        /// </summary>
        public static FriendActionResult ParseAction(long httpStatus, string body, string retryAfter = null)
        {
            var result = Parse<FriendActionResult>(body);
            if (result?.detail == null) return null;
            result.HttpStatus = httpStatus;
            result.RetryAfterSeconds = RetryAfter(retryAfter);
            return result;
        }

        /// <summary>A <c>Retry-After</c> header in seconds, or null when it is missing or not a number of seconds.</summary>
        public static int? RetryAfter(string header) =>
            int.TryParse(header?.Trim(), System.Globalization.NumberStyles.None,
                System.Globalization.CultureInfo.InvariantCulture, out var seconds)
                ? seconds
                : null;

        /// <summary>A list, or null unless the body is a successful list answer.</summary>
        public static FriendList ParseList(long httpStatus, string body)
        {
            if (httpStatus < 200 || httpStatus >= 300) return null;
            var result = Parse<FriendList>(body);
            if (result?.detail == null) return null;
            result.users ??= new List<FriendEntry>();
            result.counts ??= new FriendCounts();
            return result;
        }

        /// <summary>The status code of any friends answer, or null when the body has none.</summary>
        public static FriendStatus? StatusOf(string body) => Parse<FriendActionResult>(body)?.detail?.Status;

        internal static T Parse<T>(string body) where T : class
        {
            if (string.IsNullOrWhiteSpace(body)) return null;
            try
            {
                // DateParseHandling.None: by default Newtonsoft turns ISO dates into local-time DateTimes, and
                // `since` would come back reformatted in the device's culture and time zone.
                using var reader = new JsonTextReader(new System.IO.StringReader(body))
                    { DateParseHandling = DateParseHandling.None };
                var token = JToken.ReadFrom(reader);
                // DRF answers some refusals with {"detail": "text"}; only an object detail is ours.
                if (token is not JObject obj || obj["detail"] is not JObject) return null;
                return obj.ToObject<T>();
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
