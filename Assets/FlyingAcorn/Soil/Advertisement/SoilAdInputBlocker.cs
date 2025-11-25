using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FlyingAcorn.Soil.Advertisement
{
    /// <summary>
    /// Manages input blocking state while an ad is shown.
    /// - Provides IsBlocked property that game code can check to disable gameplay input
    /// - Disables PlayerInput components (New Input System) to prevent gameplay input
    /// - Does NOT create overlays/shields that would block ad clicks
    /// - Does NOT modify Time.timeScale - game code should handle pausing
    /// </summary>
    /// <remarks>
    /// <para><b>Game Pause Handling:</b></para>
    /// <para>Game code should handle pausing by subscribing to ad events:</para>
    /// <code>
    /// Events.OnInterstitialAdShown += (data) => PauseGame();
    /// Events.OnInterstitialAdClosed += (data) => ResumeGame();
    /// Events.OnRewardedAdShown += (data) => PauseGame();
    /// Events.OnRewardedAdClosed += (data) => ResumeGame();
    /// </code>
    /// <para>Or check IsBlocked in your game loop:</para>
    /// <code>
    /// void Update() {
    ///     if (SoilAdInputBlocker.IsBlocked) return;
    ///     // Normal gameplay code
    /// }
    /// </code>
    /// </remarks>
    public static class SoilAdInputBlocker
    {

        private static int _depth;

        // Failsafe timeout to automatically unblock if something goes wrong (seconds)
        private const float FailsafeTimeoutSeconds = 40f;
        private static float _lastBlockRealtime;

#if ENABLE_INPUT_SYSTEM
        private static readonly List<PlayerInput> _disabledPlayerInputs = new List<PlayerInput>();
#endif

        /// <summary>
        /// Returns true if an ad is currently being shown and input should be blocked.
        /// Game code should check this property before processing gameplay input.
        /// </summary>
        public static bool IsBlocked => _depth > 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void InitializeOnLoad()
        {
            // Ensure clean state between domain reloads
            _depth = 0;
            _lastBlockRealtime = 0f;
#if ENABLE_INPUT_SYSTEM
            _disabledPlayerInputs.Clear();
#endif
        }

        public static void Block(Canvas adCanvas = null)
        {
            _depth++;
            if (_depth > 1)
                return;

            _lastBlockRealtime = Time.unscaledTime;

            // Don't create overlays or shields - they block ad clicks
            // Game code should check IsBlocked or subscribe to ad events for pause logic

#if ENABLE_INPUT_SYSTEM
            // Disable PlayerInput components to block gameplay input
            // This doesn't affect EventSystem or UI Button clicks
#if UNITY_2023_1_OR_NEWER
            var playerInputs = Object.FindObjectsByType<PlayerInput>(FindObjectsSortMode.None);
#else
            var playerInputs = Object.FindObjectsOfType<PlayerInput>();
#endif
            foreach (var pi in playerInputs)
            {
                if (pi && pi.enabled)
                {
                    pi.enabled = false;
                    _disabledPlayerInputs.Add(pi);
                }
            }
#endif
        }

        public static void Unblock()
        {
            if (_depth == 0)
            {
                // Already unblocked - ensure timer is reset even on redundant calls
                _lastBlockRealtime = 0f;
                return;
            }

            _depth--;
            if (_depth > 0)
                return;

#if ENABLE_INPUT_SYSTEM
            foreach (var pi in _disabledPlayerInputs)
            {
                if (pi)
                    pi.enabled = true;
            }
            _disabledPlayerInputs.Clear();
#endif

            // Reset failsafe timer when fully unblocked
            _lastBlockRealtime = 0f;
        }

        /// <summary>
        /// Emergency method to forcefully clear all input blocking state.
        /// Use sparingly from higher-level error handlers if an ad fails to close properly.
        /// </summary>
        public static void ForceUnblock()
        {
            _depth = 1; // ensure Unblock() logic runs
            Unblock();
        }

        /// <summary>
        /// Call periodically (e.g., from an Update in a manager) to enforce a failsafe timeout.
        /// If an ad has been blocking input for longer than FailsafeTimeoutSeconds, this will
        /// automatically clear the blocker so the game cannot remain frozen indefinitely.
        /// </summary>
        public static void FailsafeTick()
        {
            if (_depth <= 0)
                return;

            if (_lastBlockRealtime <= 0f)
                return;

            if (Time.unscaledTime - _lastBlockRealtime > FailsafeTimeoutSeconds)
            {
                // Silently force unblock - failsafe triggered
                ForceUnblock();
            }
        }
    }
}
