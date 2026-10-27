using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Adds a "Mythica" menu to the Unity editor with one-click scene setup and WebGL build.
public static class MythicaBuildMenu
{
    const string ScenePath = "Assets/Scenes/MythicaSanctuary.unity";
    const string BuildPath = "Builds/WebGL";

    [MenuItem("Mythica/Create Game Scene")]
    public static void CreateScene()
    {
        Directory.CreateDirectory("Assets/Scenes");
        // The game builds itself at runtime, so the scene only needs the default camera and light.
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        EditorSceneManager.SaveScene(scene, ScenePath);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
        Debug.Log("Mythica: created " + ScenePath + ". Press Play to try the game.");
    }

    [MenuItem("Mythica/Build WebGL")]
    public static void BuildWebGL()
    {
        if (!File.Exists(ScenePath)) CreateScene();
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

        PlayerSettings.productName = "Mythica Sanctuary";
        PlayerSettings.companyName = "Green Bear Labs";
        PlayerSettings.runInBackground = true;
        // Gzip with a decompression fallback runs on any web host, even one that can't set
        // Content-Encoding headers. Switch the fallback off if the arcade server is configured for gzip.
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;

        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = BuildPath,
            target = BuildTarget.WebGL,
            options = BuildOptions.None,
        };
        var report = BuildPipeline.BuildPlayer(options);
        Debug.Log("Mythica WebGL build: " + report.summary.result + " (" + report.summary.totalSize / (1024 * 1024) + " MB) at " + BuildPath);
        if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            EditorUtility.RevealInFinder(BuildPath);
    }
}
