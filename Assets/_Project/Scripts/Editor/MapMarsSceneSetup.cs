using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// =========================================================
// SETUP DE LA SCENE MAP_MARS
// Menu : Blockforge > Setup Map MARS Scene
// - Map v6 (21/10/2026, « v3_3 » : ponts sans garde-corps ; sinon
//   comme la v5 « v3_2 » / Mars Map TDM v4 : bases en
//   plateau a 3 sorties, routes de 13-14 m, vallees a rampes,
//   5 ponts par equipe, buttes et eclats rocheux) : GLB
//   Map_Mars_v6.glb (export Three.js de Map_Mars_TDM, memes
//   marqueurs de spawn) importe par glTFast ; remplace la v4, la
//   v3, la v2 et l'ancien Map_Mars.fbx, conserves dans le projet
//   mais plus instancies
// - Si la nouvelle version est deposee a la racine du projet
//   (Map_Mars_TDM_v3_3.glb, v3_2 ou Map_Mars_TDM.glb), elle est deplacee
//   et renommee en Art/Models/Map_Mars_v6.glb puis importee, et
//   la scene est mise a jour toute seule (verification toutes les
//   3 s hors Play mode) ; deposee directement sous ce nom dans
//   Art/Models, son import declenche aussi le setup
// - Retire du GLB le prefixe CW_ (ancien nom du projet,
//   "Colors War") sur les noeuds et materiaux :
//   CW_Rock_001 -> Rock_001, MAT_CW_Mars_Sand -> MAT_Mars_Sand
//   (reecriture du bloc JSON du GLB, le binaire est intact)
// - Retire l'instance precedente de la map (Map_Mars FBX,
//   Map_Mars_v2 ou v3), instancie le GLB a l'identite (Y-up,
//   metres : pas de rotation -90 comme pour le FBX)
// - MeshCollider sur chaque maillage solide (terrain, rochers,
//   ponts, plateformes de spawn) ; sans collision : le decor
//   exterieur Map_Mars_Surround (4 x 5,2 km, hors zone de
//   jeu) et les marqueurs Markers (noeuds vides)
// - Materiaux : ceux embarques dans le GLB (MAT_Mars_Sand...),
//   convertis en HDRP par glTFast
// S'execute aussi une seule fois tout seul apres compilation
// si la scene Map_MARS est ouverte ou si la nouvelle map attend
// a la racine du projet (marqueur dans Library/).
// =========================================================

[InitializeOnLoad]
public static class MapMarsSceneSetup
{
    private const string ScenePath = BlockforgeScenes.MapMars;
    public const string ModelPath = "Assets/_Project/Art/Models/Map_Mars_v6.glb";
    // Versions precedentes, conservees pour reference (plus instanciees)
    public const string LegacyFbxPath = "Assets/_Project/Art/Models/Map_Mars.fbx";
    public const string LegacyV2Path = "Assets/_Project/Art/Models/Map_Mars_v2.glb";
    public const string LegacyV3Path = "Assets/_Project/Art/Models/Map_Mars_v3.glb";
    public const string LegacyV4Path = "Assets/_Project/Art/Models/Map_Mars_v4.glb";
    public const string LegacyV5Path = "Assets/_Project/Art/Models/Map_Mars_v5.glb";
    // Nouvelle version telle que deposee a la racine du projet (premier nom trouve)
    private static readonly string[] IncomingModelPaths = { "Map_Mars_TDM_v3_3.glb", "Map_Mars_TDM_v3_2.glb", "Map_Mars_TDM.glb" };
    public const string MapObjectName = "Map_Mars_v6";
    // v8 : GLB Map_Mars_v6 (v3_3 : ponts sans garde-corps) sans prefixe CW_
    private const string AutoRunMarker = "Library/BlockforgeMapMarsSetup-v8.done";

    // Instances des versions precedentes de la map a retirer
    private static readonly string[] PreviousMapNames = { "Map_Mars", "Map_Mars_v2", "Map_Mars_v3", "Map_Mars_v4", "Map_Mars_v5", MapObjectName, "Map_Mars_TDM", "CW_Map_Mars_TDM" };

    private static double nextIncomingPoll;

    // Sous-arbres sans collision
    private static readonly string[] NoCollisionGroupNames =
    {
        "Map_Mars_Surround", // decor exterieur, hors d'atteinte
        "Markers",           // marqueurs de spawn / centre (noeuds vides)
    };

    static MapMarsSceneSetup()
    {
        EditorApplication.delayCall += AutoRunOnce;
        EditorApplication.update += PollIncoming;
    }

    private static string IncomingModelPath()
    {
        foreach (var path in IncomingModelPaths)
        {
            if (File.Exists(path))
                return path;
        }
        return null;
    }

