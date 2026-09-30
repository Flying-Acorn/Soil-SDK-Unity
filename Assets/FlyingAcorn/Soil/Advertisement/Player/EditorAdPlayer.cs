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
            SetGameInputBlocked(true);
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
            SetGameInputBlocked(false);
            Send(format, NativeAdEventType.Closed);
        }

        private void Click(string format, AdCreative creative)
        {
            Send(format, NativeAdEventType.Clicked);
            AdLinks.Open(creative.ClickUrl);
        }

        // What the placeholder's buttons do, callable by Play-mode tests too.

        /// <summary>The fullscreen ad's close button; does nothing while it is locked.</summary>
        internal bool PressClose()
        {
            if (_fullscreen == null || !_fullscreen.Unlocked) return false;
            CloseFullscreen();
            return true;
        }

        /// <summary>The fullscreen ad's call to action (or its media).</summary>
        internal bool PressCallToAction()
        {
            if (_fullscreen == null) return false;
            Click(_fullscreen.Format, _fullscreen.Ad.Creative);
            return true;
        }

        internal bool PressBanner()
        {
            if (_banner == null) return false;
            Click(AdFormats.Banner, _banner.Creative);
            return true;
        }

        internal bool IsFullscreenUnlocked => _fullscreen != null && _fullscreen.Unlocked;

        // On a device the game is paused under a fullscreen ad and gets no input; the Editor keeps it
        // running to draw the placeholder, so its UI is switched off instead of receiving the clicks
        // meant for the ad.
        private readonly List<Behaviour> _blockedInput = new();

        private void SetGameInputBlocked(bool blocked)
        {
            if (blocked)
            {
                foreach (var system in UnityEngine.Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsSortMode.None))
                {
                    if (!system.enabled) continue;
                    system.enabled = false;
                    _blockedInput.Add(system);
                }
                return;
            }

            foreach (var system in _blockedInput)
                if (system) system.enabled = true;
            _blockedInput.Clear();
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

        private GUIStyle _text, _title, _button, _badge;
        private float _styledFor;
        private readonly Dictionary<string, string> _shaped = new();

        private void Draw()
        {
            PrepareStyles();
            if (_banner != null) DrawBanner();
            if (_fullscreen != null) DrawFullscreen(_fullscreen);
        }

        // Sizes follow the Game view, so the placeholder reads like the device ad at any resolution.
        private float Unit => Mathf.Max(12f, Mathf.Min(Screen.width, Screen.height) / 34f);

        private void PrepareStyles()
        {
            if (_text != null && Mathf.Approximately(_styledFor, Unit)) return;
            _styledFor = Unit;
            var size = Mathf.RoundToInt(Unit);
            _text = new GUIStyle(GUI.skin.label) { fontSize = size, wordWrap = true, normal = { textColor = new Color(1, 1, 1, 0.8f) } };
            _title = new GUIStyle(_text) { fontStyle = FontStyle.Bold, fontSize = Mathf.RoundToInt(size * 1.15f), normal = { textColor = Color.white } };
            _button = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(size * 1.1f), fontStyle = FontStyle.Bold };
            _badge = new GUIStyle(GUI.skin.box) { fontSize = Mathf.RoundToInt(size * 0.8f), normal = { textColor = Color.black } };
            _badge.normal.background = Texture2D.whiteTexture;
        }

        private void DrawBanner()
        {
            var height = Mathf.Round(Unit * 3.2f);
            var y = _bannerPosition switch
            {
                // Screen.safeArea is bottom-up; IMGUI is top-down.
                BannerPositions.Top => Screen.height - Screen.safeArea.yMax,
                BannerPositions.Center => (Screen.height - height) / 2f,
                _ => Screen.height - Screen.safeArea.yMin - height
            };
            var rect = new Rect(0, y, Screen.width, height);

            Fill(rect, new Color(0.11f, 0.11f, 0.11f));
            var creative = _banner.Creative;
            if (_banner.Image != null)
                GUI.DrawTexture(rect, _banner.Image, ScaleMode.ScaleToFit);
            else
                DrawTexts(Inset(rect, Unit * 0.5f), creative, 1);

            Badge(new Vector2(rect.x + 4, rect.y + 4));
            if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
                PressBanner();
        }

        private void DrawFullscreen(Presentation p)
        {
            Fill(new Rect(0, 0, Screen.width, Screen.height), Color.black);

            var creative = p.Ad.Creative;
            var barHeight = Unit * 4.5f;
            var media = new Rect(0, 0, Screen.width, Screen.height - barHeight);
            if (p.Ad.Image != null)
                GUI.DrawTexture(media, p.Ad.Image, ScaleMode.ScaleToFit);
            if (p.Ad.IsVideo)
                GUI.Label(new Rect(media.x, media.center.y - Unit * 3, media.width, Unit * 2),
                    $"▶ Video ad (simulated {SimulatedVideoSeconds:0} s in the Editor)",
                    new GUIStyle(_title) { alignment = TextAnchor.MiddleCenter });
            if (GUI.Button(media, GUIContent.none, GUIStyle.none))
                PressCallToAction();

            var bar = new Rect(0, Screen.height - barHeight, Screen.width, barHeight);
            Fill(bar, new Color(0.07f, 0.07f, 0.07f));
            var cta = new Rect(bar.xMax - Unit * 7.5f, bar.y + (barHeight - Unit * 2.2f) / 2, Unit * 6.5f, Unit * 2.2f);
            DrawTexts(new Rect(bar.x + Unit, bar.y + Unit * 0.6f, cta.x - bar.x - Unit * 2, barHeight - Unit * 1.2f),
                creative, 2);
            if (GUI.Button(cta, Shape(string.IsNullOrEmpty(creative.CallToAction) ? "Open" : creative.CallToAction), _button))
                PressCallToAction();

            Badge(new Vector2(Unit, Unit), "Ad (Editor)");
            var size = Unit * 2.2f;
            var close = new Rect(Screen.width - size - Unit, Unit, size, size);
            if (p.Unlocked)
            {
                if (GUI.Button(close, "✕", _button)) PressClose();
            }
            else
            {
                var visible = Time.realtimeSinceStartup - p.StartedAt;
                var remaining = FullscreenLockPolicy.SecondsRemaining(p.Options, p.Ad.IsVideo, SimulatedVideoSeconds,
                    visible, Mathf.Min(visible, SimulatedVideoSeconds), visible >= SimulatedVideoSeconds, false);
                GUI.Box(close, Mathf.Max(1, remaining).ToString(), _button);
            }
        }

        private void DrawTexts(Rect area, AdCreative creative, int descriptionLines)
        {
            var rtl = IsRightToLeft(creative.Title) || IsRightToLeft(creative.Description);
            var title = new GUIStyle(_title) { alignment = rtl ? TextAnchor.UpperRight : TextAnchor.UpperLeft, wordWrap = false };
            var text = new GUIStyle(_text) { alignment = rtl ? TextAnchor.UpperRight : TextAnchor.UpperLeft };
            var titleHeight = title.fontSize * 1.4f;
            GUI.Label(new Rect(area.x, area.y, area.width, titleHeight), Shape(creative.Title), title);
            GUI.Label(new Rect(area.x, area.y + titleHeight, area.width, text.fontSize * 1.35f * descriptionLines),
                Shape(creative.Description), text);
        }

        private void Badge(Vector2 at, string label = "Ad")
        {
            var content = new GUIContent(label);
            var size = _badge.CalcSize(content);
            GUI.color = new Color(1f, 0.8f, 0.2f);
            GUI.Box(new Rect(at, size), content, _badge);
            GUI.color = Color.white;
        }

        private static void Fill(Rect rect, Color color)
        {
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = Color.white;
        }

        private static bool IsRightToLeft(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            foreach (var c in text)
                if (RTLTMPro.TextUtils.IsRTLCharacter(c)) return true;
            return false;
        }

        // IMGUI draws characters as they come; Persian and Arabic need joining and reordering first.
        private string Shape(string text)
        {
            if (!IsRightToLeft(text)) return text ?? "";
            if (_shaped.TryGetValue(text, out var shaped)) return shaped;
            var output = new RTLTMPro.FastStringBuilder(RTLTMPro.RTLSupport.DefaultBufferSize);
            RTLTMPro.RTLSupport.FixRTL(text, output, farsi: true, fixTextTags: false);
            return _shaped[text] = output.ToString();
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
