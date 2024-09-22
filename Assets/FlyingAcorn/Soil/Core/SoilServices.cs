using System.Threading.Tasks;
using FlyingAcorn.Soil.Core.User;

namespace FlyingAcorn.Soil.Core
{
    public static class SoilServices
    {
        public static UserInfo UserInfo => AuthenticatePlayerPrefs.UserInfoInstance;

        public static async Task Initialize()
        {
            await Authenticate.AuthenticateUser(AuthenticatePlayerPrefs.AppID, AuthenticatePlayerPrefs.SDKToken);
        }

        public static void SetRegistrationInfo(string appID, string sdkToken)
        {
            AuthenticatePlayerPrefs.AppID = appID;
            AuthenticatePlayerPrefs.SDKToken = sdkToken;
        }
    }
}