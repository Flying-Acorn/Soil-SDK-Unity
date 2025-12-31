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
    /// <summary>
    /// Static class for leaderboard operations including score submission and fetching.
    /// </summary>
    public static class Leaderboard
    {
        private static string LeaderboardV2BaseUrl => $"{Core.Data.Constants.ApiUrl}/leaderboard/v2";
        private static string ReportScoreUrl => $"{LeaderboardV2BaseUrl}/reportscore/";
        private static string LeaderboardV3BaseUrl => $"{Core.Data.Constants.ApiUrl}/leaderboard/v3";
        private static string FetchLeaderboardUrl => $"{LeaderboardV3BaseUrl}/getleaderboard/";

        /// <summary>
        /// Gets whether the Leaderboard service is ready for use.
        /// </summary>
        [UsedImplicitly] public static bool Ready => SoilServices.Ready;


        /// <summary>
        /// Submits a score to a leaderboard.
        /// </summary>
        /// <param name="score">The score to submit.</param>
        /// <param name="leaderboardId">The ID of the leaderboard.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The user's score entry with rank information.</returns>
        [UsedImplicitly]
        public static async UniTask<UserScore> ReportScore(double score, string leaderboardId, CancellationToken cancellationToken = default)
        {
            var payload = new Dictionary<string, object>
            {
                { "score", score },
                { "leaderboard_identifier", leaderboardId }
            };
            return await ReportScore(payload, cancellationToken);
        }

        ///<summary>
        ///Report a score to the leaderboard, when your scores are too big to be parsed as a double.
        /// <summary>
        /// Submits a string score to a leaderboard. You can also submit string scores for large numbers.
        /// </summary>
        /// <param name="score">The score to submit as a string.</param>
        /// <param name="leaderboardId">The ID of the leaderboard.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <returns>The user's score entry with rank information.</returns>
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
                { "leaderboard_identifier", leaderboardId }
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
                // Leaderboard report: moderately heavy (ranking + DB write) -> 1.75x base timeout
                var effectiveTimeout = (int)(UserPlayerPrefs.RequestTimeout * 1.75f);
                await DataUtils.ExecuteUnityWebRequestWithTimeout(request, effectiveTimeout);
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

        /// <summary>
        /// Removes your score from a leaderboard.
        /// </summary>
        /// <param name="leaderboardId">The ID of the leaderboard.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
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
            // Use DELETE on the report score endpoint to delete the score
            using var request = new UnityEngine.Networking.UnityWebRequest(ReportScoreUrl, UnityEngine.Networking.UnityWebRequest.kHttpVerbDELETE);
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

        /// <summary>
        /// Gets top players from a leaderboard. Get leaderboard relative to the current player by setting relative to true.
        /// </summary>
        /// <param name="leaderboardId">The ID of the leaderboard.</param>
        /// <param name="count">Number of players to fetch.</param>
        /// <param name="relative">If true, shows players around your rank.</param>
        /// <param name="cancellationToken">Optional cancellation token.</param>
        /// <param name="iteration">Optional iteration token for pagination.</param>
        /// <returns>LeaderboardResponse containing user scores, iteration info, and next reset timestamp.</returns>
        [UsedImplicitly]
        public static async UniTask<LeaderboardResponse> FetchLeaderboardAsync(string leaderboardId, int count = 10,
            bool relative = false, CancellationToken cancellationToken = default, string iteration = null)
        {
            if (!SoilServices.Ready)
                throw new SoilException("Soil SDK is not ready", SoilExceptionErrorCode.InvalidRequest);

            var payload = new Dictionary<string, object>
            {
                { "leaderboard_identifier", leaderboardId },
                { "count", count },
                { "relative", relative }
            };
            if (iteration != null)
            {
                payload["iteration"] = iteration;
            }
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
                // Leaderboard fetch: potentially large payload but read-only -> 1.75x base timeout
                var effectiveTimeout = (int)(UserPlayerPrefs.RequestTimeout * 1.75f);
                await DataUtils.ExecuteUnityWebRequestWithTimeout(request, effectiveTimeout);
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
            var leaderboard = JsonConvert.DeserializeObject<LeaderboardResponse>(text);
            Analytics.MyDebug.Info(JsonConvert.SerializeObject(leaderboard));
            LeaderboardPlayerPrefs.SetCachedLeaderboardData(leaderboardId, text, relative);
            return leaderboard;
        }
    }
}