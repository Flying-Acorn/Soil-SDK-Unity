using Cdm.Authentication.Browser;
using UnityEngine;

namespace FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.AuthPlatforms
{
    public class GoogleOtherPlatformAuthentication : GoogleIOSAuthentication
    {
        public GoogleOtherPlatformAuthentication(ThirdPartySettings thirdPartySettings) : base(thirdPartySettings)
        {
        }

        protected override IBrowser GetSuitableBrowsers()
        {
            if (Application.platform == RuntimePlatform.Android || Application.platform == RuntimePlatform.IPhonePlayer)
            {
                return new DeepLinkBrowser();
            }

            return new StandaloneBrowser();
        }
    }
}