using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;

namespace FlyingAcorn.Soil.Advertisement.Logic
{
    public enum CreativeAssetKind
    {
        Image,
        Video,
        Logo
    }

    /// <summary>
    /// A Unity-free description of one cached banner/interstitial/rewarded asset whose file is
    /// already on disk.
    /// </summary>
    public sealed class CreativeAssetInfo
    {
        public string Id;
        public CreativeAssetKind Kind;
        public string LocalPath;
        public string ClickUrl;

        /// <summary>Id of the ad this asset came from; its texts travel with it.</summary>
        public string AdId;
        public string TitleText;
        public string DescriptionText;
        public string CallToActionText;
    }

    public enum AdCreativeError
    {
        None,
        /// <summary>Nothing cached for the format.</summary>
        NoAssets,
        /// <summary>Assets exist, but no video or image to show (and, for a banner, no title either).</summary>
        NoMedia
    }

    /// <summary>
    /// What the native player draws for one banner, interstitial or rewarded ad. Serialized as
    /// the creative JSON of NativeAds/PROTOCOL.md.
    /// </summary>
    public sealed class AdCreative
    {
        [JsonProperty("adId")] public string AdId;
        [JsonProperty("videoPath")] public string VideoPath;
        [JsonProperty("imagePath")] public string ImagePath;
        [JsonProperty("logoPath")] public string LogoPath;
        [JsonProperty("title")] public string Title;
        [JsonProperty("description")] public string Description;
        [JsonProperty("callToAction")] public string CallToAction;
        [JsonProperty("clickUrl")] public string ClickUrl;

        /// <summary>Ids of the cached assets used, so the Unity layer can describe the ad in events.</summary>
        [JsonIgnore] public string VideoAssetId;
        [JsonIgnore] public string ImageAssetId;
        [JsonIgnore] public string LogoAssetId;

        [JsonIgnore] public bool HasVideo => !string.IsNullOrEmpty(VideoPath);
        [JsonIgnore] public bool HasImage => !string.IsNullOrEmpty(ImagePath);

        public string ToJson()
        {
            return JsonConvert.SerializeObject(this, Formatting.None,
                new JsonSerializerSettings { NullValueHandling = NullValueHandling.Ignore });
        }
    }

    /// <summary>
    /// Picks what one banner, interstitial or rewarded ad shows out of the assets cached for its
    /// format.
    ///
    /// The main media anchors the creative: a video (fullscreen formats only) wins over an image,
    /// and the texts and click URL come from the SAME ad as that media, so the call to action on
    /// screen always belongs to the advertiser whose media is playing. An image from another ad
    /// of the same ad group is still kept next to a video as its fallback, which is why the cache
    /// stores both.
    /// </summary>
    public static class AdCreativeBuilder
    {
        public static AdCreative Build(string format, IList<CreativeAssetInfo> assets, out AdCreativeError error)
        {
            var usable = assets?.Where(a => a != null && !string.IsNullOrEmpty(a.LocalPath)).ToList()
                         ?? new List<CreativeAssetInfo>();
            if (usable.Count == 0)
            {
                error = AdCreativeError.NoAssets;
                return null;
            }

            var isBanner = format == AdFormats.Banner;
            var video = isBanner ? null : usable.FirstOrDefault(a => a.Kind == CreativeAssetKind.Video);
            var images = usable.Where(a => a.Kind == CreativeAssetKind.Image).ToList();
            var image = SameAd(images, video) ?? images.FirstOrDefault();
            var main = video ?? image;

            var logos = usable.Where(a => a.Kind == CreativeAssetKind.Logo).ToList();
            var logo = SameAd(logos, main) ?? logos.FirstOrDefault();

            // Texts belong to the ad whose media is on screen; a logo-only cache still has them.
            var textSource = main ?? logo;
            var title = NullIfEmpty(textSource?.TitleText);

            if (main == null && !(isBanner && title != null))
            {
                error = AdCreativeError.NoMedia;
                return null;
            }

            error = AdCreativeError.None;
            return new AdCreative
            {
                AdId = textSource?.AdId ?? main?.Id ?? logo?.Id,
                VideoPath = video?.LocalPath,
                VideoAssetId = video?.Id,
                ImagePath = image?.LocalPath,
                ImageAssetId = image?.Id,
                LogoPath = logo?.LocalPath,
                LogoAssetId = logo?.Id,
                Title = title,
                Description = NullIfEmpty(textSource?.DescriptionText),
                CallToAction = NullIfEmpty(textSource?.CallToActionText),
                // Only web and store links survive (AdLinkPolicy); a refused link is no link.
                ClickUrl = AdLinkPolicy.Sanitize(main?.ClickUrl)
                           ?? usable.Select(a => AdLinkPolicy.Sanitize(a.ClickUrl)).FirstOrDefault(u => u != null)
            };
        }

        private static CreativeAssetInfo SameAd(List<CreativeAssetInfo> candidates, CreativeAssetInfo anchor)
        {
            if (anchor == null || string.IsNullOrEmpty(anchor.AdId)) return null;
            return candidates.FirstOrDefault(c => c.AdId == anchor.AdId);
        }

        private static string NullIfEmpty(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value;
        }
    }
}
