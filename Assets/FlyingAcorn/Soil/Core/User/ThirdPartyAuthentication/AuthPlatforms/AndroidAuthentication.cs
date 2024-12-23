using System;
using CredentialBridge;
using FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Data;
using UnityEngine;

namespace FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.AuthPlatforms
{
    public class AndroidAuthentication : IPlatformAuthentication
    {
        public ThirdPartySettings ThirdPartySettings { get; }
        public Action<AuthenticatedUser> OnSignInSuccessCallback { get; set; }
        public Action<string> OnSignInFailureCallback { get; set; }

        public AndroidAuthentication(ThirdPartySettings thirdPartySettings)
        {
            ThirdPartySettings = thirdPartySettings;
        }
        public void Authenticate()
        {
            if (ThirdPartySettings.ThirdParty != Constants.ThirdParty.Google)
            {
                Debug.LogError("AndroidAuthentication only supports Google");
                return;
            }

            switch (ThirdPartySettings.ThirdParty)
            {
                case Constants.ThirdParty.Google:
                    CredentialManager.OnLoginSucess.RemoveListener(OnLoginSuccess);
                    CredentialManager.OnLoginFailed.RemoveListener(OnLoginFailed);
                    CredentialManager.OnLoginSucess.AddListener(OnLoginSuccess);
                    CredentialManager.OnLoginFailed.AddListener(OnLoginFailed);
                    CredentialManager.SetupOathID(ThirdPartySettings.ClientId);
                    CredentialManager.StartCredentialProcess();
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void OnLoginFailed(CredentialExceptionData arg0)
        {
            Debug.LogError($"OnLoginFailed: {arg0.message}");
            OnSignInFailureCallback?.Invoke(arg0.message);
        }

        private void OnLoginSuccess(CredentialUserData arg0)
        {
            Debug.Log($"OnLoginSuccess: {arg0}");
            var user = new AuthenticatedUser
            {
                Id = arg0.id,
                Email = arg0.id,
                Name = arg0.givenName,
                LastName = arg0.familyName,
                DisplayName = arg0.displayName,
                Picture = arg0.profilePictureUri
            };
            OnSignInSuccessCallback?.Invoke(user);
        }
    }
}