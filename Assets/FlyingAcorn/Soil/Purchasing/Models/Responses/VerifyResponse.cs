// ReSharper disable InconsistentNaming
namespace FlyingAcorn.Soil.Purchasing.Models.Responses
{
    public class VerifyResponse : PurchaseResponseBase
    {
        public bool paid;
        public string transaction_id;
        public double fee;
        public string fee_type;
    }
}