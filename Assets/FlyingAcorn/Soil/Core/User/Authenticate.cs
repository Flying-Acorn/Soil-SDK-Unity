using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
using FlyingAcorn.Soil.Core.Data;
using FlyingAcorn.Soil.Core.JWTTools;
using JetBrains.Annotations;
using Newtonsoft.Json;
using UnityEngine;

namespace FlyingAcorn.Soil.Core.User
{
    public static class Authenticate
    {
        internal static readonly string UserBaseUrl = $"{Constants.ApiUrl}/users";

        private static readonly string RegisterPlayerUrl = $"{UserBaseUrl}/register/";
        private static readonly string RefreshTokenUrl = $"{UserBaseUrl}/refreshtoken/";

        [UsedImplicitly] public static Action<TokenData> OnTokenRefreshed;
        [UsedImplicitly] public static Action<TokenData> OnUserRegistered;
        [UsedImplicitly] public static Action<UserInfo> OnPlayerInfoFetched;
        [UsedImplicitly] public static Action<UserInfo> OnUserReady;

        public static async Task AuthenticateUser(bool forceRegister = false,
            bool forceRefresh = false, bool forceFetchPlayerInfo = false)
        {
            if (forceRegister || UserPlayerPrefs.TokenData == null ||
                string.IsNullOrEmpty(UserPlayerPrefs.TokenData.Access) ||
                string.IsNullOrEmpty(UserPlayerPrefs.TokenData.Refresh))
            {
                try
                {
                    await RegisterPlayer();
                }
                catch (Exception e)
                {
                    throw new Exception($"Failed to register player: {e.Message}, abandoning the process.");
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

            var currentPlayerInfo = UserPlayerPrefs.UserInfo;
            if (forceFetchPlayerInfo || currentPlayerInfo == null || string.IsNullOrEmpty(currentPlayerInfo.uuid))
            {
                try
                {
                    await UserApiHandler.FetchPlayerInfo();
                }
                catch (Exception e)
                {
                    Debug.LogError(e.Message);
                }
            }

            OnUserReady?.Invoke(UserPlayerPrefs.UserInfo);
        }

        private static async Task RegisterPlayer()
        {
            Debug.Log("Registering player...");
            var appID = UserPlayerPrefs.AppID;
            var sdkToken = UserPlayerPrefs.SDKToken;

            var payload = new Dictionary<string, string>
            {
                { "iss", appID }
            };
            var bearerToken = JwtUtils.GenerateJwt(payload, sdkToken);
            var body = UserInfo.Properties.GeneratePropertiesDynamicPlayerProperties();
            var stringBody = JsonConvert.SerializeObject(new Dictionary<string, object> { { "properties", body } });
            var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var request = new HttpRequestMessage(HttpMethod.Post, RegisterPlayerUrl);
            request.Content = new StringContent(stringBody, Encoding.UTF8, "application/json");
            var response = await client.SendAsync(request);
            var responseString = response.Content.ReadAsStringAsync().Result;


            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Network error while registering player. Response: {responseString}");
            }

            UserPlayerPrefs.TokenData = JsonConvert.DeserializeObject<TokenData>(responseString);
            Debug.Log($"Player registered successfully. Response: {responseString}");
            OnUserRegistered?.Invoke(UserPlayerPrefs.TokenData);
        }

        internal static async Task RefreshTokenIfNeeded(bool force)
        {
            if (!force && JwtUtils.IsTokenValid(UserPlayerPrefs.TokenData.Access))
            {
                return;
            }

            if (!JwtUtils.IsTokenValid(UserPlayerPrefs.TokenData.Refresh))
            {
                Debug.LogError("Refresh token is almost expired. Please re-register.");
                return;
            }

            var stringBody = JsonConvert.SerializeObject(new Dictionary<string, string>
            {
                { "refresh", UserPlayerPrefs.TokenData.Refresh }
            });


            var client = new HttpClient();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var request = new HttpRequestMessage(HttpMethod.Post, RefreshTokenUrl);
            request.Content = new StringContent(stringBody, Encoding.UTF8, "application/json");
            var response = await client.SendAsync(request);
            var responseString = response.Content.ReadAsStringAsync().Result;

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Network error while refreshing tokens. Response: {responseString}");
            }

            UserPlayerPrefs.TokenData = JsonConvert.DeserializeObject<TokenData>(responseString);
            Debug.Log($"Tokens refreshed successfully. Response: {responseString}");
            OnTokenRefreshed?.Invoke(UserPlayerPrefs.TokenData);
        }

        public static AuthenticationHeaderValue GetAuthorizationHeader()
        {
            return new AuthenticationHeaderValue("Bearer", UserPlayerPrefs.TokenData.Access);
        }
    }
}