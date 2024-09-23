using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using FlyingAcorn.Soil.Core.JWTTools;
using Newtonsoft.Json;
using UnityEngine;

namespace FlyingAcorn.Soil.Core.User
{
    public static class UserApiHandler
    {
        private static readonly string GetPlayerInfoUrl = $"{Authenticate.UserBaseUrl}/";

        internal static async Task FetchPlayerInfo()
        {
            Debug.Log("Fetching player info...");

            if (!JwtUtils.IsTokenValid(AuthenticatePlayerPrefs.TokenData.Access))
            {
                Debug.LogWarning("Access token is not valid. Trying to refresh tokens...");
                await Authenticate.RefreshTokenIfNeeded(true);
            }


            var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = Authenticate.GetAuthorizationHeader();
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            var request = new HttpRequestMessage(HttpMethod.Get, GetPlayerInfoUrl);

            var response = await client.SendAsync(request);
            var responseString = response.Content.ReadAsStringAsync().Result;

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Network error while fetching player info. Response: {responseString}");
            }

            AuthenticatePlayerPrefs.UserInfo = JsonConvert.DeserializeObject<UserInfo>(responseString);
            Debug.Log($"Player info fetched successfully. Response: {responseString}");
        }
    }
}