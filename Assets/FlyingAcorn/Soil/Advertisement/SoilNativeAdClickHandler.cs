using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace FlyingAcorn.Soil.Advertisement
{
    /// <summary>
    /// Attached by <see cref="Advertisement.ShowNativeAd"/> to each GameObject the game registered
    /// for a native ad, so a tap anywhere on the ad is attributed and opens the click URL. uGUI
    /// gives a click only to the nearest click handler above the tapped object, so one is also
    /// attached next to every Button, Toggle or other click handler inside a registered
    /// GameObject; each tap still reaches exactly one of them.
    ///
    /// The component is added at show time and removed on hide/destroy rather than living on the
    /// game's prefabs, so the game keeps full ownership of its native ad layout.
    /// </summary>
    [DisallowMultipleComponent]
    public class SoilNativeAdClickHandler : MonoBehaviour, IPointerClickHandler
    {
        private Action _onClick;

        internal void Bind(Action onClick)
        {
            _onClick = onClick;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            _onClick?.Invoke();
        }

        private void OnDisable()
        {
            // A disabled view must not keep firing clicks against a stale ad.
            _onClick = null;
        }
    }
}
