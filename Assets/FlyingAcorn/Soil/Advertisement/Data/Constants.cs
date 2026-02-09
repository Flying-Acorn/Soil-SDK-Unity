using FlyingAcorn.Soil.Core.Data;

namespace FlyingAcorn.Soil.Advertisement.Data
{
    public class Constants
    {
        public string AssetsBaseDomain => DataUtils.GetTheHatedRegionDomain();
        public enum AdFormat
        {
            banner,
            interstitial,
            rewarded,
        }

        public enum AssetType
        {
            image,
            video,
            header_text,
            description_text,
            button_text,
            logo
        }

        public enum SelectionReason
        {
            performance_optimized,
            only_eligible,
            round_robin,
            random
        }

        public enum AdError
        {
            None,
            Unknown,
            NoFill,
            NetworkError,
            InternalError,
            InvalidRequest,
            Timeout,
            AdAlreadyLoaded,
            AdNotReady,
            AdClosedByUser
        }

        public enum AdPosition
        {
            TopCenter,
            MiddleCenter,
            BottomCenter,
        }
    }
}