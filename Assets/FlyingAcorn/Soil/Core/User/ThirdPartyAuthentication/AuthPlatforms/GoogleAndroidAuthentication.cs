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
        // GetCredentialException.getType() values from androidx.credentials, plus the bridge's own failures.
        private const string UserCanceledType = "android.credentials.GetCredentialException.TYPE_USER_CANCELED";
        private const string NoCredentialType = "android.credentials.GetCredentialException.TYPE_NO_CREDENTIAL";
        private const string InterruptedType = "android.credentials.GetCredentialException.TYPE_INTERRUPTED";
        private const string ProviderConfigurationType =
            "androidx.credentials.TYPE_GET_CREDENTIAL_PROVIDER_CONFIGURATION_EXCEPTION";
        private const string UnsupportedType = "androidx.credentials.TYPE_GET_CREDENTIAL_UNSUPPORTED_EXCEPTION";

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
                    // Per-request callbacks: SocialAuthentication creates a handler per attempt, so listening on
                    // CredentialManager's static events would deliver each result to every past attempt.
                    CredentialManager.SetupOathID(ThirdPartySettings.ClientId);
                    CredentialManager.StartCredentialProcess(OnLoginSuccess, OnLoginFailed);
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
            Debug.LogError($"OnLoginFailed: {arg0.type} {arg0.message}");
            IPlatformAuthentication.OnSignInFailureCallback?.Invoke(ThirdParty.google,
                new SoilException(arg0.message, MapErrorCode(arg0.type)));
        }

        /// <summary>
        /// Gives each Credential Manager failure its own code so games can tell "no Google account" or "device
        /// can't do this" apart from a real network error, instead of everything reading as Unknown.
        /// </summary>
        internal static SoilExceptionErrorCode MapErrorCode(string credentialErrorType)
        {
            return credentialErrorType switch
            {
                UserCanceledType => SoilExceptionErrorCode.Canceled,
                NoCredentialType => SoilExceptionErrorCode.NotFound,
                InterruptedType => SoilExceptionErrorCode.TransportError,
                ProviderConfigurationType => SoilExceptionErrorCode.ServiceUnavailable,
                UnsupportedType => SoilExceptionErrorCode.ServiceUnavailable,
                CredentialManager.BridgeErrorType => SoilExceptionErrorCode.MisConfiguration,
                CredentialManager.InvalidResponseType => SoilExceptionErrorCode.InvalidResponse,
                _ => SoilExceptionErrorCode.Unknown
            };
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