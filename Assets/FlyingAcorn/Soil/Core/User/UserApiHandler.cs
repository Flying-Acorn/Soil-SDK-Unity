using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using FlyingAcorn.Soil.Core.Data;
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
            
            if (UserPlayerPrefs.TokenData == null || string.IsNullOrEmpty(UserPlayerPrefs.TokenData.Access))
            {
                throw new Exception("Access token is missing. Abandoning the process.");
            }

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

            UserPlayerPrefs.UserInfo = JsonConvert.DeserializeObject<UserInfo>(responseString);
            Debug.Log($"Player info fetched successfully. Response: {UserPlayerPrefs.UserInfo.uuid}");
            return UserPlayerPrefs.UserInfo;
        }

        [ItemNotNull]
        [UsedImplicitly]
        public static async Task<UserInfo> UpdatePlayerInfo(UserInfo userInfo)
        {
            await SoilServices.Initialize();

            var legalFields = UserPlayerPrefs.UserInfo.GetChangedFields(userInfo);
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

            UserPlayerPrefs.UserInfo = JsonConvert.DeserializeObject<UserInfo>(responseString);
            Debug.Log($"Player info updated successfully. Response: {UserPlayerPrefs.UserInfo.uuid}");
            return UserPlayerPrefs.UserInfo;
        }

        public static void ReplaceUser(UserInfo linkResponseAlternateUser, TokenData tokens)
        {
            linkResponseAlternateUser.Validate();
            tokens.Validate();
            UserPlayerPrefs.UserInfo = UserPlayerPrefs.UserInfo.ChangeUser(linkResponseAlternateUser);
            UserPlayerPrefs.TokenData = UserPlayerPrefs.TokenData.ChangeTokenData(tokens);
        }
    }
}