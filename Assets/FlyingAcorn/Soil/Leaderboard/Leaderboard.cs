using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using FlyingAcorn.Soil.Core;
using FlyingAcorn.Soil.Core.User;
using FlyingAcorn.Soil.Leaderboard.Models;
using Newtonsoft.Json;
using UnityEngine;

namespace FlyingAcorn.Soil.Leaderboard
{
    public static class Leaderboard
    {
        private static readonly string LeaderboardBaseUrl = $"{Core.Data.Constants.ApiUrl}/leaderboard";

        private static readonly string ReportScoreUrl = $"{LeaderboardBaseUrl}/reportscore/";
        private static readonly string FetchLeaderboardUrl = $"{LeaderboardBaseUrl}/getleaderboard/";

        public static async void ReportScore(string score, string leaderboardId, Action<UserScore> successCallback = null,
            Action<string> errorCallback = null)
        {
            try
            {
                await SoilServices.Initialize();
            }
            catch (Exception e)
            {
                Debug.LogError($"FlyingAcorn ====> Failed to initialize SoilServices. Error: {e.Message}");
                errorCallback?.Invoke(e.Message);
                return;
            }

            var payload = new Dictionary<string, object>
            {
                { "score", score },
                { "leaderboard_identifier", leaderboardId },
                { "properties", UserInfo.Properties.GeneratePropertiesDynamicPlayerProperties() }
            };
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
                Debug.LogWarning($"FlyingAcorn ====> Failed to Update score. Error: {e.Message}");
                errorCallback?.Invoke(e.Message);
                return;
            }

            if (response is not { IsSuccessStatusCode: true })
            {
                Debug.LogWarning($"FlyingAcorn ====> Failed to Update score. Error: {responseString}");
                errorCallback?.Invoke(responseString);
            }
            else
            {
                var userScore = JsonConvert.DeserializeObject<UserScore>(responseString);
                successCallback?.Invoke(userScore);
            }
        }

        public static async void FetchLeaderboard(string leaderboardId, int count = 10, bool relative = false,
            Action<List<UserScore>> successCallback = null, Action<string> errorCallback = null)
        {
            try
            {
                await SoilServices.Initialize();
            }
            catch (Exception e)
            {
                Debug.LogError($"FlyingAcorn ====> Failed to initialize SoilServices. Error: {e.Message}");
                errorCallback?.Invoke(e.Message);
                return;
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
                Debug.LogWarning($"FlyingAcorn ====> Failed to fetch leaderboard. Error: {e.Message}");
                errorCallback?.Invoke(e.Message);
                return;
            }

            if (response is not { IsSuccessStatusCode: true })
            {
                Debug.LogWarning($"FlyingAcorn ====> Failed to fetch leaderboard. Error: {responseString}");
                errorCallback?.Invoke(responseString);
            }
            else
            {
                var leaderboard = JsonConvert.DeserializeObject<List<Models.UserScore>>(responseString);
                LeaderboardPlayerPrefs.SetCachedLeaderboardData(leaderboardId, responseString);
                successCallback?.Invoke(leaderboard);
            }
        }
    }
}