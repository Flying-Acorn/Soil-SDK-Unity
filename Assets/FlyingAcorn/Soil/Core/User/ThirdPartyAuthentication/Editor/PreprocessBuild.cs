using System.IO;
using System.Xml;
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
            const string androidManifestPath = "Plugins/Android/AndroidManifest.xml";
            var fullPath = Path.Combine(Application.dataPath, androidManifestPath);

            var scheme = configurations.RedirectUri.Split(':')[0];
            string subScheme = null;
            if (configurations.RedirectUri.Contains('/'))
            {
                subScheme = configurations.RedirectUri.Split('/')[1];
                subScheme = subScheme.Split('?')[0];
            }

            var doc = new XmlDocument();
            doc.Load(fullPath);
            var manifest = doc.DocumentElement;
            var application = manifest["application"];
            var activity = application["activity"];
            var namespaceOfPrefix = application.GetNamespaceOfPrefix("android");
            // if scheme already exists, do not add it again
            var intentFilters = doc.GetElementsByTagName("intent-filter");
            foreach (XmlNode intentFilter in intentFilters)
            {
                var data = intentFilter["data"];
                var schemeAttr = data?.Attributes["android:scheme"];
                if (schemeAttr == null)
                    continue;
                if (schemeAttr.Value == scheme)
                    return;
                var hostAttr = data.Attributes["android:host"];
                if (hostAttr == null)
                    continue;
                if (hostAttr.Value == subScheme)
                    return;
            }

            var intentFilterNode = doc.CreateElement("intent-filter");
            var actionNode = doc.CreateElement("action");
            actionNode.SetAttribute("name", namespaceOfPrefix, "android.intent.action.VIEW");
            intentFilterNode.AppendChild(actionNode);
            var categoryNode = doc.CreateElement("category");
            categoryNode.SetAttribute("name", namespaceOfPrefix, "android.intent.category.DEFAULT");
            intentFilterNode.AppendChild(categoryNode);
            categoryNode = doc.CreateElement("category");
            categoryNode.SetAttribute("name", namespaceOfPrefix, "android.intent.category.BROWSABLE");
            intentFilterNode.AppendChild(categoryNode);
            var dataNode = doc.CreateElement("data");
            dataNode.SetAttribute("scheme", namespaceOfPrefix, scheme);
            if (subScheme != null)
                dataNode.SetAttribute("host", namespaceOfPrefix, subScheme);
            intentFilterNode.AppendChild(dataNode);
            activity.AppendChild(intentFilterNode);

            doc.Save(fullPath);

            Debug.Log($"[FABuildTools] Added intent filter for scheme {scheme} and host {subScheme}");
        }
#endif
    }
}