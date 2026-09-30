using System;
using FlyingAcorn.Soil.Advertisement.Logic;

namespace FlyingAcorn.Soil.Advertisement.Player
{
    /// <summary>
    /// The player of platforms without a native one (desktop, WebGL, consoles): it has no ads, so
    /// every load is answered with no fill and every show fails as not loaded. Answers arrive on
    /// the next frame through <c>report</c>, like a real player's.
    /// </summary>
    internal sealed class NullAdPlayer : IAdPlayer
    {
        private readonly Action<NativeAdEvent> _report;

        public NullAdPlayer(Action<NativeAdEvent> report)
        {
            _report = report;
        }

        public void Load(string format, string creativeJson) =>
            _report(new NativeAdEvent
            {
                Format = format, Type = NativeAdEventType.LoadFailed, Error = NativeAdErrors.NoFill,
                Message = "no ad player on this platform"
            });

        public void Show(string format, string optionsJson) =>
            _report(new NativeAdEvent
            {
                Format = format, Type = NativeAdEventType.ShowFailed, Error = NativeAdErrors.NotLoaded,
                Message = "no ad player on this platform"
            });

        public void Hide(string format)
        {
        }

        public void Destroy(string format)
        {
        }
    }
}
