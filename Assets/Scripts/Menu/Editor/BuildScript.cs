using UnityEditor;
using System.IO;

namespace PrisaControlada.Menu
{
    public static class BuildScript
    {
        public static void Build()
        {
            string[] scenes = {
                "Assets/Scenes/MainMenu.unity",
                "Assets/Scenes/Prototype.unity"
            };

            string buildPath = "D:\\Tarea\\diseñoYprogramacion\\build";

            BuildPlayerOptions options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = buildPath + "/PrisaControlada.exe",
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            };

            var report = BuildPipeline.BuildPlayer(options);

            if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
                UnityEngine.Debug.Log($"Build succeeded: {report.summary.totalSize} bytes");
            else
                UnityEngine.Debug.LogError($"Build failed: {report.summary.result}");
        }
    }
}
