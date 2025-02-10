using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Cdm.Authentication.Browser;
using Cdm.Authentication.Clients;
using Cdm.Authentication.OAuth2;
using FlyingAcorn.Analytics;
using FlyingAcorn.Soil.Core.Data;
using FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Data;
using Newtonsoft.Json;
using UnityEngine;
using Constants = FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Data.Constants;

namespace FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.AuthPlatforms
{
    public class IOSAuthentication : IPlatformAuthentication
    {
        public ThirdPartySettings ThirdPartySettings { get; }
        public Action<LinkAccountInfo, ThirdPartySettings> OnSignInSuccessCallback { get; set; }
        public Action<SoilException> OnSignInFailureCallback { get; set; }
        private static CancellationTokenSource _cancellationTokenSource;
        private AuthenticationSession _authenticationSession;

        public IOSAuthentication(ThirdPartySettings thirdPartySettings)
        {
            ThirdPartySettings = thirdPartySettings;
        }

        public async void Authenticate()
        {
            if (_authenticationSession == null)
            {
                if (ThirdPartySettings.ThirdParty != Constants.ThirdParty.google)
                {
                    _authenticationSession = null;
                }
                else
                {
                    var googleAuth = new GoogleAuth(new AuthorizationCodeFlow.Configuration()
                    {
                        clientId = ThirdPartySettings.ClientId,
                        clientSecret = ThirdPartySettings.ClientSecret,
                        redirectUri = ThirdPartySettings.RedirectUri,
                        scope = ThirdPartySettings.Scope
                    });
                    _authenticationSession = new AuthenticationSession(googleAuth, GetSuitableBrowsers());
                }
            }

            if (_authenticationSession == null)
            {
                MyDebug.LogWarning("Authentication is not supported for this platform.");
                return;
            }

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();


            try
            {
                var accessTokenResponse = await _authenticationSession.AuthenticateAsync();
                var authenticatedUser = await GetUserInfoAsync();
                OnSignInSuccessCallback?.Invoke(authenticatedUser, ThirdPartySettings);
            }
            catch (AuthorizationCodeRequestException ex)
            {
                MyDebug.LogWarning(ex.error.description);
                OnSignInFailureCallback?.Invoke(new SoilException(ex.error.description, SoilExceptionErrorCode.InvalidRequest));
            }
            catch (AccessTokenRequestException ex)
            {
                MyDebug.LogWarning(ex.error.description);
                OnSignInFailureCallback?.Invoke(new SoilException(ex.error.description, SoilExceptionErrorCode.InvalidToken));
            }
            catch (HttpListenerException ex)
            {
                MyDebug.LogWarning(ex.Message);
                OnSignInFailureCallback(new SoilException(ex.Message, SoilExceptionErrorCode.AnotherOngoingInstance));
            }
            catch (Exception ex)
            {
                MyDebug.LogWarning(ex.Message);
                OnSignInFailureCallback?.Invoke(new SoilException(ex.Message));
            }
        }

        private async void RefreshTokenAsync()
        {
            if (_authenticationSession == null) return;
            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();

            try
            {
                var accessTokenResponse =
                    await _authenticationSession.RefreshTokenAsync(_cancellationTokenSource.Token);

                Debug.Log(
                    $"Refresh token response:\n {JsonConvert.SerializeObject(accessTokenResponse, Formatting.Indented)}");
            }
            catch (AccessTokenRequestException ex)
            {
                Debug.LogError($"{nameof(AccessTokenRequestException)} " +
                               $"error: {ex.error.code}, description: {ex.error.description}, uri: {ex.error.uri}");
            }
            catch (Exception ex)
            {
                Debug.LogException(ex);
            }
        }

        private async Task<LinkAccountInfo> GetUserInfoAsync()
        {
            if (_authenticationSession == null)
            {
                throw new SoilException("Authentication session is not initialized.", SoilExceptionErrorCode.InvalidRequest);
            }

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = new CancellationTokenSource();

            if (!_authenticationSession.SupportsUserInfo())
            {
                MyDebug.LogWarning($"User info is not supported by {_authenticationSession.GetClientName()} client.");
                return new LinkAccountInfo();
            }

            var userInfo = await _authenticationSession.GetUserInfoAsync(_cancellationTokenSource.Token);
            MyDebug.Info($"User id: {userInfo.id}, name:{userInfo.name}, email: {userInfo.email}");
            var extraData = JsonConvert.SerializeObject(userInfo, Formatting.Indented);
            return new LinkAccountInfo()
            {
                social_account_id = userInfo.email,
                email = userInfo.email,
                display_name = userInfo.name,
                profile_picture = userInfo.picture,
                extra_data = extraData
            };
        }


        protected virtual IBrowser GetSuitableBrowsers()
        {
            return new ASWebAuthenticationSessionBrowser();
        }
    }
}