using System;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FlyingAcorn.Soil.Advertisement.Logic
{
    /// <summary>Format names shared with the native players (same spelling as <c>AdFormat</c>).</summary>
    public static class AdFormats
    {
        public const string Banner = "banner";
        public const string Interstitial = "interstitial";
        public const string Rewarded = "rewarded";

        public static bool IsFullscreen(string format) => format == Interstitial || format == Rewarded;
        public static bool IsPlayerFormat(string format) => format == Banner || IsFullscreen(format);
    }

    /// <summary>The native player side of NativeAds/PROTOCOL.md.</summary>
    public interface IAdPlayer
    {
        void Load(string format, string creativeJson);
        void Show(string format, string optionsJson);
        void Hide(string format);
        void Destroy(string format);
    }

    public enum NativeAdEventType
    {
        Unknown,
        Loaded,
        LoadFailed,
        Shown,
        ShowFailed,
        Clicked,
        Rewarded,
        Closed
    }

    /// <summary>Error codes of the protocol, plus the ones the Unity side raises itself.</summary>
    public static class NativeAdErrors
    {
        public const string InvalidFormat = "invalid_format";
        public const string InvalidCreative = "invalid_creative";
        public const string MediaUnreadable = "media_unreadable";
        public const string NotLoaded = "not_loaded";
        public const string AlreadyShowing = "already_showing";
        public const string NoHost = "no_host";
        public const string Internal = "internal";

        /// <summary>Unity side: the ad server had nothing for the format.</summary>
        public const string NoFill = "no_fill";

        /// <summary>Unity side: the ad server or the asset downloads could not be reached.</summary>
        public const string Network = "network";
    }

    /// <summary>One message from a native player.</summary>
    public sealed class NativeAdEvent
    {
        public string Format;
        public NativeAdEventType Type;
        public string Media;
        public long DurationMs;
        public string Error;
        public string Message;

        /// <summary>Parses a player message; returns null for anything that is not one.</summary>
        public static NativeAdEvent Parse(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                var o = JObject.Parse(json);
                return new NativeAdEvent
                {
                    Format = (string)o["format"],
                    Type = ParseType((string)o["event"]),
                    Media = (string)o["media"],
                    DurationMs = o["durationMs"]?.Type == JTokenType.Integer || o["durationMs"]?.Type == JTokenType.Float
                        ? (long)o["durationMs"] : 0,
                    Error = (string)o["error"],
                    Message = (string)o["message"]
                };
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static NativeAdEventType ParseType(string value)
        {
            switch (value)
            {
                case "loaded": return NativeAdEventType.Loaded;
                case "loadFailed": return NativeAdEventType.LoadFailed;
                case "shown": return NativeAdEventType.Shown;
                case "showFailed": return NativeAdEventType.ShowFailed;
                case "clicked": return NativeAdEventType.Clicked;
                case "rewarded": return NativeAdEventType.Rewarded;
                case "closed": return NativeAdEventType.Closed;
                default: return NativeAdEventType.Unknown;
            }
        }
    }

    /// <summary>
    /// How long a fullscreen ad keeps its close button locked; see "Fullscreen lock policy" in
    /// NativeAds/PROTOCOL.md. Rewarded ads grant the reward when they unlock.
    /// </summary>
    public sealed class FullscreenShowOptions
    {
        [JsonProperty("imageLockSeconds")] public float ImageLockSeconds;
        [JsonProperty("videoLockFraction")] public float VideoLockFraction;
        [JsonProperty("minVideoLockSeconds")] public float MinVideoLockSeconds;
        [JsonProperty("maxLockSeconds")] public float MaxLockSeconds;
        [JsonProperty("startMuted")] public bool StartMuted;

        /// <summary>Interstitial: closable after 5 s, or 80% of a video.</summary>
        public static FullscreenShowOptions InterstitialDefaults() => new()
        {
            ImageLockSeconds = 5f,
            VideoLockFraction = 0.8f,
            MinVideoLockSeconds = 5f,
            MaxLockSeconds = 60f
        };

        /// <summary>Rewarded: closable (and rewarded) after 20 s, or the whole video.</summary>
        public static FullscreenShowOptions RewardedDefaults() => new()
        {
            ImageLockSeconds = 20f,
            VideoLockFraction = 1f,
            MinVideoLockSeconds = 0f,
            MaxLockSeconds = 60f
        };

        public static FullscreenShowOptions DefaultsFor(string format) =>
            format == AdFormats.Rewarded ? RewardedDefaults() : InterstitialDefaults();

        public string ToJson() => JsonConvert.SerializeObject(this);
    }

    public static class BannerPositions
    {
        public const string Bottom = "bottom";
        public const string Top = "top";
        public const string Center = "center";

        public static string ToJson(string position) =>
            JsonConvert.SerializeObject(new { position = position ?? Bottom });
    }
}
