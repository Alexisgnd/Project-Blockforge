using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine;

// =========================================================
// SETUP DES MATERIAUX DES ROTORS (Recon / Invader / Assault)
// Menu : Blockforge > Setup Rotor Blade Materials
// - Cree les materiaux HDRP/Lit de la palette partagee des
//   rotors (couleurs unies, pas de textures) dans
//   Art/Materials/Blocks
// - Remappe les materiaux des 3 FBX RotorBlade_* vers ces
//   materiaux partages
// S'execute une seule fois automatiquement apres compilation
// (marqueur dans Library/).
// =========================================================

[InitializeOnLoad]
public static class RotorBladeMaterialSetup
{
    private const string ModelDir = "Assets/_Project/Art/Models/Blocks/Movement";
    private const string MaterialParentDir = "Assets/_Project/Art/Materials";
    private const string MaterialDir = "Assets/_Project/Art/Materials/Blocks";
    private const string AutoRunMarker = "Library/BlockforgeRotorBladeMaterials.done";

    private static readonly string[] FbxNames =
    {
        "RotorBlade_Recon",
        "RotorBlade_Invader",
        "RotorBlade_Assault",
    };

    private struct RotorMat
    {
        public string AssetName;
        public Color BaseColor;   // lineaire (valeurs glTF)
        public float Metallic;
        public float Smoothness;
    }

    // Nom du materiau dans les FBX -> definition du materiau partage.
    // Pas d'emissif : le lisere cyan des rotors est peint, pas lumineux.
    private static readonly Dictionary<string, RotorMat> FbxMaterials = new()
    {
        { "white_armor", new RotorMat { AssetName = "Rotor_WhiteArmor", BaseColor = new Color(0.815f, 0.847f, 0.880f), Metallic = 0.15f, Smoothness = 0.55f } },
        { "dark_steel",  new RotorMat { AssetName = "Rotor_DarkSteel",  BaseColor = new Color(0.041f, 0.054f, 0.074f), Metallic = 0.30f, Smoothness = 0.45f } },
        { "grey_metal",  new RotorMat { AssetName = "Rotor_GreyMetal",  BaseColor = new Color(0.323f, 0.381f, 0.434f), Metallic = 0.35f, Smoothness = 0.60f } },
        { "cyan_edge",   new RotorMat { AssetName = "Rotor_CyanEdge",   BaseColor = new Color(0.028f, 0.539f, 0.687f), Metallic = 0.20f, Smoothness = 0.68f } },
    };

    static RotorBladeMaterialSetup()
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

    [MenuItem("Blockforge/Setup Rotor Blade Materials")]
    public static void Setup()
    {
        var materials = CreateMaterials();
        RemapFbxMaterials(materials);
        Debug.Log("[RotorBladeMaterialSetup] Materiaux HDRP crees et remappes sur les 3 FBX RotorBlade_*.");
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
                Debug.LogError($"[RotorBladeMaterialSetup] Importeur du FBX introuvable : {fbxPath}");
                continue;
            }

            foreach (var (matName, mat) in materials)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), matName), mat);

            importer.SaveAndReimport();
        }
    }
}
