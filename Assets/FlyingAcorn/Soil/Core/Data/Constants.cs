using System.Collections.Generic;

namespace FlyingAcorn.Soil.Core.Data
{
    public static class Constants
    {
        internal const string DemoAppID = "c425a46d-5a49-4986-b3fe-e9d61cd957d3";
        internal const string DemoAppSDKToken = "8c500e120772a66a1daad9cdfebedbaa3f31d6949ce8d41c94b49f125401ff00";

        internal static string ApiUrl
        {
            get
            {
                if (SoilServices.UserInfo == null)
                    return FallBackApiUrl;
                var region = SoilServices.UserInfo.country;
                return string.IsNullOrEmpty(region)
                    ? FallBackApiUrl
                    : APIPerRegion.GetValueOrDefault(region, FallBackApiUrl);
            }
        }

        internal const int DefaultTimeout = 6;
        internal const string FallBackApiUrl = "https://wwsoil.flyingacorn.studio/api";

        private static readonly Dictionary<string, string> APIPerRegion = new Dictionary<string, string>
        {
            { "IR", "https://irsoil.flyingacorn.studio/api" },
            { "WW", FallBackApiUrl },
            { "", FallBackApiUrl },
        };

        public enum Store
        {
            Unknown,
            BetaChannel,
            Postman,
            GooglePlay,
            AppStore,
            CafeBazaar,
            Myket,
            Github,
            LandingPage
        }

        public enum DataScopes
        {
            SoilPublicUserInfo,
        }
    }
}