using System;
using FlyingAcorn.Soil.Core.Data;
using FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Data;

namespace FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.AuthPlatforms
{
    public interface IPlatformAuthentication
    {
        public ThirdPartySettings ThirdPartySettings { get; }
        public Action<LinkAccountInfo, ThirdPartySettings> OnSignInSuccessCallback { get; set; }
        public Action<SoilException> OnSignInFailureCallback { get; set; }

        public void Authenticate();
    }
}