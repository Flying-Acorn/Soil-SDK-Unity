using FlyingAcorn.Soil.Core.Data;
using Newtonsoft.Json;
using UnityEngine;

namespace FlyingAcorn.Soil.Core.User
{
    public static class AuthenticatePlayerPrefs
    {
        private const string KeysPrefix = "flying_acorn_soil_";
        internal static UserInfo UserInfoInstance;

        public static string AppID
        {
            get => PlayerPrefs.GetString(KeysPrefix + "app_id", Constants.DemoAppID);
            set => PlayerPrefs.SetString(KeysPrefix + "app_id", value);
        }

        public static string SDKToken
        {
            get => PlayerPrefs.GetString(KeysPrefix + "sdk_token", Constants.DemoAppSDKToken);

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

        internal static string GetKeysPrefix()
        {
            if (!string.IsNullOrEmpty(AppID)) return $"{KeysPrefix}{AppID}_";
            Debug.LogWarning("AppID is not set. Please set AppID before using AuthenticatePlayerPrefs.");
            return string.Empty;
        }
    }
}