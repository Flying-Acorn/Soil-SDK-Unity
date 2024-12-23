namespace FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Data
{
    public static class Constants
    {
        public enum ThirdParty
        {
            Google,
        }
        
        public enum LinkStatus
        {
            SoilUserAlreadyLinked,
            LinkCreated,
            LinkFailed,
        }
    }
}