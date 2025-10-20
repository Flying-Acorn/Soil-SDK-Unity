using System;
using System.Text;
using Cysharp.Threading.Tasks;
using FlyingAcorn.Analytics;
using FlyingAcorn.Soil.Core.Data;
using FlyingAcorn.Soil.Core.JWTTools;
using FlyingAcorn.Soil.Core.User.Authentication;
using JetBrains.Annotations;
using Newtonsoft.Json;
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
        internal static async UniTask<UserInfo> FetchPlayerInfo(bool allowDuringInitialization = false)
        {
            if (!SoilServices.Ready && !allowDuringInitialization)
                throw new SoilException("SoilServices is not initialized. Cannot fetch player info.", SoilExceptionErrorCode.NotReady);

            if (_fetchPlayerInfoTask?.AsTask() is { IsCompletedSuccessfully: false, IsCompleted: true })
                _fetchPlayerInfoTask = null;

            if (_fetchPlayerInfoTask == null)
            {
                _fetchPlayerInfoTask = FetchPlayerInfoInternal();
            }
            else if (!_fetchPlayerInfoTask?.AsTask().IsCompleted ?? false)
            {
                MyDebug.Info("Player info fetch in progress, sharing request");
            }

            try
            {
                return await _fetchPlayerInfoTask.Value;
            }
            catch (Exception)
            {
                if (_fetchPlayerInfoTask?.AsTask() != null && _fetchPlayerInfoTask.Value.AsTask().IsFaulted)
                {
                    _fetchPlayerInfoTask = null;
                }
                throw;
            }
        }

        private static async UniTask<UserInfo> FetchPlayerInfoInternal()
        {
            MyDebug.Info("Fetching player info");

            if (UserPlayerPrefs.TokenData == null || string.IsNullOrEmpty(UserPlayerPrefs.TokenData.Access))
            {
                throw new Exception("Access token is missing. Abandoning the process.");
            }

            if (!JwtUtils.IsTokenValid(UserPlayerPrefs.TokenData.Access))
            {
                MyDebug.LogWarning("Access token invalid, refreshing");
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
            MyDebug.Info($"Player info fetched successfully. Response: {UserPlayerPrefs.UserInfo.uuid}");
            return UserPlayerPrefs.UserInfo;
        }

        /// <summary>
        /// Updates player information by passing a UserInfo object.
        /// WARNING: If you pass SoilServices.UserInfo directly without copying, 
        /// no changes will be detected. Use UpdatePlayerInfo() builder methods or Copy() first.
        /// </summary>
        /// <param name="userInfo">The modified user info. Should be a copy, not the original reference.</param>
        /// <returns>The updated UserInfo from the server</returns>
        [ItemNotNull]
        [UsedImplicitly]
        internal static async UniTask<UserInfo> UpdatePlayerInfoAsync(UserInfo userInfo)
        {
            if (!SoilServices.Ready)
            {
                throw new SoilException("SoilServices is not initialized. Cannot update player info.",
                    SoilExceptionErrorCode.NotReady);
            }

            // SAFETY CHECK: Ensure we're not modifying the original stored UserInfo
            // If the passed userInfo is the same reference as the stored one, create a copy
            if (ReferenceEquals(userInfo, UserPlayerPrefs.UserInfo))
            {
                MyDebug.LogWarning("UpdatePlayerInfoAsync: Detected same object reference as stored UserInfo. " +
                                  "This suggests you may have modified SoilServices.UserInfo directly. " +
                                  "Consider using UpdatePlayerInfoAsync() with fluent builder methods instead.");
                
                // We'll continue but this indicates a potential issue in usage
            }

            var legalFields = UserPlayerPrefs.UserInfo.GetChangedFields(userInfo);
            if (legalFields.Count == 0)
            {
                MyDebug.Info("No legal fields to update.");
                return userInfo;
            }

            var stringBody = JsonConvert.SerializeObject(legalFields);
            byte[] bodyData = Encoding.UTF8.GetBytes(stringBody);

            using UnityWebRequest request = new UnityWebRequest(GetPlayerInfoUrl, "POST");
            request.uploadHandler = new UploadHandlerRaw(bodyData);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = UserPlayerPrefs.RequestTimeout;
            MyDebug.Info($"Updating player info with data: {stringBody}");

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
            MyDebug.Info($"Player info updated successfully. Response: {UserPlayerPrefs.UserInfo.uuid}");
            return UserPlayerPrefs.UserInfo;
        }

        /// <summary>
        /// Creates a fluent builder for updating player info safely.
        /// This automatically creates a copy of the current UserInfo to avoid reference issues.
        /// </summary>
        /// <returns>A UserInfoUpdateBuilder for fluent API usage</returns>
        [ItemNotNull]
        [UsedImplicitly]
        public static UserInfoUpdateBuilder UpdatePlayerInfo()
        {
            if (!SoilServices.Ready)
            {
                throw new SoilException("SoilServices is not initialized. Cannot update player info.",
                    SoilExceptionErrorCode.NotReady);
            }

            return new UserInfoUpdateBuilder(UserPlayerPrefs.UserInfo.Copy());
        }

        /// <summary>
        /// Fluent builder for safely updating UserInfo without reference issues
        /// </summary>
        public class UserInfoUpdateBuilder
        {
            private readonly UserInfo _userInfo;

            internal UserInfoUpdateBuilder(UserInfo userInfo)
            {
                _userInfo = userInfo;
            }

            public UserInfoUpdateBuilder WithName(string name)
            {
                _userInfo.RecordName(name);
                return this;
            }

            public UserInfoUpdateBuilder WithUsername(string username)
            {
                _userInfo.RecordUsername(username);
                return this;
            }

            public UserInfoUpdateBuilder WithAvatarAsset(string avatarAsset)
            {
                _userInfo.RecordAvatarAsset(avatarAsset);
                return this;
            }

            public UserInfoUpdateBuilder WithCustomProperty(string key, object value)
            {
                _userInfo.RecordCustomProperty(key, value);
                return this;
            }

            /// <summary>
            /// Executes the update with all the accumulated changes.
            /// Note: You can also await the builder directly without calling this method.
            /// </summary>
            /// <returns>The updated UserInfo</returns>
            public async UniTask<UserInfo> ExecuteAsync()
            {
                return await UpdatePlayerInfoAsync(_userInfo);
            }

            /// <summary>
            /// Gets the modified UserInfo without executing the update.
            /// Useful for inspecting changes before committing.
            /// </summary>
            /// <returns>The modified UserInfo</returns>
            public UserInfo GetModified()
            {
                return _userInfo;
            }

            /// <summary>
            /// Allows the builder to be awaited directly without calling ExecuteAsync()
            /// </summary>
            public UniTask<UserInfo>.Awaiter GetAwaiter()
            {
                return ExecuteAsync().GetAwaiter();
            }
        }

        internal static void ReplaceUser(UserInfo linkResponseAlternateUser, TokenData tokens)
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

        internal static void ReplaceRegionInfo(UserInfo comingUser)
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