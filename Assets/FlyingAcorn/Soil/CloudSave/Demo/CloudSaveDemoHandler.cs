using System;
using System.Collections.Generic;
using FlyingAcorn.Analytics;
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
            _ = CloudSave.Initialize();
            LoadAll();
            saveButton.onClick.AddListener(Save);
            loadButton.onClick.AddListener(Load);
        }

        private async void Save()
        {
            try
            {
                await CloudSave.SaveAsync(keyInput.text, valueInput.text);
                statusText.text = "";
            }
            catch (Exception e)
            {
                statusText.text = e.Message;
            }

            LoadAll();
        }

        private async void Load()
        {
            try
            {
                var value = await CloudSave.LoadAsync(keyInput.text);
                MyDebug.Info($"Loaded {value.key}: {value.value}");
                statusText.text = "";
            }
            catch (Exception e)
            {
                statusText.text = e.Message;
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