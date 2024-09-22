using FlyingAcorn.Soil.Core.Data;
using Newtonsoft.Json;
using UnityEngine;

namespace FlyingAcorn.Soil.Core.User
{
    public static class AuthenticatePlayerPrefs
    {
        private const string KeysPrefix = "flying_acorn_soil_";
        internal static UserInfo UserInfoInstance;
        private const string DemoAppID = "c425a46d-5a49-4986-b3fe-e9d61cd957d3";
        private const string DemoAppSDKToken = "8c500e120772a66a1daad9cdfebedbaa3f31d6949ce8d41c94b49f125401ff00";

        public static string AppID
        {
            get
            {
                if (string.IsNullOrEmpty(PlayerPrefs.GetString(KeysPrefix + "app_id")))
                    return PlayerPrefs.GetString(KeysPrefix + "app_id", DemoAppID);
                Debug.LogWarning("AppID is not set. Please set AppID before using AuthenticatePlayerPrefs.");
                return PlayerPrefs.GetString(KeysPrefix + "app_id", DemoAppID);
            }
            set => PlayerPrefs.SetString(KeysPrefix + "app_id", value);
        }

        public static string SDKToken
        {
            get
            {
                if (!string.IsNullOrEmpty(PlayerPrefs.GetString(KeysPrefix + "sdk_token")))
                {
                    return PlayerPrefs.GetString(KeysPrefix + "sdk_token", DemoAppSDKToken);
                }

                Debug.LogWarning("SDKToken is not set. Please set SDKToken before using AuthenticatePlayerPrefs.");
                return PlayerPrefs.GetString(KeysPrefix + "sdk_token", DemoAppSDKToken);
            }

            set => PlayerPrefs.SetString(GetKeysPrefix() + "sdk_token", value);
        }

        internal static UserInfo UserInfo
        {
            get
            {
                if (UserInfoInstance != null) return UserInfoInstance;
                UserInfoInstance =
                    JsonConvert.DeserializeObject<UserInfo>(PlayerPrefs.GetString(GetKeysPrefix() + "player_info"));
                return UserInfoInstance;
            }
            set
            {
                PlayerPrefs.SetString(GetKeysPrefix() + "player_info", JsonConvert.SerializeObject(value));
                UserInfoInstance = value;
            }
        }

        public static TokenData TokenData
        {
            get => JsonConvert.DeserializeObject<TokenData>(PlayerPrefs.GetString(GetKeysPrefix() + "token_data"));
            set => PlayerPrefs.SetString(GetKeysPrefix() + "token_data", JsonConvert.SerializeObject(value));
        }

        private static string GetKeysPrefix()
        {
            if (!string.IsNullOrEmpty(AppID)) return $"{KeysPrefix}{AppID}_";
            Debug.LogWarning("AppID is not set. Please set AppID before using AuthenticatePlayerPrefs.");
            return string.Empty;
        }
    }
}