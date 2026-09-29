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
        // What Credential Manager reports when the player dismisses the account picker.
        private const string UserCanceledType = "android.credentials.GetCredentialException.TYPE_USER_CANCELED";

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
            var errorCode = arg0.type == UserCanceledType
                ? SoilExceptionErrorCode.Canceled
                : SoilExceptionErrorCode.Unknown;
            IPlatformAuthentication.OnSignInFailureCallback?.Invoke(ThirdParty.google,
                new SoilException(arg0.message, errorCode));
        }

        private void OnLoginSuccess(CredentialUserData arg0)
        {
            Debug.Log($"OnLoginSuccess: {arg0}");
            // Keep the token out of extra_data.
            var shown = arg0;
            shown.idToken = null;
            var extraData = JsonConvert.SerializeObject(shown);
            var user = new LinkAccountInfo
            {
                social_account_id = arg0.id,
                email = arg0.id,
                name = arg0.givenName,
                last_name = arg0.familyName,
                display_name = arg0.displayName,
                profile_picture = arg0.profilePictureUri,
                extra_data = extraData,
                id_token = arg0.idToken ?? string.Empty
            };
            IPlatformAuthentication.OnSignInSuccessCallback?.Invoke(user, ThirdPartySettings);
        }
    }
}