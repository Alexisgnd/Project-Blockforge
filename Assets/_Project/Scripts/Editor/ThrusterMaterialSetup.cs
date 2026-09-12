using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine;

// =========================================================
// SETUP DES MATERIAUX DES PROPULSEURS (N1 Lynx -> N5 Cheetah)
// Menu : Blockforge > Setup Thruster Materials
// - Cree les materiaux HDRP/Lit de la palette partagee des
//   propulseurs (couleurs unies, pas de textures ; anneau
//   d'echappement bleu emissif) dans Art/Materials/Blocks
// - Remappe les materiaux des 5 FBX Thruster_N* vers ces
//   materiaux partages
// S'execute une seule fois automatiquement apres compilation
// (marqueur dans Library/).
// =========================================================

[InitializeOnLoad]
public static class ThrusterMaterialSetup
{
    private const string ModelDir = "Assets/_Project/Art/Models/Blocks/Movement";
    private const string MaterialParentDir = "Assets/_Project/Art/Materials";
    private const string MaterialDir = "Assets/_Project/Art/Materials/Blocks";
    private const string AutoRunMarker = "Library/BlockforgeThrusterMaterials.done";

    // Intensite emissive de l'echappement, en nits (exposition fixe EV 9.5 dans
    // les scenes), meme valeur que les armes laser et les pattes d'insecte.
    private const float GlowNits = 800f;

    public static readonly string[] FbxNames =
    {
        "Thruster_N1_Lynx",
        "Thruster_N2_Panther",
        "Thruster_N3_Leopard",
        "Thruster_N4_Puma",
        "Thruster_N5_Cheetah",
    };

    private struct ThrusterMat
    {
        public string AssetName;
        public Color BaseColor;      // lineaire (valeurs glTF, identiques sur les 5 niveaux)
        public float Metallic;
        public float Smoothness;     // 1 - roughness glTF
        public Color EmissiveColor;  // lineaire, noir = pas d'emission
    }

    // Nom du materiau dans les FBX -> definition du materiau partage
    private static readonly Dictionary<string, ThrusterMat> FbxMaterials = new()
    {
        { "thruster_ceramic_white", new ThrusterMat { AssetName = "Thruster_CeramicWhite", BaseColor = new Color(0.820f, 0.870f, 0.900f), Metallic = 0.25f, Smoothness = 0.68f, EmissiveColor = Color.black } },
        { "thruster_graphite",      new ThrusterMat { AssetName = "Thruster_Graphite",     BaseColor = new Color(0.055f, 0.068f, 0.075f), Metallic = 0.65f, Smoothness = 0.61f, EmissiveColor = Color.black } },
        { "thruster_titanium",      new ThrusterMat { AssetName = "Thruster_Titanium",     BaseColor = new Color(0.320f, 0.390f, 0.430f), Metallic = 0.80f, Smoothness = 0.70f, EmissiveColor = Color.black } },
        { "thruster_azure",         new ThrusterMat { AssetName = "Thruster_Azure",        BaseColor = new Color(0.035f, 0.400f, 0.650f), Metallic = 0.45f, Smoothness = 0.73f, EmissiveColor = Color.black } },
        { "thruster_deep_nozzle",   new ThrusterMat { AssetName = "Thruster_DeepNozzle",   BaseColor = new Color(0.014f, 0.028f, 0.040f), Metallic = 0.30f, Smoothness = 0.50f, EmissiveColor = Color.black } },
        { "thruster_exhaust_blue",  new ThrusterMat { AssetName = "Thruster_ExhaustBlue",  BaseColor = new Color(0.035f, 0.570f, 0.870f), Metallic = 0.25f, Smoothness = 0.76f, EmissiveColor = new Color(0.010f, 0.160f, 0.300f) } },
    };

    static ThrusterMaterialSetup()
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

    [MenuItem("Blockforge/Setup Thruster Materials")]
    public static void Setup()
    {
        var materials = CreateMaterials();
        RemapFbxMaterials(materials);
        Debug.Log($"[ThrusterMaterialSetup] Materiaux HDRP crees et remappes sur les {FbxNames.Length} FBX Thruster_*.");
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
                Debug.LogError($"[ThrusterMaterialSetup] Importeur du FBX introuvable : {fbxPath}");
                continue;
            }

            foreach (var (matName, mat) in materials)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), matName), mat);

            importer.SaveAndReimport();
        }
    }
}
