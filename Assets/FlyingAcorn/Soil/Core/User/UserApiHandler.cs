using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using FlyingAcorn.Soil.Core.Data;
using FlyingAcorn.Soil.Core.JWTTools;
using FlyingAcorn.Soil.Core.User.Authentication;
using JetBrains.Annotations;
using Newtonsoft.Json;
using UnityEngine;

namespace FlyingAcorn.Soil.Core.User
{
    public static class UserApiHandler
    {
        private static readonly string GetPlayerInfoUrl = $"{Authenticate.UserBaseUrl}/";
        private static HttpClient _fetchClient;
        private static HttpClient _updateClient;
        internal static Action<bool> OnUserFilled; // True means user is changed

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

            _fetchClient?.Dispose();
            _fetchClient = new HttpClient();
            _fetchClient.DefaultRequestHeaders.Authorization = Authenticate.GetAuthorizationHeader();
            _fetchClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var request = new HttpRequestMessage(HttpMethod.Get, GetPlayerInfoUrl);

            var response = await _fetchClient.SendAsync(request);
            var responseString = response.Content.ReadAsStringAsync().Result;

            if (!response.IsSuccessStatusCode)
            {
                throw new SoilException($"Network error while fetching player info. Response: {responseString}",
                    SoilExceptionErrorCode.TransportError);
            }
            
            var fetchedUser = JsonConvert.DeserializeObject<UserInfo>(responseString);
            ReplaceUser(fetchedUser, UserPlayerPrefs.TokenData);
            Debug.Log($"Player info fetched successfully. Response: {UserPlayerPrefs.UserInfo.uuid}");
            return UserPlayerPrefs.UserInfo;
        }

        [ItemNotNull]
        [UsedImplicitly]
        public static async Task<UserInfo> UpdatePlayerInfoAsync(UserInfo userInfo)
        {
            await SoilServices.Initialize();

            var legalFields = UserPlayerPrefs.UserInfo.GetChangedFields(userInfo);
            if (legalFields.Count == 0)
            {
                Debug.Log("No legal fields to update.");
                return userInfo;
            }

            var stringBody = JsonConvert.SerializeObject(legalFields);

            // _updateClient?.Dispose(); // Uncomment to prevent async
            _updateClient = new HttpClient();
            _updateClient.DefaultRequestHeaders.Authorization = Authenticate.GetAuthorizationHeader();
            _updateClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var request = new HttpRequestMessage(HttpMethod.Post, GetPlayerInfoUrl);
            request.Content = new StringContent(stringBody, System.Text.Encoding.UTF8, "application/json");

            var response = await _updateClient.SendAsync(request);
            var responseString = response.Content.ReadAsStringAsync().Result;

            if (!response.IsSuccessStatusCode)
            {
                throw new SoilException($"Network error while updating player info. Response: {responseString}",
                    SoilExceptionErrorCode.TransportError);
            }

            var updatedUser = JsonConvert.DeserializeObject<UserInfo>(responseString);
            ReplaceUser(updatedUser, UserPlayerPrefs.TokenData);
            Debug.Log($"Player info updated successfully. Response: {UserPlayerPrefs.UserInfo.uuid}");
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
    }
}