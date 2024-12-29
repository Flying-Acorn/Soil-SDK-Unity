namespace FlyingAcorn.Soil.Core.Data
{
    public class RequestFailedException : SoilException
    {
        public new int ErrorCode { get; }

        public RequestFailedException(int code, string mesage) : base(mesage)
        {
            ErrorCode = code;
        }
    }
}