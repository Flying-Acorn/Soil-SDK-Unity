using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FlyingAcorn.Analytics;
using FlyingAcorn.Soil.Core.Data;
using FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.AuthPlatforms;
using FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Data;
using Newtonsoft.Json;
using UnityEngine;
using Constants = FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Data.Constants;

namespace FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication
{
    public abstract class SocialAuthentication
    {
        private static bool Initialized =>
            _thirdPartySettings != null && _initTask is { IsCompletedSuccessfully: true };

        private static Task _initTask;
        private static List<ThirdPartySettings> _thirdPartySettings;
        public static Action<LinkPostResponse> OnLinkSuccessCallback { get; set; }
        public static Action<UnlinkResponse> OnUnlinkSuccessCallback { get; set; }
        public static Action<LinkGetResponse> OnGetAllLinksSuccessCallback { get; set; }
        public static Action<Constants.ThirdParty> OnAccessRevoked { get; set; }
        public static Action<Constants.ThirdParty, SoilException> OnLinkFailureCallback { get; set; }
        public static Action<Constants.ThirdParty, SoilException> OnUnlinkFailureCallback { get; set; }
        public static Action<Constants.ThirdParty, SoilException> OnGetAllLinksFailureCallback { get; set; }

        private static List<IPlatformAuthentication> _availableHandlers = new();

        public static async Task Initialize(List<ThirdPartySettings> thirdPartySettings = null)
        {
            if (Initialized)
                return;

            if (_initTask is { IsCompletedSuccessfully: false, IsCompleted: true })
                _initTask = null;

            if (_initTask != null)
            {
                await _initTask;
                return;
            }

            _thirdPartySettings = thirdPartySettings;
            _initTask = await InitializeInternal();
        }

        private static async Task<Task> InitializeInternal()
        {
            MyDebug.Info("Initializing Social Authentication");
            await SoilServices.Initialize();
            if (_thirdPartySettings == null)
            {
                _thirdPartySettings = Resources.LoadAll<ThirdPartySettings>("ThirdParties").ToList();
                if (_thirdPartySettings.Count == 0)
                    throw new SoilException("No third party settings found",
                        SoilExceptionErrorCode.ServiceUnavailable);
            }

            await UserApiHandler.FetchPlayerInfo();
            foreach (var party in LinkingPlayerPrefs.SilentUnlinkQueue)
                Unlink(party);

            _availableHandlers = new List<IPlatformAuthentication>();
            foreach (var handler in _thirdPartySettings.Select(GetAuthHandler).Where(handler => handler != null))
                _availableHandlers.Add(handler);

            IPlatformAuthentication.OnSignInFailureCallback -= OnLinkFailureCallback;
            IPlatformAuthentication.OnAccessRevoked -= AccessRevoked;
            IPlatformAuthentication.OnSignInSuccessCallback -= OnSigninSuccess;
            IPlatformAuthentication.OnSignInFailureCallback += OnLinkFailureCallback;
            IPlatformAuthentication.OnAccessRevoked += AccessRevoked;
            IPlatformAuthentication.OnSignInSuccessCallback += OnSigninSuccess;
            return Task.CompletedTask;
        }

        private static void AccessRevoked(ThirdPartySettings obj)
        {
            var party = obj.ThirdParty;
            MyDebug.Info("Access revoked for " + party);
            LinkingPlayerPrefs.SetUserId(party, "");
            LinkingPlayerPrefs.RemoveLink(party);
            OnAccessRevoked?.Invoke(party);
            Unlink(party);
        }

        private static bool IsPartyAvailable(Constants.ThirdParty party)
        {
            var settings = GetConfigFile(party);
            if (!settings)
                return false;
            return SoilServices.UserInfo.linkable_parties != null &&
                   SoilServices.UserInfo.linkable_parties.Any(linkableParty => linkableParty.party == party);
        }

        public static void Link(Constants.ThirdParty party)
        {
            if (!Initialized)
            {
                MyDebug.LogWarning("Social Authentication not initialized");
                OnLinkFailureCallback?.Invoke(party, new SoilException("Social Authentication not initialized",
                    SoilExceptionErrorCode.NotReady));
                return;
            }

            if (!IsPartyAvailable(party))
            {
                MyDebug.LogWarning("Party not available");
                OnLinkFailureCallback?.Invoke(party, new SoilException("Party not available",
                    SoilExceptionErrorCode.ServiceUnavailable));
                return;
            }

            var settings = GetConfigFile(party);
            if (!settings)
            {
                MyDebug.LogWarning("Settings not found");
                OnLinkFailureCallback?.Invoke(party, new SoilException("Settings not found",
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
                Constants.ThirdParty.none => null,
                _ => throw new SoilException("Unsupported third party", SoilExceptionErrorCode.ServiceUnavailable)
            };
        }

        public static async void Unlink(Constants.ThirdParty party)
        {
            if (!Initialized)
            {
                MyDebug.LogWarning("Social Authentication not initialized");
                OnUnlinkFailureCallback?.Invoke(party, new SoilException("Social Authentication not initialized",
                    SoilExceptionErrorCode.NotReady));
                return;
            }

            var settings = GetConfigFile(party);
            try
            {
                var unlinkResponse = await ThirdPartyAPIHandler.Unlink(settings);
                MyDebug.Verbose("Unlinked user with " + JsonConvert.SerializeObject(unlinkResponse));
                OnUnlinkSuccessCallback?.Invoke(unlinkResponse);
            }
            catch (SoilException e)
            {
                LinkingPlayerPrefs.EnqueueSilentUnlink(party);
                OnUnlinkFailureCallback?.Invoke(party, e);
            }
            catch (Exception e)
            {
                LinkingPlayerPrefs.EnqueueSilentUnlink(party);
                var soilException = new SoilException(e.Message);
                OnUnlinkFailureCallback?.Invoke(party, soilException);
            }
        }

        public static async void GetLinks()
        {
            if (!Initialized)
            {
                MyDebug.LogWarning("Social Authentication not initialized");
                OnGetAllLinksFailureCallback?.Invoke(Constants.ThirdParty.none,
                    new SoilException("Social Authentication not initialized", SoilExceptionErrorCode.NotReady));
                return;
            }

            try
            {
                var links = await ThirdPartyAPIHandler.GetLinks();
                OnGetAllLinksSuccessCallback?.Invoke(links);
            }
            catch (SoilException e)
            {
                OnGetAllLinksFailureCallback?.Invoke(Constants.ThirdParty.none, e);
            }
            catch (Exception e)
            {
                MyDebug.LogWarning(e);
                var soilException = new SoilException(e.Message);
                OnGetAllLinksFailureCallback?.Invoke(Constants.ThirdParty.none, soilException);
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
                OnLinkFailureCallback?.Invoke(settings.ThirdParty, e);
            }
            catch (Exception e)
            {
                MyDebug.LogWarning(e);
                var soilException = new SoilException(e.Message);
                OnLinkFailureCallback?.Invoke(settings.ThirdParty, soilException);
            }
        }

        private static ThirdPartySettings GetConfigFile(Constants.ThirdParty party)
        {
            return _thirdPartySettings?.Find(settings =>
                settings.ThirdParty == party && settings.Platform == Application.platform);
        }

        public static void Update()
        {
            foreach (var link in _availableHandlers)
                link.Update();
        }

        public static void UpdateSpecific(Constants.ThirdParty party)
        {
            foreach (var link in _availableHandlers.Where(link => link.ThirdPartySettings.ThirdParty == party))
                link.Update();
        }
    }
}