using System;
using System.Globalization;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FlyingAcorn.Soil.Socialization.Logic
{
    /// <summary>
    /// Every <c>detail.code</c> the referrals API answers with. The server only ever appends, so an unknown
    /// number is a newer server, not an error.
    /// </summary>
    public enum ReferralStatus
    {
        /// <summary>The code was entered: the code's owner is now the player's inviter.</summary>
        Invited = 0,
        /// <summary>No player in this game has that code (or their code was stopped).</summary>
        CodeNotFound = 1,
        /// <summary>The player entered their own code.</summary>
        OwnCode = 2,
        /// <summary>The player already has an inviter. It never changes.</summary>
        AlreadyInvited = 3,
        /// <summary>The player's account is older than the app's window for entering a code.</summary>
        WindowClosed = 4,
        /// <summary>The code's owner was invited by this player, so cannot also be their inviter.</summary>
        MutualInvite = 5,
        InvalidRequest = 6,
        ReferralError = 7,
        Throttled = 8,
        /// <summary>The answer of <c>GetReferralInfo</c>.</summary>
        ReferralInfo = 9,
    }

    [Serializable]
    public class ReferralStatusDetail
    {
        public int code;
        public string message;

        [JsonIgnore] public ReferralStatus Status => (ReferralStatus)code;
    }

    /// <summary>
    /// What a player was given, already added on the server to their Soil economy balance of
    /// <see cref="currency"/>. It is for showing only: nothing more needs to be called to grant it, and a game that
    /// also adds it to its own wallet pays it twice (a game that moves that currency into its own wallet picks it
    /// up there).
    /// </summary>
    [Serializable]
    public class ReferralReward
    {
        /// <summary>The identifier of the Soil virtual currency.</summary>
        public string currency;
        public int amount;
    }

    /// <summary>What the invite screen shows. The player's own code is <c>SoilServices.UserInfo.public_id</c>.</summary>
    [Serializable]
    public class ReferralInfo
    {
        public ReferralStatusDetail detail;
        /// <summary>The player has an inviter.</summary>
        public bool invited;
        /// <summary>
        /// Who invited the player. Null when they were not invited, and also when the inviter's account was
        /// deleted though <see cref="invited"/> stays true.
        /// </summary>
        public FriendProfile invited_by;
        /// <summary>The player can still enter a code: not invited yet, and inside the app's window.</summary>
        public bool can_redeem;
        /// <summary>When the window to enter a code closes (ISO 8601, UTC); null when the app has no window.</summary>
        public string redeem_until;
        /// <summary>How many players entered this player's code, rewarded or not.</summary>
        public int invited_count;

        /// <summary><see cref="redeem_until"/> as a UTC time; null when there is no window.</summary>
        [JsonIgnore]
        public DateTime? RedeemUntilUtc =>
            DateTime.TryParse(redeem_until, CultureInfo.InvariantCulture,
                DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var when)
                ? when
                : null;
    }

    /// <summary>
    /// The outcome of entering a code. A refusal - unknown code, already invited, window closed - is an answer,
    /// not an exception: check <see cref="Status"/>.
    /// </summary>
    [Serializable]
    public class ReferralInvite
    {
        public ReferralStatusDetail detail;
        /// <summary>
        /// What this player was given for being invited, already in their Soil currency balance: show it, do not
        /// grant it again. Null when the invite was refused or failed, or the app gives invited players nothing.
        /// </summary>
        public ReferralReward reward;

        [JsonIgnore] public ReferralStatus Status => detail?.Status ?? ReferralStatus.ReferralError;

        /// <summary>The code was entered: the player now has this inviter.</summary>
        [JsonIgnore] public bool Succeeded => Status == ReferralStatus.Invited;
    }

    /// <summary>The answer of <c>RedeemReferralCode</c>.</summary>
    [Serializable]
    public class ReferralRedeemResult : ReferralInvite
    {
        /// <summary>The player whose code was entered. Only set when <see cref="ReferralInvite.Succeeded"/>.</summary>
        public FriendProfile inviter;
        /// <summary>The HTTP status the answer came with.</summary>
        [JsonIgnore] public long HttpStatus;
        /// <summary>Seconds to wait before trying again, when <see cref="ReferralInvite.Status"/> is Throttled.</summary>
        [JsonIgnore] public int? RetryAfterSeconds;
    }

    /// <summary>The request and answer formats of <c>/api/socialization/referrals/</c>.</summary>
    public static class ReferralsProtocol
    {
        public const string BasePath = "socialization/referrals/";
        public const string RedeemPath = BasePath + "redeem/";

        public static string RedeemBody(string code) => new JObject { ["code"] = code }.ToString(Formatting.None);

        /// <summary>The info, or null unless the body is a successful info answer.</summary>
        public static ReferralInfo ParseInfo(long httpStatus, string body)
        {
            if (httpStatus < 200 || httpStatus >= 300) return null;
            var result = FriendsProtocol.Parse<ReferralInfo>(body);
            return result?.detail == null ? null : result;
        }

        /// <summary>
        /// The answer of entering a code, or null when the body is not one: a sign-in or feature refusal, a proxy
        /// error page. Refusals the referrals API makes itself carry a status code and are returned.
        /// </summary>
        public static ReferralRedeemResult ParseRedeem(long httpStatus, string body, string retryAfter = null)
        {
            var result = FriendsProtocol.Parse<ReferralRedeemResult>(body);
            if (result?.detail == null) return null;
            result.HttpStatus = httpStatus;
            result.RetryAfterSeconds = FriendsProtocol.RetryAfter(retryAfter);
            return result;
        }

        /// <summary>The status code of any referrals answer, or null when the body has none.</summary>
        public static ReferralStatus? StatusOf(string body) => FriendsProtocol.Parse<ReferralInvite>(body)?.detail?.Status;
    }
}
