using System;
using System.Text;
using Cysharp.Threading.Tasks;
using FlyingAcorn.Soil.Core;
using FlyingAcorn.Soil.Core.Data;
using FlyingAcorn.Soil.Core.User;
using FlyingAcorn.Soil.Core.User.Authentication;
using FlyingAcorn.Soil.Socialization.Data;
using FlyingAcorn.Soil.Socialization.Logic;
using UnityEngine.Networking;

namespace FlyingAcorn.Soil.Socialization
{
    /// <summary>
    /// Friend requests: send, accept, decline, cancel, remove and block. Needs the app's Friend requests feature.
    /// The old instant add (<see cref="AddFriendWithUUID"/> and the other obsolete calls) keeps working beside it,
    /// on the same friendships, until the app turns the old one off.
    /// <para>
    /// Every action is safe to retry. A refusal - no such player, a limit, a block, too many requests - comes back
    /// as a <see cref="FriendActionResult"/> with its <see cref="FriendActionResult.Status"/>. A
    /// <see cref="SocializationException"/> is thrown for no connection or a timeout, an expired sign-in, the
    /// feature being off (Forbidden), the player's account not found (NotFound), and for
    /// <see cref="GetFriendList"/> read too often (TooManyRequests, with
    /// <see cref="SocializationException.RetryAfterSeconds"/>). Every call reports these through the returned task,
    /// never by throwing on the spot.
    /// </para>
    /// <para>
    /// When signing in moves the player onto an existing account, their friends, requests and blocks move
    /// with them: fetch the lists again after a sign-in.
    /// </para>
    /// </summary>
    public static partial class Socialization
    {
        private static string ApiBaseUrl => $"{Core.Data.Constants.ApiUrl}/";

        /// <summary>One of the player's lists, newest first, with every list's count (for badges).</summary>
        public static async UniTask<FriendList> GetFriendList(FriendListKind kind = FriendListKind.Friends)
        {
            EnsureReady(SocializationOperation.GetFriendList);
            var url = $"{ApiBaseUrl}{FriendsProtocol.BasePath}?list={FriendsProtocol.ListQuery(kind)}";
            using var request = UnityWebRequest.Get(url);
            var response = await Send(request, SocializationOperation.GetFriendList);
            return FriendsProtocol.ParseList(response.Status, response.Body)
                   ?? throw Failure(response, SocializationOperation.GetFriendList);
        }

        /// <summary>
        /// Asks a player to be friends. If they already asked, this accepts and answers FriendshipCreated, unless
        /// their friendships are paused (see <see cref="FriendStatus.SocializationRestricted"/>): then it waits as
        /// RequestSent like any other request.
        /// </summary>
        public static UniTask<FriendActionResult> SendFriendRequest(string uuid) => ByUuid(FriendsProtocol.Request, uuid);

        /// <summary>
        /// Asks a player to be friends by their player code (<c>SoilServices.UserInfo.public_id</c> on their
        /// device). Casing, spaces and dashes do not matter. Answers FriendNotFound for a code that matches
        /// nobody in this game. Rate-limited together with requests by UUID and the old AddFriendWithUUID.
        /// <para>
        /// In an app with the Referrals feature and its "A friend request counts as entering the code" switch on, this also
        /// enters the code as the player's referral code: when that made a new invite, it is in
        /// <see cref="FriendActionResult.invite"/> (null otherwise, with no reason given: use
        /// <see cref="RedeemReferralCode"/> to tell the player why).
        /// </para>
        /// </summary>
        public static async UniTask<FriendActionResult> SendFriendRequestByCode(string playerCode)
        {
            // Async, like every call here: a bad argument faults the returned task instead of throwing on the spot.
            RequireText(playerCode, nameof(playerCode));
            return await Act(FriendsProtocol.Request, FriendsProtocol.ByPublicId(playerCode.Trim()));
        }

        public static UniTask<FriendActionResult> AcceptFriendRequest(string uuid) => ByUuid(FriendsProtocol.Accept, uuid);

        public static UniTask<FriendActionResult> DeclineFriendRequest(string uuid) => ByUuid(FriendsProtocol.Decline, uuid);

        /// <summary>Withdraws a request this player sent.</summary>
        public static UniTask<FriendActionResult> CancelFriendRequest(string uuid) => ByUuid(FriendsProtocol.Cancel, uuid);

        /// <summary>Ends a friendship for both players.</summary>
        public static UniTask<FriendActionResult> RemoveFriend(string uuid) => ByUuid(FriendsProtocol.Remove, uuid);

