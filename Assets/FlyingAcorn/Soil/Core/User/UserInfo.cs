// ReSharper disable InconsistentNaming
// ReSharper disable ArrangeThisQualifier
// ReSharper disable ParameterHidesMember
// ReSharper disable UnassignedField.Global
// ReSharper disable UnusedMember.Global

using System;
using System.Collections.Generic;
using FlyingAcorn.Analytics;
using FlyingAcorn.Soil.Core.Data;
using FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Data;
using FlyingAcorn.Soil.RemoteConfig.ABTesting;
using JetBrains.Annotations;
using Newtonsoft.Json;
using UnityEngine;

namespace FlyingAcorn.Soil.Core.User
{
    /* json schema:
     {
           "username": "username",
           "uuid": "uuid",
           "name": "DentalGuy53",
           "app": "NumberChain",
           "app_id": "uuid",
           "bundle": "com.test1.sa",
           "current_build": "4.0.3",
           "created_at": "2024-09-17 09:41:28.697506+00:00",
           "country": "FI",
           "avatar_asset": "avatar_asset",
           "properties": {
               "flyingacorn_platform": "Android",
               "flyingacorn_version": "4.0.3",
               "flyingacorn_build": "164",
               "flyingacorn_store_name": "AppStore",
               "flyingacorn_package": "com.test1.sa"
           }
       }
     */

    [UsedImplicitly]
    [Serializable]
    public class UserInfo
    {
        [JsonProperty] internal string bundle;
        [JsonProperty] internal string country;
        [JsonProperty] internal string avatar_asset;
        [JsonProperty] internal string created_at;
        [JsonProperty] internal string current_build;
        [JsonProperty] internal string name;
        [JsonProperty] internal Properties properties;
        [JsonProperty] internal Dictionary<string, object> custom_properties;
        [JsonProperty] internal string username;
        [JsonProperty] internal string uuid;
        [JsonProperty] internal List<AppParty> linkable_parties;

        public UserInfo ChangeUser(UserInfo newUser)
        {
            newUser.Validate();
            uuid = newUser.uuid;
            return RecordAvatarAsset(newUser.avatar_asset).RecordName(newUser.name).RecordUsername(newUser.username);
        }

        public UserInfo RecordCustomProperty(string key, object value)
        {
            this.custom_properties[key] = value;
            return this;
        }

        public UserInfo RecordAvatarAsset(string avatarAsset)
        {
            this.avatar_asset = avatarAsset;
            return this;
        }

        public UserInfo RecordName(string name)
        {
            this.name = name;
            return this;
        }

        public UserInfo RecordUsername(string username)
        {
            this.username = username;
            return this;
        }

        public Dictionary<string, object> GetChangedFields(UserInfo userInfo)
        {
            var changedFields = new Dictionary<string, object>();
            foreach (var propertyInfo in userInfo.GetType().GetAllFields())
            {
                var value = propertyInfo.GetValue(userInfo);
                var isNullOrEmpty = value == null || string.IsNullOrEmpty(value.ToString());
                if (isNullOrEmpty)
                    continue;

                if (!propertyInfo.GetValue(this).Equals(value))
                    changedFields.Add(propertyInfo.Name, value);
            }

            return changedFields;
        }

        [UsedImplicitly]
        [Serializable]
        public class Properties
        {
            private const string KeysPrefix = "flyingacorn_";
            public string flyingacorn_build;
            public string flyingacorn_build_time;
            public string flyingacorn_device_model;
            public string flyingacorn_device_name;
            public string flyingacorn_device_type;
            public string flyingacorn_device_unique_id;
            public string flyingacorn_graphics_device_id;
            public string flyingacorn_graphics_device_name;
            public string flyingacorn_graphics_device_type;
            public string flyingacorn_graphics_device_vendor;
            public string flyingacorn_graphics_device_vendor_id;
            public string flyingacorn_graphics_device_version;
            public string flyingacorn_package;
            public string flyingacorn_platform;
            public string flyingacorn_scripting_backend;
            public string flyingacorn_store_name;
            public string flyingacorn_unity_version;
            public string flyingacorn_version;
            public string flyingacorn_cohort_id;

            public static Dictionary<string, object> GeneratePropertiesDynamicPlayerProperties()
            {
                return new Dictionary<string, object>
                {
                    { $"{KeysPrefix}platform", Application.platform.ToString() },
                    { $"{KeysPrefix}version", Application.version },
                    { $"{KeysPrefix}build", DataUtils.GetUserBuildNumber() },
                    { $"{KeysPrefix}build_time", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") },
                    { $"{KeysPrefix}scripting_backend", DataUtils.GetScriptingBackend() },
                    { $"{KeysPrefix}unity_version", Application.unityVersion },
                    { $"{KeysPrefix}store_name", DataUtils.GetStore().ToString() },
                    { $"{KeysPrefix}package", Application.identifier },
                    { $"{KeysPrefix}device_model", SystemInfo.deviceModel },
                    { $"{KeysPrefix}device_type", SystemInfo.deviceType.ToString() },
                    { $"{KeysPrefix}device_name", SystemInfo.deviceName },
                    { $"{KeysPrefix}device_unique_id", SystemInfo.deviceUniqueIdentifier },
                    { $"{KeysPrefix}graphics_device_name", SystemInfo.graphicsDeviceName },
                    { $"{KeysPrefix}graphics_device_type", SystemInfo.graphicsDeviceType.ToString() },
                    { $"{KeysPrefix}graphics_device_vendor", SystemInfo.graphicsDeviceVendor },
                    { $"{KeysPrefix}graphics_device_id", SystemInfo.graphicsDeviceID },
                    { $"{KeysPrefix}graphics_device_vendor_id", SystemInfo.graphicsDeviceVendorID },
                    { $"{KeysPrefix}graphics_device_version", SystemInfo.graphicsDeviceVersion },
                    { $"{KeysPrefix}cohort_id", ABTestingPlayerPrefs.GetLastExperimentId() },
                    { $"{KeysPrefix}analytics_debug_mode", AnalyticsPlayerPrefs.UserDebugMode },
                    { $"{KeysPrefix}installation_version", AnalyticsPlayerPrefs.InstallationVersion },
                    { $"{KeysPrefix}installation_build", AnalyticsPlayerPrefs.InstallationBuild }
                };
            }

            public Dictionary<string, object> ToDictionary()
            {
                var allFields = GetType().GetFields();
                var result = new Dictionary<string, object>();
                foreach (var field in allFields)
                {
                    if (field.GetValue(this) != null)
                        result[field.Name] = field.GetValue(this);
                }

                return result;
            }
        }

        public void Validate()
        {
            if (string.IsNullOrEmpty(this.uuid))
                throw new Exception("uuid is required");
            if (string.IsNullOrEmpty(this.username))
                throw new Exception("username is required");
            if (string.IsNullOrEmpty(this.name))
                throw new Exception("name is required");
        }
    }
}