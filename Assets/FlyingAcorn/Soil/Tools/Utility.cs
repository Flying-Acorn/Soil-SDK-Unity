using UnityEngine;

namespace FlyingAcorn.Tools
{
    internal static class Utility
    {
        public static string GetUserBuildNumber()
        {
            var buildSettings = Resources.Load<BuildData>("Build_Settings");
            var build = "Unknown";
            if (buildSettings != null && !string.IsNullOrEmpty(buildSettings.BuildNumber))
                build = buildSettings.BuildNumber;

            return build;
        }

        public static string GetBundleId()
        {
            return Application.identifier;
        }

    }
}