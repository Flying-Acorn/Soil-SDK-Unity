using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading.Tasks;
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
    public static class Socialization
    {
        [UsedImplicitly] public static bool Ready => SoilServices.Ready;
        private static readonly string SocializationBaseUrl = $"{Core.Data.Constants.ApiUrl}/socialization/";
        private static readonly string FriendsUrl = $"{SocializationBaseUrl}friends/";
        private static readonly string FriendsLeaderboardUrl = $"{SocializationBaseUrl}getfriendleaderboard/";

        public static async Task Initialize()
        {
            await SoilServices.Initialize();
        }

        public static async Task<FriendsResponse> GetFriends()
        {
            await Initialize();
            
            using var friendshipClient = new HttpClient();
            friendshipClient.Timeout = TimeSpan.FromSeconds(UserPlayerPrefs.RequestTimeout);
            friendshipClient.DefaultRequestHeaders.Authorization = Authenticate.GetAuthorizationHeader();
            friendshipClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var request = new HttpRequestMessage(HttpMethod.Get, FriendsUrl);
            
            HttpResponseMessage response;
            string responseString;
            
            try
            {
                response = await friendshipClient.SendAsync(request);
                responseString = await response.Content.ReadAsStringAsync();
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                throw new SoilException("Request timed out while getting friends", 
                    SoilExceptionErrorCode.TransportError);
            }
            catch (HttpRequestException ex)
            {
                throw new SoilException($"Network error while getting friends: {ex.Message}", 
                    SoilExceptionErrorCode.TransportError);
            }
            catch (Exception ex)
            {
                throw new SoilException($"Unexpected error while getting friends: {ex.Message}", 
                    SoilExceptionErrorCode.TransportError);
            }

            // Check HTTP status code
            if (!response.IsSuccessStatusCode)
            {
                throw new SoilException($"Server returned error {response.StatusCode}: {responseString}", 
                    SoilExceptionErrorCode.TransportError);
            }

            FriendsResponse firendshipResponse;
            try
            {
                firendshipResponse = JsonConvert.DeserializeObject<FriendsResponse>(responseString);
            }
            catch (Exception)
            {
                throw new SoilException($"Invalid response format while getting friends. Response: {responseString}",
                    SoilExceptionErrorCode.TransportError);
            }

            if (firendshipResponse.detail.code == Constants.FriendshipStatus.FriendshipExists &&
                (firendshipResponse.friends == null || firendshipResponse.friends.Count == 0))
                throw new SoilException("Problem with getting friends. No friends found", SoilExceptionErrorCode.InvalidResponse);

            return firendshipResponse;
        }

        public static async Task<FriendsResponse> AddFriendWithUUID(string uuid)
        {
            if (string.IsNullOrEmpty(uuid))
                throw new SoilException("UUID cannot be null or empty", SoilExceptionErrorCode.InvalidRequest);
            
            await Initialize();
            
            using var friendshipClient = new HttpClient();
            friendshipClient.Timeout = TimeSpan.FromSeconds(UserPlayerPrefs.RequestTimeout);
            friendshipClient.DefaultRequestHeaders.Authorization = Authenticate.GetAuthorizationHeader();
            friendshipClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            
            var request = new HttpRequestMessage(HttpMethod.Post, FriendsUrl);
            var body = new Dictionary<string, object>
            {
                { "uuid", uuid }
            };
            request.Content = new StringContent(JsonConvert.SerializeObject(body));
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            
            HttpResponseMessage response;
            string responseString;
            
            try
            {
                response = await friendshipClient.SendAsync(request);
                responseString = await response.Content.ReadAsStringAsync();
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                throw new SoilException("Request timed out while adding friend", 
                    SoilExceptionErrorCode.TransportError);
            }
            catch (HttpRequestException ex)
            {
                throw new SoilException($"Network error while adding friend: {ex.Message}", 
                    SoilExceptionErrorCode.TransportError);
            }
            catch (Exception ex)
            {
                throw new SoilException($"Unexpected error while adding friend: {ex.Message}", 
                    SoilExceptionErrorCode.TransportError);
            }

            // Check HTTP status code
            if (!response.IsSuccessStatusCode)
            {
                throw new SoilException($"Server returned error {response.StatusCode}: {responseString}", 
                    SoilExceptionErrorCode.TransportError);
            }

            FriendsResponse friendshipResponse;
            try
            {
                friendshipResponse = JsonConvert.DeserializeObject<FriendsResponse>(responseString);
            }
            catch (Exception)
            {
                throw new SoilException($"Invalid response format while adding friend. Response: {responseString}",
                    SoilExceptionErrorCode.TransportError);
            }

            return friendshipResponse;
        }

        public static async Task<FriendsResponse> RemoveFriendWithUUID(string uuid)
        {
            if (string.IsNullOrEmpty(uuid))
                throw new SoilException("UUID cannot be null or empty", SoilExceptionErrorCode.InvalidRequest);
            
            await Initialize();
            
            using var friendshipClient = new HttpClient();
            friendshipClient.Timeout = TimeSpan.FromSeconds(UserPlayerPrefs.RequestTimeout);
            friendshipClient.DefaultRequestHeaders.Authorization = Authenticate.GetAuthorizationHeader();
            friendshipClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var request = new HttpRequestMessage(HttpMethod.Delete, FriendsUrl);
            var body = new Dictionary<string, object>
            {
                { "uuid", uuid }
            };
            request.Content = new StringContent(JsonConvert.SerializeObject(body));
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
            
            HttpResponseMessage response;
            string responseString;
            
            try
            {
                response = await friendshipClient.SendAsync(request);
                responseString = await response.Content.ReadAsStringAsync();
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                throw new SoilException("Request timed out while removing friend", 
                    SoilExceptionErrorCode.TransportError);
            }
            catch (HttpRequestException ex)
            {
                throw new SoilException($"Network error while removing friend: {ex.Message}", 
                    SoilExceptionErrorCode.TransportError);
            }
            catch (Exception ex)
            {
                throw new SoilException($"Unexpected error while removing friend: {ex.Message}", 
                    SoilExceptionErrorCode.TransportError);
            }

            // Check HTTP status code
            if (!response.IsSuccessStatusCode)
            {
                throw new SoilException($"Server returned error {response.StatusCode}: {responseString}", 
                    SoilExceptionErrorCode.TransportError);
            }

            FriendsResponse firendshipResponse;
            try
            {
                firendshipResponse = JsonConvert.DeserializeObject<FriendsResponse>(responseString);
            }
            catch (Exception)
            {
                throw new SoilException($"Invalid response format while removing friend. Response: {responseString}",
                    SoilExceptionErrorCode.TransportError);
            }

            return firendshipResponse;
        }

        public static async Task<List<UserScore>> GetFriendsLeaderboard(string leaderboardId, int count = 10,
            bool relative = false)
        {
            if (string.IsNullOrEmpty(leaderboardId))
                throw new SoilException("Leaderboard ID cannot be null or empty",
                    SoilExceptionErrorCode.InvalidRequest);
            await Initialize();
            var payload = new Dictionary<string, object>
            {
                { "leaderboard_identifier", leaderboardId },
                { "count", count },
                { "relative", relative },
            };

            var stringBody = JsonConvert.SerializeObject(payload);

            using var fetchClient = new HttpClient();
            fetchClient.Timeout = TimeSpan.FromSeconds(UserPlayerPrefs.RequestTimeout);
            fetchClient.DefaultRequestHeaders.Authorization = Authenticate.GetAuthorizationHeader();
            var request = new HttpRequestMessage(HttpMethod.Post, FriendsLeaderboardUrl);
            request.Content = new StringContent(stringBody, Encoding.UTF8, "application/json");
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            
            HttpResponseMessage response;
            string responseString;
            
            try
            {
                response = await fetchClient.SendAsync(request);
                responseString = await response.Content.ReadAsStringAsync();
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                throw new SoilException("Request timed out while fetching friends leaderboard", 
                    SoilExceptionErrorCode.TransportError);
            }
            catch (HttpRequestException ex)
            {
                throw new SoilException($"Network error while fetching friends leaderboard: {ex.Message}", 
                    SoilExceptionErrorCode.TransportError);
            }
            catch (Exception ex)
            {
                throw new SoilException($"Unexpected error while fetching friends leaderboard: {ex.Message}", 
                    SoilExceptionErrorCode.TransportError);
            }

            if (response is not { IsSuccessStatusCode: true })
            {
                throw new SoilException($"Server returned error {response.StatusCode}: {responseString}",
                    SoilExceptionErrorCode.TransportError);
            }

            var leaderboard = JsonConvert.DeserializeObject<List<UserScore>>(responseString);
            SocializationPlayerPrefs.SetCachedLeaderboardData(leaderboardId, responseString, relative);
            return leaderboard;
        }
    }
}