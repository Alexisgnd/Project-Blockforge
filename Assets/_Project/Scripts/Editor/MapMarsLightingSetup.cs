using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
using UnityEngine.SceneManagement;

// =========================================================
// ECLAIRAGE DE LA SCENE MAP_MARS
// Menu : Blockforge > Setup Map MARS Lighting
// La map v2 (Map_Mars_v2.glb) paraissait trop sombre avec le
// profil Sky and Fog partage (exposition auto histogramme).
// - Volume global "Map Lighting Override" (priorite 10) propre
//   a Map_MARS, avec son profil dans Art/MapMars/ : le profil
//   partage avec Map_Test n'est pas modifie
//     * Exposure : compensation +ExposureCompensation EV
//     * Indirect Lighting Controller : lumiere indirecte
//       (ciel + rebonds) x IndirectDiffuseMultiplier pour
//       deboucher les ombres des falaises et rochers
// - Soleil remonte a SunElevation degres (ombres moins longues)
// Reglages en tete de fichier ; relancer le menu apres modif.
// S'execute une fois tout seul apres compilation si Map_MARS
// est la scene active (marqueur dans Library/).
// =========================================================

[InitializeOnLoad]
public static class MapMarsLightingSetup
{
    private const string ScenePath = BlockforgeScenes.MapMars;
    private const string ProfileDir = "Assets/_Project/Art/MapMars";
    private const string ProfilePath = ProfileDir + "/MapMarsLightingProfile.asset";
    private const string VolumeName = "Map Lighting Override";
    private const string AutoRunMarker = "Library/BlockforgeMapMarsLighting-v1.done";

    // Reglages
    public const float ExposureCompensation = 1.0f;     // EV (+1 = deux fois plus clair)
    public const float IndirectDiffuseMultiplier = 2.0f; // 1 = valeur HDRP par defaut
    public const float SunElevation = 55f;              // degres au-dessus de l'horizon (avant : 45)

    static MapMarsLightingSetup()
    {
        EditorApplication.delayCall += AutoRunOnce;
    }

    private static void AutoRunOnce()
    {
        if (File.Exists(AutoRunMarker) || EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        if (SceneManager.GetActiveScene().path != ScenePath)
            return;

        if (Setup())
            File.WriteAllText(AutoRunMarker, "done");
    }

    [MenuItem("Blockforge/Setup Map MARS Lighting")]
    public static void SetupFromMenu()
    {
        if (!BlockforgeScenes.OpenForSetup(ScenePath, out _))
            return;

        if (Setup())
            File.WriteAllText(AutoRunMarker, "done");
    }

    private static bool Setup()
    {
        var scene = SceneManager.GetActiveScene();
        var profile = GetOrCreateProfile();

        // Volume global dedie, au-dessus du volume Sky and Fog (priorite 0)
        var volume = Object.FindObjectsByType<Volume>(FindObjectsInactive.Include)
                           .FirstOrDefault(v => v.gameObject.name == VolumeName);
        if (volume == null)
        {
            var go = new GameObject(VolumeName);
            SceneManager.MoveGameObjectToScene(go, scene);
            volume = go.AddComponent<Volume>();
        }
        volume.isGlobal = true;
        volume.priority = 10f;
        volume.weight = 1f;
        volume.sharedProfile = profile;
        EditorUtility.SetDirty(volume);

        string sunInfo = "aucun soleil (Directional Light) trouve";
        var sun = Object.FindObjectsByType<Light>(FindObjectsInactive.Include)
                        .FirstOrDefault(l => l.type == LightType.Directional);
        if (sun != null)
        {
            var euler = sun.transform.eulerAngles;
            sun.transform.rotation = Quaternion.Euler(SunElevation, euler.y, 0f);
            EditorUtility.SetDirty(sun.transform);
            sunInfo = $"soleil '{sun.name}' a {SunElevation} deg";
        }

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[MapMarsLightingSetup] Map_MARS eclaircie : exposition +{ExposureCompensation} EV, " +
                  $"lumiere indirecte x{IndirectDiffuseMultiplier}, {sunInfo}. Scene sauvegardee.");
        return true;
    }

    private static VolumeProfile GetOrCreateProfile()
    {
        if (!AssetDatabase.IsValidFolder(ProfileDir))
            AssetDatabase.CreateFolder("Assets/_Project/Art", "MapMars");

        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile == null)
        {
            profile = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(profile, ProfilePath);
        }

        if (!profile.TryGet(out Exposure exposure))
        {
            exposure = profile.Add<Exposure>(false);
            AssetDatabase.AddObjectToAsset(exposure, profile);
        }
        // Seule la compensation est surchargee : le mode (auto histogramme) et
        // les limites restent ceux du profil Sky and Fog
        exposure.compensation.Override(ExposureCompensation);

        if (!profile.TryGet(out IndirectLightingController indirect))
        {
            indirect = profile.Add<IndirectLightingController>(false);
            AssetDatabase.AddObjectToAsset(indirect, profile);
        }
        indirect.indirectDiffuseLightingMultiplier.Override(IndirectDiffuseMultiplier);

        EditorUtility.SetDirty(exposure);
        EditorUtility.SetDirty(indirect);
        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        return profile;
    }
}
