using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine;

// =========================================================
// SETUP DES MATERIAUX DES PATTES D'INSECTE (Walker, Soldier)
// Menu : Blockforge > Setup Insect Leg Materials
// - Cree les materiaux HDRP/Lit de la palette partagee des
//   pattes (couleurs unies, pas de textures ; liseres cyan
//   emissifs) dans Art/Materials/Blocks
// - Remappe les materiaux des 2 FBX InsectLeg_* vers ces
//   materiaux partages
// S'execute une seule fois automatiquement apres compilation
// (marqueur dans Library/).
// =========================================================

[InitializeOnLoad]
public static class InsectLegMaterialSetup
{
    private const string ModelDir = "Assets/_Project/Art/Models/Blocks/Movement";
    private const string MaterialParentDir = "Assets/_Project/Art/Materials";
    private const string MaterialDir = "Assets/_Project/Art/Materials/Blocks";
    private const string AutoRunMarker = "Library/BlockforgeInsectLegMaterials.done";

    // Intensite emissive du glow, en nits (exposition fixe EV 9.5 dans les scenes),
    // meme valeur que les armes laser.
    private const float GlowNits = 800f;

    public static readonly string[] FbxNames =
    {
        "InsectLeg_Walker",
        "InsectLeg_Soldier",
    };

    private struct LegMat
    {
        public string AssetName;
        public Color BaseColor;      // lineaire (valeurs glTF, moyenne des deux pattes)
        public float Metallic;
        public float Smoothness;     // 1 - roughness glTF
        public Color EmissiveColor;  // lineaire, noir = pas d'emission
    }

    // Nom du materiau dans les FBX -> definition du materiau partage
    private static readonly Dictionary<string, LegMat> FbxMaterials = new()
    {
        { "leg_armor_white",    new LegMat { AssetName = "InsectLeg_ArmorWhite",    BaseColor = new Color(0.780f, 0.819f, 0.876f), Metallic = 0.25f, Smoothness = 0.62f, EmissiveColor = Color.black } },
        { "leg_carbon_dark",    new LegMat { AssetName = "InsectLeg_CarbonDark",    BaseColor = new Color(0.014f, 0.017f, 0.026f), Metallic = 0.30f, Smoothness = 0.40f, EmissiveColor = Color.black } },
        { "leg_steel_grey",     new LegMat { AssetName = "InsectLeg_SteelGrey",     BaseColor = new Color(0.182f, 0.218f, 0.284f), Metallic = 0.35f, Smoothness = 0.55f, EmissiveColor = Color.black } },
        { "leg_soldier_yellow", new LegMat { AssetName = "InsectLeg_SoldierYellow", BaseColor = new Color(0.871f, 0.451f, 0.017f), Metallic = 0.20f, Smoothness = 0.58f, EmissiveColor = Color.black } },
        { "leg_cyan_glow",      new LegMat { AssetName = "InsectLeg_CyanGlow",      BaseColor = new Color(0.025f, 0.626f, 0.944f), Metallic = 0.10f, Smoothness = 0.70f, EmissiveColor = new Color(0.010f, 0.387f, 0.638f) } },
    };

    static InsectLegMaterialSetup()
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

    [MenuItem("Blockforge/Setup Insect Leg Materials")]
    public static void Setup()
    {
        var materials = CreateMaterials();
        RemapFbxMaterials(materials);
        Debug.Log($"[InsectLegMaterialSetup] Materiaux HDRP crees et remappes sur les {FbxNames.Length} FBX InsectLeg_*.");
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
                Debug.LogError($"[InsectLegMaterialSetup] Importeur du FBX introuvable : {fbxPath}");
                continue;
            }

            foreach (var (matName, mat) in materials)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), matName), mat);

            importer.SaveAndReimport();
        }
    }
}
