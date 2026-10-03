#if UNITY_EDITOR
using System;
using UnityEngine;

namespace FlyingAcorn.Soil.Advertisement.Player
{
    /// <summary>
    /// The IMGUI hook of the Editor's simulated player. Added by <see cref="EditorAdPlayer"/> only,
    /// so device builds never pay for an OnGUI every frame.
    /// </summary>
    [AddComponentMenu("")]
    internal sealed class EditorAdPlayerView : MonoBehaviour
    {
        internal event Action Gui;

        private void OnGUI()
        {
            Gui?.Invoke();
        }
    }
}
#endif
