using System;
using CredentialBridge;
using FlyingAcorn.Soil.Core.Data;
using FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Data;
using Newtonsoft.Json;
using UnityEngine;
using Constants = FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Data.Constants;

namespace FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.AuthPlatforms
{
    public class AndroidAuthentication : IPlatformAuthentication
    {
        public ThirdPartySettings ThirdPartySettings { get; }
        public Action<LinkAccountInfo, ThirdPartySettings> OnSignInSuccessCallback { get; set; }
        public Action<SoilException> OnSignInFailureCallback { get; set; }

        public AndroidAuthentication(ThirdPartySettings thirdPartySettings)
        {
            ThirdPartySettings = thirdPartySettings;
        }
        public void Authenticate()
        {
            if (ThirdPartySettings.ThirdParty != Constants.ThirdParty.google)
            {
                Debug.LogError("AndroidAuthentication only supports Google");
                OnLoginFailed(new CredentialExceptionData());
                return;
            }

            switch (ThirdPartySettings.ThirdParty)
            {
                case Constants.ThirdParty.google:
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
            OnSignInFailureCallback?.Invoke(new SoilException(arg0.message));
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
            OnSignInSuccessCallback?.Invoke(user, ThirdPartySettings);
        }
    }
}