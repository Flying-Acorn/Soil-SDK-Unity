// ReSharper disable UnusedMember.Global
// ReSharper disable UnusedMemberInSuper.Global
#pragma warning disable IDE1006

using System.Collections.Generic;

namespace FlyingAcorn.Soil.Economy.Models.Responses
{
    public class EconomyError
    {
        public string detail { get; set; }
        public Errors errors { get; set; }
    }

    public class Errors
    {
        public List<string> balance;
        public List<string> name;
        public List<string> identifier;
        public List<string> amount;
    }

    public enum EconomyErrorCodes // Errors other than these are likely to be 500 and cannot map to EconomyError
    {
        InsufficientBalanceOrOutOfRange = 422,
        ItemOrCurrencyNotFound = 404,
        InvalidRequest = 400, // Probably have errors field populated
        InternalError = 500
    }
}
