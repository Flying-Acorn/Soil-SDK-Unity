// ReSharper disable InconsistentNaming
// ReSharper disable ArrangeThisQualifier
// ReSharper disable ParameterHidesMember
// ReSharper disable UnassignedField.Global
// ReSharper disable UnusedMember.Global

using System;
using System.Collections.Generic;
using FlyingAcorn.Analytics;
using FlyingAcorn.Soil.Core.Data;
using JetBrains.Annotations;
using Newtonsoft.Json;
using UnityEngine;
using TimeZoneInfo = FlyingAcorn.Soil.Core.Data.TimeZoneInfo;

namespace FlyingAcorn.Soil.Core.User
{
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

        [CanBeNull]
        internal string RealtimeCountry()
        {
            return properties is { flyingacorn_country_realtime: not null }
                ? properties.flyingacorn_country_realtime
                : null;
        }

        internal UserInfo ChangeUser(UserInfo newUser)
        {
            newUser.Validate();
            uuid = newUser.uuid;
            return RecordAvatarAsset(newUser.avatar_asset).RecordName(newUser.name).RecordUsername(newUser.username);
        }

        internal UserInfo ChangeRegionInfo(UserInfo comingUser)
        {
            if (comingUser.country != null)
                country = comingUser.country;
            var newRealtimeCountry = comingUser.RealtimeCountry();
            if (newRealtimeCountry == null) return this;
            properties ??= new Properties();
            properties.flyingacorn_country_realtime = newRealtimeCountry;
            return this;
        }

        /// <summary>
        /// Creates a copy of this UserInfo instance for safe modification.
        /// Uses shallow copy for server-managed fields (properties, linkable_parties)
        /// to preserve reference equality for change detection.
        /// </summary>
        /// <returns>A new UserInfo instance with the same data</returns>
        internal UserInfo Copy()
        {
            // Create a deep copy using JSON serialization
            var json = JsonConvert.SerializeObject(this);
            var copy = JsonConvert.DeserializeObject<UserInfo>(json);
            
            // Preserve references to server-managed complex objects
            // so they won't be detected as "changed" during comparison
            copy.properties = this.properties;
            copy.linkable_parties = this.linkable_parties;
            
            return copy;
        }

        /// <summary>
        /// Records a custom property. 
        /// WARNING: This modifies the current instance. When updating user info,
        /// use UserApiHandler.UpdatePlayerInfo() builder methods instead.
        /// </summary>
        /// <param name="key">Property key</param>
        /// <param name="value">Property value</param>
        /// <returns>This instance (for method chaining)</returns>
        internal UserInfo RecordCustomProperty(string key, object value)
        {
            // Prevent users from overwriting reserved SDK properties
            if (key.StartsWith(Constants.PropertyKeyPrefix))
            {
                throw new ArgumentException($"Property key '{key}' is reserved for SDK use. Custom property keys cannot start with 'flyingacorn_'.", nameof(key));
            }
            
            var newProps = new Dictionary<string, object>(this.custom_properties ?? new Dictionary<string, object>());
            newProps[key] = value;
            this.custom_properties = newProps;
            return this;
        }

        /// <summary>
        /// Records an internal SDK property (flyingacorn_*).
        /// This method bypasses the restriction on flyingacorn_ prefixed keys.
        /// For internal SDK use only.
        /// WARNING: This modifies the current instance.
        /// </summary>
        /// <param name="key">Property key (can start with flyingacorn_)</param>
        /// <param name="value">Property value</param>
        /// <returns>This instance (for method chaining)</returns>
        internal UserInfo RecordInternalProperty(string key, object value)
        {
            // If you want to enforce the prefix, uncomment the following lines:
            if (!key.StartsWith(Constants.PropertyKeyPrefix))
                key = Constants.PropertyKeyPrefix + key;
            var newProps = new Dictionary<string, object>(this.custom_properties ?? new Dictionary<string, object>());
            newProps[key] = value;
            this.custom_properties = newProps;
            return this;
        }

        /// <summary>
        /// Records an avatar asset URL.
        /// WARNING: This modifies the current instance. When updating user info,
        /// use UserApiHandler.UpdatePlayerInfo() builder methods instead.
        /// </summary>
        /// <param name="avatarAsset">Avatar asset URL</param>
        /// <returns>This instance (for method chaining)</returns>
        internal UserInfo RecordAvatarAsset(string avatarAsset)
        {
            this.avatar_asset = avatarAsset;
            return this;
        }

        /// <summary>
        /// Records a display name.
        /// WARNING: This modifies the current instance. When updating user info,
        /// use UserApiHandler.UpdatePlayerInfo() builder methods instead.
        /// </summary>
        /// <param name="name">Display name</param>
        /// <returns>This instance (for method chaining)</returns>
        internal UserInfo RecordName(string name)
        {
            this.name = name;
            return this;
        }

        /// <summary>
        /// Records a username.
        /// WARNING: This modifies the current instance. When updating user info,
        /// use UserApiHandler.UpdatePlayerInfo() builder methods instead.
        /// </summary>
        /// <param name="username">Username</param>
        /// <returns>This instance (for method chaining)</returns>
        internal UserInfo RecordUsername(string username)
        {
            this.username = username;
            return this;
        }

