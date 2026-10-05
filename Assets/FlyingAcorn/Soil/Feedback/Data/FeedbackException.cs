using FlyingAcorn.Soil.Core.Data;

namespace FlyingAcorn.Soil.Feedback.Data
{
    public class FeedbackException : SoilException
    {
        public FeedbackOperation Operation { get; set; }

        public FeedbackException(string message, FeedbackOperation operation = FeedbackOperation.Unknown,
            SoilExceptionErrorCode errorCode = SoilExceptionErrorCode.Unknown) : base(message, errorCode)
        {
            Operation = operation;
        }
    }

    /// <summary>Only ever appended to.</summary>
    public enum FeedbackOperation
    {
        Unknown = 0,
        GetChannels = 1,
        Send = 2,
        GetMyFeedback = 3,
    }
}
