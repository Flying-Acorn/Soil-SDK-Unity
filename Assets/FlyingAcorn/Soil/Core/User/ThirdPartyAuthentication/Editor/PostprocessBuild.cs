using System.Linq;
using FlyingAcorn.Analytics.BuildData.Editor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using static FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Data.Constants;

namespace FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Editor
{
    public class PostprocessBuild : IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
            HandleDeepLink(report);
        }

        private static void HandleDeepLink(BuildReport report)
        {
            if (!UserPlayerPrefs.DeepLinkActivated)
            {
                Debug.LogWarning("[FABuildTools] Ignoring deep link since it is disabled");
                return;
            }

            var configurations = Resources.LoadAll<ThirdPartySettings>("ThirdParties").ToList();
            var googleIOS = configurations.Find(settings =>
                settings.ThirdParty == ThirdParty.google &&
                settings.Platform == RuntimePlatform.IPhonePlayer);
            if (!googleIOS)
                throw new BuildFailedException("Third party settings not found");

#if UNITY_IOS
            DeeplinkTools.AddIOSDeeplink(report, googleIOS.RedirectUri);
#endif
        }
    }
}