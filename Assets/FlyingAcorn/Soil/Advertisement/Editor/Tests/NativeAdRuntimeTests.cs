#if UNITY_INCLUDE_TESTS
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using FlyingAcorn.Soil.Advertisement.Data;
using FlyingAcorn.Soil.Advertisement.Models;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using static FlyingAcorn.Soil.Advertisement.Data.Constants;

namespace FlyingAcorn.Soil.Advertisement.Tests
{
    /// <summary>
    /// The native ad runtime in the Editor: click attribution through uGUI, registrations keyed by
    /// GameObject, texture lifetime and LoadAd waiting for caching. The SDK lives in the game's
    /// own assembly, so its private state is reached through reflection.
    /// </summary>
    public class NativeAdRuntimeTests
    {
        private const BindingFlags Private = BindingFlags.NonPublic | BindingFlags.Static;

        private readonly List<GameObject> _views = new();
        private string _directory;
        private int _clicks;
        private int _loaded;
        private readonly List<AdError> _errors = new();

        [SetUp]
        public void SetUp()
        {
            ResetAds();
            Advertisement.UseEditorTestAds = false; // these tests check Soil's own answers
            _clicks = 0;
            _loaded = 0;
            _errors.Clear();
            Events.OnNativeAdClicked += _ => _clicks++;
            Events.OnNativeAdLoaded += _ => _loaded++;
            Events.OnNativeAdError += data => _errors.Add(data.AdError);
        }

        [TearDown]
        public void TearDown()
        {
            Advertisement.DestroyNativeAd();
            foreach (var view in _views)
                if (view) UnityEngine.Object.DestroyImmediate(view);
            _views.Clear();
            ResetAds();
            if (_directory != null && Directory.Exists(_directory))
                Directory.Delete(_directory, true);
            _directory = null;
        }

        #region Clicks

        [Test]
        public void TapOnAControlInsideARegisteredContainer_IsAttributedOnce()
        {
            var card = View("Card");
            var button = View("Button", card);
            button.AddComponent<Button>();
            var label = View("Label", button);
            var toggle = View("Toggle", card);
            toggle.AddComponent<Toggle>();
            var badge = View("Badge", card);
            SetContent("ad-1");

            Advertisement.ShowNativeAd(NativeAdReferences.ForContainer(card));

            foreach (var tapped in new[] { label, button, toggle, badge, card })
            {
                var before = _clicks;
                Tap(tapped);
                Assert.AreEqual(before + 1, _clicks, tapped.name);
            }
        }

        [Test]
        public void AViewThatFillsTwoSlots_IsStillOneClick()
        {
            var card = View("Card");
            var button = View("Button", card);
            button.AddComponent<Button>();
            SetContent("ad-1");

            Advertisement.ShowNativeAd(new NativeAdReferences(
                callToActionGameObject: button, iconGameObject: card, containerGameObject: card,
                additionalGameObjects: new[] { button }));

            Tap(button);
            Assert.AreEqual(1, _clicks);
            Tap(card);
            Assert.AreEqual(2, _clicks);
            Assert.AreEqual(1, Handlers(button), "one handler per GameObject");
        }

        [Test]
        public void HidingOnePlace_KeepsAControlAnotherPlaceStillCovers()
        {
            var card = View("Card");
            var button = View("Button", card);
            button.AddComponent<Button>();
            SetContent("ad-1");

            Advertisement.ShowNativeAd(NativeAdReferences.ForContainer(card));
            Advertisement.ShowNativeAd(new NativeAdReferences(callToActionGameObject: button));
            Advertisement.HideNativeAd(new NativeAdReferences(callToActionGameObject: button));

            Tap(button);
            Assert.AreEqual(1, _clicks, "the container still covers its button");

            Advertisement.HideNativeAd(NativeAdReferences.ForContainer(card));
            Tap(button);
            Tap(card);
            Assert.AreEqual(1, _clicks);
        }

        [Test]
        public void InlineReferences_AreReleasedByAnEquivalentHide()
        {
            var card = View("Card");
            var button = View("Button", card);
            button.AddComponent<Button>();
            SetContent("ad-1");

            for (var i = 0; i < 5; i++)
                Advertisement.ShowNativeAd(NativeAdReferences.ForContainer(card));
            Assert.AreEqual(1, Count("_nativeAdTargets"));
            Assert.AreEqual(2, Count("_nativeAdBindings"));

            Advertisement.HideNativeAd(NativeAdReferences.ForContainer(card));
            Assert.AreEqual(0, Count("_nativeAdTargets"));
            Assert.AreEqual(0, Count("_nativeAdBindings"));

            Tap(button);
            Tap(card);
            Assert.AreEqual(0, _clicks);
        }

