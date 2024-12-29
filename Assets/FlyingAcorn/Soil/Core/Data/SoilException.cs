using System;

namespace FlyingAcorn.Soil.Core.Data
{
    public class SoilException : Exception
    {
        public int ErrorCode { get; set; }
        
        public SoilException(string message) : base(message)
        {
        }
    }

    public enum SoilExceptionErrorCode
    {
        MinValue = 0,
        Unknown = 0,
        TransportError = 1,
        Timeout = 2,
        ServiceUnavailable = 3,
        ApiMissing = 4,
        RequestRejected = 5,
        TooManyRequests = 50,
        InvalidToken = 51,
        TokenExpired = 52,
        Forbidden = 53,
        NotFound = 54,
        InvalidRequest = 55,
        ProjectPolicyAccessDenied = 56,
        PlayerPolicyAccessDenied = 57,
        Conflict = 58,
    }
}