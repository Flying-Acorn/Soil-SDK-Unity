using System;
using System.Collections.Generic;
using FlyingAcorn.Soil.Purchasing.EconomyExtension.Models;
using FlyingAcorn.Soil.Purchasing.Models.Items;
using JetBrains.Annotations;
// ReSharper disable InconsistentNaming

namespace FlyingAcorn.Soil.Purchasing.Models
{
    [UsedImplicitly]
    [Serializable]
    public class Item
    {
        public string name;
        public string sku;
        public string description;
        public bool enabled;
        public PriceModel price_model;
        public List<Localization> localizations;
        public NormalItem normal_item;
        public List<InventoryItem> inventory_items;
        public List<VirtualCurrencyItem> virtual_currencies;
    }
}