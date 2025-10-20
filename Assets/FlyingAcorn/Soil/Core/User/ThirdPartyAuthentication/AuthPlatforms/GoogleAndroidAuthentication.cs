using CredentialBridge;
using FlyingAcorn.Soil.Core.Data;
using FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Data;
using Newtonsoft.Json;
using UnityEngine;
using static FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Data.Constants;

namespace FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.AuthPlatforms
{
    public class GoogleAndroidAuthentication : IPlatformAuthentication
    {
        public ThirdPartySettings ThirdPartySettings { get; }

        public GoogleAndroidAuthentication(ThirdPartySettings thirdPartySettings)
        {
            ThirdPartySettings = thirdPartySettings;
        }

        public void Authenticate()
        {
            if (ThirdPartySettings.ThirdParty != ThirdParty.google)
            {
                Debug.LogError("AndroidAuthentication only supports Google");
                OnLoginFailed(new CredentialExceptionData());
                return;
            }

            switch (ThirdPartySettings.ThirdParty)
            {
                case ThirdParty.google:
                    CredentialManager.OnLoginSucess.RemoveListener(OnLoginSuccess);
                    CredentialManager.OnLoginFailed.RemoveListener(OnLoginFailed);
                    CredentialManager.OnLoginSucess.AddListener(OnLoginSuccess);
                    CredentialManager.OnLoginFailed.AddListener(OnLoginFailed);
                    CredentialManager.SetupOathID(ThirdPartySettings.ClientId);
                    CredentialManager.StartCredentialProcess();
                    break;
                default:
                    throw new SoilException("Unsupported third party", SoilExceptionErrorCode.ServiceUnavailable);
            }
        }

        public void Update()
        {
        }

        private void OnLoginFailed(CredentialExceptionData arg0)
        {
            Debug.LogError($"OnLoginFailed: {arg0.message}");
            IPlatformAuthentication.OnSignInFailureCallback?.Invoke(ThirdParty.google,
                new SoilException(arg0.message));
        }

        private void OnLoginSuccess(CredentialUserData arg0)
        {
            Debug.Log($"OnLoginSuccess: {arg0}");
            var extraData = JsonConvert.SerializeObject(arg0);
            var user = new LinkAccountInfo
            {
                social_account_id = arg0.id,
                email = arg0.id,
                name = arg0.givenName,
                last_name = arg0.familyName,
                display_name = arg0.displayName,
                profile_picture = arg0.profilePictureUri,
                extra_data = extraData
            };
            IPlatformAuthentication.OnSignInSuccessCallback?.Invoke(user, ThirdPartySettings);
        }
    }
}