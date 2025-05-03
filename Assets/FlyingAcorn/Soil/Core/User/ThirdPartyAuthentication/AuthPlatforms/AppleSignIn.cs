using System;
using AppleAuth;
using AppleAuth.Enums;
using AppleAuth.Extensions;
using AppleAuth.Interfaces;
using AppleAuth.Native;
using FlyingAcorn.Analytics;
using FlyingAcorn.Soil.Core.Data;
using FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Data;
using Newtonsoft.Json;
using Constants = FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Data.Constants;

namespace FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.AuthPlatforms
{
    public class AppleSignIn : IPlatformAuthentication
    {
        public ThirdPartySettings ThirdPartySettings { get; }
        private readonly IAppleAuthManager _appleAuthManager;

        public AppleSignIn(ThirdPartySettings thirdPartySettings)
        {
            ThirdPartySettings = thirdPartySettings;

            if (!AppleAuthManager.IsCurrentPlatformSupported) return;
            var deserializer = new PayloadDeserializer();
            _appleAuthManager = new AppleAuthManager(deserializer);
            SetUp();
        }

        public void Update()
        {
            _appleAuthManager?.Update();
        }

        private void SetUp()
        {
            MyDebug.Info("Setting up Apple Sign In");
            var userID = LinkingPlayerPrefs.GetUserId(Constants.ThirdParty.apple);
            if (!string.IsNullOrEmpty(userID))
                CheckCredentialStatusForUserId(userID);
        }

        private void CheckCredentialStatusForUserId(string appleUserId)
        {
            MyDebug.Info("Checking credential status for user id: " + appleUserId);
            // If there is an apple ID available, we should check the credential state
            _appleAuthManager.GetCredentialState(
                appleUserId,
                state =>
                {
                    MyDebug.Info("Credential state: " + state);
                    switch (state)
                    {
                        // If it's authorized, login with that user id
                        case CredentialState.Authorized:
                            return;
                        case CredentialState.Revoked:
                            IPlatformAuthentication.OnAccessRevoked?.Invoke(ThirdPartySettings);
                            return;
                        case CredentialState.NotFound:
                            return;
                        case CredentialState.Transferred:
                            return;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(state), state, null);
                    }
                },
                error =>
                {
                    var authorizationErrorCode = error.GetAuthorizationErrorCode();
                    MyDebug.LogWarning("Error while trying to get credential state " + authorizationErrorCode + " " +
                                       error);
                });
        }

        private void AttemptQuickLogin()
        {
            var quickLoginArgs = new AppleAuthQuickLoginArgs();
            _appleAuthManager.QuickLogin(quickLoginArgs, FinishSignIn, OnError);
        }

        private void SignInWithApple()
        {
            MyDebug.Info("Sign in with Apple");
            var loginArgs = new AppleAuthLoginArgs(LoginOptions.IncludeEmail | LoginOptions.IncludeFullName);
            _appleAuthManager.LoginWithAppleId(loginArgs, FinishSignIn, OnError);
        }

        private void OnError(IAppleError error)
        {
            var authorizationErrorCode = error.GetAuthorizationErrorCode();
            MyDebug.LogWarning("Sign in with Apple failed " + authorizationErrorCode + " " + error);
            switch (authorizationErrorCode)
            {
                case AuthorizationErrorCode.Unknown:
                    IPlatformAuthentication.OnSignInFailureCallback?.Invoke(Constants.ThirdParty.apple,
                        new SoilException("Unknown error", SoilExceptionErrorCode.Unknown));
                    break;
                case AuthorizationErrorCode.Canceled:
                    IPlatformAuthentication.OnSignInFailureCallback?.Invoke(Constants.ThirdParty.apple,
                        new SoilException("User canceled", SoilExceptionErrorCode.Canceled));
                    break;
                case AuthorizationErrorCode.InvalidResponse:
                    IPlatformAuthentication.OnSignInFailureCallback?.Invoke(Constants.ThirdParty.apple,
                        new SoilException("Invalid response", SoilExceptionErrorCode.InvalidResponse));
                    break;
                case AuthorizationErrorCode.NotHandled:
                    IPlatformAuthentication.OnSignInFailureCallback?.Invoke(Constants.ThirdParty.apple,
                        new SoilException("Not handled", SoilExceptionErrorCode.Unknown));
                    break;
                case AuthorizationErrorCode.Failed:
                    IPlatformAuthentication.OnSignInFailureCallback?.Invoke(Constants.ThirdParty.apple,
                        new SoilException("Failed", SoilExceptionErrorCode.Unknown));
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void FinishSignIn(ICredential credential)
        {
            LinkingPlayerPrefs.SetUserId(Constants.ThirdParty.apple, credential.User);
            if (credential is not IAppleIDCredential appleIdCredential)
            {
                MyDebug.LogError("Sign in with Apple failed, credential is null");
                IPlatformAuthentication.OnSignInFailureCallback?.Invoke(Constants.ThirdParty.apple,
                    new SoilException("Credential is null", SoilExceptionErrorCode.InvalidResponse));
                return;
            }

            MyDebug.Info(
                $"Sign in with Apple succeeded, user id: {appleIdCredential.User}, email: {appleIdCredential.Email}");
            IPlatformAuthentication.OnSignInSuccessCallback?.Invoke(new LinkAccountInfo
            {
                social_account_id = appleIdCredential.User,
                email = appleIdCredential.Email,
                name = appleIdCredential.FullName?.GivenName,
                last_name = appleIdCredential.FullName?.FamilyName,
                display_name = appleIdCredential.FullName?.GivenName,
                profile_picture = "",
                extra_data = JsonConvert.SerializeObject(appleIdCredential)
            }, ThirdPartySettings);
        }

        public void Authenticate()
        {
            SignInWithApple();
        }
    }
}