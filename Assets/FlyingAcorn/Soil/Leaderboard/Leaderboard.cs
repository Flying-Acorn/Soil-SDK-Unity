using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using FlyingAcorn.Analytics;
using FlyingAcorn.Soil.Core;
using FlyingAcorn.Soil.Core.User;
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
        ///Report a score to the leaderboard, preferably use ReportScore(double score, string leaderboardId) instead.
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
                    throw new Exception($"Soil ====> Failed to Report score. Error: {e.Message}");
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

            var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = Authenticate.GetAuthorizationHeader();
            var request = new HttpRequestMessage(HttpMethod.Post, ReportScoreUrl);
            request.Content = new StringContent(stringBody, Encoding.UTF8, "application/json");
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            HttpResponseMessage response;
            string responseString;
            try
            {
                response = await client.SendAsync(request);
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

        public static async Task<List<UserScore>> FetchLeaderboard(string leaderboardId, int count = 10,
            bool relative = false)
        {
            try
            {
                await Initialize();
            }
            catch (Exception e)
            {
                throw new Exception(e.Message);
            }

            var payload = new Dictionary<string, object>
            {
                { "leaderboard_identifier", leaderboardId },
                { "count", count },
                { "relative", relative },
                { "properties", UserInfo.Properties.GeneratePropertiesDynamicPlayerProperties() }
            };
            var stringBody = JsonConvert.SerializeObject(payload);

            var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = Authenticate.GetAuthorizationHeader();
            var request = new HttpRequestMessage(HttpMethod.Post, FetchLeaderboardUrl);
            request.Content = new StringContent(stringBody, Encoding.UTF8, "application/json");
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            HttpResponseMessage response;
            string responseString;
            try
            {
                response = await client.SendAsync(request);
                responseString = response.Content.ReadAsStringAsync().Result;
            }
            catch (Exception e)
            {
                var fullMessage = $"Soil ====> Failed to fetch leaderboard. Error: {e.Message}";
                throw new Exception(fullMessage);
            }

            if (response is not { IsSuccessStatusCode: true })
            {
                var fullMessage = $"Soil ====> Failed to fetch leaderboard. Error: {responseString}";
                throw new Exception(fullMessage);
            }

            var leaderboard = JsonConvert.DeserializeObject<List<UserScore>>(responseString);
            LeaderboardPlayerPrefs.SetCachedLeaderboardData(leaderboardId, responseString, relative);
            return leaderboard;
        }
    }
}