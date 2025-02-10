using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using FlyingAcorn.Soil.Core;
using FlyingAcorn.Soil.Core.Data;
using FlyingAcorn.Soil.Core.User;
using FlyingAcorn.Soil.Core.User.Authentication;
using FlyingAcorn.Soil.Leaderboard.Models;
using JetBrains.Annotations;
using Newtonsoft.Json;

namespace FlyingAcorn.Soil.Leaderboard
{
    public static class Leaderboard
    {
        private static readonly string LeaderboardBaseUrl = $"{Core.Data.Constants.ApiUrl}/leaderboard";

        private static readonly string ReportScoreUrl = $"{LeaderboardBaseUrl}/reportscore/";
        private static readonly string FetchLeaderboardUrl = $"{LeaderboardBaseUrl}/getleaderboard/";
        private static HttpClient _reportScoreClient;
        private static HttpClient _fetchLeaderboardClient;
        private static HttpClient _deleteScoreClient;
        [UsedImplicitly] public static bool Ready => SoilServices.Ready;

        public static async Task Initialize()
        {
            await SoilServices.Initialize();
        }

        [UsedImplicitly]
        public static async Task<UserScore> ReportScore(double score, string leaderboardId)
        {
            var payload = new Dictionary<string, object>
            {
                { "score", score },
                { "leaderboard_identifier", leaderboardId },
                { "properties", UserInfo.Properties.GeneratePropertiesDynamicPlayerProperties() }
            };
            return await ReportScore(payload);
        }

        ///<summary>
        ///Report a score to the leaderboard, when your scores are too big to be parsed as a double.
        ///</summary>
        [UsedImplicitly]
        public static async Task<UserScore> ReportScore(string score, string leaderboardId)
        {
            try
            {
                var doubleScore = double.Parse(score);
                return await ReportScore(doubleScore, leaderboardId);
            }
            catch (Exception e)
            {
                if (e is not OverflowException)
                    throw new SoilException($"Soil ====> Failed to Report score. Error: {e.Message}");
            }

            var payload = new Dictionary<string, object>
            {
                { "score", score },
                { "leaderboard_identifier", leaderboardId },
                { "properties", UserInfo.Properties.GeneratePropertiesDynamicPlayerProperties() }
            };
            return await ReportScore(payload);
        }

        private static async Task<UserScore> ReportScore(Dictionary<string, object> payload)
        {
            await Initialize();
            var stringBody = JsonConvert.SerializeObject(payload);

            _reportScoreClient?.Dispose();
            _reportScoreClient = new HttpClient();
            _reportScoreClient.DefaultRequestHeaders.Authorization = Authenticate.GetAuthorizationHeader();
            var request = new HttpRequestMessage(HttpMethod.Post, ReportScoreUrl);
            request.Content = new StringContent(stringBody, Encoding.UTF8, "application/json");
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            HttpResponseMessage response;
            string responseString;
            try
            {
                response = await _reportScoreClient.SendAsync(request);
                responseString = response.Content.ReadAsStringAsync().Result;
            }
            catch (Exception e)
            {
                var fullMessage = $"Soil ====> Failed to Report score. Error: {e.Message}";
                throw new Exception(fullMessage);
            }

            if (response is { IsSuccessStatusCode: true })
                return JsonConvert.DeserializeObject<UserScore>(responseString);
            {
                var fullMessage = $"Soil ====> Failed to Report score. Error: {responseString}";
                throw new Exception(fullMessage);
            }
        }

        public static async Task DeleteScore(string leaderboardId)
        {
            await Initialize();
            var payload = new Dictionary<string, object>
            {
                { "leaderboard_identifier", leaderboardId },
            };
            var stringBody = JsonConvert.SerializeObject(payload);

            _deleteScoreClient?.Dispose();
            _deleteScoreClient = new HttpClient();
            _deleteScoreClient.DefaultRequestHeaders.Authorization = Authenticate.GetAuthorizationHeader();
            var request = new HttpRequestMessage(HttpMethod.Delete, ReportScoreUrl);
            request.Content = new StringContent(stringBody, Encoding.UTF8, "application/json");
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            HttpResponseMessage response;
            try
            {
                response = await _deleteScoreClient.SendAsync(request);
            }
            catch (Exception e)
            {
                var fullMessage = $"Soil ====> Failed to delete score. Error: {e.Message}";
                throw new SoilException(fullMessage, SoilExceptionErrorCode.InvalidRequest);
            }

            if (response is not { StatusCode: HttpStatusCode.NoContent or HttpStatusCode.NotFound })
            {
                var responseString = response.Content.ReadAsStringAsync().Result;
                var fullMessage = $"Soil ====> Failed to delete score. Error: {responseString}";
                throw new SoilException(fullMessage);
            }
        }

        public static async Task<List<UserScore>> FetchLeaderboardAsync(string leaderboardId, int count = 10,
            bool relative = false)
        {
            await Initialize();

            var payload = new Dictionary<string, object>
            {
                { "leaderboard_identifier", leaderboardId },
                { "count", count },
                { "relative", relative },
                { "properties", UserInfo.Properties.GeneratePropertiesDynamicPlayerProperties() }
            };
            var stringBody = JsonConvert.SerializeObject(payload);

            // _fetchLeaderboardClient?.Dispose(); // support async
            _fetchLeaderboardClient = new HttpClient();
            _fetchLeaderboardClient.DefaultRequestHeaders.Authorization = Authenticate.GetAuthorizationHeader();
            var request = new HttpRequestMessage(HttpMethod.Post, FetchLeaderboardUrl);
            request.Content = new StringContent(stringBody, Encoding.UTF8, "application/json");
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            HttpResponseMessage response;
            string responseString;
            try
            {
                response = await _fetchLeaderboardClient.SendAsync(request);
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
            LeaderboardPlayerPrefs.SetCachedLeaderboardData(leaderboardId, responseString, relative);
            return leaderboard;
        }
    }
}