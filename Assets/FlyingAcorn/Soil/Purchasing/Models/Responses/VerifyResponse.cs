// ReSharper disable InconsistentNaming

using System;
using JetBrains.Annotations;

namespace FlyingAcorn.Soil.Purchasing.Models.Responses
{
    [Serializable]
    public class VerifyResponse
    {
        [UsedImplicitly] public Purchase purchase;
    }
}