using System;
using System.Threading.Tasks;
using FlyingAcorn.Analytics;
using FlyingAcorn.Soil.Core.Data;
using FlyingAcorn.Soil.Core.JWTTools;
using FlyingAcorn.Soil.Core.User;
using FlyingAcorn.Soil.Core.User.Authentication;
using JetBrains.Annotations;
using UnityEngine;
using Constants = FlyingAcorn.Soil.Core.Data.Constants;
using Object = UnityEngine.Object;

namespace FlyingAcorn.Soil.Core
{
    public static class SoilServices
    {
        private static DeepLinkHandler _deepLinkComponent;
        private static bool _readyBroadcasted;
        private static Task _initTask;
        private static GameObject _soilCoreGameObject;
        [UsedImplicitly] public static UserInfo UserInfo => UserPlayerPrefs.UserInfoInstance;
        [UsedImplicitly] public static Action OnServicesReady;
        [UsedImplicitly] public static bool Ready => _initTask is { IsCompletedSuccessfully: true };

        static SoilServices()
        {
            UserApiHandler.OnUserFilled += userChanged =>
            {
                if (!userChanged) return;
                _readyBroadcasted = false;
                _ = Initialize();
            };
        }

        public static async Task Initialize()
        {
            _soilCoreGameObject = GameObject.Find("SoilCore");
            if (!_soilCoreGameObject)
            {
                _soilCoreGameObject = new GameObject("SoilCore");
                Object.DontDestroyOnLoad(_soilCoreGameObject);
            }
            SetupDeeplink();

            if (_initTask is { IsCompletedSuccessfully: false, IsCompleted: true })
                _initTask = null;

            switch (Ready)
            {
                case true when !JwtUtils.IsTokenValid(UserPlayerPrefs.TokenData.Access):
                    try
                    {
                        _initTask = null;
                        _initTask = Authenticate.RefreshTokenIfNeeded();
                    }
                    catch (Exception e)
                    {
                        MyDebug.LogWarning("Soil-Core: " + $"Failed to refresh token {e.Message} {e.StackTrace}");
                    }

                    break;
                case true:
                    BroadcastReady();
                    return;
            }

            if (UserPlayerPrefs.AppID == Constants.DemoAppID ||
                UserPlayerPrefs.SDKToken == Constants.DemoAppSDKToken)
                MyDebug.LogError(
                    $"Soil-Core: AppID or SDKToken are not set. You must create and fill {nameof(SDKSettings)}. Using demo values.");

            try
            {
                _initTask ??= Authenticate.AuthenticateUser();
                await _initTask;
            }
            catch (Exception e)
            {
                MyDebug.Info("Soil: " + $"Failed to authenticate user " + e.Message + " " +
                                   e.StackTrace);
                throw;
            }

            BroadcastReady();
        }

        private static void BroadcastReady()
        {
            if (_readyBroadcasted) return;
            MyDebug.Info("Soil-Core: Services are ready");

            _readyBroadcasted = true;
            OnServicesReady?.Invoke();
        }

        [UsedImplicitly]
        public static void SetupDeeplink()
        {
            if (!UserPlayerPrefs.DeepLinkActivated) return;
            if (_deepLinkComponent)
                return;

            _deepLinkComponent = _soilCoreGameObject.AddComponent<DeepLinkHandler>();
        }
    }
}