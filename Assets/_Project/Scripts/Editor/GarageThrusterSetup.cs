using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

// =========================================================
// PROPULSEURS DU VAISSEAU (scene Garage)
// Menu : Blockforge > Setup Garage Thrusters
// - Importe les textures Art/Textures/Thruster{Plume,Core,
//   Spark}.png (generees par Tools/Garage/generate_thruster_textures.py)
// - Cree les materiaux HDRP/Unlit additifs Art/Garage/Thruster*
//   (emissif hors exposition : meme eclat quelle que soit
//   l'exposition automatique, bloom sur les zones > 1)
// - Pose un ancrage "Thruster_L/R" (ShipThrusterFX) au centre
//   de chaque disque cyan "Intake core" du Mothership, oriente
//   vers l'exterieur, avec une lumiere ponctuelle cyan
// Relancable : met a jour materiaux et positions, conserve
// les reglages faits sur les composants ShipThrusterFX.
// =========================================================

public static class GarageThrusterSetup
{
    private const string TextureDir = "Assets/_Project/Art/Textures";
    private const string MaterialDir = "Assets/_Project/Art/Garage";
    private const string RootName = "ShipThrusters";
    private const string EngineMeshName = "Intake core";

    // Lumiere du jet (unite par defaut d'une lumiere ponctuelle HDRP : lumen)
    private const float LightIntensity = 40000f;
    private const float LightRange = 40f;
    private static readonly Color LightColor = new(0.35f, 0.85f, 1f);

    [MenuItem("Blockforge/Setup Garage Thrusters")]
    public static void Setup()
    {
        var plume = EnsureMaterial("ThrusterPlume", ImportTexture("ThrusterPlume"), 2.5f);
        var core = EnsureMaterial("ThrusterCore", ImportTexture("ThrusterCore"), 4f);
        var spark = EnsureMaterial("ThrusterSpark", ImportTexture("ThrusterSpark"), 6f);
        if (plume == null || core == null || spark == null)
            return;

        if (!BlockforgeScenes.OpenForSetup(BlockforgeScenes.Garage, out var scene))
            return;

        var ship = GameObject.Find(GarageSceneSetup.ShipName);
        if (ship == null)
        {
            Debug.LogError($"[GarageThrusters] '{GarageSceneSetup.ShipName}' introuvable : lance d'abord Blockforge > Setup Garage Ship.");
            return;
        }

        var root = GameObject.Find(RootName);
        if (root == null)
            root = new GameObject(RootName);
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);

        int placed = 0;
        foreach (var renderer in ship.GetComponentsInChildren<Renderer>(true))
        {
            // glTFast suffixe les noms en double : "Intake core", "Intake core_0"
            string meshName = renderer.gameObject.name;
            if (meshName != EngineMeshName && !meshName.StartsWith(EngineMeshName + "_"))
                continue;

            var bounds = renderer.bounds;
            float outward = Mathf.Sign(bounds.center.z); // le disque regarde vers l'exterieur de la nacelle
            string side = bounds.center.x < 0f ? "L" : "R";
            var anchor = EnsureChild(root.transform, $"Thruster_{side}");
            anchor.SetPositionAndRotation(
                new Vector3(bounds.center.x, bounds.center.y, outward > 0f ? bounds.max.z + 0.02f : bounds.min.z - 0.02f),
                Quaternion.LookRotation(Vector3.forward * outward, Vector3.up));

            if (!anchor.TryGetComponent<ShipThrusterFX>(out var fx))
                fx = anchor.gameObject.AddComponent<ShipThrusterFX>();
            fx.plumeMaterial = plume;
            fx.coreMaterial = core;
            fx.sparkMaterial = spark;
            fx.flameLight = EnsureLight(anchor);
            EditorUtility.SetDirty(fx);
            placed++;
        }

        if (placed == 0)
        {
            Debug.LogError($"[GarageThrusters] Aucun moteur '{EngineMeshName}' sous le vaisseau.");
            return;
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[GarageThrusters] {placed} propulseur(s) poses sur les moteurs '{EngineMeshName}'. Scene sauvegardee.");
    }

    // =========================================================
    // ASSETS
    // =========================================================

    private static Texture2D ImportTexture(string name)
    {
        string path = $"{TextureDir}/{name}.png";
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null)
        {
            Debug.LogError($"[GarageThrusters] Texture introuvable : {path}");
            return null;
        }

        importer.textureType = TextureImporterType.Default;
        importer.textureShape = TextureImporterShape.Texture2D;
        importer.sRGBTexture = true;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = true;
        importer.wrapMode = TextureWrapMode.Clamp;   // U = travers du plan, V = longueur du jet
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
    }

    // HDRP/Unlit transparent additif. La couleur "unlit" est noire (elle subirait
    // l'exposition) : tout l'eclat passe par l'emissif, poids d'exposition nul.
    // ShipThrusterFX module ensuite _EmissiveColor (vacillement).
    private static Material EnsureMaterial(string name, Texture2D texture, float intensity)
    {
        if (texture == null)
            return null;

        string path = $"{MaterialDir}/{name}.mat";
        var shader = Shader.Find("HDRP/Unlit");
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null)
        {
            material = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(material, path);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
        }

        material.SetFloat("_SurfaceType", 1f);       // Transparent
        material.SetFloat("_BlendMode", 1f);         // Additive
        material.SetFloat("_DoubleSidedEnable", 1f);
        material.SetFloat("_EnableFogOnTransparent", 0f);
        material.SetColor("_UnlitColor", new Color(0f, 0f, 0f, 1f));
        material.SetTexture("_UnlitColorMap", texture);
        material.SetTexture("_EmissiveColorMap", texture);
        material.SetColor("_EmissiveColor", Color.white * intensity);
        material.SetFloat("_EmissiveExposureWeight", 0f);
        HDMaterial.ValidateMaterial(material);

        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        return material;
    }

    // =========================================================
    // SCENE
    // =========================================================

    private static Transform EnsureChild(Transform parent, string name)
    {
        var child = parent.Find(name);
        if (child != null)
            return child;
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        return go.transform;
    }

    private static Light EnsureLight(Transform anchor)
    {
        var lightTransform = EnsureChild(anchor, "FlameLight");
        lightTransform.localPosition = new Vector3(0f, 0f, 3f); // juste devant le disque
        lightTransform.localRotation = Quaternion.identity;

        if (!lightTransform.TryGetComponent<Light>(out var light))
        {
            light = lightTransform.gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.intensity = LightIntensity;
        }
        if (!lightTransform.TryGetComponent<HDAdditionalLightData>(out _))
            lightTransform.gameObject.AddComponent<HDAdditionalLightData>();

        light.color = LightColor;
        light.range = LightRange;
        light.shadows = LightShadows.None;
        EditorUtility.SetDirty(light);
        return light;
    }
}
