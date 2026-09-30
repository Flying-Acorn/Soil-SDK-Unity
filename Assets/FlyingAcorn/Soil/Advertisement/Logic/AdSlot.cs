using System;
using System.Collections.Generic;
using System.Linq;

namespace FlyingAcorn.Soil.Advertisement.Logic
{
    public enum AdSlotNotice
    {
        Loaded,
        LoadFailed,
        Shown,
        ShowFailed,
        Clicked,
        Rewarded,
        Closed
    }

    /// <summary>
    /// The Unity-side state of one banner, interstitial or rewarded format, driving the native
    /// player's slot (NativeAds/PROTOCOL.md) and deciding which public events to raise.
    ///
    /// Rules:
    /// - Every successful preparation is announced once with Loaded; a LoadAd call on a prepared
    ///   slot is answered with Loaded again. LoadAd never goes unanswered: it gets Loaded or
    ///   LoadFailed, possibly later (while assets are still caching or the player is decoding).
    /// - A gated slot (rewarded cooldown) is not ready, and its Loaded waits for the gate to open.
    /// - A fullscreen show consumes the prepared ad; the same creative is prepared again right
    ///   after it closes, so the next LoadAd is answered at once. A banner can be shown again.
    /// </summary>
    public sealed class AdSlot
    {
        private readonly IAdPlayer _player;
        private readonly Func<bool> _isGated;
        private bool _loadRequested;
        private bool _announced;
        private bool _noFill;

        public AdSlot(string format, IAdPlayer player, Func<bool> isGated = null)
        {
            Format = format;
            IsFullscreen = AdFormats.IsFullscreen(format);
            _player = player;
            _isGated = isGated ?? (() => false);
        }

        public event Action<AdSlotNotice, string> Notice;

        public string Format { get; }
        public bool IsFullscreen { get; }
        public AdCreative Creative { get; private set; }

        /// <summary>What the player will actually play: video, image or text.</summary>
        public string Media { get; private set; }

        public bool IsCaching { get; private set; }
        public bool IsPreparing { get; private set; }
        public bool IsPrepared { get; private set; }

        /// <summary>From Show until closed or failed: the ad is on screen or about to be.</summary>
        public bool IsShowing { get; private set; }

        public bool IsReady => IsPrepared && !(IsFullscreen && IsShowing) && !_isGated();

        /// <summary>The format's assets are being (re)downloaded; LoadAd calls wait for them.</summary>
        public void BeginCaching()
        {
            IsCaching = true;
        }

        /// <summary>
        /// Takes the creative built from freshly cached assets, or null when the ad server had
        /// nothing for this format. An ad already on screen is not disturbed.
        /// </summary>
        public void SetCreative(AdCreative creative)
        {
            IsCaching = false;
            Creative = creative;
            _noFill = creative == null;
            if (creative != null)
            {
                Prepare();
                return;
            }

            IsPrepared = false;
            IsPreparing = false;
            _announced = false;
            Media = null;
            if (!IsShowing)
                _player.Destroy(Format);
            FailPendingLoad(NativeAdErrors.NoFill);
        }

        /// <summary>The caching round failed outright (e.g. no network); pending LoadAd calls fail.</summary>
        public void CachingFailed(string error)
        {
            IsCaching = false;
            if (Creative == null)
                FailPendingLoad(error);
            else if (!IsPrepared && !IsPreparing && !IsShowing)
                Prepare();
        }

        public void RequestLoad()
        {
            _loadRequested = true;
            if (IsPrepared || IsPreparing || IsCaching || IsShowing)
            {
                Tick();
                return;
            }

            if (Creative != null)
            {
                Prepare();
                return;
            }

            FailPendingLoad(_noFill ? NativeAdErrors.NoFill : NativeAdErrors.NotLoaded);
        }

        /// <summary>Called every frame: delivers a Loaded that was waiting for the gate.</summary>
        public void Tick()
        {
            if (!IsReady) return;
            if (_announced && !_loadRequested) return;

            _announced = true;
            _loadRequested = false;
            Raise(AdSlotNotice.Loaded);
        }

