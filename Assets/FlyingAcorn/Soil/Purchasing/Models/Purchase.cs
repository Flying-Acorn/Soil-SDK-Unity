using System;
// ReSharper disable UnusedMember.Global
// ReSharper disable InconsistentNaming

namespace FlyingAcorn.Soil.Purchasing.Models
{
    [Serializable]
    public class Purchase
    {
        public string purchase_id;
        public string sku;
        public bool paid;
        public bool expired;
        public string transaction_id;
        public double? fee;
        public string fee_type;
        public string pay_url;
        public double? price;
        public string currency;
    }
}