using System;
using System.Collections.Generic;
using FlyingAcorn.Soil.Core;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FlyingAcorn.Soil.RemoteConfig.Demo
{
    public class RemoteConfigDemoHandler : MonoBehaviour
    {
        [SerializeField] private Button setDevButton;
        [SerializeField] private TextMeshProUGUI setDevText;

        [SerializeField] private TextMeshProUGUI fetchedDataText;

        [SerializeField] private Button fetchButton;

        private static bool DevMode
        {
            get => PlayerPrefs.GetInt($"{RemoteConfigPlayerPrefs.PrefsPrefix}_demo_dev_mode", 0) == 1;
            set => PlayerPrefs.SetInt($"{RemoteConfigPlayerPrefs.PrefsPrefix}_demo_dev_mode", value ? 1 : 0);
        }

        private void Start()
        {
            SetDevButton();
            fetchedDataText.text = "Initializing Soil SDK...";
            
            SoilServices.OnServicesReady += OnSoilServicesReady;

            SoilServices.OnInitializationFailed += OnSoilServicesInitializationFailed;

            if (SoilServices.Ready)
            {
                OnSoilServicesReady();
            }
            else
            {
                SoilServices.InitializeAsync();
            }

            setDevButton.onClick.AddListener(SetDevMode);
            fetchButton.onClick.AddListener(InitializeAndFetchTest);
        }
        
        private void OnDestroy()
        {
            if (SoilServices.OnServicesReady != null)
                SoilServices.OnServicesReady -= OnSoilServicesReady;
            if (SoilServices.OnInitializationFailed != null)
                SoilServices.OnInitializationFailed -= OnSoilServicesInitializationFailed;
        }
        
        private void OnSoilServicesReady()
        {
            fetchedDataText.text = "Soil SDK ready. Press Fetch to get remote config.";
        }

        private void OnSoilServicesInitializationFailed(Exception exception)
        {
            fetchedDataText.text = $"SDK initialization failed: {exception.Message}";
        }

        #region remote config usage

        private void InitializeAndFetchTest()
        {
            fetchedDataText.text = "Working...";
            
            RemoteConfig.OnServerAnswer -= HandleReceivedConfigs;
            RemoteConfig.OnServerAnswer += HandleReceivedConfigs;
            RemoteConfig.FetchConfig(new Dictionary<string, object>
                { { "devmode", DevMode ? "1" : "0" } });
        }

        private void FetchTest()
        {
            RemoteConfig.OnServerAnswer -= HandleReceivedConfigs;
            RemoteConfig.OnServerAnswer += HandleReceivedConfigs;
            RemoteConfig.FetchConfig(new Dictionary<string, object>
                { { "devmode", DevMode ? "1" : "0" } });
        }

        private void HandleReceivedConfigs(bool b)
        {
            if (!b)
            {
                fetchedDataText.text = "Failed to fetch remote config";
                return;
            }

            fetchedDataText.text = RemoteConfig.UserDefinedConfigs.ToString(Formatting.Indented);
        }

        #endregion

        #region buttons

        private void SetDevMode()
        {
            DevMode = !DevMode;
            SetDevButton();
        }

        private void SetDevButton()
        {
            setDevText.text = "Devmode=" + (DevMode ? 1 : 0);
        }

        #endregion
    }
}