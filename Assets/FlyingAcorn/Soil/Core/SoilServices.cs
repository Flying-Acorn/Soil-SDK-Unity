using System;
using System.Threading.Tasks;
using FlyingAcorn.Analytics;
using FlyingAcorn.Soil.Core.Data;
using FlyingAcorn.Soil.Core.User;
using JetBrains.Annotations;
using Constants = FlyingAcorn.Soil.Core.Data.Constants;

namespace FlyingAcorn.Soil.Core
{
    public static class SoilServices
    {
        private static Task _initTask;
        [UsedImplicitly] public static UserInfo UserInfo => UserPlayerPrefs.UserInfoInstance;
        [UsedImplicitly] public static Action OnServicesReady;
        public static bool Ready => _initTask is { IsCompleted: true };

        public static async Task Initialize()
        {
            if (UserPlayerPrefs.AppID == Constants.DemoAppID ||
                UserPlayerPrefs.SDKToken == Constants.DemoAppSDKToken)
                MyDebug.LogError(
                    $"AppID or SDKToken are not set. You must create and fill a {nameof(SDKSettings)}. Using demo values.");
            try
            {
                _initTask ??= Authenticate.AuthenticateUser();
                await _initTask;
            }
            catch (Exception e)
            {
                MyDebug.LogWarning("Failed to authenticate user: " + e.Message + " " + e.StackTrace);
                throw;
            }
            OnServicesReady?.Invoke();
        }
    }
}