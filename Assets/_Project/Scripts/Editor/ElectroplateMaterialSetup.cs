using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine;

// =========================================================
// SETUP DES MATERIAUX DES ELECTROPLATES (T2 -> T9)
// Menu : Blockforge > Setup Electroplate Materials
// - Cree les materiaux HDRP/Lit des electroplates dans
//   Art/Materials/Blocks : 5 materiaux partages (titane,
//   graphite, chanfrein, pastilles cyan emissives, fixations)
//   + une ceramique bleue emissive par tier (la teinte varie
//   de T2 a T9)
// - Remappe les materiaux des 8 FBX Electroplate_T* vers ces
//   materiaux
// Source Blender : Tools/Blender/Electroplates.blend
// S'execute une seule fois automatiquement apres compilation
// (marqueur dans Library/).
// =========================================================

[InitializeOnLoad]
public static class ElectroplateMaterialSetup
{
    private const string ModelDir = "Assets/_Project/Art/Models/Blocks/Defense";
    private const string MaterialParentDir = "Assets/_Project/Art/Materials";
    private const string MaterialDir = "Assets/_Project/Art/Materials/Blocks";
    private const string AutoRunMarker = "Library/BlockforgeElectroplateMaterials.done";

    // Intensite emissive en nits (exposition fixe EV 9.5 dans les scenes),
    // meme valeur que les autres blocs a liseres cyan.
    private const float GlowNits = 800f;

    public static readonly string[] FbxNames =
    {
        "Electroplate_T2",
        "Electroplate_T3",
        "Electroplate_T4",
        "Electroplate_T5",
        "Electroplate_T6",
        "Electroplate_T7",
        "Electroplate_T8",
        "Electroplate_T9",
    };

    private struct PlateMat
    {
        public string AssetName;
        public Color BaseColor;      // lineaire (valeurs glTF)
        public float Metallic;
        public float Smoothness;     // 1 - roughness glTF
        public Color EmissiveColor;  // lineaire (emissiveFactor glTF), noir = pas d'emission
    }

    // Nom du materiau dans les FBX -> definition du materiau.
    // Les 5 premiers sont communs aux 8 tiers ; chaque FBX ne reference que
    // son propre bleu (le remap d'un nom absent d'un FBX est sans effet).
    private static readonly Dictionary<string, PlateMat> FbxMaterials = new()
    {
        { "electroplate_titanium",  new PlateMat { AssetName = "Electroplate_Titanium",  BaseColor = new Color(0.480f, 0.550f, 0.610f), Metallic = 0.75f, Smoothness = 0.70f, EmissiveColor = Color.black } },
        { "electroplate_graphite",  new PlateMat { AssetName = "Electroplate_Graphite",  BaseColor = new Color(0.065f, 0.095f, 0.130f), Metallic = 0.65f, Smoothness = 0.57f, EmissiveColor = Color.black } },
        { "electroplate_bevel",     new PlateMat { AssetName = "Electroplate_Bevel",     BaseColor = new Color(0.720f, 0.780f, 0.800f), Metallic = 0.80f, Smoothness = 0.76f, EmissiveColor = Color.black } },
        { "electroplate_cells",     new PlateMat { AssetName = "Electroplate_Cells",     BaseColor = new Color(0.320f, 0.740f, 0.960f), Metallic = 0.35f, Smoothness = 0.75f, EmissiveColor = new Color(0.100f, 0.320f, 0.430f) } },
        { "electroplate_fasteners", new PlateMat { AssetName = "Electroplate_Fasteners", BaseColor = new Color(0.025f, 0.045f, 0.065f), Metallic = 0.80f, Smoothness = 0.70f, EmissiveColor = Color.black } },
        // Ceramique bleue par tier : emissif = couleur de base x 0.22 (facteur du generateur)
        { "electroplate_blue_t2", Blue("Electroplate_Blue_T2", 0.160f, 0.660f, 0.880f) },
        { "electroplate_blue_t3", Blue("Electroplate_Blue_T3", 0.190f, 0.480f, 0.760f) },
        { "electroplate_blue_t4", Blue("Electroplate_Blue_T4", 0.120f, 0.420f, 0.820f) },
        { "electroplate_blue_t5", Blue("Electroplate_Blue_T5", 0.160f, 0.470f, 0.860f) },
        { "electroplate_blue_t6", Blue("Electroplate_Blue_T6", 0.055f, 0.340f, 0.720f) },
        { "electroplate_blue_t7", Blue("Electroplate_Blue_T7", 0.150f, 0.480f, 0.850f) },
        { "electroplate_blue_t8", Blue("Electroplate_Blue_T8", 0.075f, 0.450f, 0.850f) },
        { "electroplate_blue_t9", Blue("Electroplate_Blue_T9", 0.055f, 0.290f, 0.570f) },
    };

    private static PlateMat Blue(string assetName, float r, float g, float b) => new()
    {
        AssetName = assetName,
        BaseColor = new Color(r, g, b),
        Metallic = 0.28f,
        Smoothness = 0.72f,
        EmissiveColor = new Color(r * 0.22f, g * 0.22f, b * 0.22f),
    };

    static ElectroplateMaterialSetup()
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

    [MenuItem("Blockforge/Setup Electroplate Materials")]
    public static void Setup()
    {
        var materials = CreateMaterials();
        RemapFbxMaterials(materials);
        Debug.Log($"[ElectroplateMaterialSetup] Materiaux HDRP crees et remappes sur les {FbxNames.Length} FBX Electroplate_T*.");
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
                Debug.LogError($"[ElectroplateMaterialSetup] Importeur du FBX introuvable : {fbxPath}");
                continue;
            }

            foreach (var (matName, mat) in materials)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), matName), mat);

            importer.SaveAndReimport();
        }
    }
}
