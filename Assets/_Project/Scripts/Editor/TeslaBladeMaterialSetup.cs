using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine;

// =========================================================
// SETUP DES MATERIAUX DES LAMES DE TESLA (N1 Slicer -> N3 Nova)
// Menu : Blockforge > Setup Tesla Blade Materials
// - Cree les materiaux HDRP/Lit de la palette partagee des
//   lames de Tesla (couleurs unies, pas de textures ; rails
//   d'energie cyan et coeur de plasma emissifs) dans
//   Art/Materials/Blocks
// - Remappe les materiaux des 3 FBX Tesla_N* vers ces
//   materiaux partages
// Source Blender : Tools/Blender/TeslaBlades.blend
// S'execute une seule fois automatiquement apres compilation
// (marqueur dans Library/).
// =========================================================

[InitializeOnLoad]
public static class TeslaBladeMaterialSetup
{
    private const string ModelDir = "Assets/_Project/Art/Models/Blocks/Weapons";
    private const string MaterialParentDir = "Assets/_Project/Art/Materials";
    private const string MaterialDir = "Assets/_Project/Art/Materials/Blocks";
    private const string AutoRunMarker = "Library/BlockforgeTeslaBladeMaterials.done";

    // Intensite emissive en nits (exposition fixe EV 9.5 dans les scenes),
    // meme valeur que les lasers et les lanceurs de plasma.
    private const float GlowNits = 800f;

    public static readonly string[] FbxNames =
    {
        "Tesla_N1_Slicer",
        "Tesla_N2_Ripper",
        "Tesla_N3_Nova",
    };

    private struct TeslaMat
    {
        public string AssetName;
        public Color BaseColor;      // lineaire (valeurs glTF)
        public float Metallic;
        public float Smoothness;     // 1 - roughness glTF
        public Color EmissiveColor;  // lineaire (emissiveFactor glTF), noir = pas d'emission
    }

    // Nom du materiau dans les FBX -> definition du materiau partage (identique sur les 3 niveaux)
    private static readonly Dictionary<string, TeslaMat> FbxMaterials = new()
    {
        { "tesla_titanium",      new TeslaMat { AssetName = "Tesla_Titanium",     BaseColor = new Color(0.760f, 0.800f, 0.820f), Metallic = 0.64f, Smoothness = 0.71f, EmissiveColor = Color.black } },
        { "tesla_graphite",      new TeslaMat { AssetName = "Tesla_Graphite",     BaseColor = new Color(0.095f, 0.125f, 0.145f), Metallic = 0.80f, Smoothness = 0.69f, EmissiveColor = Color.black } },
        { "tesla_machined_edge", new TeslaMat { AssetName = "Tesla_MachinedEdge", BaseColor = new Color(0.400f, 0.490f, 0.530f), Metallic = 0.85f, Smoothness = 0.76f, EmissiveColor = Color.black } },
        { "tesla_cyan_plasma",   new TeslaMat { AssetName = "Tesla_CyanPlasma",   BaseColor = new Color(0.130f, 0.880f, 1.000f), Metallic = 0.12f, Smoothness = 0.81f, EmissiveColor = new Color(0.100f, 0.800f, 1.000f) } },
        { "tesla_plasma_core",   new TeslaMat { AssetName = "Tesla_PlasmaCore",   BaseColor = new Color(0.650f, 0.980f, 1.000f), Metallic = 0.08f, Smoothness = 0.85f, EmissiveColor = new Color(0.460f, 0.950f, 1.000f) } },
        { "tesla_recess",        new TeslaMat { AssetName = "Tesla_Recess",       BaseColor = new Color(0.022f, 0.033f, 0.042f), Metallic = 0.45f, Smoothness = 0.55f, EmissiveColor = Color.black } },
    };

    static TeslaBladeMaterialSetup()
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

    [MenuItem("Blockforge/Setup Tesla Blade Materials")]
    public static void Setup()
    {
        var materials = CreateMaterials();
        RemapFbxMaterials(materials);
        Debug.Log($"[TeslaBladeMaterialSetup] Materiaux HDRP crees et remappes sur les {FbxNames.Length} FBX Tesla_N*.");
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
                Debug.LogError($"[TeslaBladeMaterialSetup] Importeur du FBX introuvable : {fbxPath}");
                continue;
            }

            foreach (var (matName, mat) in materials)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), matName), mat);

            importer.SaveAndReimport();
        }
    }
}
