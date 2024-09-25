using System;
using UnityEngine;

namespace FlyingAcorn.Soil.Core.Demo
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
            SoilServices.SetRegistrationInfo(appID, sdkToken);
            try
            {
                await SoilServices.Initialize();
            }
            catch (Exception e)
            {
                Debug.LogError("Failed to initialize SoilServices: " + e.Message + " " + e.StackTrace);
            }
        }
    }
}