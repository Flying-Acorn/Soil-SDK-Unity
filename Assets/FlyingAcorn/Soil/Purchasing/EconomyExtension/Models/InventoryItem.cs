using FlyingAcorn.Soil.Purchasing.Models.Items;
using JetBrains.Annotations;

namespace FlyingAcorn.Soil.Purchasing.EconomyExtension.Models
{
    [UsedImplicitly]
    public class InventoryItem : Economy.Models.InventoryItem, IQuantifiedItem
    {
        public int Quantity { get; set; }
    }
}