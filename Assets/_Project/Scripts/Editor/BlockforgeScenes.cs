using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Profile;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// =========================================================
// SCENES DU PROJET (outils editeur)
// - Chemins canoniques des scenes et ordre du build
// - OpenForSetup : ouvre une scene pour un setup si elle n'est
//   pas deja active (invite de sauvegarde de la scene courante)
// - RegisterAllInBuildSettings : liste globale des scenes ET
//   liste du profil de build actif (Assets/Settings/Build
//   Profiles/Windows Dev.asset surcharge la liste globale :
//   sans lui, SceneManager.LoadScene echoue meme en Play mode)
// - La scene garage "active" (Garage ou Garage_V2) est celle
//   que les setups du garage modifient.
// =========================================================

public static class BlockforgeScenes
{
    public const string Boot = "Assets/_Project/Scenes/Boot.unity";
    public const string MainMenu = "Assets/_Project/Scenes/MainMenu.unity";
    public const string Garage = "Assets/_Project/Scenes/Garage.unity";
    public const string GarageV2 = "Assets/_Project/Scenes/Garage_V2.unity";
    public const string MapMars = "Assets/_Project/Scenes/Map_MARS.unity";
    public const string MapTest = "Assets/_Project/Scenes/Map_Test.unity";

    // Boot doit rester en index 0 (premiere scene chargee par le build)
    public static readonly string[] BuildOrder = { Boot, MainMenu, Garage, GarageV2, MapMars, MapTest };

    public static bool IsGarageScene(string path)
    {
        return path == Garage || path == GarageV2;
    }

    // Scene garage ciblee par les setups : la scene active si c'est un garage,
    // sinon Garage.unity (comportement historique).
    public static string ResolveGarageScenePath()
    {
        string active = SceneManager.GetActiveScene().path;
        return IsGarageScene(active) ? active : Garage;
    }

    // Ouvre la scene si elle n'est pas deja active. Deja active : on continue
    // dedans, meme non sauvegardee (le setup la sauvegarde a la fin, comme
    // l'aurait fait l'invite). false = l'utilisateur a annule l'invite.
    public static bool OpenForSetup(string path, out Scene scene)
    {
        scene = SceneManager.GetActiveScene();
        if (scene.path == path)
            return true;

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return false;

        scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
        return true;
    }

    // Enregistre les scenes existantes dans l'ordre canonique (Boot en 0), en
    // conservant l'etat active/inactive des entrees deja presentes et les
    // scenes inconnues existantes ; applique la meme liste au profil de build
    // actif quand il surcharge la liste globale.
    public static void RegisterAllInBuildSettings()
    {
        EditorBuildSettings.scenes = Merge(EditorBuildSettings.scenes);

        var profile = BuildProfile.GetActiveBuildProfile();
        if (profile != null && profile.overrideGlobalScenes)
        {
            profile.scenes = Merge(profile.scenes);
            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssetIfDirty(profile);
            Debug.Log($"[BlockforgeScenes] Scenes enregistrees dans la liste globale et le profil de build '{profile.name}' : " +
                      string.Join(", ", profile.scenes.Select(s => Path.GetFileNameWithoutExtension(s.path) + (s.enabled ? "" : " (inactive)"))));
        }
        else
        {
            Debug.Log("[BlockforgeScenes] Scenes enregistrees dans la liste globale : " +
                      string.Join(", ", EditorBuildSettings.scenes.Select(s => Path.GetFileNameWithoutExtension(s.path))));
        }
    }

    // Charge un modele (FBX ou GLB via glTFast) comme prefab instanciable. Un
    // GLB copie dans le projet avant l'installation de glTFast a ete importe en
    // asset generique : on force une reimportation une fois avant d'abandonner.
    public static GameObject LoadModelAsset(string path)
    {
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        if (model != null || !File.Exists(path))
            return model;

        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        return AssetDatabase.LoadAssetAtPath<GameObject>(path);
    }

    private static EditorBuildSettingsScene[] Merge(EditorBuildSettingsScene[] existing)
    {
        var enabledByPath = new Dictionary<string, bool>();
        foreach (var s in existing)
            enabledByPath[s.path] = s.enabled;

        var result = new List<EditorBuildSettingsScene>();
        foreach (var path in BuildOrder)
        {
            if (!File.Exists(path))
                continue;
            bool enabled = !enabledByPath.TryGetValue(path, out bool known) || known;
            result.Add(new EditorBuildSettingsScene(path, enabled));
        }

        // Scenes hors liste canonique : conservees si le fichier existe encore
        // (supprime au passage les entrees fantomes comme Arena.unity)
        foreach (var s in existing)
        {
            if (!BuildOrder.Contains(s.path) && File.Exists(s.path))
                result.Add(new EditorBuildSettingsScene(s.path, s.enabled));
        }
        return result.ToArray();
    }
}
