using System.Threading.Tasks;
using FlyingAcorn.Soil.Core.User;

namespace FlyingAcorn.Soil.Core
{
    public static class SoilServices
    {
        public static async Task Initialize(string appID, string sdkToken)
        {
            await Authenticate.AuthenticateUser(appID, sdkToken);
        }
    }
}