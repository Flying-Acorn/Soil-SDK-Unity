using System;
using FlyingAcorn.Analytics;
using FlyingAcorn.Soil.Advertisement.Logic;
using UnityEngine;

namespace FlyingAcorn.Soil.Advertisement.Player
{
    /// <summary>
    /// Drives the Android player (Plugins/Android/SoilAds.androidlib). Calls are fire-and-forget;
    /// results arrive at <see cref="SoilAdsNativeReceiver"/>. A call that cannot even reach Java
    /// is reported back as a failure so no request goes unanswered.
    /// </summary>
    internal sealed class AndroidAdPlayer : IAdPlayer
    {
        private const string BridgeClass = "com.flyingacorn.soil.ads.SoilAdsBridge";

        private readonly Action<NativeAdEvent> _report;
        private readonly AndroidJavaClass _bridge;

        public AndroidAdPlayer(string receiverObject, string receiverMethod, Action<NativeAdEvent> report)
        {
            _report = report;
            try
            {
                _bridge = new AndroidJavaClass(BridgeClass);
                using var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
                using var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity");
                _bridge.CallStatic("initialize", activity, receiverObject, receiverMethod);
            }
            catch (Exception e)
            {
                MyDebug.LogWarning($"[Advertisement] Android ad player unavailable: {e.Message}");
                _bridge = null;
            }
        }

        public void Load(string format, string creativeJson) =>
            Call("load", format, NativeAdEventType.LoadFailed, creativeJson);

        public void Show(string format, string optionsJson) =>
            Call("show", format, NativeAdEventType.ShowFailed, optionsJson);

        public void Hide(string format) => Call("hide", format, NativeAdEventType.Unknown);

        public void Destroy(string format) => Call("destroy", format, NativeAdEventType.Unknown);

        private void Call(string method, string format, NativeAdEventType failure, string json = null)
        {
            try
            {
                if (_bridge == null) throw new InvalidOperationException("player not available");
                if (json == null) _bridge.CallStatic(method, format);
                else _bridge.CallStatic(method, format, json);
            }
            catch (Exception e)
            {
                MyDebug.LogWarning($"[Advertisement] Android player {method}({format}) failed: {e.Message}");
                if (failure != NativeAdEventType.Unknown)
                    _report(new NativeAdEvent
                    {
                        Format = format, Type = failure, Error = NativeAdErrors.Internal, Message = e.Message
                    });
            }
        }
    }
}
