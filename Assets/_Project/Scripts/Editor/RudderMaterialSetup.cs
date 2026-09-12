using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine;

// =========================================================
// SETUP DES MATERIAUX DES AILERONS (Hawk -> Bat, 7 niveaux)
// Menu : Blockforge > Setup Rudder Materials
// - Cree les materiaux HDRP/Lit de la palette partagee des
//   ailerons (couleurs unies, pas de textures) dans
//   Art/Materials/Blocks
// - Remappe les materiaux des 7 FBX Rudder_* vers ces
//   materiaux partages
// Source Blender : Tools/Blender/Rudders.blend (pipeline
// GLB -> FBX + icones : Tools/Blender/rudders_pipeline.py)
// S'execute une seule fois automatiquement apres compilation
// (marqueur dans Library/).
// =========================================================

[InitializeOnLoad]
public static class RudderMaterialSetup
{
    private const string ModelDir = "Assets/_Project/Art/Models/Blocks/Movement";
    private const string MaterialParentDir = "Assets/_Project/Art/Materials";
    private const string MaterialDir = "Assets/_Project/Art/Materials/Blocks";
    private const string AutoRunMarker = "Library/BlockforgeRudderMaterials.done";

    // Intensite HDRP (nits) appliquee a la couleur emissive lineaire du glTF
    private const float GlowNits = 800f;

    private static readonly string[] FbxNames =
    {
        "Rudder_N1_Hawk",
        "Rudder_N2_Falcon",
        "Rudder_N3_Kestrel",
        "Rudder_N4_Eagle",
        "Rudder_N5_VampireBat",
        "Rudder_N6_Albatross",
        "Rudder_N7_Bat",
    };

    private struct RudderMat
    {
        public string AssetName;
        public Color BaseColor;      // lineaire (valeurs glTF, identiques sur les 7 niveaux)
        public float Metallic;
        public float Smoothness;     // 1 - roughness glTF
        public Color EmissiveColor;  // lineaire, noir = pas d'emission
    }

    // Nom du materiau dans les FBX -> definition du materiau partage.
    // Le lisere cyan est legerement lumineux (emission du glTF) ; l'or
    // n'existe que sur Albatross et Bat (hauts niveaux).
    private static readonly Dictionary<string, RudderMat> FbxMaterials = new()
    {
        { "rudder_white",       new RudderMat { AssetName = "Rudder_White",      BaseColor = new Color(0.820f, 0.860f, 0.870f), Metallic = 0.15f, Smoothness = 0.62f, EmissiveColor = Color.black } },
        { "rudder_graphite",    new RudderMat { AssetName = "Rudder_Graphite",   BaseColor = new Color(0.075f, 0.090f, 0.100f), Metallic = 0.15f, Smoothness = 0.62f, EmissiveColor = Color.black } },
        { "rudder_steel",       new RudderMat { AssetName = "Rudder_Steel",      BaseColor = new Color(0.250f, 0.300f, 0.330f), Metallic = 0.40f, Smoothness = 0.62f, EmissiveColor = Color.black } },
        { "rudder_silver_edge", new RudderMat { AssetName = "Rudder_SilverEdge", BaseColor = new Color(0.460f, 0.520f, 0.550f), Metallic = 0.40f, Smoothness = 0.62f, EmissiveColor = Color.black } },
        { "rudder_cyan",        new RudderMat { AssetName = "Rudder_CyanSeam",   BaseColor = new Color(0.030f, 0.740f, 0.860f), Metallic = 0.15f, Smoothness = 0.62f, EmissiveColor = new Color(0.000f, 0.250f, 0.320f) } },
        { "rudder_gold",        new RudderMat { AssetName = "Rudder_Gold",       BaseColor = new Color(0.960f, 0.640f, 0.025f), Metallic = 0.15f, Smoothness = 0.62f, EmissiveColor = Color.black } },
    };

    static RudderMaterialSetup()
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

    [MenuItem("Blockforge/Setup Rudder Materials")]
    public static void Setup()
    {
        var materials = CreateMaterials();
        RemapFbxMaterials(materials);
        Debug.Log($"[RudderMaterialSetup] Materiaux HDRP crees et remappes sur les {FbxNames.Length} FBX Rudder_*.");
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
                Debug.LogError($"[RudderMaterialSetup] Importeur du FBX introuvable : {fbxPath}");
                continue;
            }

            foreach (var (matName, mat) in materials)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), matName), mat);

            importer.SaveAndReimport();
        }
    }
}
