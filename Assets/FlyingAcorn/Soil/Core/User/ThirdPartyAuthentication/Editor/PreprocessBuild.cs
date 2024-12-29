using FlyingAcorn.Soil.Core.Data.BuildData.Editor;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace FlyingAcorn.Soil.Core.User.ThirdPartyAuthentication.Editor
{
    public class PreprocessBuild : IPreprocessBuildWithReport
    {
        public int callbackOrder { get; }

        public void OnPreprocessBuild(BuildReport report)
        {
            if (!UserPlayerPrefs.DeepLinkActivated)
            {
                Debug.LogWarning("[FABuildTools] Ignoring deep link since it is disabled");
                return;
            }

#if UNITY_ANDROID
            HandleAndroidDeeplink(report);
#endif
        }

#if UNITY_ANDROID
        /*
         *
         *
        <intent-filter>
            <action android:name="android.intent.action.VIEW"/>
            <category android:name="android.intent.category.DEFAULT"/>
            <category android:name="android.intent.category.BROWSABLE"/>
            <data android:scheme="com.flyingacorn.soilsdk"/>
            <data android:scheme="com.flyingacorn.soilsdk" android:host="auth"/>
        </intent-filter>
         */
        private void HandleAndroidDeeplink(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.Android)
                return;
            var configurations = Resources.Load<ThirdPartySettings>(SocialAuthentication.AndroidSettingName);
            if (!configurations || string.IsNullOrEmpty(configurations.RedirectUri))
            {
                Debug.Log("[FABuildTools] Redirect URI not found in ThirdPartySettings");
                return;
            }
            DeeplinkTools.AddAndroidDeeplink(configurations.RedirectUri);
        }
#endif
    }
}