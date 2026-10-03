#if UNITY_EDITOR
using System.IO;
using FlyingAcorn.Soil.Advertisement.Data;
using FlyingAcorn.Soil.Advertisement.Logic;
using UnityEngine;
using static FlyingAcorn.Soil.Advertisement.Data.Constants;

namespace FlyingAcorn.Soil.Advertisement.Player
{
    /// <summary>
    /// Editor only: stand-in ads for when Soil has nothing to show (no fill, no network), so every
    /// ad place in the game's layout can be seen in Play mode, as with other networks' test ads.
    /// A real ad from the server always replaces them. Nothing here exists in player builds.
    /// </summary>
    internal static class EditorTestAds
    {
        internal const string NativeAdId = "soil-editor-test-native";
        private const string Title = "Soil test ad";
        private const string ClickUrl = "https://example.com/soil-editor-test-ad";

        private static string _posterPath;

        internal static AdCreative CreativeFor(AdFormat format)
        {
            var creative = new AdCreative
            {
                AdId = $"soil-editor-test-{format}",
                Title = Title,
                Description = $"Editor only: Soil had no {format} ad to show.",
                CallToAction = "Install",
                ClickUrl = ClickUrl
            };
            // The banner is a text banner; fullscreen ads need media, so they get a generated image.
            if (format != AdFormat.banner)
            {
                try
                {
                    creative.ImagePath = PosterPath();
                }
                catch (System.Exception e)
                {
                    // Without its image the test ad simply does not load, like a no fill.
                    Debug.LogWarning($"[Advertisement] Could not write the Editor test ad image: {e.Message}");
                }
            }
            return creative;
        }

        internal static NativeAdContent NativeContent() =>
            new(NativeAdId, Title, "Editor only: Soil had no native ad to show.", "Install", ClickUrl,
                Gradient(256, 256), Gradient(600, 314));

        private static string PosterPath()
        {
            if (_posterPath != null && File.Exists(_posterPath)) return _posterPath;
            var texture = Gradient(540, 675);
            var directory = Path.Combine(Application.temporaryCachePath, "SoilEditorTestAds");
            Directory.CreateDirectory(directory);
            _posterPath = Path.Combine(directory, "poster.png");
            File.WriteAllBytes(_posterPath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
            return _posterPath;
        }

        /// <summary>A diagonal navy-to-purple gradient with stripes: clearly a placeholder.</summary>
        private static Texture2D Gradient(int width, int height)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            var pixels = new Color32[width * height];
            Color from = new Color32(0x11, 0x20, 0x3D, 0xFF);
            Color to = new Color32(0x8B, 0x5C, 0xF6, 0xFF);
            for (var y = 0; y < height; y++)
            for (var x = 0; x < width; x++)
            {
                var t = (x / (float)width + y / (float)height) * 0.5f;
                var color = Color.Lerp(from, to, t);
                if ((x + y) / 24 % 2 == 0) color *= 0.92f;
                color.a = 1f;
                pixels[y * width + x] = color;
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }
    }
}
#endif
