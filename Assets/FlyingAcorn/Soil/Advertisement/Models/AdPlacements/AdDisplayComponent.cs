using System;
using UnityEngine;
using UnityEngine.UI;
using RTLTMPro;
using FlyingAcorn.Soil.Advertisement.Data;
using static FlyingAcorn.Soil.Advertisement.Data.Constants;

namespace FlyingAcorn.Soil.Advertisement.Models.AdPlacements
{
    /// <summary>
    /// Enumeration for different types of text elements in ads
    /// Used for applying appropriate RTL configuration and styling
    /// </summary>
    public enum TextType
    {
        Button,
        Header, 
        Description
    }
    
    public class AdDisplayComponent : MonoBehaviour
    {
        [Header("UI Components")]
        public Canvas adCanvas;
        public Image backgroundImage;
        public Image mainAssetImage;
        public Image logoImage;
        public RTLTextMeshPro adTitleText;
        public RTLTextMeshPro adDescriptionText;
        public Button actionButton;
        public RTLTextMeshPro actionButtonText;
        public Button closeButton;
        
        [Header("Ad Configuration")]
        public AdFormat adFormat;
        public bool showCloseButton = true;
        
        [Header("Font Configuration")]
        [Tooltip("Custom font to use for all ad text elements. Leave empty to use default fonts.")]
        public TMPro.TMP_FontAsset customAdFont;
        
        private AssetCacheEntry _currentMainAsset;
        private AssetCacheEntry _currentLogoAsset;
        private Ad _currentAd;
        private Action _onCloseCallback;
        private Action _onClickCallback;
        private Action _onRewardedCallback;
        private Action _onShownCallback;
        private Action _onImpressionCallback;
        
        // Countdown functionality
        private float _countdownTime;
        private bool _isCountingDown = false;
        private RTLTextMeshPro _closeButtonText;
        
        private void Awake()
        {
            // Setup button listeners
            SetupButtonListeners();
        }
        
        public void SetCanvas(Canvas canvas)
        {
            adCanvas = canvas;
        }
        
        private void SetupButtonListeners()
        {
            if (actionButton != null)
            {
                actionButton.onClick.AddListener(OnActionButtonClicked);
            }

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(OnCloseButtonClicked);
                closeButton.gameObject.SetActive(showCloseButton);
                
                // Get or find the close button text component
                _closeButtonText = closeButton.GetComponentInChildren<RTLTextMeshPro>();
                if (_closeButtonText == null)
                {
                    UnityEngine.Debug.LogWarning("[AdDisplayComponent] No RTLTextMeshPro found in close button for countdown display");
                }
            }
        }
        
        public void ShowAd(Ad ad, Action onClose = null, Action onClick = null, Action onRewarded = null, Action onShown = null)
        {
            _currentAd = ad;
            _onCloseCallback = onClose;
            _onClickCallback = onClick;
            _onRewardedCallback = onRewarded;
            _onShownCallback = onShown;
            
            if (adCanvas == null)
            {
                UnityEngine.Debug.LogError($"AdCanvas is null for {adFormat}! Cannot show ad.");
                return;
            }
            
            // Load and display assets
            LoadAndDisplayAssets();
            
            // Start close button countdown based on ad format
            StartCloseButtonCountdown();
            
            // Show the ad
            ShowCanvasDirectly();
        }
        
        private void ShowCanvasDirectly()
        {
            try
            {
                // Ensure the shared canvas is active
                if (adCanvas != null)
                {
                    if (!adCanvas.enabled)
                    {
                        adCanvas.enabled = true;
                        Analytics.MyDebug.Info($"Activated shared canvas");
                    }
                    
                    // Show this ad type's background
                    if (backgroundImage != null && backgroundImage.gameObject != null)
                    {
                        backgroundImage.gameObject.SetActive(true);
                        
                        // Fire events and reassign listeners
                        FireAdShownEvents();
                        ReassignButtonListeners();
                    }
                    else
                    {
                        UnityEngine.Debug.LogError($"Background image not found for {adFormat} ad");
                    }
                    
                    Canvas.ForceUpdateCanvases();
                }
                else
                {
                    UnityEngine.Debug.LogError($"Shared canvas not found for {adFormat} ad");
                }
            }
            catch (System.Exception ex)
            {
                UnityEngine.Debug.LogError($"Exception during {adFormat} ad display: {ex.Message}");
            }
        }
        
