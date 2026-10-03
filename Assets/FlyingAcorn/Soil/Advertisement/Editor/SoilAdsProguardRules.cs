#if UNITY_ANDROID
using System.IO;
using UnityEditor;
using UnityEditor.Android;
using UnityEngine;

namespace FlyingAcorn.Soil.Advertisement.Editor
{
    /// <summary>
    /// Keeps the native ad player (Plugins/Android/SoilAds.androidlib) when the game is minified with
    /// R8/ProGuard. C# reaches <c>com.flyingacorn.soil.ads.SoilAdsBridge</c> only by name through JNI,
    /// which the shrinker cannot see, so without a keep rule the bridge is removed or renamed and every
    /// ad call fails at run time.
    /// <para>
    /// Unity's generated unityLibrary module lists <c>proguard-unity.txt</c> as a consumer ProGuard file
    /// (Unity 2022.3 and Unity 6), so a rule there applies to the whole app whatever the game's minify
    /// settings or custom ProGuard file. Unity rewrites that file on every export; the rule is appended
    /// after generation, once.
    /// </para>
    /// </summary>
    public class SoilAdsProguardRules : IPostGenerateGradleAndroidProject
    {
        internal const string KeepRule = "-keep class com.flyingacorn.soil.ads.** { *; }";
        private const string Comment = "# Soil native ads: called from C# by name through JNI";

        public int callbackOrder => 0;

        /// <param name="path">The unityLibrary Gradle module.</param>
        public void OnPostGenerateGradleAndroidProject(string path)
        {
            WarnAboutLowCompileSdk();
            var file = Path.Combine(path, "proguard-unity.txt");
            if (!File.Exists(file))
            {
                Debug.LogWarning($"[Advertisement] {file} not found; add \"{KeepRule}\" to your ProGuard rules " +
                                 "or native ads will not work in minified builds.");
                return;
            }

            var rules = File.ReadAllText(file);
            if (rules.Contains(KeepRule)) return;
            var separator = rules.Length == 0 || rules.EndsWith("\n") ? "" : "\n";
            File.AppendAllText(file, $"{separator}{Comment}\n{KeepRule}\n");
        }

        /// <summary>The player compiles against Android 13 (API 33) APIs, guarded at run time.</summary>
        internal const int MinimumCompileSdk = 33;

        /// <summary>
        /// Unity compiles the androidlib with compileSdk = the Target API Level, so a level pinned
        /// below 33 fails the Gradle build on the player's Android 13 back-gesture API with an
        /// unhelpful "cannot find symbol". Says why, up front. (Automatic uses the newest installed SDK.)
        /// </summary>
        private static void WarnAboutLowCompileSdk()
        {
            var target = (int)PlayerSettings.Android.targetSdkVersion;
            if (target == (int)AndroidSdkVersions.AndroidApiLevelAuto || target >= MinimumCompileSdk) return;
            Debug.LogError($"[Advertisement] Target API Level {target} is below {MinimumCompileSdk}: the Soil ad " +
                           "player (SoilAds.androidlib) will not compile. Set Player Settings > Android > " +
                           $"Target API Level to Automatic or {MinimumCompileSdk}+. Devices down to API 22 are still supported.");
        }
    }
}
#endif
