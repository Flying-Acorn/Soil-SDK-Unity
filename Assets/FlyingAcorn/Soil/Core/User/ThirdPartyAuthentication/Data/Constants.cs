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
            AlreadyLinked = 0,
            LinkCreated = 1,
            LinkFound = 2,
            LinkDeleted = 3,
            AnotherLinkExists = 4,
            LinkNotFound = 5,
            PartyNotFound = 6,
        }
    }
}