        [Test]
        public void DestroyedViews_AreForgottenOnTheNextShow()
        {
            var first = View("First");
            var second = View("Second");
            SetContent("ad-1");

            Advertisement.ShowNativeAd(NativeAdReferences.ForContainer(first));
            UnityEngine.Object.DestroyImmediate(first);
            Advertisement.ShowNativeAd(NativeAdReferences.ForContainer(second));

            Assert.AreEqual(1, Count("_nativeAdTargets"));
            Assert.AreEqual(1, Count("_nativeAdBindings"));
        }

        #endregion

        #region Textures

        [Test]
        public void ReloadingTheSameAd_KeepsItsContentAndTextures()
        {
            CacheNativeAd("ad-1", Png("icon-a", Color.red), Png("main-a", Color.blue));

            Advertisement.LoadAd(AdFormat.native);
            var content = Advertisement.GetNativeAdContent();
            Assert.IsNotNull(content);
            Assert.IsTrue(content.HasMainImage);

            Advertisement.LoadAd(AdFormat.native);
            Assert.AreEqual(2, _loaded);
            Assert.AreSame(content, Advertisement.GetNativeAdContent());
            Assert.IsTrue(content.Icon != null);
            Assert.IsTrue(content.MainImage != null);
        }

        [Test]
        public void ReplacedContent_FreesTheTexturesItNoLongerShares()
        {
            CacheNativeAd("ad-1", Png("icon-a", Color.red), Png("main-a", Color.blue));
            Advertisement.LoadAd(AdFormat.native);
            var first = Advertisement.GetNativeAdContent();

            // The icon was downloaded again: a new file.
            Entry(AssetType.native_icon).LocalPath = Png("icon-b", Color.green);
            Advertisement.LoadAd(AdFormat.native);
            var second = Advertisement.GetNativeAdContent();

            Assert.AreNotSame(first, second);
            Assert.IsTrue(first.Icon == null, "nothing showed the old icon, so it is destroyed");
            Assert.AreSame(first.MainImage, second.MainImage, "an unchanged file is not decoded again");
            Assert.IsTrue(second.MainImage != null);

            // Only the texts changed: the textures move to the new content.
            Entry(AssetType.native_icon).MainHeaderText = "New title";
            Advertisement.LoadAd(AdFormat.native);
            var third = Advertisement.GetNativeAdContent();
            Assert.AreEqual("New title", third.Title);
            Assert.AreSame(second.Icon, third.Icon);
            Assert.IsTrue(third.Icon != null);
            Assert.IsTrue(third.MainImage != null);
        }

        [Test]
        public void ReplacedContent_KeepsItsTexturesWhileARegisteredViewShowsIt()
        {
            var card = View("Card");
            CacheNativeAd("ad-1", Png("icon-a", Color.red), null);
            Advertisement.LoadAd(AdFormat.native);
            var shown = Advertisement.ShowNativeAd(NativeAdReferences.ForContainer(card));

            Entry(AssetType.native_icon).LocalPath = Png("icon-b", Color.green);
            Advertisement.LoadAd(AdFormat.native);
            var next = Advertisement.GetNativeAdContent();
            Assert.AreNotSame(shown, next);
            Assert.IsTrue(shown.Icon != null, "the card still shows it");

            Advertisement.HideNativeAd(NativeAdReferences.ForContainer(card));
            Assert.IsTrue(shown.Icon == null);
            Assert.IsTrue(next.Icon != null);
        }

        [Test]
        public void DestroyNativeAd_FreesItsTextures()
        {
            var card = View("Card");
            CacheNativeAd("ad-1", Png("icon-a", Color.red), Png("main-a", Color.blue));
            Advertisement.LoadAd(AdFormat.native);
            var content = Advertisement.ShowNativeAd(NativeAdReferences.ForContainer(card));

            Advertisement.DestroyNativeAd();

            Assert.IsNull(Advertisement.GetNativeAdContent());
            Assert.IsTrue(content.Icon == null);
            Assert.IsTrue(content.MainImage == null);
        }

        #endregion

        #region LoadAd while caching

