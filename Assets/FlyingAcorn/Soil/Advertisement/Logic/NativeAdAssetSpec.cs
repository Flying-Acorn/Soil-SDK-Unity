namespace FlyingAcorn.Soil.Advertisement.Logic
{
    /// <summary>
    /// The fixed size standard every native ad asset must meet. Native ads are drawn by the
    /// game inside a layout the SDK does not control, so - unlike banner/interstitial creatives,
    /// which are allowed a range of ratios - each native asset has exactly one accepted
    /// dimension and one byte budget.
    ///
    /// The main image is pinned to 1.91:1 at 1200x628, the most widely used landscape ad-image
    /// size across ad platforms, so an advertiser can usually reuse a creative they already have
    /// and the host layout can reserve a fixed slot for it. The icon is only required to be
    /// square (1:1) at any size, because it is scaled into a small view; the ceiling just guards
    /// device texture memory.
    ///
    /// These MUST stay in sync with the server-side advertisement/validators.py
    /// NATIVE_ASSET_SPECS, which rejects off-spec uploads.
    ///
    /// The client re-checks the spec because a cached asset may predate a server-side change,
    /// and rendering an off-spec creative distorts the host layout.
    /// </summary>
    public static class NativeAdAssetSpec
    {
        /// <summary>Icons must be square; any pixel size up to this ceiling is accepted.</summary>
        public const int IconMaxDimension = 2000;
        public const int IconMaxSizeMb = 1;

        public const int ImageWidth = 1200;
        public const int ImageHeight = 628;
        public const int ImageMaxSizeMb = 2;

        public const long IconMaxSizeBytes = (long)IconMaxSizeMb * 1024 * 1024;
        public const long ImageMaxSizeBytes = (long)ImageMaxSizeMb * 1024 * 1024;

        /// <summary>
        /// An icon is valid when it is square and within budget. The pixel count is deliberately
        /// not pinned: the icon is scaled into a small view, so only the 1:1 shape matters -
        /// a non-square icon would distort or crop in the host layout.
        /// </summary>
        public static bool IsValidIcon(int width, int height, long sizeBytes)
        {
            if (!IsWithinBudget(sizeBytes, IconMaxSizeBytes))
                return false;

            // Non-positive means "unknown" - see MatchesDimensions.
            if (width <= 0 || height <= 0)
                return true;

            return width == height && width <= IconMaxDimension;
        }

        public static bool IsValidImage(int width, int height, long sizeBytes)
        {
            return MatchesDimensions(width, height, ImageWidth, ImageHeight)
                && IsWithinBudget(sizeBytes, ImageMaxSizeBytes);
        }

        /// <summary>
        /// Non-positive dimensions mean "unknown" - the server did not report them, or the cache
        /// entry predates them. Unknown is not a violation: the upload was already validated
        /// server-side, and blocking on missing metadata would drop perfectly good ads. Only a
        /// dimension we can actually see to be wrong is rejected.
        /// </summary>
        private static bool MatchesDimensions(int width, int height, int expectedWidth, int expectedHeight)
        {
            if (width <= 0 || height <= 0)
                return true;

            return width == expectedWidth && height == expectedHeight;
        }

        /// <summary>
        /// A non-positive size means "unknown" (e.g. the entry was restored from prefs before the
        /// file was measured); that is not treated as a violation, only an over-budget file is.
        /// </summary>
        private static bool IsWithinBudget(long sizeBytes, long maxBytes)
        {
            return sizeBytes <= 0 || sizeBytes <= maxBytes;
        }
    }
}
