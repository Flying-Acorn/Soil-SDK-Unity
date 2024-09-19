using FlyingAcorn.Soil.Core.Data;
using UnityEngine;

namespace FlyingAcorn.Soil.Core.User
{
    public static class AuthenticatePlayerPrefs
    {
        private const string KeysPrefix = "flying_acorn_soil_";

        public static string AppID
        {
            get => PlayerPrefs.GetString(KeysPrefix + "app_id");
            set => PlayerPrefs.SetString(KeysPrefix + "app_id", value);
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

        public static UserInfo UserInfo
        {
            get => JsonUtility.FromJson<UserInfo>(PlayerPrefs.GetString(GetKeysPrefix() + "player_info"));
            set => PlayerPrefs.SetString(GetKeysPrefix() + "player_info", JsonUtility.ToJson(value));
        }

        public static TokenData TokenData
        {
            get => JsonUtility.FromJson<TokenData>(PlayerPrefs.GetString(GetKeysPrefix() + "token_data"));
            set => PlayerPrefs.SetString(GetKeysPrefix() + "token_data", JsonUtility.ToJson(value));
        }

        private static string GetKeysPrefix()
        {
            if (!string.IsNullOrEmpty(AppID)) return $"{KeysPrefix}{AppID}_";
            Debug.LogWarning("AppID is not set. Please set AppID before using AuthenticatePlayerPrefs.");
            return string.Empty;
        }
    }
}