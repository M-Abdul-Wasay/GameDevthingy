#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using System.IO;

[InitializeOnLoad]
public static class FixAllMaterials
{
    private static bool executed = false;

    static FixAllMaterials()
    {
        EditorApplication.delayCall += () =>
        {
            if (!executed)
            {
                executed = true;
                ExecuteFix();
            }
        };
    }

    [MenuItem("Tools/Fix All Materials & Textures")]
    public static void ExecuteFix()
    {
        Debug.Log("[FixAllMaterials] Starting material scan and fix...");

        // Determine active pipeline
        RenderPipelineAsset rpa = GraphicsSettings.currentRenderPipeline;
        if (rpa == null) rpa = GraphicsSettings.defaultRenderPipeline;
        if (rpa == null) rpa = QualitySettings.renderPipeline;

        bool isURP = rpa != null;
        Debug.Log("[FixAllMaterials] Active Render Pipeline: " + (isURP ? rpa.name : "Built-in (Standard)"));

        Shader targetShader = null;
        if (isURP)
        {
            targetShader = Shader.Find("Universal Render Pipeline/Lit");
            if (targetShader == null) targetShader = Shader.Find("URP/Lit");
        }
        else
        {
            targetShader = Shader.Find("Standard");
        }

        if (targetShader == null)
        {
            Debug.LogError("[FixAllMaterials] Could not find target shader for pipeline!");
            return;
        }

        Debug.Log("[FixAllMaterials] Using target shader: " + targetShader.name);

        string[] materialFolders = new[]
        {
            "Assets/things/Materials",
            "Assets/Boss 1/Materials",
            "Assets/Boss 2/Materials",
            "Assets/Main Character form 1/Materials",
            "Assets/Main Character form 2/Materials"
        };

        string[] guids = AssetDatabase.FindAssets("t:Material", materialFolders);
        int fixedCount = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null) continue;

            // Find current diffuse texture
            Texture tex = null;
            if (mat.HasProperty("_BaseMap") && mat.GetTexture("_BaseMap") != null)
                tex = mat.GetTexture("_BaseMap");
            else if (mat.HasProperty("_MainTex") && mat.GetTexture("_MainTex") != null)
                tex = mat.GetTexture("_MainTex");

            // If texture was not assigned, try to find matching texture by name in same/parent folder
            if (tex == null)
            {
                string matName = Path.GetFileNameWithoutExtension(path);
                string dir = Path.GetDirectoryName(path);
                string parentDir = Path.GetDirectoryName(dir);
                string[] possibleTexGuids = AssetDatabase.FindAssets(matName + " t:Texture", new[] { dir, parentDir });
                if (possibleTexGuids.Length > 0)
                {
                    tex = AssetDatabase.LoadAssetAtPath<Texture>(AssetDatabase.GUIDToAssetPath(possibleTexGuids[0]));
                }
            }

            // Assign proper shader
            mat.shader = targetShader;

            // Assign textures and colors
            if (isURP)
            {
                if (tex != null) mat.SetTexture("_BaseMap", tex);
                mat.SetColor("_BaseColor", Color.white);
            }
            else
            {
                if (tex != null) mat.SetTexture("_MainTex", tex);
                mat.SetColor("_Color", Color.white);
            }

            EditorUtility.SetDirty(mat);
            fixedCount++;
            Debug.Log($"[FixAllMaterials] Fixed: {mat.name} -> Shader: {mat.shader.name}, Texture: {(tex != null ? tex.name : "NONE")}");
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log($"[FixAllMaterials] SUCCESS! Successfully checked and updated {fixedCount} materials!");
    }
}
#endif
