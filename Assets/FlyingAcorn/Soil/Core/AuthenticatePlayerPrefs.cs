using FlyingAcorn.Soil.Core.Models;
using UnityEngine;

namespace FlyingAcorn.Soil.Core
{
    public class AuthenticatePlayerPrefs
    {
        private const string KeysPrefix = "flying_acorn_soil_";

        public static string AccessToken
        {
            get => PlayerPrefs.GetString(GetKeysPrefix() + "access_token");
            set => PlayerPrefs.SetString(GetKeysPrefix() + "access_token", value);
        }

        public static string RefreshToken => PlayerPrefs.GetString(GetKeysPrefix() + "refresh_token");

        public static string AppID
        {
            get => PlayerPrefs.GetString(KeysPrefix + "game_id");
            set => PlayerPrefs.SetString(KeysPrefix + "game_id", value);
        }

        public static string SDKToken
        {
            get
            {
                if (!string.IsNullOrEmpty(PlayerPrefs.GetString(GetKeysPrefix() + "sdk_token")))
                    return PlayerPrefs.GetString(GetKeysPrefix() + "sdk_token");
                Debug.LogError("SDKToken is not set. Please set SDKToken before using AuthenticatePlayerPrefs.");
                return string.Empty;

            }

            set => PlayerPrefs.SetString(GetKeysPrefix() + "sdk_token", value);
        }

        private static string GetKeysPrefix()
        {
            if (!string.IsNullOrEmpty(AppID)) return $"{KeysPrefix}{AppID}_";
            Debug.LogError("GameID is not set. Please set GameID before using AuthenticatePlayerPrefs.");
            return string.Empty;
        }
        
        public static PlayerInfo PlayerInfo
        {
            get => JsonUtility.FromJson<PlayerInfo>(PlayerPrefs.GetString(GetKeysPrefix() + "player_info"));
            set => PlayerPrefs.SetString(GetKeysPrefix() + "player_info", JsonUtility.ToJson(value));
        }
    }
}