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
                    throw new SoilException($"Failed to report score. Error: {e.Message}", 
                        SoilExceptionErrorCode.InvalidRequest);
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

            using var reportScoreClient = new HttpClient();
            reportScoreClient.Timeout = TimeSpan.FromSeconds(UserPlayerPrefs.RequestTimeout);
            reportScoreClient.DefaultRequestHeaders.Authorization = Authenticate.GetAuthorizationHeader();
            var request = new HttpRequestMessage(HttpMethod.Post, ReportScoreUrl);
            request.Content = new StringContent(stringBody, Encoding.UTF8, "application/json");
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            
            HttpResponseMessage response;
            string responseString;
            
            try
            {
                response = await reportScoreClient.SendAsync(request);
                responseString = await response.Content.ReadAsStringAsync();
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                throw new SoilException("Request timed out while reporting score", 
                    SoilExceptionErrorCode.TransportError);
            }
            catch (HttpRequestException ex)
            {
                throw new SoilException($"Network error while reporting score: {ex.Message}", 
                    SoilExceptionErrorCode.TransportError);
            }
            catch (Exception ex)
            {
                throw new SoilException($"Unexpected error while reporting score: {ex.Message}", 
                    SoilExceptionErrorCode.TransportError);
            }

            if (response is { IsSuccessStatusCode: true })
                return JsonConvert.DeserializeObject<UserScore>(responseString);
            else
            {
                throw new SoilException($"Server returned error {response.StatusCode}: {responseString}",
                    SoilExceptionErrorCode.TransportError);
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

            using var deleteLeaderboardClient = new HttpClient();
            deleteLeaderboardClient.Timeout = TimeSpan.FromSeconds(UserPlayerPrefs.RequestTimeout);
            deleteLeaderboardClient.DefaultRequestHeaders.Authorization = Authenticate.GetAuthorizationHeader();
            var request = new HttpRequestMessage(HttpMethod.Delete, ReportScoreUrl);
            request.Content = new StringContent(stringBody, Encoding.UTF8, "application/json");
            request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
            
            HttpResponseMessage response;
            
            try
            {
                response = await deleteLeaderboardClient.SendAsync(request);
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                throw new SoilException("Request timed out while deleting score", 
                    SoilExceptionErrorCode.TransportError);
            }
            catch (HttpRequestException ex)
            {
                throw new SoilException($"Network error while deleting score: {ex.Message}", 
                    SoilExceptionErrorCode.TransportError);
            }
            catch (Exception ex)
            {
                throw new SoilException($"Unexpected error while deleting score: {ex.Message}", 
                    SoilExceptionErrorCode.TransportError);
            }

            if (response is not { StatusCode: HttpStatusCode.NoContent or HttpStatusCode.NotFound })
            {
                var responseString = await response.Content.ReadAsStringAsync();
                throw new SoilException($"Server returned error {response.StatusCode}: {responseString}",
                    SoilExceptionErrorCode.TransportError);
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

            using var fetchClient = new HttpClient();
            fetchClient.Timeout = TimeSpan.FromSeconds(UserPlayerPrefs.RequestTimeout);
            fetchClient.DefaultRequestHeaders.Authorization = Authenticate.GetAuthorizationHeader();
            var request = new HttpRequestMessage(HttpMethod.Post, FetchLeaderboardUrl);
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
                throw new SoilException("Request timed out while fetching leaderboard", 
                    SoilExceptionErrorCode.TransportError);
            }
            catch (HttpRequestException ex)
            {
                throw new SoilException($"Network error while fetching leaderboard: {ex.Message}", 
                    SoilExceptionErrorCode.TransportError);
            }
            catch (Exception ex)
            {
                throw new SoilException($"Unexpected error while fetching leaderboard: {ex.Message}", 
                    SoilExceptionErrorCode.TransportError);
            }

            if (response is not { IsSuccessStatusCode: true })
            {
                throw new SoilException($"Server returned error {response.StatusCode}: {responseString}",
                    SoilExceptionErrorCode.TransportError);
            }

            var leaderboard = JsonConvert.DeserializeObject<List<UserScore>>(responseString);
            LeaderboardPlayerPrefs.SetCachedLeaderboardData(leaderboardId, responseString, relative);
            return leaderboard;
        }
    }
}