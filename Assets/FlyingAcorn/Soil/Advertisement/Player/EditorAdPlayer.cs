using System;
using System.Collections.Generic;
using System.IO;
using FlyingAcorn.Soil.Advertisement.Logic;
using Newtonsoft.Json;
using UnityEngine;

namespace FlyingAcorn.Soil.Advertisement.Player
{
    /// <summary>
    /// Stands in for the native players in the Editor (and on platforms without one). It follows
    /// the same protocol - asynchronous loads, slots, the lock policy, one fullscreen ad at a
    /// time - and draws a plain placeholder with IMGUI, so game flows can be exercised without a
    /// device. Videos are not played; a video ad is simulated for <see cref="SimulatedVideoSeconds"/>.
    /// </summary>
    internal sealed class EditorAdPlayer : IAdPlayer
    {
        private const float SimulatedVideoSeconds = 6f;

        private sealed class Loaded
        {
            public AdCreative Creative;
            public Texture2D Image;
            public bool IsVideo;
            public bool IsText;
        }

        private sealed class Presentation
        {
            public string Format;
            public Loaded Ad;
            public FullscreenShowOptions Options;
            public float StartedAt;
            public bool Unlocked;
        }

        private readonly Action<NativeAdEvent> _report;
        private readonly Dictionary<string, Loaded> _slots = new();
        private readonly Queue<NativeAdEvent> _pending = new();
        private Presentation _fullscreen;
        private Loaded _banner;
        private string _bannerPosition = BannerPositions.Bottom;

        public EditorAdPlayer(SoilAdsNativeReceiver receiver, Action<NativeAdEvent> report)
        {
            _report = report;
            receiver.Ticked += Tick;
            receiver.Gui += Draw;
        }

        public void Load(string format, string creativeJson)
        {
            if (!AdFormats.IsPlayerFormat(format))
            {
                Send(format, NativeAdEventType.LoadFailed, error: NativeAdErrors.InvalidFormat);
                return;
            }

            AdCreative creative;
            try
            {
                creative = JsonConvert.DeserializeObject<AdCreative>(creativeJson ?? "");
            }
            catch (Exception)
            {
                creative = null;
            }

            if (creative == null)
            {
                Send(format, NativeAdEventType.LoadFailed, error: NativeAdErrors.InvalidCreative);
                return;
            }

            var loaded = new Loaded { Creative = creative, Image = ReadImage(creative.ImagePath) };
            loaded.IsVideo = format != AdFormats.Banner && File.Exists(creative.VideoPath ?? "");
            loaded.IsText = !loaded.IsVideo && loaded.Image == null && format == AdFormats.Banner
                            && !string.IsNullOrEmpty(creative.Title);

            if (!loaded.IsVideo && loaded.Image == null && !loaded.IsText)
            {
                var anyMedia = !string.IsNullOrEmpty(creative.ImagePath) || !string.IsNullOrEmpty(creative.VideoPath);
                Send(format, NativeAdEventType.LoadFailed,
                    error: anyMedia ? NativeAdErrors.MediaUnreadable : NativeAdErrors.InvalidCreative);
                return;
            }

            _slots[format] = loaded;
            var media = loaded.IsVideo ? "video" : loaded.IsText ? "text" : "image";
            Send(format, NativeAdEventType.Loaded, media: media,
                durationMs: loaded.IsVideo ? (long)(SimulatedVideoSeconds * 1000) : 0);
        }

        public void Show(string format, string optionsJson)
        {
            if (!_slots.TryGetValue(format ?? "", out var ad))
            {
                Send(format, NativeAdEventType.ShowFailed,
                    error: AdFormats.IsPlayerFormat(format) ? NativeAdErrors.NotLoaded : NativeAdErrors.InvalidFormat);
                return;
            }

            if (format == AdFormats.Banner)
            {
                _bannerPosition = ReadPosition(optionsJson);
                if (_banner != null)
                {
                    _banner = ad;
                    return;
                }

                _banner = ad;
                Send(format, NativeAdEventType.Shown);
                return;
            }

            if (_fullscreen != null)
            {
                Send(format, NativeAdEventType.ShowFailed, error: NativeAdErrors.AlreadyShowing);
                return;
            }

            _slots.Remove(format);
            _fullscreen = new Presentation
            {
                Format = format,
                Ad = ad,
                Options = ReadOptions(optionsJson, format),
                StartedAt = Time.realtimeSinceStartup
            };
            Send(format, NativeAdEventType.Shown);
        }

        public void Hide(string format)
        {
            if (format == AdFormats.Banner)
            {
                if (_banner == null) return;
                _banner = null;
                Send(format, NativeAdEventType.Closed);
                return;
            }

            if (_fullscreen != null && _fullscreen.Format == format)
                CloseFullscreen();
        }

        public void Destroy(string format)
        {
            Hide(format);
            _slots.Remove(format ?? "");
        }

        private void Tick()
        {
            if (_fullscreen != null && !_fullscreen.Unlocked && IsUnlocked(_fullscreen))
            {
                _fullscreen.Unlocked = true;
                if (_fullscreen.Format == AdFormats.Rewarded)
                    Send(_fullscreen.Format, NativeAdEventType.Rewarded);
            }

            // Delivered one frame later, like the native players do.
            var count = _pending.Count;
            for (var i = 0; i < count; i++)
                _report(_pending.Dequeue());
        }