        internal Dictionary<string, object> GetChangedFields(UserInfo userInfo)
        {
            var changedFields = new Dictionary<string, object>();
            foreach (var propertyInfo in userInfo.GetType().GetAllFields())
            {
                var value = propertyInfo.GetValue(userInfo);
                
                // Skip custom_properties field - we'll handle it separately
                if (propertyInfo.Name == "custom_properties")
                    continue;
                    
                var isNullOrEmpty = value == null || string.IsNullOrEmpty(value.ToString());
                if (isNullOrEmpty)
                    continue;

                var objectValue = propertyInfo.GetValue(this);
                if (objectValue == null || !objectValue.Equals(value))
                    changedFields.Add(propertyInfo.Name, value);
            }
            
            // Merge custom_properties into properties for server compatibility
            // Only include properties that have actually changed
            if (userInfo.custom_properties != null && userInfo.custom_properties.Count > 0)
            {
                // Get or create the properties dictionary
                Dictionary<string, object> propertiesDict = null;
                
                // Get cached internal properties for comparison
                var cachedInternalProps = UserPlayerPrefs.InternalProperties;
                
                // Check each custom property to see if it has changed
                foreach (var kvp in userInfo.custom_properties)
                {
                    // Skip invalid keys or null values - they should not be included in properties
                    if (string.IsNullOrEmpty(kvp.Key) || kvp.Value == null)
                        continue;
                        
                    // Compare with existing custom_properties
                    bool hasChanged = true;
                    
                    // For internal properties (flyingacorn_*), check against cached values
                    if (kvp.Key.StartsWith(Constants.PropertyKeyPrefix))
                    {
                        if (cachedInternalProps != null && cachedInternalProps.ContainsKey(kvp.Key))
                        {
                            var cachedValue = cachedInternalProps[kvp.Key];
                            if (cachedValue != null && cachedValue.Equals(kvp.Value))
                            {
                                hasChanged = false;
                            }
                        }
                    }
                    // For regular custom properties, check against in-memory custom_properties
                    else if (this.custom_properties != null && this.custom_properties.ContainsKey(kvp.Key))
                    {
                        var existingValue = this.custom_properties[kvp.Key];
                        // Check if the value is actually different
                        if (existingValue != null && existingValue.Equals(kvp.Value))
                        {
                            hasChanged = false;
                        }
                    }
                    
                    // Only add changed properties
                    if (hasChanged)
                    {
                        // Lazily initialize propertiesDict only if we have changes
                        if (propertiesDict == null)
                        {
                            if (changedFields.ContainsKey(nameof(properties)))
                            {
                                propertiesDict = changedFields[nameof(properties)] as Dictionary<string, object> 
                                               ?? new Dictionary<string, object>();
                            }
                            else
                            {
                                // Initialize empty dictionary - we only want to send changed properties
                                propertiesDict = new Dictionary<string, object>();
                            }
                        }
                        
                        propertiesDict[kvp.Key] = kvp.Value;
                        
                        // Cache internal properties for future comparison
                        if (kvp.Key.StartsWith(Constants.PropertyKeyPrefix))
                        {
                            cachedInternalProps[kvp.Key] = kvp.Value;
                            UserPlayerPrefs.InternalProperties = cachedInternalProps;
                        }
                    }
                }
                
                // Only update changedFields if we actually have changed properties
                if (propertiesDict != null)
                {
                    changedFields[nameof(properties)] = propertiesDict;
                }
            }

            return changedFields;
        }

        [UsedImplicitly]
        [Serializable]
        public class Properties
        {
            internal const string KeysPrefix = "flyingacorn_";
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
            public string flyingacorn_country_realtime;

            public static Dictionary<string, object> GeneratePropertiesDynamicPlayerProperties()
            {
                var timezone = JsonConvert.DeserializeObject<TimeZoneInfo>(JsonConvert.SerializeObject(System.TimeZoneInfo.Local));
                return new Dictionary<string, object>
                {
                    { Constants.PlatformKey, Application.platform.ToString() },
                    { Constants.VersionKey, Application.version },
                    { Constants.BuildKey, DataUtils.GetUserBuildNumber() },
                    { Constants.BuildTimeKey, DataUtils.GetBuildDate() },
                    { Constants.ScriptingBackendKey, DataUtils.GetScriptingBackend() },
                    { Constants.UnityVersionKey, Application.unityVersion },
                    { Constants.StoreNameKey, DataUtils.GetStore().ToString() },
                    { Constants.PackageKey, Application.identifier },
                    { Constants.DeviceModelKey, SystemInfo.deviceModel },
                    { Constants.DeviceTypeKey, SystemInfo.deviceType.ToString() },
                    { Constants.DeviceNameKey, SystemInfo.deviceName },
                    { Constants.DeviceUniqueIdKey, SystemInfo.deviceUniqueIdentifier },
                    { Constants.GraphicsDeviceNameKey, SystemInfo.graphicsDeviceName },
                    { Constants.GraphicsDeviceTypeKey, SystemInfo.graphicsDeviceType.ToString() },
                    { Constants.GraphicsDeviceVendorKey, SystemInfo.graphicsDeviceVendor },
                    { Constants.GraphicsDeviceIdKey, SystemInfo.graphicsDeviceID },
                    { Constants.GraphicsDeviceVendorIdKey, SystemInfo.graphicsDeviceVendorID },
                    { Constants.GraphicsDeviceVersionKey, SystemInfo.graphicsDeviceVersion },
                    { Constants.AnalyticsDebugModeKey, AnalyticsPlayerPrefs.UserDebugMode },
                    { Constants.InstallationVersionKey, AnalyticsPlayerPrefs.InstallationVersion },
                    { Constants.InstallationBuildKey, AnalyticsPlayerPrefs.InstallationBuild },
                    { Constants.TimezoneKey, timezone }
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