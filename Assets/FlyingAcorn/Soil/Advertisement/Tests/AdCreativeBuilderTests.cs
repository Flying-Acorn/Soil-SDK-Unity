using System.Collections.Generic;
using FlyingAcorn.Soil.Advertisement.Logic;
using Newtonsoft.Json.Linq;
using NUnit.Framework;

namespace FlyingAcorn.Soil.Advertisement.Tests
{
    /// <summary>
    /// Which cached files a banner, interstitial or rewarded ad plays, and that the texts and
    /// click URL shown always belong to the ad whose media is on screen.
    /// </summary>
    public class AdCreativeBuilderTests
    {
        private static CreativeAssetInfo Asset(CreativeAssetKind kind, string adId, string id = null,
            string title = null, string description = null, string cta = null, string clickUrl = null)
        {
            return new CreativeAssetInfo
            {
                Id = id ?? $"{adId}-{kind}",
                Kind = kind,
                LocalPath = $"/cache/{adId}-{kind}",
                AdId = adId,
                TitleText = title,
                DescriptionText = description,
                CallToActionText = cta,
                ClickUrl = clickUrl
            };
        }

        [Test]
        public void NoAssets_IsAnError()
        {
            Assert.IsNull(AdCreativeBuilder.Build(AdFormats.Interstitial, null, out var error));
            Assert.AreEqual(AdCreativeError.NoAssets, error);

            Assert.IsNull(AdCreativeBuilder.Build(AdFormats.Interstitial, new List<CreativeAssetInfo>(), out error));
            Assert.AreEqual(AdCreativeError.NoAssets, error);
        }

        [Test]
        public void AssetsWithoutAFile_AreIgnored()
        {
            var image = Asset(CreativeAssetKind.Image, "a");
            image.LocalPath = "";
            Assert.IsNull(AdCreativeBuilder.Build(AdFormats.Interstitial, new[] { image, null }, out var error));
            Assert.AreEqual(AdCreativeError.NoAssets, error);
        }

        [Test]
        public void Fullscreen_PrefersVideo_AndKeepsAnImageAsFallback()
        {
            var assets = new[]
            {
                Asset(CreativeAssetKind.Image, "image-ad", title: "Image title"),
                Asset(CreativeAssetKind.Video, "video-ad", title: "Video title", cta: "Play", clickUrl: "https://v")
            };

            var creative = AdCreativeBuilder.Build(AdFormats.Rewarded, assets, out var error);

            Assert.AreEqual(AdCreativeError.None, error);
            Assert.AreEqual("/cache/video-ad-Video", creative.VideoPath);
            Assert.AreEqual("/cache/image-ad-Image", creative.ImagePath, "an image from another ad of the group is the fallback");
            Assert.AreEqual("Video title", creative.Title, "texts follow the media on screen");
            Assert.AreEqual("Play", creative.CallToAction);
            Assert.AreEqual("https://v", creative.ClickUrl);
            Assert.AreEqual("video-ad", creative.AdId);
        }

        [Test]
        public void Fullscreen_PrefersTheImageOfTheVideosOwnAd()
        {
            var assets = new[]
            {
                Asset(CreativeAssetKind.Image, "other"),
                Asset(CreativeAssetKind.Video, "same"),
                Asset(CreativeAssetKind.Image, "same")
            };

            var creative = AdCreativeBuilder.Build(AdFormats.Interstitial, assets, out _);

            Assert.AreEqual("/cache/same-Image", creative.ImagePath);
        }

        [Test]
        public void Banner_NeverPlaysVideo()
        {
            var assets = new[]
            {
                Asset(CreativeAssetKind.Video, "v", title: "Video"),
                Asset(CreativeAssetKind.Image, "i", title: "Image")
            };

            var creative = AdCreativeBuilder.Build(AdFormats.Banner, assets, out _);

            Assert.IsNull(creative.VideoPath);
            Assert.AreEqual("/cache/i-Image", creative.ImagePath);
            Assert.AreEqual("Image", creative.Title);
        }

        [Test]
        public void Banner_WithOnlyALogoAndATitle_IsATextBanner()
        {
            var assets = new[] { Asset(CreativeAssetKind.Logo, "a", title: "Word Master", cta: "Install") };

            var creative = AdCreativeBuilder.Build(AdFormats.Banner, assets, out var error);

            Assert.AreEqual(AdCreativeError.None, error);
            Assert.IsNull(creative.ImagePath);
            Assert.AreEqual("/cache/a-Logo", creative.LogoPath);
            Assert.AreEqual("Word Master", creative.Title);
        }

        [Test]
        public void Banner_WithNothingToShow_IsNoMedia()
        {
            var assets = new[] { Asset(CreativeAssetKind.Video, "a", title: "Video only") };

            Assert.IsNull(AdCreativeBuilder.Build(AdFormats.Banner, assets, out var error));
            Assert.AreEqual(AdCreativeError.NoMedia, error);
        }

        [Test]
        public void Fullscreen_WithOnlyALogo_IsNoMedia()
        {
            var assets = new[] { Asset(CreativeAssetKind.Logo, "a", title: "Title") };

            Assert.IsNull(AdCreativeBuilder.Build(AdFormats.Interstitial, assets, out var error));
            Assert.AreEqual(AdCreativeError.NoMedia, error);
        }

        [Test]
        public void Logo_PrefersTheMainAdsOwn()
        {
            var assets = new[]
            {
                Asset(CreativeAssetKind.Logo, "other"),
                Asset(CreativeAssetKind.Image, "main"),
                Asset(CreativeAssetKind.Logo, "main")
            };

            var creative = AdCreativeBuilder.Build(AdFormats.Interstitial, assets, out _);

            Assert.AreEqual("/cache/main-Logo", creative.LogoPath);
        }

        [Test]
        public void BlankTexts_AreDropped()
        {
            var assets = new[] { Asset(CreativeAssetKind.Image, "a", title: "  ", description: "", cta: null) };

            var creative = AdCreativeBuilder.Build(AdFormats.Interstitial, assets, out _);

            Assert.IsNull(creative.Title);
            Assert.IsNull(creative.Description);
            Assert.IsNull(creative.CallToAction);
        }

        [Test]
        public void ClickUrl_FallsBackToAnyAssetOfTheGroup()
        {
            var assets = new[]
            {
                Asset(CreativeAssetKind.Image, "a"),
                Asset(CreativeAssetKind.Logo, "b", clickUrl: "https://group")
            };

            var creative = AdCreativeBuilder.Build(AdFormats.Interstitial, assets, out _);

            Assert.AreEqual("https://group", creative.ClickUrl);
        }

        [Test]
        public void Json_UsesProtocolNames_AndOmitsMissingFields()
        {
            var creative = new AdCreative { AdId = "x", ImagePath = "/i.png", Title = "T", ImageAssetId = "secret" };

            var json = JObject.Parse(creative.ToJson());

            Assert.AreEqual("x", (string)json["adId"]);
            Assert.AreEqual("/i.png", (string)json["imagePath"]);
            Assert.AreEqual("T", (string)json["title"]);
            Assert.IsNull(json["videoPath"]);
            Assert.IsNull(json["ImageAssetId"]);
            Assert.AreEqual(3, json.Count);
        }

        [Test]
        public void Json_KeepsRightToLeftTextIntact()
        {
            var creative = new AdCreative { AdId = "x", Title = "استاد کلمات" };

            var json = JObject.Parse(creative.ToJson());

            Assert.AreEqual("استاد کلمات", (string)json["title"]);
        }
    }
}
