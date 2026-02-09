using FlyingAcorn.Soil.Core.Data;

namespace FlyingAcorn.Soil.Purchasing
{
    public static class Constants
    {
        internal static string ApiUrl => $"{DataUtils.GetTheHatedRegionDomain()}/api/iap";
    }
}