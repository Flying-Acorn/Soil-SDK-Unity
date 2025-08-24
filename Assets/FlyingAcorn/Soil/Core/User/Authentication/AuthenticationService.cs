using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using FlyingAcorn.Soil.Core.User.Authentication.Data;

namespace FlyingAcorn.Soil.Core.User.Authentication
{
    public static class AuthenticationService
    {
        public static string LastNotificationDate { get; set; }

        public static UniTask<List<Notification>> GetNotificationsAsync()
        {
            return UniTask.FromResult(new List<Notification>());
        }
    }
}