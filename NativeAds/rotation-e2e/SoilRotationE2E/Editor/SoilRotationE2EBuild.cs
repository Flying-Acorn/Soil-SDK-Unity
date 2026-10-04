using System;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Batch-mode Android build of the rotation test player (see run.sh). Player settings are
/// changed only for the build and restored afterwards; run.sh also restores the settings files.
/// </summary>
public static class SoilRotationE2EBuild
{
    private const string ScenePath = "Assets/SoilRotationE2E/SoilRotationE2E.unity";
    private const string AppId = "com.flyingacorn.soilads.rotation";

    public static void BuildAndroid()
    {
        var group = BuildTargetGroup.Android;
        var backend = PlayerSettings.GetScriptingBackend(group);
        var architectures = PlayerSettings.Android.targetArchitectures;
        var appId = PlayerSettings.GetApplicationIdentifier(group);
        var bundle = EditorUserBuildSettings.buildAppBundle;
        var http = PlayerSettings.insecureHttpOption;
        try
        {
            PlayerSettings.SetScriptingBackend(group, ScriptingImplementation.IL2CPP);
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.X86_64 | AndroidArchitecture.ARM64;
            PlayerSettings.SetApplicationIdentifier(group, AppId);
            EditorUserBuildSettings.buildAppBundle = false;
            // The local Soil server run.sh points the build at speaks plain HTTP.
            PlayerSettings.insecureHttpOption = InsecureHttpOption.AlwaysAllowed;
            Build(Arg("-e2eOutput"));
        }
        finally
        {
            PlayerSettings.SetScriptingBackend(group, backend);
            PlayerSettings.Android.targetArchitectures = architectures;
            PlayerSettings.SetApplicationIdentifier(group, appId);
            EditorUserBuildSettings.buildAppBundle = bundle;
            PlayerSettings.insecureHttpOption = http;
            AssetDatabase.SaveAssets();
        }
    }

    private static void Build(string output)
    {
        // The studio's build tools refuse a batch build without a store (run.sh restores it).
        foreach (var guid in AssetDatabase.FindAssets("t:BuildData"))
        {
            var data = AssetDatabase.LoadAssetAtPath<FlyingAcorn.Analytics.BuildData.BuildData>(AssetDatabase.GUIDToAssetPath(guid));
            if (data == null) continue;
            data.StoreName = FlyingAcorn.Analytics.BuildData.Constants.Store.GooglePlay;
            EditorUtility.SetDirty(data);
        }
        AssetDatabase.SaveAssets();

        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, ScenePath);

        var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = output,
            target = BuildTarget.Android,
            options = BuildOptions.Development
        });

        Debug.Log($"SOILROT-BUILD {report.summary.result} errors={report.summary.totalErrors} -> {output}");
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
