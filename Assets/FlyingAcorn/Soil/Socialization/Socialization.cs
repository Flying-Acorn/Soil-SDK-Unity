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
using FlyingAcorn.Soil.Socialization.Data;
using FlyingAcorn.Soil.Socialization.Helpers;
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
            catch (Exception ex)
            {
                throw SocializationErrorHandler.HandleHttpException(ex, SocializationOperation.GetFriends);
            }

            // Check HTTP status code
            if (!response.IsSuccessStatusCode)
            {
                throw SocializationErrorHandler.HandleHttpResponse(response, responseString, SocializationOperation.GetFriends);
            }

            FriendsResponse firendshipResponse;
            try
            {
                firendshipResponse = JsonConvert.DeserializeObject<FriendsResponse>(responseString);
            }
            catch (Exception)
            {
                throw SocializationErrorHandler.HandleSerializationError(responseString, SocializationOperation.GetFriends);
            }

            if (firendshipResponse.detail.code == Constants.FriendshipStatus.FriendshipExists &&
                (firendshipResponse.friends == null || firendshipResponse.friends.Count == 0))
                throw new SocializationException("Problem with getting friends. No friends found", 
                    SocializationOperation.GetFriends, SoilExceptionErrorCode.InvalidResponse);

            return firendshipResponse;
        }

        public static async Task<FriendsResponse> AddFriendWithUUID(string uuid)
        {
            SocializationErrorHandler.ValidateParameter(uuid, nameof(uuid), SocializationOperation.AddFriend);
            
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
            catch (Exception ex)
            {
                throw SocializationErrorHandler.HandleHttpException(ex, SocializationOperation.AddFriend);
            }

            // Check HTTP status code
            if (!response.IsSuccessStatusCode)
            {
                throw SocializationErrorHandler.HandleHttpResponse(response, responseString, SocializationOperation.AddFriend);
            }

            FriendsResponse friendshipResponse;
            try
            {
                friendshipResponse = JsonConvert.DeserializeObject<FriendsResponse>(responseString);
            }
            catch (Exception)
            {
                throw SocializationErrorHandler.HandleSerializationError(responseString, SocializationOperation.AddFriend);
            }

            return friendshipResponse;
        }

        public static async Task<FriendsResponse> RemoveFriendWithUUID(string uuid)
        {
            SocializationErrorHandler.ValidateParameter(uuid, nameof(uuid), SocializationOperation.RemoveFriend);
            
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
            catch (Exception ex)
            {
                throw SocializationErrorHandler.HandleHttpException(ex, SocializationOperation.RemoveFriend);
            }

            // Check HTTP status code
            if (!response.IsSuccessStatusCode)
            {
                throw SocializationErrorHandler.HandleHttpResponse(response, responseString, SocializationOperation.RemoveFriend);
            }

            FriendsResponse firendshipResponse;
            try
            {
                firendshipResponse = JsonConvert.DeserializeObject<FriendsResponse>(responseString);
            }
            catch (Exception)
            {
                throw SocializationErrorHandler.HandleSerializationError(responseString, SocializationOperation.RemoveFriend);
            }

            return firendshipResponse;
        }

        public static async Task<List<UserScore>> GetFriendsLeaderboard(string leaderboardId, int count = 10,
            bool relative = false)
        {
            SocializationErrorHandler.ValidateParameter(leaderboardId, nameof(leaderboardId), SocializationOperation.GetFriendsLeaderboard);
            
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
            catch (Exception ex)
            {
                throw SocializationErrorHandler.HandleHttpException(ex, SocializationOperation.GetFriendsLeaderboard);
            }

            if (response is not { IsSuccessStatusCode: true })
            {
                throw SocializationErrorHandler.HandleHttpResponse(response, responseString, SocializationOperation.GetFriendsLeaderboard);
            }

            List<UserScore> leaderboard;
            try
            {
                leaderboard = JsonConvert.DeserializeObject<List<UserScore>>(responseString);
            }
            catch (Exception)
            {
                throw SocializationErrorHandler.HandleSerializationError(responseString, SocializationOperation.GetFriendsLeaderboard);
            }

            SocializationPlayerPrefs.SetCachedLeaderboardData(leaderboardId, responseString, relative);
            return leaderboard;
        }
    }
}