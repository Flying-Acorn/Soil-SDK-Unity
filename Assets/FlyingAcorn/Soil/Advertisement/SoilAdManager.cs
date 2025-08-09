using FlyingAcorn.Analytics;
using FlyingAcorn.Soil.Advertisement.Models.AdPlacements;
using UnityEngine;
using UnityEngine.UI;

namespace FlyingAcorn.Soil.Advertisement
{
    public class SoilAdManager : MonoBehaviour
    {
        public static SoilAdManager Instance { get; private set; }
        [Header("Ad Placement Prefabs")]
        public BannerAdPlacement bannerAdPlacement;
        public InterstitialAdPlacement interstitialAdPlacement;
        public RewardedAdPlacement rewardedAdPlacement;
        [SerializeField] private Canvas canvasReference;
        public CanvasReferences canvasReferences;

        public class CanvasReferences
        {
            public Vector2 ReferenceResolution;
            public CanvasScaler.ScaleMode UIScaleMode;
            public CanvasScaler.ScreenMatchMode ScreenMatchMode;
            public float MatchWidthOrHeight;
            public float ReferencePixelsPerUnit;
            public int Layer;

        }

        private void Awake()
        {
            if (Instance && Instance != this)
            {
                Destroy(this.gameObject);
                return;
            }
            Instance = this;
            if (transform.parent == null)
                DontDestroyOnLoad(this.gameObject);
            else
                MyDebug.Info("SoilAdManager is not a root GameObject, expecting you to manage its lifecycle accordingly.");
            if (canvasReference && canvasReference.TryGetComponent<CanvasScaler>(out var scaler))
            {
                canvasReferences = new CanvasReferences
                {
                    ReferenceResolution = scaler.referenceResolution,
                    UIScaleMode = scaler.uiScaleMode,
                    ScreenMatchMode = scaler.screenMatchMode,
                    MatchWidthOrHeight = scaler.matchWidthOrHeight,
                    ReferencePixelsPerUnit = scaler.referencePixelsPerUnit,
                    Layer = canvasReference.gameObject.layer
                };
            }
            else
            {
                MyDebug.LogWarning("Canvas reference is not set. Please assign a Canvas to the SoilAdManager.");
            }
        }
    }
}