using System;
using JetBrains.Annotations;

namespace FlyingAcorn.Soil.Purchasing.Models
{
    [UsedImplicitly]
    [Serializable]
    public class Item
    {
        public string name;
        public string sku;
        public bool enabled;
        public string description;
    }
}