        private void FireAdShownEvents()
        {
            _onShownCallback?.Invoke();
            _onImpressionCallback?.Invoke();
        }

        public void HideAd()
        {
            // Stop countdown when hiding ad
            if (_isCountingDown)
            {
                _isCountingDown = false;
                CancelInvoke(nameof(UpdateCountdown));
            }
            
            if (backgroundImage != null && backgroundImage.gameObject != null)
                backgroundImage.gameObject.SetActive(false);
        }
        
        private void ClearCallbacks()
        {
            // Stop any ongoing countdown
            if (_isCountingDown)
            {
                _isCountingDown = false;
                CancelInvoke(nameof(UpdateCountdown));
            }
            
            // Clear callback references (called separately to avoid issues with close button)
            _onCloseCallback = null;
            _onClickCallback = null;
            _onRewardedCallback = null;
            _onShownCallback = null;
            _onImpressionCallback = null;
        }

        private void LoadAndDisplayAssets()
        {
            if (_currentAd == null) return;
                
            Analytics.MyDebug.Info($"[AdDisplayComponent] Loading assets for {adFormat} ad");
                
            // Setup text elements based on data availability
            SetupTextElements();
            
            // Setup button text
            SetupActionButtonText();
            
            // Apply custom font to all text elements
            ApplyCustomFontToAllText();
            
            // Load assets
            LoadMainAsset();
            LoadLogoAsset();
        }
        
        private void SetupTextElements()
        {
            // Show/hide title text based on main_header availability
            if (adTitleText != null)
            {
                if (_currentAd.main_header != null && !string.IsNullOrEmpty(_currentAd.main_header.text_content))
                {
                    SetTextWithConnection(adTitleText, _currentAd.main_header.text_content, TextType.Header);
                    adTitleText.gameObject.SetActive(true);
                }
                else
                {
                    adTitleText.gameObject.SetActive(false);
                }
            }
            
            // Show/hide description text based on description availability
            if (adDescriptionText != null)
            {
                if (_currentAd.description != null && !string.IsNullOrEmpty(_currentAd.description.text_content))
                {
                    SetTextWithConnection(adDescriptionText, _currentAd.description.text_content, TextType.Description);
                    adDescriptionText.gameObject.SetActive(true);
                }
                else
                {
                    adDescriptionText.gameObject.SetActive(false);
                }
            }
        }
        
        private void SetupActionButtonText()
        {
            // Handle action button text with show/hide based on availability
            if (actionButtonText != null)
            {
                if (_currentAd.action_button != null && !string.IsNullOrEmpty(_currentAd.action_button.text_content))
                {
                    SetTextWithConnection(actionButtonText, _currentAd.action_button.text_content, TextType.Button);
                    actionButtonText.gameObject.SetActive(true);
                    actionButtonText.fontStyle = TMPro.FontStyles.Bold;
                    actionButtonText.color = Color.white;
                }
                else if (_currentAd.action_button != null && !string.IsNullOrEmpty(_currentAd.action_button.alt_text))
                {
                    SetTextWithConnection(actionButtonText, _currentAd.action_button.alt_text, TextType.Button);
                    actionButtonText.gameObject.SetActive(true);
                    actionButtonText.fontStyle = TMPro.FontStyles.Bold;
                    actionButtonText.color = Color.white;
                }
                else
                {
                    // Hide the text component but keep the button functional
                    actionButtonText.gameObject.SetActive(false);
                }
            }
            else if (actionButton != null)
            {
                // Try to find text component dynamically
                actionButtonText = actionButton.GetComponentInChildren<RTLTextMeshPro>();
                if (actionButtonText != null)
                {
                    if (_currentAd.action_button != null && !string.IsNullOrEmpty(_currentAd.action_button.text_content))
                    {
                        SetTextWithConnection(actionButtonText, _currentAd.action_button.text_content, TextType.Button);
                        actionButtonText.gameObject.SetActive(true);
                        actionButtonText.fontStyle = TMPro.FontStyles.Bold;
                        actionButtonText.color = Color.white;
                        
                        Analytics.MyDebug.Info($"[AdDisplayComponent] Found and configured ActionButtonText dynamically");
                    }
                    else if (_currentAd.action_button != null && !string.IsNullOrEmpty(_currentAd.action_button.alt_text))
                    {
                        SetTextWithConnection(actionButtonText, _currentAd.action_button.alt_text, TextType.Button);
                        actionButtonText.gameObject.SetActive(true);
                        actionButtonText.fontStyle = TMPro.FontStyles.Bold;
                        actionButtonText.color = Color.white;
                        
                        Analytics.MyDebug.Info($"[AdDisplayComponent] Found and configured ActionButtonText dynamically");
                    }
                    else
                    {
                        // Hide the text component but keep the button functional
                        actionButtonText.gameObject.SetActive(false);
                    }
                }
            }
            
            // Ensure the action button itself stays active for click functionality
            if (actionButton != null)
            {
                actionButton.gameObject.SetActive(true);
            }
        }
        
