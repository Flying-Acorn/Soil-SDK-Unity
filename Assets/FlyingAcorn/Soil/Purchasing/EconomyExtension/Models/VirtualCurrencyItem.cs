using FlyingAcorn.Soil.Purchasing.Models.Items;

namespace FlyingAcorn.Soil.Purchasing.EconomyExtension.Models
{
    public class VirtualCurrencyItem : Economy.Models.VirtualCurrency, IQuantifiedItem
    {
        public int Quantity { get; set; }
    }
}