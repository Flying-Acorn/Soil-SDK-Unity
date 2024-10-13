using System;
using System.Threading.Tasks;
using FlyingAcorn.Analytics;
using FlyingAcorn.Soil.Core.User;
using JetBrains.Annotations;
using Constants = FlyingAcorn.Soil.Core.Data.Constants;

namespace FlyingAcorn.Soil.Core
{
    public static class SoilServices
    {
        [UsedImplicitly] public static UserInfo UserInfo => UserPlayerPrefs.UserInfoInstance;
        [UsedImplicitly] public static Action OnServicesReady;

        public static bool Ready;

        public static async Task Initialize()
        {
            if (UserPlayerPrefs.AppID == Constants.DemoAppID ||
                UserPlayerPrefs.SDKToken == Constants.DemoAppSDKToken)
                MyDebug.LogError(
                    "AppID or SDKToken are not set. You must call SetRegistrationInfo at least once. Using demo values.");
            try
            {
                await Authenticate.AuthenticateUser();
            }
            catch (Exception e)
            {
                MyDebug.LogWarning("Failed to authenticate user: " + e.Message + " " + e.StackTrace);
                throw;
            }
            Ready = true;
            OnServicesReady?.Invoke();
        }

        public static void SetRegistrationInfo(string appID, string sdkToken)
        {
            UserPlayerPrefs.AppID = appID;
            UserPlayerPrefs.SDKToken = sdkToken;
        }
    }
}