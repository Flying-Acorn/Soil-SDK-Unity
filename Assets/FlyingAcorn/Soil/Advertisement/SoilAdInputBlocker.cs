using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif

namespace FlyingAcorn.Soil.Advertisement
{
    /// <summary>
    /// Blocks gameplay input while an ad is shown.
    /// - Disables PlayerInput components (new Input System) during block.
    /// - Adds a transparent UI overlay to capture pointer raycasts so EventSystem reports pointer over UI.
    /// - Optionally pauses gameplay by setting Time.timeScale to 0 (see <see cref="PauseGameplayDuringAds"/>).
    /// </summary>
    /// <remarks>
    /// <para><b>IMPORTANT - Time.timeScale Behavior:</b></para>
    /// <para>When <see cref="PauseGameplayDuringAds"/> is enabled, this SDK modifies the global <see cref="Time.timeScale"/>
    /// to pause gameplay during ads. This affects ALL game systems that depend on scaled time, including:</para>
    /// <list type="bullet">
    /// <item>Physics simulation (Rigidbody, forces, velocities)</item>
    /// <item>Animations using Animator or Animation components</item>
    /// <item>Particle systems</item>
    /// <item>Coroutines using WaitForSeconds</item>
    /// <item>Time.deltaTime-based movement and logic</item>
    /// </list>
    /// <para>If your game has its own pause system or depends on specific Time.timeScale values,
    /// set <see cref="PauseGameplayDuringAds"/> to false and handle pausing in your own ad event callbacks
    /// (e.g., OnInterstitialAdShown, OnRewardedAdShown).</para>
    /// </remarks>
    public static class SoilAdInputBlocker
    {
        /// <summary>
        /// When true, sets Time.timeScale to 0 during ad display to pause gameplay.
        /// When false, only blocks input without affecting time scale.
        /// Default is true for backward compatibility.
        /// </summary>
        /// <remarks>
        /// Set this to false if your game manages its own pause logic or if you need more control
        /// over what systems pause during ads. You can then subscribe to ad events and implement
        /// custom pause behavior.
        /// </remarks>
        internal static bool PauseGameplayDuringAds = true;

        private static int _depth;
        private static GameObject _overlay;
        private static readonly System.Collections.Generic.List<GameObject> _physicsShields = new System.Collections.Generic.List<GameObject>();
        private static float _prevTimeScale = 1f;
        private static bool _pausedTime;

#if ENABLE_INPUT_SYSTEM
        private static readonly List<PlayerInput> _disabledPlayerInputs = new List<PlayerInput>();
#endif

        internal static bool IsBlocked => _depth > 0;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void InitializeOnLoad()
        {
            // Ensure clean state between domain reloads
            _depth = 0;
            _overlay = null;
            _prevTimeScale = 1f;
            _pausedTime = false;
            PauseGameplayDuringAds = true; // Reset to default on domain reload
#if ENABLE_INPUT_SYSTEM
            _disabledPlayerInputs.Clear();
#endif
        }

        public static void Block(Canvas adCanvas = null)
        {
            _depth++;
            if (_depth > 1)
                return;

            TryEnsureOverlay(adCanvas);
            TryEnsurePhysicsShields();

            // Optionally pause game time so gameplay systems based on deltaTime/physics stop
            if (PauseGameplayDuringAds)
            {
                _prevTimeScale = Time.timeScale;
                Time.timeScale = 0f;
                _pausedTime = true;
            }

#if ENABLE_INPUT_SYSTEM
            _disabledPlayerInputs.Clear();
#if UNITY_2023_1_OR_NEWER
            var inputs = Object.FindObjectsByType<PlayerInput>(FindObjectsSortMode.None);
#else
            var inputs = Object.FindObjectsOfType<PlayerInput>();
#endif
            foreach (var pi in inputs)
            {
                if (pi.enabled)
                {
                    _disabledPlayerInputs.Add(pi);
                    pi.enabled = false;
                }
            }
#endif
        }

        public static void Unblock()
        {
            if (_depth == 0)
            {
                // Extra safety: detect and warn about misuse in development
                Debug.LogWarning("SoilAdInputBlocker.Unblock called but no active blocks are present (depth == 0). Ignoring.");
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

            if (_overlay)
            {
                Object.Destroy(_overlay);
                _overlay = null;
            }

            // Remove physics shields
            for (int i = 0; i < _physicsShields.Count; i++)
            {
                var go = _physicsShields[i];
                if (go)
                    Object.Destroy(go);
            }
            _physicsShields.Clear();

            // Restore time scale last so event listeners run with normal time
            if (_pausedTime)
            {
                Time.timeScale = _prevTimeScale <= 0f ? 1f : _prevTimeScale;
                _pausedTime = false;
            }
        }

        private static void TryEnsureOverlay(Canvas adCanvas)
        {
            if (_overlay)
                return;

            Canvas targetCanvas = adCanvas;
            if (!targetCanvas)
            {
#if UNITY_2023_1_OR_NEWER
                targetCanvas = Object.FindFirstObjectByType<Canvas>();
#else
                targetCanvas = Object.FindObjectOfType<Canvas>();
#endif
                if (!targetCanvas)
                    return;
            }

            _overlay = new GameObject("AdInputBlockerOverlay",
                typeof(RectTransform),
                typeof(CanvasRenderer),
                typeof(Image));
            _overlay.transform.SetParent(targetCanvas.transform, false);

            var rt = (RectTransform)_overlay.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            rt.SetAsFirstSibling(); // keep behind ad visuals but still raycastable

            var img = _overlay.GetComponent<Image>();
            img.color = new Color(0, 0, 0, 0);   // fully transparent
            img.raycastTarget = true;            // capture pointer so it's considered over UI
        }

        private static void TryEnsurePhysicsShields()
        {
            // Create a near-camera collider per active camera to block 3D/2D raycasts into gameplay
            var cameras = Camera.allCameras;
            for (int i = 0; i < cameras.Length; i++)
            {
                var cam = cameras[i];
                if (!cam || !cam.enabled)
                    continue;

                // Avoid duplicating on repeated calls
                var existing = cam.transform.Find("AdInputBlockerShield");
                if (existing)
                {
                    _physicsShields.Add(existing.gameObject);
                    continue;
                }

                var shield = new GameObject("AdInputBlockerShield");
                shield.layer = LayerMask.NameToLayer("Default");
                shield.transform.SetParent(cam.transform, false);

                // Place just in front of the near clip plane
                float z = Mathf.Max(0.05f, cam.nearClipPlane + 0.05f);
                shield.transform.localPosition = new Vector3(0, 0, z);
                shield.transform.localRotation = Quaternion.identity;

                float width, height, depth = 0.1f;
                if (cam.orthographic)
                {
                    height = cam.orthographicSize * 2f;
                    width = height * cam.aspect;

                    var col2D = shield.AddComponent<BoxCollider2D>();
                    col2D.size = new Vector2(width, height);
                    // Use trigger to avoid physical forces while still blocking raycasts
                    col2D.isTrigger = true;
                }
                else
                {
                    float dist = z;
                    height = 2f * dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
                    width = height * cam.aspect;

                    var col = shield.AddComponent<BoxCollider>();
                    col.size = new Vector3(width, height, depth);
                    // Use trigger to avoid physical forces while still blocking raycasts
                    col.isTrigger = true;
                }

                _physicsShields.Add(shield);
            }
        }
    }
}
