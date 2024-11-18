using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

namespace FlyingAcorn.Soil.Core.Data
{
    public static class DataUtils
    {
        private static BuildData.BuildData _buildSettings;

        internal static string GetScriptingBackend()
        {
            if (!_buildSettings)
                _buildSettings = Resources.Load<BuildData.BuildData>("Build_Settings");
            var scriptingBackend = "Unknown";
            if (_buildSettings && !string.IsNullOrEmpty(_buildSettings.ScriptingBackend))
                scriptingBackend = _buildSettings.ScriptingBackend;

            return scriptingBackend;
        }

        public static string GetUserBuildNumber()
        {
            if (!_buildSettings)
                _buildSettings = Resources.Load<BuildData.BuildData>("Build_Settings");
            var build = "Unknown";
            if (_buildSettings && !string.IsNullOrEmpty(_buildSettings.BuildNumber))
                build = _buildSettings.BuildNumber;

            return build;
        }

        public static string GetStoreName()
        {
            if (!_buildSettings)
                _buildSettings = Resources.Load<BuildData.BuildData>("Build_Settings");
            string storeName = null;
            if (_buildSettings && !string.IsNullOrEmpty(_buildSettings.StoreName))
                storeName = _buildSettings.StoreName;

            if (!string.IsNullOrEmpty(storeName)) return storeName;
            Debug.LogWarning("Store name is not set in Build_Settings. Please set it.");
            storeName = "Unknown";
            return storeName;
        }
        
        public static bool HasIAP()
        {
            if (!_buildSettings)
                _buildSettings = Resources.Load<BuildData.BuildData>("Build_Settings");
            return _buildSettings && _buildSettings.hasIAP;
        }

        public static IEnumerable<FieldInfo> GetAllFields(this Type t)
        {
            if (t == null)
                return Enumerable.Empty<FieldInfo>();

            const BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic |
                                       BindingFlags.Static | BindingFlags.Instance |
                                       BindingFlags.DeclaredOnly;
            return t.GetFields(flags).Concat(GetAllFields(t.BaseType));
        }
    }
}