using System;
using TMPro;
using UnityEngine;

namespace FlyingAcorn.Soil.Core.Demo
{
    public class Initializer : MonoBehaviour
    {
        [SerializeField] private bool initOnStart;
    [SerializeField] private TextMeshProUGUI statusText;
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
            
            // Subscribe to initialization events
            SoilServices.OnServicesReady += OnSoilServicesReady;
            SoilServices.OnInitializationFailed += OnSoilServicesInitializationFailed;
        }

        private void OnDestroy()
        {
            // Unsubscribe from events
            if (SoilServices.OnServicesReady != null)
                SoilServices.OnServicesReady -= OnSoilServicesReady;
            if (SoilServices.OnInitializationFailed != null)
                SoilServices.OnInitializationFailed -= OnSoilServicesInitializationFailed;
        }

        private void Start()
        {
            if (initOnStart)
                Initialize();
        }

        private static void Initialize()
        {
            if (!Instance)
                throw new Exception("Initializer instance is null");
            
            Debug.Log("[Soil SDK] Starting initialization...");
            if (SoilServices.Ready)
            {
                Instance.OnSoilServicesReady();
            }
            else
            {
                SoilServices.InitializeAsync();
            }
        }
        
        private void OnSoilServicesReady()
        {
            var msg = "[Soil SDK] Services are ready! User: " + (SoilServices.UserInfo?.uuid ?? "Unknown");
            Debug.Log(msg);
            if (statusText != null) statusText.text = "Soil SDK ready";
        }
        
        private void OnSoilServicesInitializationFailed(Exception exception)
        {
            var msg = $"[Soil SDK] Initialization failed: {exception.Message}";
            Debug.LogError(msg);
            if (statusText != null) statusText.text = msg;
        }
    }
}