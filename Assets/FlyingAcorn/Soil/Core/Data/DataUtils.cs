using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FlyingAcorn.Analytics;
using UnityEngine;
using static FlyingAcorn.Soil.Core.Data.Constants;

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

        public static Store GetStore()
        {
            if (!_buildSettings)
                _buildSettings = Resources.Load<BuildData.BuildData>("Build_Settings");
            var storeName = Store.Unknown;
            if (_buildSettings && _buildSettings.StoreName != Store.Unknown)
                storeName = _buildSettings.StoreName;
            if (storeName != Store.Unknown) return storeName;
            if (!Application.isEditor)
                MyDebug.LogError("Store name is not set in BuildData");
            return Store.Unknown;
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

        public static RegionSettings GetSettingsForTimeZone()
        {
            var tz = System.TimeZoneInfo.Local;

            var utcOffset = tz.BaseUtcOffset;
            var standardName = tz.StandardName ?? string.Empty;

            var region = Region.WW;

            if (utcOffset == TimeSpan.FromHours(3.5) || utcOffset == TimeSpan.FromHours(4.5) ||
                standardName.Contains("Iran", StringComparison.OrdinalIgnoreCase) ||
                standardName.Contains("Tehran", StringComparison.OrdinalIgnoreCase))
            {
                region = Region.IR;
            }

            MyDebug.Verbose($"TimeZone - Offset: {utcOffset}, StandardName: {standardName}, Mapped Region: {region}");

            return APIPerRegion.Find(x => x.Region == region) ?? new RegionSettings
            {
                Region = region,
                ApiUrl = FallBackApiUrl
            };
        }

        internal static string FindApiUrl()
        {
            switch (GetStore())
            {
                case Store.CafeBazaar:
                case Store.Myket:
                    return IRApiUrl;
                case Store.LandingPage:
                case Store.Unknown:
                case Store.BetaChannel:
                case Store.Postman:
                case Store.GooglePlay:
                case Store.AppStore:
                case Store.Github:
                default:
                    break;
            }

            var timezoneSettings = GetSettingsForTimeZone();
            
            if (SoilServices.UserInfo?.country == null)
            {
                MyDebug.Verbose($"No user info available, using timezone-based URL: {timezoneSettings.ApiUrl}");
                return timezoneSettings.ApiUrl ?? FallBackApiUrl;
            }
            
            var region = SoilServices.UserInfo.country;
            var regionEnum = Enum.TryParse(region, true, out Region regionParsed) ? regionParsed : Region.WW;
            var settingForCountry = APIPerRegion.Find(x => x.Region == regionEnum);
            
            if (settingForCountry == null || settingForCountry.Region == Region.WW)
            {
                MyDebug.Verbose($"User region is WW or not found ({regionEnum}), preferring timezone-based URL: {timezoneSettings.ApiUrl}");
                return timezoneSettings.ApiUrl ?? FallBackApiUrl;
            }
            
            MyDebug.Verbose($"Using country-specific API URL for region {regionEnum}: {settingForCountry.ApiUrl}");
            return settingForCountry.ApiUrl ?? FallBackApiUrl;
        }
    }
}