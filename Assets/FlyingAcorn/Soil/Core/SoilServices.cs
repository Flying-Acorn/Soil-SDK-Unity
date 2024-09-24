using System;
using System.Threading.Tasks;
using FlyingAcorn.Soil.Core.Data;
using FlyingAcorn.Soil.Core.User;
using UnityEngine;

namespace FlyingAcorn.Soil.Core
{
    public static class SoilServices
    {
        public static UserInfo UserInfo => UserPlayerPrefs.UserInfoInstance;
        public static Action OnServicesReady;

        public static bool Ready;

        public static async Task Initialize()
        {
            if (UserPlayerPrefs.AppID == Constants.DemoAppID ||
                UserPlayerPrefs.SDKToken == Constants.DemoAppSDKToken)
                Debug.LogError(
                    "AppID or SDKToken are not set. You must call SetRegistrationInfo at least once. Using demo values.");
            await Authenticate.AuthenticateUser();
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