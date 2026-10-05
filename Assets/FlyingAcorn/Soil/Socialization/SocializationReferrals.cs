using System.Text;
using Cysharp.Threading.Tasks;
using FlyingAcorn.Soil.Socialization.Data;
using FlyingAcorn.Soil.Socialization.Logic;
using UnityEngine.Networking;

namespace FlyingAcorn.Soil.Socialization
{
    /// <summary>
    /// Referrals: a new player names the player who invited them by entering that player's code. Needs the app's
    /// Referrals feature.
    /// <para>
    /// A player's referral code is their player code, <c>SoilServices.UserInfo.public_id</c>: the same code friend
    /// requests use. A player has at most one inviter, ever. Rewards for both players are added on the server to
    /// their Soil economy currency balances the moment the code is entered: the invited player's reward is in the
    /// answer, the inviter finds theirs by their balance rising.
    /// </para>
    /// <para>
    /// A refusal - unknown code, already invited, window closed, too many tries - comes back as a
    /// <see cref="ReferralRedeemResult"/> with its <see cref="ReferralInvite.Status"/>. A
    /// <see cref="SocializationException"/> is thrown for no connection or a timeout, an expired sign-in, the
    /// feature being off (Forbidden), the player's account not found (NotFound), and for
    /// <see cref="GetReferralInfo"/> read too often (TooManyRequests, with
    /// <see cref="SocializationException.RetryAfterSeconds"/>).
    /// </para>
    /// </summary>
    public static partial class Socialization
    {
        /// <summary>
        /// What the invite screen shows: who invited the player, whether and until when they can still enter a
        /// code, and how many players entered theirs.
        /// </summary>
        public static async UniTask<ReferralInfo> GetReferralInfo()
        {
            EnsureReady(SocializationOperation.GetReferralInfo);
            using var request = UnityWebRequest.Get(ApiBaseUrl + ReferralsProtocol.BasePath);
            var response = await Send(request, SocializationOperation.GetReferralInfo);
            return ReferralsProtocol.ParseInfo(response.Status, response.Body)
                   ?? throw Failure(response, SocializationOperation.GetReferralInfo);
        }

        /// <summary>
        /// Names the player whose code this is as this player's inviter, and grants both players the app's
        /// rewards. Casing, spaces and dashes do not matter. Safe to retry, for example after a timeout: the same
        /// code again answers Invited with the reward given the first time (granted only once); a different code
        /// answers AlreadyInvited.
        /// </summary>
        /// <param name="code">The inviter's player code (their <c>SoilServices.UserInfo.public_id</c>).</param>
        public static async UniTask<ReferralRedeemResult> RedeemReferralCode(string code)
        {
            RequireText(code, nameof(code), SocializationOperation.RedeemReferralCode);
            EnsureReady(SocializationOperation.RedeemReferralCode);
            using var request = new UnityWebRequest(ApiBaseUrl + ReferralsProtocol.RedeemPath,
                UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(ReferralsProtocol.RedeemBody(code.Trim()))),
                downloadHandler = new DownloadHandlerBuffer()
            };
            request.SetRequestHeader("Content-Type", "application/json");
            var response = await Send(request, SocializationOperation.RedeemReferralCode);
            return ReferralsProtocol.ParseRedeem(response.Status, response.Body, response.RetryAfter)
                   ?? throw Failure(response, SocializationOperation.RedeemReferralCode);
        }
    }
}
