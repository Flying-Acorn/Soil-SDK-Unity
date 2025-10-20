using FlyingAcorn.Soil.Core.Data;
using static FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Data.Constants;

namespace FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Data
{
    public static class ThirdPartyExtensions
    {
        public static ThirdParty ToThirdParty(this ThirdPartyType type)
        {
            return type switch
            {
                ThirdPartyType.google => ThirdParty.google,
                ThirdPartyType.facebook => ThirdParty.facebook,
                ThirdPartyType.unity => ThirdParty.unity,
                ThirdPartyType.apple => ThirdParty.apple,
                _ => ThirdParty.none
            };
        }

        public static ThirdPartyType ToThirdPartyType(this ThirdParty party)
        {
            return party switch
            {
                ThirdParty.google => ThirdPartyType.google,
                ThirdParty.facebook => ThirdPartyType.facebook,
                ThirdParty.unity => ThirdPartyType.unity,
                ThirdParty.apple => ThirdPartyType.apple,
                _ => ThirdPartyType.none
            };
        }
    }
}