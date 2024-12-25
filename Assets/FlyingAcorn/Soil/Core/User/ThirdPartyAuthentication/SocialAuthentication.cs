using System;
using System.Threading.Tasks;
using FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.AuthPlatforms;
using FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Data;
using JetBrains.Annotations;
using UnityEngine;

namespace FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication
{
    public abstract class SocialAuthentication
    {
        private static Task _thirdPartyInitializer;
        [UsedImplicitly] public const string AndroidSettingName = "AndroidGoogleAuthSetting";
        [UsedImplicitly] public const string IOSSettingName = "IOSGoogleAuthSetting";
        private const string EditorSettingsName = "EditorGoogleAuthSetting";

        private static string CurrentPlatformSettingName
        {
            get
            {
                if (Application.isEditor)
                    return EditorSettingsName;
                if (Application.platform == RuntimePlatform.Android)
                    return AndroidSettingName;
                if (Application.platform == RuntimePlatform.IPhonePlayer)
                    return IOSSettingName;
                return null;
            }
        }

        public static Action<LinkPostResponse> OnLinkSuccessCallback { get; set; }
        public static Action<UnlinkResponse> OnUnlinkSuccessCallback { get; set; }
        public static Action<LinkGetResponse> OnGetAllLinksSuccessCallback { get; set; }
        public static Action<string> OnLinkFailureCallback { get; set; }
        public static Action<string> OnUnlinkFailureCallback { get; set; }
        public static Action<string> OnGetAllLinksFailureCallback { get; set; }

        public static async Task Initialize()
        {
            await SoilServices.Initialize();
            _thirdPartyInitializer ??= UserApiHandler.FetchPlayerInfo();
            await _thirdPartyInitializer;
        }

        public static async void Link(Constants.ThirdParty party)
        {
            try
            {
                await Initialize();
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                OnLinkFailureCallback?.Invoke(e.Message);
                return;
            }

            var settings = GetConfigFile(party);
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
            try
            {
                await Initialize();
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                OnUnlinkFailureCallback?.Invoke(e.Message);
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
                await Initialize();
            }
            catch (Exception e)
            {
                Debug.LogError(e);
                OnGetAllLinksFailureCallback?.Invoke(e.Message);
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
            catch (Exception e)
            {
                Debug.LogWarning(e);
                OnLinkFailureCallback?.Invoke(e.Message);
            }
        }

        private static ThirdPartySettings GetConfigFile(Constants.ThirdParty party)
        {
            var configurations = Resources.Load<ThirdPartySettings>(CurrentPlatformSettingName);
            if (!configurations)
            {
                throw new Exception("Third party settings not found");
            }

            if (party != Constants.ThirdParty.google)
            {
                throw new NotSupportedException($"Third party {party} is not supported");
            }

            return configurations;
        }
    }
}