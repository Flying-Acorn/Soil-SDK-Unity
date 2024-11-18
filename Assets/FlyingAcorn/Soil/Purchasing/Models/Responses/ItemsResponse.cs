// ReSharper disable InconsistentNaming

using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace FlyingAcorn.Soil.Purchasing.Models.Responses
{
    [Serializable]
    public class ItemsResponse
    {
        [UsedImplicitly] public List<Item> items;
    }
}