        private static bool IsUnlocked(Presentation p)
        {
            var visible = Time.realtimeSinceStartup - p.StartedAt;
            return FullscreenLockPolicy.IsUnlocked(p.Options, p.Ad.IsVideo, SimulatedVideoSeconds, visible,
                Mathf.Min(visible, SimulatedVideoSeconds), visible >= SimulatedVideoSeconds, false);
        }

        private void CloseFullscreen()
        {
            var format = _fullscreen.Format;
            _fullscreen = null;
            Send(format, NativeAdEventType.Closed);
        }

        private void Click(string format, AdCreative creative)
        {
            if (!string.IsNullOrEmpty(creative.ClickUrl))
                Application.OpenURL(creative.ClickUrl);
            Send(format, NativeAdEventType.Clicked);
        }

        private void Send(string format, NativeAdEventType type, string media = null, long durationMs = 0,
            string error = null)
        {
            _pending.Enqueue(new NativeAdEvent
            {
                Format = format, Type = type, Media = media, DurationMs = durationMs, Error = error,
                Message = error == null ? null : "editor simulation"
            });
        }

        #region Drawing

        private void Draw()
        {
            if (_banner != null) DrawBanner();
            if (_fullscreen != null) DrawFullscreen(_fullscreen);
        }

        private void DrawBanner()
        {
            var height = Mathf.Round(Mathf.Min(Screen.width, Screen.height) * 0.14f);
            var y = _bannerPosition switch
            {
                // Screen.safeArea is bottom-up; IMGUI is top-down.
                BannerPositions.Top => Screen.height - Screen.safeArea.yMax,
                BannerPositions.Center => (Screen.height - height) / 2f,
                _ => Screen.height - Screen.safeArea.yMin - height
            };
            var rect = new Rect(0, y, Screen.width, height);

            GUI.Box(rect, GUIContent.none);
            var creative = _banner.Creative;
            if (_banner.Image != null)
                GUI.DrawTexture(rect, _banner.Image, ScaleMode.ScaleToFit);
            else
                GUI.Label(Inset(rect, 12), $"{creative.Title}\n{creative.Description}");

            GUI.Label(new Rect(rect.x + 4, rect.y + 2, 60, 20), "Ad");
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
                Click(AdFormats.Banner, creative);
        }

        private void DrawFullscreen(Presentation p)
        {
            var screen = new Rect(0, 0, Screen.width, Screen.height);
            GUI.color = Color.black;
            GUI.DrawTexture(screen, Texture2D.whiteTexture);
            GUI.color = Color.white;

            var creative = p.Ad.Creative;
            var barHeight = Screen.height * 0.16f;
            var media = new Rect(0, 0, Screen.width, Screen.height - barHeight);
            if (p.Ad.Image != null)
                GUI.DrawTexture(media, p.Ad.Image, ScaleMode.ScaleToFit);
            if (p.Ad.IsVideo)
                GUI.Label(Inset(media, 40), $"▶ Video ad (simulated {SimulatedVideoSeconds:0} s in the Editor)");
            if (GUI.Button(media, GUIContent.none, GUIStyle.none))
                Click(p.Format, creative);

            var bar = new Rect(0, Screen.height - barHeight, Screen.width, barHeight);
            GUI.Box(bar, GUIContent.none);
            GUI.Label(Inset(new Rect(bar.x, bar.y, bar.width * 0.65f, bar.height), 16),
                $"{creative.Title}\n{creative.Description}");
            var cta = new Rect(bar.width * 0.68f, bar.y + bar.height * 0.25f, bar.width * 0.28f, bar.height * 0.5f);
            if (GUI.Button(cta, string.IsNullOrEmpty(creative.CallToAction) ? "Open" : creative.CallToAction))
                Click(p.Format, creative);

            GUI.Label(new Rect(16, 16, 200, 24), "Ad (Editor)");
            var size = Mathf.Max(48f, Screen.height * 0.06f);
            var close = new Rect(Screen.width - size - 16, 16, size, size);
            if (p.Unlocked)
            {
                if (GUI.Button(close, "✕")) CloseFullscreen();
            }
            else
            {
                var visible = Time.realtimeSinceStartup - p.StartedAt;
                var remaining = FullscreenLockPolicy.SecondsRemaining(p.Options, p.Ad.IsVideo, SimulatedVideoSeconds,
                    visible, Mathf.Min(visible, SimulatedVideoSeconds), false);
                GUI.Box(close, remaining.ToString());
            }
        }

        private static Rect Inset(Rect r, float by) => new(r.x + by, r.y + by, r.width - 2 * by, r.height - 2 * by);

        #endregion

        private static Texture2D ReadImage(string path)
        {
            if (string.IsNullOrEmpty(path) || !File.Exists(path)) return null;
            try
            {
                var texture = new Texture2D(2, 2);
                if (texture.LoadImage(File.ReadAllBytes(path))) return texture;
                UnityEngine.Object.Destroy(texture);
            }
            catch (Exception)
            {
                // Unreadable images are reported through the load result.
            }

            return null;
        }

        private static FullscreenShowOptions ReadOptions(string json, string format)
        {
            try
            {
                return JsonConvert.DeserializeObject<FullscreenShowOptions>(json) ?? FullscreenShowOptions.DefaultsFor(format);
            }
            catch (Exception)
            {
                return FullscreenShowOptions.DefaultsFor(format);
            }
        }

        private static string ReadPosition(string json)
        {
            try
            {
                var position = (string)Newtonsoft.Json.Linq.JObject.Parse(json ?? "{}")["position"];
                return string.IsNullOrEmpty(position) ? BannerPositions.Bottom : position;
            }
            catch (Exception)
            {
                return BannerPositions.Bottom;
            }
        }
    }
}
