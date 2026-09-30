using System;

namespace FlyingAcorn.Soil.Advertisement.Logic
{
    /// <summary>
    /// Which click links an ad may open. Click URLs come from the ad server and are opened by the
    /// OS. Web links and app store links always pass. Where <see cref="AppLinksAllowed"/> is on
    /// (Android, where stores and apps link with their own schemes and the player opens them as
    /// browsable links only), any other app scheme passes too, except the ones that act on the
    /// device (javascript:, file:, content:, intent:, tel:, sms:, ...). Anything refused is treated
    /// as if the ad had no link; the click is still reported.
    /// </summary>
    public static class AdLinkPolicy
    {
        private static readonly string[] WebAndStoreSchemes = { "http", "https", "market", "itms-apps", "itms-appss" };

        private static readonly string[] DeviceSchemes =
        {
            "javascript", "vbscript", "data", "file", "content", "intent", "android-app", "about", "blob",
            "tel", "sms", "smsto", "mms", "mmsto", "mailto", "wtai"
        };

#if UNITY_ANDROID
        private const bool DefaultAppLinksAllowed = true;
#else
        private const bool DefaultAppLinksAllowed = false;
#endif

        /// <summary>Whether app schemes other than web and store links may be opened.</summary>
        public static bool AppLinksAllowed { get; set; } = DefaultAppLinksAllowed;

        public static void ResetStatics()
        {
            AppLinksAllowed = DefaultAppLinksAllowed;
        }

        public static bool IsAllowed(string url)
        {
            return Sanitize(url) != null;
        }

        /// <summary>The link, trimmed, when its scheme is allowed; otherwise null.</summary>
        public static string Sanitize(string url)
        {
            if (string.IsNullOrWhiteSpace(url)) return null;
            var trimmed = url.Trim();

            var colon = trimmed.IndexOf(':');
            if (colon <= 0) return null;
            // A link with nothing after the scheme opens nothing.
            if (trimmed.Length == colon + 1) return null;
            foreach (var c in trimmed)
                if (char.IsControl(c)) return null;

            var scheme = trimmed.Substring(0, colon);
            if (!IsSchemeName(scheme)) return null;
            if (Contains(WebAndStoreSchemes, scheme)) return trimmed;
            return AppLinksAllowed && !Contains(DeviceSchemes, scheme) ? trimmed : null;
        }

        // RFC 3986: ALPHA *( ALPHA / DIGIT / "+" / "-" / "." )
        private static bool IsSchemeName(string scheme)
        {
            if (!IsAsciiLetter(scheme[0])) return false;
            foreach (var c in scheme)
                if (!IsAsciiLetter(c) && !(c >= '0' && c <= '9') && c != '+' && c != '-' && c != '.')
                    return false;
            return true;
        }

        private static bool IsAsciiLetter(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z');
        }

        private static bool Contains(string[] schemes, string scheme)
        {
            foreach (var s in schemes)
                if (string.Equals(s, scheme, StringComparison.OrdinalIgnoreCase))
                    return true;
            return false;
        }
    }
}
