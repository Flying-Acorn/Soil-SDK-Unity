using FlyingAcorn.Soil.Advertisement.Models;
using static FlyingAcorn.Soil.Advertisement.Data.Constants;

namespace FlyingAcorn.Soil.Advertisement.Data
{
    public class AdEventData
    {
        public readonly AdFormat AdFormat;
        public readonly AdError AdError;
        public readonly Ad ad;

        protected AdEventData(AdFormat adFormat, AdError adError = AdError.None)
        {
            AdFormat = adFormat;
            AdError = adError;
        }
    }
}