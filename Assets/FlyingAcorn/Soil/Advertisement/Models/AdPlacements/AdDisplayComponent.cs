using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Video;
using System.Linq;
using RTLTMPro;
using FlyingAcorn.Soil.Advertisement.Data;
using static FlyingAcorn.Soil.Advertisement.Data.Constants;
using FlyingAcorn.Analytics;
using TMPro;
using System.Collections;
using UnityEngine.Experimental.Rendering;

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
        [Tooltip("Used for all ad display - images, fallbacks, and video content from render texture")]
        public RawImage rawAssetImage;
        [Tooltip("Video player component for video ads only - renders to rawAssetImage when video is prepared")]
        public VideoPlayer mainAssetVideoPlayer;
        [Tooltip("Optional: Pre-assigned render texture. If null, will be created dynamically with optimized size")]
        public RenderTexture videoRenderTexture;
        public Image logoImage;
        public Button actionButton;
        public RTLTextMeshPro adTitleText;
        public RTLTextMeshPro adDescriptionText;
        public RTLTextMeshPro actionButtonText;
        public Button closeButton;
        public Image closeButtonImage;

        // Track the video preparation coroutine so it can be stopped if ad is closed
        private Coroutine _videoPrepareCoroutine;
        private Coroutine _countdownCoroutine;

        [Header("Video Configuration")]
        [Tooltip("Maximum resolution for dynamically created render textures (0 = use source resolution)")]
        public int maxRenderTextureSize = 1024;
        [Tooltip("Quality setting for render texture format")]
        public RenderTextureFormat renderTextureFormat = RenderTextureFormat.RGB565;
        [Tooltip("Create a render texture sized to the video for better performance")]
        public bool useDynamicRenderTexture = true;
        [Tooltip("Prefer routing video audio through an AudioSource for better A/V sync")]
        public bool preferAudioSourceOutput = true;
        [Tooltip("Optional AudioSource to output video audio when preferAudioSourceOutput is enabled")]
        public AudioSource videoAudioSource;
        [Tooltip("Drop frames to keep up with audio and reduce lag/stutter")]
        public bool dropFramesToMaintainSync = true;
        [Tooltip("Enable extra debug checks like first-frame white test (GPU readback) - disable in production")]
        public bool enableVideoDebugChecks = false;
        [Tooltip("Force ARGB32 render texture on mobile platforms for maximum compatibility")]
        public bool forceARGB32OnMobile = false;
        [Tooltip("Silence format fallback warnings (will log Verbose instead)")]
        public bool silentFormatFallbacks = true;

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
        private int _frameDropCount = 0; // Track frame drops for performance monitoring
        private bool _isVideoBuffering = false; // Track buffering state
        private int _consecutiveSyncIssues = 0; // Track consecutive sync drift issues
        private bool _forceMutedDueToSync = false; // Track if audio was muted due to sync issues

        // Countdown functionality
        private float _countdownTime;
        private bool _isCountingDown = false;
        private RTLTextMeshPro _closeButtonText;

        private void OnEnable()
        {
            SetupButtonListeners();
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
            if (mainAssetVideoPlayer == null)
            {
                if (adFormat == AdFormat.banner)
                    return;
                else
                {
                    MyDebug.LogError("[AdDisplayComponent] VideoPlayer component is required for video ads");
                    return;
                }
            }
            // Setup video player events
            mainAssetVideoPlayer.prepareCompleted += OnVideoPrepared;
            mainAssetVideoPlayer.started += OnVideoStarted;
            mainAssetVideoPlayer.loopPointReached += OnVideoFinished;
            mainAssetVideoPlayer.errorReceived += OnVideoError;

            // Configure video player defaults
            mainAssetVideoPlayer.playOnAwake = false;
            mainAssetVideoPlayer.waitForFirstFrame = true;
            mainAssetVideoPlayer.skipOnDrop = dropFramesToMaintainSync; // drop frames to keep A/V in sync on mobile

            // Platform-specific time update mode configuration for better sync
            // Prefer DSPTime so audio acts as the clock source; helps avoid drift
            mainAssetVideoPlayer.timeUpdateMode = VideoTimeUpdateMode.DSPTime;

            // Audio configuration for better sync and silent mode handling
            if (preferAudioSourceOutput && videoAudioSource != null)
            {
                try
                {
                    mainAssetVideoPlayer.audioOutputMode = VideoAudioOutputMode.AudioSource;
                    // Route first audio track to provided AudioSource; remaining tracks can be added if needed
                    mainAssetVideoPlayer.EnableAudioTrack(0, true);
                    mainAssetVideoPlayer.SetTargetAudioSource(0, videoAudioSource);

                    // Configure audio source to play independently of global pause state:
                    // - ignoreListenerPause ensures audio continues when AudioListener.pause=true
                    // - This allows ad audio to play even when gameplay audio is globally paused
                    // - Critical for ads to remain audible during gameplay pause (Time.timeScale=0)
                    videoAudioSource.ignoreListenerPause = true;
                    videoAudioSource.mute = false;
                }
                catch (System.Exception ex)
                {
                    // Some platforms don't support AudioSource output mode, fall back to Direct
                    MyDebug.LogWarning($"[AdDisplayComponent] AudioSource output mode not supported on this platform, falling back to Direct mode: {ex.Message}");
                    mainAssetVideoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
                }
            }
            else
            {
                mainAssetVideoPlayer.audioOutputMode = VideoAudioOutputMode.Direct;
            }

            // Add frame drop event for monitoring performance
            mainAssetVideoPlayer.frameDropped += OnVideoFrameDropped;

            // Initialize audio mute state early for consistent sync
            UpdateAudioMuteState();

            // Setup RawImage component for video display
            SetupVideoRawImage();
        }

        /// <summary>
        /// Sets up the RawImage component for all ad display (images and video)
        /// </summary>
        private void SetupVideoRawImage()
        {
            // Keep hidden until we have content to display to avoid white flashes
            rawAssetImage.texture = null;
            rawAssetImage.gameObject.SetActive(false);
        }

        /// <summary>
        /// Configures the video render mode and creates optimized render texture if needed
        /// </summary>
        private void ConfigureVideoRenderMode()
        {
            var preAssignedTexture = videoRenderTexture;

            mainAssetVideoPlayer.renderMode = VideoRenderMode.RenderTexture;
            // If we want optimal perf, create a texture matching the video; otherwise, use pre-assigned
            if (useDynamicRenderTexture && mainAssetVideoPlayer.isPrepared)
            {
                // Create or resize a dynamic RT based on video dimensions
                int vw = Mathf.Max(2, (int)mainAssetVideoPlayer.width);
                int vh = Mathf.Max(2, (int)mainAssetVideoPlayer.height);
                if (videoRenderTexture == null || videoRenderTexture.width != vw || videoRenderTexture.height != vh)
                {
                    // Cleanup old
                    CleanupDynamicRenderTexture();
                    videoRenderTexture = CreateOptimizedRenderTexture(vw, vh);
                }
                mainAssetVideoPlayer.targetTexture = videoRenderTexture;
                MyDebug.Verbose($"[AdDisplayComponent] Using dynamic render texture: {videoRenderTexture.width}x{videoRenderTexture.height} (video: {vw}x{vh})");
            }
            else
            {
                mainAssetVideoPlayer.targetTexture = preAssignedTexture;
                if (preAssignedTexture != null)
                    MyDebug.Verbose($"[AdDisplayComponent] Using pre-assigned render texture: {preAssignedTexture.width}x{preAssignedTexture.height}");
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

            // Keep exact aspect; NPOT textures are fine on modern mobile GPUs
            // Ensure even dimensions to avoid some hardware decoder quirks
            if ((targetWidth & 1) == 1) targetWidth++;
            if ((targetHeight & 1) == 1) targetHeight++;

            // Choose a supported GraphicsFormat for the color buffer (avoids runtime fallback warnings)
            GraphicsFormat preferredGfxFormat;
            // If requested, standardize to ARGB32 on mobile for broad compatibility
            bool forceArgb = forceARGB32OnMobile && Application.isMobilePlatform;
            switch (forceArgb ? RenderTextureFormat.ARGB32 : renderTextureFormat)
            {
                case RenderTextureFormat.RGB565:
                    preferredGfxFormat = GraphicsFormat.B5G6R5_UNormPack16;
                    break;
                case RenderTextureFormat.ARGB32:
                    preferredGfxFormat = GraphicsFormat.R8G8B8A8_UNorm;
                    break;
                default:
                    preferredGfxFormat = GraphicsFormat.R8G8B8A8_UNorm; // Safe default, will be checked below
                    break;
            }

#pragma warning disable 618
            if (!SystemInfo.IsFormatSupported(preferredGfxFormat, FormatUsage.Render))
            {
                var compatible = SystemInfo.GetCompatibleFormat(GraphicsFormat.R8G8B8A8_UNorm, FormatUsage.Render);
#pragma warning restore 618
                if (silentFormatFallbacks)
                    MyDebug.Verbose($"[AdDisplayComponent] GraphicsFormat {preferredGfxFormat} not supported; using {compatible}");
                else
                    MyDebug.LogWarning($"[AdDisplayComponent] GraphicsFormat {preferredGfxFormat} not supported; using {compatible}");
                preferredGfxFormat = compatible;
            }

            var desc = new RenderTextureDescriptor(targetWidth, targetHeight)
            {
                depthBufferBits = 0,
                graphicsFormat = preferredGfxFormat,
                msaaSamples = 1,
                sRGB = (QualitySettings.activeColorSpace == ColorSpace.Linear),
                mipCount = 1,
                useMipMap = false,
                autoGenerateMips = false
            };

            MyDebug.Verbose($"[AdDisplayComponent] Creating render texture: {targetWidth}x{targetHeight} (original: {videoWidth}x{videoHeight}), gfxFormat: {preferredGfxFormat}");

            // Create the render texture
            var renderTexture = new RenderTexture(desc)
            {
                name = $"VideoAd_RenderTexture_{adFormat}",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            renderTexture.Create();
            return renderTexture;
        }

        public void ShowAd(Ad ad, Action onClose = null, Action onClick = null, Action onRewarded = null, Action onShown = null)
        {
            // Always reset display state before showing a new ad (prevents stretched images, video issues)
            ResetDisplayState();

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
                    AnalyticsManager.ErrorEvent(Analytics.Constants.ErrorSeverity.FlyingAcornErrorSeverity.ErrorSeverity, "AdBackgroundImageNotFound");
                }

                Canvas.ForceUpdateCanvases();
            }
            catch (System.Exception)
            {
                AnalyticsManager.ErrorEvent(Analytics.Constants.ErrorSeverity.FlyingAcornErrorSeverity.ErrorSeverity, "AdDisplayException");
            }
        }

        private void FireAdShownEvents()
        {
            _onShownCallback?.Invoke();
            _onImpressionCallback?.Invoke();
        }

        public void HideAd()
        {

            // Reset display state to ensure clean reuse
            ResetDisplayState();

            if (backgroundImage != null && backgroundImage.gameObject != null)
                backgroundImage.gameObject.SetActive(false);
        }

        /// <summary>
        /// Resets all display-related state for reuse (RawImage, VideoPlayer, textures, rects, flags, etc)
        /// </summary>
        private void ResetDisplayState()
        {
            // Stop countdown
            if (_isCountingDown)
            {
                _isCountingDown = false;
                if (_countdownCoroutine != null)
                {
                    StopCoroutine(_countdownCoroutine);
                    _countdownCoroutine = null;
                }
            }

            // Stop and clean up video preparation coroutine if running
            if (_videoPrepareCoroutine != null)
            {
                StopCoroutine(_videoPrepareCoroutine);
                _videoPrepareCoroutine = null;
            }

            // Stop video if playing
            if (_isVideoAd && mainAssetVideoPlayer != null && mainAssetVideoPlayer.isPlaying)
            {
                mainAssetVideoPlayer.Stop();
            }

            // Clean up dynamic render texture
            CleanupDynamicRenderTexture();

            // Reset RawImage
            if (rawAssetImage != null)
            {
                rawAssetImage.texture = null;
                rawAssetImage.uvRect = new Rect(0, 0, 1, 1);
                rawAssetImage.SetNativeSize();
                var rectTransform = rawAssetImage.GetComponent<RectTransform>();
                if (rectTransform != null)
                {
                    rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                    rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                    rectTransform.pivot = new Vector2(0.5f, 0.5f);
                    rectTransform.anchoredPosition = Vector2.zero;
                    rectTransform.sizeDelta = Vector2.zero;
                }
                // Hide until a valid texture is assigned
                rawAssetImage.gameObject.SetActive(false);
            }

            // Reset VideoPlayer
            if (mainAssetVideoPlayer != null)
            {
                mainAssetVideoPlayer.Stop();
                mainAssetVideoPlayer.targetTexture = null;
                mainAssetVideoPlayer.url = string.Empty;
                mainAssetVideoPlayer.enabled = false;
                mainAssetVideoPlayer.gameObject.SetActive(false);
            }

            // Reset flags
            _isVideoAd = false;
            _videoLoaded = false;
            _videoStarted = false;
            _frameDropCount = 0;
            _isVideoBuffering = false;
            _consecutiveSyncIssues = 0;
            _forceMutedDueToSync = false;
        }

        /// <summary>
        /// Cleans up the dynamically created render texture to free memory
        /// </summary>
        private void CleanupDynamicRenderTexture()
        {
            // Clear the texture from rawAssetImage when hiding the ad
            if (rawAssetImage != null)
            {
                rawAssetImage.texture = null;
                rawAssetImage.gameObject.SetActive(false);
                MyDebug.Verbose("[AdDisplayComponent] RawImage texture cleared");
            }
        }

        private void ClearCallbacks()
        {
            // Stop any ongoing countdown
            if (_isCountingDown)
            {
                _isCountingDown = false;
                if (_countdownCoroutine != null)
                {
                    StopCoroutine(_countdownCoroutine);
                    _countdownCoroutine = null;
                }
            }

            // Stop and clean up video preparation coroutine if running
            if (_videoPrepareCoroutine != null)
            {
                StopCoroutine(_videoPrepareCoroutine);
                _videoPrepareCoroutine = null;
                MyDebug.Verbose("[AdDisplayComponent] Stopped video preparation coroutine on ClearCallbacks");
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
        /// - Always show fallback image immediately to prevent blank display
        /// </summary>
        private void LoadMainAsset()
        {
            // Check network connectivity for video streaming
            bool isOnline = Application.internetReachability != NetworkReachability.NotReachable;

            // Videos: Check for video URL when online
            bool hasVideoUrl = false;
            string videoUrl = null;
            if (isOnline && _currentAd?.main_video?.url != null)
            {
                videoUrl = _currentAd.main_video.url;
                hasVideoUrl = !string.IsNullOrEmpty(videoUrl);
            }

            // Always check for cached image (fallback when offline or no video)
            _currentMainAsset = Advertisement.GetCachedAsset(adFormat, AssetType.image);
            bool hasCachedImage = _currentMainAsset != null;

            MyDebug.Verbose($"LoadMainAsset - Online: {isOnline}, HasVideoUrl: {hasVideoUrl}, HasCachedImage: {hasCachedImage}");

            if (hasCachedImage)
            {
                MyDebug.Verbose($"LoadMainAsset - Found cached image asset: ID={_currentMainAsset.Id}, LocalPath={_currentMainAsset.LocalPath}, IsValid={_currentMainAsset.IsValid}");
            }
            else
            {
                AnalyticsManager.ErrorEvent(Analytics.Constants.ErrorSeverity.FlyingAcornErrorSeverity.WarningSeverity, "AdNoCachedImage");
                // Let's also check what assets are available
                var allAssets = Advertisement.GetCachedAssets(adFormat);
                MyDebug.Verbose($"LoadMainAsset - Available {adFormat} assets: {allAssets?.Count ?? 0}");
                if (allAssets != null)
                {
                    foreach (var asset in allAssets)
                    {
                        MyDebug.Verbose($"  Available asset: {asset.AssetType} - {asset.Id} - Valid: {asset.IsValid}");
                    }
                }
            }

            // ALWAYS show fallback image immediately if available to prevent blank display
            if (hasCachedImage)
            {
                ShowFallbackImageDuringVideoLoad(_currentMainAsset);
            }

            // Adaptive caching: If video is available and online, use async coroutine for download/preparation
            if (hasVideoUrl && isOnline)
            {
                StartCoroutine(DownloadAndCacheVideoAndContinue(_currentAd.main_video.id, videoUrl, adFormat, hasCachedImage));
                return;
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
            // ... existing code ...
        }

        // Coroutine for async video download and asset loading
        private IEnumerator DownloadAndCacheVideoAndContinue(string videoId, string videoUrl, AdFormat adFormat, bool hasCachedImage)
        {
            // Validate video compatibility for better sync
            if (!ValidateVideoCompatibility(videoUrl))
            {
                AnalyticsManager.ErrorEvent(Analytics.Constants.ErrorSeverity.FlyingAcornErrorSeverity.WarningSeverity, "AdVideoCompatibilityValidationFailed");
                LoadImageAsset();
                yield break;
            }

            // Async HEAD request to get video size
            long videoSizeBytes = -1;
            string contentType = null;
            bool headFailed = false;
            using (var uwr = UnityEngine.Networking.UnityWebRequest.Head(videoUrl))
            {
                yield return uwr.SendWebRequest();
                if (uwr.result == UnityEngine.Networking.UnityWebRequest.Result.Success)
                {
                    var contentLengthHeader = uwr.GetResponseHeader("Content-Length");
                    if (!string.IsNullOrEmpty(contentLengthHeader))
                        long.TryParse(contentLengthHeader, out videoSizeBytes);
                    contentType = uwr.GetResponseHeader("Content-Type");
                    MyDebug.Verbose($"[AdDisplayComponent] Video HEAD response: ContentLength={videoSizeBytes}, ContentType={contentType}");
                }
                else
                {
                    AnalyticsManager.ErrorEvent(Analytics.Constants.ErrorSeverity.FlyingAcornErrorSeverity.ErrorSeverity, "AdVideoHeadRequestFailed");
                    headFailed = true;
                }
            }

            // If video size is known and < 15MB, download and cache locally
            const long maxCacheSizeBytes = 15 * 1024 * 1024; // 15MB
            bool shouldCacheVideo = videoSizeBytes > 0 && videoSizeBytes <= maxCacheSizeBytes && !headFailed;

            if (shouldCacheVideo)
            {
                MyDebug.Verbose($"[AdDisplayComponent] Video size {videoSizeBytes} bytes < 15MB, will cache locally");
                bool done = false;
                string localVideoPath = null;
                yield return Advertisement.DownloadAndCacheVideoAsync(videoId, videoUrl, (path) =>
                {
                    localVideoPath = path;
                    done = true;
                });
                while (!done) yield return null;
                if (!string.IsNullOrEmpty(localVideoPath))
                {
                    _currentMainAsset = new AssetCacheEntry
                    {
                        Id = videoId,
                        LocalPath = localVideoPath,
                        OriginalUrl = videoUrl,
                        AssetType = AssetType.video,
                        AdFormat = adFormat
                    };
                    _isVideoAd = true;
                    if (hasCachedImage)
                    {
                        var fallbackImageAsset = Advertisement.GetCachedAsset(adFormat, AssetType.image);
                        ShowFallbackImageDuringVideoLoad(fallbackImageAsset);
                    }
                    // Video asset is ready; now prepare the VideoPlayer for true preloading
                    if (mainAssetVideoPlayer != null)
                    {
                        mainAssetVideoPlayer.source = VideoSource.Url;
                        // Ensure local files use file:// prefix for reliability across platforms
                        var localUrl = localVideoPath.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                            ? localVideoPath
                            : (localVideoPath.StartsWith("file://", StringComparison.OrdinalIgnoreCase) ? localVideoPath : "file://" + localVideoPath);
                        mainAssetVideoPlayer.url = localUrl;
                        mainAssetVideoPlayer.enabled = true;
                        mainAssetVideoPlayer.gameObject.SetActive(true);
                        // Start preparation coroutine
                        if (_videoPrepareCoroutine != null)
                        {
                            StopCoroutine(_videoPrepareCoroutine);
                        }
                        _videoPrepareCoroutine = StartCoroutine(PrepareVideoDelayed());
                    }
                    yield break;
                }
                else
                {
                    AnalyticsManager.ErrorEvent(Analytics.Constants.ErrorSeverity.FlyingAcornErrorSeverity.ErrorSeverity, "AdVideoDownloadCacheFailed");
                    // fallback to image
                }
            }
            else
            {
                MyDebug.Verbose($"[AdDisplayComponent] Video size {videoSizeBytes} bytes > 15MB or unknown, will stream");
                // For streaming, set up and prepare the VideoPlayer as well
                if (mainAssetVideoPlayer != null)
                {
                    _currentMainAsset = new AssetCacheEntry
                    {
                        Id = videoId,
                        LocalPath = videoUrl, // Use URL directly for streaming
                        OriginalUrl = videoUrl,
                        AssetType = AssetType.video,
                        AdFormat = adFormat
                    };
                    _isVideoAd = true;

                    // Show fallback image during video preparation for streaming videos too
                    if (hasCachedImage)
                    {
                        var fallbackImageAsset = Advertisement.GetCachedAsset(adFormat, AssetType.image);
                        ShowFallbackImageDuringVideoLoad(fallbackImageAsset);
                    }

                    mainAssetVideoPlayer.source = VideoSource.Url;
                    mainAssetVideoPlayer.url = videoUrl;
                    mainAssetVideoPlayer.enabled = true;
                    mainAssetVideoPlayer.gameObject.SetActive(true);
                    if (_videoPrepareCoroutine != null)
                    {
                        StopCoroutine(_videoPrepareCoroutine);
                    }
                    _videoPrepareCoroutine = StartCoroutine(PrepareVideoDelayed());
                    yield break;
                }
            }
            // fallback to image if video not available
            LoadImageAsset();
        }

        /// <summary>
        /// Validates video format and codec compatibility for better sync
        /// </summary>
        private bool ValidateVideoCompatibility(string videoUrl)
        {
            if (string.IsNullOrEmpty(videoUrl))
            {
                MyDebug.LogWarning("[AdDisplayComponent] Video URL is empty or null");
                return false;
            }

            // Check for supported video formats
            string url = videoUrl.ToLower();
            bool isSupported = url.Contains(".mp4") || url.Contains(".m4v") || url.Contains(".mov") ||
                              url.Contains("video/mp4") || url.Contains("video/quicktime");

            if (!isSupported)
            {
                AnalyticsManager.ErrorEvent(Analytics.Constants.ErrorSeverity.FlyingAcornErrorSeverity.WarningSeverity, "AdVideoFormatNotOptimal");
                // Still allow playback but log the warning
            }

            return true; // Allow all formats but warn about potentially problematic ones
        }

        /// <summary>
        /// Coroutine to prepare video with improved timing to ensure VideoPlayer is properly initialized
        /// and audio sync is maintained.
        /// Uses unscaled time to work correctly even when Time.timeScale is 0 (paused gameplay).
        /// </summary>
        private IEnumerator PrepareVideoDelayed()
        {
            // Wait for proper initialization using real time (unaffected by Time.timeScale)
            // This ensures video preparation continues even if gameplay is paused
            yield return new WaitForSecondsRealtime(0.1f);

            // Double-check that the VideoPlayer is still valid and enabled
            if (mainAssetVideoPlayer != null && mainAssetVideoPlayer.enabled)
            {
                bool preparationFailed = false;

                try
                {
                    // Pre-apply audio settings before preparation for better sync
                    UpdateAudioMuteState();

                    mainAssetVideoPlayer.Prepare();
                    MyDebug.Verbose($"[AdDisplayComponent] VideoPlayer preparation initiated successfully");
                }
                catch (System.Exception)
                {
                    AnalyticsManager.ErrorEvent(Analytics.Constants.ErrorSeverity.FlyingAcornErrorSeverity.ErrorSeverity, "AdVideoPlayerPreparationFailed");
                    preparationFailed = true;
                }

                if (preparationFailed)
                {
                    // Fallback to image if preparation fails
                    _isVideoAd = false;
                    LoadImageAsset();
                }
                else
                {
                    // Wait until video is actually prepared for better sync
                    yield return new WaitUntil(() => mainAssetVideoPlayer.isPrepared);
                    MyDebug.Verbose($"[AdDisplayComponent] VideoPlayer preparation completed successfully");
                }
            }
            else
            {
                AnalyticsManager.ErrorEvent(Analytics.Constants.ErrorSeverity.FlyingAcornErrorSeverity.ErrorSeverity, "AdVideoPlayerNotReady");
                _isVideoAd = false;
                LoadImageAsset();
            }

            // When finished, clear the coroutine reference
            _videoPrepareCoroutine = null;
        }

        private void ShowFallbackImageDuringVideoLoad(AssetCacheEntry imageAsset)
        {
            if (imageAsset != null)
            {
                MyDebug.Verbose($"[AdDisplayComponent] Fallback image candidate: {imageAsset.Id}");
                var texture = Advertisement.LoadTexture(imageAsset.Id);
                rawAssetImage.texture = texture;
                rawAssetImage.gameObject.SetActive(true);
                StretchRawImageAndSetNative();

                MyDebug.Verbose($"[AdDisplayComponent] Showing fallback image {imageAsset.Id} while video prepares");
            }
        }

        private void LoadImageAsset()
        {
            MyDebug.Verbose($"[AdDisplayComponent] LoadImageAsset called - _currentMainAsset: {_currentMainAsset?.Id}, rawAssetImage: {rawAssetImage != null}");

            if (_currentMainAsset != null && rawAssetImage != null)
            {
                MyDebug.Verbose($"[AdDisplayComponent] Attempting to load texture for asset: {_currentMainAsset.Id}, LocalPath: {_currentMainAsset.LocalPath}, IsValid: {_currentMainAsset.IsValid}");

                var texture = Advertisement.LoadTexture(_currentMainAsset.Id);
                if (texture != null)
                {
                    MyDebug.Verbose($"[AdDisplayComponent] Loaded texture for asset: {_currentMainAsset.Id}, TextureSize: {texture.width}x{texture.height}");
                    rawAssetImage.texture = texture;
                    rawAssetImage.gameObject.SetActive(true);
                    StretchRawImageAndSetNative();

                    // Ensure video player is disabled for image ads
                    if (mainAssetVideoPlayer != null)
                    {
                        mainAssetVideoPlayer.gameObject.SetActive(false);
                        mainAssetVideoPlayer.enabled = false;
                    }

                    MyDebug.Verbose($"[AdDisplayComponent] Image asset loaded and displayed successfully: {_currentMainAsset.Id}");
                }
                else
                {
                    AnalyticsManager.ErrorEvent(Analytics.Constants.ErrorSeverity.FlyingAcornErrorSeverity.ErrorSeverity, "AdTextureLoadFailed");

                    // Don't give up immediately - try to continue showing the ad without image
                    rawAssetImage.texture = null;

                    // For banners, avoid showing a white rectangle; hide the ad if no image is available
                    if (adFormat == AdFormat.banner)
                    {
                        MyDebug.LogWarning("[AdDisplayComponent] Banner image failed to load; hiding banner to avoid white texture");
                        HideEntireAd();
                        return;
                    }
                    else
                    {
                        // For other formats, keep the flow but do not show an empty white RawImage
                        rawAssetImage.gameObject.SetActive(false);
                    }

                    // Disable video components
                    if (mainAssetVideoPlayer != null)
                    {
                        mainAssetVideoPlayer.gameObject.SetActive(false);
                        mainAssetVideoPlayer.enabled = false;
                    }

                    AnalyticsManager.ErrorEvent(Analytics.Constants.ErrorSeverity.FlyingAcornErrorSeverity.WarningSeverity, "AdContinueWithoutMainImage");
                }
            }
            else
            {
                AnalyticsManager.ErrorEvent(Analytics.Constants.ErrorSeverity.FlyingAcornErrorSeverity.ErrorSeverity, "AdImageAssetLoadFailed");

                if (rawAssetImage != null)
                {
                    rawAssetImage.texture = null;
                    if (adFormat == AdFormat.banner)
                    {
                        MyDebug.LogWarning("[AdDisplayComponent] No main image asset; hiding banner to avoid white texture");
                        HideEntireAd();
                        return;
                    }
                    else
                    {
                        // For other formats, do not show an empty RawImage
                        rawAssetImage.gameObject.SetActive(false);
                    }
                }

                // Disable video components
                if (mainAssetVideoPlayer != null)
                {
                    mainAssetVideoPlayer.gameObject.SetActive(false);
                    mainAssetVideoPlayer.enabled = false;
                }

                AnalyticsManager.ErrorEvent(Analytics.Constants.ErrorSeverity.FlyingAcornErrorSeverity.WarningSeverity, "AdShowWithoutMainImage");
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
                    AnalyticsManager.ErrorEvent(Analytics.Constants.ErrorSeverity.FlyingAcornErrorSeverity.ErrorSeverity, "AdLogoTextureLoadFailed");
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
                    AdFormat.rewarded => Mathf.Max(20f, videoDuration), // Full video or minimum 10 seconds
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
                    AdFormat.rewarded => 20f, // 10 seconds
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
                closeButtonImage.raycastTarget = false;
                closeButton.gameObject.SetActive(true);
                UpdateCountdownDisplay();
                // Use unscaled time so countdown continues while gameplay is paused (Time.timeScale=0)
                if (_countdownCoroutine != null)
                {
                    StopCoroutine(_countdownCoroutine);
                    _countdownCoroutine = null;
                }
                _countdownCoroutine = StartCoroutine(CloseButtonCountdownRealtime());
            }
        }

        /// <summary>
        /// Countdown coroutine for close button using real time.
        /// Uses unscaled time so the countdown continues even when Time.timeScale is 0 (paused gameplay).
        /// </summary>
        private IEnumerator CloseButtonCountdownRealtime()
        {
            while (_isCountingDown && _countdownTime > 0f)
            {
                yield return new WaitForSecondsRealtime(1f);
                if (!_isCountingDown) { _countdownCoroutine = null; yield break; }
                _countdownTime -= 1f;
                UpdateCountdownDisplay();
            }

            if (_isCountingDown)
            {
                // Reached zero, enable close
                _isCountingDown = false;
                EnableCloseButton();
            }
            _countdownCoroutine = null;
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
            {
                _onRewardedCallback?.Invoke();
                _onRewardedCallback = null;
            }

            if (closeButton != null)
            {
                closeButton.interactable = true;
                closeButtonImage.raycastTarget = true;
                closeButton.gameObject.SetActive(true);

                if (_closeButtonText != null)
                {
                    _closeButtonText.text = "X";
                }
            }
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
            ConfigureVideoRenderMode();
            _videoLoaded = true;
            MyDebug.Verbose($"[AdDisplayComponent] Video prepared for {adFormat} ad");

            // Handle render texture setup for both dynamic and pre-assigned cases
            if (mainAssetVideoPlayer.renderMode == VideoRenderMode.RenderTexture && rawAssetImage != null)
                SetupVideoForDisplay();

            // Ensure audio tracks are enabled and mute state applied post-prepare
            try
            {
                if (mainAssetVideoPlayer.audioOutputMode == VideoAudioOutputMode.AudioSource && videoAudioSource != null)
                {
                    // Route and enable first track explicitly after prepare
                    mainAssetVideoPlayer.EnableAudioTrack(0, true);
                    mainAssetVideoPlayer.SetTargetAudioSource(0, videoAudioSource);
                }
                else if (mainAssetVideoPlayer.audioOutputMode == VideoAudioOutputMode.Direct)
                {
                    // Ensure first track is enabled for Direct output as well
                    mainAssetVideoPlayer.EnableAudioTrack(0, true);
                }
            }
            catch (Exception)
            {
                // Some platforms throw if tracks not available yet; ignore and proceed
            }
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
            var activeRenderTexture = videoRenderTexture;

            // Preserve aspect ratio of the video when fitting into parent rect
            if (activeRenderTexture != null)
            {
                FitRawImageToParentPreserveAspect(activeRenderTexture.width, activeRenderTexture.height);
            }

            MyDebug.Verbose($"[AdDisplayComponent] Configured RawImage for video display with : dynamic render texture ({activeRenderTexture.width}x{activeRenderTexture.height}), will switch to video when it starts");
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
        /// Fits the RawImage into its parent while preserving the given texture aspect ratio.
        /// Centers the image and chooses the largest size that fits inside parent rect.
        /// </summary>
        private void FitRawImageToParentPreserveAspect(int texWidth, int texHeight)
        {
            if (rawAssetImage == null) return;
            var rectTransform = rawAssetImage.GetComponent<RectTransform>();
            if (rectTransform == null) return;
            var parent = rectTransform.parent as RectTransform;
            if (parent == null) return;

            float pw = Mathf.Max(1f, parent.rect.width);
            float ph = Mathf.Max(1f, parent.rect.height);
            float pr = pw / ph;
            float tr = Mathf.Max(1, texWidth) / (float)Mathf.Max(1, texHeight);

            float targetW, targetH;
            if (tr > pr)
            {
                // Video is wider than parent: fit width
                targetW = pw;
                targetH = pw / tr;
            }
            else
            {
                // Video is taller: fit height
                targetH = ph;
                targetW = ph * tr;
            }

            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            rectTransform.sizeDelta = new Vector2(targetW, targetH);
        }
        private void StretchRawImageAndSetNative()
        {
            rawAssetImage.SetNativeSize();
            var rectTransform = rawAssetImage.GetComponent<RectTransform>();
            var parentRectTransform = rectTransform.parent as RectTransform;
            rectTransform.anchorMin = new Vector2(0.5f, 0.5f);
            rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            rectTransform.pivot = new Vector2(0.5f, 0.5f);
            rectTransform.anchoredPosition = Vector2.zero;
            if (adFormat == AdFormat.banner)
                rectTransform.sizeDelta = new Vector2(parentRectTransform.rect.width, parentRectTransform.rect.height);
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
                MyDebug.Verbose($"  RawAssetImage GameObject Active: {rawAssetImage.gameObject.activeInHierarchy}");
                MyDebug.Verbose($"  RawAssetImage Texture: {rawAssetImage.texture}");
            }
        }



        private void OnVideoStarted(VideoPlayer vp)
        {
            _videoStarted = true;
            MyDebug.Verbose($"[AdDisplayComponent] Video started for {adFormat} ad");

            // Start monitoring audio-video sync
            StartCoroutine(MonitorAudioVideoSync());

            // Now that video is playing, ensure rawAssetImage displays the video render texture
            if (rawAssetImage != null && mainAssetVideoPlayer != null && mainAssetVideoPlayer.targetTexture != null)
            {
                rawAssetImage.texture = mainAssetVideoPlayer.targetTexture;
                rawAssetImage.gameObject.SetActive(true);
                MyDebug.Verbose($"[AdDisplayComponent] rawAssetImage now displaying video content from render texture");

                if (enableVideoDebugChecks)
                {
                    // Check if the first frame is blank/white (common on failed/partial loads)
                    Texture2D frameCheck = new Texture2D(mainAssetVideoPlayer.targetTexture.width, mainAssetVideoPlayer.targetTexture.height, TextureFormat.RGB24, false);
                    try
                    {
                        RenderTexture.active = mainAssetVideoPlayer.targetTexture;
                        frameCheck.ReadPixels(new Rect(0, 0, frameCheck.width, frameCheck.height), 0, 0);
                        frameCheck.Apply();
                        RenderTexture.active = null;
                        Color32[] pixels = frameCheck.GetPixels32();
                        int whiteCount = pixels.Where(p => p.r > 240 && p.g > 240 && p.b > 240).Count();
                        float whiteRatio = (float)whiteCount / pixels.Length;
                        MyDebug.Verbose($"[AdDisplayComponent] Video frame check: {whiteCount} white pixels, ratio={whiteRatio:F2}"); // Conversation log
                        if (whiteRatio > 0.95f)
                        {
                            MyDebug.LogWarning("[AdDisplayComponent] Blank video frame; falling back to image");
                            _isVideoAd = false;
                            var fallbackImageAsset = Advertisement.GetCachedAsset(adFormat, AssetType.image);
                            MyDebug.Verbose($"[AdDisplayComponent] Fallback image asset after blank video: {fallbackImageAsset?.Id}"); // Conversation log
                            ShowFallbackImageDuringVideoLoad(fallbackImageAsset);
                            return;
                        }
                    }
                    finally
                    {
                        // Always dispose the temporary texture to prevent memory leaks
                        if (frameCheck != null)
                            DestroyImmediate(frameCheck);
                    }
                }
            }

            // remaining ad display logic
        }



        /// <summary>
        private void OnVideoFinished(VideoPlayer vp)
        {
            MyDebug.Verbose($"[AdDisplayComponent] Video finished for {adFormat}");

            // Show image fallback after video finishes once
            var fallbackImageAsset = Advertisement.GetCachedAsset(adFormat, AssetType.image);
            MyDebug.Verbose($"[AdDisplayComponent] Showing fallback image after video finished: {fallbackImageAsset?.Id}"); // Conversation log
            ShowFallbackImageDuringVideoLoad(fallbackImageAsset);

            // For rewarded ads, enable close button when video finishes
            if (adFormat == AdFormat.rewarded)
            {
                MyDebug.Verbose("[AdDisplayComponent] Enabling close button after rewarded video");
                EnableCloseButton();
            }
        }

        private void OnVideoError(VideoPlayer vp, string message)
        {
            AnalyticsManager.ErrorEvent(Analytics.Constants.ErrorSeverity.FlyingAcornErrorSeverity.ErrorSeverity, "AdVideoError");

            // Check for common sync-related errors
            bool isSyncError = message.Contains("audio") || message.Contains("sync") ||
                              message.Contains("timing") || message.Contains("decode");

            if (isSyncError)
            {
                MyDebug.LogWarning("[AdDisplayComponent] Detected potential sync-related video error, attempting recovery");

                // Try to recover by resetting time update mode
                if (mainAssetVideoPlayer != null)
                {
#if UNITY_IOS
                    mainAssetVideoPlayer.timeUpdateMode = VideoTimeUpdateMode.GameTime; // Fallback for iOS
#elif UNITY_ANDROID
                    mainAssetVideoPlayer.timeUpdateMode = VideoTimeUpdateMode.DSPTime; // Fallback for Android
#endif
                    MyDebug.Verbose("[AdDisplayComponent] Applied fallback time update mode for sync recovery");
                }
            }

            // Fallback to image display if video fails or if video is interrupted (network error, blank frame, etc)
            _isVideoAd = false;
            var fallbackImageAsset = Advertisement.GetCachedAsset(adFormat, AssetType.image);
            MyDebug.Verbose($"[AdDisplayComponent] Fallback image asset after video error: {fallbackImageAsset?.Id}"); // Conversation log
            ShowFallbackImageDuringVideoLoad(fallbackImageAsset);
        }

        /// <summary>
        /// Handles frame drops for better sync monitoring
        /// </summary>
        private void OnVideoFrameDropped(VideoPlayer vp)
        {
            _frameDropCount++;
            if (_frameDropCount > 20) // Alert after multiple drops
            {
                AnalyticsManager.ErrorEvent(Analytics.Constants.ErrorSeverity.FlyingAcornErrorSeverity.WarningSeverity, "AdVideoFrameDrops");
                _frameDropCount = 0; // Reset counter after alert
            }
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
            bool systemMuted = AudioListener.pause || AudioListener.volume == 0;

            // Combine system mute with sync mute
            _shouldMuteAudio = systemMuted || _forceMutedDueToSync;

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
            else if (mainAssetVideoPlayer.audioOutputMode == VideoAudioOutputMode.AudioSource && videoAudioSource != null)
            {
                videoAudioSource.mute = _shouldMuteAudio;
            }

            MyDebug.Verbose($"[AdDisplayComponent] Audio mute state updated: {_shouldMuteAudio} (System: {systemMuted}, Sync: {_forceMutedDueToSync})");
        }

        /// <summary>
        /// Forces audio mute due to major sync issues to prevent out-of-sync audio playback
        /// </summary>
        private void ForceMuteAudioDueToSync()
        {
            if (_forceMutedDueToSync) return; // Already muted due to sync

            _forceMutedDueToSync = true;
            _shouldMuteAudio = true;

            // Apply mute immediately
            if (mainAssetVideoPlayer != null && mainAssetVideoPlayer.audioOutputMode == VideoAudioOutputMode.Direct)
            {
                for (ushort trackIndex = 0; trackIndex < mainAssetVideoPlayer.audioTrackCount; trackIndex++)
                {
                    mainAssetVideoPlayer.SetDirectAudioMute(trackIndex, true);
                }
            }

            AnalyticsManager.ErrorEvent(Analytics.Constants.ErrorSeverity.FlyingAcornErrorSeverity.WarningSeverity, "AdAudioForceMutedDueToSync");
        }

        /// <summary>
        /// Attempts to restore audio if sync issues have been resolved
        /// </summary>
        private void TryRestoreAudioFromSyncMute()
        {
            if (!_forceMutedDueToSync) return;

            // Only restore if we haven't had sync issues for a while
            if (_consecutiveSyncIssues == 0)
            {
                _forceMutedDueToSync = false;
                UpdateAudioMuteState(); // This will restore normal mute state
                MyDebug.LogWarning("[AdDisplayComponent] Audio restored after sync issues resolved");
            }
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
        /// Monitors audio-video sync and logs potential issues.
        /// Automatically mutes audio if major sync issues (1.0s+ drift) persist for 3+ consecutive checks.
        /// Uses unscaled time to work correctly even when Time.timeScale is 0 (paused gameplay).
        /// </summary>
        private IEnumerator MonitorAudioVideoSync()
        {
            if (!_isVideoAd || mainAssetVideoPlayer == null) yield break;

            float lastVideoTime = 0f;
            float lastCheckTime = Time.unscaledTime; // Use unscaled time since ads play during pause
            int syncCheckCount = 0;
            _consecutiveSyncIssues = 0; // Reset counter at start

            while (_videoStarted && mainAssetVideoPlayer.isPlaying && syncCheckCount < 10)
            {
                // Check every second using real time (unaffected by Time.timeScale)
                yield return new WaitForSecondsRealtime(1f);

                float currentVideoTime = (float)mainAssetVideoPlayer.time;
                float currentRealTime = Time.unscaledTime;
                float expectedProgress = currentRealTime - lastCheckTime;
                float actualProgress = currentVideoTime - lastVideoTime;

                // Skip first 2 checks to allow sync to stabilize, and skip if video not advancing (buffering)
                if (syncCheckCount >= 2 && actualProgress > 0f)
                {
                    // Detect buffering state
                    _isVideoBuffering = (actualProgress < 0.1f && expectedProgress > 0.5f);

                    if (!_isVideoBuffering)
                    {
                        float syncDrift = Mathf.Abs(expectedProgress - actualProgress);

                        // Check for major sync issues (0.3s or more drift)
                        if (syncDrift > 0.3f)
                        {
                            _consecutiveSyncIssues++;
                            AnalyticsManager.ErrorEvent(Analytics.Constants.ErrorSeverity.FlyingAcornErrorSeverity.WarningSeverity, "AdVideoSyncDriftMajor");

                            // If we have 3 or more consecutive major sync issues, mute audio to prevent out-of-sync audio
                            if (_consecutiveSyncIssues >= 3 && !_forceMutedDueToSync)
                            {
                                ForceMuteAudioDueToSync();
                            }
                        }
                        else if (syncDrift <= 0.5f)
                        {
                            // Reset counter if sync is good
                            if (_consecutiveSyncIssues > 0)
                            {
                                _consecutiveSyncIssues = 0;
                                MyDebug.Verbose("[AdDisplayComponent] A/V sync recovered, resetting issue counter");

                                // Try to restore audio if it was muted due to sync issues
                                TryRestoreAudioFromSyncMute();
                            }
                        }

                        // Log warning for any sync drift over 0.5s (but don't mute yet)
                        if (syncDrift > 0.5f)
                        {
                            AnalyticsManager.ErrorEvent(Analytics.Constants.ErrorSeverity.FlyingAcornErrorSeverity.WarningSeverity, "AdVideoSyncDrift");
                        }
                    }
                    else
                    {
                        MyDebug.Verbose($"[AdDisplayComponent] Video buffering detected, skipping sync check");
                    }
                }

                lastVideoTime = currentVideoTime;
                lastCheckTime = currentRealTime;
                syncCheckCount++;
            }
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

        // Inspector utility: apply sensible defaults and wire recommended components
        [UnityEngine.ContextMenu("Soil Ads/Apply Best Options")]
        private void ContextApplyBestOptions()
        {
            ApplyBestOptions(true);
        }

        /// <summary>
        /// Applies recommended defaults for robust mobile playback and sync.
        /// Also ensures an AudioSource exists and is assigned when requested.
        /// </summary>
        /// <param name="createAudioSourceIfMissing">Create and assign an AudioSource if none is set.</param>
        public void ApplyBestOptions(bool createAudioSourceIfMissing = true)
        {
            // Video/display settings
            useDynamicRenderTexture = true;            // Match RT to video for perf
            preferAudioSourceOutput = true;           // Route audio via AudioSource for better A/V sync
            dropFramesToMaintainSync = true;          // Allow frame drop to stay in sync on mobile
            enableVideoDebugChecks = false;           // Avoid GPU readbacks in production
            forceARGB32OnMobile = true;               // Standardize to ARGB32 on mobile for compatibility
            silentFormatFallbacks = true;             // Silence format fallback warnings by default

            // Conservative, mobile-friendly render texture settings
            renderTextureFormat = RenderTextureFormat.RGB565;
            if (!SystemInfo.SupportsRenderTextureFormat(renderTextureFormat))
            {
                // Fall back to ARGB32 if RGB565 is not supported
                renderTextureFormat = SystemInfo.SupportsRenderTextureFormat(RenderTextureFormat.ARGB32)
                    ? RenderTextureFormat.ARGB32
                    : RenderTextureFormat.Default;
                if (silentFormatFallbacks)
                    MyDebug.Verbose($"[AdDisplayComponent] Best Options: Preferred RGB565 not supported; using {renderTextureFormat}");
                else
                    MyDebug.LogWarning($"[AdDisplayComponent] Best Options: Preferred RGB565 not supported; using {renderTextureFormat}");
            }
            if (maxRenderTextureSize <= 0 || maxRenderTextureSize > 4096)
                maxRenderTextureSize = 1024;          // Cap to 1024 by default (safe baseline)

            // Ensure an AudioSource is available and assigned
            if (createAudioSourceIfMissing)
            {
                if (videoAudioSource == null)
                {
                    var existing = GetComponent<AudioSource>();
                    if (existing == null)
                    {
                        existing = GetComponentInChildren<AudioSource>(true);
                    }

                    if (existing == null)
                    {
#if UNITY_EDITOR
                        var go = new GameObject("Ad Video AudioSource");
                        go.transform.SetParent(this.transform, false);
                        existing = go.AddComponent<AudioSource>();
                        // Record creation in undo history for editor convenience
                        UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Create Ad Video AudioSource");
#else
                        var go = new GameObject("Ad Video AudioSource");
                        go.transform.SetParent(this.transform, false);
                        existing = go.AddComponent<AudioSource>();
#endif
                    }

                    videoAudioSource = existing;
                }

                // Enforce recommended AudioSource settings
                if (videoAudioSource != null)
                {
                    videoAudioSource.playOnAwake = false;
                    videoAudioSource.loop = false;
                    videoAudioSource.spatialBlend = 0f; // 2D audio for UI
                }
            }

#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
            if (videoAudioSource != null)
            {
                UnityEditor.EditorUtility.SetDirty(videoAudioSource);
            }
#endif

            MyDebug.Verbose("[AdDisplayComponent] Applied best options to component");
        }

        private void OnDisable()
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
                mainAssetVideoPlayer.frameDropped -= OnVideoFrameDropped;
            }
        }
    }
}
