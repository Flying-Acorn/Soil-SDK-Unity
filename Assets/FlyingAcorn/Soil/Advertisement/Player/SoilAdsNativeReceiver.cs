using System;
using UnityEngine;
using UnityEngine.Scripting;

namespace FlyingAcorn.Soil.Advertisement.Player
{
    /// <summary>
    /// The hidden, scene-independent object the native players send their events to
    /// (UnitySendMessage), and the frame tick of the ad runtime. Its name is part of the
    /// contract with the players, so it must stay unique.
    /// </summary>
    [AddComponentMenu("")]
    internal sealed class SoilAdsNativeReceiver : MonoBehaviour
    {
        internal const string ObjectName = "SoilAdsNativeReceiver";
        internal const string MethodName = nameof(OnNativeAdEvent);

        internal event Action<string> MessageReceived;
        internal event Action Ticked;
        internal event Action Gui;

        private static SoilAdsNativeReceiver _instance;

        internal static SoilAdsNativeReceiver GetOrCreate()
        {
            if (_instance) return _instance;

            var go = new GameObject(ObjectName) { hideFlags = HideFlags.HideInHierarchy };
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<SoilAdsNativeReceiver>();
            return _instance;
        }

        /// <summary>Called by the native players with one event JSON (by name, so kept from stripping).</summary>
        [Preserve]
        public void OnNativeAdEvent(string json)
        {
            MessageReceived?.Invoke(json);
        }

        private void Update()
        {
            Ticked?.Invoke();
        }

        private void OnGUI()
        {
            Gui?.Invoke();
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }
    }
}
