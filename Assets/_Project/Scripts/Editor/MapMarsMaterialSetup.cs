using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine;
using UnityEngine.SceneManagement;

// =========================================================
// SETUP DES MATERIAUX DE LA MAP MARS
// Menu : Blockforge > Setup Map MARS Materials
// - Configure l'import des textures de la map (mask map
//   lineaire, normal maps)
// - Cree les materiaux HDRP/Lit dans Art/Materials/Map
// - Remappe les materiaux du FBX Map_Mars vers ces materiaux
// - Remet l'instance Map_Mars de la scene Map_MARS a l'origine
// S'execute une seule fois automatiquement apres compilation
// (marqueur dans Library/).
// =========================================================

[InitializeOnLoad]
public static class MapMarsMaterialSetup
{
    private const string FbxPath = "Assets/_Project/Art/Models/Map_Mars.fbx";
    private const string TextureDir = "Assets/_Project/Art/Textures/Map";
    private const string MaterialParentDir = "Assets/_Project/Art/Materials";
    private const string MaterialDir = "Assets/_Project/Art/Materials/Map";
    private const string ScenePath = "Assets/_Project/Scenes/Map_MARS.unity";
    private const string AutoRunMarker = "Library/BlockforgeMapMarsMaterials.done";

    // Nom du materiau dans le FBX -> base du nom des textures/materiaux
    private static readonly Dictionary<string, string> FbxMaterials = new()
    {
        { "MAT_CW_Mars_Sand", "Mars_Sand" },
        { "MAT_CW_Mars_Rock", "Mars_Rock" },
        { "MAT_CW_Mars_Rim", "Mars_Rim" },
        { "MAT_CW_Mars_Basin", "Mars_Basin" },
        { "MAT_CW_Mars_Road", "Mars_Road" },
        { "MAT_CW_Objective_Concrete", "Objective_Concrete" },
        { "MAT_CW_Objective_Gunmetal", "Objective_Gunmetal" },
        { "MAT_CW_Objective_Amber", "Objective_Amber" },
        { "MAT_CW_Steel", "Steel" },
    };

    static MapMarsMaterialSetup()
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

    [MenuItem("Blockforge/Setup Map MARS Materials")]
    public static void Setup()
    {
        ConfigureTextureImporters();
        var materials = CreateMaterials();
        RemapFbxMaterials(materials);
        ResetMapInstance();
        Debug.Log("[MapMarsMaterialSetup] Textures configurees, materiaux HDRP crees et remappes sur Map_Mars.fbx.");
    }

    private static void ConfigureTextureImporters()
    {
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { TextureDir }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                continue;

            var dirty = false;
            if (path.EndsWith("_Normal.png"))
            {
                if (importer.textureType != TextureImporterType.NormalMap)
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    dirty = true;
                }
            }
            else if (path.EndsWith("_MaskMap.png"))
            {
                // Mask map HDRP (metallic/AO/smoothness) : donnees lineaires
                if (importer.sRGBTexture)
                {
                    importer.sRGBTexture = false;
                    dirty = true;
                }
            }

            if (dirty)
                importer.SaveAndReimport();
        }
    }

    private static Dictionary<string, Material> CreateMaterials()
    {
        if (!AssetDatabase.IsValidFolder(MaterialDir))
            AssetDatabase.CreateFolder(MaterialParentDir, "Map");

        var shader = Shader.Find("HDRP/Lit");
        var result = new Dictionary<string, Material>();

        foreach (var (fbxName, baseName) in FbxMaterials)
        {
            var matPath = $"{MaterialDir}/{baseName}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, matPath);
            }

            mat.SetColor("_BaseColor", Color.white);
            mat.SetTexture("_BaseColorMap", LoadTex(baseName, "Albedo"));

            var mask = LoadTex(baseName, "MaskMap");
            if (mask != null)
                mat.SetTexture("_MaskMap", mask);

            var normal = LoadTex(baseName, "Normal");
            if (normal != null)
                mat.SetTexture("_NormalMap", normal);

            HDMaterial.ValidateMaterial(mat);
            EditorUtility.SetDirty(mat);
            result[fbxName] = mat;
        }

        AssetDatabase.SaveAssets();
        return result;
    }

    private static Texture2D LoadTex(string baseName, string suffix)
        => AssetDatabase.LoadAssetAtPath<Texture2D>($"{TextureDir}/{baseName}_{suffix}.png");

    private static void RemapFbxMaterials(Dictionary<string, Material> materials)
    {
        if (AssetImporter.GetAtPath(FbxPath) is not ModelImporter importer)
        {
            Debug.LogError($"[MapMarsMaterialSetup] Importeur du FBX introuvable : {FbxPath}");
            return;
        }

        foreach (var (fbxName, mat) in materials)
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), fbxName), mat);

        importer.SaveAndReimport();
    }

    private static void ResetMapInstance()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath)
            return;

        foreach (var root in scene.GetRootGameObjects())
        {
            if (root.name != "Map_Mars")
                continue;

            root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            root.transform.localScale = Vector3.one;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            break;
        }
    }
}