        /// <summary>Asks the player to show the prepared ad. Failures are raised as ShowFailed.</summary>
        public void Show(string optionsJson)
        {
            if (IsFullscreen && IsShowing)
            {
                Raise(AdSlotNotice.ShowFailed, NativeAdErrors.AlreadyShowing);
                return;
            }

            if (!IsPrepared)
            {
                Raise(AdSlotNotice.ShowFailed, NativeAdErrors.NotLoaded);
                return;
            }

            IsShowing = true;
            if (IsFullscreen)
            {
                IsPrepared = false;
                _announced = false;
            }

            _player.Show(Format, optionsJson);
        }

        public void Hide()
        {
            _player.Hide(Format);
        }

        public void HandleEvent(NativeAdEvent e)
        {
            if (e == null) return;
            switch (e.Type)
            {
                case NativeAdEventType.Loaded:
                    // Only the latest load reports; anything else is stale.
                    if (!IsPreparing) return;
                    IsPreparing = false;
                    IsPrepared = true;
                    Media = e.Media;
                    Tick();
                    break;

                case NativeAdEventType.LoadFailed:
                    if (!IsPreparing) return;
                    IsPreparing = false;
                    IsPrepared = false;
                    _loadRequested = false;
                    Raise(AdSlotNotice.LoadFailed, e.Error ?? NativeAdErrors.Internal);
                    break;

                case NativeAdEventType.Shown:
                    Raise(AdSlotNotice.Shown);
                    break;

                case NativeAdEventType.ShowFailed:
                    IsShowing = false;
                    Raise(AdSlotNotice.ShowFailed, e.Error ?? NativeAdErrors.Internal);
                    PrepareAgainIfConsumed();
                    break;

                case NativeAdEventType.Clicked:
                    Raise(AdSlotNotice.Clicked);
                    break;

                case NativeAdEventType.Rewarded:
                    Raise(AdSlotNotice.Rewarded);
                    break;

                case NativeAdEventType.Closed:
                    IsShowing = false;
                    Raise(AdSlotNotice.Closed);
                    PrepareAgainIfConsumed();
                    Tick();
                    break;
            }
        }

        private void PrepareAgainIfConsumed()
        {
            if (IsFullscreen && Creative != null && !IsPrepared && !IsPreparing)
                Prepare();
        }

        private void Prepare()
        {
            IsPrepared = false;
            IsPreparing = true;
            _announced = false;
            Media = null;
            _player.Load(Format, Creative.ToJson());
        }

        private void FailPendingLoad(string error)
        {
            if (!_loadRequested) return;
            _loadRequested = false;
            Raise(AdSlotNotice.LoadFailed, error);
        }

        private void Raise(AdSlotNotice notice, string error = null)
        {
            Notice?.Invoke(notice, error);
        }
    }

    /// <summary>
    /// The three player formats together: routes player events to their slot and keeps a
    /// single fullscreen ad on screen at a time.
    /// </summary>
    public sealed class AdSlots
    {
        private readonly Dictionary<string, AdSlot> _slots = new();

        public AdSlots(IAdPlayer player, Func<bool> isRewardedGated = null)
        {
            _slots[AdFormats.Banner] = new AdSlot(AdFormats.Banner, player);
            _slots[AdFormats.Interstitial] = new AdSlot(AdFormats.Interstitial, player);
            _slots[AdFormats.Rewarded] = new AdSlot(AdFormats.Rewarded, player, isRewardedGated);
        }

        public AdSlot this[string format] => _slots.TryGetValue(format ?? string.Empty, out var slot) ? slot : null;

        public IEnumerable<AdSlot> All => _slots.Values;

        public bool IsFullscreenShowing => _slots.Values.Any(s => s.IsFullscreen && s.IsShowing);

        public void Show(string format, string optionsJson)
        {
            var slot = this[format];
            if (slot == null) return;

            // One fullscreen ad at a time, across formats.
            if (slot.IsFullscreen && IsFullscreenShowing && !slot.IsShowing)
            {
                slot.HandleEvent(new NativeAdEvent
                {
                    Format = format, Type = NativeAdEventType.ShowFailed, Error = NativeAdErrors.AlreadyShowing
                });
                return;
            }

            slot.Show(optionsJson);
        }

        public void HandleEvent(NativeAdEvent e)
        {
            if (e == null) return;
            this[e.Format]?.HandleEvent(e);
        }

        public void Tick()
        {
            foreach (var slot in _slots.Values)
                slot.Tick();
        }
    }
}
