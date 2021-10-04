using Unity.Build;
using UnityEditor;
using UnityEngine;

public class BuildRunner
{
    public static void BuildServer()
    {
        var bc = AssetDatabase.LoadAssetAtPath<BuildConfiguration>(
            "Assets/BuildSettings/WindowsClassicBuildConfiguration.buildconfiguration");
        var result = bc.Build(); //This will build your game
    }

    public static void BuildClient()
    {
        var bc = AssetDatabase.LoadAssetAtPath<BuildConfiguration>(
            "Assets/BuildSettings/WindowsClassicBuildConfigurationClient.buildconfiguration");
        var result = bc.Build(); //This will build your game
    }
}