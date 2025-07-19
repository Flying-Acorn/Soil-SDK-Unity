using FlyingAcorn.Analytics;
using FlyingAcorn.Soil.Advertisement.Models.AdPlacements;
using UnityEngine;

namespace FlyingAcorn.Soil.Advertisement
{
    public class SoilAdManager : MonoBehaviour
    {
        public static SoilAdManager Instance { get; private set; }
        [Header("Ad Placement Prefabs")]
        public BannerAdPlacement bannerAdPlacement;
        public InterstitialAdPlacement interstitialAdPlacement;
        public RewardedAdPlacement rewardedAdPlacement;
        public Canvas canvasReference;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this.gameObject);
                return;
            }
            Instance = this;
            if (transform.parent == null)
                DontDestroyOnLoad(this.gameObject);
            else
                MyDebug.Info("SoilAdManager is not a root GameObject, expecting you to manage its lifecycle accordingly.");
        }
    }
}