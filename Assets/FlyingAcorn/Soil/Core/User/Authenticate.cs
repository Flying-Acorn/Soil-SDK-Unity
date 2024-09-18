using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using FlyingAcorn.Soil.Core.Data;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Networking;
using Utils = FlyingAcorn.Soil.Core.JWTTools.Utils;

namespace FlyingAcorn.Soil.Core.User
{
    public static class Authenticate
    {
        private const string ApiUrl = "https://soil.flyingacorn.studio/api";

        private static readonly string UserBaseUrl = $"{ApiUrl}/users";
        private static readonly string RegisterPlayerUrl = $"{UserBaseUrl}/register";
        private static readonly string RefreshTokenUrl = $"{UserBaseUrl}/refreshtoken";
        private static readonly string GetPlayerInfoUrl = $"{UserBaseUrl}/";

        private static IEnumerator RegisterPlayer()
        {
            Debug.Log("Registering player...");
            var appID = AuthenticatePlayerPrefs.AppID;
            var sdkToken = AuthenticatePlayerPrefs.SDKToken;
            
            var payload = new Dictionary<string, string>
            {
                { "iss", appID }
            };
            var bearerToken = Utils.GenerateJwt(payload, sdkToken);
            var body = Data.Utils.GenerateUserProperties();
            var form = new WWWForm();
            form.AddField("properties", JsonConvert.SerializeObject(body));

            using var request = UnityWebRequest.Post(RegisterPlayerUrl, form);
            request.SetRequestHeader("Authorization", $"Bearer {bearerToken}");
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result != UnityWebRequest.Result.Success)
                Debug.LogError(request.error);
            else
                try
                {
                    AuthenticatePlayerPrefs.TokenData = JsonUtility.FromJson<TokenData>(request.downloadHandler.text);
                    Debug.Log($"Player registered successfully. Response: {request.downloadHandler.text}");
                }
                catch (Exception e)
                {
                    Debug.LogError($"Error: {e.Message}");
                }
        }

        public static async Task AuthenticateUser(string appID, string sdkToken)
        {
            AuthenticatePlayerPrefs.AppID = appID;
            AuthenticatePlayerPrefs.SDKToken = sdkToken;
            
            var currentPlayerInfo = AuthenticatePlayerPrefs.UserInfo;
            if (currentPlayerInfo == null || string.IsNullOrEmpty(currentPlayerInfo.uuid) ||
                string.IsNullOrEmpty(AuthenticatePlayerPrefs.TokenData.Access) ||
                string.IsNullOrEmpty(AuthenticatePlayerPrefs.TokenData.Refresh))
            {
                SoilInitializer.Instance.StartCoroutine(RegisterPlayer());
            }
            else
            {
                Debug.Log($"Player is already registered. Player info: {currentPlayerInfo}");
                if (!CanUseAccessToken()) await RefreshToken();
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