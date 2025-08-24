using System;
using System.Collections.Generic;
using System.Net;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
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
        private static string LeaderboardBaseUrl => $"{Core.Data.Constants.ApiUrl}/leaderboard/v2";
        private static string DeleteLeaderboardUrl => $"{LeaderboardBaseUrl}/delete/";
        private static string ReportScoreUrl => $"{LeaderboardBaseUrl}/reportscore/";
        private static string FetchLeaderboardUrl => $"{LeaderboardBaseUrl}/getleaderboard/";

        [UsedImplicitly] public static bool Ready => SoilServices.Ready;


        [UsedImplicitly]
        public static async UniTask<UserScore> ReportScore(double score, string leaderboardId, CancellationToken cancellationToken = default)
        {
            var payload = new Dictionary<string, object>
            {
                { "score", score },
                { "leaderboard_identifier", leaderboardId },
                { "properties", UserInfo.Properties.GeneratePropertiesDynamicPlayerProperties() }
            };
            return await ReportScore(payload, cancellationToken);
        }

        ///<summary>
        ///Report a score to the leaderboard, when your scores are too big to be parsed as a double.
        ///</summary>
        [UsedImplicitly]
        public static async UniTask<UserScore> ReportScore(string score, string leaderboardId, CancellationToken cancellationToken = default)
        {
            // Prefer TryParse to avoid exception cost; fall back to string payload only when truly non-double or overflow
            if (double.TryParse(score, out var parsed))
            {
                return await ReportScore(parsed, leaderboardId, cancellationToken);
            }

            var payload = new Dictionary<string, object>
            {
                { "score", score },
                { "leaderboard_identifier", leaderboardId },
                { "properties", UserInfo.Properties.GeneratePropertiesDynamicPlayerProperties() }
            };
            return await ReportScore(payload, cancellationToken);
        }

        private static async UniTask<UserScore> ReportScore(Dictionary<string, object> payload, CancellationToken cancellationToken)
        {
            if (!SoilServices.Ready)
                throw new SoilException("Soil SDK is not ready", SoilExceptionErrorCode.InvalidRequest);

            var stringBody = JsonConvert.SerializeObject(payload);
            var authHeader = Authenticate.GetAuthorizationHeader()?.ToString();
            using var request = new UnityEngine.Networking.UnityWebRequest(ReportScoreUrl, UnityEngine.Networking.UnityWebRequest.kHttpVerbPOST);
            var bodyRaw = Encoding.UTF8.GetBytes(stringBody);
            request.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            if (!string.IsNullOrEmpty(authHeader)) request.SetRequestHeader("Authorization", authHeader);

            // Cancellation support
            using var reg = cancellationToken.CanBeCanceled
                ? cancellationToken.Register(() => { if (!request.isDone) request.Abort(); })
                : default;

            try
            {
                await DataUtils.ExecuteUnityWebRequestWithTimeout(request, UserPlayerPrefs.RequestTimeout);
            }
            catch (SoilException)
            {
                throw; // propagate
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw new SoilException("Report score canceled", SoilExceptionErrorCode.Canceled);
            }
            catch (Exception ex)
            {
                throw new SoilException($"Unexpected error while reporting score: {ex.Message}", SoilExceptionErrorCode.TransportError);
            }

            var status = request.responseCode;
            var text = request.downloadHandler.text;
            if (status >= 200 && status < 300)
            {
                return JsonConvert.DeserializeObject<UserScore>(text);
            }
            throw new SoilException($"Server returned error {(HttpStatusCode)status}: {text}", SoilExceptionErrorCode.TransportError);
        }

        public static async UniTask DeleteScore(string leaderboardId, CancellationToken cancellationToken = default)
        {
            if (!SoilServices.Ready)
                throw new SoilException("Soil SDK is not ready", SoilExceptionErrorCode.InvalidRequest);

            var payload = new Dictionary<string, object>
            {
                { "leaderboard_identifier", leaderboardId },
            };
            var stringBody = JsonConvert.SerializeObject(payload);

            var authHeader = Authenticate.GetAuthorizationHeader()?.ToString();
            // Use the proper delete endpoint instead of the report score endpoint
            using var request = new UnityEngine.Networking.UnityWebRequest(DeleteLeaderboardUrl, UnityEngine.Networking.UnityWebRequest.kHttpVerbDELETE);
            var bodyRaw = Encoding.UTF8.GetBytes(stringBody);
            request.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            if (!string.IsNullOrEmpty(authHeader)) request.SetRequestHeader("Authorization", authHeader);
            using var reg = cancellationToken.CanBeCanceled
                ? cancellationToken.Register(() => { if (!request.isDone) request.Abort(); })
                : default;
            try
            {
                await DataUtils.ExecuteUnityWebRequestWithTimeout(request, UserPlayerPrefs.RequestTimeout);
            }
            catch (SoilException) { throw; }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            { throw new SoilException("Delete score canceled", SoilExceptionErrorCode.Canceled); }
            catch (Exception ex)
            { throw new SoilException($"Unexpected error while deleting score: {ex.Message}", SoilExceptionErrorCode.TransportError); }

            var status = request.responseCode;
            if (status == (long)HttpStatusCode.NoContent || status == (long)HttpStatusCode.NotFound) return;
            var text = request.downloadHandler.text;
            throw new SoilException($"Server returned error {(HttpStatusCode)status}: {text}", SoilExceptionErrorCode.TransportError);
        }

        public static async UniTask<List<UserScore>> FetchLeaderboardAsync(string leaderboardId, int count = 10,
            bool relative = false, CancellationToken cancellationToken = default)
        {
            if (!SoilServices.Ready)
                throw new SoilException("Soil SDK is not ready", SoilExceptionErrorCode.InvalidRequest);

            var payload = new Dictionary<string, object>
            {
                { "leaderboard_identifier", leaderboardId },
                { "count", count },
                { "relative", relative },
                { "properties", UserInfo.Properties.GeneratePropertiesDynamicPlayerProperties() }
            };
            var stringBody = JsonConvert.SerializeObject(payload);

            var authHeader = Authenticate.GetAuthorizationHeader()?.ToString();
            using var request = new UnityEngine.Networking.UnityWebRequest(FetchLeaderboardUrl, UnityEngine.Networking.UnityWebRequest.kHttpVerbPOST);
            var bodyRaw = Encoding.UTF8.GetBytes(stringBody);
            request.uploadHandler = new UnityEngine.Networking.UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new UnityEngine.Networking.DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            if (!string.IsNullOrEmpty(authHeader)) request.SetRequestHeader("Authorization", authHeader);
            using var reg = cancellationToken.CanBeCanceled
                ? cancellationToken.Register(() => { if (!request.isDone) request.Abort(); })
                : default;
            try
            {
                await DataUtils.ExecuteUnityWebRequestWithTimeout(request, UserPlayerPrefs.RequestTimeout);
            }
            catch (SoilException) { throw; }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            { throw new SoilException("Fetch leaderboard canceled", SoilExceptionErrorCode.Canceled); }
            catch (Exception ex)
            { throw new SoilException($"Unexpected error while fetching leaderboard: {ex.Message}", SoilExceptionErrorCode.TransportError); }

            var status = request.responseCode;
            var text = request.downloadHandler.text;
            if (status < 200 || status >= 300)
            {
                throw new SoilException($"Server returned error {(HttpStatusCode)status}: {text}", SoilExceptionErrorCode.TransportError);
            }
            var leaderboard = JsonConvert.DeserializeObject<List<UserScore>>(text);
            LeaderboardPlayerPrefs.SetCachedLeaderboardData(leaderboardId, text, relative);
            return leaderboard;
        }
    }
}