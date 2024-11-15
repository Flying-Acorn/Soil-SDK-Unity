using System;
using JetBrains.Annotations;

namespace FlyingAcorn.Soil.Purchasing.Models.Items
{
    [UsedImplicitly]
    [Serializable]
    public class NormalItem : IQuantifiedItem
    {
        public int Quantity { get; set; }
    }
}