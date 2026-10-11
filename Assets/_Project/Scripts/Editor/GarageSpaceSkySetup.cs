using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

// =========================================================
// CIEL SPATIAL DU GARAGE (noir + etoiles)
// Menu : Blockforge > Setup Garage Space Sky
//
// Pourquoi pas un simple ciel HDRI noir ? En HDRP le ciel du
// Visual Environment eclaire aussi la scene (lumiere ambiante,
// reflets) : un ciel noir eteint l'interieur du hangar et
// l'exposition automatique surexpose alors sol et neons.
// On garde donc le ciel du projet pour l'ECLAIRAGE et on ne
// change que ce que la camera AFFICHE :
// - camera du joueur : fond = couleur noire au lieu du ciel
//   (HDAdditionalCameraData.clearColorMode = Color) ;
// - "SpaceStarDome" : sphere de 2 km de rayon autour du
//   vaisseau, materiau HDRP/Unlit emissif (champ d'etoiles
//   Art/Textures/SpaceStarfield_Dome.png, projection
//   equirectangulaire generee par script), poids
//   d'exposition 0 : les etoiles gardent leur eclat quelle
//   que soit l'exposition automatique ;
// - "GarageExposureVolume" : l'exposition auto mesurait la
//   moyenne de l'image, fond noir compris, et surexposait
//   tout. Mode histogramme : les pixels les plus sombres
//   (l'espace) sont ignores, et l'exposition est bornee.
// Relancable : met a jour texture, materiau, dome, camera,
// exposition (les valeurs se retouchent ensuite dans
// Art/Garage/GarageExposureProfile).
// =========================================================

public static class GarageSpaceSkySetup
{
    private const string DomeTexturePath = "Assets/_Project/Art/Textures/SpaceStarfield_Dome.png";
    private const string DomeMaterialPath = "Assets/_Project/Art/Garage/SpaceStarDome.mat";
    private const string DomeName = "SpaceStarDome";
    private const string ExposureProfilePath = "Assets/_Project/Art/Garage/GarageExposureProfile.asset";
    private const string ExposureVolumeName = "GarageExposureVolume";

    // Exposition (EV100) : bornes autour de l'exposition du garage avec le
    // ciel du projet (soleil ~18 000 lux) ; histogramme = part des pixels
    // pris en compte, du plus sombre au plus clair (0-50 % ignores).
    private const float ExposureMin = 11.5f;
    private const float ExposureMax = 14f;
    private static readonly Vector2 HistogramPercentages = new(50f, 90f);
    private const float DomeRadius = 2000f; // camera : far clip 1 000 km

    // Eclat des etoiles (emissif lineaire, hors exposition) : 1 = blanc,
    // au-dessus les plus brillantes declenchent un peu de bloom.
    private const float StarIntensity = 1.3f;

    // Premiere version (ciel HDRI noir), abandonnee : nettoyee si presente
    private const string ObsoleteVolumeName = "SpaceSkyVolume";
    private const string ObsoleteProfilePath = "Assets/_Project/Art/Garage/GarageSpaceVolumeProfile.asset";
    private const string ObsoleteCubemapPath = "Assets/_Project/Art/Textures/SpaceStarfield_Cube.png";

