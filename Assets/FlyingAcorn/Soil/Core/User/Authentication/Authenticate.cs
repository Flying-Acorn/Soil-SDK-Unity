using System;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using FlyingAcorn.Analytics;
using FlyingAcorn.Soil.Core.Data;
using FlyingAcorn.Soil.Core.JWTTools;
using JetBrains.Annotations;
using Newtonsoft.Json;
using UnityEngine.Networking;

namespace FlyingAcorn.Soil.Core.User.Authentication
{
    public static class Authenticate
    {
        internal static string UserBaseUrl => $"{Core.Data.Constants.ApiUrl}/users";

        private static string RegisterPlayerUrl => $"{UserBaseUrl}/register/";
        private static string RefreshTokenUrl => $"{UserBaseUrl}/refreshtoken/";

        // Lock to prevent concurrent user registrations
        private static readonly SemaphoreSlim _registrationLock = new SemaphoreSlim(1, 1);
        private static bool _registrationInProgress = false;

        [UsedImplicitly] public static Action<TokenData> OnTokenRefreshed;
        [UsedImplicitly] public static Action<TokenData> OnUserRegistered;
        [UsedImplicitly] public static Action<UserInfo> OnPlayerInfoFetched;
        [UsedImplicitly] public static Action<UserInfo> OnUserReady;

        internal static async UniTask AuthenticateUser(bool forceRegister = false,
            bool forceRefresh = false, bool forceFetchPlayerInfo = false)
        {
            MyDebug.Verbose($"[AuthenticateUser] Starting authentication - forceRegister: {forceRegister}, forceRefresh: {forceRefresh}");
            
            var userIsMissing = UserPlayerPrefs.TokenData == null ||
                                string.IsNullOrEmpty(UserPlayerPrefs.TokenData.Access) ||
                                string.IsNullOrEmpty(UserPlayerPrefs.TokenData.Refresh);
            
            MyDebug.Verbose($"[AuthenticateUser] User missing check: {userIsMissing}");
            
            if (forceRegister || userIsMissing)
            {
                MyDebug.Verbose($"[AuthenticateUser] Acquiring registration lock...");
                await _registrationLock.WaitAsync();
                try
                {
                    // Double-check after acquiring lock - another thread might have registered
                    var userStillMissing = UserPlayerPrefs.TokenData == null ||
                                         string.IsNullOrEmpty(UserPlayerPrefs.TokenData.Access) ||
                                         string.IsNullOrEmpty(UserPlayerPrefs.TokenData.Refresh);
                    
                    MyDebug.Verbose($"[AuthenticateUser] Double-check after lock: userStillMissing={userStillMissing}, _registrationInProgress={_registrationInProgress}");
                    
                    if (forceRegister || userStillMissing)
                    {
                        if (_registrationInProgress)
                        {
                            MyDebug.Verbose("[AuthenticateUser] Registration already in progress - skipping duplicate registration");
                            return;
                        }

                        _registrationInProgress = true;
                        try
                        {
                            await RegisterPlayer();
                        }
                        finally
                        {
                            _registrationInProgress = false;
                        }
                    }
                    else
                    {
                        MyDebug.Verbose("[AuthenticateUser] User data available after acquiring registration lock - skipping registration");
                    }
                }
                catch (Exception e)
                {
                    _registrationInProgress = false;
                    throw new Exception($"Failed to register player: {e.Message}, abandoning the process.");
                }
                finally
                {
                    _registrationLock.Release();
                }
            }
            else
            {
                try
                {
                    await RefreshTokenIfNeeded(forceRefresh);
                }
                catch (Exception e)
                {
                    throw new Exception($"Failed to refresh tokens: {e.Message}, abandoning the process.");
                }
            }

            var currentPlayerInfo = UserPlayerPrefs.UserInfoInstance;
            var playerInfoIsMissing = currentPlayerInfo == null || string.IsNullOrEmpty(currentPlayerInfo.uuid);
            if (forceFetchPlayerInfo || playerInfoIsMissing)
            {
                try
                {
                    // Allow fetch during initialization prior to Ready being true
                    await UserApiHandler.FetchPlayerInfo(allowDuringInitialization: true);
                }
                catch (Exception e)
                {
                    if (playerInfoIsMissing || forceFetchPlayerInfo)
                        throw new Exception($"Failed to fetch player info: {e.Message}, abandoning the process.");
                    MyDebug.Info($"Failed to fetch player info: {e.Message}, continuing with the existing info.");
                }
            }

            OnUserReady?.Invoke(UserPlayerPrefs.UserInfoInstance);
        }

