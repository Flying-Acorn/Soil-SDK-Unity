using System.Collections.Generic;
using FlyingAcorn.Analytics;
using FlyingAcorn.Soil.Advertisement.Data;
using FlyingAcorn.Soil.Advertisement.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static FlyingAcorn.Soil.Advertisement.Data.Constants;

namespace FlyingAcorn.Soil.Advertisement.Demo
{
    /// <summary>
    /// A complete, minimal native ad integration.
    ///
    /// Native ads are the one format the SDK does not draw: it hands over the assets and this
    /// script renders them into ordinary uGUI objects, then registers those objects so a tap
    /// anywhere on the ad opens the advertiser's link.
    ///
    /// Scene setup: put a panel under a Canvas (with an EventSystem in the scene), add a
    /// RawImage for the icon, a RawImage for the main image, and three TMP_Text objects for the
    /// headline, body and call to action. Drop this component on the panel and wire the fields.
    /// Everything except the icon, headline and call to action is optional.
    /// </summary>
    public class NativeAdExample : MonoBehaviour
    {
        [Header("Ad views (leave any of these empty to not show that asset)")]
        [SerializeField] private GameObject adRoot;
        [SerializeField] private RawImage iconImage;
        [SerializeField] private RawImage mainImage;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text descriptionText;
        [SerializeField] private TMP_Text callToActionText;

        [Header("Demo controls")]
        [SerializeField] private Button loadButton;
        [SerializeField] private Button hideButton;
        [SerializeField] private TextMeshProUGUI statusText;

        private void Awake()
        {
            if (adRoot) adRoot.SetActive(false);

            if (loadButton) loadButton.onClick.AddListener(LoadNativeAd);
            if (hideButton) hideButton.onClick.AddListener(HideNativeAd);

            Events.OnInitialized += OnInitialized;
            Events.OnInitializeFailed += OnInitializeFailed;

            Events.OnNativeAdLoaded += OnNativeAdLoaded;
            Events.OnNativeAdError += OnNativeAdError;
            Events.OnNativeAdShown += OnNativeAdShown;
            Events.OnNativeAdClicked += OnNativeAdClicked;
            Events.OnNativeAdClosed += OnNativeAdClosed;

            // Ask for the native format at initialization. Assets are selected and cached here,
            // so a format you never show is wasted bandwidth - only request what you will use.
            Advertisement.InitializeAsync(new List<AdFormat> { AdFormat.native });
        }

        private void OnDestroy()
        {
            if (loadButton) loadButton.onClick.RemoveListener(LoadNativeAd);
            if (hideButton) hideButton.onClick.RemoveListener(HideNativeAd);

            Events.OnInitialized -= OnInitialized;
            Events.OnInitializeFailed -= OnInitializeFailed;

            Events.OnNativeAdLoaded -= OnNativeAdLoaded;
            Events.OnNativeAdError -= OnNativeAdError;
            Events.OnNativeAdShown -= OnNativeAdShown;
            Events.OnNativeAdClicked -= OnNativeAdClicked;
            Events.OnNativeAdClosed -= OnNativeAdClosed;

            // Release the ad before the views it was registered against are destroyed.
            Advertisement.DestroyNativeAd();
        }

        private void OnInitialized()
        {
            Log("Initialized. Loading native ad...");
            LoadNativeAd();
        }

        private void OnInitializeFailed(string error)
        {
            Log($"Initialization failed: {error}");
        }

        private void LoadNativeAd()
        {
            if (Advertisement.IsFormatReady(AdFormat.native))
            {
                ShowNativeAd();
                return;
            }

            Advertisement.LoadAd(AdFormat.native);
        }

        private void OnNativeAdLoaded(AdEventData data)
        {
            Log("Native ad loaded.");
            ShowNativeAd();
        }

        /// <summary>
        /// The whole integration in one method: register the views, then paint the content.
        /// </summary>
        private void ShowNativeAd()
        {
            // Register every view you render the ad into - each one becomes clickable. If your
            // layout has a single root, NativeAdReferences.ForContainer(adRoot) is equivalent and
            // shorter, because clicks bubble up from children to the container's handler.
            var references = new NativeAdReferences(
                titleGameObject: titleText ? titleText.gameObject : null,
                descriptionGameObject: descriptionText ? descriptionText.gameObject : null,
                callToActionGameObject: callToActionText ? callToActionText.gameObject : null,
                iconGameObject: iconImage ? iconImage.gameObject : null,
                mainImageGameObject: mainImage ? mainImage.gameObject : null);

            // ShowNativeAd returns the content and also raises OnNativeAdContentReady, so the
            // rendering code can live elsewhere if you prefer.
            var content = Advertisement.ShowNativeAd(references);
            if (content == null)
            {
                Log("No native ad ready to show.");
                return;
            }

            Render(content);

            if (adRoot) adRoot.SetActive(true);
        }

        /// <summary>
        /// Title, call to action and icon are always present. Description and main image are
        /// optional - hide those views rather than leaving an empty gap in the layout.
        /// </summary>
        private void Render(NativeAdContent content)
        {
            if (titleText)
                titleText.text = content.Title;

            if (callToActionText)
                callToActionText.text = content.CallToAction;

            if (iconImage)
                iconImage.texture = content.Icon;

            if (descriptionText)
            {
                var hasDescription = !string.IsNullOrEmpty(content.Description);
                descriptionText.gameObject.SetActive(hasDescription);
                if (hasDescription)
                    descriptionText.text = content.Description;
            }

            if (mainImage)
            {
                mainImage.gameObject.SetActive(content.HasMainImage);
                if (content.HasMainImage)
                    mainImage.texture = content.MainImage;
            }
        }

        private void HideNativeAd()
        {
            if (adRoot) adRoot.SetActive(false);

            // Stops click attribution and raises OnNativeAdClosed. The ad stays loaded, so it can
            // be shown again without another request; use DestroyNativeAd to release it.
            Advertisement.HideNativeAd();
        }

        private void OnNativeAdError(AdEventData data)
        {
            // NoFill means there is no native inventory right now - retrying later is worthwhile.
            // InvalidRequest means the creative itself is unusable (for example an icon that is
            // not square), so retrying will not help until it is fixed in the dashboard.
            Log($"Native ad error: {data.AdError}");
        }

        private void OnNativeAdShown(AdEventData data) => Log("Native ad shown.");

        private void OnNativeAdClicked(AdEventData data) => Log("Native ad clicked.");

        private void OnNativeAdClosed(AdEventData data) => Log("Native ad closed.");

        private void Log(string message)
        {
            MyDebug.Info($"[NativeAdDemo] {message}");
            if (statusText)
                statusText.text += $"\n{message}";
        }
    }
}
