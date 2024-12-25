using System;
using System.IO;
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
#if UNITY_IOS
            HandleIOSDeeplink(report);
#endif
        }

#if UNITY_IOS
        private void HandleIOSDeeplink(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS)
                return;

            var plistPath = report.summary.outputPath + "/Info.plist";
            var plist = new PlistDocument();
            plist.ReadFromString(File.ReadAllText(plistPath));

            // Register ios URL scheme for external apps to launch this app.
            var urlTypes = !plist.root.values.ContainsKey("CFBundleURLTypes")
                ? plist.root.CreateArray("CFBundleURLTypes")
                : plist.root["CFBundleURLTypes"].AsArray();
            var urlTypeDict = urlTypes.AddDict();
            urlTypeDict.SetString("CFBundleURLName", "");
            urlTypeDict.SetString("CFBundleTypeRole", "Editor");

            var urlSchemes = urlTypeDict.CreateArray("CFBundleURLSchemes");

            var configurations = Resources.Load<ThirdPartySettings>(SocialAuthentication.IOSSettingName);
            if (configurations == null)
                throw new BuildFailedException("Third party settings not found");
            urlSchemes.AddString(new Uri(configurations.RedirectUri).Scheme);

            // Save all changes.
            File.WriteAllText(plistPath, plist.WriteToString());
        }
#endif
    }
}