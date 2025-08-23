using System;
using System.Collections.Generic;
using FlyingAcorn.Analytics;
using FlyingAcorn.Soil.Core;
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
            SoilServices.OnServicesReady += OnSoilServicesReady;
            SoilServices.OnInitializationFailed += OnSoilServicesInitializationFailed;
            SoilServices.InitializeAsync();
            
            saveButton.onClick.AddListener(Save);
            loadButton.onClick.AddListener(Load);
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
            statusText.text = "Soil SDK ready. Initializing CloudSave...";
            LoadAll();
        }
        
        private void OnSoilServicesInitializationFailed(Exception exception)
        {
            statusText.text = $"SDK initialization failed: {exception.Message}";
        }

        private async void Save()
        {
            statusText.text = "Saving data...";
            try
            {
                await CloudSave.SaveAsync(keyInput.text, valueInput.text);
                statusText.text = "Data saved successfully";
            }
            catch (Exception e)
            {
                statusText.text = $"Save failed: {e.Message}";
            }

            LoadAll();
        }

        private async void Load()
        {
            statusText.text = "Loading data...";
            try
            {
                var value = await CloudSave.LoadAsync(keyInput.text);
                MyDebug.Info($"Loaded {value.key}: {value.value}");
                statusText.text = "Data loaded successfully";
            }
            catch (Exception e)
            {
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