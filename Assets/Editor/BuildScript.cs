using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

// Unity -batchmode -quit -projectPath . -executeMethod BuildScript.BuildWebGL -logFile build.log
public static class BuildScript
{
    const string OutputPath = "Builds/WebGL";

    [MenuItem("Build/Apply WebGL Settings")]
    public static void ApplyWebGLSettings()
    {
        PlayerSettings.WebGL.template = "PROJECT:Mobile";
        PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Gzip;
        // GitHub Pages does not send Content-Encoding headers
        PlayerSettings.WebGL.decompressionFallback = true;
        PlayerSettings.WebGL.dataCaching = true;
        PlayerSettings.WebGL.nameFilesAsHashes = false;
        AssetDatabase.SaveAssets();
        Debug.Log("WebGL settings applied.");
    }

    [MenuItem("Build/Build WebGL")]
    public static void BuildWebGL()
    {
        ApplyWebGLSettings();

        var scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray();
        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = OutputPath,
            target = BuildTarget.WebGL,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        bool ok = report.summary.result == BuildResult.Succeeded;
        Debug.Log($"WebGL build {report.summary.result}: {report.summary.totalSize / (1024f * 1024f):F1} MB -> {OutputPath}");

        if (Application.isBatchMode && !ok)
            EditorApplication.Exit(1);
    }
}
