using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine;

// =========================================================
// SETUP DES MATERIAUX ET DE L'IMPORT DES CHENILLES
// Menu : Blockforge > Setup Tank Track Materials
// - Cree les materiaux HDRP/Lit de la palette partagee des
//   4 chenilles (couleurs unies, liseres cyan emissifs) dans
//   Art/Materials/Blocks
// - Remappe les materiaux des 4 FBX TankTrack_N* vers ces
//   materiaux partages
// - Configure l'import animation des FBX : rig Generic,
//   clips Roll_Forward / Roll_Reverse (16 s @ 60 fps, une
//   take FBX par clip) en Loop Time, sans Loop Pose
// Source Blender : Tools/Blender/TankTracks.blend
//   (pack GLB "Tank_Tracks_BlockForge" du 13/09/2026)
// S'execute une seule fois automatiquement apres compilation
// (marqueur dans Library/).
// =========================================================

[InitializeOnLoad]
public static class TankTrackMaterialSetup
{
    private const string ModelDir = "Assets/_Project/Art/Models/Blocks/Movement";
    private const string MaterialParentDir = "Assets/_Project/Art/Materials";
    private const string MaterialDir = "Assets/_Project/Art/Materials/Blocks";
    private const string AutoRunMarker = "Library/BlockforgeTankTrackMaterials.done";

    // Intensite emissive en nits (exposition fixe EV 9.5 dans les scenes),
    // meme valeur que les lasers, pattes d'insecte, propulseurs et bouclier.
    private const float GlowNits = 800f;

    public static readonly string[] FbxNames =
    {
        "TankTrack_N1_Bison",
        "TankTrack_N2_Rhino",
        "TankTrack_N3_Elephant",
        "TankTrack_N4_Mammoth",
    };

    // Noms des clips (takes FBX = strips NLA Blender = clips glTF du pack)
    public const string ClipForward = "Roll_Forward";
    public const string ClipReverse = "Roll_Reverse";

    private struct TrackMat
    {
        public string AssetName;
        public Color BaseColor;      // lineaire (valeurs glTF, moyenne des 4 chenilles)
        public float Metallic;
        public float Smoothness;     // 1 - roughness glTF
        public Color EmissiveColor;  // lineaire (emissiveFactor glTF), noir = pas d'emission
    }

    // Nom du materiau dans les FBX -> definition du materiau partage
    private static readonly Dictionary<string, TrackMat> FbxMaterials = new()
    {
        { "track_rubber", new TrackMat { AssetName = "Track_Rubber", BaseColor = new Color(0.063f, 0.098f, 0.137f), Metallic = 0.05f, Smoothness = 0.13f, EmissiveColor = Color.black } },
        { "track_armor",  new TrackMat { AssetName = "Track_Armor",  BaseColor = new Color(0.522f, 0.545f, 0.553f), Metallic = 0.55f, Smoothness = 0.50f, EmissiveColor = Color.black } },
        { "track_trim",   new TrackMat { AssetName = "Track_Trim",   BaseColor = new Color(0.290f, 0.318f, 0.345f), Metallic = 0.60f, Smoothness = 0.52f, EmissiveColor = Color.black } },
        { "track_cyan",   new TrackMat { AssetName = "Track_Cyan",   BaseColor = new Color(0.145f, 0.808f, 0.863f), Metallic = 0.25f, Smoothness = 0.58f, EmissiveColor = new Color(0.040f, 0.320f, 0.370f) } },
        { "track_steel",  new TrackMat { AssetName = "Track_Steel",  BaseColor = new Color(0.160f, 0.200f, 0.235f), Metallic = 0.62f, Smoothness = 0.58f, EmissiveColor = Color.black } },
        { "track_blue",   new TrackMat { AssetName = "Track_Blue",   BaseColor = new Color(0.075f, 0.465f, 0.670f), Metallic = 0.25f, Smoothness = 0.50f, EmissiveColor = Color.black } },
        { "track_dark",   new TrackMat { AssetName = "Track_Dark",   BaseColor = new Color(0.031f, 0.060f, 0.085f), Metallic = 0.10f, Smoothness = 0.40f, EmissiveColor = Color.black } },
        { "track_bolt",   new TrackMat { AssetName = "Track_Bolt",   BaseColor = new Color(0.720f, 0.755f, 0.760f), Metallic = 0.72f, Smoothness = 0.67f, EmissiveColor = Color.black } },
    };

    static TankTrackMaterialSetup()
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

    [MenuItem("Blockforge/Setup Tank Track Materials")]
    public static void Setup()
    {
        var materials = CreateMaterials();
        foreach (var fbxName in FbxNames)
            ConfigureImporter(fbxName, materials);
        Debug.Log($"[TankTrackMaterialSetup] Materiaux HDRP crees, remappes et clips configures sur les {FbxNames.Length} FBX TankTrack_*.");
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

    // Remap des materiaux + import animation (rig Generic, clips en boucle)
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
            Debug.LogError($"[TankTrackMaterialSetup] Importeur du FBX introuvable : {fbxPath}");
            return;
        }

        foreach (var (matName, mat) in materials)
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), matName), mat);

        // Rig Generic (Animator) : l'armature (Root > Link_NN / Wheel_Axle_NN) pilote
        // un SkinnedMeshRenderer a poids rigides. Pas de root motion dans les clips.
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.importAnimation = true;

        // Clips reconstruits depuis les takes du FBX (Roll_Forward, Roll_Reverse :
        // 961 frames @ 60 fps, la derniere identique a la premiere). Loop Time
        // pour la boucle, pas de Loop Pose : la boucle mecanique est deja exacte.
        var clips = importer.defaultClipAnimations;
        for (int i = 0; i < clips.Length; i++)
        {
            clips[i].name = clips[i].takeName;
            clips[i].loopTime = true;
            clips[i].loop = false;
            clips[i].keepOriginalPositionY = true;
            clips[i].keepOriginalPositionXZ = true;
            clips[i].keepOriginalOrientation = true;
        }
        importer.clipAnimations = clips;

        importer.SaveAndReimport();
    }
}
