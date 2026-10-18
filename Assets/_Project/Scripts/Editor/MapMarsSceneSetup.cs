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
// - Map v2 (18/10/2026) : GLB Map_Mars_v2.glb (export Three.js
//   de Map_Mars_TDM) importe par glTFast, remplace l'ancien
//   Map_Mars.fbx (conserve dans le projet, plus instancie)
// - Si la nouvelle version est deposee a la racine du projet
//   (Map_Mars_TDM.glb), elle est deplacee et renommee en
//   Art/Models/Map_Mars_v2.glb puis importee
// - Retire du GLB le prefixe CW_ (ancien nom du projet,
//   "Colors War") sur les noeuds et materiaux :
//   CW_Rock_001 -> Rock_001, MAT_CW_Mars_Sand -> MAT_Mars_Sand
//   (reecriture du bloc JSON du GLB, le binaire est intact)
// - Retire l'instance precedente de la map (Map_Mars FBX ou
//   Map_Mars_v2), instancie le GLB a l'identite (Y-up, metres :
//   pas de rotation -90 comme pour le FBX)
// - MeshCollider sur chaque maillage solide (terrain, rochers,
//   ponts, plateformes de spawn) ; sans collision : le decor
//   exterieur Map_Mars_Surround (4 x 5,2 km, hors zone de
//   jeu) et les marqueurs Markers (noeuds vides)
// - Materiaux : ceux embarques dans le GLB (MAT_Mars_Sand...),
//   convertis en HDRP par glTFast
// S'execute aussi une seule fois tout seul apres compilation
// si la scene Map_MARS est ouverte ou si Map_Mars_TDM.glb attend
// a la racine du projet (marqueur dans Library/).
// =========================================================

[InitializeOnLoad]
public static class MapMarsSceneSetup
{
    private const string ScenePath = BlockforgeScenes.MapMars;
    public const string ModelPath = "Assets/_Project/Art/Models/Map_Mars_v2.glb";
    // Ancienne version (FBX Z-up), conservee pour reference
    public const string LegacyFbxPath = "Assets/_Project/Art/Models/Map_Mars.fbx";
    // Nouvelle version telle que deposee a la racine du projet
    private const string IncomingModelPath = "Map_Mars_TDM.glb";
    public const string MapObjectName = "Map_Mars_v2";
    // v4 : GLB Map_Mars_v2 sans prefixe CW_
    private const string AutoRunMarker = "Library/BlockforgeMapMarsSetup-v4.done";

    // Instances des versions precedentes de la map a retirer
    private static readonly string[] PreviousMapNames = { "Map_Mars", MapObjectName, "Map_Mars_TDM", "CW_Map_Mars_TDM" };

    // Sous-arbres sans collision
    private static readonly string[] NoCollisionGroupNames =
    {
        "Map_Mars_Surround", // decor exterieur, hors d'atteinte
        "Markers",           // marqueurs de spawn / centre (noeuds vides)
    };

    static MapMarsSceneSetup()
    {
        EditorApplication.delayCall += AutoRunOnce;
    }

    // Lancement automatique one-shot si Map_MARS est deja la scene
    // active, ou si une nouvelle version vient d'etre deposee a la
    // racine du projet (Map_Mars_TDM.glb) : dans ce cas on ouvre
    // Map_MARS le temps du setup puis on revient a la scene d'origine.
    private static void AutoRunOnce()
    {
        if (File.Exists(AutoRunMarker) || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        string previous = SceneManager.GetActiveScene().path;
        bool sceneActive = previous == ScenePath;
        if (!sceneActive && !File.Exists(IncomingModelPath))
            return;

        if (!BlockforgeScenes.OpenForSetup(ScenePath, out _))
            return;

        if (Setup())
            File.WriteAllText(AutoRunMarker, "done");

        if (!sceneActive && !string.IsNullOrEmpty(previous) && File.Exists(previous))
            EditorSceneManager.OpenScene(previous, OpenSceneMode.Single);
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
        int removed = 0;
        foreach (var root in scene.GetRootGameObjects())
        {
            var source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(root);
            bool isPreviousMap = System.Array.IndexOf(PreviousMapNames, root.name) >= 0
                                 || (source != null && (source == model || source == legacyFbx));
            if (!isPreviousMap)
                continue;

            Undo.DestroyObjectImmediate(root);
            removed++;
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
        instance.name = MapObjectName;
        instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        instance.transform.localScale = Vector3.one;
        Undo.RegisterCreatedObjectUndo(instance, "Ajout Map_Mars_v2");

        var (colliders, skipped) = AddColliders(instance);
        int markers = CountMarkers(instance.transform);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[MapMarsSceneSetup] {Path.GetFileName(ModelPath)} instancie dans Map_MARS ({removed} ancienne(s) instance(s) retiree(s)) : " +
                  $"{colliders} MeshCollider, {skipped} maillages sans collision (decor exterieur), {markers} marqueurs Marker_*. Scene sauvegardee.");
        return true;
    }

    // Deplace la version deposee a la racine du projet vers Art/Models sous
    // son nom definitif. Ne remplace jamais un Map_Mars_v2.glb deja present.
    private static void ImportIncomingModel()
    {
        if (!File.Exists(IncomingModelPath))
            return;

        if (File.Exists(ModelPath))
        {
            Debug.LogWarning($"[MapMarsSceneSetup] {IncomingModelPath} present a la racine mais {ModelPath} existe deja : " +
                             "fichier de la racine laisse tel quel, le GLB du projet est conserve.");
            return;
        }

        Directory.CreateDirectory(Path.GetDirectoryName(ModelPath));
        File.Move(IncomingModelPath, ModelPath);
        AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
        Debug.Log($"[MapMarsSceneSetup] {IncomingModelPath} deplace et renomme en {ModelPath}, import glTFast lance.");
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
