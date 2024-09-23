using FlyingAcorn.Soil.Core.Data;
using Newtonsoft.Json;
using UnityEngine;

namespace FlyingAcorn.Soil.Core.User
{
    public static class AuthenticatePlayerPrefs
    {
        internal static UserInfo UserInfoInstance;

        private static readonly string TokenDataKey = $"{GetKeysPrefix()}token_data";
        private static readonly string UserInfoKey = $"{GetKeysPrefix()}player_info";
        private static readonly string AppIDKey = $"{GetKeysPrefix()}app_id";
        private static readonly string SDKTokenKey = $"{GetKeysPrefix()}sdk_token";

        internal static UserInfo UserInfo
        {
            get
            {
                if (UserInfoInstance != null) return UserInfoInstance;
                UserInfoInstance =
                    JsonConvert.DeserializeObject<UserInfo>(PlayerPrefs.GetString(UserInfoKey));
                return UserInfoInstance;
            }
            set
            {
                PlayerPrefs.SetString(UserInfoKey, JsonConvert.SerializeObject(value));
                UserInfoInstance = value;
            }
        }

        public static TokenData TokenData
        {
            get => JsonConvert.DeserializeObject<TokenData>(PlayerPrefs.GetString(TokenDataKey));
            set => PlayerPrefs.SetString(TokenDataKey, JsonConvert.SerializeObject(value));
        }
        
        public static string AppID
        {
            get => PlayerPrefs.GetString(AppIDKey, Constants.DemoAppID);
            set => PlayerPrefs.SetString(AppIDKey, value);
        }

        public static string SDKToken
        {
            get => PlayerPrefs.GetString(SDKTokenKey, Constants.DemoAppSDKToken);
            set => PlayerPrefs.SetString(SDKTokenKey, value);
        }


        internal static string GetKeysPrefix()
        {
            return $"flying_acorn_soil_{AppID}_";
        }
    }
}