using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine;

// =========================================================
// SETUP DES MATERIAUX DES DISTRIBUTEURS NANO (N1 Blinder -> N3 Constructor)
// Menu : Blockforge > Setup Nano Disruptor Materials
// - Cree les materiaux HDRP/Lit de la palette partagee des
//   distributeurs nano / healers (couleurs unies, pas de
//   textures ; reservoir et emetteur cyan emissifs, accents
//   jaunes du Constructor) dans Art/Materials/Blocks
// - Remappe les materiaux des 3 FBX Nano_N* vers ces
//   materiaux partages
// Source Blender : Tools/Blender/NanoDisruptors.blend
// S'execute une seule fois automatiquement apres compilation
// (marqueur dans Library/).
// =========================================================

[InitializeOnLoad]
public static class NanoDisruptorMaterialSetup
{
    private const string ModelDir = "Assets/_Project/Art/Models/Blocks/Defense";
    private const string MaterialParentDir = "Assets/_Project/Art/Materials";
    private const string MaterialDir = "Assets/_Project/Art/Materials/Blocks";
    private const string AutoRunMarker = "Library/BlockforgeNanoDisruptorMaterials.done";

    // Intensite emissive en nits (exposition fixe EV 9.5 dans les scenes),
    // meme valeur que les autres blocs a liseres cyan.
    private const float GlowNits = 800f;

    public static readonly string[] FbxNames =
    {
        "Nano_N1_Blinder",
        "Nano_N2_Mender",
        "Nano_N3_Constructor",
    };

    private struct NanoMat
    {
        public string AssetName;
        public Color BaseColor;      // lineaire (valeurs glTF)
        public float Metallic;
        public float Smoothness;     // 1 - roughness glTF
        public Color EmissiveColor;  // lineaire (emissiveFactor glTF), noir = pas d'emission
    }

    // Nom du materiau dans les FBX -> definition du materiau partage.
    // Les 6 premiers sont communs aux 3 niveaux ; le jaune n'existe que sur
    // Constructor (le remap d'un nom absent d'un FBX est sans effet).
    private static readonly Dictionary<string, NanoMat> FbxMaterials = new()
    {
        { "nano_graphite_housing",   new NanoMat { AssetName = "Nano_GraphiteHousing",   BaseColor = new Color(0.055f, 0.070f, 0.080f), Metallic = 0.45f, Smoothness = 0.55f, EmissiveColor = Color.black } },
        { "nano_satin_silver",       new NanoMat { AssetName = "Nano_SatinSilver",       BaseColor = new Color(0.650f, 0.710f, 0.740f), Metallic = 0.72f, Smoothness = 0.70f, EmissiveColor = Color.black } },
        { "nano_dark_steel",         new NanoMat { AssetName = "Nano_DarkSteel",         BaseColor = new Color(0.120f, 0.150f, 0.170f), Metallic = 0.70f, Smoothness = 0.68f, EmissiveColor = Color.black } },
        { "nano_cyan_energy",        new NanoMat { AssetName = "Nano_CyanEnergy",        BaseColor = new Color(0.015f, 0.650f, 0.880f), Metallic = 0.15f, Smoothness = 0.77f, EmissiveColor = new Color(0.010f, 0.320f, 0.460f) } },
        { "nano_white_ceramic",      new NanoMat { AssetName = "Nano_WhiteCeramic",      BaseColor = new Color(0.880f, 0.910f, 0.930f), Metallic = 0.38f, Smoothness = 0.72f, EmissiveColor = Color.black } },
        { "nano_recess_black",       new NanoMat { AssetName = "Nano_RecessBlack",       BaseColor = new Color(0.018f, 0.024f, 0.030f), Metallic = 0.10f, Smoothness = 0.38f, EmissiveColor = Color.black } },
        { "nano_constructor_yellow", new NanoMat { AssetName = "Nano_ConstructorYellow", BaseColor = new Color(1.000f, 0.680f, 0.025f), Metallic = 0.45f, Smoothness = 0.70f, EmissiveColor = Color.black } },
    };

    static NanoDisruptorMaterialSetup()
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

    [MenuItem("Blockforge/Setup Nano Disruptor Materials")]
    public static void Setup()
    {
        var materials = CreateMaterials();
        RemapFbxMaterials(materials);
        Debug.Log($"[NanoDisruptorMaterialSetup] Materiaux HDRP crees et remappes sur les {FbxNames.Length} FBX Nano_N*.");
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
                Debug.LogError($"[NanoDisruptorMaterialSetup] Importeur du FBX introuvable : {fbxPath}");
                continue;
            }

            foreach (var (matName, mat) in materials)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), matName), mat);

            importer.SaveAndReimport();
        }
    }
}
