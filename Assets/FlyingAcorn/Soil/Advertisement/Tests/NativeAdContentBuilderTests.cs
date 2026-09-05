using System.Collections.Generic;
using NUnit.Framework;
using FlyingAcorn.Soil.Advertisement.Logic;

namespace FlyingAcorn.Soil.Advertisement.Tests
{
    /// <summary>
    /// Rules for turning the cached native assets into one renderable ad: what is required,
    /// what is optional, and - most importantly - that the text and images shown together
    /// always come from the same creative.
    /// </summary>
    public class NativeAdContentBuilderTests
    {
        private const string AdA = "ad-a";
        private const string AdB = "ad-b";

        private static NativeAdAssetInfo Icon(string adId = AdA, string id = "icon-1",
            int width = 256, int height = 256,   // any square is a valid icon
            long sizeBytes = 100_000, string title = "Headline", string cta = "Install",
            string description = "Body copy", string clickUrl = "https://example.com/click")
        {
            return new NativeAdAssetInfo
            {
                Id = id,
                Kind = NativeAdAssetKind.Icon,
                Width = width,
                Height = height,
                SizeBytes = sizeBytes,
                ClickUrl = clickUrl,
                AdId = adId,
                TitleText = title,
                DescriptionText = description,
                CallToActionText = cta
            };
        }

        private static NativeAdAssetInfo MainImage(string adId = AdA, string id = "image-1",
            int width = NativeAdAssetSpec.ImageWidth, int height = NativeAdAssetSpec.ImageHeight,
            long sizeBytes = 500_000, string clickUrl = "https://example.com/click")
        {
            return new NativeAdAssetInfo
            {
                Id = id,
                Kind = NativeAdAssetKind.MainImage,
                Width = width,
                Height = height,
                SizeBytes = sizeBytes,
                ClickUrl = clickUrl,
                AdId = adId
            };
        }

        private static NativeAdContentModel Build(params NativeAdAssetInfo[] assets)
        {
            return NativeAdContentBuilder.Build(new List<NativeAdAssetInfo>(assets), out _);
        }

        private static NativeAdContentError ErrorFor(params NativeAdAssetInfo[] assets)
        {
            NativeAdContentBuilder.Build(new List<NativeAdAssetInfo>(assets), out var error);
            return error;
        }

        [Test]
        public void Build_FullAssetSet_ProducesCompleteContent()
        {
            var content = Build(Icon(), MainImage());

            Assert.IsNotNull(content);
            Assert.AreEqual(AdA, content.AdId);
            Assert.AreEqual("Headline", content.Title);
            Assert.AreEqual("Body copy", content.Description);
            Assert.AreEqual("Install", content.CallToAction);
            Assert.AreEqual("https://example.com/click", content.ClickUrl);
            Assert.AreEqual("icon-1", content.IconAssetId);
            Assert.AreEqual("image-1", content.MainImageAssetId);
            Assert.IsTrue(content.HasMainImage);
        }

        [Test]
        public void Build_IconOnly_IsStillARenderableAd()
        {
            var content = Build(Icon());

            Assert.IsNotNull(content);
            Assert.IsFalse(content.HasMainImage);
            Assert.IsNull(content.MainImageAssetId);
        }

        [Test]
        public void Build_MissingDescription_IsAllowed()
        {
            var content = Build(Icon(description: null));

            Assert.IsNotNull(content);
            Assert.IsNull(content.Description);
        }

        [Test]
        public void Build_NoAssets_ReportsNoAssets()
        {
            Assert.AreEqual(NativeAdContentError.NoAssets, ErrorFor());
            Assert.IsNull(NativeAdContentBuilder.Build(null, out var error));
            Assert.AreEqual(NativeAdContentError.NoAssets, error);
        }

        [Test]
        public void Build_ImageWithoutIcon_ReportsMissingIcon()
        {
            Assert.AreEqual(NativeAdContentError.MissingIcon, ErrorFor(MainImage()));
            Assert.IsNull(Build(MainImage()));
        }

        [Test]
        public void Build_OffSpecIcon_ReportsOffSpecIconRatherThanMissing()
        {
            var error = ErrorFor(Icon(width: 256, height: 128));

            Assert.AreEqual(NativeAdContentError.OffSpecIcon, error,
                "an icon that exists but is not square is a creative problem, not an empty cache");
        }

        [Test]
        public void Build_SquareIconOfAnySize_IsAccepted()
        {
            foreach (var side in new[] { 64, 512, 1024 })
                Assert.IsNotNull(Build(Icon(width: side, height: side)), $"{side}x{side}");
        }

