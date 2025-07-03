using FlyingAcorn.Soil.Core.Data;

namespace FlyingAcorn.Soil.Socialization.Data
{
    public class SocializationException : SoilException
    {
        public SocializationOperation Operation { get; set; }
        
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
        GetFriendsLeaderboard = 4
    }
}