        private void LoadMainAsset()
        {
            _currentMainAsset = Advertisement.GetCachedAsset(adFormat, AssetType.image);
            
            if (_currentMainAsset != null && mainAssetImage != null)
            {
                var texture = Advertisement.LoadTexture(_currentMainAsset.Id);
                if (texture != null)
                {
                    var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                    mainAssetImage.sprite = sprite;
                    mainAssetImage.gameObject.SetActive(true);
                }
                else
                {
                    UnityEngine.Debug.LogError($"Failed to load texture for main asset: {_currentMainAsset.Id}");
                    mainAssetImage?.gameObject.SetActive(false);
                }
            }
            else
            {
                mainAssetImage?.gameObject.SetActive(false);
            }
        }
        
        private void LoadLogoAsset()
        {
            _currentLogoAsset = Advertisement.GetCachedAsset(adFormat, AssetType.logo);
            
            if (_currentLogoAsset != null && logoImage != null)
            {
                var texture = Advertisement.LoadTexture(_currentLogoAsset.Id);
                if (texture != null)
                {
                    var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                    logoImage.sprite = sprite;
                    logoImage.gameObject.SetActive(true);
                }
                else
                {
                    UnityEngine.Debug.LogError($"Failed to load texture for logo asset: {_currentLogoAsset.Id}");
                    logoImage?.gameObject.SetActive(false);
                }
            }
            else
            {
                logoImage?.gameObject.SetActive(false);
            }
        }
        
        private void OnActionButtonClicked()
        {
            // Fire the click callback
            _onClickCallback?.Invoke();
            
            // Fire the appropriate click event based on ad format
            var eventData = new AdEventData(adFormat);
            eventData.ad = _currentAd;
            
            // Try to open URL from ad data
            string urlToOpen = _currentAd?.action_button?.url ?? 
                              _currentAd?.main_image?.url ?? 
                              _currentAd?.logo?.url;
            
            if (!string.IsNullOrEmpty(urlToOpen))
            {
                Analytics.MyDebug.Info($"[AdDisplayComponent] Opening URL: {urlToOpen}");
                Application.OpenURL(urlToOpen);
            }
        }
        
        private void OnCloseButtonClicked()
        {
            var closeCallback = _onCloseCallback;
            
            // Hide the ad
            HideAd();
            
            // Fire the close callback
            closeCallback?.Invoke();
        }
        
        private void ReassignButtonListeners()
        {
            if (actionButton != null)
            {
                actionButton.onClick.RemoveAllListeners();
                actionButton.onClick.AddListener(OnActionButtonClicked);
            }
            
            if (closeButton != null)
            {
                closeButton.onClick.RemoveAllListeners();
                closeButton.onClick.AddListener(OnCloseButtonClicked);
            }
        }
        
        private void StartCloseButtonCountdown()
        {
            if (closeButton == null || !showCloseButton)
                return;
                
            // Set countdown duration based on ad format
            _countdownTime = adFormat switch
            {
                AdFormat.banner => 0f, // Immediate
                AdFormat.interstitial => 5f, // 5 seconds
                AdFormat.rewarded => 10f, // 10 seconds
                _ => 0f
            };
            
            if (_countdownTime <= 0f)
            {
                // Show close button immediately for banner ads
                EnableCloseButton();
            }
            else
            {
                // Start countdown for interstitial and rewarded ads
                _isCountingDown = true;
                closeButton.interactable = false;
                closeButton.gameObject.SetActive(true);
                UpdateCountdownDisplay();
                InvokeRepeating(nameof(UpdateCountdown), 1f, 1f);
            }
        }
        
