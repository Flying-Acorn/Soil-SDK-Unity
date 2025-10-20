using System;
using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using FlyingAcorn.Analytics;
using FlyingAcorn.Soil.Core.Data;
using FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.AuthPlatforms;
using FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Data;
using Newtonsoft.Json;
using UnityEngine;
using static FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Data.Constants;

namespace FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication
{
    public abstract class SocialAuthentication
    {
        private static bool Ready => SoilServices.Ready && _initialized;

        private static List<ThirdPartySettings> _thirdPartySettings;
        public static Action OnInitializationSuccess { get; set; }
        public static Action<SoilException> OnInitializationFailed { get; set; }
        public static Action<LinkPostResponse> OnLinkSuccessCallback { get; set; }
        public static Action<UnlinkResponse> OnUnlinkSuccessCallback { get; set; }
        public static Action<LinkGetResponse> OnGetAllLinksSuccessCallback { get; set; }
        public static Action<ThirdParty> OnAccessRevoked { get; set; }
        public static Action<ThirdParty, SoilException> OnLinkFailureCallback { get; set; }
        public static Action<ThirdParty, SoilException> OnUnlinkFailureCallback { get; set; }
        public static Action<ThirdParty, SoilException> OnGetAllLinksFailureCallback { get; set; }

        private static List<IPlatformAuthentication> _availableHandlers = new();
        protected static bool _isInitializing;
        protected static bool _initialized;
        private static List<LinkPostResponse> _myLinks = new();
        public static List<LinkPostResponse> LinkedInfo => _myLinks.AsReadOnly().ToList();

        public static void Initialize(List<ThirdPartySettings> thirdPartySettings = null)
        {
            if (Ready)
                return;
            if (_isInitializing)
                return;

            _isInitializing = true;
            _initialized = false;

            _thirdPartySettings = thirdPartySettings;

            if (SoilServices.Ready)
            {
                SoilInitSuccess();
                return;
            }
            UnlistenCore();
            SoilServices.OnInitializationFailed += SoilInitFailed;
            SoilServices.OnServicesReady += SoilInitSuccess;
            
            // Initialize SoilServices if not ready
            SoilServices.InitializeAsync();
        }

        private static void UnlistenCore()
        {
            SoilServices.OnInitializationFailed -= SoilInitFailed;
            SoilServices.OnServicesReady -= SoilInitSuccess;
        }

        private static void SoilInitFailed(Exception exception)
        {
            UnlistenCore();
            _isInitializing = false;
            var soilEx = exception as SoilException ?? new SoilException(exception?.Message ?? "Initialization failed");
            FireInitFailed(soilEx);
        }

        private static void SoilInitSuccess()
        {
            UnlistenCore();
            if (_thirdPartySettings == null)
            {
                _thirdPartySettings = Resources.LoadAll<ThirdPartySettings>("ThirdParties").ToList();
                if (_thirdPartySettings.Count == 0)
                    FireInitFailed(new SoilException("No Third Party Settings found", SoilExceptionErrorCode.NotReady));
            }

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

            OnGetAllLinksSuccessCallback -= OnGetAllLinksSuccess;
            OnGetAllLinksSuccessCallback += OnGetAllLinksSuccess;

            OnGetAllLinksFailureCallback -= OnGetAllLinksFailure;
            OnGetAllLinksFailureCallback += OnGetAllLinksFailure;
            GetLinks();
        }

        private static void OnGetAllLinksSuccess(LinkGetResponse response)
        {
            _myLinks = response != null && response.linked_accounts != null ? response.linked_accounts : new List<LinkPostResponse>();
            foreach (var link in _myLinks)
                LinkingPlayerPrefs.SetUserId(link.detail.app_party.party, link.social_account_info.social_account_id);
            FireInitSuccess();
        }

        private static void OnGetAllLinksFailure(ThirdParty party, SoilException exception)
        {
            FireInitFailed(exception);
        }

        private static void FireInitFailed(SoilException soilException)
        {
            _isInitializing = false;
            _initialized = false;
            OnInitializationFailed?.Invoke(soilException);
        }

        private static void FireInitSuccess()
        {
            _isInitializing = false;
            _initialized = true;
            OnInitializationSuccess?.Invoke();
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

        private static bool IsPartyAvailable(ThirdParty party)
        {
            var settings = GetConfigFile(party);
            if (!settings)
                return false;
            return SoilServices.UserInfo.linkable_parties != null &&
                   SoilServices.UserInfo.linkable_parties.Any(linkableParty => linkableParty.party.ToThirdParty() == party);
        }

        public static void Link(ThirdParty party)
        {
            if (!Ready)
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
                ThirdParty.google => Application.platform switch
                {
                    RuntimePlatform.Android => new GoogleAndroidAuthentication(settings),
                    RuntimePlatform.IPhonePlayer => new GoogleIOSAuthentication(settings),
                    _ => new GoogleOtherPlatformAuthentication(settings)
                },
                ThirdParty.apple => new AppleSignIn(settings),
                ThirdParty.facebook => null,
                ThirdParty.unity => null,
                ThirdParty.none => null,
                _ => throw new SoilException("Unsupported third party", SoilExceptionErrorCode.ServiceUnavailable)
            };
        }

