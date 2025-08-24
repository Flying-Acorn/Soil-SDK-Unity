using System;
using System.Collections.Generic;
using FlyingAcorn.Soil.Core;
using FlyingAcorn.Soil.Core.Data;
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
        private List<ItemRow> _rows = new();

        private void Start()
        {
            Failed("Initializing Soil SDK...");
            Purchasing.OnPurchasingInitialized += OnPurchasingInitialized;
            SoilServices.OnInitializationFailed += OnSoilServicesInitializationFailed;

            Purchasing.OnItemsReceived += FillItems;
            Purchasing.OnPurchaseSuccessful += OnPurchaseSuccessful;
            Purchasing.OnPurchaseStart += OnPurchaseStart;
            verifyButton.onClick.AddListener(VerifyAllPurchases);

            if (Purchasing.Ready)
            {
                OnPurchasingInitialized();
            }
            else
            {
                Purchasing.Initialize(verifyOnInitialize: true);
            }
        }

        private void OnPurchasingInitialized()
        {
            Failed("Purchasing initialized");
        }

        private void OnDestroy()
        {
            Purchasing.OnPurchasingInitialized -= OnPurchasingInitialized;
            SoilServices.OnInitializationFailed -= OnSoilServicesInitializationFailed;
            Purchasing.OnItemsReceived -= FillItems;
            Purchasing.OnPurchaseSuccessful -= OnPurchaseSuccessful;
            Purchasing.OnPurchaseStart -= OnPurchaseStart;

            if (verifyButton != null)
                verifyButton.onClick.RemoveListener(VerifyAllPurchases);
        }

        private void OnSoilServicesInitializationFailed(SoilException exception)
        {
            Failed($"SDK initialization failed: {exception.Message}");
        }

        private void OnApplicationFocus(bool focusStatus)
        {
            if (focusStatus)
                VerifyAllPurchases();
        }

        private void OnPurchaseStart(Item obj)
        {
            Failed($"Purchasing {obj.sku}...");
        }

        private void OnPurchaseSuccessful(Purchase obj)
        {
            Failed($"Purchased {obj.sku} successfully!");
        }

        private void FillItems(List<Item> items)
        {
            Failed($"Received {items.Count} items.");
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

        private void BuyItem(string sku)
        {
            Failed($"Purchasing {sku}...");
            _ = Purchasing.BuyItem(sku);
        }

        private void Failed(string error)
        {
            resultText.text = error;
        }

        private void VerifyAllPurchases()
        {
            Failed("Verifying all purchases...");
            if (PurchasingPlayerPrefs.UnverifiedPurchaseIds.Count == 0)
            {
                Failed("No unverified purchases.");
                return;
            }

            Purchasing.SafeVerifyAllPurchases();
        }
    }
}