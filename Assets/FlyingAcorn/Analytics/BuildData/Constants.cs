namespace FlyingAcorn.Analytics.BuildData
{
    public static class Constants
    {
        public const string BuildSettingsName = "FA_Build_Settings";

        public enum Store
        {
            Unknown = 0,
            BetaChannel = 1,
            Postman = 2,
            GooglePlay = 3,
            AppStore = 4,
            // Regional Android stores. Compiled out of iOS players so their names are not part of
            // that binary, and kept in the editor so build tooling still resolves them for Android
            // jobs. The values are explicit so omitting them never shifts the members below, which
            // are serialized by value in build settings and player prefs.
#if !UNITY_IOS || UNITY_EDITOR
            CafeBazaar = 5,
            Myket = 6,
#endif
            Github = 7,
            LandingPage = 8
        }
    }
}
