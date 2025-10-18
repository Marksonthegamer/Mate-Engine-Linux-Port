using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Lachee.Discord.Editor
{
	[InitializeOnLoad]
	internal sealed class DiscordNativeInstall
	{
		const bool ENABLED = false;

		const string PLUGIN_PATH_86_64 = "Discord RPC/Plugins/x86_64";
		const string PLUGIN_PATH_86 = "Discord RPC/Plugins/x86";
		const string PLUGIN_NAME = "DiscordRPC.Native.dll";

		static DiscordNativeInstall()
		{
			if (PlayerSettings.GetApiCompatibilityLevel(BuildTargetGroup.Standalone) == ApiCompatibilityLevel.NET_2_0_Subset)
			{
				var result = EditorUtility.DisplayDialog("Incompatible API Level", "You are currently using the .NET 2.0 Subset in this project. Discord RPC is incompatible with this version and requires the full version.\r\n\r\n" +
					"Failure to change to the full version of .NET 2.0 may break builds and hard crash the game.\r\n\r\n" +
					"Would you like to upgrade the project to .NET 2.0 now?", "Yes", "No");

				if (result)
				{
					PlayerSettings.SetApiCompatibilityLevel(BuildTargetGroup.Standalone, ApiCompatibilityLevel.NET_2_0);
					Debug.Log("Converted project to .NET 2.0 successfully");
				}
				else
				{
					Debug.LogError("Discord RPC is unable to work in a .NET 2.0 SUBSET enviroment. Builds may not work and may hardcrash if not fixed. Please manually fix by changing player settings.");
				}
			}

#if !UNITY_2019_OR_NEWER
			FixLinkerSettings();
#endif
		}


		static void FixLinkerSettings()
        {
            string linkPath = "Assets/DiscordRPC/DiscordRPC.xml";
            string cscPath = "Assets/DiscordRPC/csc.rsp";

           bool needsRefresh = false;

			if (!File.Exists(linkPath)) {
				string linkContent = "<linker><assembly fullname=\"DiscordRPC\" preserve=\"all\" /></linker>";
				File.WriteAllText(linkPath, linkContent);
				needsRefresh = true;
			}

			if (needsRefresh || !File.Exists(cscPath))
			{
				string cscContent = $"/res:{linkPath}";
				File.WriteAllText(cscPath, cscContent);
				needsRefresh = true;
			}

			if (needsRefresh)
				AssetDatabase.Refresh();
        }
	}
}