        [Test]
        public void LoadAd_WithNothingCaching_AnswersAtOnce()
        {
            Advertisement.LoadAd(AdFormat.native);
            CollectionAssert.AreEqual(new[] { AdError.NoFill }, _errors);
        }

        [Test]
        public void LoadAd_WhileNativeFilesCache_WaitsForThem()
        {
            SetPrivate("_nativeCaching", true);

            Advertisement.LoadAd(AdFormat.native);
            Assert.AreEqual(0, _errors.Count + _loaded, "no answer before the files are cached");

            CacheNativeAd("ad-1", Png("icon-a", Color.red), null);
            InvokePrivate("OnFormatAssetsReady", AdFormat.native);
            Assert.AreEqual(1, _loaded);
            Assert.AreEqual(0, _errors.Count);
        }

        [Test]
        public void LoadAd_WhileNativeFilesCache_FailsWhenCachingFails()
        {
            SetPrivate("_nativeCaching", true);

            Advertisement.LoadAd(AdFormat.native);
            Advertisement.LoadAd(AdFormat.native);
            InvokePrivate("CachingFailed", AdFormat.native, "network");

            CollectionAssert.AreEqual(new[] { AdError.NetworkError }, _errors, "answered once");
            Assert.AreEqual(0, _loaded);

            Advertisement.LoadAd(AdFormat.native);
            Assert.AreEqual(2, _errors.Count, "caching is over: answered at once");
        }

        #endregion

        #region Helpers

        private GameObject View(string name, GameObject parent = null)
        {
            var view = new GameObject(name, typeof(RectTransform), typeof(Image));
            if (parent) view.transform.SetParent(parent.transform, false);
            _views.Add(view);
            return view;
        }

        /// <summary>What a uGUI input module does on a tap: the click goes to the nearest handler.</summary>
        private static void Tap(GameObject tapped)
        {
            var handler = ExecuteEvents.GetEventHandler<IPointerClickHandler>(tapped);
            Assert.IsNotNull(handler, $"nothing handles a tap on {tapped.name}");
            ExecuteEvents.Execute(handler, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
        }

        private static int Handlers(GameObject go) => go.GetComponents<SoilNativeAdClickHandler>().Length;

        private static void SetContent(string adId)
        {
            SetPrivate("_nativeAdContent",
                new NativeAdContent(adId, "Title", null, "Install", "https://example.com/" + adId, null, null));
        }

        private string Png(string name, Color color)
        {
            _directory ??= Path.Combine(Path.GetTempPath(), "SoilNativeAdTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_directory);
            var texture = new Texture2D(4, 4);
            var pixels = new Color[16];
            for (var i = 0; i < pixels.Length; i++) pixels[i] = color;
            texture.SetPixels(pixels);
            var path = Path.Combine(_directory, name + ".png");
            File.WriteAllBytes(path, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            return path;
        }

        private static void CacheNativeAd(string adId, string iconPath, string mainImagePath)
        {
            var cache = CachedAssets();
            cache["native_native_icon_icon"] = new AssetCacheEntry
            {
                Id = "icon", AssetType = AssetType.native_icon, AdFormat = AdFormat.native, LocalPath = iconPath,
                AdId = adId, MainHeaderText = "Title", ActionButtonText = "Install",
                ClickUrl = "https://example.com/" + adId
            };
            if (mainImagePath != null)
                cache["native_native_image_main"] = new AssetCacheEntry
                {
                    Id = "main", AssetType = AssetType.native_image, AdFormat = AdFormat.native,
                    LocalPath = mainImagePath, AdId = adId
                };
        }

        private static AssetCacheEntry Entry(AssetType type)
        {
            foreach (var entry in CachedAssets().Values)
                if (entry.AssetType == type) return entry;
            return null;
        }

        private static Dictionary<string, AssetCacheEntry> CachedAssets() =>
            (Dictionary<string, AssetCacheEntry>)typeof(AssetCache).GetField("_cachedAssets", Private).GetValue(null);

        private static int Count(string field) =>
            ((ICollection)typeof(Advertisement).GetField(field, Private).GetValue(null)).Count;

        private static void SetPrivate(string field, object value) =>
            typeof(Advertisement).GetField(field, Private).SetValue(null, value);

        private static void InvokePrivate(string method, params object[] args) =>
            typeof(Advertisement).GetMethod(method, Private).Invoke(null, args);

        private static void ResetAds() => InvokePrivate("ResetStatics");

        #endregion
    }
}
#endif
