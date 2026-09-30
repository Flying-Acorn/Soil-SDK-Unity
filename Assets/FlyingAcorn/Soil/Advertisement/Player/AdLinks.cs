using FlyingAcorn.Soil.Advertisement.Logic;
using UnityEngine;

namespace FlyingAcorn.Soil.Advertisement.Player
{
    /// <summary>
    /// Opens an ad's click link. An ad's link is usually the advertiser's click tracker, so in the
    /// Editor it is only logged: a test click must never count as a real one. Click events are
    /// raised either way, so game logic can still be exercised. Only web and app store links are
    /// opened (<see cref="AdLinkPolicy"/>).
    /// </summary>
    internal static class AdLinks
    {
        internal const string EditorNotice = "[Advertisement] Editor: ad click not sent, so it is not counted:";

        internal static void Open(string url)
        {
            url = AdLinkPolicy.Sanitize(url);
            if (url == null) return;
#if UNITY_EDITOR
            // Plain Debug.Log: MyDebug would forward this as an analytics event.
            Debug.Log($"{EditorNotice} {url}");
#else
            Application.OpenURL(url);
#endif
        }
    }
}
