using FlyingAcorn.Soil.Core.Data;

namespace FlyingAcorn.Soil.Socialization.Data
{
    public class SocializationException : SoilException
    {
        public SocializationOperation Operation { get; set; }

        /// <summary>
        /// Seconds the server asked to wait before trying again (its <c>Retry-After</c> header), for example when
        /// a list or the invite screen was read too often (<see cref="SoilExceptionErrorCode.TooManyRequests"/>).
        /// Null when the server gave none.
        /// </summary>
        public int? RetryAfterSeconds { get; set; }

        public SocializationException(string message, SocializationOperation operation = SocializationOperation.Unknown, 
            SoilExceptionErrorCode errorCode = SoilExceptionErrorCode.Unknown) : base(message, errorCode)
        {
            Operation = operation;
        }
    }

    public enum SocializationOperation
    {
        Unknown = 0,
        GetFriends = 1,
        AddFriend = 2,
        RemoveFriend = 3,
        GetFriendsLeaderboard = 4,
        GetFriendList = 5,
        FriendAction = 6,
        GetReferralInfo = 7,
        RedeemReferralCode = 8
    }
}
