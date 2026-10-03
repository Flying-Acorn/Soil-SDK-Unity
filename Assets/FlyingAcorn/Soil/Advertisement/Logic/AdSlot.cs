using System;
using System.Collections.Generic;
using System.Diagnostics;

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
    ///   after it closes, so the next LoadAd is answered at once.
    /// - A banner stays ready while it is on screen (showing it again only moves it) and, as the
    ///   SDK always did, is used up when it closes: it is prepared again and announced with a new
    ///   Loaded, which is what games wait for before showing a banner again.
    /// - A load the player never answers fails with <see cref="NativeAdErrors.Timeout"/> after
    ///   <see cref="PrepareTimeoutSeconds"/>; its answer, if it still comes, is ignored.
    /// - A fullscreen show the player never answers at all (no shown, showFailed or closed) fails
    ///   the same way after <see cref="ShowAnswerTimeoutSeconds"/>, so the slot cannot stay
    ///   "showing" forever. An ad that did appear is never timed out: the game is paused under it.
    /// - A notice handler that throws is reported and skipped; the slot's own state changes
    ///   always complete.
    /// </summary>
    public sealed class AdSlot
    {
        /// <summary>How long the player may take to answer a load.</summary>
        public const double PrepareTimeoutSeconds = 60;

        /// <summary>How long the player may take to answer a fullscreen show in any way.</summary>
        public const double ShowAnswerTimeoutSeconds = 30;

        private readonly IAdPlayer _player;
        private readonly Func<bool> _isGated;
        private readonly Func<double> _now;
        private readonly Action<Exception> _onHandlerError;
        private bool _loadRequested;
        private bool _announced;
        private bool _noFill;
        private double _prepareStartedAt;
        private bool _awaitingShowAnswer;
        private double _showStartedAt;
        // A fullscreen show given up on by the watchdog: whatever it still reports is not raised.
        private bool _showAbandoned;

        /// <param name="format">The player format this slot drives.</param>
        /// <param name="player">The native player.</param>
        /// <param name="isGated">While true the slot is not ready and holds Loaded back.</param>
        /// <param name="clock">
        /// Seconds on a monotonic clock that only needs to advance while the game runs; drives the
        /// watchdogs. Defaults to real time.
        /// </param>
        /// <param name="onHandlerError">Receives exceptions thrown by <see cref="Notice"/> handlers.</param>
        public AdSlot(string format, IAdPlayer player, Func<bool> isGated = null, Func<double> clock = null,
            Action<Exception> onHandlerError = null)
        {
            Format = format;
            IsFullscreen = AdFormats.IsFullscreen(format);
            _player = player;
            _isGated = isGated ?? (() => false);
            _now = clock ?? RealTime;
            _onHandlerError = onHandlerError;
        }

        private static readonly Stopwatch Clock = Stopwatch.StartNew();
        private static double RealTime() => Clock.Elapsed.TotalSeconds;

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
            // Nothing to load and nothing on its way: answer now, even while an ad is on screen
            // (e.g. a banner whose files were removed meanwhile), or the call would never be answered.
            if (Creative == null && !IsCaching && !IsPrepared && !IsPreparing)
            {
                FailPendingLoad(_noFill ? NativeAdErrors.NoFill : NativeAdErrors.NotLoaded);
                return;
            }

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

        /// <summary>
        /// Called every frame: runs the watchdogs and delivers a Loaded that was waiting for the gate.
        /// </summary>
        public void Tick()
        {
            CheckWatchdogs();
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
                _awaitingShowAnswer = true;
                _showAbandoned = false;
                _showStartedAt = _now();
            }

            _player.Show(Format, optionsJson);
        }

        public void Hide()
        {
            _player.Hide(Format);
        }

        /// <summary>Answers a show that was refused before reaching the player; the slot is unchanged.</summary>
        public void RejectShow(string error)
        {
            Raise(AdSlotNotice.ShowFailed, error);
        }

        public void HandleEvent(NativeAdEvent e)
        {
            if (e == null) return;
            if (IsFullscreen && IsAnswerToAbandonedShow(e)) return;

            switch (e.Type)
            {
                case NativeAdEventType.Loaded:
                    // Only the latest load reports; anything else is stale (including the answer
                    // to a load the watchdog already failed).
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
                    _awaitingShowAnswer = false;
                    Raise(AdSlotNotice.Shown);
                    break;

                case NativeAdEventType.ShowFailed:
                    _awaitingShowAnswer = false;
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
                    _awaitingShowAnswer = false;
                    IsShowing = false;
                    if (!IsFullscreen)
                    {
                        IsPrepared = false;
                        _announced = false;
                    }
                    Raise(AdSlotNotice.Closed);
                    PrepareAgainIfConsumed();
                    Tick();
                    break;
            }
        }

        /// <summary>
        /// Swallows what a show the watchdog gave up on still reports. If it did appear after all,
        /// it is taken down again: the game was already told it failed.
        /// </summary>
        private bool IsAnswerToAbandonedShow(NativeAdEvent e)
        {
            if (!_showAbandoned) return false;
            switch (e.Type)
            {
                case NativeAdEventType.Shown:
                    _player.Hide(Format);
                    return true;
                case NativeAdEventType.Clicked:
                case NativeAdEventType.Rewarded:
                    return true;
                case NativeAdEventType.ShowFailed:
                case NativeAdEventType.Closed:
                    _showAbandoned = false;
                    return true;
                default:
                    return false;
            }
        }

        private void CheckWatchdogs()
        {
            if (IsPreparing && _now() - _prepareStartedAt >= PrepareTimeoutSeconds)
            {
                IsPreparing = false;
                IsPrepared = false;
                _loadRequested = false;
                Media = null;
                // Cancels the decode that never answered (an ad on screen is left alone); a later
                // load of this slot starts from scratch.
                if (!IsShowing)
                    _player.Destroy(Format);
                Raise(AdSlotNotice.LoadFailed, NativeAdErrors.Timeout);
            }

            if (_awaitingShowAnswer && _now() - _showStartedAt >= ShowAnswerTimeoutSeconds)
            {
                _awaitingShowAnswer = false;
                _showAbandoned = true;
                IsShowing = false;
                _player.Hide(Format);
                Raise(AdSlotNotice.ShowFailed, NativeAdErrors.Timeout);
                PrepareAgainIfConsumed();
            }
        }

        private void PrepareAgainIfConsumed()
        {
            if (Creative != null && !IsPrepared && !IsPreparing)
                Prepare();
        }

        private void Prepare()
        {
            IsPrepared = false;
            IsPreparing = true;
            _announced = false;
            Media = null;
            _prepareStartedAt = _now();
            _player.Load(Format, Creative.ToJson());
        }

        private void FailPendingLoad(string error)
        {
            if (!_loadRequested) return;
            _loadRequested = false;
            Raise(AdSlotNotice.LoadFailed, error);
        }

        // Each handler runs on its own: one that throws neither stops the others nor the slot's
        // own bookkeeping after the notice.
        private void Raise(AdSlotNotice notice, string error = null)
        {
            var handlers = Notice;
            if (handlers == null) return;
            foreach (var handler in handlers.GetInvocationList())
            {
                try
                {
                    ((Action<AdSlotNotice, string>)handler)(notice, error);
                }
                catch (Exception ex)
                {
                    try { _onHandlerError?.Invoke(ex); } catch { /* the reporter must not break the slot either */ }
                }
            }
        }
    }

    /// <summary>
    /// The three player formats together: routes player events to their slot and keeps a
    /// single fullscreen ad on screen at a time.
    /// </summary>
    public sealed class AdSlots
    {
        private readonly Dictionary<string, AdSlot> _slots = new();

        /// <param name="player">The native player.</param>
        /// <param name="isRewardedGated">The rewarded cooldown.</param>
        /// <param name="clock">See <see cref="AdSlot(string, IAdPlayer, Func{bool}, Func{double}, Action{Exception})"/>.</param>
        /// <param name="onHandlerError">Receives exceptions thrown by notice handlers.</param>
        public AdSlots(IAdPlayer player, Func<bool> isRewardedGated = null, Func<double> clock = null,
            Action<Exception> onHandlerError = null)
        {
            _slots[AdFormats.Banner] = new AdSlot(AdFormats.Banner, player, null, clock, onHandlerError);
            _slots[AdFormats.Interstitial] = new AdSlot(AdFormats.Interstitial, player, null, clock, onHandlerError);
            _slots[AdFormats.Rewarded] = new AdSlot(AdFormats.Rewarded, player, isRewardedGated, clock, onHandlerError);
        }

        public AdSlot this[string format] => _slots.TryGetValue(format ?? string.Empty, out var slot) ? slot : null;

        public IEnumerable<AdSlot> All => _slots.Values;

        public bool IsFullscreenShowing
        {
            get
            {
                // Polled every frame by games (SoilAdInputBlocker.IsBlocked): no LINQ, no allocation.
                foreach (var slot in _slots.Values)
                    if (slot.IsFullscreen && slot.IsShowing)
                        return true;
                return false;
            }
        }

        public void Show(string format, string optionsJson)
        {
            var slot = this[format];
            if (slot == null) return;

            // One fullscreen ad at a time, across formats.
            if (slot.IsFullscreen && IsFullscreenShowing && !slot.IsShowing)
            {
                slot.RejectShow(NativeAdErrors.AlreadyShowing);
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
