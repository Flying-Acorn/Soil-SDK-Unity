using System.Collections.Generic;

namespace FlyingAcorn.Soil.Advertisement.Logic
{
    /// <summary>
    /// Which native asset slot a cached entry fills.
    /// </summary>
    public enum NativeAdAssetKind
    {
        Icon,
        MainImage
    }

    /// <summary>
    /// A Unity-free description of one cached native asset, so the assembly rules below can be
    /// unit-tested without a player loop or a real asset cache.
    /// </summary>
    public sealed class NativeAdAssetInfo
    {
        public string Id;
        public NativeAdAssetKind Kind;
        public int Width;
        public int Height;
        public long SizeBytes;
        public string ClickUrl;

        /// <summary>Id of the ad this asset came from; used to keep one creative intact.</summary>
        public string AdId;
        public string TitleText;
        public string DescriptionText;
        public string CallToActionText;
    }

    public enum NativeAdContentError
    {
        None,
        /// <summary>Nothing cached for the native format yet.</summary>
        NoAssets,
        /// <summary>No icon at all among the cached assets.</summary>
        MissingIcon,
        /// <summary>An icon exists but does not meet <see cref="NativeAdAssetSpec"/>.</summary>
        OffSpecIcon,
        MissingTitle,
        MissingCallToAction
    }

    /// <summary>
    /// The assembled native ad, ready for the game to render. Texture loading happens in the
    /// Unity layer; this model only carries ids and text.
    /// </summary>
    public sealed class NativeAdContentModel
    {
        public string AdId;
        public string Title;
        public string Description;
        public string CallToAction;
        public string ClickUrl;
        public string IconAssetId;
        public string MainImageAssetId;

        public bool HasMainImage => !string.IsNullOrEmpty(MainImageAssetId);
    }

    /// <summary>
    /// Assembles a native ad out of the cached assets for the native format.
    ///
    /// The icon anchors the creative: the headline, body and call to action are taken from the
    /// SAME ad as the icon, and the main image is only used when it belongs to that ad too. An
    /// ad group can hold several native ads, so picking each field independently would show one
    /// advertiser's icon next to another's headline - the same mixing bug that was fixed for
    /// banners. An off-spec main image is dropped (the ad still renders icon-only); an off-spec
    /// icon fails the whole build, because there is no icon-less native layout.
    /// </summary>
    public static class NativeAdContentBuilder
    {
        public static NativeAdContentModel Build(IList<NativeAdAssetInfo> assets, out NativeAdContentError error)
        {
            if (assets == null || assets.Count == 0)
            {
                error = NativeAdContentError.NoAssets;
                return null;
            }

            NativeAdAssetInfo icon = null;
            var sawIcon = false;
            foreach (var asset in assets)
            {
                if (asset == null || asset.Kind != NativeAdAssetKind.Icon)
                    continue;

                sawIcon = true;
                if (NativeAdAssetSpec.IsValidIcon(asset.Width, asset.Height, asset.SizeBytes))
                {
                    icon = asset;
                    break;
                }
            }

            if (icon == null)
            {
                error = sawIcon ? NativeAdContentError.OffSpecIcon : NativeAdContentError.MissingIcon;
                return null;
            }

            if (string.IsNullOrEmpty(icon.TitleText))
            {
                error = NativeAdContentError.MissingTitle;
                return null;
            }

            if (string.IsNullOrEmpty(icon.CallToActionText))
            {
                error = NativeAdContentError.MissingCallToAction;
                return null;
            }

            var mainImage = FindMainImageFor(assets, icon.AdId);

            error = NativeAdContentError.None;
            return new NativeAdContentModel
            {
                AdId = icon.AdId,
                Title = icon.TitleText,
                Description = icon.DescriptionText,
                CallToAction = icon.CallToActionText,
                ClickUrl = !string.IsNullOrEmpty(icon.ClickUrl) ? icon.ClickUrl : mainImage?.ClickUrl,
                IconAssetId = icon.Id,
                MainImageAssetId = mainImage?.Id
            };
        }

        /// <summary>
        /// The main image must belong to the same ad as the icon. When the cached entry carries no
        /// ad id (older cache entries did not record one) it is accepted, since there is nothing to
        /// contradict and an icon-only ad is the worse outcome.
        /// </summary>
        private static NativeAdAssetInfo FindMainImageFor(IList<NativeAdAssetInfo> assets, string adId)
        {
            foreach (var asset in assets)
            {
                if (asset == null || asset.Kind != NativeAdAssetKind.MainImage)
                    continue;

                var belongsToSameAd = string.IsNullOrEmpty(asset.AdId)
                    || string.IsNullOrEmpty(adId)
                    || asset.AdId == adId;
                if (!belongsToSameAd)
                    continue;

                if (NativeAdAssetSpec.IsValidImage(asset.Width, asset.Height, asset.SizeBytes))
                    return asset;
            }

            return null;
        }
    }
}
