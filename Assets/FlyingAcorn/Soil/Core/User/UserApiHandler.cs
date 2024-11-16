using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using FlyingAcorn.Soil.Core.JWTTools;
using JetBrains.Annotations;
using Newtonsoft.Json;
using UnityEngine;

namespace FlyingAcorn.Soil.Core.User
{
    public static class UserApiHandler
    {
        private static readonly string GetPlayerInfoUrl = $"{Authenticate.UserBaseUrl}/";

        [ItemNotNull]
        [UsedImplicitly]
        public static async Task<UserInfo> FetchPlayerInfo()
        {
            Debug.Log("Fetching player info...");

            if (!JwtUtils.IsTokenValid(UserPlayerPrefs.TokenData.Access)) // Because Authenticate is dependent on this
            {
                Debug.LogWarning("Access token is not valid. Trying to refresh tokens...");
                await Authenticate.RefreshTokenIfNeeded(true);
            }

            var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = Authenticate.GetAuthorizationHeader();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var request = new HttpRequestMessage(HttpMethod.Get, GetPlayerInfoUrl);

            var response = await client.SendAsync(request);
            var responseString = response.Content.ReadAsStringAsync().Result;

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Network error while fetching player info. Response: {responseString}");
            }

            Debug.Log($"Player info fetched successfully. Response: {responseString}");
            UserPlayerPrefs.UserInfo = JsonConvert.DeserializeObject<UserInfo>(responseString);
            return UserPlayerPrefs.UserInfo;
        }

        [ItemNotNull]
        [UsedImplicitly]
        public static async Task<UserInfo> UpdatePlayerInfo(UserInfo userInfo)
        {
            Debug.Log("Updating player info...");
            try
            {
                Debug.LogWarning("Soil services are not ready. Trying to initialize...");
                await SoilServices.Initialize();
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to initialize Soil services: {e.Message}");
                throw;
            }


            var legalFields = new Dictionary<string, object>();
            foreach (var propertyInfo in userInfo.GetType().GetProperties())
            {
                var value = propertyInfo.GetValue(userInfo);
                var isNullOrEmpty = value == null || string.IsNullOrEmpty(value.ToString());
                if (isNullOrEmpty)
                    continue;
                var currentUser = UserPlayerPrefs.UserInfo;
                var unchanged = value.ToString() ==
                                currentUser.GetType().GetProperty(propertyInfo.Name)?.GetValue(currentUser)?.ToString();
                if (unchanged)
                    continue;
                legalFields.Add(propertyInfo.Name, value);
            }

            if (legalFields.Count == 0)
            {
                Debug.Log("No legal fields to update.");
                return userInfo;
            }

            var stringBody = JsonConvert.SerializeObject(legalFields);

            var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = Authenticate.GetAuthorizationHeader();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var request = new HttpRequestMessage(HttpMethod.Post, GetPlayerInfoUrl);
            request.Content = new StringContent(stringBody, System.Text.Encoding.UTF8, "application/json");

            var response = await client.SendAsync(request);
            var responseString = response.Content.ReadAsStringAsync().Result;

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Network error while updating player info. Response: {responseString}");
            }

            Debug.Log($"Player info updated successfully. Response: {responseString}");
            UserPlayerPrefs.UserInfo = JsonConvert.DeserializeObject<UserInfo>(responseString);
            return UserPlayerPrefs.UserInfo;
        }
    }
}