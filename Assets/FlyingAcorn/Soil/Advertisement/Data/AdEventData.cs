using FlyingAcorn.Soil.Advertisement.Models;
using static FlyingAcorn.Soil.Advertisement.Data.Constants;

namespace FlyingAcorn.Soil.Advertisement.Data
{
    public class AdEventData
    {
        public AdFormat AdFormat;
        public AdError AdError;
        public Ad ad;

        public AdEventData(AdFormat adFormat, AdError adError = AdError.None)
        {
            AdFormat = adFormat;
            AdError = adError;
        }
    }
}