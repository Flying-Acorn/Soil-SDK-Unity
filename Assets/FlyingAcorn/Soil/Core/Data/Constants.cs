namespace FlyingAcorn.Soil.Core.Data
{
    public static class Constants
    {
        internal const string DemoAppID = "c425a46d-5a49-4986-b3fe-e9d61cd957d3";
        internal const string DemoAppSDKToken = "8c500e120772a66a1daad9cdfebedbaa3f31d6949ce8d41c94b49f125401ff00";
        internal const string ApiUrl = "https://soil.flyingacorn.studio/api";

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