        /// <summary>
        /// Ends any friendship and hides the other player's requests. They are not told: their requests look
        /// like they are still waiting.
        /// </summary>
        public static UniTask<FriendActionResult> BlockPlayer(string uuid) => ByUuid(FriendsProtocol.Block, uuid);

        /// <summary>Lifts a block. An ended friendship does not come back.</summary>
        public static UniTask<FriendActionResult> UnblockPlayer(string uuid) => ByUuid(FriendsProtocol.Unblock, uuid);

        private static async UniTask<FriendActionResult> ByUuid(string action, string uuid)
        {
            RequireText(uuid, nameof(uuid));
            return await Act(action, FriendsProtocol.ByUuid(uuid));
        }

        private static async UniTask<FriendActionResult> Act(string action, string json)
        {
            EnsureReady(SocializationOperation.FriendAction);
            using var request = new UnityWebRequest(ApiBaseUrl + FriendsProtocol.ActionPath(action),
                UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)),
                downloadHandler = new DownloadHandlerBuffer()
            };
            request.SetRequestHeader("Content-Type", "application/json");
            var response = await Send(request, SocializationOperation.FriendAction);
            return FriendsProtocol.ParseAction(response.Status, response.Body, response.RetryAfter)
                   ?? throw Failure(response, SocializationOperation.FriendAction);
        }

        private readonly struct HttpAnswer
        {
            public readonly long Status;
            public readonly string Body;
            public readonly string RetryAfter;
            /// <summary>Unity's error text, for a request that got no answer (Status 0).</summary>
            public readonly string Error;

            public HttpAnswer(long status, string body, string retryAfter, string error)
            {
                Status = status;
                Body = body;
                RetryAfter = retryAfter;
                Error = error;
            }
        }

        private static async UniTask<HttpAnswer> Send(UnityWebRequest request, SocializationOperation operation)
        {
            request.SetRequestHeader("Accept", "application/json");
            var authHeader = Authenticate.GetAuthorizationHeader()?.ToString();
            if (!string.IsNullOrEmpty(authHeader)) request.SetRequestHeader("Authorization", authHeader);
            try
            {
                await DataUtils.ExecuteUnityWebRequestWithTimeout(request, UserPlayerPrefs.RequestTimeout);
            }
            catch (SoilException e)
            {
                throw new SocializationException(e.Message, operation, e.ErrorCode);
            }
            catch (Exception e)
            {
                throw new SocializationException($"Unexpected error while calling {Feature(operation)}: {e.Message}", operation,
                    SoilExceptionErrorCode.TransportError);
            }
            return new HttpAnswer(request.responseCode, request.downloadHandler?.text,
                request.GetResponseHeader("Retry-After"), request.error);
        }

        private static void EnsureReady(SocializationOperation operation)
        {
            if (!Ready)
                throw new SocializationException($"SoilServices is not initialized. Cannot use {Feature(operation)}.", operation,
                    SoilExceptionErrorCode.NotReady);
        }

        private static SocializationException Failure(HttpAnswer response, SocializationOperation operation)
        {
            var code = response.Status switch
            {
                401 => SoilExceptionErrorCode.InvalidToken,
                // The app does not have the Friend requests (or Referrals) feature turned on.
                403 => SoilExceptionErrorCode.Forbidden,
                // The player's account could not be found.
                404 => SoilExceptionErrorCode.NotFound,
                // A read (list or invite screen) asked for too often, or a proxy refusing: see RetryAfterSeconds.
                429 => SoilExceptionErrorCode.TooManyRequests,
                >= 200 and < 300 => SoilExceptionErrorCode.InvalidResponse,
                503 => SoilExceptionErrorCode.ServiceUnavailable,
                _ => SoilExceptionErrorCode.TransportError,
            };
            var what = Feature(operation) == "referrals" ? "Referrals" : "Friends";
            // No answer at all (no connection, DNS, TLS): Unity's error says why; there is no body.
            var reason = response.Status == 0 && !string.IsNullOrEmpty(response.Error) ? response.Error : response.Body;
            return new SocializationException($"{what} request failed ({response.Status}): {reason}", operation, code)
            {
                RetryAfterSeconds = FriendsProtocol.RetryAfter(response.RetryAfter)
            };
        }

        private static string Feature(SocializationOperation operation) =>
            operation is SocializationOperation.GetReferralInfo or SocializationOperation.RedeemReferralCode
                ? "referrals"
                : "friends";

        private static void RequireText(string value, string name,
            SocializationOperation operation = SocializationOperation.FriendAction)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new SocializationException($"{name} cannot be null or empty", operation,
                    SoilExceptionErrorCode.InvalidRequest);
        }
    }
}
