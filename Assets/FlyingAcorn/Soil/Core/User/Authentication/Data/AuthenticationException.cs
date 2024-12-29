using System.Collections.Generic;
using FlyingAcorn.Soil.Core.Data;

namespace FlyingAcorn.Soil.Core.User.Authentication.Data
{
    public abstract class AuthenticationException : SoilException
    {
        protected AuthenticationException(string message, List<Notification> notifications, AuthenticationErrorCode errorCode) : base(message)
        {
            Notifications = notifications;
            ErrorCode = errorCode;
        }

        public List<Notification> Notifications { get; set; }
        public new AuthenticationErrorCode ErrorCode { get; set; }
        public const int MinValue = 100;
    }
    
    public enum AuthenticationErrorCode
    {
        ClientInvalidUserState = 100,
        ClientInvalidUser = 101,
        ClientInvalidToken = 102,
        ClientInvalidTokenState = 103,
        ClientInvalidTokenExpired = 104,
        EnvironmentMismatch = 105,
        BannedUser = 106,
    }
}