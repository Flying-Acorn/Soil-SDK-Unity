using System.Collections.Generic;
using System.Threading.Tasks;
using FlyingAcorn.Soil.Core.User.Authentication.Data;

namespace FlyingAcorn.Soil.Core.User.Authentication
{
    public static class AuthenticationService
    {
        public static string LastNotificationDate { get; set; }

        public static Task<List<Notification>> GetNotificationsAsync()
        {
            return Task.FromResult(new List<Notification>());
        }
    }
}