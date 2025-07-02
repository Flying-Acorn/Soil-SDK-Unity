using System;

namespace FlyingAcorn.Soil.Core.Data
{
    public class SoilException : Exception
    {
        public SoilExceptionErrorCode ErrorCode { get; set; }

        public SoilException(string message, SoilExceptionErrorCode errorCode = SoilExceptionErrorCode.Unknown) :
            base(message)
        {
            ErrorCode = errorCode;
        }
    }

    public class SoilNotFoundException : SoilException
    {
        public SoilNotFoundException(string message) :
            base(message, SoilExceptionErrorCode.NotFound)
        {
        }
    }

    public enum SoilExceptionErrorCode
    {
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
        AnotherOngoingInstance = 59,
        InvalidResponse = 60,
        MisConfiguration = 61,
        Canceled = 62,
        NotReady = 63,
    }
}