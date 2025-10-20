using System;
using FlyingAcorn.Soil.Core.Data;
using FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Data;
using JetBrains.Annotations;
using static FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Data.Constants;

namespace FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.AuthPlatforms
{
    public interface IPlatformAuthentication
    {
        [UsedImplicitly] public ThirdPartySettings ThirdPartySettings { get; }
        public static Action<LinkAccountInfo, ThirdPartySettings> OnSignInSuccessCallback { get; set; }
        public static Action<ThirdPartySettings> OnAccessRevoked { get; set; }
        public static Action<ThirdParty, SoilException> OnSignInFailureCallback { get; set; }
        public void Authenticate();
        void Update(); // Call this in the main update loop to check for authentication status
    }
}