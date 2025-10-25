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

namespace FlyingAcorn.Analytics.BuildData.Editor
{
    public static class DeeplinkTools
    {
#if UNITY_ANDROID
        internal static void AddAndroidDeeplink(string fullLink)
        {
            const string androidManifestPath = "Plugins/Android/AndroidManifest.xml";
            var fullPath = Path.Combine(Application.dataPath, androidManifestPath);

            if (!File.Exists(fullPath))
            {
                throw new UnityEditor.Build.BuildFailedException(
                    $"[FABuildTools] AndroidManifest.xml not found at {fullPath}. Please ensure the file exists.");
            }

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
            try
            {
                doc.Load(fullPath);
            }
            catch (Exception ex)
            {
                throw new UnityEditor.Build.BuildFailedException(
                    $"[FABuildTools] Failed to load AndroidManifest.xml: {ex.Message}");
            }

            var manifest = doc.DocumentElement;
            if (manifest == null)
            {
                throw new UnityEditor.Build.BuildFailedException(
                    "[FABuildTools] AndroidManifest.xml has no root element.");
            }

            var application = manifest["application"];
            if (application == null)
            {
                throw new UnityEditor.Build.BuildFailedException(
                    "[FABuildTools] AndroidManifest.xml has no <application> element.");
            }

            var activity = application["activity"];
            if (activity == null)
            {
                throw new UnityEditor.Build.BuildFailedException(
                    "[FABuildTools] AndroidManifest.xml has no <activity> element in <application>.");
            }

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
                if (hostAttr != null && hostAttr.Value == subScheme)
                {
                    Debug.Log($"[FABuildTools] Intent filter for scheme {scheme} and host {subScheme} already exists, skipping.");
                    return;
                }
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
            if (!string.IsNullOrEmpty(subScheme))
                dataNode.SetAttribute("host", namespaceOfPrefix, subScheme);
            intentFilterNode.AppendChild(dataNode);
            activity.AppendChild(intentFilterNode);

            try
            {
                doc.Save(fullPath);
                Debug.Log($"[FABuildTools] Added intent filter for scheme {scheme} and host {subScheme}");
            }
            catch (Exception ex)
            {
                throw new UnityEditor.Build.BuildFailedException(
                    $"[FABuildTools] Failed to save AndroidManifest.xml: {ex.Message}");
            }
        }
#endif

#if UNITY_IOS
        internal static void AddIOSDeeplink(BuildReport report, string fullUrl)
        {
            if (report.summary.platform != BuildTarget.iOS)
                return;

            var plistPath = report.summary.outputPath + "/Info.plist";
            
            if (!File.Exists(plistPath))
            {
                throw new UnityEditor.Build.BuildFailedException(
                    $"[FABuildTools] Info.plist not found at {plistPath}");
            }

            var plist = new PlistDocument();
            try
            {
                plist.ReadFromString(File.ReadAllText(plistPath));
            }
            catch (Exception ex)
            {
                throw new UnityEditor.Build.BuildFailedException(
                    $"[FABuildTools] Failed to read Info.plist: {ex.Message}");
            }

            var scheme = new Uri(fullUrl).Scheme;

            // Register ios URL scheme for external apps to launch this app.
            var urlTypes = !plist.root.values.ContainsKey("CFBundleURLTypes")
                ? plist.root.CreateArray("CFBundleURLTypes")
                : plist.root["CFBundleURLTypes"].AsArray();

            // Check if scheme already exists
            foreach (var urlType in urlTypes.values)
            {
                if (urlType is PlistElementDict dict && dict.values.ContainsKey("CFBundleURLSchemes"))
                {
                    var schemes = dict["CFBundleURLSchemes"].AsArray();
                    foreach (var existingScheme in schemes.values)
                    {
                        if (existingScheme.AsString() == scheme)
                        {
                            Debug.Log($"[FABuildTools] URL scheme {scheme} already exists in Info.plist, skipping.");
                            return;
                        }
                    }
                }
            }

            var urlTypeDict = urlTypes.AddDict();
            urlTypeDict.SetString("CFBundleURLName", $"{Application.identifier}.{scheme}");
            urlTypeDict.SetString("CFBundleTypeRole", "Editor");

            var urlSchemes = urlTypeDict.CreateArray("CFBundleURLSchemes");
            urlSchemes.AddString(scheme);

            // Save all changes.
            try
            {
                File.WriteAllText(plistPath, plist.WriteToString());
                Debug.Log($"[FABuildTools] Added URL scheme {scheme} to Info.plist");
            }
            catch (Exception ex)
            {
                throw new UnityEditor.Build.BuildFailedException(
                    $"[FABuildTools] Failed to save Info.plist: {ex.Message}");
            }
        }
#endif
    }
}
