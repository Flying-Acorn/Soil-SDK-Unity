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
            Debug.LogError("Store name is not set in Build_Settings. Please set it.");
            storeName = "Unknown";
            return storeName;
        }
    }
}