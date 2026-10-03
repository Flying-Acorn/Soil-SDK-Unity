using System.Collections.Generic;
using FlyingAcorn.Soil.Advertisement.Logic;
using NUnit.Framework;

namespace FlyingAcorn.Soil.Advertisement.Tests
{
    /// <summary>Web and app store links are opened for an ad click; other app links only where allowed.</summary>
    public class AdLinkPolicyTests
    {
        [SetUp]
        public void WebAndStoreOnly()
        {
            AdLinkPolicy.AppLinksAllowed = false;
        }

        [TearDown]
        public void Restore()
        {
            AdLinkPolicy.ResetStatics();
        }

        [TestCase("https://example.com/click?x=1")]
        [TestCase("http://example.com")]
        [TestCase("HTTPS://EXAMPLE.COM")]
        [TestCase("market://details?id=com.example")]
        [TestCase("itms-apps://apps.apple.com/app/id1")]
        [TestCase("itms-appss://apps.apple.com/app/id1")]
        public void WebAndStoreLinks_AreAllowed(string url)
        {
            Assert.IsTrue(AdLinkPolicy.IsAllowed(url));
            Assert.AreEqual(url, AdLinkPolicy.Sanitize(url));
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("   ")]
        [TestCase("javascript:alert(1)")]
        [TestCase("file:///data/data/app/secret")]
        [TestCase("intent://scan/#Intent;scheme=zxing;end")]
        [TestCase("tel:+100")]
        [TestCase("sms:+100")]
        [TestCase("content://com.example/x")]
        [TestCase("example.com/no-scheme")]
        [TestCase("//example.com")]
        [TestCase("https:")]
        [TestCase("https://exa\nmple.com")]
        [TestCase("httpsx://example.com")]
        public void AnythingElse_IsNoLink(string url)
        {
            Assert.IsFalse(AdLinkPolicy.IsAllowed(url));
            Assert.IsNull(AdLinkPolicy.Sanitize(url));
        }

        [TestCase("storeapp://details?id=com.example")]
        [TestCase("some-app+x.y://open/page")]
        [TestCase("httpsx://example.com")]
        public void AppLinks_OnlyWhereAllowed(string url)
        {
            Assert.IsNull(AdLinkPolicy.Sanitize(url));

            AdLinkPolicy.AppLinksAllowed = true;
            Assert.AreEqual(url, AdLinkPolicy.Sanitize(url));
        }

        [TestCase("javascript:alert(1)")]
        [TestCase("JavaScript:alert(1)")]
        [TestCase("file:///data/data/app/secret")]
        [TestCase("intent://scan/#Intent;scheme=zxing;end")]
        [TestCase("android-app://com.example/https/x")]
        [TestCase("content://com.example/x")]
        [TestCase("data:text/html,x")]
        [TestCase("tel:+100")]
        [TestCase("sms:+100")]
        [TestCase("mailto:a@example.com")]
        [TestCase("1app://x")]
        [TestCase("my app://x")]
        [TestCase("app://exa\nmple")]
        public void DeviceActions_AreRefused_EvenWhereAppLinksAreAllowed(string url)
        {
            AdLinkPolicy.AppLinksAllowed = true;
            Assert.IsNull(AdLinkPolicy.Sanitize(url));
        }

        [Test]
        public void Sanitize_TrimsSurroundingSpace()
        {
            Assert.AreEqual("https://example.com", AdLinkPolicy.Sanitize("  https://example.com \t"));
        }

        [Test]
        public void Creative_WithARefusedLink_HasNoClickUrl_OrFallsBackToAnAllowedOne()
        {
            var video = new CreativeAssetInfo
            {
                Id = "v", Kind = CreativeAssetKind.Video, LocalPath = "/v.mp4", AdId = "a",
                ClickUrl = "javascript:alert(1)"
            };
            var creative = AdCreativeBuilder.Build(AdFormats.Interstitial, new List<CreativeAssetInfo> { video }, out _);
            Assert.IsNotNull(creative);
            Assert.IsNull(creative.ClickUrl);

            var image = new CreativeAssetInfo
            {
                Id = "i", Kind = CreativeAssetKind.Image, LocalPath = "/i.png", AdId = "b",
                ClickUrl = "https://allowed"
            };
            creative = AdCreativeBuilder.Build(AdFormats.Interstitial, new List<CreativeAssetInfo> { video, image }, out _);
            Assert.AreEqual("https://allowed", creative.ClickUrl);
        }

        [Test]
        public void NativeAd_WithARefusedLink_HasNoClickUrl()
        {
            var icon = new NativeAdAssetInfo
            {
                Id = "icon", Kind = NativeAdAssetKind.Icon, Width = 256, Height = 256, SizeBytes = 1000,
                AdId = "a", TitleText = "t", CallToActionText = "c", ClickUrl = "file:///etc/passwd"
            };
            var content = NativeAdContentBuilder.Build(new List<NativeAdAssetInfo> { icon }, out var error);

            Assert.IsNotNull(content, error.ToString());
            Assert.IsNull(content.ClickUrl);
        }
    }
}
