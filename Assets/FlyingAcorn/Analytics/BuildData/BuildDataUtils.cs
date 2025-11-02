using UnityEngine;

namespace FlyingAcorn.Analytics.BuildData
{
    public static class BuildDataUtils
    {
        private static BuildData _buildSettings;

        public static string GetUserBuildNumber()
        {
            if (!_buildSettings)
                _buildSettings = Resources.Load<BuildData>(Constants.BuildSettingsName);
            var build = "Unknown";
            if (_buildSettings && !string.IsNullOrEmpty(_buildSettings.BuildNumber))
                build = _buildSettings.BuildNumber;

            return build;
        }

        public static string GetScriptingBackend()
        {
            if (!_buildSettings)
                _buildSettings = Resources.Load<BuildData>(Constants.BuildSettingsName);
            var scriptingBackend = "Unknown";
            if (_buildSettings && !string.IsNullOrEmpty(_buildSettings.ScriptingBackend))
                scriptingBackend = _buildSettings.ScriptingBackend;

            return scriptingBackend;
        }

        public static System.DateTime GetBuildDate()
        {
            _buildSettings = Resources.Load<BuildData>(Constants.BuildSettingsName);
            var buildTime = _buildSettings ? _buildSettings.LastBuildTime : null; // Format is "yyyy/MM/dd-HH:mm:ss"
            var buildDate = System.DateTime.TryParseExact(buildTime, "yyyy/MM/dd-HH:mm:ss", null,
                System.Globalization.DateTimeStyles.None, out var parsedDate)
                ? parsedDate
                : System.DateTime.MinValue;
            return buildDate;
        }

        internal static Constants.Store GetBuildStore()
        {
            if (!_buildSettings)
                _buildSettings = Resources.Load<BuildData>(Constants.BuildSettingsName);
            var storeName = Constants.Store.Unknown;
            if (_buildSettings && _buildSettings.StoreName != Constants.Store.Unknown)
                storeName = _buildSettings.StoreName;
            if (storeName != Constants.Store.Unknown) return storeName;
            if (!Application.isEditor)
                Debug.LogError("Store name is not set in BuildData");
            return Constants.Store.Unknown;
        }
    }
}
