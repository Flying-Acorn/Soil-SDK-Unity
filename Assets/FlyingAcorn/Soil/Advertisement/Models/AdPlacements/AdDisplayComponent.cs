using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using RTLTMPro;
using FlyingAcorn.Soil.Advertisement.Data;
using static FlyingAcorn.Soil.Advertisement.Data.Constants;
using FlyingAcorn.Analytics;
using TMPro;
using System.Collections;

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
        public Image backgroundImage;
        [Tooltip("Used for static image ads only")]
        public Image mainAssetImage;
        [Tooltip("Used for video display only - shows video content from render texture")]
        public RawImage rawAssetImage;
        [Tooltip("Video player component for video ads only")]
        public VideoPlayer mainAssetVideoPlayer;
        [Tooltip("Optional: Pre-assigned render texture. If null, will be created dynamically with optimized size")]
        public RenderTexture videoRenderTexture;
        public Image logoImage;
        public Button actionButton;
        public RTLTextMeshPro adTitleText;
        public RTLTextMeshPro adDescriptionText;
        public RTLTextMeshPro actionButtonText;
        public Button closeButton;
        private Image _closeButtonImage;

        [Header("Video Configuration")]
        [Tooltip("Maximum resolution for dynamically created render textures (0 = use source resolution)")]
        public int maxRenderTextureSize = 1024;
        [Tooltip("Quality setting for render texture format")]
        public RenderTextureFormat renderTextureFormat = RenderTextureFormat.RGB565;

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

        // Video support
        private bool _isVideoAd = false;
        private bool _videoLoaded = false;
        private bool _videoStarted = false;
        private bool _shouldMuteAudio = false;
        private RenderTexture _dynamicRenderTexture;

        // Countdown functionality
        private float _countdownTime;
        private bool _isCountingDown = false;
        private RTLTextMeshPro _closeButtonText;

        private void Awake()
        {
            // Setup button listeners
            SetupButtonListeners();
            if (closeButton != null)
                _closeButtonImage = closeButton.GetComponent<Image>();

            // Setup video player if available
            SetupVideoPlayer();
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
                    MyDebug.LogWarning("[AdDisplayComponent] No RTLTextMeshPro found in close button for countdown display");
                }
            }
        }

        private void SetupVideoPlayer()
        {
            if (mainAssetVideoPlayer != null)
            {
                // Setup video player events
                mainAssetVideoPlayer.prepareCompleted += OnVideoPrepared;
                mainAssetVideoPlayer.started += OnVideoStarted;
                mainAssetVideoPlayer.loopPointReached += OnVideoFinished;
                mainAssetVideoPlayer.errorReceived += OnVideoError;

                // Configure video player defaults
                mainAssetVideoPlayer.playOnAwake = false;
                mainAssetVideoPlayer.waitForFirstFrame = true;
                mainAssetVideoPlayer.skipOnDrop = false; // Changed to false for better sync

                // Audio configuration for better sync and silent mode handling
                mainAssetVideoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
                mainAssetVideoPlayer.SetDirectAudioMute(0, AudioListener.pause || AudioListener.volume == 0);
                // Setup render texture (will be configured properly when video loads)
                ConfigureVideoRenderMode();

                // Setup RawImage component for video display
                SetupVideoRawImage();
            }
        }

        /// <summary>
        /// Sets up the RawImage component for video display
        /// </summary>
        private void SetupVideoRawImage()
        {
            if (rawAssetImage != null)
            {
                // Ensure RawImage is initially disabled until video is ready
                rawAssetImage.gameObject.SetActive(false);
                rawAssetImage.texture = null;
            }
        }

        /// <summary>
        /// Configures the video render mode and creates optimized render texture if needed
        /// </summary>
        private void ConfigureVideoRenderMode()
        {
            if (mainAssetVideoPlayer == null) return;

            // Check if we have a pre-assigned render texture (either via inspector or already set on video player)
            var preAssignedTexture = videoRenderTexture ?? mainAssetVideoPlayer.targetTexture;

            if (preAssignedTexture != null)
            {
                // Use pre-assigned render texture
                mainAssetVideoPlayer.renderMode = VideoRenderMode.RenderTexture;
                mainAssetVideoPlayer.targetTexture = preAssignedTexture;
                MyDebug.Verbose($"[AdDisplayComponent] Using pre-assigned render texture: {preAssignedTexture.width}x{preAssignedTexture.height}");
            }
            else if (rawAssetImage != null)
            {
                // Will create dynamic render texture when video is prepared (we need video dimensions)
                mainAssetVideoPlayer.renderMode = VideoRenderMode.RenderTexture;
                MyDebug.Verbose("[AdDisplayComponent] Will create dynamic render texture when video is prepared");
            }
            else
            {
                // Fallback to camera far plane or material override
                mainAssetVideoPlayer.renderMode = VideoRenderMode.CameraFarPlane;
                MyDebug.Verbose("[AdDisplayComponent] Using CameraFarPlane render mode as fallback");
            }
        }

        /// <summary>
        /// Creates an optimized render texture based on video dimensions and settings
        /// </summary>
        private RenderTexture CreateOptimizedRenderTexture(int videoWidth, int videoHeight)
        {
            // Calculate optimal dimensions
            int targetWidth = videoWidth;
            int targetHeight = videoHeight;

            // Apply maximum size constraint if specified
            if (maxRenderTextureSize > 0)
            {
                float aspectRatio = (float)videoWidth / videoHeight;

                if (videoWidth > maxRenderTextureSize || videoHeight > maxRenderTextureSize)
                {
                    if (videoWidth > videoHeight)
                    {
                        targetWidth = maxRenderTextureSize;
                        targetHeight = Mathf.RoundToInt(maxRenderTextureSize / aspectRatio);
                    }
                    else
                    {
                        targetHeight = maxRenderTextureSize;
                        targetWidth = Mathf.RoundToInt(maxRenderTextureSize * aspectRatio);
                    }
                }
            }

            // Ensure dimensions are power of 2 for better GPU performance
            targetWidth = Mathf.NextPowerOfTwo(targetWidth);
            targetHeight = Mathf.NextPowerOfTwo(targetHeight);

            MyDebug.Verbose($"[AdDisplayComponent] Creating render texture: {targetWidth}x{targetHeight} (original: {videoWidth}x{videoHeight})");

            // Create the render texture
            var renderTexture = new RenderTexture(targetWidth, targetHeight, 0, renderTextureFormat)
            {
                name = $"VideoAd_RenderTexture_{adFormat}",
                useMipMap = false,
                autoGenerateMips = false,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            renderTexture.Create();
            return renderTexture;
        }

        public void ShowAd(Ad ad, Action onClose = null, Action onClick = null, Action onRewarded = null, Action onShown = null)
        {
            _currentAd = ad;
            _onCloseCallback = onClose;
            _onClickCallback = onClick;
            _onRewardedCallback = onRewarded;
            _onShownCallback = onShown;

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
                if (backgroundImage != null && backgroundImage.gameObject != null)
                {
                    backgroundImage.gameObject.SetActive(true);

                    // Start video playback for video ads
                    if (_isVideoAd && _videoLoaded && !_videoStarted)
                    {
                        StartVideoPlayback();
                    }

                    // Fire events and reassign listeners
                    FireAdShownEvents();
                    ReassignButtonListeners();
                }
                else
                {
                    MyDebug.LogError($"Background image not found for {adFormat} ad");
                }

                Canvas.ForceUpdateCanvases();
            }
            catch (System.Exception ex)
            {
                MyDebug.LogError($"Exception during {adFormat} ad display: {ex.Message}");
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

            // Stop video if playing
            if (_isVideoAd && mainAssetVideoPlayer != null && mainAssetVideoPlayer.isPlaying)
            {
                mainAssetVideoPlayer.Stop();
            }

            // Clean up dynamic render texture
            CleanupDynamicRenderTexture();

            if (backgroundImage != null && backgroundImage.gameObject != null)
                backgroundImage.gameObject.SetActive(false);
        }

        /// <summary>
        /// Cleans up the dynamically created render texture to free memory
        /// </summary>
        private void CleanupDynamicRenderTexture()
        {
            if (_dynamicRenderTexture != null)
            {
                if (_dynamicRenderTexture.IsCreated())
                {
                    _dynamicRenderTexture.Release();
                }
                DestroyImmediate(_dynamicRenderTexture);

                // Clear the video player's target texture only if we were using the dynamic one
                if (mainAssetVideoPlayer != null && mainAssetVideoPlayer.targetTexture == _dynamicRenderTexture)
                {
                    mainAssetVideoPlayer.targetTexture = null;
                }

                _dynamicRenderTexture = null;
                MyDebug.Verbose("[AdDisplayComponent] Dynamic render texture cleaned up");
            }

            // Reset video-specific components to their original state
            if (rawAssetImage != null && rawAssetImage.gameObject != null)
            {
                rawAssetImage.texture = null;
                rawAssetImage.gameObject.SetActive(false);
                MyDebug.Verbose("[AdDisplayComponent] Video RawImage component reset to original state");
            }

            // Reset static image component if needed
            if (mainAssetImage != null && mainAssetImage.gameObject != null)
            {
                mainAssetImage.sprite = null;
                mainAssetImage.enabled = true;
                MyDebug.Verbose("[AdDisplayComponent] Static Image component reset to original state");
            }
        }

        private void ClearCallbacks()
        {
            // Stop any ongoing countdown
            if (_isCountingDown)
            {
                _isCountingDown = false;
                CancelInvoke(nameof(UpdateCountdown));
            }

            // Stop video if playing
            if (_isVideoAd && mainAssetVideoPlayer != null && mainAssetVideoPlayer.isPlaying)
            {
                mainAssetVideoPlayer.Stop();
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

            Analytics.MyDebug.Verbose($"[AdDisplayComponent] Loading assets for {adFormat} ad");

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
                    actionButtonText.transform.parent.gameObject.SetActive(true);
                    actionButtonText.fontStyle = TMPro.FontStyles.Bold;
                    actionButtonText.color = Color.white;
                }
                else if (_currentAd.action_button != null && !string.IsNullOrEmpty(_currentAd.action_button.alt_text))
                {
                    SetTextWithConnection(actionButtonText, _currentAd.action_button.alt_text, TextType.Button);
                    actionButtonText.transform.parent.gameObject.SetActive(true);
                    actionButtonText.fontStyle = TMPro.FontStyles.Bold;
                    actionButtonText.color = Color.white;
                }
                else
                {
                    // Hide the text component but keep the button functional
                    actionButtonText.transform.parent.gameObject.SetActive(false);
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
                        actionButtonText.transform.parent.gameObject.SetActive(true);
                        actionButtonText.fontStyle = TMPro.FontStyles.Bold;
                        actionButtonText.color = Color.white;

                        Analytics.MyDebug.Verbose($"[AdDisplayComponent] Found and configured ActionButtonText dynamically");
                    }
                    else if (_currentAd.action_button != null && !string.IsNullOrEmpty(_currentAd.action_button.alt_text))
                    {
                        SetTextWithConnection(actionButtonText, _currentAd.action_button.alt_text, TextType.Button);
                        actionButtonText.transform.parent.gameObject.SetActive(true);
                        actionButtonText.fontStyle = TMPro.FontStyles.Bold;
                        actionButtonText.color = Color.white;

                        Analytics.MyDebug.Verbose($"[AdDisplayComponent] Found and configured ActionButtonText dynamically");
                    }
                    else
                    {
                        // Hide the text component but keep the button functional
                        actionButtonText.transform.parent.gameObject.SetActive(false);
                    }
                }
            }

            // Ensure the action button itself stays active for click functionality
            if (actionButton != null)
            {
                actionButton.gameObject.SetActive(true);
            }
        }

        /// <summary>
        /// Loads the main asset for display with new caching strategy:
        /// - Videos: Stream directly from URLs when online (no caching)
        /// - Images: Use cached versions when available (offline fallback)
        /// - Priority: Video when online, cached image otherwise
        /// </summary>
        private void LoadMainAsset()
        {
            // Check network connectivity for video streaming
            bool isOnline = Application.internetReachability != NetworkReachability.NotReachable;

            // Videos are no longer cached - check for video URL when online
            bool hasVideoUrl = false;
            if (isOnline && _currentAd?.main_video?.url != null)
            {
                hasVideoUrl = !string.IsNullOrEmpty(_currentAd.main_video.url);
            }

            // Always check for cached image (fallback when offline or no video)
            _currentMainAsset = Advertisement.GetCachedAsset(adFormat, AssetType.image);
            bool hasCachedImage = _currentMainAsset != null;

            MyDebug.Verbose($"LoadMainAsset - Online: {isOnline}, HasVideoUrl: {hasVideoUrl}, HasCachedImage: {hasCachedImage}");

            // Priority: Video streaming when online and available, otherwise cached image
            if (hasVideoUrl && isOnline)
            {
                MyDebug.Verbose($"Loading video from URL: {_currentAd.main_video.url}");
                _isVideoAd = true;
                // Create a temporary asset entry for video streaming
                _currentMainAsset = new AssetCacheEntry
                {
                    Id = _currentAd.main_video.id,
                    LocalPath = _currentAd.main_video.url, // Use URL directly for streaming
                    OriginalUrl = _currentAd.main_video.url,
                    AssetType = AssetType.video,
                    AdFormat = adFormat
                };
                LoadVideoAsset();
            }
            else if (hasCachedImage)
            {
                MyDebug.Verbose($"Loading cached image asset: {_currentMainAsset.Id}");
                _isVideoAd = false;
                LoadImageAsset();
            }
            else
            {
                MyDebug.LogWarning("No media assets available to display (offline and no cached image) - hiding entire ad");
                _isVideoAd = false;
                _currentMainAsset = null;

                // Hide entire ad when no media is available offline
                HideEntireAd();
            }
        }

        /// <summary>
        /// Hides the entire ad when no media assets are available offline
        /// Provides clean error state for video-only ads when device is offline
        /// </summary>
        private void HideEntireAd()
        {
            MyDebug.LogWarning("[AdDisplayComponent] Hiding entire ad - no displayable content available offline");

            // Hide the entire background/container - this effectively hides the whole ad
            if (backgroundImage != null && backgroundImage.gameObject != null)
            {
                backgroundImage.gameObject.SetActive(false);
            }

            // Also hide individual components for completeness
            if (mainAssetVideoPlayer != null)
                mainAssetVideoPlayer.gameObject.SetActive(false);
            if (rawAssetImage != null)
                rawAssetImage.gameObject.SetActive(false);
            if (mainAssetImage != null)
                mainAssetImage.gameObject.SetActive(false);

            // Trigger close callback immediately to signal ad failure
            // This causes the placement to handle the error state properly
            _onCloseCallback?.Invoke();

            MyDebug.LogWarning("[AdDisplayComponent] Ad closed due to no offline content available");
        }

        private void LoadVideoAsset()
        {
            if (mainAssetVideoPlayer == null)
            {
                MyDebug.LogError($"[AdDisplayComponent] Video asset found but no VideoPlayer component assigned for {adFormat} ad");
                // Fallback to loading as image
                LoadImageAsset();
                return;
            }

            // Ensure the VideoPlayer GameObject and component are valid
            if (mainAssetVideoPlayer.gameObject == null)
            {
                MyDebug.LogError($"[AdDisplayComponent] VideoPlayer GameObject is null for {adFormat} ad");
                LoadImageAsset();
                return;
            }

            try
            {
                string videoUrl = null;

                // Check if this is a direct URL (streaming) or cached file
                if (_currentMainAsset.LocalPath.StartsWith("http://") || _currentMainAsset.LocalPath.StartsWith("https://"))
                {
                    // Direct URL streaming - no caching
                    videoUrl = _currentMainAsset.LocalPath;
                    MyDebug.Verbose($"[AdDisplayComponent] Streaming video from URL: {videoUrl}");
                }
                else
                {
                    // Cached file - use the traditional method
                    videoUrl = Advertisement.LoadVideoUrl(_currentMainAsset.Id);
                    MyDebug.Verbose($"[AdDisplayComponent] Loading cached video: {videoUrl}");
                }

                if (!string.IsNullOrEmpty(videoUrl))
                {
                    // Prepare video for loading
                    _videoLoaded = false;
                    _videoStarted = false;

                    // Set video source
                    mainAssetVideoPlayer.source = VideoSource.Url;
                    mainAssetVideoPlayer.url = videoUrl;

                    // Enable video player and ensure component is enabled
                    mainAssetVideoPlayer.gameObject.SetActive(true);
                    mainAssetVideoPlayer.enabled = true;

                    // Hide static image since we're showing video
                    if (mainAssetImage != null)
                    {
                        mainAssetImage.gameObject.SetActive(false);
                    }

                    // Setup RawImage for video display (will be properly configured when video is prepared)
                    if (rawAssetImage != null && mainAssetVideoPlayer.renderMode == VideoRenderMode.RenderTexture)
                    {
                        rawAssetImage.gameObject.SetActive(false); // Keep inactive to prevent white first frame
                        rawAssetImage.texture = null; // Will be set when video is prepared
                    }

                    MyDebug.Verbose($"[AdDisplayComponent] VideoPlayer state - GameObject active: {mainAssetVideoPlayer.gameObject.activeInHierarchy}, Component enabled: {mainAssetVideoPlayer.enabled}");

                    // Wait a frame to ensure the VideoPlayer is properly initialized before preparing
                    StartCoroutine(PrepareVideoDelayed());

                    MyDebug.Verbose($"[AdDisplayComponent] Loading video asset: {videoUrl}");
                }
                else
                {
                    MyDebug.LogError($"[AdDisplayComponent] Failed to get video URL for asset: {_currentMainAsset.Id}");
                    LoadImageAsset(); // Fallback to image
                }
            }
            catch (System.Exception ex)
            {
                MyDebug.LogError($"[AdDisplayComponent] Failed to load video asset: {ex.Message}");
                LoadImageAsset(); // Fallback to image
            }
        }

        /// <summary>
        /// Coroutine to prepare video with a slight delay to ensure VideoPlayer is properly initialized
        /// </summary>
        private IEnumerator PrepareVideoDelayed()
        {
            // Wait for the end of frame to ensure VideoPlayer is fully initialized
            yield return new WaitForEndOfFrame();

            // Double-check that the VideoPlayer is still valid and enabled
            if (mainAssetVideoPlayer != null &&
                mainAssetVideoPlayer.gameObject.activeInHierarchy &&
                mainAssetVideoPlayer.enabled)
            {
                try
                {
                    mainAssetVideoPlayer.Prepare();
                    MyDebug.Verbose($"[AdDisplayComponent] VideoPlayer preparation initiated successfully");
                }
                catch (System.Exception ex)
                {
                    MyDebug.LogError($"[AdDisplayComponent] Failed to prepare VideoPlayer: {ex.Message}");
                    // Fallback to image if preparation fails
                    _isVideoAd = false;
                    LoadImageAsset();
                }
            }
            else
            {
                MyDebug.LogError("[AdDisplayComponent] VideoPlayer is not ready for preparation - falling back to image");
                _isVideoAd = false;
                LoadImageAsset();
            }
        }

        private void LoadImageAsset()
        {
            if (_currentMainAsset != null && mainAssetImage != null)
            {
                var texture = Advertisement.LoadTexture(_currentMainAsset.Id);
                if (texture != null)
                {
                    var sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                    mainAssetImage.sprite = sprite;
                    mainAssetImage.gameObject.SetActive(true);

                    // Ensure video components are completely disabled for image ads
                    if (mainAssetVideoPlayer != null)
                    {
                        mainAssetVideoPlayer.gameObject.SetActive(false);
                        mainAssetVideoPlayer.enabled = false;
                    }

                    if (rawAssetImage != null)
                    {
                        rawAssetImage.gameObject.SetActive(false);
                    }
                }
                else
                {
                    MyDebug.LogError($"Failed to load texture for main asset: {_currentMainAsset.Id}");
                    mainAssetImage?.gameObject.SetActive(false);

                    // Disable video components
                    if (mainAssetVideoPlayer != null)
                        mainAssetVideoPlayer.gameObject.SetActive(false);
                    if (rawAssetImage != null)
                        rawAssetImage.gameObject.SetActive(false);
                }
            }
            else
            {
                mainAssetImage?.gameObject.SetActive(false);

                // Disable video components
                if (mainAssetVideoPlayer != null)
                    mainAssetVideoPlayer.gameObject.SetActive(false);
                if (rawAssetImage != null)
                    rawAssetImage.gameObject.SetActive(false);
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
                    MyDebug.LogError($"Failed to load texture for logo asset: {_currentLogoAsset.Id}");
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
                              _currentAd?.main_video?.url ??
                              _currentAd?.main_image?.url ??
                              _currentAd?.logo?.url;

            if (!string.IsNullOrEmpty(urlToOpen))
            {
                Analytics.MyDebug.Verbose($"[AdDisplayComponent] Opening URL: {urlToOpen}");
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

            // Set countdown duration based on ad format and type
            if (_isVideoAd)
            {
                // For video ads, use video duration or minimum countdown
                var videoDuration = GetVideoDuration();
                _countdownTime = adFormat switch
                {
                    AdFormat.banner => 0f, // Immediate
                    AdFormat.interstitial => Mathf.Max(5f, videoDuration * 0.8f), // 80% of video or minimum 5 seconds
                    AdFormat.rewarded => Mathf.Max(10f, videoDuration), // Full video or minimum 10 seconds
                    _ => 0f
                };
            }
            else
            {
                // For static image ads, use fixed durations
                _countdownTime = adFormat switch
                {
                    AdFormat.banner => 0f, // Immediate
                    AdFormat.interstitial => 5f, // 5 seconds
                    AdFormat.rewarded => 10f, // 10 seconds
                    _ => 0f
                };
            }

            if (_countdownTime <= 0f)
            {
                EnableCloseButton();
            }
            else
            {
                _isCountingDown = true;
                closeButton.interactable = false;
                _closeButtonImage.raycastTarget = false;
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
            if (adFormat == AdFormat.rewarded)
                _onRewardedCallback?.Invoke();

            if (closeButton != null)
            {
                closeButton.interactable = true;
                _closeButtonImage.raycastTarget = true;
                closeButton.gameObject.SetActive(true);

                if (_closeButtonText != null)
                {
                    _closeButtonText.text = "X";
                }
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

        /// <summary>
        /// Applies the custom font to all TextMeshPro components in the ad
        /// Call this method after setting up text content to ensure proper font application
        /// </summary>
        private void ApplyCustomFontToAllText()
        {
            if (customAdFont == null)
            {
                MyDebug.Verbose("[AdDisplayComponent] No custom font assigned, using default fonts");
                return;
            }

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
                MyDebug.Verbose($"[AdDisplayComponent] {componentName} text component is null, skipping font application");
                return;
            }

            if (customAdFont == null)
            {
                MyDebug.Verbose($"[AdDisplayComponent] No custom font assigned for {componentName}");
                return;
            }

            // Store the original font for potential restoration
            var originalFont = textComponent.font;

            // Apply the custom font
            textComponent.font = customAdFont;
            UpdateStyle(textComponent);
        }

        /// <summary>
        /// Sets up custom font configuration - call this in Awake or when font is assigned
        /// </summary>
        public void SetCustomFont(TMPro.TMP_FontAsset font)
        {
            customAdFont = font;
            // Apply immediately if we have text components ready
            ApplyCustomFontToAllText();
        }

        private void SetTextWithConnection(RTLTextMeshPro textComponent, string text, TextType textType = TextType.Button)
        {
            if (textComponent == null || string.IsNullOrEmpty(text))
                return;

            textComponent.text = text;
        }

        private void UpdateStyle(RTLTextMeshPro textComponent)
        {
            if (adFormat == AdFormat.banner) return;
            var duplicatedMaterial = new Material(textComponent.fontMaterial);
            duplicatedMaterial.SetFloat(ShaderUtilities.ID_OutlineWidth, 0.2f);
            duplicatedMaterial.SetColor(ShaderUtilities.ID_OutlineColor, Color.black);
            duplicatedMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, 0.35f);
            textComponent.fontMaterial = duplicatedMaterial;
        }

        // Video event handlers
        private void OnVideoPrepared(VideoPlayer vp)
        {
            _videoLoaded = true;
            MyDebug.Verbose($"[AdDisplayComponent] Video prepared for {adFormat} ad");

            // Handle render texture setup for both dynamic and pre-assigned cases
            if (mainAssetVideoPlayer.renderMode == VideoRenderMode.RenderTexture && rawAssetImage != null)
            {
                if (mainAssetVideoPlayer.targetTexture == null)
                {
                    // Create dynamic render texture if no target texture is assigned
                    int videoWidth = (int)mainAssetVideoPlayer.width;
                    int videoHeight = (int)mainAssetVideoPlayer.height;

                    if (videoWidth > 0 && videoHeight > 0)
                    {
                        _dynamicRenderTexture = CreateOptimizedRenderTexture(videoWidth, videoHeight);
                        mainAssetVideoPlayer.targetTexture = _dynamicRenderTexture;

                        MyDebug.Verbose($"[AdDisplayComponent] Created dynamic render texture: {videoWidth}x{videoHeight}");
                    }
                    else
                    {
                        MyDebug.LogWarning($"[AdDisplayComponent] Invalid video dimensions: {videoWidth}x{videoHeight}");
                        return;
                    }
                }
                else
                {
                    // Use the pre-assigned render texture
                    MyDebug.Verbose($"[AdDisplayComponent] Using pre-assigned render texture: {mainAssetVideoPlayer.targetTexture.width}x{mainAssetVideoPlayer.targetTexture.height}");
                }

                // Setup video display for BOTH dynamic and pre-assigned render textures
                SetupVideoForDisplay();
            }

            // Check and apply audio mute settings
            UpdateAudioMuteState();

            // Auto-play video once it's prepared (for interstitial and rewarded)
            if (adFormat == AdFormat.interstitial || adFormat == AdFormat.rewarded)
            {
                StartVideoPlayback();
            }
        }

        /// <summary>
        /// Sets up the separated RawImage component to display video content from render texture
        /// </summary>
        private void SetupVideoForDisplay()
        {
            // Get the active render texture (either dynamic or pre-assigned)
            var activeRenderTexture = _dynamicRenderTexture ?? mainAssetVideoPlayer.targetTexture;

            if (activeRenderTexture == null || mainAssetVideoPlayer == null)
            {
                MyDebug.LogError($"[AdDisplayComponent] No render texture available or mainAssetVideoPlayer is null. activeRenderTexture: {activeRenderTexture}, mainAssetVideoPlayer: {mainAssetVideoPlayer}");
                return;
            }

            // Set up the rawAssetImage texture but DON'T activate it yet to prevent white first frame
            if (rawAssetImage != null)
            {
                rawAssetImage.texture = activeRenderTexture;
                rawAssetImage.SetNativeSize();
                var rectTransform = rawAssetImage.GetComponent<RectTransform>();
                rectTransform.anchorMin = Vector2.zero;
                rectTransform.anchorMax = Vector2.one;
                rectTransform.offsetMin = Vector2.zero;
                rectTransform.offsetMax = Vector2.zero;

                rawAssetImage.gameObject.SetActive(false);

                MyDebug.Verbose($"[AdDisplayComponent] Configured separated RawImage component for video display with {(activeRenderTexture == _dynamicRenderTexture ? "dynamic" : "pre-assigned")} render texture ({activeRenderTexture.width}x{activeRenderTexture.height}), will activate when video starts");
            }
            else
            {
                MyDebug.LogError("[AdDisplayComponent] No rawAssetImage component assigned for video display");
                return;
            }

            // Ensure static image component is hidden since video is displayed on rawAssetImage
            if (mainAssetImage != null)
            {
                mainAssetImage.gameObject.SetActive(false);
            }

            // Print component state for debugging
            DebugComponentStates();

            // Ensure video player visual rendering is disabled (we only want audio and render texture output)
            if (mainAssetVideoPlayer != null)
            {
                // Keep the VideoPlayer component enabled but hide any visual renderers
                var videoRenderer = mainAssetVideoPlayer.GetComponent<Renderer>();
                if (videoRenderer != null)
                {
                    videoRenderer.enabled = false;
                    MyDebug.Verbose("[AdDisplayComponent] Disabled VideoPlayer Renderer component");
                }

                // Also check for any Canvas Renderer that might be showing video directly
                var canvasRenderer = mainAssetVideoPlayer.GetComponent<CanvasRenderer>();
                if (canvasRenderer != null)
                {
                    canvasRenderer.SetAlpha(0f);
                    MyDebug.Verbose("[AdDisplayComponent] Set VideoPlayer CanvasRenderer alpha to 0");
                }
            }
        }

        /// <summary>
        /// Debug method to print component states
        /// </summary>
        private void DebugComponentStates()
        {
            if (mainAssetVideoPlayer != null)
            {
                MyDebug.Verbose($"[AdDisplayComponent] Component States:");
                MyDebug.Verbose($"  VideoPlayer GameObject Active: {mainAssetVideoPlayer.gameObject.activeInHierarchy}");
                MyDebug.Verbose($"  VideoPlayer TargetTexture: {mainAssetVideoPlayer.targetTexture}");
                MyDebug.Verbose($"  Dynamic RenderTexture: {_dynamicRenderTexture}");

                if (rawAssetImage != null)
                {
                    MyDebug.Verbose($"  RawAssetImage GameObject Active: {rawAssetImage.gameObject.activeInHierarchy}");
                    MyDebug.Verbose($"  RawAssetImage Texture: {rawAssetImage.texture}");
                }

                if (mainAssetImage != null)
                {
                    MyDebug.Verbose($"  MainAssetImage GameObject Active: {mainAssetImage.gameObject.activeInHierarchy}");
                }
            }
        }



        private void OnVideoStarted(VideoPlayer vp)
        {
            _videoStarted = true;
            MyDebug.Verbose($"[AdDisplayComponent] Video started for {adFormat} ad");

            // Now that the video has started and first frame is rendered, activate the rawAssetImage to prevent white frame
            if (rawAssetImage != null && rawAssetImage.texture != null)
            {
                rawAssetImage.gameObject.SetActive(true);
                MyDebug.Verbose($"[AdDisplayComponent] Activated rawAssetImage now that video has started - no more white first frame");
            }

            // Get the active render texture (either dynamic or pre-assigned)
            var activeRenderTexture = _dynamicRenderTexture ?? mainAssetVideoPlayer.targetTexture;

            // Verify the RawImage on VideoPlayer is properly displaying the video
            if (activeRenderTexture != null && mainAssetVideoPlayer != null)
            {
                var rawImage = rawAssetImage.GetComponent<RawImage>();

                if (rawImage != null && rawImage.enabled)
                {
                    // RawImage on VideoPlayer is handling the display directly
                    MyDebug.Verbose($"[AdDisplayComponent] RawImage on VideoPlayer is displaying video directly from {(activeRenderTexture == _dynamicRenderTexture ? "dynamic" : "pre-assigned")} render texture");
                }
                else
                {
                    MyDebug.Verbose("[AdDisplayComponent] No valid RawImage component found on VideoPlayer for video display");
                }
            }
            else
            {
                MyDebug.LogWarning($"[AdDisplayComponent] No render texture available for video display. activeRenderTexture: {activeRenderTexture}, mainAssetVideoPlayer: {mainAssetVideoPlayer}");
            }
        }



        /// <summary>
        private void OnVideoFinished(VideoPlayer vp)
        {
            MyDebug.Verbose($"[AdDisplayComponent] Video finished for {adFormat} ad");

            // For rewarded ads, enable close button when video finishes
            if (adFormat == AdFormat.rewarded)
            {
                EnableCloseButton();
            }
        }

        private void OnVideoError(VideoPlayer vp, string message)
        {
            MyDebug.LogError($"[AdDisplayComponent] Video error for {adFormat} ad: {message}");

            // Fallback to image display if video fails
            _isVideoAd = false;
            LoadImageAsset();
        }

        private void StartVideoPlayback()
        {
            if (mainAssetVideoPlayer != null && _videoLoaded && !_videoStarted)
            {
                // Update audio state before playing
                UpdateAudioMuteState();

                mainAssetVideoPlayer.Play();
                MyDebug.Verbose($"[AdDisplayComponent] Starting video playback for {adFormat} ad");
            }
        }

        /// <summary>
        /// Updates the audio mute state based on device silent mode and system audio settings
        /// </summary>
        private void UpdateAudioMuteState()
        {
            if (mainAssetVideoPlayer == null) return;

            // Check if device is in silent mode or audio is disabled
            _shouldMuteAudio = AudioListener.pause || AudioListener.volume == 0;

            // On mobile platforms, also check for silent mode
#if UNITY_ANDROID || UNITY_IOS
            // Unity automatically handles device silent mode for VideoPlayer audio on mobile
            // but we can add extra checks if needed
            if (Application.isMobilePlatform)
            {
                // The system will automatically mute video audio when device is in silent mode
                // We just need to ensure our audio output mode is set correctly
                MyDebug.Verbose($"[AdDisplayComponent] Mobile platform detected, system will handle silent mode");
            }
#endif

            // Apply mute state to video player
            if (mainAssetVideoPlayer.audioOutputMode == VideoAudioOutputMode.Direct)
            {
                for (ushort trackIndex = 0; trackIndex < mainAssetVideoPlayer.audioTrackCount; trackIndex++)
                {
                    mainAssetVideoPlayer.SetDirectAudioMute(trackIndex, _shouldMuteAudio);
                }
            }

            MyDebug.Verbose($"[AdDisplayComponent] Audio mute state updated: {_shouldMuteAudio}");
        }

        /// <summary>
        /// Returns true if this is a video ad and video is currently playing
        /// </summary>
        public bool IsVideoPlaying()
        {
            return _isVideoAd && mainAssetVideoPlayer != null && mainAssetVideoPlayer.isPlaying;
        }

        /// <summary>
        /// Returns true if this ad contains video content
        /// </summary>
        public bool IsVideoAd()
        {
            return _isVideoAd;
        }

        /// <summary>
        /// Gets the video duration in seconds, or 0 if not a video ad
        /// </summary>
        public float GetVideoDuration()
        {
            if (_isVideoAd && mainAssetVideoPlayer != null && _videoLoaded)
            {
                return (float)mainAssetVideoPlayer.length;
            }
            return 0f;
        }

        private void OnDestroy()
        {
            // Cleanup dynamic render texture
            CleanupDynamicRenderTexture();

            // Cleanup video player events
            if (mainAssetVideoPlayer != null)
            {
                mainAssetVideoPlayer.prepareCompleted -= OnVideoPrepared;
                mainAssetVideoPlayer.started -= OnVideoStarted;
                mainAssetVideoPlayer.loopPointReached -= OnVideoFinished;
                mainAssetVideoPlayer.errorReceived -= OnVideoError;
            }
        }
    }
}
