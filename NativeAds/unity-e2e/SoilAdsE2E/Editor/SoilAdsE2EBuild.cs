using System;
using FlyingAcorn.Soil.Advertisement.E2E;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Batch-mode builds of the end-to-end test player (see run.sh). Player
/// settings are changed only for the build and restored afterwards.
/// </summary>
public static class SoilAdsE2EBuild
{
    private const string ScenePath = "Assets/SoilAdsE2E/SoilAdsE2E.unity";
    private const string AppId = "com.flyingacorn.soilads.e2e";

    public static void BuildAndroid()
    {
        var group = BuildTargetGroup.Android;
        var backend = PlayerSettings.GetScriptingBackend(group);
        var architectures = PlayerSettings.Android.targetArchitectures;
        var appId = PlayerSettings.GetApplicationIdentifier(group);
        var bundle = EditorUserBuildSettings.buildAppBundle;
        try
        {
            PlayerSettings.SetScriptingBackend(group, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.X86_64 | AndroidArchitecture.ARM64;
            PlayerSettings.SetApplicationIdentifier(group, AppId);
            EditorUserBuildSettings.buildAppBundle = false;
            Build(BuildTarget.Android, Arg("-e2eOutput"));
        }
        finally
        {
            PlayerSettings.SetScriptingBackend(group, backend);
            PlayerSettings.Android.targetArchitectures = architectures;
            PlayerSettings.SetApplicationIdentifier(group, appId);
            EditorUserBuildSettings.buildAppBundle = bundle;
            AssetDatabase.SaveAssets();
        }
    }

    public static void BuildIosSimulator()
    {
        var group = BuildTargetGroup.iOS;
        var sdk = PlayerSettings.iOS.sdkVersion;
        var appId = PlayerSettings.GetApplicationIdentifier(group);
        try
        {
            PlayerSettings.iOS.sdkVersion = iOSSdkVersion.SimulatorSDK;
            PlayerSettings.SetApplicationIdentifier(group, AppId);
            Build(BuildTarget.iOS, Arg("-e2eOutput"));
        }
        finally
        {
            PlayerSettings.iOS.sdkVersion = sdk;
            PlayerSettings.SetApplicationIdentifier(group, appId);
            AssetDatabase.SaveAssets();
        }
    }

    /// <summary>
    /// Runs the scenarios in Play mode against the Editor's simulated player (no build). The
    /// runner quits the Editor when it is done; its exit code is the verdict.
    /// </summary>
    public static void PlayInEditor()
    {
        CreateScene();
        EditorApplication.isPlaying = true;
    }

    private static void CreateScene()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        new GameObject("SoilAdsE2E").AddComponent<SoilAdsE2ERunner>();
        EditorSceneManager.SaveScene(scene, ScenePath);
    }

    private static void Build(BuildTarget target, string output)
    {
        // The studio's build tools refuse a batch build without a store; any store will do here
        // (run.sh restores the settings files afterwards).
        foreach (var guid in AssetDatabase.FindAssets("t:BuildData"))
        {
            var data = AssetDatabase.LoadAssetAtPath<FlyingAcorn.Analytics.BuildData.BuildData>(AssetDatabase.GUIDToAssetPath(guid));
            if (data == null) continue;
            data.StoreName = target == BuildTarget.iOS
                ? FlyingAcorn.Analytics.BuildData.Constants.Store.AppStore
                : FlyingAcorn.Analytics.BuildData.Constants.Store.GooglePlay;
            EditorUtility.SetDirty(data);
        }
        AssetDatabase.SaveAssets();

        CreateScene();

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = output,
            target = target,
            options = BuildOptions.Development
        });

        Debug.Log($"SOILADS-E2E-BUILD {report.summary.result} errors={report.summary.totalErrors} -> {output}");
        if (report.summary.result != BuildResult.Succeeded)
            EditorApplication.Exit(1);
    }

    private static string Arg(string name)
    {
        var args = Environment.GetCommandLineArgs();
        var index = Array.IndexOf(args, name);
        if (index < 0 || index + 1 >= args.Length)
            throw new ArgumentException($"missing {name} <path>");
        return args[index + 1];
    }
}