        private static async UniTask RegisterPlayer()
        {
            MyDebug.Verbose("Registering player...");
            MyDebug.Info($"[RegisterPlayer] Starting user registration process");
            
            var appID = UserPlayerPrefs.AppID;
            var sdkToken = UserPlayerPrefs.SDKToken;

            var payload = new Dictionary<string, string>
            {
                { "iss", appID }
            };
            var bearerToken = JwtUtils.GenerateJwt(payload, sdkToken);
            var body = UserInfo.Properties.GeneratePropertiesDynamicPlayerProperties();
            var stringBody = JsonConvert.SerializeObject(new Dictionary<string, object> { { "properties", body } });
            byte[] bodyData = Encoding.UTF8.GetBytes(stringBody);

            using UnityWebRequest request = new UnityWebRequest(RegisterPlayerUrl, "POST");
            request.uploadHandler = new UploadHandlerRaw(bodyData);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = UserPlayerPrefs.RequestTimeout;

            // Set headers
            request.SetRequestHeader("Authorization", $"Bearer {bearerToken}");
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");

            // Use UniTaskCompletionSource for proper async/await with UnityWebRequest
            await DataUtils.ExecuteUnityWebRequestWithTimeout(request, UserPlayerPrefs.RequestTimeout);

            if (request.result != UnityWebRequest.Result.Success)
            {
                var errorMessage = $"Request failed: {request.error ?? "Unknown error"} (Code: {request.responseCode})";

                if (request.result == UnityWebRequest.Result.ConnectionError)
                {
                    throw new SoilException($"Network error while registering player: {errorMessage}",
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
                    throw new SoilException($"Unexpected error while registering player: {errorMessage}",
                        SoilExceptionErrorCode.TransportError);
                }
            }

            var responseString = request.downloadHandler?.text ?? "";
            var tokenData = JsonConvert.DeserializeObject<TokenData>(responseString);
            if (tokenData == null)
            {
                throw new SoilException("Failed to deserialize token data from registration response",
                    SoilExceptionErrorCode.TransportError);
            }

            UserPlayerPrefs.TokenData = tokenData;
            MyDebug.Info($"Player registered successfully. Response: {responseString}");
            OnUserRegistered?.Invoke(UserPlayerPrefs.TokenData);
        }

        internal static async UniTask RefreshTokenIfNeeded(bool force = false)
        {
            if (!force && JwtUtils.IsTokenValid(UserPlayerPrefs.TokenData.Access))
            {
                return;
            }

            if (!JwtUtils.IsTokenValid(UserPlayerPrefs.TokenData.Refresh))
            {
                // TODO: Check user lifetime and re-register
                throw new SoilException("Refresh token is invalid. Re-register player.",
                    SoilExceptionErrorCode.InvalidRequest);
            }

            var stringBody = JsonConvert.SerializeObject(new Dictionary<string, string>
            {
                { "refresh", UserPlayerPrefs.TokenData.Refresh }
            });
            byte[] bodyData = Encoding.UTF8.GetBytes(stringBody);

            using UnityWebRequest request = new UnityWebRequest(RefreshTokenUrl, "POST");
            request.uploadHandler = new UploadHandlerRaw(bodyData);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.timeout = UserPlayerPrefs.RequestTimeout;

            // Set headers
            request.SetRequestHeader("Content-Type", "application/json");
            request.SetRequestHeader("Accept", "application/json");

            await DataUtils.ExecuteUnityWebRequestWithTimeout(request, UserPlayerPrefs.RequestTimeout);

            if (request.result != UnityWebRequest.Result.Success)
            {
                var errorMessage = $"Request failed: {request.error ?? "Unknown error"} (Code: {request.responseCode})";

                if (request.result == UnityWebRequest.Result.ConnectionError)
                {
                    throw new SoilException($"Network error while refreshing tokens: {errorMessage}",
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
                    throw new SoilException($"Unexpected error while refreshing tokens: {errorMessage}",
                        SoilExceptionErrorCode.TransportError);
                }
            }

            var responseString = request.downloadHandler?.text ?? "";
            var tokenData = JsonConvert.DeserializeObject<TokenData>(responseString);
            if (tokenData == null)
            {
                throw new SoilException("Failed to deserialize token data from refresh response",
                    SoilExceptionErrorCode.TransportError);
            }

            UserPlayerPrefs.TokenData = tokenData;
            MyDebug.Info($"Tokens refreshed successfully. Response: {responseString}");
            OnTokenRefreshed?.Invoke(UserPlayerPrefs.TokenData);
        }

        internal static AuthenticationHeaderValue GetAuthorizationHeader()
        {
            return new AuthenticationHeaderValue("Bearer", UserPlayerPrefs.TokenData.Access);
        }

        internal static string GetAuthorizationHeaderString()
        {
            return $"Bearer {UserPlayerPrefs.TokenData.Access}";
        }
    }
}