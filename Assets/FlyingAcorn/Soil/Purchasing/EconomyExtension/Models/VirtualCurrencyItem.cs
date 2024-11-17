using FlyingAcorn.Soil.Purchasing.Models.Items;
using JetBrains.Annotations;

namespace FlyingAcorn.Soil.Purchasing.EconomyExtension.Models
{
    [UsedImplicitly]
    public class VirtualCurrencyItem : Economy.Models.VirtualCurrency, IQuantifiedItem
    {
        public int Quantity { get; set; }
    }
}