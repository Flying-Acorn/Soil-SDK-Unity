using System;
using JetBrains.Annotations;
// ReSharper disable InconsistentNaming

namespace FlyingAcorn.Soil.Purchasing.Models
{
    [UsedImplicitly]
    [Serializable]
    public class PriceModel
    {
        public string currency;
        public double price;
    }
}