using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine;

// =========================================================
// SETUP DES MATERIAUX DES CANONS ELECTRIQUES (N1 Piercer -> N4 Erazer)
// Menu : Blockforge > Setup Rail Cannon Materials
// - Cree les materiaux HDRP/Lit de la palette partagee des
//   canons a rail (couleurs unies, pas de textures ; anneaux
//   et emetteurs cyan emissifs) dans Art/Materials/Blocks.
//   Les 4 GLB d'origine avaient chacun leur palette : elle a
//   ete fusionnee par role a l'export Blender (rail_*)
// - Remappe les materiaux des 4 FBX Rail_N* vers ces
//   materiaux partages
// Source Blender : Tools/Blender/RailCannons.blend
// S'execute une seule fois automatiquement apres compilation
// (marqueur dans Library/).
// =========================================================

[InitializeOnLoad]
public static class RailCannonMaterialSetup
{
    private const string ModelDir = "Assets/_Project/Art/Models/Blocks/Weapons";
    private const string MaterialParentDir = "Assets/_Project/Art/Materials";
    private const string MaterialDir = "Assets/_Project/Art/Materials/Blocks";
    private const string AutoRunMarker = "Library/BlockforgeRailCannonMaterials.done";

    // Intensite emissive en nits (exposition fixe EV 9.5 dans les scenes),
    // meme valeur que les lasers, plasmas et lames de Tesla.
    private const float GlowNits = 800f;

    public static readonly string[] FbxNames =
    {
        "Rail_N1_Piercer",
        "Rail_N2_Penetrator",
        "Rail_N3_Decimator",
        "Rail_N4_Erazer",
    };

    private struct RailMat
    {
        public string AssetName;
        public Color BaseColor;      // lineaire (moyenne des valeurs glTF des 4 modeles)
        public float Metallic;
        public float Smoothness;     // 1 - roughness glTF
        public Color EmissiveColor;  // lineaire (emissiveFactor glTF), noir = pas d'emission
    }

    // Nom du materiau dans les FBX -> definition du materiau partage.
    // steel n'existe que sur Decimator, yellow que sur Erazer (le remap d'un
    // nom absent d'un FBX est sans effet).
    private static readonly Dictionary<string, RailMat> FbxMaterials = new()
    {
        { "rail_alloy",    new RailMat { AssetName = "Rail_Alloy",    BaseColor = new Color(0.800f, 0.840f, 0.850f), Metallic = 0.60f, Smoothness = 0.70f, EmissiveColor = Color.black } },
        { "rail_white",    new RailMat { AssetName = "Rail_White",    BaseColor = new Color(0.910f, 0.930f, 0.925f), Metallic = 0.45f, Smoothness = 0.71f, EmissiveColor = Color.black } },
        { "rail_edge",     new RailMat { AssetName = "Rail_Edge",     BaseColor = new Color(0.530f, 0.595f, 0.620f), Metallic = 0.70f, Smoothness = 0.72f, EmissiveColor = Color.black } },
        { "rail_gunmetal", new RailMat { AssetName = "Rail_Gunmetal", BaseColor = new Color(0.130f, 0.168f, 0.190f), Metallic = 0.70f, Smoothness = 0.67f, EmissiveColor = Color.black } },
        { "rail_black",    new RailMat { AssetName = "Rail_Black",    BaseColor = new Color(0.055f, 0.088f, 0.110f), Metallic = 0.40f, Smoothness = 0.50f, EmissiveColor = Color.black } },
        { "rail_cyan",     new RailMat { AssetName = "Rail_Cyan",     BaseColor = new Color(0.270f, 0.860f, 0.915f), Metallic = 0.35f, Smoothness = 0.75f, EmissiveColor = new Color(0.090f, 0.460f, 0.530f) } },
        { "rail_steel",    new RailMat { AssetName = "Rail_Steel",    BaseColor = new Color(0.384f, 0.467f, 0.498f), Metallic = 0.65f, Smoothness = 0.69f, EmissiveColor = Color.black } },
        { "rail_yellow",   new RailMat { AssetName = "Rail_Yellow",   BaseColor = new Color(0.969f, 0.827f, 0.251f), Metallic = 0.25f, Smoothness = 0.71f, EmissiveColor = Color.black } },
    };

    static RailCannonMaterialSetup()
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

    [MenuItem("Blockforge/Setup Rail Cannon Materials")]
    public static void Setup()
    {
        var materials = CreateMaterials();
        RemapFbxMaterials(materials);
        Debug.Log($"[RailCannonMaterialSetup] Materiaux HDRP crees et remappes sur les {FbxNames.Length} FBX Rail_N*.");
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
                Debug.LogError($"[RailCannonMaterialSetup] Importeur du FBX introuvable : {fbxPath}");
                continue;
            }

            foreach (var (matName, mat) in materials)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), matName), mat);

            importer.SaveAndReimport();
        }
    }
}
