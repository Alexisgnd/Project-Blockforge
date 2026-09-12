using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine;

// =========================================================
// SETUP DES MATERIAUX DES ROUES (Scout -> Monster)
// Menu : Blockforge > Setup Wheel Materials
// - Cree les materiaux HDRP/Lit de la palette partagee des
//   roues (couleurs unies, pas de textures) dans
//   Art/Materials/Blocks
// - Remappe les materiaux des 6 FBX Wheel_* vers ces
//   materiaux partages
// S'execute une seule fois automatiquement apres compilation
// (marqueur dans Library/).
// =========================================================

[InitializeOnLoad]
public static class WheelBlockMaterialSetup
{
    private const string ModelDir = "Assets/_Project/Art/Models/Blocks/Movement";
    private const string MaterialParentDir = "Assets/_Project/Art/Materials";
    private const string MaterialDir = "Assets/_Project/Art/Materials/Blocks";
    // v2 : ajout du Scout (N1) apres le premier passage
    private const string AutoRunMarker = "Library/BlockforgeWheelMaterials-v2.done";

    public static readonly string[] FbxNames =
    {
        "Wheel_Scout",
        "Wheel_Discover",
        "Wheel_Pathfinder",
        "Wheel_Stormer",
        "Wheel_Geoterrain",
        "Wheel_Monster",
    };

    private struct WheelMat
    {
        public string AssetName;
        public Color BaseColor;   // lineaire (valeurs glTF)
        public float Metallic;
        public float Smoothness;  // 1 - roughness glTF
    }

    // Nom du materiau dans les FBX -> definition du materiau partage
    private static readonly Dictionary<string, WheelMat> FbxMaterials = new()
    {
        { "wheel_rubber",      new WheelMat { AssetName = "Wheel_Rubber",     BaseColor = new Color(0.024f, 0.028f, 0.030f), Metallic = 0.00f, Smoothness = 0.17f } },
        { "wheel_tread",       new WheelMat { AssetName = "Wheel_Tread",      BaseColor = new Color(0.055f, 0.062f, 0.065f), Metallic = 0.00f, Smoothness = 0.14f } },
        { "wheel_alloy",       new WheelMat { AssetName = "Wheel_Alloy",      BaseColor = new Color(0.580f, 0.640f, 0.670f), Metallic = 0.78f, Smoothness = 0.70f } },
        { "wheel_dark_steel",  new WheelMat { AssetName = "Wheel_DarkSteel",  BaseColor = new Color(0.105f, 0.140f, 0.160f), Metallic = 0.72f, Smoothness = 0.62f } },
        { "wheel_bright_edge", new WheelMat { AssetName = "Wheel_BrightEdge", BaseColor = new Color(0.800f, 0.840f, 0.850f), Metallic = 0.80f, Smoothness = 0.76f } },
        { "wheel_cyan_trim",   new WheelMat { AssetName = "Wheel_CyanTrim",   BaseColor = new Color(0.025f, 0.620f, 0.820f), Metallic = 0.45f, Smoothness = 0.75f } },
        { "wheel_yellow_trim", new WheelMat { AssetName = "Wheel_YellowTrim", BaseColor = new Color(0.980f, 0.680f, 0.025f), Metallic = 0.50f, Smoothness = 0.70f } },
    };

    static WheelBlockMaterialSetup()
    {
        EditorApplication.delayCall += AutoRunOnce;
    }

    private static void AutoRunOnce()
    {
        if (File.Exists(AutoRunMarker))
            return;

        Setup();
        File.WriteAllText(AutoRunMarker, "done");
    }

    [MenuItem("Blockforge/Setup Wheel Materials")]
    public static void Setup()
    {
        var materials = CreateMaterials();
        RemapFbxMaterials(materials);
        Debug.Log($"[WheelBlockMaterialSetup] Materiaux HDRP crees et remappes sur les {FbxNames.Length} FBX Wheel_*.");
    }

    private static Dictionary<string, Material> CreateMaterials()
    {
        if (!AssetDatabase.IsValidFolder(MaterialDir))
            AssetDatabase.CreateFolder(MaterialParentDir, "Blocks");

        var shader = Shader.Find("HDRP/Lit");
        var result = new Dictionary<string, Material>();

        foreach (var (fbxName, def) in FbxMaterials)
        {
            var matPath = $"{MaterialDir}/{def.AssetName}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
            }

            mat.SetColor("_BaseColor", def.BaseColor);
            mat.SetFloat("_Metallic", def.Metallic);
            mat.SetFloat("_Smoothness", def.Smoothness);
            mat.SetColor("_EmissiveColor", Color.black);

            HDMaterial.ValidateMaterial(mat);
            EditorUtility.SetDirty(mat);
            result[fbxName] = mat;
        }

        AssetDatabase.SaveAssets();
        return result;
    }

    private static void RemapFbxMaterials(Dictionary<string, Material> materials)
    {
        foreach (var fbxName in FbxNames)
        {
            var fbxPath = $"{ModelDir}/{fbxName}.fbx";
            if (AssetImporter.GetAtPath(fbxPath) is not ModelImporter importer)
            {
                Debug.LogError($"[WheelBlockMaterialSetup] Importeur du FBX introuvable : {fbxPath}");
                continue;
            }

            foreach (var (matName, mat) in materials)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), matName), mat);

            importer.SaveAndReimport();
        }
    }
}
