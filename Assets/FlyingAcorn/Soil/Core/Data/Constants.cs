using System;
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
                return DataUtils.FindApiUrl();
            }
        }

        internal const int DefaultTimeout = 6;
        internal const string FallBackApiUrl = "https://wwsoil.flyingacorn.studio/api";
        private const bool IsRegionalApiAvailable = true;
        internal static string IRApiUrl() => IsRegionalApiAvailable ? $"{DataUtils.GetRegionalApiDomain()}/api" : FallBackApiUrl;

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

        public enum DataScopes
        {
            SoilPublicUserInfo,
        }
    }
}