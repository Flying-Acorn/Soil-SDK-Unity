using System;
using JWT;

namespace FlyingAcorn.Soil.Core.JWTTools
{
    /// <summary>
    /// A DateTimeProvider that adjusts UtcNow by a given offset (in seconds)
    /// to compensate for device clock drift relative to the server.
    /// </summary>
    public sealed class OffsetDateTimeProvider : IDateTimeProvider
    {
        private readonly long _offsetSeconds;

        /// <param name="offsetSeconds">
        /// The offset in seconds (serverTime - deviceTime).
        /// Positive means the device clock is behind the server.
        /// Negative means the device clock is ahead of the server.
        /// </param>
        public OffsetDateTimeProvider(long offsetSeconds)
        {
            _offsetSeconds = offsetSeconds;
        }

        public DateTimeOffset GetNow()
        {
            try
            {
                return DateTimeOffset.UtcNow.AddSeconds(_offsetSeconds);
            }
            catch (ArgumentOutOfRangeException)
            {
                // Fallback to strict UtcNow if the offset is absurdly large 
                // (e.g., user manipulated device year to 9999 and back)
                return DateTimeOffset.UtcNow;
            }
        }
    }
}
