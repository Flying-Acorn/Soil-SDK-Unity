using FlyingAcorn.Soil.Core.Data;

namespace FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Data
{
    public static class ThirdPartyExtensions
    {
        public static Constants.ThirdParty ToThirdParty(this ThirdPartyType type)
        {
            return type switch
            {
                ThirdPartyType.google => Constants.ThirdParty.google,
                ThirdPartyType.facebook => Constants.ThirdParty.facebook,
                ThirdPartyType.unity => Constants.ThirdParty.unity,
                ThirdPartyType.apple => Constants.ThirdParty.apple,
                _ => Constants.ThirdParty.none
            };
        }

        public static ThirdPartyType ToThirdPartyType(this Constants.ThirdParty party)
        {
            return party switch
            {
                Constants.ThirdParty.google => ThirdPartyType.google,
                Constants.ThirdParty.facebook => ThirdPartyType.facebook,
                Constants.ThirdParty.unity => ThirdPartyType.unity,
                Constants.ThirdParty.apple => ThirdPartyType.apple,
                _ => ThirdPartyType.none
            };
        }
    }
}