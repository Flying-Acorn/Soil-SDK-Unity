using System;
using System.Text;
using Cysharp.Threading.Tasks;
using FlyingAcorn.Analytics;
using FlyingAcorn.Soil.Core.Data;
using FlyingAcorn.Soil.Core.JWTTools;
using FlyingAcorn.Soil.Core.User.Authentication;
using JetBrains.Annotations;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;

namespace FlyingAcorn.Soil.Core.User
{
    public static class UserApiHandler
    {
        private static string GetPlayerInfoUrl => $"{Authenticate.UserBaseUrl}/";
        private static UniTask<UserInfo>? _fetchPlayerInfoTask;
        internal static Action<bool> OnUserFilled; // True means user is changed

        [ItemNotNull]
        [UsedImplicitly]
        public static async UniTask<UserInfo> FetchPlayerInfo(bool allowDuringInitialization = false)
        {
            // Permit during core initialization when tokens are being established
            if (!SoilServices.Ready && !allowDuringInitialization)
                throw new SoilException("SoilServices is not initialized. Cannot fetch player info.", SoilExceptionErrorCode.NotReady);

            // Clear failed tasks to allow retry
            if (_fetchPlayerInfoTask?.AsTask() is { IsCompletedSuccessfully: false, IsCompleted: true })
                _fetchPlayerInfoTask = null;

            // Prevent multiple concurrent fetch operations from blocking
            if (_fetchPlayerInfoTask == null)
            {
                _fetchPlayerInfoTask = FetchPlayerInfoInternal();
            }
            else if (!_fetchPlayerInfoTask?.AsTask().IsCompleted ?? false)
            {
                MyDebug.Verbose("Player info fetch already in progress, sharing existing request...");
            }

            try
            {
                return await _fetchPlayerInfoTask.Value;
            }
            catch (Exception)
            {
                // Clear the failed task for subsequent retry attempts
                if (_fetchPlayerInfoTask?.AsTask() != null && _fetchPlayerInfoTask.Value.AsTask().IsFaulted)
                {
                    _fetchPlayerInfoTask = null;
                }
                throw;
            }
        }

        private static async UniTask<UserInfo> FetchPlayerInfoInternal()
        {
            MyDebug.Verbose("Fetching player info...");

            if (UserPlayerPrefs.TokenData == null || string.IsNullOrEmpty(UserPlayerPrefs.TokenData.Access))
            {
                throw new Exception("Access token is missing. Abandoning the process.");
            }

            if (!JwtUtils.IsTokenValid(UserPlayerPrefs.TokenData.Access)) // Because Authenticate is dependent on this
            {
                MyDebug.LogWarning("Access token is not valid. Trying to refresh tokens...");
                await Authenticate.RefreshTokenIfNeeded(true);
            }

            using UnityWebRequest request = UnityWebRequest.Get(GetPlayerInfoUrl);
            request.timeout = UserPlayerPrefs.RequestTimeout;

            // Set authorization header
            var authHeader = Authenticate.GetAuthorizationHeaderString();
            request.SetRequestHeader("Authorization", authHeader);
            request.SetRequestHeader("Accept", "application/json");

            await DataUtils.ExecuteUnityWebRequestWithTimeout(request, UserPlayerPrefs.RequestTimeout);

            if (request.result != UnityWebRequest.Result.Success)
            {
                var errorMessage = $"Request failed: {request.error ?? "Unknown error"} (Code: {request.responseCode})";

                if (request.result == UnityWebRequest.Result.ConnectionError)
                {
                    throw new SoilException($"Network error while fetching player info: {errorMessage}",
                        SoilExceptionErrorCode.TransportError);
                }
                else if (request.result == UnityWebRequest.Result.ProtocolError)
                {
                    var errorResponseText = request.downloadHandler?.text ?? "No response content";
                    throw new SoilException($"Server returned error {request.responseCode}: {errorResponseText}",
                        SoilExceptionErrorCode.TransportError);
                }
                else
                {
                    throw new SoilException($"Unexpected error while fetching player info: {errorMessage}",
                        SoilExceptionErrorCode.TransportError);
                }
            }

            var responseText = request.downloadHandler?.text ?? "";
            var fetchedUser = JsonConvert.DeserializeObject<UserInfo>(responseText);
            if (fetchedUser == null)
            {
                throw new SoilException("Failed to deserialize user info response",
                    SoilExceptionErrorCode.TransportError);
            }

            ReplaceUser(fetchedUser, UserPlayerPrefs.TokenData);
            MyDebug.Verbose($"Player info fetched successfully. Response: {UserPlayerPrefs.UserInfo.uuid}");
            return UserPlayerPrefs.UserInfo;
        }

