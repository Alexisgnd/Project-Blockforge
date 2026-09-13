using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine;

// =========================================================
// SETUP DES MATERIAUX DES LANCEURS DE PLASMA (N1 Pulser -> N6 Goliathon)
// Menu : Blockforge > Setup Plasma Weapon Materials
// - Cree les materiaux HDRP/Lit de la palette partagee des
//   lanceurs de plasma (couleurs unies, pas de textures ;
//   bandes d'induction et emetteurs "Ion cyan" emissifs) dans
//   Art/Materials/Blocks
// - Remappe les materiaux des 6 FBX Plasma_N* vers ces
//   materiaux partages
// Source Blender : Tools/Blender/PlasmaWeapons.blend
// S'execute une seule fois automatiquement apres compilation
// (marqueur dans Library/).
// =========================================================

[InitializeOnLoad]
public static class PlasmaWeaponMaterialSetup
{
    private const string ModelDir = "Assets/_Project/Art/Models/Blocks/Weapons";
    private const string MaterialParentDir = "Assets/_Project/Art/Materials";
    private const string MaterialDir = "Assets/_Project/Art/Materials/Blocks";
    private const string AutoRunMarker = "Library/BlockforgePlasmaWeaponMaterials.done";

    // Intensite emissive en nits (exposition fixe EV 9.5 dans les scenes),
    // meme valeur que les lasers et les autres blocs a liseres cyan.
    private const float GlowNits = 800f;

    public static readonly string[] FbxNames =
    {
        "Plasma_N1_Pulser",
        "Plasma_N2_Disruptor",
        "Plasma_N3_Bombarder",
        "Plasma_N4_Ravager",
        "Plasma_N5_Devastator",
        "Plasma_N6_Goliathon",
    };

    private struct PlasmaMat
    {
        public string AssetName;
        public Color BaseColor;      // lineaire (valeurs glTF)
        public float Metallic;
        public float Smoothness;     // 1 - roughness glTF
        public Color EmissiveColor;  // lineaire (emissiveFactor glTF), noir = pas d'emission
    }

    // Nom du materiau dans les FBX -> definition du materiau partage.
    // Les 5 premiers sont communs aux 6 niveaux ; le jaune n'existe que sur
    // Goliathon (le remap d'un nom absent d'un FBX est sans effet).
    private static readonly Dictionary<string, PlasmaMat> FbxMaterials = new()
    {
        { "plasma_ceramic_pearl",     new PlasmaMat { AssetName = "Plasma_CeramicPearl",     BaseColor = new Color(0.670f, 0.730f, 0.760f), Metallic = 0.62f, Smoothness = 0.70f, EmissiveColor = Color.black } },
        { "plasma_machined_aluminum", new PlasmaMat { AssetName = "Plasma_MachinedAluminum", BaseColor = new Color(0.360f, 0.430f, 0.470f), Metallic = 0.80f, Smoothness = 0.70f, EmissiveColor = Color.black } },
        { "plasma_graphite_recess",   new PlasmaMat { AssetName = "Plasma_GraphiteRecess",   BaseColor = new Color(0.025f, 0.045f, 0.058f), Metallic = 0.70f, Smoothness = 0.70f, EmissiveColor = Color.black } },
        { "plasma_bore_shadow",       new PlasmaMat { AssetName = "Plasma_BoreShadow",       BaseColor = new Color(0.005f, 0.012f, 0.018f), Metallic = 0.30f, Smoothness = 0.70f, EmissiveColor = Color.black } },
        { "plasma_ion_cyan",          new PlasmaMat { AssetName = "Plasma_IonCyan",          BaseColor = new Color(0.020f, 0.760f, 0.950f), Metallic = 0.40f, Smoothness = 0.78f, EmissiveColor = new Color(0.020f, 0.760f, 0.950f) } },
        { "plasma_goliathon_yellow",  new PlasmaMat { AssetName = "Plasma_GoliathonYellow",  BaseColor = new Color(0.960f, 0.730f, 0.012f), Metallic = 0.45f, Smoothness = 0.70f, EmissiveColor = Color.black } },
    };

    static PlasmaWeaponMaterialSetup()
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

    [MenuItem("Blockforge/Setup Plasma Weapon Materials")]
    public static void Setup()
    {
        var materials = CreateMaterials();
        RemapFbxMaterials(materials);
        Debug.Log($"[PlasmaWeaponMaterialSetup] Materiaux HDRP crees et remappes sur les {FbxNames.Length} FBX Plasma_N*.");
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
                Debug.LogError($"[PlasmaWeaponMaterialSetup] Importeur du FBX introuvable : {fbxPath}");
                continue;
            }

            foreach (var (matName, mat) in materials)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), matName), mat);

            importer.SaveAndReimport();
        }
    }
}
