namespace FlyingAcorn.Soil.Purchasing
{
    public static class Purchasing
    {
        private static readonly string PurchasingBaseUrl = $"{Core.Data.Constants.ApiUrl}/iap";
        private static readonly string PurchaseBaseUrl = $"{PurchasingBaseUrl}/purchase";
        
        private static readonly string ItemsUrl = $"{PurchaseBaseUrl}/items/";
        private static readonly string CreatePurchaseUrl = $"{PurchaseBaseUrl}/";
        private static readonly string VerifyPurchaseUrl = $"{PurchaseBaseUrl}/verify/";
        private static readonly string PurchaseInvoiceUrl = PurchaseBaseUrl + "{purchase_id}/invoice/";
    }
}