        private static readonly Dictionary<ThirdParty, bool> _unlinkInProgress = new();

        public static void Unlink(ThirdParty party)
        {
            MyDebug.Verbose($"SocialAuthentication.Unlink called for {party}");

            // Guard against concurrent unlink operations for the same party
            lock (_unlinkInProgress)
            {
                if (_unlinkInProgress.ContainsKey(party) && _unlinkInProgress[party])
                {
                    MyDebug.LogWarning($"Unlink already in progress for {party}, ignoring duplicate request");
                    return;
                }
                _unlinkInProgress[party] = true;
            }

            UnlinkAsync(party).Forget();
        }

        private static async UniTask UnlinkAsync(ThirdParty party)
        {
            try
            {
                MyDebug.Verbose($"UnlinkAsync called for {party}");
                if (!Ready)
                {
                    MyDebug.LogWarning("Social Authentication not initialized");
                    OnUnlinkFailureCallback?.Invoke(party, new SoilException("Social Authentication not initialized",
                        SoilExceptionErrorCode.NotReady));
                    return;
                }

                var settings = GetConfigFile(party);
                try
                {
                    MyDebug.Verbose($"Calling ThirdPartyAPIHandler.Unlink for {party}");
                    var unlinkResponse = await ThirdPartyAPIHandler.Unlink(settings);
                    MyDebug.Verbose($"ThirdPartyAPIHandler.Unlink completed for {party}. Response: {JsonConvert.SerializeObject(unlinkResponse)}");
                    MyDebug.Verbose($"About to invoke OnUnlinkSuccessCallback for {party}");
                    _myLinks.RemoveAll(link => link.detail.app_party.party == party);
                    LinkingPlayerPrefs.SetUserId(party, "");
                    OnUnlinkSuccessCallback?.Invoke(unlinkResponse);
                    MyDebug.Verbose($"OnUnlinkSuccessCallback invoked for {party}");
                }
                catch (SoilException e)
                {
                    MyDebug.LogWarning($"Unlink failed for {party}: {e.Message}");
                    LinkingPlayerPrefs.EnqueueSilentUnlink(party);
                    OnUnlinkFailureCallback?.Invoke(party, e);
                }
                catch (Exception e)
                {
                    MyDebug.LogWarning($"Unlink failed for {party} with unexpected error: {e.Message}");
                    LinkingPlayerPrefs.EnqueueSilentUnlink(party);
                    var soilException = new SoilException(e.Message);
                    OnUnlinkFailureCallback?.Invoke(party, soilException);
                }
            }
            finally
            {
                // Clear the in-progress flag
                lock (_unlinkInProgress)
                {
                    _unlinkInProgress[party] = false;
                }
                MyDebug.Verbose($"UnlinkAsync completed for {party}, cleared in-progress flag");
            }
        }

        public static void GetLinks()
        {
            GetLinksAsync().Forget();
        }

        private static async UniTask GetLinksAsync()
        {
            if (!SoilServices.Ready) // Only needs to check if SoilServices is ready
            {
                MyDebug.LogWarning("Social Authentication not initialized");
                OnGetAllLinksFailureCallback?.Invoke(ThirdParty.none,
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
                OnGetAllLinksFailureCallback?.Invoke(ThirdParty.none, e);
            }
            catch (Exception e)
            {
                MyDebug.LogWarning(e);
                var soilException = new SoilException(e.Message);
                OnGetAllLinksFailureCallback?.Invoke(ThirdParty.none, soilException);
            }
        }

        private static void OnSigninSuccess(LinkAccountInfo thirdPartyUser, ThirdPartySettings settings)
        {
            OnSigninSuccessAsync(thirdPartyUser, settings).Forget();
        }

        private static async UniTask OnSigninSuccessAsync(LinkAccountInfo thirdPartyUser, ThirdPartySettings settings)
        {
            try
            {
                MyDebug.Info("Linking user with " + settings.ThirdParty);
                var authenticatedUser = await ThirdPartyAPIHandler.Link(thirdPartyUser, settings);
                if (_myLinks.Any(link => link.detail.app_party.party == settings.ThirdParty))
                    _myLinks.RemoveAll(link => link.detail.app_party.party == settings.ThirdParty);
                _myLinks.Add(authenticatedUser);
                LinkingPlayerPrefs.SetUserId(settings.ThirdParty, thirdPartyUser.social_account_id);
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

        private static ThirdPartySettings GetConfigFile(ThirdParty party)
        {
            return _thirdPartySettings?.Find(settings =>
                settings.ThirdParty == party && settings.Platform == Application.platform);
        }

        public static void Update()
        {
            foreach (var link in _availableHandlers)
                link.Update();
        }

        public static void UpdateSpecific(ThirdParty party)
        {
            foreach (var link in _availableHandlers.Where(link => link.ThirdPartySettings.ThirdParty == party))
                link.Update();
        }
    }
}