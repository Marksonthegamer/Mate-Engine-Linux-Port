using System;
using UnityEditor;
using UnityEditor.Build.Reporting;

public static class CliBuilder
{
    public static void Build()
    {
        var args = Environment.GetCommandLineArgs();
        string outputDir = string.Empty;
        for (int i = 0; i < args.Length; i++)
        {
            if (args[i].Equals("--output", StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                outputDir = args[i + 1].Trim('"');
                if (outputDir == string.Empty)
                {
                    LogError("Please specify a valid output directory.");
                    EditorApplication.Exit(1);
                    return;
                }
                break;
            }
        }
        var options = new BuildPlayerOptions
        {
            scenes = new[] { "Assets/MATE ENGINE - Scenes/Mate Engine Main.unity"},
            locationPathName = outputDir,
            target = BuildTarget.StandaloneLinux64,
            options = BuildOptions.CompressWithLz4HC
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;

        if (summary.result == BuildResult.Succeeded)
            Log($"Build succeeded → {summary.totalSize / 1048576f:F1} MB at {outputDir}");
        else
        {
            LogError("Build failed!");
            EditorApplication.Exit(1);
        }
    }

    static void Log(string message)
    {
        Console.WriteLine();
        Console.WriteLine("##############################################");
        Console.WriteLine("CliBuilder: " + message);
        Console.WriteLine("##############################################");
        Console.WriteLine();
    }
    
    static void LogError(string message)
    {
        Console.WriteLine();
        Console.WriteLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
        Console.WriteLine("CliBuilder: " + message);
        Console.WriteLine("!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!");
        Console.WriteLine();
    }
}