using System.Collections.Generic;
using UnityEngine;

namespace FlyingAcorn.Soil.Core.Data
{
    public static class Utils
    {
        private const string KeysPrefix = "flyingacorn_";

        public static Dictionary<string, object> GenerateUserProperties()
        {
            return new Dictionary<string, object>
            {
                { $"{KeysPrefix}platform", Application.platform.ToString() },
                { $"{KeysPrefix}version", Application.version },
                // { $"{KeysPrefix}build",  },
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
    }
}