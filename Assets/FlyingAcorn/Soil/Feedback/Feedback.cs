using System;
using System.Collections.Generic;
using System.Text;
using Cysharp.Threading.Tasks;
using FlyingAcorn.Soil.Core;
using FlyingAcorn.Soil.Core.Data;
using FlyingAcorn.Soil.Core.User;
using FlyingAcorn.Soil.Core.User.Authentication;
using FlyingAcorn.Soil.Feedback.Data;
using FlyingAcorn.Soil.Feedback.Logic;
using JetBrains.Annotations;
using UnityEngine.Networking;

namespace FlyingAcorn.Soil.Feedback
{
    /// <summary>
    /// Players telling the studio what they think: support messages, a rating with a reason, ideas, word
    /// suggestions. Needs the app's Feedback feature, and channels set up on the dashboard; a game sends to a
    /// channel by its key. Private: no player ever sees another's feedback.
    /// <para>
    /// Sending is safe to retry with the same <see cref="FeedbackSubmission"/>. A refusal - unknown channel, a rule,
    /// the daily limit - comes back as a <see cref="FeedbackSendResult"/> with its
    /// <see cref="FeedbackSendResult.Status"/>; only a transport failure, an expired sign-in or the feature being
    /// off throws a <see cref="FeedbackException"/>.
    /// </para>
    /// </summary>
    public static class Feedback
    {
        [UsedImplicitly] public static bool Ready => SoilServices.Ready;

        private static string ApiBaseUrl => $"{Core.Data.Constants.ApiUrl}/";

        /// <summary>
        /// The channels this game can send to, their rules, and how many more this player can send to each today.
        /// Use it to build a form, or to hide a feedback button whose channel is off.
        /// </summary>
        public static async UniTask<FeedbackChannelList> GetChannels()
        {
            EnsureReady(FeedbackOperation.GetChannels);
            using var request = UnityWebRequest.Get(ApiBaseUrl + FeedbackProtocol.ChannelsPath);
            var (status, body, _) = await Send(request, FeedbackOperation.GetChannels);
            return FeedbackProtocol.ParseChannels(status, body) ?? throw Failure(status, body, FeedbackOperation.GetChannels);
        }

        /// <summary>Sends one piece of feedback. Retrying the same submission after a timeout saves it once.</summary>
        public static async UniTask<FeedbackSendResult> Send(FeedbackSubmission submission)
        {
            if (submission == null || string.IsNullOrWhiteSpace(submission.Channel))
                throw new FeedbackException("A feedback channel is required", FeedbackOperation.Send,
                    SoilExceptionErrorCode.InvalidRequest);
            EnsureReady(FeedbackOperation.Send);
            using var request = new UnityWebRequest(ApiBaseUrl + FeedbackProtocol.BasePath, UnityWebRequest.kHttpVerbPOST)
            {
                uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(FeedbackProtocol.ToJson(submission))),
                downloadHandler = new DownloadHandlerBuffer()
            };
            request.SetRequestHeader("Content-Type", "application/json");
            var (status, body, retryAfter) = await Send(request, FeedbackOperation.Send);
            return FeedbackProtocol.ParseSend(status, body, retryAfter) ?? throw Failure(status, body, FeedbackOperation.Send);
        }

        /// <summary>A written message, such as a support request or an idea.</summary>
        public static UniTask<FeedbackSendResult> SendMessage(string channel, string message, string target = null,
            IDictionary<string, object> data = null) =>
            Send(new FeedbackSubmission(channel) { Message = message, Target = target, Data = data });

        /// <summary>
        /// A 1 to 5 rating, with an optional reason. Typical use: a rating panel that asks "why?" below four stars
        /// and sends players who liked it to the store review instead.
        /// </summary>
        public static UniTask<FeedbackSendResult> SendRating(string channel, int rating, string reason = null,
            string target = null, IDictionary<string, object> data = null) =>
            Send(new FeedbackSubmission(channel) { Rating = rating, Message = reason, Target = target, Data = data });

        /// <summary>
        /// Several targets in one send - say, words a player added one by one - for a grouped channel whose
        /// <see cref="FeedbackChannelInfo.max_targets_per_send"/> is above 1. Each distinct target is saved and counted
        /// as if sent alone; <see cref="FeedbackSendResult.targets"/> tells what became of each.
        /// </summary>
        public static UniTask<FeedbackSendResult> SendTargets(string channel, IList<string> targets,
            IDictionary<string, object> data = null) =>
            Send(new FeedbackSubmission(channel) { Targets = targets, Data = data });

        /// <summary>
        /// The player's latest submissions (up to 50), newest first, with what the team did with each:
        /// planned, applied or declined. Pass a channel to see only that one.
        /// </summary>
        public static async UniTask<FeedbackList> GetMyFeedback(string channel = null)
        {
            EnsureReady(FeedbackOperation.GetMyFeedback);
            using var request = UnityWebRequest.Get(ApiBaseUrl + FeedbackProtocol.ListPath(channel));
            var (status, body, _) = await Send(request, FeedbackOperation.GetMyFeedback);
            return FeedbackProtocol.ParseList(status, body) ?? throw Failure(status, body, FeedbackOperation.GetMyFeedback);
        }

        private static async UniTask<(long status, string body, string retryAfter)> Send(UnityWebRequest request,
            FeedbackOperation operation)
        {
            request.SetRequestHeader("Accept", "application/json");
            var authHeader = Authenticate.GetAuthorizationHeader()?.ToString();
            if (!string.IsNullOrEmpty(authHeader)) request.SetRequestHeader("Authorization", authHeader);
            try
            {
                await DataUtils.ExecuteUnityWebRequestWithTimeout(request, UserPlayerPrefs.RequestTimeout);
            }
            catch (SoilException e)
            {
                throw new FeedbackException(e.Message, operation, e.ErrorCode);
            }
            catch (Exception e)
            {
                throw new FeedbackException($"Unexpected error while calling feedback: {e.Message}", operation,
                    SoilExceptionErrorCode.TransportError);
            }
            return (request.responseCode, request.downloadHandler?.text, request.GetResponseHeader("Retry-After"));
        }

        private static void EnsureReady(FeedbackOperation operation)
        {
            if (!Ready)
                throw new FeedbackException("SoilServices is not initialized. Cannot use feedback.", operation,
                    SoilExceptionErrorCode.NotReady);
        }

        private static FeedbackException Failure(long status, string body, FeedbackOperation operation)
        {
            var code = status switch
            {
                401 => SoilExceptionErrorCode.InvalidToken,
                // The app does not have the Feedback feature turned on.
                403 => SoilExceptionErrorCode.Forbidden,
                // A 429 the API did not answer itself, such as the proxy's per-IP limit.
                429 => SoilExceptionErrorCode.TooManyRequests,
                >= 200 and < 300 => SoilExceptionErrorCode.InvalidResponse,
                503 => SoilExceptionErrorCode.ServiceUnavailable,
                _ => SoilExceptionErrorCode.TransportError,
            };
            return new FeedbackException($"Feedback request failed ({status}): {body}", operation, code);
        }
    }
}