    // Une map deposee a la racine pendant que l'editeur tourne : rien ne recompile,
    // donc on la guette (toutes les 3 s, hors Play mode et hors import)
    private static void PollIncoming()
    {
        if (EditorApplication.timeSinceStartup < nextIncomingPoll)
            return;
        nextIncomingPoll = EditorApplication.timeSinceStartup + 3.0;
        if (EditorApplication.isPlayingOrWillChangePlaymode || EditorApplication.isCompiling || EditorApplication.isUpdating)
            return;
        if (IncomingModelPath() == null || File.Exists(ModelPath))
            return;
        Debug.Log("[MapMarsSceneSetup] Nouvelle map Mars detectee a la racine du projet : mise a jour de Map_MARS.");
        RunInMapScene();
    }

    // Import direct de Art/Models/Map_Mars_v3.glb (glisse dans le projet) : setup a la suite
    private sealed class ImportWatcher : AssetPostprocessor
    {
        private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
        {
            if (File.Exists(AutoRunMarker) || System.Array.IndexOf(imported, ModelPath) < 0)
                return;
            EditorApplication.delayCall += () =>
            {
                if (!EditorApplication.isPlayingOrWillChangePlaymode && !File.Exists(AutoRunMarker))
                    RunInMapScene();
            };
        }
    }

    // Ouvre Map_MARS le temps du setup puis revient a la scene d'origine
    private static void RunInMapScene()
    {
        string previous = SceneManager.GetActiveScene().path;
        bool sceneActive = previous == ScenePath;
        if (!BlockforgeScenes.OpenForSetup(ScenePath, out _))
            return;
        if (Setup())
            File.WriteAllText(AutoRunMarker, "done");
        if (!sceneActive && !string.IsNullOrEmpty(previous) && File.Exists(previous))
            EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
    }

    // Lancement automatique one-shot si Map_MARS est deja la scene
    // active, ou si une nouvelle version vient d'etre deposee a la
    // racine du projet (Map_Mars_TDM.glb) : dans ce cas on ouvre
    // Map_MARS le temps du setup puis on revient a la scene d'origine.
    private static void AutoRunOnce()
    {
        if (File.Exists(AutoRunMarker) || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        // Rien a faire tant que la v3 n'est ni dans le projet ni a la racine
        if (!File.Exists(ModelPath) && IncomingModelPath() == null)
            return;
        if (EditorApplication.isCompiling || EditorApplication.isUpdating)
        {
            EditorApplication.delayCall += AutoRunOnce;
            return;
        }
        RunInMapScene();
    }

    [MenuItem("Blockforge/Setup Map MARS Scene")]
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

        ImportIncomingModel();
        StripLegacyPrefix();

        var model = BlockforgeScenes.LoadModelAsset(ModelPath);
        if (model == null)
        {
            Debug.LogError($"[MapMarsSceneSetup] GLB introuvable ou pas encore importe : {ModelPath} " +
                           "(importeur glTFast com.unity.cloud.gltfast). Relance le menu une fois l'import termine.");
            return false;
        }

        var legacyFbx = AssetDatabase.LoadAssetAtPath<GameObject>(LegacyFbxPath);
        var legacyV2 = AssetDatabase.LoadAssetAtPath<GameObject>(LegacyV2Path);
        var legacyV3 = AssetDatabase.LoadAssetAtPath<GameObject>(LegacyV3Path);
        var legacyV4 = AssetDatabase.LoadAssetAtPath<GameObject>(LegacyV4Path);
        var legacyV5 = AssetDatabase.LoadAssetAtPath<GameObject>(LegacyV5Path);
        int removed = 0;
        foreach (var root in scene.GetRootGameObjects())
        {
            var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(root);
            bool isPreviousMap = System.Array.IndexOf(PreviousMapNames, root.name) >= 0
                                 || (source != null && (source == model || source == legacyFbx || source == legacyV2 || source == legacyV3 || source == legacyV4 || source == legacyV5));
            if (!isPreviousMap)
                continue;

            Undo.DestroyObjectImmediate(root);
            removed++;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
        instance.name = MapObjectName;
        instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        instance.transform.localScale = Vector3.one;
        Undo.RegisterCreatedObjectUndo(instance, "Ajout " + MapObjectName);

        var (colliders, skipped) = AddColliders(instance);
        int markers = CountMarkers(instance.transform);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[MapMarsSceneSetup] {Path.GetFileName(ModelPath)} instancie dans Map_MARS ({removed} ancienne(s) instance(s) retiree(s)) : " +
                  $"{colliders} MeshCollider, {skipped} maillages sans collision (decor exterieur), {markers} marqueurs Marker_*. Scene sauvegardee.");
        return true;
    }

