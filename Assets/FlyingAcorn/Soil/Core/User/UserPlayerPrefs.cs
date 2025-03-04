using FlyingAcorn.Soil.Core.Data;
using Newtonsoft.Json;
using UnityEngine;

namespace FlyingAcorn.Soil.Core.User
{
    public static class UserPlayerPrefs
    {
        internal static UserInfo UserInfoInstance => _userInfoInstance ?? UserInfo;

        private static UserInfo _userInfoInstance;

        private static readonly string TokenDataKey = $"{GetKeysPrefix()}token_data";
        private static readonly string UserInfoKey = $"{GetKeysPrefix()}player_info";
        private static readonly string AppIDKey = $"{GetKeysPrefix()}app_id";
        private static readonly string SDKTokenKey = $"{GetKeysPrefix()}sdk_token";

        internal static UserInfo UserInfo
        {
            get
            {
                if (_userInfoInstance != null) return _userInfoInstance;
                _userInfoInstance = JsonConvert.DeserializeObject<UserInfo>(PlayerPrefs.GetString(UserInfoKey, "")) ??
                                    new UserInfo();
                return _userInfoInstance;
            }
            set
            {
                PlayerPrefs.SetString(UserInfoKey, JsonConvert.SerializeObject(value));
                _userInfoInstance = value;
            }
        }

        public static TokenData TokenData
        {
            get => JsonConvert.DeserializeObject<TokenData>(PlayerPrefs.GetString(TokenDataKey));
            set => PlayerPrefs.SetString(TokenDataKey, JsonConvert.SerializeObject(value));
        }

        public static string AppID
        {
            get
            {
                var settings = Resources.Load<SDKSettings>(nameof(SDKSettings));
                var appIdIsValid = settings && !string.IsNullOrEmpty(settings.AppID);
                return appIdIsValid ? settings.AppID : PlayerPrefs.GetString(AppIDKey, Constants.DemoAppID);
            }
        }

        public static string SDKToken
        {
            get
            {
                var settings = Resources.Load<SDKSettings>(nameof(SDKSettings));
                var sdkTokenIsValid = settings && !string.IsNullOrEmpty(settings.SdkToken);
                return sdkTokenIsValid
                    ? settings.SdkToken
                    : PlayerPrefs.GetString(SDKTokenKey, Constants.DemoAppSDKToken);
            }
        }

        public static bool DeepLinkActivated
        {
            get
            {
                var settings = Resources.Load<SDKSettings>(nameof(SDKSettings));
                return settings && settings.DeepLinkEnabled;
            }
        }
        
        public static int RequestTimeout
        {
            get
            {
                var settings = Resources.Load<SDKSettings>(nameof(SDKSettings));
                return settings ? settings.RequestTimeout : Constants.DefaultTimeout;
            }
        }


        internal static string GetKeysPrefix()
        {
            return $"FA_soil_{AppID}_";
        }
    }
}