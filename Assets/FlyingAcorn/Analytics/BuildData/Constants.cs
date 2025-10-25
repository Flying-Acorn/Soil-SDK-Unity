using System;

namespace FlyingAcorn.Analytics.BuildData
{
    public static class Constants
    {
        public const string BuildSettingsName = "FA_Build_Settings";

        public enum Store
        {
            Unknown,
            BetaChannel,
            Postman,
            GooglePlay,
            AppStore,
            CafeBazaar, // Iran Local Store
            Myket, // Iran Local Store
            Github,
            LandingPage
        }
    }
}
