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

        private string _sku;

        private void Start()
        {
            // Bind to the stored sku, not the label: the label is display text and a localized or
            // truncated row would send the wrong value to the server.
            buyButton.onClick.AddListener(() => OnClick?.Invoke(_sku));
        }

        public void SetData(Item item)
        {
            _sku = item.sku;
            skuText.text = item.sku;
            canBuyText.text = item.enabled ? "Yes" : "No";

            // The server rejects a disabled item, so do not offer it. Rows are still shown, because
            // the items endpoint returns disabled entries on purpose (a greyed-out slot, and so a
            // verification response for a since-disabled sku can still be resolved).
            buyButton.interactable = item.enabled;

            priceText.text = DescribePrice(item);
            normalItemText.text = item.normal_item != null ? item.normal_item.Quantity.ToString() : "-";
            inventoryItemsText.text = item.inventory_items is { Count: > 0 }
                ? item.inventory_items.Sum(i => i.Quantity).ToString()
                : "-";
            virtualCurrencyItemsText.text = item.virtual_currencies is { Count: > 0 }
                ? item.virtual_currencies.Sum(i => i.Quantity).ToString()
                : "-";
        }

        private static string DescribePrice(Item item)
        {
            // price_model is null for an item with no price configured; such an item cannot be
            // bought, and reading through it here used to take the whole shop down.
            if (item.price_model == null)
                return "no price";

            var price = $"{item.price_model.currency} {item.price_model.amount}";
            if (item.vat_free)
                return $"{price} (vat free)";
            return item.vat_included ? $"{price} (vat incl.)" : $"{price} + vat";
        }
    }
}
