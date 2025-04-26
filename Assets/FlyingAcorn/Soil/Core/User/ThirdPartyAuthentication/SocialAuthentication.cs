using System;
using System.Collections.Generic;
using System.Linq;
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
        public static Action<Constants.ThirdParty> OnAccessRevoked { get; set; }
        public static Action<SoilException> OnLinkFailureCallback { get; set; }
        public static Action<SoilException> OnUnlinkFailureCallback { get; set; }
        public static Action<SoilException> OnGetAllLinksFailureCallback { get; set; }
        
        private static List<IPlatformAuthentication> _availableHandlers = new();

        public static async Task Initialize(List<ThirdPartySettings> thirdPartySettings)
        {
            MyDebug.Info("Initializing Social Authentication");
            await SoilServices.Initialize();
            if (_thirdPartySettings != null)
                return;
            _thirdPartySettings = thirdPartySettings;
            if (SoilServices.UserInfo.linkable_parties == null)
            {
                _thirdPartyInitializer ??= await UserApiHandler.FetchPlayerInfo();
            }
            _availableHandlers = new List<IPlatformAuthentication>();
            foreach (var handler in _thirdPartySettings.Select(GetAuthHandler).Where(handler => handler != null))
                _availableHandlers.Add(handler);

            IPlatformAuthentication.OnSignInFailureCallback += OnLinkFailureCallback;
            IPlatformAuthentication.OnAccessRevoked += AccessRevoked;
            IPlatformAuthentication.OnSignInSuccessCallback += OnSigninSuccess;
        }

        private static void AccessRevoked(ThirdPartySettings obj)
        {
            MyDebug.Info("Access revoked for " + obj.ThirdParty);
            if (SoilServices.UserInfo.linkable_parties == null)
                return;
            var party = SoilServices.UserInfo.linkable_parties.Find(p => p.party == obj.ThirdParty);
            if (party == null)
                return;
            LinkingPlayerPrefs.SetUserId(party.party, "");
            LinkingPlayerPrefs.RemoveLink(party);
            OnAccessRevoked?.Invoke(party.party);
        }

        public static bool IsPartyAvailable(Constants.ThirdParty party)
        {
            if (_thirdPartySettings == null)
                return false;

            var settings = GetConfigFile(party);
            if (!settings)
                return false;
            return SoilServices.UserInfo.linkable_parties != null &&
                   SoilServices.UserInfo.linkable_parties.Any(linkableParty => linkableParty.party == party);
        }

        public static void Link(Constants.ThirdParty party)
        {
            if (!IsPartyAvailable(party))
            {
                MyDebug.LogWarning("Party not available");
                OnLinkFailureCallback?.Invoke(new SoilException("Party not available",
                    SoilExceptionErrorCode.ServiceUnavailable));
                return;
            }

            if (_thirdPartySettings == null)
            {
                MyDebug.LogWarning("Settings not found");
                OnLinkFailureCallback?.Invoke(new SoilException("Settings not found",
                    SoilExceptionErrorCode.ServiceUnavailable));
                return;
            }

            var settings = GetConfigFile(party);
            if (!settings)
            {
                MyDebug.LogWarning("Settings not found");
                OnLinkFailureCallback?.Invoke(new SoilException("Settings not found",
                    SoilExceptionErrorCode.ServiceUnavailable));
                return;
            }

            var authenticationHandler = GetAuthHandler(settings);
            authenticationHandler.Authenticate();
        }

        private static IPlatformAuthentication GetAuthHandler(ThirdPartySettings settings)
        {
            return settings.ThirdParty switch
            {
                Constants.ThirdParty.google => Application.platform switch
                {
                    RuntimePlatform.Android => new GoogleAndroidAuthentication(settings),
                    RuntimePlatform.IPhonePlayer => new GoogleIOSAuthentication(settings),
                    _ => new GoogleOtherPlatformAuthentication(settings)
                },
                Constants.ThirdParty.apple => new AppleSignIn(settings),
                Constants.ThirdParty.facebook => null,
                Constants.ThirdParty.unity => null,
                _ => throw new SoilException("Unsupported third party", SoilExceptionErrorCode.ServiceUnavailable)
            };
        }

        public static async Task Unlink(Constants.ThirdParty party)
        {
            if (_thirdPartySettings == null)
            {
                MyDebug.LogWarning("Settings not found");
                OnUnlinkFailureCallback?.Invoke(new SoilException("Settings not found",
                    SoilExceptionErrorCode.ServiceUnavailable));
                return;
            }

            var settings = GetConfigFile(party);
            var unlinkResponse = await ThirdPartyAPIHandler.Unlink(settings);
            OnUnlinkSuccessCallback?.Invoke(unlinkResponse);
        }

        public static async void GetLinks()
        {
            try
            {
                if (_thirdPartySettings == null)
                {
                    MyDebug.LogWarning("Settings not found");
                    OnGetAllLinksFailureCallback?.Invoke(new SoilException("Settings not found",
                        SoilExceptionErrorCode.ServiceUnavailable));
                    return;
                }

                var links = await ThirdPartyAPIHandler.GetLinks();
                OnGetAllLinksSuccessCallback?.Invoke(links);
            }
            catch (SoilException e)
            {
                OnGetAllLinksFailureCallback?.Invoke(e);
            }
            catch (Exception e)
            {
                MyDebug.LogWarning(e);
                var soilException = new SoilException(e.Message);
                OnGetAllLinksFailureCallback?.Invoke(soilException);
            }
        }

        private static async void OnSigninSuccess(LinkAccountInfo thirdPartyUser, ThirdPartySettings settings)
        {
            try
            {
                MyDebug.Info("Linking user with " + settings.ThirdParty);
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

        public static void Update()
        {
            foreach (var link in _availableHandlers)
                link.Update();
        }
    }
}