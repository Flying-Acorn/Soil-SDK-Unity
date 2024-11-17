namespace FlyingAcorn.Soil.Purchasing.Models
{
    public class Purchase
    {
        public string purchase_id;
        public bool paid;
        public bool expired;
        public string sku;
        public string transaction_id;
        public double fee;
        public string fee_type;
        public string pay_url;
    }
}