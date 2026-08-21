using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine;

// =========================================================
// SETUP DES MATERIAUX DES ARMES LASER (N1 a N6)
// Menu : Blockforge > Setup Laser Weapon Materials
// - Cree les materiaux HDRP/Lit de la palette partagee des
//   armes (couleurs unies, pas de textures) dans
//   Art/Materials/Blocks
// - Remappe les materiaux des 6 FBX Laser_N* vers ces
//   materiaux partages
// S'execute une seule fois automatiquement apres compilation
// (marqueur dans Library/).
// =========================================================

[InitializeOnLoad]
public static class LaserWeaponMaterialSetup
{
    private const string ModelDir = "Assets/_Project/Art/Models/Blocks/Weapons";
    private const string MaterialParentDir = "Assets/_Project/Art/Materials";
    private const string MaterialDir = "Assets/_Project/Art/Materials/Blocks";
    private const string AutoRunMarker = "Library/BlockforgeLaserWeaponMaterials.done";

    // Intensite emissive du glow, en nits (exposition fixe EV 9.5 dans les scenes).
    private const float GlowNits = 800f;

    private static readonly string[] FbxNames =
    {
        "Laser_N1_Wasp",
        "Laser_N2_Hornet",
        "Laser_N3_Blaster",
        "Laser_N4_Vaporizer",
        "Laser_N5_Disintegrator",
        "Laser_N6_Leviathan",
    };

    private struct WeaponMat
    {
        public string AssetName;
        public Color BaseColor;      // lineaire (valeurs glTF)
        public float Metallic;
        public float Smoothness;
        public Color EmissiveColor;  // lineaire, noir = pas d'emission
    }

    // Nom du materiau dans les FBX -> definition du materiau partage
    private static readonly Dictionary<string, WeaponMat> FbxMaterials = new()
    {
        { "hull-white",   new WeaponMat { AssetName = "Weapon_HullWhite",   BaseColor = new Color(0.807f, 0.831f, 0.863f), Metallic = 0.15f, Smoothness = 0.65f, EmissiveColor = Color.black } },
        { "armor-dark",   new WeaponMat { AssetName = "Weapon_ArmorDark",   BaseColor = new Color(0.024f, 0.031f, 0.045f), Metallic = 0.30f, Smoothness = 0.50f, EmissiveColor = Color.black } },
        { "gunmetal",     new WeaponMat { AssetName = "Weapon_Gunmetal",    BaseColor = new Color(0.105f, 0.125f, 0.159f), Metallic = 0.40f, Smoothness = 0.60f, EmissiveColor = Color.black } },
        { "base-grey",    new WeaponMat { AssetName = "Weapon_BaseGrey",    BaseColor = new Color(0.323f, 0.356f, 0.407f), Metallic = 0.20f, Smoothness = 0.40f, EmissiveColor = Color.black } },
        { "glow-cyan",    new WeaponMat { AssetName = "Weapon_GlowCyan",    BaseColor = new Color(0.042f, 0.672f, 1.000f), Metallic = 0.00f, Smoothness = 0.70f, EmissiveColor = new Color(0.024f, 0.578f, 0.913f) } },
        { "accent-amber", new WeaponMat { AssetName = "Weapon_AccentAmber", BaseColor = new Color(0.922f, 0.386f, 0.025f), Metallic = 0.25f, Smoothness = 0.55f, EmissiveColor = Color.black } },
    };

    static LaserWeaponMaterialSetup()
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

    [MenuItem("Blockforge/Setup Laser Weapon Materials")]
    public static void Setup()
    {
        var materials = CreateMaterials();
        RemapFbxMaterials(materials);
        Debug.Log("[LaserWeaponMaterialSetup] Materiaux HDRP crees et remappes sur les 6 FBX Laser_N*.");
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
            mat.SetColor("_EmissiveColor", def.EmissiveColor * GlowNits);

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
                Debug.LogError($"[LaserWeaponMaterialSetup] Importeur du FBX introuvable : {fbxPath}");
                continue;
            }

            foreach (var (matName, mat) in materials)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), matName), mat);

            importer.SaveAndReimport();
        }
    }
}
