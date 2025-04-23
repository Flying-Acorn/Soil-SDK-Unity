using UnityEngine;

namespace FlyingAcorn.Analytics
{
    public static class AnalyticsPlayerPrefs
    {
        private const string Prefix = "FA_Analytics_";

        public static int SessionCount
        {
            get => PlayerPrefs.GetInt($"{Prefix}SessionCount", 0);
            set => PlayerPrefs.SetInt($"{Prefix}SessionCount", value);
        }

        public static Constants.ErrorSeverity.FlyingAcornErrorSeverity SavedLogLevel
        {
            get => (Constants.ErrorSeverity.FlyingAcornErrorSeverity)PlayerPrefs.GetInt(
                $"{Prefix}SavedLogLevel", (int)Constants.ErrorSeverity.FlyingAcornErrorSeverity.InfoSeverity);
            set => PlayerPrefs.SetInt($"{Prefix}SavedLogLevel", (int)value);
        }

        public static string InstallationVersion
        {
            get => PlayerPrefs.GetString($"{Prefix}InstallationVersion");
            set => PlayerPrefs.SetString($"{Prefix}InstallationVersion", value);
        }

        public static string InstallationBuild
        {
            get => PlayerPrefs.GetString($"{Prefix}InstallationBuild");
            set => PlayerPrefs.SetString($"{Prefix}InstallationBuild", value);
        }

        public static bool UserDebugMode
        {
            get => PlayerPrefs.GetInt($"{Prefix}UserDebugMode", 0) == 1;
            internal set => PlayerPrefs.SetInt($"{Prefix}UserDebugMode", value ? 1 : 0);
        }

        public static string CustomUserId
        {
            get
            {
                var id = PlayerPrefs.GetString($"{Prefix}CustomUserId");
                if (string.IsNullOrEmpty(id))
                    id = SystemInfo.deviceUniqueIdentifier;
                return id;
            }
            set => PlayerPrefs.SetString($"{Prefix}CustomUserId", value);
        }
    }
}