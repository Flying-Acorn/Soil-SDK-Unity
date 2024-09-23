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
        private static readonly string UserBaseUrl = $"{Constants.ApiUrl}/users";

        private static readonly string RegisterPlayerUrl = $"{UserBaseUrl}/register/";
        private static readonly string RefreshTokenUrl = $"{UserBaseUrl}/refreshtoken/";
        private static readonly string GetPlayerInfoUrl = $"{UserBaseUrl}/";
        
        [UsedImplicitly] public static Action<TokenData> OnTokenRefreshed;
        [UsedImplicitly] public static Action<TokenData> OnUserRegistered;
        [UsedImplicitly] public static Action<UserInfo> OnPlayerInfoFetched;
        [UsedImplicitly] public static Action<UserInfo> OnUserReady;

        public static async Task AuthenticateUser(bool forceRegister = false,
            bool forceRefresh = false, bool forceFetchPlayerInfo = false)
        {
            if (forceRegister || AuthenticatePlayerPrefs.TokenData == null ||
                string.IsNullOrEmpty(AuthenticatePlayerPrefs.TokenData.Access) ||
                string.IsNullOrEmpty(AuthenticatePlayerPrefs.TokenData.Refresh))
                await RegisterPlayer();
            else
                await RefreshTokenIfNeeded(forceRefresh);

            var currentPlayerInfo = AuthenticatePlayerPrefs.UserInfo;
            if (forceFetchPlayerInfo || currentPlayerInfo == null || string.IsNullOrEmpty(currentPlayerInfo.uuid))
                await FetchPlayerInfo();
            
            OnUserReady?.Invoke(AuthenticatePlayerPrefs.UserInfo);
        }

        private static async Task RegisterPlayer()
        {
            Debug.Log("Registering player...");
            var appID = AuthenticatePlayerPrefs.AppID;
            var sdkToken = AuthenticatePlayerPrefs.SDKToken;

            var payload = new Dictionary<string, string>
            {
                { "iss", appID }
            };
            var bearerToken = JwtUtils.GenerateJwt(payload, sdkToken);
            var body = UserInfo.Properties.GeneratePropertiesFromDevice();
            var stringBody = JsonConvert.SerializeObject(new Dictionary<string, object> { { "properties", body } });
            var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var request = new HttpRequestMessage(HttpMethod.Post, RegisterPlayerUrl);
            request.Content = new StringContent(stringBody, Encoding.UTF8, "application/json");
            var response = await client.SendAsync(request);
            var responseString = response.Content.ReadAsStringAsync().Result;


            if (!response.IsSuccessStatusCode)
                Debug.LogError(responseString);
            else
                try
                {
                    AuthenticatePlayerPrefs.TokenData = JsonConvert.DeserializeObject<TokenData>(responseString);
                    Debug.Log($"Player registered successfully. Response: {responseString}");
                    OnUserRegistered?.Invoke(AuthenticatePlayerPrefs.TokenData);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error: {e.Message}");
                }
        }

        private static async Task FetchPlayerInfo()
        {
            Debug.Log("Fetching player info...");

            if (!JwtUtils.IsTokenValid(AuthenticatePlayerPrefs.TokenData.Access))
            {
                Debug.LogError("Access token is not valid. Trying to refresh tokens...");
                await RefreshTokenIfNeeded(true);
            }


            var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = GetAuthorizationHeader();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var request = new HttpRequestMessage(HttpMethod.Get, GetPlayerInfoUrl);

            var response = await client.SendAsync(request);
            var responseString = response.Content.ReadAsStringAsync().Result;

            if (!response.IsSuccessStatusCode)
                Debug.LogError(responseString);
            else
                try
                {
                    AuthenticatePlayerPrefs.UserInfo = JsonConvert.DeserializeObject<UserInfo>(responseString);
                    Debug.Log($"Player info fetched successfully. Response: {responseString}");
                    OnPlayerInfoFetched?.Invoke(AuthenticatePlayerPrefs.UserInfo);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error: {e.Message}");
                }
        }

        private static async Task RefreshTokenIfNeeded(bool force)
        {
            Debug.Log("Refreshing tokens...");

            if (!force && JwtUtils.IsTokenValid(AuthenticatePlayerPrefs.TokenData.Access))
            {
                Debug.Log("Access token is still valid. No need to refresh.");
                return;
            }

            if (!JwtUtils.IsTokenValid(AuthenticatePlayerPrefs.TokenData.Refresh))
            {
                Debug.LogError("Refresh token is almost expired. Please re-register.");
                return;
            }

            var stringBody = JsonConvert.SerializeObject(new Dictionary<string, string>
            {
                { "refresh", AuthenticatePlayerPrefs.TokenData.Refresh }
            });


            var client = new HttpClient();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            var request = new HttpRequestMessage(HttpMethod.Post, RefreshTokenUrl);
            request.Content = new StringContent(stringBody, Encoding.UTF8, "application/json");
            var response = await client.SendAsync(request);
            var responseString = response.Content.ReadAsStringAsync().Result;

            if (!response.IsSuccessStatusCode)
                Debug.LogError(responseString);
            else
                try
                {
                    AuthenticatePlayerPrefs.TokenData = JsonConvert.DeserializeObject<TokenData>(responseString);
                    Debug.Log($"Tokens refreshed successfully. Response: {responseString}");
                    OnTokenRefreshed?.Invoke(AuthenticatePlayerPrefs.TokenData);
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error: {e.Message}");
                }
        }

        public static AuthenticationHeaderValue GetAuthorizationHeader()
        {
            return new AuthenticationHeaderValue("Bearer", AuthenticatePlayerPrefs.TokenData.Access);
        }
    }
}