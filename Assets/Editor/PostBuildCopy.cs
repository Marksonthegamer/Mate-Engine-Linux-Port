using UnityEditor;
using UnityEditor.Callbacks;
using System.IO;
using UnityEngine;

public abstract class PostBuildCopy
{
    [PostProcessBuild]
    public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
    {
        string buildDir = Path.GetDirectoryName(pathToBuiltProject);

        string launcherScript = Path.Combine(Directory.GetCurrentDirectory(), "launch.sh");
        
        if (buildDir != null)
        {
            string scriptDestination = Path.Combine(buildDir, "launch.sh");
            
            if (File.Exists(launcherScript))
            {
                File.Copy(launcherScript, scriptDestination, true);
                Debug.Log("Copied launch.sh to build folder.");
            }
            else
            {
                Debug.LogError("Looks like you forgot to put a extra but important script to launch the game!");
            }
        }
    }
}
