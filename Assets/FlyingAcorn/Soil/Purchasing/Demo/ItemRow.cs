using System;
using System.Linq;
using FlyingAcorn.Soil.Purchasing.Models;
using UnityEngine;
using UnityEngine.UI;

namespace FlyingAcorn.Soil.Purchasing.Demo
{
    public class ItemRow : MonoBehaviour
    {
        [SerializeField] private TMPro.TextMeshProUGUI canBuyText;
        [SerializeField] private TMPro.TextMeshProUGUI skuText;
        [SerializeField] private TMPro.TextMeshProUGUI priceText;
        [SerializeField] private TMPro.TextMeshProUGUI normalItemText;
        [SerializeField] private TMPro.TextMeshProUGUI inventoryItemsText;
        [SerializeField] private TMPro.TextMeshProUGUI virtualCurrencyItemsText;
        [SerializeField] private Button buyButton;
        public Action<string> OnClick;
        
        private void Start()
        {
            buyButton.onClick.AddListener(() => OnClick?.Invoke(skuText.text));
        }

        public void SetData(Item item)
        {
            canBuyText.text = item.enabled ? "Yes" : "No";
            skuText.text = item.sku;
            priceText.text = $"{item.price_model.currency} {item.price_model.amount}";
            normalItemText.text = item.normal_item != null ? item.normal_item.Quantity.ToString() : "-";
            inventoryItemsText.text = item.inventory_items != null
                ? item.inventory_items.Select(i => i.Quantity).Sum().ToString()
                : "-";
            virtualCurrencyItemsText.text = item.virtual_currencies != null
                ? item.virtual_currencies.Select(i => i.Quantity).Sum().ToString()
                : "-";
        }
    }
}