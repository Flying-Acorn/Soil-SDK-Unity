using System;
using System.IO;
using FlyingAcorn.Soil.Core.Data.BuildData.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Editor
{
    public class PostprocessBuild : IPostprocessBuildWithReport
    {
        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (!UserPlayerPrefs.DeepLinkActivated)
            {
                Debug.LogWarning("[FABuildTools] Ignoring deep link since it is disabled");
                return;
            }
            var configurations = Resources.Load<ThirdPartySettings>(SocialAuthentication.IOSSettingName);
            if (configurations == null)
                throw new BuildFailedException("Third party settings not found");

#if UNITY_IOS
            DeeplinkTools.AddIOSDeeplink(report, configurations.RedirectUri);
#endif
        }

    }
}