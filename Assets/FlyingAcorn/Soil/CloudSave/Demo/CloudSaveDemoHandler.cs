using System;
using System.Collections.Generic;
using FlyingAcorn.Analytics;
using FlyingAcorn.Soil.Core;
using FlyingAcorn.Soil.Core.Data;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FlyingAcorn.Soil.CloudSave.Demo
{
    public class CloudSaveDemoHandler : MonoBehaviour
    {
        public SaveRow saveRowPrefab;
        public VerticalLayoutGroup saveRowsContainer;
        public List<SaveRow> saveRows;
        public TMP_InputField keyInput;
        public TMP_InputField valueInput;
        public Button saveButton;
        public Button loadButton;
        public TextMeshProUGUI statusText;

        private void Start()
        {
            statusText.text = "Initializing Soil SDK...";
            if (!SoilServices.Ready)
            {
                SoilServices.OnServicesReady += OnSoilServicesReady;
                SoilServices.OnInitializationFailed += OnSoilServicesInitializationFailed;
                SoilServices.InitializeAsync();
            }
            else
            {
                OnSoilServicesReady();
            }

            saveButton.onClick.AddListener(Save);
            loadButton.onClick.AddListener(Load);
        }

        private void OnDestroy()
        {
            if (SoilServices.OnServicesReady != null)
                SoilServices.OnServicesReady -= OnSoilServicesReady;
            if (SoilServices.OnInitializationFailed != null)
                SoilServices.OnInitializationFailed -= OnSoilServicesInitializationFailed;

            if (saveButton != null)
                saveButton.onClick.RemoveListener(Save);
            if (loadButton != null)
                loadButton.onClick.RemoveListener(Load);
        }

        private void OnSoilServicesReady()
        {
            // Double-check that SoilServices is actually ready
            if (!SoilServices.Ready)
            {
                MyDebug.LogWarning("OnSoilServicesReady called but SoilServices.Ready is still false. Retrying...");
                // Wait a frame and check again
                Invoke(nameof(OnSoilServicesReady), 0.1f);
                return;
            }
            
            statusText.text = "Soil SDK ready. Initializing CloudSave...";
            MyDebug.Info($"CloudSave demo - SoilServices.Ready: {SoilServices.Ready}");
            LoadAll();
        }

        private void OnSoilServicesInitializationFailed(SoilException exception)
        {
            statusText.text = $"SDK initialization failed: {exception.Message}";
        }

        private void Save()
        {
            if (string.IsNullOrEmpty(keyInput.text) || string.IsNullOrEmpty(valueInput.text))
            {
                statusText.text = "Please enter both key and value";
                return;
            }
            
            if (!SoilServices.Ready)
            {
                statusText.text = "Soil SDK is not ready yet. Please wait...";
                return;
            }
            
            _ = SaveAsync();
        }

        private async Cysharp.Threading.Tasks.UniTask SaveAsync()
        {
            statusText.text = "Saving data...";
            MyDebug.Info($"CloudSave SaveAsync - SoilServices.Ready: {SoilServices.Ready}, CloudSave.Ready: {CloudSave.Ready}");
            try
            {
                await CloudSave.SaveAsync(keyInput.text, valueInput.text);
                statusText.text = "Data saved successfully";
            }
            catch (Exception e)
            {
                MyDebug.LogError($"CloudSave SaveAsync failed: {e.Message}");
                statusText.text = $"Save failed: {e.Message}";
            }

            LoadAll();
        }

        private void Load()
        {
            if (string.IsNullOrEmpty(keyInput.text))
            {
                statusText.text = "Please enter a key to load";
                return;
            }
            
            if (!SoilServices.Ready)
            {
                statusText.text = "Soil SDK is not ready yet. Please wait...";
                return;
            }
            
            _ = LoadAsync();
        }

        private async Cysharp.Threading.Tasks.UniTask LoadAsync()
        {
            statusText.text = "Loading data...";
            MyDebug.Info($"CloudSave LoadAsync - SoilServices.Ready: {SoilServices.Ready}, CloudSave.Ready: {CloudSave.Ready}");
            try
            {
                var value = await CloudSave.LoadAsync(keyInput.text);
                MyDebug.Info($"Loaded {value.key}: {value.value}");
                statusText.text = "Data loaded successfully";
            }
            catch (Exception e)
            {
                MyDebug.LogError($"CloudSave LoadAsync failed: {e.Message}");
                statusText.text = $"Load failed: {e.Message}";
            }

            LoadAll();
        }

        private void LoadAll()
        {
            var saves = CloudSavePlayerPrefs.Saves;
            saveRows.ForEach(row => Destroy(row.gameObject));
            saveRows.Clear();
            foreach (var save in saves)
            {
                var saveRow = Instantiate(saveRowPrefab, saveRowsContainer.transform);
                var value = CloudSavePlayerPrefs.Load(save.key);
                saveRow.SetData(save.key, value.ToString());
                saveRows.Add(saveRow);
            }
        }
    }
}