        [ItemNotNull]
        [UsedImplicitly]
        public static async UniTask<UserInfo> UpdatePlayerInfoAsync(UserInfo userInfo)
        {
            if (!SoilServices.Ready)
            {
                throw new SoilException("SoilServices is not initialized. Cannot update player info.",
                    SoilExceptionErrorCode.NotReady);
            }

            var legalFields = UserPlayerPrefs.UserInfo.GetChangedFields(userInfo);
            if (legalFields.Count == 0)
            {
                MyDebug.Verbose("No legal fields to update.");
                return userInfo;
            }

            var stringBody = JsonConvert.SerializeObject(legalFields);
            byte[] bodyData = Encoding.UTF8.GetBytes(stringBody);

            using UnityWebRequest request = new UnityWebRequest(GetPlayerInfoUrl, "POST");
            request.uploadHandler = new UploadHandlerRaw(bodyData);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = UserPlayerPrefs.RequestTimeout;

            // Set headers
            var authHeader = Authenticate.GetAuthorizationHeaderString();
            request.SetRequestHeader("Authorization", authHeader);
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");

            await DataUtils.ExecuteUnityWebRequestWithTimeout(request, UserPlayerPrefs.RequestTimeout);

            if (request.result != UnityWebRequest.Result.Success)
            {
                var errorMessage = $"Request failed: {request.error ?? "Unknown error"} (Code: {request.responseCode})";

                if (request.result == UnityWebRequest.Result.ConnectionError)
                {
                    throw new SoilException($"Network error while updating player info: {errorMessage}",
                        SoilExceptionErrorCode.TransportError);
                }
                else if (request.result == UnityWebRequest.Result.ProtocolError)
                {
                    var errorResponseText = request.downloadHandler?.text ?? "No response content";
                    throw new SoilException($"Server returned error {request.responseCode}: {errorResponseText}",
                        SoilExceptionErrorCode.TransportError);
                }
                else
                {
                    throw new SoilException($"Unexpected error while updating player info: {errorMessage}",
                        SoilExceptionErrorCode.TransportError);
                }
            }

            var responseText = request.downloadHandler?.text ?? "";
            var updatedUser = JsonConvert.DeserializeObject<UserInfo>(responseText);
            if (updatedUser == null)
            {
                throw new SoilException("Failed to deserialize updated user info response",
                    SoilExceptionErrorCode.TransportError);
            }

            ReplaceUser(updatedUser, UserPlayerPrefs.TokenData);
            MyDebug.Verbose($"Player info updated successfully. Response: {UserPlayerPrefs.UserInfo.uuid}");
            return UserPlayerPrefs.UserInfo;
        }

        public static void ReplaceUser(UserInfo linkResponseAlternateUser, TokenData tokens)
        {
            if (linkResponseAlternateUser == null)
            {
                throw new SoilException("Link response alternate user is null", SoilExceptionErrorCode.InvalidResponse);
            }
            var userIsDifferent = UserPlayerPrefs.UserInfo.uuid != linkResponseAlternateUser.uuid;

            linkResponseAlternateUser.Validate();
            tokens.Validate();
            UserPlayerPrefs.UserInfo = userIsDifferent ? UserPlayerPrefs.UserInfo.ChangeUser(linkResponseAlternateUser) : linkResponseAlternateUser;
            UserPlayerPrefs.TokenData = UserPlayerPrefs.TokenData.ChangeTokenData(tokens);
            OnUserFilled?.Invoke(userIsDifferent);
        }

        public static void ReplaceRegionInfo(UserInfo comingUser)
        {
            if (comingUser == null)
            {
                throw new SoilException("Coming user is null", SoilExceptionErrorCode.InvalidResponse);
            }
            comingUser.Validate();

            if (UserPlayerPrefs.UserInfo == null)
            {
                throw new SoilException("UserPlayerPrefs.UserInfo is null", SoilExceptionErrorCode.InvalidResponse);
            }

            UserPlayerPrefs.UserInfo = UserPlayerPrefs.UserInfo.ChangeRegionInfo(comingUser);
        }
    }
}