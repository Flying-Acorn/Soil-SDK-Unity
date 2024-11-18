using System;
using System.Threading.Tasks;
using FlyingAcorn.Analytics;
using FlyingAcorn.Soil.Core.Data;
using FlyingAcorn.Soil.Core.JWTTools;
using FlyingAcorn.Soil.Core.User;
using JetBrains.Annotations;
using Constants = FlyingAcorn.Soil.Core.Data.Constants;

namespace FlyingAcorn.Soil.Core
{
    public static class SoilServices
    {
        private static bool _readyBroadcasted;
        private static Task _initTask;
        [UsedImplicitly] public static UserInfo UserInfo => UserPlayerPrefs.UserInfoInstance;
        [UsedImplicitly] public static Action OnServicesReady;
        [UsedImplicitly] public static bool Ready => _initTask is { IsCompleted: true };

        public static async Task Initialize()
        {
            switch (Ready)
            {
                case true when !JwtUtils.IsTokenValid(UserPlayerPrefs.TokenData.Access):
                    try
                    {
                        _initTask = Authenticate.RefreshTokenIfNeeded();
                    }
                    catch (Exception e)
                    {
                        MyDebug.LogWarning("Soil: " + $"Failed to refresh token " + e.Message + " " +
                                           e.StackTrace);
                    }

                    break;
                case true:
                    return;
            }

            if (UserPlayerPrefs.AppID == Constants.DemoAppID ||
                UserPlayerPrefs.SDKToken == Constants.DemoAppSDKToken)
                MyDebug.LogError(
                    $"Soil-Core: AppID or SDKToken are not set. You must create and fill a {nameof(SDKSettings)}. Using demo values.");

            try
            {
                _initTask ??= Authenticate.AuthenticateUser();
                await _initTask;
            }
            catch (Exception e)
            {
                MyDebug.LogWarning("Soil: " + $"Failed to authenticate user " + e.Message + " " +
                                   e.StackTrace);
                throw;
            }

            if (_readyBroadcasted) return;
            _readyBroadcasted = true;
            OnServicesReady?.Invoke();
        }
    }
}