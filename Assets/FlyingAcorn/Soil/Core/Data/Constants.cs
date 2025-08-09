using System;
using System.Collections.Generic;

namespace FlyingAcorn.Soil.Core.Data
{
    public static class Constants
    {
        internal const string DemoAppID = "c425a46d-5a49-4986-b3fe-e9d61cd957d3";
        internal const string DemoAppSDKToken = "8c500e120772a66a1daad9cdfebedbaa3f31d6949ce8d41c94b49f125401ff00";
        internal const string BuildSettingsName = "FA_Build_Settings";
        internal static string ApiUrl
        {
            get
            {
                return DataUtils.FindApiUrl();
            }
        }

        internal const int DefaultTimeout = 6;
        internal const string FallBackApiUrl = "https://wwsoil.flyingacorn.studio/api";
        internal const string IRApiUrl = "https://soil.flyingacorn.ir/api";

        internal static readonly List<RegionSettings> APIPerRegion = new()
        {
            new RegionSettings {Region = Region.WW, ApiUrl = FallBackApiUrl},
            new RegionSettings {Region = Region.IR, ApiUrl = IRApiUrl}
        };

        [Serializable]
        public class RegionSettings
        {
            public Region Region;
            public string ApiUrl;
        }

        public enum Region
        {
            WW,
            IR
        }

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

        public enum DataScopes
        {
            SoilPublicUserInfo,
        }
    }
}