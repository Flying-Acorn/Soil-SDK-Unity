using System;
using JetBrains.Annotations;

namespace FlyingAcorn.Soil.Economy.Models
{
    [UsedImplicitly]
    [Serializable]
    public class InventoryItem : IEconomyItem
    {
        public string Identifier { get; set; }
        public string Name { get; set; }
    }
}