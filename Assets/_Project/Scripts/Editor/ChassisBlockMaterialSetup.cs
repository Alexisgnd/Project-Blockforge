using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine;

// =========================================================
// SETUP DES MATERIAUX DES 17 BLOCS CHASSIS
// Menu : Blockforge > Setup Chassis Block Materials
// - Cree les materiaux HDRP/Lit textures de la palette
//   partagee des chassis dans Art/Materials/Blocks :
//     Chassis_Armor : armure blanche a panneaux (blocs 01-12)
//     Rod_*         : metal brosse teinte (tiges 13-17)
//   Les textures viennent des GLB sources (extraites en PNG
//   dans Art/Textures/Blocks) ; metallic/smoothness sont des
//   constantes glTF -> pas de mask map.
// - Remappe les materiaux des 17 FBX vers ces materiaux
// S'execute une seule fois automatiquement apres compilation
// (marqueur dans Library/).
// =========================================================

[InitializeOnLoad]
public static class ChassisBlockMaterialSetup
{
    private const string ModelDir = "Assets/_Project/Art/Models/Blocks/Chassis";
    private const string TextureDir = "Assets/_Project/Art/Textures/Blocks";
    private const string MaterialParentDir = "Assets/_Project/Art/Materials";
    private const string MaterialDir = "Assets/_Project/Art/Materials/Blocks";
    private const string AutoRunMarker = "Library/BlockforgeChassisBlockMaterials.done";

    private const string ArmorTexture = "Chassis_ArmorPanels";
    private const string MetalTexture = "Chassis_BrushedMetal";

    public static readonly string[] FbxNames =
    {
        "Chassis_Cube",
        "Chassis_Pente",
        "Chassis_Coin",
        "Chassis_CoinInterieur",
        "Chassis_PenteArrondie",
        "Chassis_CoinArrondi",
        "Chassis_CoinInterieurArrondi",
        "Chassis_PenteConcave",
        "Chassis_CoinConcave",
        "Chassis_CoinInterieurConcave",
        "Chassis_Cone",
        "Chassis_Pyramide",
        "Rod_Court",
        "Rod_Long",
        "Rod_Arc",
        "Rod_Diag2D",
        "Rod_Diag3D",
    };

    private struct ChassisMat
    {
        public string AssetName;
        public string Texture;     // nom du PNG dans Art/Textures/Blocks (albedo sRGB)
        public Color BaseColor;    // facteur glTF (lineaire), multiplie la texture
        public float Metallic;
        public float Smoothness;   // 1 - roughness glTF
    }

    // Nom du materiau dans les FBX -> definition du materiau partage
    private static readonly Dictionary<string, ChassisMat> FbxMaterials = new()
    {
        { "chassis_armor", new ChassisMat { AssetName = "Chassis_Armor", Texture = ArmorTexture, BaseColor = Color.white,                     Metallic = 0.15f, Smoothness = 0.52f } },
        { "rod_plate",     new ChassisMat { AssetName = "Rod_Plate",     Texture = MetalTexture, BaseColor = new Color(0.84f, 0.87f, 0.90f), Metallic = 0.82f, Smoothness = 0.70f } },
        { "rod_gasket",    new ChassisMat { AssetName = "Rod_Gasket",    Texture = MetalTexture, BaseColor = new Color(0.12f, 0.15f, 0.18f), Metallic = 0.25f, Smoothness = 0.48f } },
        { "rod_collar",    new ChassisMat { AssetName = "Rod_Collar",    Texture = MetalTexture, BaseColor = new Color(0.57f, 0.62f, 0.67f), Metallic = 0.82f, Smoothness = 0.70f } },
        { "rod_screw",     new ChassisMat { AssetName = "Rod_Screw",     Texture = MetalTexture, BaseColor = new Color(0.23f, 0.27f, 0.30f), Metallic = 0.25f, Smoothness = 0.48f } },
        { "rod_core",      new ChassisMat { AssetName = "Rod_Core",      Texture = MetalTexture, BaseColor = new Color(0.30f, 0.34f, 0.38f), Metallic = 0.82f, Smoothness = 0.70f } },
        { "rod_facing",    new ChassisMat { AssetName = "Rod_Facing",    Texture = MetalTexture, BaseColor = new Color(0.88f, 0.91f, 0.94f), Metallic = 0.82f, Smoothness = 0.70f } },
        { "rod_ring",      new ChassisMat { AssetName = "Rod_Ring",      Texture = MetalTexture, BaseColor = new Color(0.66f, 0.71f, 0.77f), Metallic = 0.82f, Smoothness = 0.70f } },
    };

    static ChassisBlockMaterialSetup()
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

    [MenuItem("Blockforge/Setup Chassis Block Materials")]
    public static void Setup()
    {
        var materials = CreateMaterials();
        RemapFbxMaterials(materials);
        Debug.Log($"[ChassisBlockMaterialSetup] Materiaux HDRP crees et remappes sur les {FbxNames.Length} FBX chassis.");
    }

    private static Texture2D LoadAlbedo(string name)
    {
        string path = $"{TextureDir}/{name}.png";
        if (AssetImporter.GetAtPath(path) is TextureImporter importer)
        {
            bool dirty = false;
            if (importer.textureType != TextureImporterType.Default) { importer.textureType = TextureImporterType.Default; dirty = true; }
            if (!importer.sRGBTexture) { importer.sRGBTexture = true; dirty = true; }
            if (importer.wrapMode != TextureWrapMode.Repeat) { importer.wrapMode = TextureWrapMode.Repeat; dirty = true; }
            if (dirty)
                importer.SaveAndReimport();
        }

        var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        if (tex == null)
            Debug.LogWarning($"[ChassisBlockMaterialSetup] Texture introuvable : {path}");
        return tex;
    }

    private static Dictionary<string, Material> CreateMaterials()
    {
        if (!AssetDatabase.IsValidFolder(MaterialDir))
            AssetDatabase.CreateFolder(MaterialParentDir, "Blocks");

        var shader = Shader.Find("HDRP/Lit");
        var textures = new Dictionary<string, Texture2D>
        {
            { ArmorTexture, LoadAlbedo(ArmorTexture) },
            { MetalTexture, LoadAlbedo(MetalTexture) },
        };
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

            mat.SetTexture("_BaseColorMap", textures[def.Texture]);
            mat.SetColor("_BaseColor", def.BaseColor);
            mat.SetFloat("_Metallic", def.Metallic);
            mat.SetFloat("_Smoothness", def.Smoothness);
            mat.SetColor("_EmissiveColor", Color.black);

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
                Debug.LogError($"[ChassisBlockMaterialSetup] Importeur du FBX introuvable : {fbxPath}");
                continue;
            }

            foreach (var (matName, mat) in materials)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), matName), mat);

            importer.SaveAndReimport();
        }
    }
}
