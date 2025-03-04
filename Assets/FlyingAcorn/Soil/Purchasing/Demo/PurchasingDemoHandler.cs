using System.Collections.Generic;
using FlyingAcorn.Soil.Purchasing.Models;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FlyingAcorn.Soil.Purchasing.Demo
{
    public class PurchasingDemoHandler : MonoBehaviour
    {
        [SerializeField] private ItemRow itemRowPrefab;
        [SerializeField] private VerticalLayoutGroup shopContainer;
        [SerializeField] private TextMeshProUGUI resultText;
        [SerializeField] private Button verifyButton;
        [SerializeField] private bool enableWithRemote;
        private List<ItemRow> _rows = new();
        private const string ConfigEnableKey = "purchasing_enabled";

        private void Start()
        {
            Purchasing.OnItemsReceived += FillItems;
            Purchasing.OnPurchaseSuccessful += OnPurchaseSuccessful;
            Purchasing.OnPurchaseStart += OnPurchaseStart;
            verifyButton.onClick.AddListener(VerifyAllPurchases);
            if (!enableWithRemote)
            {
                Log("Purchasing is enabled. Getting items...");
                _ = Purchasing.Initialize();
                return;
            }
            RemoteConfig.RemoteConfig.OnServerAnswer += OnServerAnswer;
            OnServerAnswer(false);
            Log("Fetching remote config...");
            RemoteConfig.RemoteConfig.FetchConfig();
        }

        private void OnServerAnswer(bool obj)
        {
            if (RemoteConfig.RemoteConfig.UserDefinedConfigs == null)
            {
                return;
            }

            if (RemoteConfig.RemoteConfig.UserDefinedConfigs.TryGetValue(ConfigEnableKey, out var value))
            {
                if (bool.TryParse(value.ToString(), out var isShopEnabled))
                {
                    if (isShopEnabled)
                    {
                        Log("Purchasing is enabled. Getting items...");
                        _ = Purchasing.Initialize();
                    }
                    else
                    {
                        Log("Purchasing is disabled.");
                    }
                }
                else
                {
                    Log("Failed to parse remote config value.");
                }
            }
            else
            {
                Log("Failed to find remote config key.");
            }
        }

        private void OnApplicationFocus(bool focusStatus)
        {
            if (focusStatus)
                VerifyAllPurchases();
        }

        private void OnPurchaseStart(Item obj)
        {
            Log($"Purchasing {obj.sku}...");
        }

        private void OnPurchaseSuccessful(Purchase obj)
        {
            Log($"Purchased {obj.sku} successfully!");
        }

        private void FillItems(List<Item> items)
        {
            Log($"Received {items.Count} items.");
            foreach (var row in _rows)
            {
                row.OnClick -= BuyItem;
                Destroy(row.gameObject);
            }

            _rows = new List<ItemRow>();

            foreach (var item in items)
            {
                var row = Instantiate(itemRowPrefab, shopContainer.transform);
                row.SetData(item);
                row.OnClick += BuyItem;
                _rows.Add(row);
            }
        }

        private static void BuyItem(string sku)
        {
            _ = Purchasing.BuyItem(sku);
        }

        private void Log(string error)
        {
            resultText.text = error;
        }

        private void VerifyAllPurchases()
        {
            if (!Purchasing.Ready)
                return;
            Log("Verifying all purchases...");
            if (PurchasingPlayerPrefs.UnverifiedPurchaseIds.Count == 0)
            {
                Log("No unverified purchases.");
                return;
            }

            Purchasing.SafeVerifyAllPurchases();
        }
    }
}