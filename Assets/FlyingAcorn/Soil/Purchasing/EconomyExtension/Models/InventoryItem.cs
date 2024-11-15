using FlyingAcorn.Soil.Purchasing.Models.Items;

namespace FlyingAcorn.Soil.Purchasing.EconomyExtension.Models
{
    public class InventoryItem : Economy.Models.InventoryItem, IQuantifiedItem
    {
        public int Quantity { get; set; }
    }
}