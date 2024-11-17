using System;
using JetBrains.Annotations;

// ReSharper disable InconsistentNaming

namespace FlyingAcorn.Soil.Purchasing.Models
{
    [UsedImplicitly]
    [Serializable]
    public class Localization
    {
        public string title;
        public string description;
        public string tagline;
        public string language;
    }
}