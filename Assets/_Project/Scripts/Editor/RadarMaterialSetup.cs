using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine;

// =========================================================
// SETUP DES MATERIAUX ET DE L'IMPORT DU RADAR
// Menu : Blockforge > Setup Radar Materials
// - Cree les materiaux HDRP/Lit de la palette du radar
//   (couleurs unies, illumination cyan emissive) dans
//   Art/Materials/Blocks
// - Remappe les materiaux du FBX Radar vers ces materiaux
// - Configure l'import animation du FBX : rig Generic, clips
//   Fold / Unfold (1,5 s @ 30 fps, une take FBX par clip)
//   joues une seule fois (pas de Loop Time)
// Source Blender : Tools/Blender/Radar.blend
//   (GLB "Radar_Module_Folding_v3" du 15/09/2026)
// S'execute une seule fois automatiquement apres compilation
// (marqueur dans Library/).
// =========================================================

[InitializeOnLoad]
public static class RadarMaterialSetup
{
    private const string ModelDir = "Assets/_Project/Art/Models/Blocks/Special";
    private const string MaterialParentDir = "Assets/_Project/Art/Materials";
    private const string MaterialDir = "Assets/_Project/Art/Materials/Blocks";
    private const string AutoRunMarker = "Library/BlockforgeRadarMaterials.done";

    // Intensite emissive en nits (exposition fixe EV 9.5 dans les scenes),
    // meme valeur que les autres blocs a liseres cyan.
    private const float GlowNits = 800f;

    public static readonly string[] FbxNames =
    {
        "Radar",
    };

    // Noms des clips (takes FBX = strips NLA Blender = animations glTF)
    public const string ClipFold = "Fold";
    public const string ClipUnfold = "Unfold";

    private struct RadarMat
    {
        public string AssetName;
        public Color BaseColor;      // lineaire (valeurs glTF)
        public float Metallic;
        public float Smoothness;     // 1 - roughness glTF
        public Color EmissiveColor;  // lineaire (emissiveFactor glTF), noir = pas d'emission
    }

    // Nom du materiau dans le FBX -> definition du materiau
    private static readonly Dictionary<string, RadarMat> FbxMaterials = new()
    {
        { "radar_graphite",     new RadarMat { AssetName = "Radar_Graphite",    BaseColor = new Color(0.055f, 0.070f, 0.080f), Metallic = 0.45f, Smoothness = 0.55f, EmissiveColor = Color.black } },
        { "radar_satin_silver", new RadarMat { AssetName = "Radar_SatinSilver", BaseColor = new Color(0.650f, 0.710f, 0.740f), Metallic = 0.72f, Smoothness = 0.70f, EmissiveColor = Color.black } },
        { "radar_dark_steel",   new RadarMat { AssetName = "Radar_DarkSteel",   BaseColor = new Color(0.120f, 0.150f, 0.170f), Metallic = 0.70f, Smoothness = 0.68f, EmissiveColor = Color.black } },
        { "radar_cyan",         new RadarMat { AssetName = "Radar_Cyan",        BaseColor = new Color(0.008f, 0.830f, 0.830f), Metallic = 0.10f, Smoothness = 0.70f, EmissiveColor = new Color(0.010f, 0.650f, 0.650f) } },
        { "radar_white_frame",  new RadarMat { AssetName = "Radar_WhiteFrame",  BaseColor = new Color(0.760f, 0.840f, 0.840f), Metallic = 0.50f, Smoothness = 0.72f, EmissiveColor = Color.black } },
        { "radar_dark_teal",    new RadarMat { AssetName = "Radar_DarkTeal",    BaseColor = new Color(0.012f, 0.037f, 0.038f), Metallic = 0.20f, Smoothness = 0.50f, EmissiveColor = Color.black } },
        { "radar_yellow",       new RadarMat { AssetName = "Radar_Yellow",      BaseColor = new Color(1.000f, 0.680f, 0.025f), Metallic = 0.45f, Smoothness = 0.70f, EmissiveColor = Color.black } },
    };

    static RadarMaterialSetup()
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

    [MenuItem("Blockforge/Setup Radar Materials")]
    public static void Setup()
    {
        var materials = CreateMaterials();
        foreach (var fbxName in FbxNames)
            ConfigureImporter(fbxName, materials);
        Debug.Log("[RadarMaterialSetup] Materiaux HDRP crees, remappes et clips configures sur le FBX Radar.");
    }

    public static string FbxPath(string fbxName) => $"{ModelDir}/{fbxName}.fbx";

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

    // Remap des materiaux + import animation (rig Generic, clips joues une fois)
    private static void ConfigureImporter(string fbxName, Dictionary<string, Material> materials)
    {
        var fbxPath = FbxPath(fbxName);
        if (AssetImporter.GetAtPath(fbxPath) is not ModelImporter importer)
        {
            AssetDatabase.ImportAsset(fbxPath, ImportAssetOptions.ForceSynchronousImport);
            importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
        }
        if (importer == null)
        {
            Debug.LogError($"[RadarMaterialSetup] Importeur du FBX introuvable : {fbxPath}");
            return;
        }

        foreach (var (matName, mat) in materials)
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), matName), mat);

        // Rig Generic (Animator) : les clips tournent les empties Left/Right_Panel_Hinge
        // (rotation locale, pas de root motion).
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.importAnimation = true;

        // Clips reconstruits depuis les takes du FBX (Fold, Unfold : 46 frames @ 30 fps).
        // Pas de boucle : chaque clip est joue une fois et tient sa derniere pose.
        var clips = importer.defaultClipAnimations;
        for (int i = 0; i < clips.Length; i++)
        {
            clips[i].name = clips[i].takeName;
            clips[i].loopTime = false;
            clips[i].loop = false;
            clips[i].keepOriginalPositionY = true;
            clips[i].keepOriginalPositionXZ = true;
            clips[i].keepOriginalOrientation = true;
        }
        importer.clipAnimations = clips;

        importer.SaveAndReimport();
    }
}
