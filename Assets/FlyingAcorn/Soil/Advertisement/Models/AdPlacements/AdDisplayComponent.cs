using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FlyingAcorn.Soil.Advertisement.Data;
using static FlyingAcorn.Soil.Advertisement.Data.Constants;

namespace FlyingAcorn.Soil.Advertisement.Models.AdPlacements
{
    public class AdDisplayComponent : MonoBehaviour
    {
        [Header("UI Components")]
        public Canvas adCanvas;
        public Image backgroundImage;
        public Image mainAssetImage;
        public Image logoImage;
        public TextMeshProUGUI adTitleText;
        public TextMeshProUGUI adDescriptionText;
        public Button actionButton;
        public TextMeshProUGUI actionButtonText;
        public Button closeButton;
        
        [Header("Ad Configuration")]
        public AdFormat adFormat;
        public bool showCloseButton = true;
        
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
        private TextMeshProUGUI _closeButtonText;
        
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
                _closeButtonText = closeButton.GetComponentInChildren<TextMeshProUGUI>();
                if (_closeButtonText == null)
                {
                    UnityEngine.Debug.LogWarning("[AdDisplayComponent] No TextMeshProUGUI found in close button for countdown display");
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
                    adTitleText.text = _currentAd.main_header.text_content;
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
                    adDescriptionText.text = _currentAd.description.text_content;
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
                    actionButtonText.text = _currentAd.action_button.text_content;
                    actionButtonText.gameObject.SetActive(true);
                    actionButtonText.fontStyle = TMPro.FontStyles.Bold;
                    actionButtonText.color = Color.white;
                }
                else if (_currentAd.action_button != null && !string.IsNullOrEmpty(_currentAd.action_button.alt_text))
                {
                    actionButtonText.text = _currentAd.action_button.alt_text;
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
                actionButtonText = actionButton.GetComponentInChildren<TextMeshProUGUI>();
                if (actionButtonText != null)
                {
                    if (_currentAd.action_button != null && !string.IsNullOrEmpty(_currentAd.action_button.text_content))
                    {
                        actionButtonText.text = _currentAd.action_button.text_content;
                        actionButtonText.gameObject.SetActive(true);
                        actionButtonText.fontStyle = TMPro.FontStyles.Bold;
                        actionButtonText.color = Color.white;
                        Analytics.MyDebug.Info($"[AdDisplayComponent] Found and configured ActionButtonText dynamically");
                    }
                    else if (_currentAd.action_button != null && !string.IsNullOrEmpty(_currentAd.action_button.alt_text))
                    {
                        actionButtonText.text = _currentAd.action_button.alt_text;
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
    }
}
