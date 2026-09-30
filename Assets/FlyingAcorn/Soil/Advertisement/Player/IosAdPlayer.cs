using System;
using System.Runtime.InteropServices;
using FlyingAcorn.Analytics;
using FlyingAcorn.Soil.Advertisement.Logic;

namespace FlyingAcorn.Soil.Advertisement.Player
{
    /// <summary>
    /// Drives the iOS player (Plugins/iOS/SoilAds). Calls are fire-and-forget; results arrive at
    /// <see cref="SoilAdsNativeReceiver"/>. A call that cannot reach the player is reported back
    /// as a failure so no request goes unanswered.
    /// </summary>
    internal sealed class IosAdPlayer : IAdPlayer
    {
        [DllImport("__Internal")] private static extern void SoilAds_Initialize(string receiverObject, string receiverMethod);
        [DllImport("__Internal")] private static extern void SoilAds_Load(string format, string creativeJson);
        [DllImport("__Internal")] private static extern void SoilAds_Show(string format, string optionsJson);
        [DllImport("__Internal")] private static extern void SoilAds_Hide(string format);
        [DllImport("__Internal")] private static extern void SoilAds_Destroy(string format);

        private readonly Action<NativeAdEvent> _report;
        private readonly bool _available;

        public IosAdPlayer(string receiverObject, string receiverMethod, Action<NativeAdEvent> report)
        {
            _report = report;
            try
            {
                SoilAds_Initialize(receiverObject, receiverMethod);
                _available = true;
            }
            catch (Exception e)
            {
                MyDebug.LogWarning($"[Advertisement] iOS ad player unavailable: {e.Message}");
            }
        }

        public void Load(string format, string creativeJson) =>
            Call(() => SoilAds_Load(format, creativeJson), format, NativeAdEventType.LoadFailed);

        public void Show(string format, string optionsJson) =>
            Call(() => SoilAds_Show(format, optionsJson), format, NativeAdEventType.ShowFailed);

        public void Hide(string format) => Call(() => SoilAds_Hide(format), format, NativeAdEventType.Unknown);

        public void Destroy(string format) => Call(() => SoilAds_Destroy(format), format, NativeAdEventType.Unknown);

        private void Call(Action call, string format, NativeAdEventType failure)
        {
            try
            {
                if (!_available) throw new InvalidOperationException("player not available");
                call();
            }
            catch (Exception e)
            {
                MyDebug.LogWarning($"[Advertisement] iOS player call for {format} failed: {e.Message}");
                if (failure != NativeAdEventType.Unknown)
                    _report(new NativeAdEvent
                    {
                        Format = format, Type = failure, Error = NativeAdErrors.Internal, Message = e.Message
                    });
            }
        }
    }
}
