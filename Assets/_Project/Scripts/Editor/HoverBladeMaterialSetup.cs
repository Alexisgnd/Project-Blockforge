using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine;

// =========================================================
// SETUP DES MATERIAUX DES LAMES DE SURVOL (N1 Squall -> N6 Hurricane)
// Menu : Blockforge > Setup Hover Blade Materials
// - Cree les materiaux HDRP/Lit de la palette partagee des
//   lames de survol (couleurs unies, pas de textures ; liseres
//   cyan emissifs) dans Art/Materials/Blocks
// - Remappe les materiaux des 6 FBX HoverBlade_N* vers ces
//   materiaux partages
// Source Blender : Tools/Blender/HoverBlades.blend
// S'execute une seule fois automatiquement apres compilation
// (marqueur dans Library/).
// =========================================================

[InitializeOnLoad]
public static class HoverBladeMaterialSetup
{
    private const string ModelDir = "Assets/_Project/Art/Models/Blocks/Movement";
    private const string MaterialParentDir = "Assets/_Project/Art/Materials";
    private const string MaterialDir = "Assets/_Project/Art/Materials/Blocks";
    private const string AutoRunMarker = "Library/BlockforgeHoverBladeMaterials.done";

    // Intensite emissive en nits (exposition fixe EV 9.5 dans les scenes),
    // meme valeur que les lasers, pattes d'insecte, propulseurs et bouclier.
    private const float GlowNits = 800f;

    public static readonly string[] FbxNames =
    {
        "HoverBlade_N1_Squall",
        "HoverBlade_N2_Thunder",
        "HoverBlade_N3_Storm",
        "HoverBlade_N4_Tempest",
        "HoverBlade_N5_Tornado",
        "HoverBlade_N6_Hurricane",
    };

    private struct HoverMat
    {
        public string AssetName;
        public Color BaseColor;      // lineaire (valeurs glTF)
        public float Metallic;
        public float Smoothness;     // 1 - roughness glTF
        public Color EmissiveColor;  // lineaire (emissiveFactor glTF), noir = pas d'emission
    }

    // Nom du materiau dans les FBX -> definition du materiau partage.
    // ceramic/white/steel/dark/cyan sont communs aux 6 niveaux ; yellow et
    // pale_cyan n'existent que sur Tornado, hurricane_yellow et turbine_cyan
    // que sur Hurricane (le remap d'un nom absent d'un FBX est sans effet).
    private static readonly Dictionary<string, HoverMat> FbxMaterials = new()
    {
        { "hover_ceramic",          new HoverMat { AssetName = "Hover_Ceramic",         BaseColor = new Color(0.770f, 0.810f, 0.820f), Metallic = 0.62f, Smoothness = 0.71f, EmissiveColor = Color.black } },
        { "hover_white",            new HoverMat { AssetName = "Hover_White",           BaseColor = new Color(0.910f, 0.930f, 0.910f), Metallic = 0.38f, Smoothness = 0.74f, EmissiveColor = Color.black } },
        { "hover_steel",            new HoverMat { AssetName = "Hover_Steel",           BaseColor = new Color(0.340f, 0.400f, 0.430f), Metallic = 0.85f, Smoothness = 0.75f, EmissiveColor = Color.black } },
        { "hover_dark",             new HoverMat { AssetName = "Hover_Dark",            BaseColor = new Color(0.035f, 0.055f, 0.066f), Metallic = 0.65f, Smoothness = 0.67f, EmissiveColor = Color.black } },
        { "hover_cyan",             new HoverMat { AssetName = "Hover_Cyan",            BaseColor = new Color(0.015f, 0.640f, 0.880f), Metallic = 0.42f, Smoothness = 0.76f, EmissiveColor = new Color(0.000f, 0.180f, 0.300f) } },
        { "hover_yellow",           new HoverMat { AssetName = "Hover_Yellow",          BaseColor = new Color(0.950f, 0.700f, 0.035f), Metallic = 0.40f, Smoothness = 0.72f, EmissiveColor = Color.black } },
        { "hover_pale_cyan",        new HoverMat { AssetName = "Hover_PaleCyan",        BaseColor = new Color(0.400f, 0.830f, 0.890f), Metallic = 0.48f, Smoothness = 0.76f, EmissiveColor = Color.black } },
        { "hover_hurricane_yellow", new HoverMat { AssetName = "Hover_HurricaneYellow", BaseColor = new Color(1.000f, 0.770f, 0.018f), Metallic = 0.33f, Smoothness = 0.77f, EmissiveColor = Color.black } },
        { "hover_turbine_cyan",     new HoverMat { AssetName = "Hover_TurbineCyan",     BaseColor = new Color(0.045f, 0.700f, 0.940f), Metallic = 0.48f, Smoothness = 0.78f, EmissiveColor = Color.black } },
    };

    static HoverBladeMaterialSetup()
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

    [MenuItem("Blockforge/Setup Hover Blade Materials")]
    public static void Setup()
    {
        var materials = CreateMaterials();
        RemapFbxMaterials(materials);
        Debug.Log($"[HoverBladeMaterialSetup] Materiaux HDRP crees et remappes sur les {FbxNames.Length} FBX HoverBlade_*.");
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
                Debug.LogError($"[HoverBladeMaterialSetup] Importeur du FBX introuvable : {fbxPath}");
                continue;
            }

            foreach (var (matName, mat) in materials)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), matName), mat);

            importer.SaveAndReimport();
        }
    }
}