    [MenuItem("Blockforge/Setup Garage Space Sky")]
    public static void Setup()
    {
        var texture = ImportDomeTexture();
        if (texture == null)
        {
            Debug.LogError($"[GarageSpaceSky] Texture introuvable : {DomeTexturePath}");
            return;
        }

        var material = EnsureDomeMaterial(texture);

        if (!BlockforgeScenes.OpenForSetup(BlockforgeScenes.Garage, out var scene))
            return;

        RemoveObsoleteHdriSky();
        EnsureDome(material);
        int cameras = SetCameraBackgroundBlack();
        EnsureExposureVolume();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[GarageSpaceSky] Ciel spatial applique au Garage : dome '{DomeName}' (rayon {DomeRadius} m), " +
                  $"fond noir sur {cameras} camera(s), exposition histogramme [{ExposureMin} ; {ExposureMax}] EV, " +
                  "eclairage du ciel du projet conserve. Scene sauvegardee.");
    }

    // =========================================================
    // TEXTURE + MATERIAU
    // =========================================================

    private static Texture2D ImportDomeTexture()
    {
        var importer = AssetImporter.GetAtPath(DomeTexturePath) as TextureImporter;
        if (importer == null)
            return null;

        importer.textureType = TextureImporterType.Default;
        importer.textureShape = TextureImporterShape.Texture2D;
        importer.sRGBTexture = true;
        importer.mipmapEnabled = true;
        importer.wrapModeU = TextureWrapMode.Repeat; // longitude : raccord continu
        importer.wrapModeV = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 4;
        importer.maxTextureSize = 4096;
        importer.textureCompression = TextureImporterCompression.CompressedHQ;
        importer.SaveAndReimport();

        return AssetDatabase.LoadAssetAtPath<Texture2D>(DomeTexturePath);
    }

    private static Material EnsureDomeMaterial(Texture2D texture)
    {
        var shader = Shader.Find("HDRP/Unlit");
        var material = AssetDatabase.LoadAssetAtPath<Material>(DomeMaterialPath);
        if (material == null)
        {
            material = new Material(shader) { name = "SpaceStarDome" };
            AssetDatabase.CreateAsset(material, DomeMaterialPath);
        }
        else if (material.shader != shader)
        {
            material.shader = shader;
        }

        // Couleur "unlit" noire (elle subit l'exposition) ; les etoiles passent
        // par l'emissif avec un poids d'exposition nul.
        material.SetColor("_UnlitColor", Color.black);
        material.SetTexture("_UnlitColorMap", null);
        material.SetTexture("_EmissiveColorMap", texture);
        material.SetColor("_EmissiveColor", Color.white * StarIntensity);
        material.SetFloat("_EmissiveExposureWeight", 0f);
        material.SetFloat("_DoubleSidedEnable", 1f); // vu de l'interieur de la sphere
        HDMaterial.ValidateMaterial(material);

        EditorUtility.SetDirty(material);
        AssetDatabase.SaveAssets();
        return material;
    }

    // =========================================================
    // SCENE
    // =========================================================

    private static void EnsureDome(Material material)
    {
        var dome = GameObject.Find(DomeName);
        if (dome == null)
        {
            dome = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            dome.name = DomeName;
            Object.DestroyImmediate(dome.GetComponent<Collider>());
        }

        dome.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        dome.transform.localScale = Vector3.one * (DomeRadius * 2f); // sphere primitive : diametre 1

        var renderer = dome.GetComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        renderer.allowOcclusionWhenDynamic = false;
        EditorUtility.SetDirty(dome);
    }

    // Camera du joueur (tag MainCamera) : fond noir au lieu du ciel. Le ciel
    // reste dans le Visual Environment pour la lumiere ambiante et les reflets.
    private static int SetCameraBackgroundBlack()
    {
        int count = 0;
        foreach (var camera in Object.FindObjectsByType<Camera>(FindObjectsInactive.Include))
        {
            if (!camera.CompareTag("MainCamera") && camera.GetComponentInParent<PlayerGarageController>(true) == null)
                continue;

            var hd = camera.GetComponent<HDAdditionalCameraData>();
            if (hd == null)
                hd = camera.gameObject.AddComponent<HDAdditionalCameraData>();
            hd.clearColorMode = HDAdditionalCameraData.ClearColorMode.Color;
            hd.backgroundColorHDR = Color.black;
            EditorUtility.SetDirty(hd);
            count++;
        }
        return count;
    }

    private static void EnsureExposureVolume()
    {
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ExposureProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ExposureProfilePath);

            var exposure = profile.Add<Exposure>(false);
            exposure.name = nameof(Exposure);
            AssetDatabase.AddObjectToAsset(exposure, profile);
            exposure.mode.Override(ExposureMode.AutomaticHistogram);
            exposure.limitMin.Override(ExposureMin);
            exposure.limitMax.Override(ExposureMax);
            exposure.histogramPercentages.Override(HistogramPercentages);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }

        var go = GameObject.Find(ExposureVolumeName);
        if (go == null)
            go = new GameObject(ExposureVolumeName);
        if (!go.TryGetComponent<Volume>(out var volume))
            volume = go.AddComponent<Volume>();
        volume.isGlobal = true;
        volume.priority = 10f;
        volume.sharedProfile = profile;
        EditorUtility.SetDirty(volume);
    }

    private static void RemoveObsoleteHdriSky()
    {
        var volume = GameObject.Find(ObsoleteVolumeName);
        if (volume != null)
            Object.DestroyImmediate(volume);
        if (AssetDatabase.LoadAssetAtPath<Object>(ObsoleteProfilePath) != null)
            AssetDatabase.DeleteAsset(ObsoleteProfilePath);
        if (AssetDatabase.LoadAssetAtPath<Object>(ObsoleteCubemapPath) != null)
            AssetDatabase.DeleteAsset(ObsoleteCubemapPath);
    }
}
