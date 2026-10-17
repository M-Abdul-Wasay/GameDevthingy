#if UNITY_EDITOR
using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class ExportLevel2Package
{
    [MenuItem("Tools/Export Level 2 to Desktop")]
    public static void ExecuteExport()
    {
        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
        string packagePath = Path.Combine(desktopPath, "Level2_Export.unitypackage");

        string marker = "Temp/level2_exported.txt";
        if (File.Exists(marker) && File.Exists(packagePath))
        {
            return;
        }

        Debug.Log("[ExportLevel2Package] Starting package export to: " + packagePath);

        List<string> assetPaths = new List<string>()
        {
            "Assets/level2.unity",
            "Assets/Boss 2",
            "Assets/Main Character form 2",
            "Assets/things/castle_lvl2.fbx",
            "Assets/Scripts/ArcherAI.cs",
            "Assets/Scripts/ArrowProjectile.cs",
            "Assets/Scripts/PlayerFireCharge.cs",
            "Assets/Scripts/PortalLevelTransition.cs",
            "Assets/Scripts/ShadowHeroController.cs",
            "Assets/Scripts/PlayerAnimationController.cs",
            "Assets/Scripts/CameraFollow.cs",
            "Assets/Scripts/WallCheck.cs"
        };

        List<string> validPaths = new List<string>();
        foreach (var p in assetPaths)
        {
            if (File.Exists(p) || Directory.Exists(p))
            {
                validPaths.Add(p);
            }
            else
            {
                Debug.LogWarning("[ExportLevel2Package] Asset path not found: " + p);
            }
        }

        try
        {
            AssetDatabase.ExportPackage(
                validPaths.ToArray(),
                packagePath,
                ExportPackageOptions.Recurse | ExportPackageOptions.IncludeDependencies
            );

            File.WriteAllText(marker, "Exported successfully at: " + DateTime.Now.ToString());
            Debug.Log("[ExportLevel2Package] SUCCESS! Exported Level 2 package to: " + packagePath);
        }
        catch (Exception ex)
        {
            Debug.LogError("[ExportLevel2Package] Failed to export package: " + ex);
        }
    }
}
#endif
