// ReSharper disable InconsistentNaming

using System;
using System.Collections.Generic;
using FlyingAcorn.Soil.Core.Data;
using JetBrains.Annotations;
using UnityEngine;
// ReSharper disable UnassignedField.Global

// ReSharper disable UnusedMember.Global

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
        public string app;
        public string app_id;
        public string bundle;

        public string country;
        public string created_at;
        public string current_build;
        public string name;
        public Properties properties;
        public string username;
        public string uuid;

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

            public static Dictionary<string, object> GeneratePropertiesFromDevice()
            {
                return new Dictionary<string, object>
                {
                    { $"{KeysPrefix}platform", Application.platform.ToString() },
                    { $"{KeysPrefix}version", Application.version },
                    { $"{KeysPrefix}build", DataUtils.GetUserBuildNumber() },
                    { $"{KeysPrefix}build_time", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") },
                    { $"{KeysPrefix}scripting_backend", DataUtils.GetScriptingBackend() },
                    { $"{KeysPrefix}unity_version", Application.unityVersion },
                    { $"{KeysPrefix}store_name", SystemInfo.graphicsDeviceName },
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
                    { $"{KeysPrefix}graphics_device_version", SystemInfo.graphicsDeviceVersion }
                };
            }

            public Dictionary<string,object> ToDictionary()
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
    }
}