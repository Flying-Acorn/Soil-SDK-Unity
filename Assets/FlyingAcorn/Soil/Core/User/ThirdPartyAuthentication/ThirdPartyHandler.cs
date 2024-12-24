using System;
using System.Threading.Tasks;
using FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.AuthPlatforms;
using FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Data;
using JetBrains.Annotations;
using UnityEngine;

namespace FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication
{
    public abstract class ThirdPartyHandler
    {
        public const string AndroidSettingName = "AndroidGoogleAuthSetting";
        public const string IOSSettingName = "IOSGoogleAuthSetting";

        public static string CurrentPlatformSettingName =>
            Application.platform == RuntimePlatform.Android ? AndroidSettingName : IOSSettingName;

        public static Action<LinkModel> OnLinkSuccessCallback { get; set; }
        public static Action<string> OnLinkFailureCallback { get; set; }

        public static async Task Initialize()
        {
            await SoilServices.Initialize();
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

        private static Task<LinkModel> LinkSoilUser(LinkAccountInfo thirdPartyUser)
        {
            throw new NotImplementedException();
        }

        private static async void OnSigninSuccess(LinkAccountInfo thirdPartyUser)
        {
            try
            {
                var authenticatedUser = await LinkSoilUser(thirdPartyUser);
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

            if (party != Constants.ThirdParty.Google)
            {
                throw new NotSupportedException($"Third party {party} is not supported");
            }

            return configurations;
        }

        [Serializable]
        public class LinkModel
        {
            [UsedImplicitly] public string ThirdParty { get; set; }
            [UsedImplicitly] public LinkAccountInfo User { get; set; }
            [UsedImplicitly] public string LinkStatus { get; set; }
        }
    }
}