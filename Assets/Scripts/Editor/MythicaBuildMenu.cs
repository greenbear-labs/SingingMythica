using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Adds a "Mythica" menu to the Unity editor with one-click scene setup and WebGL build.
public static class MythicaBuildMenu
{
    const string ScenePath = "Assets/Scenes/SingingMythica.unity";
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

    [MenuItem("Mythica/Build WebGL (for the Arcade)")]
    public static void BuildWebGL()
    {
        if (!File.Exists(ScenePath)) CreateScene();
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };

        PlayerSettings.productName = "Singing Mythica";
        PlayerSettings.companyName = "Green Bear Labs";
        PlayerSettings.runInBackground = true;
        // Gzip with a decompression fallback runs on any web host, even one that can't set
        // Content-Encoding headers. Switch the fallback off if the arcade server is configured for gzip.
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        PlayerSettings.WebGL.decompressionFallback = true;

        // The scene is empty until the game builds itself at runtime, so Unity can't see what it uses.
        // Keep the primitive shader and the engine's collider classes in the build.
        IncludeShader("Standard");
        PlayerSettings.stripEngineCode = false;

        var options = new BuildPlayerOptions
        {
            scenes = new[] { ScenePath },
            locationPathName = BuildPath,
            target = BuildTarget.WebGL,
            options = BuildOptions.None,
        };
        var report = BuildPipeline.BuildPlayer(options);
        Debug.Log("Mythica WebGL build: " + report.summary.result + " (" + report.summary.totalSize / (1024 * 1024) + " MB) at " + BuildPath);
        bool ok = report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded;
        // From the command line (-batchmode -executeMethod MythicaBuildMenu.BuildWebGL), report failure in the exit code.
        if (Application.isBatchMode) { if (!ok) EditorApplication.Exit(1); }
        else if (ok) EditorUtility.RevealInFinder(BuildPath);
    }

    static void IncludeShader(string name)
    {
        var shader = Shader.Find(name);
        if (shader == null) return;   // not on the built-in pipeline; the pipeline's own default material is used
        var settings = AssetDatabase.LoadAssetAtPath<UnityEngine.Rendering.GraphicsSettings>("ProjectSettings/GraphicsSettings.asset");
        var so = new SerializedObject(settings);
        var list = so.FindProperty("m_AlwaysIncludedShaders");
        for (int i = 0; i < list.arraySize; i++)
            if (list.GetArrayElementAtIndex(i).objectReferenceValue == shader) return;
        list.InsertArrayElementAtIndex(list.arraySize);
        list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = shader;
        so.ApplyModifiedProperties();
        AssetDatabase.SaveAssets();
    }
}
