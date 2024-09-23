using System.Threading.Tasks;
using FlyingAcorn.Soil.Core.Data;
using FlyingAcorn.Soil.Core.User;
using UnityEngine;

namespace FlyingAcorn.Soil.Core
{
    public static class SoilServices
    {
        public static UserInfo UserInfo => AuthenticatePlayerPrefs.UserInfoInstance;

        public static async Task Initialize()
        {
            if (AuthenticatePlayerPrefs.AppID == Constants.DemoAppID ||
                AuthenticatePlayerPrefs.SDKToken == Constants.DemoAppSDKToken)
                Debug.LogError(
                    "AppID or SDKToken are not set. You must call SetRegistrationInfo at least once. Using demo values.");
            await Authenticate.AuthenticateUser();
        }

        public static void SetRegistrationInfo(string appID, string sdkToken)
        {
            AuthenticatePlayerPrefs.AppID = appID;
            AuthenticatePlayerPrefs.SDKToken = sdkToken;
        }
    }
}