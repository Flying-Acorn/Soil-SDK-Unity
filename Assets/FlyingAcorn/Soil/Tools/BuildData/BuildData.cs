using System.Diagnostics;
using UnityEngine;
using Debug = UnityEngine.Debug;
#if UNITY_EDITOR
using UnityEditor;

#endif

namespace FlyingAcorn.Tools
{
    [CreateAssetMenu(fileName = "Build_Settings.asset", menuName = "FlyingAcorn/Build Settings")]
    public class BuildData : ScriptableObject
    {
        public string BuildNumber;
        public string LastBuildTime;
        public string RepositoryVersion;
        public string ScriptingBackend;

#if UNITY_EDITOR

        private void OnEnable()
        {
            RepositoryVersion = GetHgVersion();
            EditorRefreshScriptingBackend(EditorUserBuildSettings.activeBuildTarget);
#if UNITY_IOS
            BuildNumber = PlayerSettings.iOS.buildNumber;
#endif
#if UNITY_ANDROID
            BuildNumber = PlayerSettings.Android.bundleVersionCode.ToString();
#endif
        }

        public void EditorRefreshScriptingBackend(BuildTarget buildTarget)
        {
            var target = EditorUserBuildSettings.activeBuildTarget;
            var group = BuildPipeline.GetBuildTargetGroup(target);

            ScriptingBackend = PlayerSettings.GetScriptingBackend(group).ToString();
        }

        [MenuItem("FlyingAcorn/Tools/Print Hg Version")]
        public static string GetHgVersion()
        {
            try
            {
                var isWindows = Application.platform == RuntimePlatform.WindowsEditor;
                // Get the short commit hash of the current branch.
                var cmdArguments = isWindows ? "/c hg id -i" : "-c \"hg id -i\"";

                var processName = isWindows ? "cmd" : "/bin/bash";
                var procStartInfo = new ProcessStartInfo(processName, cmdArguments);

                // The following commands are needed to redirect the standard output.
                // This means that it will be redirected to the Process.StandardOutput StreamReader.
                procStartInfo.WorkingDirectory = Application.dataPath;
                procStartInfo.RedirectStandardOutput = true;
                procStartInfo.UseShellExecute = false;

                // Do not create the black window.
                procStartInfo.CreateNoWindow = true;

                // Now we create a process, assign its ProcessStartInfo and start it
                var proc = new Process();
                proc.StartInfo = procStartInfo;
                proc.Start();

                var hgVersion = proc.StandardOutput.ReadToEnd();
                Debug.LogFormat("[GetHgVersion] Version: {0}", hgVersion);
                // Get the output into a string
                return hgVersion;
            }
            catch
            {
                Debug.LogError("Unable to get hg hash.");
                return "unable to get version";
            }
        }
#endif
    }
}