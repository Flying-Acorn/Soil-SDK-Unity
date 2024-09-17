using System.Threading.Tasks;
using FlyingAcorn.Soil.Core.Models;
using UnityEngine;

namespace FlyingAcorn.Soil.Core
{
    public static class Authenticate
    {
        private const string ApiUrl = "https://soil.flyingacorn.com/api";

        private static readonly string UserBaseUrl = $"{ApiUrl}/users"; 
        private static readonly string RegisterPlayerUrl = $"{UserBaseUrl}/register";
        private static readonly string RefreshTokenUrl = $"{UserBaseUrl}/refreshtoken"; 
        private static readonly string GetPlayerInfoUrl = $"{UserBaseUrl}/";
        
        private static Task RegisterPlayer()
        {
            Debug.Log("Registering player...");
            return Task.CompletedTask;
        }

        public static async Task AuthenticateUser(PlayerInfo currentPlayerInfo)
        {
            if (string.IsNullOrEmpty(currentPlayerInfo.uuid) || string.IsNullOrEmpty(AuthenticatePlayerPrefs.AccessToken) ||
                string.IsNullOrEmpty(AuthenticatePlayerPrefs.RefreshToken))
            {
                await RegisterPlayer();
            }
            else
            {
                Debug.Log($"Player is already registered. Player info: {currentPlayerInfo}");
                if (!CanUseAccessToken())
                {
                    await RefreshToken();
                }
            }
        }

        private static async Task RefreshToken()
        {
            Debug.Log("Refreshing token...");
            await Task.CompletedTask;
        }

        private static bool CanUseAccessToken()
        {
            return true;
        }
    }
}