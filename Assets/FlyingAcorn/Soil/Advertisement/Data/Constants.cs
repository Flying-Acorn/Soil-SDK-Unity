using System;

namespace FlyingAcorn.Soil.Advertisement.Data
{
    public class Constants
    {
        // Relative asset URLs are served by the same Soil host that answered the ad request, so
        // the player's region decides where ad media comes from - never one fixed regional domain.
        public string AssetsBaseDomain => new Uri(Core.Data.Constants.ApiUrl).GetLeftPart(UriPartial.Authority);
        public enum AdFormat
        {
            banner,
            interstitial,
            rewarded,
            native,
        }

        public enum AssetType
        {
            image,
            video,
            header_text,
            description_text,
            button_text,
            logo,
            native_icon,
            native_image
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