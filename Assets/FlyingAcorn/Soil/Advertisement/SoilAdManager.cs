using FlyingAcorn.Analytics;
using UnityEngine;

namespace FlyingAcorn.Soil.Advertisement
{
    /// <summary>
    /// Scene anchor of Soil ads. Optional: <see cref="Advertisement.InitializeAsync"/> creates one
    /// when the scene has none. Ads are drawn by native players, so it holds no placements; it
    /// stays as a persistent MonoBehaviour games can use as a coroutine host.
    /// </summary>
    public class SoilAdManager : MonoBehaviour
    {
        public static SoilAdManager Instance { get; private set; }

        internal static SoilAdManager GetOrCreate()
        {
            if (Instance) return Instance;
            var existing = FindFirstObjectByType<SoilAdManager>();
            if (existing) return existing;
            return new GameObject(nameof(SoilAdManager)).AddComponent<SoilAdManager>();
        }

        private void Awake()
        {
            if (Instance && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            if (transform.parent == null)
                DontDestroyOnLoad(gameObject);
            else
                MyDebug.Info("SoilAdManager is not a root GameObject, expecting you to manage its lifecycle accordingly.");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