        [Test]
        public void Build_OversizedIcon_IsRejected()
        {
            Assert.AreEqual(NativeAdContentError.OffSpecIcon,
                ErrorFor(Icon(sizeBytes: NativeAdAssetSpec.IconMaxSizeBytes + 1)));
        }

        [Test]
        public void Build_PrefersAValidIconOverAnOffSpecOne()
        {
            var content = Build(Icon(id: "bad-icon", width: 64, height: 32), Icon(id: "good-icon"));

            Assert.IsNotNull(content);
            Assert.AreEqual("good-icon", content.IconAssetId);
        }

        [Test]
        public void Build_MissingTitle_ReportsMissingTitle()
        {
            Assert.AreEqual(NativeAdContentError.MissingTitle, ErrorFor(Icon(title: null)));
            Assert.AreEqual(NativeAdContentError.MissingTitle, ErrorFor(Icon(title: string.Empty)));
        }

        [Test]
        public void Build_MissingCallToAction_ReportsMissingCallToAction()
        {
            Assert.AreEqual(NativeAdContentError.MissingCallToAction, ErrorFor(Icon(cta: null)));
        }

        [Test]
        public void Build_OffSpecMainImage_IsDroppedButTheAdSurvives()
        {
            var content = Build(Icon(), MainImage(width: 600, height: 314));

            Assert.IsNotNull(content, "an unusable main image must not kill an otherwise valid native ad");
            Assert.IsFalse(content.HasMainImage);
        }

        [Test]
        public void Build_OversizedMainImage_IsDropped()
        {
            var content = Build(Icon(), MainImage(sizeBytes: NativeAdAssetSpec.ImageMaxSizeBytes + 1));

            Assert.IsNotNull(content);
            Assert.IsFalse(content.HasMainImage);
        }

        [Test]
        public void Build_DoesNotPairAnImageFromADifferentCreative()
        {
            // The regression this guards: showing ad A's icon and headline next to ad B's image.
            var content = Build(Icon(AdA), MainImage(AdB, id: "other-ad-image"));

            Assert.IsNotNull(content);
            Assert.IsFalse(content.HasMainImage);
        }

        [Test]
        public void Build_PicksTheImageBelongingToTheChosenIconsCreative()
        {
            var content = Build(
                Icon(AdA),
                MainImage(AdB, id: "b-image"),
                MainImage(AdA, id: "a-image"));

            Assert.AreEqual("a-image", content.MainImageAssetId);
        }

        [Test]
        public void Build_TextAlwaysComesFromTheChosenIconsCreative()
        {
            var otherAdIcon = Icon(AdB, id: "b-icon", width: 64, height: 32, title: "Wrong headline");
            var content = Build(otherAdIcon, Icon(AdA, title: "Right headline"));

            Assert.AreEqual("Right headline", content.Title);
            Assert.AreEqual(AdA, content.AdId);
        }

        [Test]
        public void Build_LegacyCacheEntryWithoutAdId_StillPairsTheImage()
        {
            // Entries cached before ad ids were recorded must not lose their main image.
            var content = Build(Icon(adId: null), MainImage(adId: null, id: "legacy-image"));

            Assert.AreEqual("legacy-image", content.MainImageAssetId);
        }

        [Test]
        public void Build_FallsBackToTheImageClickUrlWhenTheIconHasNone()
        {
            var content = Build(Icon(clickUrl: null), MainImage(clickUrl: "https://example.com/from-image"));

            Assert.AreEqual("https://example.com/from-image", content.ClickUrl);
        }

        [Test]
        public void Build_WithNoClickUrlAnywhere_StillBuilds()
        {
            var content = Build(Icon(clickUrl: null));

            Assert.IsNotNull(content);
            Assert.IsNull(content.ClickUrl);
        }

        [Test]
        public void Build_IgnoresNullEntriesInTheCacheList()
        {
            var content = NativeAdContentBuilder.Build(
                new List<NativeAdAssetInfo> { null, Icon(), null, MainImage() }, out var error);

            Assert.AreEqual(NativeAdContentError.None, error);
            Assert.IsNotNull(content);
            Assert.IsTrue(content.HasMainImage);
        }

        [Test]
        public void Build_OnSuccess_ReportsNoError()
        {
            Assert.AreEqual(NativeAdContentError.None, ErrorFor(Icon(), MainImage()));
        }
    }
}
