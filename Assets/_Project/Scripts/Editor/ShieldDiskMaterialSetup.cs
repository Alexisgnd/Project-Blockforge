using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine;

// =========================================================
// SETUP DES MATERIAUX DU DISQUE DE BOUCLIER
// Menu : Blockforge > Setup Shield Disk Materials
// - Cree les materiaux HDRP/Lit de la palette du disque de
//   bouclier (couleurs unies, pas de textures ; canaux
//   d'energie cyan et emetteurs emissifs) dans
//   Art/Materials/Blocks
// - Remappe les materiaux du FBX ShieldDisk vers ces
//   materiaux partages
// Source Blender : Tools/Blender/ShieldDisk.blend
// S'execute une seule fois automatiquement apres compilation
// (marqueur dans Library/).
// =========================================================

[InitializeOnLoad]
public static class ShieldDiskMaterialSetup
{
    private const string ModelDir = "Assets/_Project/Art/Models/Blocks/Special";
    private const string MaterialParentDir = "Assets/_Project/Art/Materials";
    private const string MaterialDir = "Assets/_Project/Art/Materials/Blocks";
    private const string AutoRunMarker = "Library/BlockforgeShieldDiskMaterials.done";

    // Intensite emissive en nits (exposition fixe EV 9.5 dans les scenes),
    // meme valeur que les lasers, pattes d'insecte, propulseurs et ailerons.
    private const float GlowNits = 800f;

    public static readonly string[] FbxNames =
    {
        "ShieldDisk",
    };

    private struct ShieldMat
    {
        public string AssetName;
        public Color BaseColor;      // lineaire (valeurs glTF)
        public float Metallic;
        public float Smoothness;     // 1 - roughness glTF
        public Color EmissiveColor;  // lineaire (emissiveFactor glTF), noir = pas d'emission
    }

    // Nom du materiau dans le FBX -> definition du materiau partage
    private static readonly Dictionary<string, ShieldMat> FbxMaterials = new()
    {
        { "shield_titanium",     new ShieldMat { AssetName = "Shield_Titanium",    BaseColor = new Color(0.430f, 0.490f, 0.510f), Metallic = 0.80f, Smoothness = 0.73f, EmissiveColor = Color.black } },
        { "shield_bright_edge",  new ShieldMat { AssetName = "Shield_BrightEdge",  BaseColor = new Color(0.700f, 0.760f, 0.770f), Metallic = 0.80f, Smoothness = 0.76f, EmissiveColor = Color.black } },
        { "shield_graphite",     new ShieldMat { AssetName = "Shield_Graphite",    BaseColor = new Color(0.065f, 0.085f, 0.095f), Metallic = 0.65f, Smoothness = 0.67f, EmissiveColor = Color.black } },
        { "shield_recess",       new ShieldMat { AssetName = "Shield_Recess",      BaseColor = new Color(0.013f, 0.022f, 0.028f), Metallic = 0.25f, Smoothness = 0.56f, EmissiveColor = Color.black } },
        { "shield_cyan_channel", new ShieldMat { AssetName = "Shield_CyanChannel", BaseColor = new Color(0.120f, 0.750f, 0.840f), Metallic = 0.30f, Smoothness = 0.78f, EmissiveColor = new Color(0.080f, 0.650f, 0.800f) } },
        { "shield_pale_emitter", new ShieldMat { AssetName = "Shield_PaleEmitter", BaseColor = new Color(0.550f, 0.920f, 0.920f), Metallic = 0.25f, Smoothness = 0.81f, EmissiveColor = new Color(0.300f, 0.800f, 0.850f) } },
        { "shield_blue_emitter", new ShieldMat { AssetName = "Shield_BlueEmitter", BaseColor = new Color(0.035f, 0.280f, 0.620f), Metallic = 0.35f, Smoothness = 0.77f, EmissiveColor = new Color(0.020f, 0.210f, 0.600f) } },
    };

    static ShieldDiskMaterialSetup()
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

    [MenuItem("Blockforge/Setup Shield Disk Materials")]
    public static void Setup()
    {
        var materials = CreateMaterials();
        RemapFbxMaterials(materials);
        Debug.Log($"[ShieldDiskMaterialSetup] Materiaux HDRP crees et remappes sur le FBX ShieldDisk.");
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
                Debug.LogError($"[ShieldDiskMaterialSetup] Importeur du FBX introuvable : {fbxPath}");
                continue;
            }

            foreach (var (matName, mat) in materials)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), matName), mat);

            importer.SaveAndReimport();
        }
    }
}
