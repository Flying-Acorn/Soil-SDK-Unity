// ReSharper disable InconsistentNaming

using System;
using System.Collections.Generic;
using JetBrains.Annotations;

namespace FlyingAcorn.Soil.Purchasing.Models.Responses
{
    [Serializable]
    public class BatchVerifyResponse
    {
        [UsedImplicitly] public List<Purchase> purchases;
    }
}