    // Deplace la version deposee a la racine du projet vers Art/Models sous
    // son nom definitif. Ne remplace jamais un Map_Mars_v6.glb deja present.
    private static void ImportIncomingModel()
    {
        string incoming = IncomingModelPath();
        if (incoming == null)
            return;

        if (File.Exists(ModelPath))
        {
            Debug.LogWarning($"[MapMarsSceneSetup] {incoming} present a la racine mais {ModelPath} existe deja : " +
                             "fichier de la racine laisse tel quel, le GLB du projet est conserve.");
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(ModelPath));
        File.Move(incoming, ModelPath);
        AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        Debug.Log($"[MapMarsSceneSetup] {incoming} deplace et renomme en {ModelPath}, import glTFast lance.");
    }

    // Retire le prefixe CW_ ("Colors War", ancien nom du projet) des noms
    // du GLB : "name":"CW_X" -> "name":"X", "name":"MAT_CW_X" -> "name":"MAT_X".
    // Seul le bloc JSON est reecrit (complete par des espaces a un multiple
    // de 4 octets, comme l'exige le format GLB) ; le bloc binaire est recopie
    // tel quel. Sans effet si le GLB est deja propre.
    private static readonly Regex LegacyPrefix = new("\"name\":\"(MAT_)?CW_", RegexOptions.CultureInvariant);

    private static void StripLegacyPrefix()
    {
        if (!File.Exists(ModelPath))
            return;

        byte[] glb = File.ReadAllBytes(ModelPath);
        const uint Magic = 0x46546C67;   // "glTF"
        const uint JsonChunk = 0x4E4F534A; // "JSON"
        if (glb.Length < 20 || System.BitConverter.ToUInt32(glb, 0) != Magic || System.BitConverter.ToUInt32(glb, 16) != JsonChunk)
        {
            Debug.LogWarning($"[MapMarsSceneSetup] {ModelPath} n'est pas un GLB lisible, prefixe CW_ laisse en place.");
            return;
        }

        int jsonLength = (int)System.BitConverter.ToUInt32(glb, 12);
        string json = Encoding.UTF8.GetString(glb, 20, jsonLength);
        int count = LegacyPrefix.Matches(json).Count;
        if (count == 0)
            return;

        json = LegacyPrefix.Replace(json, "\"name\":\"$1").TrimEnd(' ');
        var jsonBytes = new List<byte>(Encoding.UTF8.GetBytes(json));
        while (jsonBytes.Count % 4 != 0)
            jsonBytes.Add((byte)' ');

        int restOffset = 20 + jsonLength; // bloc binaire (BIN) et suivants
        int restLength = glb.Length - restOffset;
        int total = 20 + jsonBytes.Count + restLength;

        using (var stream = new FileStream(ModelPath, FileMode.Create, FileAccess.Write))
        using (var writer = new BinaryWriter(stream))
        {
            writer.Write(Magic);
            writer.Write(System.BitConverter.ToUInt32(glb, 4)); // version
            writer.Write((uint)total);
            writer.Write((uint)jsonBytes.Count);
            writer.Write(JsonChunk);
            writer.Write(jsonBytes.ToArray());
            writer.Write(glb, restOffset, restLength);
        }

        AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        Debug.Log($"[MapMarsSceneSetup] Prefixe CW_ retire de {count} noms dans {Path.GetFileName(ModelPath)} (noeuds et materiaux), GLB reimporte.");
    }

    // MeshCollider (statique, non convexe) sur chaque maillage solide
    private static (int added, int skipped) AddColliders(GameObject root)
    {
        int added = 0, skipped = 0;
        foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
        {
            if (filter.sharedMesh == null)
                continue;

            if (IsInNoCollisionGroup(filter.transform, root.transform))
            {
                skipped++;
                continue;
            }

            if (!filter.TryGetComponent<Collider>(out _))
            {
                var collider = filter.gameObject.AddComponent<MeshCollider>();
                collider.sharedMesh = filter.sharedMesh;
            }
            added++;
        }
        return (added, skipped);
    }

    private static bool IsInNoCollisionGroup(Transform node, Transform root)
    {
        for (var t = node; t != null && t != root; t = t.parent)
        {
            foreach (var group in NoCollisionGroupNames)
            {
                if (string.Equals(t.name, group, System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }
        }
        return false;
    }

    private static int CountMarkers(Transform root)
    {
        int count = 0;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t.name.StartsWith("Marker_", System.StringComparison.Ordinal))
                count++;
        }
        return count;
    }
}