        private void UpdateCountdown()
        {
            if (!_isCountingDown)
                return;
                
            _countdownTime -= 1f;
            
            if (_countdownTime <= 0f)
            {
                // Countdown finished - enable close button
                _isCountingDown = false;
                CancelInvoke(nameof(UpdateCountdown));
                EnableCloseButton();
            }
            else
            {
                // Update countdown display
                UpdateCountdownDisplay();
            }
        }
        
        private void UpdateCountdownDisplay()
        {
            if (_closeButtonText != null && _isCountingDown)
            {
                _closeButtonText.text = Mathf.Ceil(_countdownTime).ToString();
            }
        }

        private void EnableCloseButton()
        {
            if (closeButton != null)
            {
                closeButton.interactable = true;
                closeButton.gameObject.SetActive(true);

                if (_closeButtonText != null)
                {
                    _closeButtonText.text = "X";
                }
            }
            if (adFormat == AdFormat.rewarded)
            {
                _onRewardedCallback?.Invoke();
            }
        }
        
        /// <summary>
        /// Text type enumeration for RTL configuration
        /// </summary>
        private enum TextType
        {
            Button,
            Header,
            Description
        }
        
        private bool IsRTLText(string text)
        {
            if (string.IsNullOrEmpty(text))
                return false;
                
            foreach (char c in text)
            {
                // Check for Arabic and Persian Unicode ranges
                if ((c >= 0x0600 && c <= 0x06FF) ||  // Arabic
                    (c >= 0x0750 && c <= 0x077F) ||  // Arabic Supplement
                    (c >= 0x08A0 && c <= 0x08FF) ||  // Arabic Extended-A
                    (c >= 0xFB50 && c <= 0xFDFF) ||  // Arabic Presentation Forms-A
                    (c >= 0xFE70 && c <= 0xFEFF) ||  // Arabic Presentation Forms-B
                    (c >= 0x0590 && c <= 0x05FF))    // Hebrew
                {
                    return true;
                }
            }
            return false;
        }
        
        /// <summary>
        /// Applies the custom font to all TextMeshPro components in the ad
        /// Call this method after setting up text content to ensure proper font application
        /// </summary>
        private void ApplyCustomFontToAllText()
        {
            if (customAdFont == null)
            {
                UnityEngine.Debug.Log("[AdDisplayComponent] No custom font assigned, using default fonts");
                return;
            }
            
            UnityEngine.Debug.Log($"[AdDisplayComponent] Applying custom font '{customAdFont.name}' to all ad text elements");
            
            // Apply to all text components
            ApplyFontToTextComponent(adTitleText, "Ad Title");
            ApplyFontToTextComponent(adDescriptionText, "Ad Description");
            ApplyFontToTextComponent(actionButtonText, "Action Button");
            ApplyFontToTextComponent(_closeButtonText, "Close Button");
        }
        
        /// <summary>
        /// Applies the custom font to a specific TextMeshPro component
        /// </summary>
        private void ApplyFontToTextComponent(RTLTextMeshPro textComponent, string componentName)
        {
            if (textComponent == null)
            {
                UnityEngine.Debug.Log($"[AdDisplayComponent] {componentName} text component is null, skipping font application");
                return;
            }
            
            if (customAdFont == null)
            {
                UnityEngine.Debug.Log($"[AdDisplayComponent] No custom font assigned for {componentName}");
                return;
            }
            
            // Store the original font for potential restoration
            var originalFont = textComponent.font;
            
            // Apply the custom font
            textComponent.font = customAdFont;
            
            UnityEngine.Debug.Log($"[AdDisplayComponent] Applied custom font '{customAdFont.name}' to {componentName} (was: '{originalFont?.name ?? "null"}')");
        }
        
        /// <summary>
        /// Sets up custom font configuration - call this in Awake or when font is assigned
        /// </summary>
        public void SetCustomFont(TMPro.TMP_FontAsset font)
        {
            customAdFont = font;
            UnityEngine.Debug.Log($"[AdDisplayComponent] Custom font set to: {font?.name ?? "null"}");
            
            // Apply immediately if we have text components ready
            ApplyCustomFontToAllText();
        }
        
        private void SetTextWithConnection(RTLTextMeshPro textComponent, string text, TextType textType = TextType.Button)
        {
            if (textComponent == null || string.IsNullOrEmpty(text))
                return;
            
            textComponent.text = text;
        }
    }
}
