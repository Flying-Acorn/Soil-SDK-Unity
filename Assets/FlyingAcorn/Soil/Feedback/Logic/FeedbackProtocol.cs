using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FlyingAcorn.Soil.Feedback.Logic
{
    /// <summary>
    /// Every <c>detail.code</c> the feedback API answers with (feedback/return_codes.py on the server). The server
    /// only ever appends, so an unknown number is a newer server, not an error.
    /// </summary>
    public enum FeedbackStatus
    {
        FeedbackSent = 0,
        /// <summary>Sent before: a retry with the same client id, or the same feedback again within a day.</summary>
        AlreadyReceived = 1,
        FeedbackListed = 2,
        ChannelsListed = 3,
        /// <summary>No channel with that key in this game, or it is turned off on the dashboard.</summary>
        ChannelNotFound = 4,
        InvalidRequest = 5,
        RatingRequired = 6,
        RatingNotAllowed = 7,
        InvalidRating = 8,
        MessageRequired = 9,
        MessageTooLong = 10,
        TargetTooLong = 11,
        DataTooLarge = 12,
        /// <summary>The player sent this channel's daily limit in the last 24 hours. See RetryAfterSeconds.</summary>
        DailyLimitReached = 13,
        /// <summary>Too many sends in a short time, across all channels. See RetryAfterSeconds.</summary>
        Throttled = 14,
        FeedbackError = 15,
        /// <summary>The channel groups by target (see <see cref="FeedbackChannelInfo.group_by_target"/>) and none was sent.</summary>
        TargetRequired = 16,
    }

    /// <summary>Whether a channel takes a 1 to 5 rating.</summary>
    public enum RatingMode
    {
        None,
        Optional,
        Required,
    }

    /// <summary>
    /// The handling staff gave a submission, as the player may see it. Feedback set aside as spam reads as New.
    /// </summary>
    public enum FeedbackReviewStatus
    {
        New,
        Planned,
        Applied,
        Declined,
    }

    [Serializable]
    public class FeedbackStatusDetail
    {
        public int code;
        public string message;

        [JsonIgnore] public FeedbackStatus Status => (FeedbackStatus)code;
    }

    /// <summary>A channel the game can send to, with its rules and what this player has left today.</summary>
    [Serializable]
    public class FeedbackChannelInfo
    {
        public string key;
        public string name;
        /// <summary>"none", "optional" or "required"; see <see cref="RatingMode"/>.</summary>
        public string rating;
        public bool message_required;
        public int max_message_length;
        public int daily_limit;
        public int remaining_today;
        /// <summary>
        /// Every distinct target is one item reviewed once, ranked by how many players sent it: the target is required,
        /// and is what the dashboard exports. A rating and a message may both be left out.
        /// </summary>
        public bool group_by_target;
        /// <summary>A player counts once per target, ever: sending the same target again answers AlreadyReceived.</summary>
        public bool once_per_target;

        [JsonIgnore]
        public RatingMode Rating => rating switch
        {
            "none" => RatingMode.None,
            "required" => RatingMode.Required,
            _ => RatingMode.Optional,
        };
    }

    [Serializable]
    public class FeedbackChannelList
    {
        public FeedbackStatusDetail detail;
        public List<FeedbackChannelInfo> channels = new();

        public FeedbackChannelInfo Find(string key) => channels?.Find(c => c.key == key);
    }

    /// <summary>One of the player's own submissions. Staff notes are never sent to the game.</summary>
    [Serializable]
    public class FeedbackEntry
    {
        public string id;
        public string channel;
        public string target;
        public int? rating;
        public string message;
        public JObject data;
        public string client_id;
        /// <summary>"new", "planned", "applied" or "declined"; see <see cref="ReviewStatus"/>.</summary>
        public string status;
        /// <summary>Someone on the team has opened it.</summary>
        public bool seen;
        /// <summary>ISO 8601, UTC.</summary>
        public string created_at;

        [JsonIgnore]
        public FeedbackReviewStatus ReviewStatus => status switch
        {
            "planned" => FeedbackReviewStatus.Planned,
            "applied" => FeedbackReviewStatus.Applied,
            "declined" => FeedbackReviewStatus.Declined,
            _ => FeedbackReviewStatus.New,
        };
    }

    /// <summary>The player's latest submissions (up to 50), newest first.</summary>
    [Serializable]
    public class FeedbackList
    {
        public FeedbackStatusDetail detail;
        public List<FeedbackEntry> feedback = new();
    }

    /// <summary>
    /// What a send did. A refusal - unknown channel, a rule, the daily limit - is an answer, not an exception:
    /// check <see cref="Status"/>. Only a transport failure, an expired sign-in, or the app lacking the Feedback
    /// feature throws.
    /// </summary>
    [Serializable]
    public class FeedbackSendResult
    {
        public FeedbackStatusDetail detail;
        /// <summary>What was saved: the new submission, or the earlier one for AlreadyReceived. Null on a refusal.</summary>
        public FeedbackEntry feedback;
        [JsonIgnore] public long HttpStatus;
        /// <summary>Seconds to wait before sending again, for Throttled and DailyLimitReached.</summary>
        [JsonIgnore] public int? RetryAfterSeconds;

        [JsonIgnore] public FeedbackStatus Status => detail?.Status ?? FeedbackStatus.FeedbackError;

        /// <summary>The server has it, from this send or an earlier try.</summary>
        [JsonIgnore] public bool Succeeded => HttpStatus >= 200 && HttpStatus < 300;
    }

    /// <summary>
    /// One piece of feedback to send. Keep the same instance (and so the same <see cref="ClientId"/>) when retrying
    /// after a timeout: the server then saves it once.
    /// </summary>
    public class FeedbackSubmission
    {
        /// <summary>The channel key, as set up on the dashboard: "support", "rate_app", "word_suggestion"...</summary>
        public string Channel;
        /// <summary>Optional: what it is about - a word, a level id, a feature. Up to 100 characters.</summary>
        public string Target;
        /// <summary>Optional 1 to 5, where the channel takes ratings.</summary>
        public int? Rating;
        public string Message;
        /// <summary>Optional extra context for the team, up to 2 KB as JSON: level, score, settings...</summary>
        public IDictionary<string, object> Data;
        public readonly string ClientId;

        public FeedbackSubmission(string channel, string clientId = null)
        {
            Channel = channel;
            ClientId = string.IsNullOrEmpty(clientId) ? Guid.NewGuid().ToString() : clientId;
        }
    }

    /// <summary>The request and answer formats of <c>/api/feedback/</c>.</summary>
    public static class FeedbackProtocol
    {
        public const string BasePath = "feedback/";
        public const string ChannelsPath = "feedback/channels/";
        public const int MinRating = 1;
        public const int MaxRating = 5;
        public const int MaxTargetLength = 100;

        public static string ListPath(string channel) =>
            string.IsNullOrEmpty(channel) ? BasePath : $"{BasePath}?channel={Uri.EscapeDataString(channel)}";

        public static string ToJson(FeedbackSubmission submission)
        {
            var body = new JObject { ["channel"] = submission.Channel, ["client_id"] = submission.ClientId };
            if (!string.IsNullOrEmpty(submission.Target)) body["target"] = submission.Target;
            if (submission.Rating.HasValue) body["rating"] = submission.Rating.Value;
            if (!string.IsNullOrEmpty(submission.Message)) body["message"] = submission.Message;
            if (submission.Data != null && submission.Data.Count > 0) body["data"] = JObject.FromObject(submission.Data);
            return body.ToString(Formatting.None);
        }

        /// <summary>
        /// What the server would refuse this submission for under the channel's rules, or null when it looks fine.
        /// Lets a form show the problem without a round trip; the server checks again either way.
        /// </summary>
        public static FeedbackStatus? Check(FeedbackChannelInfo channel, FeedbackSubmission submission)
        {
            if (channel == null) return FeedbackStatus.ChannelNotFound;
            var message = submission.Message?.Trim() ?? "";
            // In the server's order, so a submission with two problems is told the same one.
            var target = submission.Target?.Trim() ?? "";
            if (target.Length > MaxTargetLength) return FeedbackStatus.TargetTooLong;
            if (channel.group_by_target && target.Length == 0) return FeedbackStatus.TargetRequired;
            if (submission.Rating.HasValue)
            {
                if (submission.Rating < MinRating || submission.Rating > MaxRating) return FeedbackStatus.InvalidRating;
                if (channel.Rating == RatingMode.None) return FeedbackStatus.RatingNotAllowed;
            }
            else if (channel.Rating == RatingMode.Required)
            {
                return FeedbackStatus.RatingRequired;
            }
            // Something must be sent: a message, a rating, or in a grouped channel the target itself.
            if (message.Length == 0 && (channel.message_required || (!submission.Rating.HasValue && !channel.group_by_target)))
                return FeedbackStatus.MessageRequired;
            if (message.Length > channel.max_message_length) return FeedbackStatus.MessageTooLong;
            // The server answers a repeat under once_per_target with AlreadyReceived even past the limit; this cannot tell.
            if (channel.remaining_today <= 0) return FeedbackStatus.DailyLimitReached;
            return null;
        }

        /// <summary>
        /// The answer of a send, or null when the body is not one: a sign-in or feature refusal, a proxy error
        /// page. Refusals the feedback API makes itself carry a status code and are returned.
        /// </summary>
        public static FeedbackSendResult ParseSend(long httpStatus, string body, string retryAfter = null)
        {
            var result = Parse<FeedbackSendResult>(body);
            if (result?.detail == null) return null;
            result.HttpStatus = httpStatus;
            if (int.TryParse(retryAfter, out var seconds) && seconds >= 0) result.RetryAfterSeconds = seconds;
            return result;
        }

        /// <summary>The channels, or null unless the body is a successful channel list.</summary>
        public static FeedbackChannelList ParseChannels(long httpStatus, string body)
        {
            if (httpStatus < 200 || httpStatus >= 300) return null;
            var result = Parse<FeedbackChannelList>(body);
            if (result?.detail == null) return null;
            result.channels ??= new List<FeedbackChannelInfo>();
            return result;
        }

        /// <summary>The player's feedback, or null unless the body is a successful list.</summary>
        public static FeedbackList ParseList(long httpStatus, string body)
        {
            if (httpStatus < 200 || httpStatus >= 300) return null;
            var result = Parse<FeedbackList>(body);
            if (result?.detail == null) return null;
            result.feedback ??= new List<FeedbackEntry>();
            return result;
        }

        private static T Parse<T>(string body) where T : class
        {
            if (string.IsNullOrWhiteSpace(body)) return null;
            try
            {
                // DateParseHandling.None: by default Newtonsoft turns ISO dates into local-time DateTimes, and
                // created_at would come back reformatted in the device's culture and time zone.
                using var reader = new JsonTextReader(new System.IO.StringReader(body))
                    { DateParseHandling = DateParseHandling.None };
                var token = JToken.ReadFrom(reader);
                // DRF answers some refusals with {"detail": "text"}; only an object detail is ours.
                if (token is not JObject obj || obj["detail"] is not JObject) return null;
                return obj.ToObject<T>();
            }
            catch (JsonException)
            {
                return null;
            }
        }
    }
}
