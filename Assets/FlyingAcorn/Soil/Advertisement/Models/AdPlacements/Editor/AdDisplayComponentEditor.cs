using UnityEngine;
using UnityEngine.Video;
using UnityEditor;

namespace FlyingAcorn.Soil.Advertisement.Models.AdPlacements.Editor
{
    /// <summary>
    /// Custom editor for AdDisplayComponent to help with video ad setup and optimization
    /// </summary>
    [CustomEditor(typeof(AdDisplayComponent))]
    public class AdDisplayComponentEditor : UnityEditor.Editor
    {
        private AdDisplayComponent adDisplay;

        private void OnEnable()
        {
            adDisplay = (AdDisplayComponent)target;
        }

        public override void OnInspectorGUI()
        {
            // Draw default inspector
            DrawDefaultInspector();

            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Video Ad Setup & Optimization", EditorStyles.boldLabel);

            // Video setup validation
            if (adDisplay.mainAssetVideoPlayer == null)
            {
                EditorGUILayout.HelpBox("No VideoPlayer assigned. Video ads will fall back to image display only.", MessageType.Warning);
                
                if (GUILayout.Button("Auto-Find VideoPlayer"))
                {
                    var videoPlayer = adDisplay.GetComponentInChildren<VideoPlayer>();
                    if (videoPlayer != null)
                    {
                        adDisplay.mainAssetVideoPlayer = videoPlayer;
                        EditorUtility.SetDirty(adDisplay);
                    }
                    else
                    {
                        EditorUtility.DisplayDialog("VideoPlayer Not Found", 
                            "No VideoPlayer component found in children. Please add one manually.", "OK");
                    }
                }
            }
            else
            {
                EditorGUILayout.HelpBox("VideoPlayer is properly assigned. Video ads are supported with dynamic optimization.", MessageType.Info);
                
                // Video player configuration check
                var videoPlayer = adDisplay.mainAssetVideoPlayer;
                bool hasIssues = false;
                
                if (videoPlayer.playOnAwake)
                {
                    EditorGUILayout.HelpBox("VideoPlayer.playOnAwake should be false for ad control.", MessageType.Warning);
                    hasIssues = true;
                }
                
                if (!videoPlayer.waitForFirstFrame)
                {
                    EditorGUILayout.HelpBox("VideoPlayer.waitForFirstFrame should be true for smooth playback.", MessageType.Warning);
                    hasIssues = true;
                }

                // New optimized settings check
                if (videoPlayer.skipOnDrop)
                {
                    EditorGUILayout.HelpBox("VideoPlayer.skipOnDrop should be false for better audio/video sync and color space handling.", MessageType.Warning);
                    hasIssues = true;
                }

                if (videoPlayer.audioOutputMode != VideoAudioOutputMode.Direct)
                {
                    EditorGUILayout.HelpBox("VideoPlayer.audioOutputMode should be Direct for better silent mode handling.", MessageType.Warning);
                    hasIssues = true;
                }

                // Color space and quality optimization check
                if (videoPlayer.renderMode != VideoRenderMode.RenderTexture)
                {
                    EditorGUILayout.HelpBox("VideoPlayer.renderMode should be RenderTexture for better color space control and quality optimization.", MessageType.Warning);
                    hasIssues = true;
                }
                
                if (hasIssues && GUILayout.Button("Fix VideoPlayer Settings"))
                {
                    videoPlayer.playOnAwake = false;
                    videoPlayer.waitForFirstFrame = true;
                    videoPlayer.skipOnDrop = false; // Better sync and color handling
                    videoPlayer.audioOutputMode = VideoAudioOutputMode.Direct; // Better audio control
                    videoPlayer.renderMode = VideoRenderMode.RenderTexture; // Better color space control
                    EditorUtility.SetDirty(videoPlayer);
                }
            }

            // Optimized render texture setup
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Render Texture Configuration", EditorStyles.boldLabel);
            
            if (adDisplay.mainAssetVideoPlayer != null)
            {
                if (adDisplay.videoRenderTexture == null)
                {
                    EditorGUILayout.HelpBox(
                        "Dynamic RenderTexture Optimization: No pre-assigned render texture found.\n" +
                        "• The system will automatically create optimized render textures at runtime\n" +
                        "• Render texture size will be based on video dimensions and maxRenderTextureSize setting\n" +
                        "• Memory will be managed automatically with proper cleanup\n" +
                        "• You can optionally assign a pre-made render texture below for manual control", 
                        MessageType.Info);

                    EditorGUILayout.BeginHorizontal();
                    if (GUILayout.Button("Create Optimized RenderTexture (1920x1080)"))
                    {
                        CreateStaticRenderTexture(1920, 1080);
                    }
                    if (GUILayout.Button("Create Optimized RenderTexture (1280x720)"))
                    {
                        CreateStaticRenderTexture(1280, 720);
                    }
                    EditorGUILayout.EndHorizontal();

                    if (GUILayout.Button("Create Custom Size Optimized RenderTexture"))
                    {
                        ShowCustomRenderTextureDialog();
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        "Static RenderTexture assigned. This will be used instead of dynamic creation.\n" +
                        $"Current size: {adDisplay.videoRenderTexture.width}x{adDisplay.videoRenderTexture.height}\n" +
                        "Consider removing this assignment to use dynamic optimization for better memory management.", 
                        MessageType.Info);

                    if (GUILayout.Button("Remove Static RenderTexture (Use Dynamic)"))
                    {
                        adDisplay.videoRenderTexture = null;
                        if (adDisplay.mainAssetVideoPlayer.targetTexture != null)
                        {
                            adDisplay.mainAssetVideoPlayer.targetTexture = null;
                        }
                        EditorUtility.SetDirty(adDisplay);
                        EditorUtility.SetDirty(adDisplay.mainAssetVideoPlayer);
                    }
                }            // Show current optimization settings
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Optimization Settings", EditorStyles.boldLabel);
            
            string maxSizeText = adDisplay.maxRenderTextureSize > 0 
                ? adDisplay.maxRenderTextureSize.ToString() 
                : "No limit (use source resolution)";
                
            EditorGUILayout.LabelField($"Max Render Texture Size: {maxSizeText}");
            EditorGUILayout.LabelField($"Render Texture Format: {adDisplay.renderTextureFormat}");
            EditorGUILayout.LabelField("Depth Buffer: 0 (None - Optimal for video)");                // Format recommendations with dynamic optimization
                var currentFormat = adDisplay.renderTextureFormat;
                var optimalFormat = GetOptimalRenderTextureFormat();
                
                if (currentFormat != optimalFormat)
                {
                    EditorGUILayout.HelpBox(
                        $"Current format: {currentFormat}\n" +
                        $"Recommended format: {optimalFormat}\n\n" +
                        "The system will automatically select the best supported format:\n" +
                        "• RGB565: Best memory efficiency (50% less memory than ARGB32)\n" +
                        "• ARGB4444: Extreme mobile optimization when RGB565 unavailable\n" +
                        "• ARGB32: Universal fallback for maximum compatibility\n\n" +
                        "Dynamic format selection reduces video quality only when necessary for device compatibility.",
                        MessageType.Info);
                        
                    if (GUILayout.Button($"Set to Optimal Format ({optimalFormat})"))
                    {
                        adDisplay.renderTextureFormat = optimalFormat;
                        EditorUtility.SetDirty(adDisplay);
                    }
                }
                else
                {
                    EditorGUILayout.HelpBox(
                        $"✓ Using optimal format: {currentFormat}\n" +
                        "This format provides the best balance of quality and performance for your target platform.",
                        MessageType.Info);
                }
                
                if (adDisplay.maxRenderTextureSize > 0)
                {
                    EditorGUILayout.HelpBox(
                        $"Dynamic render textures will be limited to {adDisplay.maxRenderTextureSize}px max dimension.\n" +
                        "Aspect ratio will be preserved. Set to 0 for no size limit.", 
                        MessageType.Info);
                }
            }

            // Format-specific guidance
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Format-Specific Features", EditorStyles.boldLabel);
            
            switch (adDisplay.adFormat)
            {
                case Data.Constants.AdFormat.banner:
                    EditorGUILayout.HelpBox("Banner ads typically use static images. Video support is available but not commonly used.", MessageType.Info);
                    break;
                case Data.Constants.AdFormat.interstitial:
                    EditorGUILayout.HelpBox("Interstitial video ads will auto-play and allow close after 5 seconds or 80% of video duration.", MessageType.Info);
                    break;
                case Data.Constants.AdFormat.rewarded:
                    EditorGUILayout.HelpBox("Rewarded video ads must play to completion before allowing close. Reward is granted when video finishes.", MessageType.Info);
                    break;
            }

            // Audio and sync settings info
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Audio & Color Space Optimization", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Improved Audio/Video Sync & Color Handling:\n" +
                "• skipOnDrop set to false for better synchronization and color space processing\n" +
                "• Direct audio output mode for better silent mode handling\n" +
                "• RenderTexture mode for optimal color space control and compatibility\n" +
                "• Automatic device silent mode detection on mobile platforms\n" +
                "• Optimized render texture formats reduce color space conflicts\n" +
                "• Dynamic quality fallbacks prevent AVFoundation color primaries issues", 
                MessageType.Info);

