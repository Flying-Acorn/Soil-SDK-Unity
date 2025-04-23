using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FlyingAcorn.Analytics;
using FlyingAcorn.Soil.Core.Data;
using FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.AuthPlatforms;
using FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Data;
using UnityEngine;
using Constants = FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Data.Constants;

namespace FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication
{
    public abstract class SocialAuthentication
    {
        private static UserInfo _thirdPartyInitializer;
        private static List<ThirdPartySettings> _thirdPartySettings;

        public static Action<LinkPostResponse> OnLinkSuccessCallback { get; set; }
        public static Action<UnlinkResponse> OnUnlinkSuccessCallback { get; set; }
        public static Action<LinkGetResponse> OnGetAllLinksSuccessCallback { get; set; }
        public static Action<SoilException> OnLinkFailureCallback { get; set; }
        public static Action<SoilException> OnUnlinkFailureCallback { get; set; }
        public static Action<SoilException> OnGetAllLinksFailureCallback { get; set; }

        public static async Task Initialize(List<ThirdPartySettings> thirdPartySettings)
        {
            await SoilServices.Initialize();
            _thirdPartySettings = thirdPartySettings;
            if (SoilServices.UserInfo.linkable_parties == null)
            {
                _thirdPartyInitializer ??= await UserApiHandler.FetchPlayerInfo();
            }

        }

        public static async void Link(Constants.ThirdParty party)
        {
            if (_thirdPartySettings == null)
            {
                MyDebug.LogWarning("Settings not found");
                OnLinkFailureCallback?.Invoke(new SoilException("Settings not found", SoilExceptionErrorCode.MisConfiguration));
                return;
            }

            var settings = GetConfigFile(party);
            if (!settings)
            {
                MyDebug.LogWarning("Settings not found");
                OnLinkFailureCallback?.Invoke(new SoilException("Settings not found", SoilExceptionErrorCode.MisConfiguration));
                return;
            }
            IPlatformAuthentication authenticationHandler = Application.platform switch
            {
                RuntimePlatform.Android => new AndroidAuthentication(settings),
                RuntimePlatform.IPhonePlayer => new IOSAuthentication(settings),
                _ => new OtherPlatformAuthentication(settings)
            };
            authenticationHandler.OnSignInFailureCallback -= OnLinkFailureCallback;
            authenticationHandler.OnSignInSuccessCallback -= OnSigninSuccess;
            authenticationHandler.OnSignInFailureCallback += OnLinkFailureCallback;
            authenticationHandler.OnSignInSuccessCallback += OnSigninSuccess;
            authenticationHandler.Authenticate();
        }

        public static async void Unlink(Constants.ThirdParty party)
        {
            if (_thirdPartySettings == null)
            {
                MyDebug.LogWarning("Settings not found");
                OnUnlinkFailureCallback?.Invoke(new SoilException("Settings not found", SoilExceptionErrorCode.MisConfiguration));
                return;
            }

            var settings = GetConfigFile(party);
            var unlinkResponse = await ThirdPartyAPIHandler.Unlink(settings);
            OnUnlinkSuccessCallback?.Invoke(unlinkResponse);
        }

        public static async void GetLinks()
        {
            if (_thirdPartySettings == null)
            {
                MyDebug.LogWarning("Settings not found");
                OnGetAllLinksFailureCallback?.Invoke(new SoilException("Settings not found", SoilExceptionErrorCode.MisConfiguration));
                return;
            }

            var links = await ThirdPartyAPIHandler.GetLinks();
            OnGetAllLinksSuccessCallback?.Invoke(links);
        }

        private static async void OnSigninSuccess(LinkAccountInfo thirdPartyUser, ThirdPartySettings settings)
        {
            try
            {
                var authenticatedUser = await ThirdPartyAPIHandler.Link(thirdPartyUser, settings);
                OnLinkSuccessCallback?.Invoke(authenticatedUser);
            }
            catch (SoilException e)
            {
                MyDebug.LogWarning(e);
                OnLinkFailureCallback?.Invoke(e);
            }
            catch (Exception e)
            {
                MyDebug.LogWarning(e);
                var soilException = new SoilException(e.Message);
                OnLinkFailureCallback?.Invoke(soilException);
            }
        }

        private static ThirdPartySettings GetConfigFile(Constants.ThirdParty party)
        {
            return _thirdPartySettings.Find(settings =>
                settings.ThirdParty == party && settings.Platform == Application.platform);
        }
    }
}