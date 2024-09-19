using UnityEngine;

namespace FlyingAcorn.Soil.Core.Sample
{
    public class Initializer : MonoBehaviour
    {
        [SerializeField] private string appID;
        [SerializeField] private string sdkToken;
        [SerializeField] private bool initOnStart;
        private static Initializer Instance { get; set; }

        private void Awake()
        {
            if (Instance != null)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            if (initOnStart)
                Initialize();
        }

        private async void Initialize()
        {
            await SoilServices.Initialize(appID, sdkToken);
        }
    }
}