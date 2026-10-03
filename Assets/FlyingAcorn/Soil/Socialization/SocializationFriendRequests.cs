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
    /// Every action is safe to retry. A refusal - no such player, a limit, a block - comes back as a
    /// <see cref="FriendActionResult"/> with its <see cref="FriendActionResult.Status"/>; only a transport
    /// failure, an expired sign-in or the feature being off throws a <see cref="SocializationException"/>.
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
            var (status, body, _) = await Send(request, SocializationOperation.GetFriendList);
            return FriendsProtocol.ParseList(status, body)
                   ?? throw Failure(status, body, SocializationOperation.GetFriendList);
        }

        /// <summary>Asks a player to be friends. If they already asked, this accepts and answers FriendshipCreated.</summary>
        public static UniTask<FriendActionResult> SendFriendRequest(string uuid) => ByUuid(FriendsProtocol.Request, uuid);

        /// <summary>
        /// Asks a player to be friends by their player code (<c>SoilServices.UserInfo.public_id</c> on their
        /// device). Casing, spaces and dashes do not matter. Answers FriendNotFound for a code that matches
        /// nobody in this game. Rate-limited together with every other request.
        /// </summary>
        public static UniTask<FriendActionResult> SendFriendRequestByCode(string playerCode)
        {
            RequireText(playerCode, nameof(playerCode));
            return Act(FriendsProtocol.Request, FriendsProtocol.ByPublicId(playerCode.Trim()));
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

        private static UniTask<FriendActionResult> ByUuid(string action, string uuid)
        {
            RequireText(uuid, nameof(uuid));
            return Act(action, FriendsProtocol.ByUuid(uuid));
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
            var (status, body, retryAfter) = await Send(request, SocializationOperation.FriendAction);
            return FriendsProtocol.ParseAction(status, body, retryAfter)
                   ?? throw Failure(status, body, SocializationOperation.FriendAction);
        }

        private static async UniTask<(long status, string body, string retryAfter)> Send(UnityWebRequest request,
            SocializationOperation operation)
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
                throw new SocializationException($"Unexpected error while calling friends: {e.Message}", operation,
                    SoilExceptionErrorCode.TransportError);
            }
            return (request.responseCode, request.downloadHandler?.text, request.GetResponseHeader("Retry-After"));
        }

        private static void EnsureReady(SocializationOperation operation)
        {
            if (!Ready)
                throw new SocializationException("SoilServices is not initialized. Cannot use friends.", operation,
                    SoilExceptionErrorCode.NotReady);
        }

        private static SocializationException Failure(long status, string body, SocializationOperation operation)
        {
            var code = status switch
            {
                401 => SoilExceptionErrorCode.InvalidToken,
                // The app does not have the Friend requests feature turned on.
                403 => SoilExceptionErrorCode.Forbidden,
                >= 200 and < 300 => SoilExceptionErrorCode.InvalidResponse,
                503 => SoilExceptionErrorCode.ServiceUnavailable,
                _ => SoilExceptionErrorCode.TransportError,
            };
            return new SocializationException($"Friends request failed ({status}): {body}", operation, code);
        }

        private static void RequireText(string value, string name)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new SocializationException($"{name} cannot be null or empty", SocializationOperation.FriendAction,
                    SoilExceptionErrorCode.InvalidRequest);
        }
    }
}
