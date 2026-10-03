using System;
using System.Text;
using Cysharp.Threading.Tasks;
using FlyingAcorn.Soil.Core;
using FlyingAcorn.Soil.Core.Data;
using FlyingAcorn.Soil.Core.User;
using FlyingAcorn.Soil.Core.User.Authentication;
using FlyingAcorn.Soil.Socialization.Data;
using FlyingAcorn.Soil.Socialization.Logic;
using JetBrains.Annotations;
using UnityEngine.Networking;

namespace FlyingAcorn.Soil.Socialization
{
    /// <summary>
    /// Friends with consent: requests, accept, decline, cancel, remove and block. Needs the app's
    /// Friends v2 (Socialization v2) feature; the original <see cref="Socialization"/> calls keep working
    /// beside it, on the same friendships, until the app turns Friends v1 off.
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
    public static class FriendsV2
    {
        [UsedImplicitly] public static bool Ready => SoilServices.Ready;

        private static string BaseUrl => $"{Core.Data.Constants.ApiUrl}/";

        /// <summary>One of the player's lists, newest first, with every list's count (for badges).</summary>
        public static async UniTask<FriendList> GetList(FriendListKind kind = FriendListKind.Friends)
        {
            EnsureReady(SocializationOperation.FriendsV2List);
            var url = $"{BaseUrl}{FriendsV2Protocol.BasePath}?list={FriendsV2Protocol.ListQuery(kind)}";
            using var request = UnityWebRequest.Get(url);
            var (status, body, _) = await Send(request, SocializationOperation.FriendsV2List);
            return FriendsV2Protocol.ParseList(status, body)
                   ?? throw Failure(status, body, SocializationOperation.FriendsV2List);
        }

        /// <summary>Asks a player to be friends. If they already asked, this accepts and answers FriendshipCreated.</summary>
        public static UniTask<FriendActionResult> SendRequest(string uuid) => ByUuid(FriendsV2Protocol.Request, uuid);

        /// <summary>
        /// Asks a player to be friends by the code they show in game (<c>SoilServices.UserInfo.public_id</c> on
        /// their device). Casing, spaces and dashes do not matter. Answers FriendNotFound for a code that matches
        /// nobody in this game. Rate-limited together with every other request.
        /// </summary>
        public static UniTask<FriendActionResult> SendRequestByPublicId(string publicId)
        {
            RequireText(publicId, nameof(publicId));
            return Act(FriendsV2Protocol.Request, FriendsV2Protocol.ByPublicId(publicId.Trim()));
        }

        public static UniTask<FriendActionResult> Accept(string uuid) => ByUuid(FriendsV2Protocol.Accept, uuid);

        public static UniTask<FriendActionResult> Decline(string uuid) => ByUuid(FriendsV2Protocol.Decline, uuid);

        /// <summary>Withdraws a request this player sent.</summary>
        public static UniTask<FriendActionResult> Cancel(string uuid) => ByUuid(FriendsV2Protocol.Cancel, uuid);

        /// <summary>Ends a friendship for both players.</summary>
        public static UniTask<FriendActionResult> Remove(string uuid) => ByUuid(FriendsV2Protocol.Remove, uuid);

        /// <summary>
        /// Ends any friendship and hides the other player's requests. They are not told: their requests look
        /// like they are still waiting.
        /// </summary>
        public static UniTask<FriendActionResult> Block(string uuid) => ByUuid(FriendsV2Protocol.Block, uuid);

        /// <summary>Lifts a block. An ended friendship does not come back.</summary>
        public static UniTask<FriendActionResult> Unblock(string uuid) => ByUuid(FriendsV2Protocol.Unblock, uuid);

        private static UniTask<FriendActionResult> ByUuid(string action, string uuid)
        {
            RequireText(uuid, nameof(uuid));
            return Act(action, FriendsV2Protocol.ByUuid(uuid));
        }

        private static async UniTask<FriendActionResult> Act(string action, string json)
        {
            EnsureReady(SocializationOperation.FriendsV2Action);
            using var request = new UnityWebRequest(BaseUrl + FriendsV2Protocol.ActionPath(action),
                UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)),
                downloadHandler = new DownloadHandlerBuffer()
            };
            request.SetRequestHeader("Content-Type", "application/json");
            var (status, body, retryAfter) = await Send(request, SocializationOperation.FriendsV2Action);
            return FriendsV2Protocol.ParseAction(status, body, retryAfter)
                   ?? throw Failure(status, body, SocializationOperation.FriendsV2Action);
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
                // The app does not have Friends v2 turned on.
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
                throw new SocializationException($"{name} cannot be null or empty", SocializationOperation.FriendsV2Action,
                    SoilExceptionErrorCode.InvalidRequest);
        }
    }
}
