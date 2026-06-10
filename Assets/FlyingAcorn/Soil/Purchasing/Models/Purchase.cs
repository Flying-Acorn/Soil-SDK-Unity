using System;
using static FlyingAcorn.Soil.Purchasing.Constants;

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
        public FeeType fee_type;
        public double? price;
        public double? price_taxed;
        public double? vat_amount;
        public bool? vat_included;
        public bool? vat_free;
        public TaxMode? tax_mode;
        public string currency;
        public string pay_date;
        public string pay_url;
    }
}