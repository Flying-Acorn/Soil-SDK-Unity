using FlyingAcorn.Soil.Core.Data;

namespace FlyingAcorn.Soil.Push.Data
{
    public class PushException : SoilException
    {
        public PushOperation Operation { get; set; }

        public PushException(string message, PushOperation operation = PushOperation.Unknown,
            SoilExceptionErrorCode errorCode = SoilExceptionErrorCode.Unknown) : base(message, errorCode)
        {
            Operation = operation;
        }
    }

    /// <summary>Only ever appended to.</summary>
    public enum PushOperation
    {
        Unknown = 0,
        Register = 1,
        Unregister = 2,
    }
}
