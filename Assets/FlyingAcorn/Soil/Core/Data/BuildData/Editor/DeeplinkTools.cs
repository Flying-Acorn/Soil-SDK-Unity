#if UNITY_ANDROID
using System.Xml;
#endif

#if UNITY_IOS
using UnityEditor.iOS.Xcode;
#endif
using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using System.IO;
using UnityEngine;

namespace FlyingAcorn.Soil.Core.Data.BuildData.Editor
{
    public static class DeeplinkTools
    {
#if UNITY_ANDROID
        internal static void AddAndroidDeeplink(string fullLink)
        {
            const string androidManifestPath = "Plugins/Android/AndroidManifest.xml";
            var fullPath = Path.Combine(Application.dataPath, androidManifestPath);

            var scheme = fullLink.Split(':')[0];
            var subScheme = "";
            if (fullLink.Contains("//"))
                subScheme = fullLink.Split('/')[2];
            else if (fullLink.Contains('/'))
            {
                subScheme = fullLink.Split('/')[1];
            }

            subScheme = subScheme.Split('?')[0];

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
                if (schemeAttr.Value != scheme)
                    continue;
                var hostAttr = data.Attributes["android:host"];
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

#if UNITY_IOS
        internal static void AddIOSDeeplink(BuildReport report, string fullUrl)
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

            urlSchemes.AddString(new Uri(fullUrl).Scheme);

            // Save all changes.
            File.WriteAllText(plistPath, plist.WriteToString());
        }
#endif
    }
}