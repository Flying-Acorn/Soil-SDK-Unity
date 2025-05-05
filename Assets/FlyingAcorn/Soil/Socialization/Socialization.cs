using System;
using System.Collections.Generic;
using System.Net;
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
            var friendshipClient = new HttpClient();
            friendshipClient.Timeout = TimeSpan.FromSeconds(UserPlayerPrefs.RequestTimeout);
            friendshipClient.DefaultRequestHeaders.Authorization = Authenticate.GetAuthorizationHeader();
            friendshipClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var request = new HttpRequestMessage(HttpMethod.Get, FriendsUrl);
            var response = await friendshipClient.SendAsync(request);
            var responseString = response.Content.ReadAsStringAsync().Result;

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                SocializationPlayerPrefs.Friends = new List<UserInfo>();
                return new FriendsResponse()
                {
                    detail = new FriendshipStatusResponse()
                    {
                        code = Constants.FriendshipStatus.FriendNotFound,
                        message = nameof(Constants.FriendshipStatus.FriendNotFound)
                    },
                    friends = new List<UserInfo>()
                };
            }

            if (!response.IsSuccessStatusCode)
            {
                throw new SoilException($"Network error while fetching player info. Response: {responseString}",
                    SoilExceptionErrorCode.TransportError);
            }

            var getLinksResponse = JsonConvert.DeserializeObject<FriendsResponse>(responseString);
            SocializationPlayerPrefs.Friends = getLinksResponse.friends;
            return getLinksResponse;
        }

        public static async Task<FriendsResponse> AddFriendWithUUID(string uuid)
        {
            if (string.IsNullOrEmpty(uuid))
                throw new SoilException("UUID cannot be null or empty", SoilExceptionErrorCode.InvalidRequest);
            await Initialize();
            var friendshipClient = new HttpClient();
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
            var response = await friendshipClient.SendAsync(request);
            var responseString = response.Content.ReadAsStringAsync().Result;

            if (!response.IsSuccessStatusCode)
            {
                throw new SoilException($"Network error while adding friend. Response: {responseString}",
                    SoilExceptionErrorCode.TransportError);
            }

            var addFriendResponse = JsonConvert.DeserializeObject<FriendsResponse>(responseString);
            return addFriendResponse;
        }

        public static async Task<FriendsResponse> RemoveFriendWithUUID(string uuid)
        {
            if (string.IsNullOrEmpty(uuid))
                throw new SoilException("UUID cannot be null or empty", SoilExceptionErrorCode.InvalidRequest);
            await Initialize();
            var friendshipClient = new HttpClient();
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
            var response = await friendshipClient.SendAsync(request);
            var responseString = response.Content.ReadAsStringAsync().Result;

            if (!response.IsSuccessStatusCode)
            {
                throw new SoilException($"Network error while removing friend. Response: {responseString}",
                    SoilExceptionErrorCode.TransportError);
            }

            var removeFriendResponse = JsonConvert.DeserializeObject<FriendsResponse>(responseString);
            return removeFriendResponse;
        }

        public static async Task<List<UserScore>> GetFriendsLeaderboard(string leaderboardId, int count = 10,
            bool relative = false)
        {
            if (string.IsNullOrEmpty(leaderboardId))
                throw new SoilException("Leaderboard ID cannot be null or empty", SoilExceptionErrorCode.InvalidRequest);
            await Initialize();
            var payload = new Dictionary<string, object>
            {
                { "leaderboard_identifier", leaderboardId },
                { "count", count },
                { "relative", relative },
            };

            var stringBody = JsonConvert.SerializeObject(payload);

            var fetchClient = new HttpClient();
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
                responseString = response.Content.ReadAsStringAsync().Result;
            }
            catch (Exception e)
            {
                var fullMessage = $"Soil ====> Failed to fetch leaderboard. Error: {e.Message}";
                throw new SoilException(fullMessage, SoilExceptionErrorCode.InvalidRequest);
            }

            if (response is not { IsSuccessStatusCode: true })
            {
                var fullMessage = $"Soil ====> Failed to fetch leaderboard. Error: {responseString}";
                throw new SoilException(fullMessage, SoilExceptionErrorCode.TransportError);
            }

            var leaderboard = JsonConvert.DeserializeObject<List<UserScore>>(responseString);
            SocializationPlayerPrefs.SetCachedLeaderboardData(leaderboardId, responseString, relative);
            return leaderboard;
        }
    }
}