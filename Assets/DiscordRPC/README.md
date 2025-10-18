
<table frame="void">
    <tr>
      <td width="200px">
        <img src="https://raw.githubusercontent.com/Lachee/discord-rpc-unity/master/Resources/logo.png" align="center" width="100%" />
      </td>
      <td>
        <h1>Discord RPC Unity</h1>
        <p>
            <a href="https://github.com/Lachee/discord-rpc-unity/actions/workflows/release.yml"><img src="https://github.com/Lachee/discord-rpc-unity/actions/workflows/release.yml/badge.svg" /></a>
            <a href="https://github.com/Lachee/discord-rpc-unity/tags"><img alt="GitHub package.json version" src="https://img.shields.io/github/package-json/v/lachee/discord-rpc-unity?label=github"></a>
            <a href="https://openupm.com/packages/com.lachee.discordrpc/"><img src="https://img.shields.io/npm/v/com.lachee.discordrpc?label=openupm&amp;registry_uri=https://package.openupm.com" /></a>
          <br>
          This package provides a wrapper for <a href="https://github.com/lachee/discord-rpc-csharp">lachee/discord-rpc-csharp</a> and
          a  better experience when intergrating with Unity3D, as well as solving some tricky annoyances such as named pipes and mono.
        </p>
      </td>
    </tr>
</table>

# Usage
Add the package to your project and look at the sample code. For more documentation about the RPC, check the 
[discord-rpc-csharp](https://github.com/lachee/discord-rpc-csharp) documentation

Check out the documentation at [https://lachee.github.io/discord-rpc-unity/](https://lachee.github.io/discord-rpc-unity/)

# Dependencies

- At _least_ Unity 2018, however:
    - Support is **only given down to** [Unity 2018.4.36f1](https://unity3d.com/unity/qa/lts-releases?version=2018.4) LTS
    - Support is **only given up to** the latest LTS 

- Newtonsoft.JSON 13
    - This is provided by [com.unity.nuget.newtonsoft-json](https://docs.unity3d.com/Packages/com.unity.nuget.newtonsoft-json@3.0/manual/index.html)
    - _this determines the 2018.4 min spec. You can go lower, but you need to supply your own newtonsoft.json 13.0 binary._


# Installation
#### OpenUPM <a href="https://openupm.com/packages/com.lachee.discordrpc/"><img src="https://img.shields.io/npm/v/com.lachee.discordrpc?label=openupm&amp;registry_uri=https://package.openupm.com" /></a>
The [openupm registry](https://openupm.com)  is a open source package manager for Unity and provides the [openupm-cli](https://github.com/openupm/openupm-cli) to manage your dependencies.
```
openupm add com.lachee.discordrpc
```

#### Manual UPM             <a href="https://github.com/Lachee/discord-rpc-unity/tags"><img alt="GitHub package.json version" src="https://img.shields.io/github/package-json/v/lachee/discord-rpc-unity?label=github"></a>
Use the Unity Package Manager to add a git package. Adding the git to your UPM will limit updates as Unity will not track versioning on git projects (even though they totally could with tags).
1. Open the Unity Package Manager and `Add Package by git URL...`
2. `https://github.com/Lachee/discord-rpc-unity.git `

For local editable versions, manually clone the repo into your package folder. Note the exact spelling on destination name.
1. `git clone https://github.com/Lachee/discord-rpc-unity.git Packages/com.lachee.discordrpc`

#### Unity Package
Go old school and download the Unity Package and import it into your project.
1. Download the `.unitypackage` from the [Releases](releases) or via the last run `Create Release` action.
2. Import that package into your Unity3D

# Logging

By default, the DiscordManager will log to the Unity Console while in the Editor.
To enable logging in builds, create a [Development Build](https://docs.unity3d.com/Manual/BuildSettings.html) and a new discordrpc.log file will be generated with your app when it runs. 

# Licensing

The license is MIT so do what you want;

However, i do appriciate attributations where possible and a link. Also if you plan to "fix" the library and sell it, please contribute back to this project with your fixes so others can benifit too.