            // New caching strategy info
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Caching Strategy & Offline Behavior", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Images-Only Caching Strategy:\n" +
                "• Videos stream directly from URLs when online (not cached)\n" +
                "• Images are cached for offline fallback display\n" +
                "• When offline with video-only ads: Ad is hidden and error state triggered\n" +
                "• Clean error handling ensures proper user experience\n" +
                "• Significantly reduces storage footprint by excluding video files", 
                MessageType.Info);
        }

        private void CreateStaticRenderTexture(int width, int height)
        {
            // Create optimized render texture with quality fallbacks
            var renderTexture = CreateOptimizedRenderTexture(width, height, $"{adDisplay.name}_VideoTexture_{width}x{height}");
            
            // Save as asset
            var path = $"Assets/{renderTexture.name}.renderTexture";
            AssetDatabase.CreateAsset(renderTexture, path);
            AssetDatabase.SaveAssets();
            
            adDisplay.videoRenderTexture = renderTexture;
            adDisplay.mainAssetVideoPlayer.renderMode = VideoRenderMode.RenderTexture;
            adDisplay.mainAssetVideoPlayer.targetTexture = renderTexture;
            
            EditorUtility.SetDirty(adDisplay);
            EditorUtility.SetDirty(adDisplay.mainAssetVideoPlayer);
            
            Debug.Log($"Created optimized render texture: {width}x{height} (Format: {renderTexture.format}) at {path}");
        }

        private RenderTexture CreateOptimizedRenderTexture(int width, int height, string textureName)
        {
            // Determine optimal format with fallbacks for compatibility
            RenderTextureFormat format = GetOptimalRenderTextureFormat();
            
            // Create render texture with optimized settings
            var renderTexture = new RenderTexture(width, height, 0, format)
            {
                name = textureName,
                useMipMap = false,
                autoGenerateMips = false,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                anisoLevel = 0, // No anisotropic filtering needed for video
                antiAliasing = 1, // No anti-aliasing for performance
                enableRandomWrite = false, // Not needed for video playback
                useDynamicScale = false, // Static size for video content
                vrUsage = VRTextureUsage.None // Explicit VR setting
            };

            // Platform-specific optimizations
            #if UNITY_ANDROID
            // Android-specific optimizations
            renderTexture.memorylessMode = RenderTextureMemoryless.None; // Keep in memory for video
            #elif UNITY_IOS
            // iOS-specific optimizations for better AVFoundation compatibility
            renderTexture.memorylessMode = RenderTextureMemoryless.None;
            #endif

            return renderTexture;
        }

        private RenderTextureFormat GetOptimalRenderTextureFormat()
        {
            // Priority order: RGB565 > ARGB4444 > ARGB32 (fallback)
            var formatPriority = new RenderTextureFormat[]
            {
                RenderTextureFormat.RGB565,    // Best memory efficiency (50% less than ARGB32)
                RenderTextureFormat.ARGB4444,  // Extreme mobile optimization (4-bit per channel)
                RenderTextureFormat.ARGB32     // Universal fallback
            };

            foreach (var format in formatPriority)
            {
                if (SystemInfo.SupportsRenderTextureFormat(format))
                {
                    Debug.Log($"Selected render texture format: {format} (Memory efficient video playback)");
                    return format;
                }
            }

            // Ultimate fallback
            Debug.LogWarning("Using ARGB32 format as fallback - may use more memory than optimal");
            return RenderTextureFormat.ARGB32;
        }

        private void ShowCustomRenderTextureDialog()
        {
            var dialog = EditorUtility.DisplayDialog(
                "Create Custom RenderTexture",
                "This will create an optimized 1024x1024 render texture with automatic format selection. You can modify the size after creation in the Project window.",
                "Create Optimized 1024x1024",
                "Cancel"
            );

            if (dialog)
            {
                CreateStaticRenderTexture(1024, 1024);
            }
        }
    }
}
