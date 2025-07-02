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
        private static string GetPlayerInfoUrl => $"{Authenticate.UserBaseUrl}/";
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

            using var fetchClient = new HttpClient();
            fetchClient.Timeout = TimeSpan.FromSeconds(UserPlayerPrefs.RequestTimeout);
            fetchClient.DefaultRequestHeaders.Authorization = Authenticate.GetAuthorizationHeader();
            fetchClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var request = new HttpRequestMessage(HttpMethod.Get, GetPlayerInfoUrl);

            HttpResponseMessage response;
            string responseString;
            
            try
            {
                response = await fetchClient.SendAsync(request);
                responseString = await response.Content.ReadAsStringAsync();
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                throw new SoilException("Request timed out while fetching player info", 
                    SoilExceptionErrorCode.TransportError);
            }
            catch (HttpRequestException ex)
            {
                throw new SoilException($"Network error while fetching player info: {ex.Message}", 
                    SoilExceptionErrorCode.TransportError);
            }
            catch (Exception ex)
            {
                throw new SoilException($"Unexpected error while fetching player info: {ex.Message}", 
                    SoilExceptionErrorCode.TransportError);
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new SoilException($"Server returned error {response.StatusCode}: {responseString}",
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

            using var updateClient = new HttpClient();
            updateClient.Timeout = TimeSpan.FromSeconds(UserPlayerPrefs.RequestTimeout);
            updateClient.DefaultRequestHeaders.Authorization = Authenticate.GetAuthorizationHeader();
            updateClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var request = new HttpRequestMessage(HttpMethod.Post, GetPlayerInfoUrl);
            request.Content = new StringContent(stringBody, System.Text.Encoding.UTF8, "application/json");

            HttpResponseMessage response;
            string responseString;
            
            try
            {
                response = await updateClient.SendAsync(request);
                responseString = await response.Content.ReadAsStringAsync();
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                throw new SoilException("Request timed out while updating player info", 
                    SoilExceptionErrorCode.TransportError);
            }
            catch (HttpRequestException ex)
            {
                throw new SoilException($"Network error while updating player info: {ex.Message}", 
                    SoilExceptionErrorCode.TransportError);
            }
            catch (Exception ex)
            {
                throw new SoilException($"Unexpected error while updating player info: {ex.Message}", 
                    SoilExceptionErrorCode.TransportError);
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new SoilException($"Server returned error {response.StatusCode}: {responseString}",
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

        public static void ReplaceRegionInfo(UserInfo comingUser)
        {
            if (comingUser == null)
            {
                throw new SoilException("Coming user is null", SoilExceptionErrorCode.InvalidResponse);
            }
            comingUser.Validate();
            
            UserPlayerPrefs.UserInfo = UserPlayerPrefs.UserInfo.ChangeRegionInfo(comingUser);
        }
    }
}