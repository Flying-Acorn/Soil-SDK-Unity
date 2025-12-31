using System;
using System.Collections.Generic;
using UnityEngine.Networking;
using System.Text;
using Cysharp.Threading.Tasks;
using FlyingAcorn.Soil.Core;
using FlyingAcorn.Soil.Core.Data;
using FlyingAcorn.Soil.Core.User;
using FlyingAcorn.Soil.Core.User.Authentication;
using FlyingAcorn.Soil.Leaderboard.Models;
using FlyingAcorn.Soil.Socialization.Models;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace FlyingAcorn.Soil.Socialization
{
    /// <summary>
    /// Static class for socialization features including friends management and social leaderboards.
    /// </summary>
    public static class Socialization
    {
        /// <summary>
        /// Gets whether the Socialization service is ready for use.
        /// </summary>
        [UsedImplicitly] public static bool Ready => SoilServices.Ready;
        private static readonly string SocializationBaseUrl = $"{Core.Data.Constants.ApiUrl}/socialization/";
        private static readonly string FriendsUrl = $"{SocializationBaseUrl}friends/";
        private static readonly string FriendsLeaderboardUrl = $"{SocializationBaseUrl}v3/getfriendleaderboard/";

        /// <summary>
        /// Fetches the current user's friends list.
        /// </summary>
        /// <returns>The friends response containing the list of friends.</returns>
        public static async UniTask<FriendsResponse> GetFriends()
        {
            if (!Ready)
            {
                throw new SoilException("SoilServices is not initialized. Cannot get friends.", 
                    SoilExceptionErrorCode.NotReady);
            }

            using var request = UnityWebRequest.Get(FriendsUrl);
            var authHeader = Authenticate.GetAuthorizationHeader()?.ToString();
            if (!string.IsNullOrEmpty(authHeader)) request.SetRequestHeader("Authorization", authHeader);
            request.SetRequestHeader("Accept", "application/json");
            try
            {
                await DataUtils.ExecuteUnityWebRequestWithTimeout(request, UserPlayerPrefs.RequestTimeout);
            }
            catch (SoilException) { throw; }
            catch (Exception ex)
            {
                throw new SoilException($"Unexpected error while getting friends: {ex.Message}", SoilExceptionErrorCode.TransportError);
            }

            if (request.responseCode < 200 || request.responseCode >= 300)
            {
                throw new SoilException($"Server returned error {(System.Net.HttpStatusCode)request.responseCode}: {request.downloadHandler?.text}", SoilExceptionErrorCode.TransportError);
            }

            FriendsResponse firendshipResponse;
            try
            {
                firendshipResponse = JsonConvert.DeserializeObject<FriendsResponse>(request.downloadHandler.text);
            }
            catch (Exception)
            {
                throw new SoilException($"Invalid response format while getting friends. Response: {request.downloadHandler?.text}", SoilExceptionErrorCode.InvalidResponse);
            }

            if (firendshipResponse.detail.code == Constants.FriendshipStatus.FriendshipExists &&
                (firendshipResponse.friends == null || firendshipResponse.friends.Count == 0))
                throw new SoilException("Problem with getting friends. No friends found", SoilExceptionErrorCode.InvalidResponse);

            return firendshipResponse;
        }

        /// <summary>
        /// Adds a friend using their UUID.
        /// </summary>
        /// <param name="uuid">The UUID of the user to add as a friend.</param>
        /// <returns>The friends response after adding the friend.</returns>
        public static async UniTask<FriendsResponse> AddFriendWithUUID(string uuid)
        {
            if (string.IsNullOrEmpty(uuid))
                throw new SoilException("UUID cannot be null or empty", SoilExceptionErrorCode.InvalidRequest);

            if (!SoilServices.Ready)
            {
                throw new SoilException("SoilServices is not initialized. Cannot add friend.", 
                    SoilExceptionErrorCode.NotReady);
            }

            var body = new Dictionary<string, object> { { "uuid", uuid } };
            var json = JsonConvert.SerializeObject(body);
            using var request = new UnityWebRequest(FriendsUrl, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)),
                downloadHandler = new DownloadHandlerBuffer()
            };
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");
            var authHeader = Authenticate.GetAuthorizationHeader()?.ToString();
            if (!string.IsNullOrEmpty(authHeader)) request.SetRequestHeader("Authorization", authHeader);
            try
            {
                await DataUtils.ExecuteUnityWebRequestWithTimeout(request, UserPlayerPrefs.RequestTimeout);
            }
            catch (SoilException) { throw; }
            catch (Exception ex)
            {
                throw new SoilException($"Unexpected error while adding friend: {ex.Message}", SoilExceptionErrorCode.TransportError);
            }

            if (request.responseCode < 200 || request.responseCode >= 300)
            {
                throw new SoilException($"Server returned error {(System.Net.HttpStatusCode)request.responseCode}: {request.downloadHandler?.text}", SoilExceptionErrorCode.TransportError);
            }

            FriendsResponse friendshipResponse;
            try
            {
                friendshipResponse = JsonConvert.DeserializeObject<FriendsResponse>(request.downloadHandler.text);
            }
            catch (Exception)
            {
                throw new SoilException($"Invalid response format while adding friend. Response: {request.downloadHandler?.text}", SoilExceptionErrorCode.InvalidResponse);
            }

            return friendshipResponse;
        }

        /// <summary>
        /// Removes a friend using their UUID.
        /// </summary>
        /// <param name="uuid">The UUID of the friend to remove.</param>
        /// <returns>The friends response after removing the friend.</returns>
        public static async UniTask<FriendsResponse> RemoveFriendWithUUID(string uuid)
        {
            if (string.IsNullOrEmpty(uuid))
                throw new SoilException("UUID cannot be null or empty", SoilExceptionErrorCode.InvalidRequest);

            if (!SoilServices.Ready)
            {
                throw new SoilException("SoilServices is not initialized. Cannot remove friend.", 
                    SoilExceptionErrorCode.NotReady);
            }

            var body = new Dictionary<string, object> { { "uuid", uuid } };
            var json = JsonConvert.SerializeObject(body);
            // UnityWebRequest doesn't have native DELETE with body; emulate via POST override or raw method
            using var request = new UnityWebRequest(FriendsUrl, "DELETE")
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json)),
                downloadHandler = new DownloadHandlerBuffer()
            };
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");
            var authHeader = Authenticate.GetAuthorizationHeader()?.ToString();
            if (!string.IsNullOrEmpty(authHeader)) request.SetRequestHeader("Authorization", authHeader);
            try
            {
                await DataUtils.ExecuteUnityWebRequestWithTimeout(request, UserPlayerPrefs.RequestTimeout);
            }
            catch (SoilException) { throw; }
            catch (Exception ex)
            {
                throw new SoilException($"Unexpected error while removing friend: {ex.Message}", SoilExceptionErrorCode.TransportError);
            }

            if (request.responseCode < 200 || request.responseCode >= 300)
            {
                throw new SoilException($"Server returned error {(System.Net.HttpStatusCode)request.responseCode}: {request.downloadHandler?.text}", SoilExceptionErrorCode.TransportError);
            }

            FriendsResponse firendshipResponse;
            try
            {
                firendshipResponse = JsonConvert.DeserializeObject<FriendsResponse>(request.downloadHandler.text);
            }
            catch (Exception)
            {
                throw new SoilException($"Invalid response format while removing friend. Response: {request.downloadHandler?.text}", SoilExceptionErrorCode.InvalidResponse);
            }

            return firendshipResponse;
        }

        /// <summary>
        /// Fetches leaderboard scores for friends.
        /// </summary>
        /// <param name="leaderboardId">The ID of the leaderboard.</param>
        /// <param name="count">Number of scores to fetch.</param>
        /// <param name="relative">If true, ranks are relative to the current user.</param>
        /// <returns>LeaderboardResponse containing user scores, iteration info, and next reset timestamp.</returns>
        public static async UniTask<LeaderboardResponse> GetFriendsLeaderboard(string leaderboardId, int count = 10,
            bool relative = false)
        {
            if (string.IsNullOrEmpty(leaderboardId))
                throw new SoilException("Leaderboard ID cannot be null or empty",
                    SoilExceptionErrorCode.InvalidRequest);
            if (!SoilServices.Ready)
            {
                throw new SoilException("SoilServices is not initialized. Cannot get friends leaderboard.",
                    SoilExceptionErrorCode.NotReady);
            }
            var payload = new Dictionary<string, object>
            {
                { "leaderboard_identifier", leaderboardId },
                { "count", count },
                { "relative", relative },
            };

            var stringBody = JsonConvert.SerializeObject(payload);

            using var request = new UnityWebRequest(FriendsLeaderboardUrl, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(stringBody)),
                downloadHandler = new DownloadHandlerBuffer()
            };
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");
            var authHeader = Authenticate.GetAuthorizationHeader()?.ToString();
            if (!string.IsNullOrEmpty(authHeader)) request.SetRequestHeader("Authorization", authHeader);
            try
            {
                await DataUtils.ExecuteUnityWebRequestWithTimeout(request, UserPlayerPrefs.RequestTimeout);
            }
            catch (SoilException) { throw; }
            catch (Exception ex)
            {
                throw new SoilException($"Unexpected error while fetching friends leaderboard: {ex.Message}", SoilExceptionErrorCode.TransportError);
            }

            if (request.responseCode < 200 || request.responseCode >= 300)
            {
                throw new SoilException($"Server returned error {(System.Net.HttpStatusCode)request.responseCode}: {request.downloadHandler?.text}", SoilExceptionErrorCode.TransportError);
            }

            var responseText = request.downloadHandler.text;
            var leaderboard = JsonConvert.DeserializeObject<LeaderboardResponse>(responseText);
            SocializationPlayerPrefs.SetCachedLeaderboardData(leaderboardId, responseText, relative);
            return leaderboard;
        }
    }
}