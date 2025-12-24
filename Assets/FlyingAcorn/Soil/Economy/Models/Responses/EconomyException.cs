using FlyingAcorn.Soil.Core.Data;

namespace FlyingAcorn.Soil.Economy.Models.Responses
{
    public enum EconomyErrorCode
    {
        InsufficientBalanceOrOutOfRange = 422,
        ItemOrCurrencyNotFound = 404,
        InvalidRequest = 400,
        InternalError = 500
    }

    /// <summary>
    /// Exception thrown when an economy-specific error occurs, containing the detailed EconomyError response.
    /// </summary>
    public class EconomyException : SoilException
    {
        public EconomyError Error { get; }
        public EconomyErrorCode EconomyErrorCode { get; }

        public EconomyException(string message, SoilExceptionErrorCode errorCode, EconomyError error = null, EconomyErrorCode economyErrorCode = EconomyErrorCode.InternalError) 
            : base(message, errorCode)
        {
            Error = error;
            EconomyErrorCode = economyErrorCode;
        }
    }
}
