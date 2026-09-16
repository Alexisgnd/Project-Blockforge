using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// =========================================================
// SETUP DE LA SCENE GARAGE_V2 (vaisseau Mothership)
// Menu : Blockforge > Setup Garage V2 Scene
// - Copie Garage.unity en Garage_V2.unity si elle n'existe pas
//   (HUD, joueur, pince, spray et BuildSystem conserves)
// - Remplace l'ancien vaisseau (ShipGarage.fbx x300) par le GLB
//   ShipGarage_V2.glb (glTFast) a l'identite, echelle 1.
//   Contrat du modele : baie de construction 31 x 31 x 31 m
//   centree a l'origine, lignes BuildGrid_X_NN / BuildGrid_Y_NN
//   (32 par axe = 31 cases de 1 m, le code mesure l'union des
//   lignes et compte les cases), empties BuildBounds_Min/Max,
//   BuildOrigin et PlayerSpawn ; pas de collider necessaire
// - Ajoute la ligne centrale du miroir (centerLine, le long de
//   Z) sous le vaisseau, valide la grille, recable
//   GarageBuildController.gridRoot et place le joueur sur
//   PlayerSpawn face a la grille
// Bascule du menu principal : Blockforge > MainMenu -> Garage V2
// / MainMenu -> Garage (V1) (champ garageSceneName des
// controleurs du menu).
// Ensuite : Setup Garage UI / Setup Garage Inventory s'appliquent
// a la scene garage active (donc a Garage_V2 si elle est ouverte).
// =========================================================

public static class GarageV2SceneSetup
{
    private const string OldModelPath = "Assets/_Project/Art/Models/ShipGarage.fbx";
    public const string ModelPath = "Assets/_Project/Art/Models/ShipGarage_V2.glb";
    private const string ShipName = "ShipGarage_V2";
    private const string CenterLineName = "centerLine";

    private struct GridInfo
    {
        public bool valid;
        public int linesAlongX; // lignes qui courent le long de X (separent les rangees)
        public int linesAlongZ; // lignes qui courent le long de Z (separent les colonnes)
        public Bounds bounds;
        public float thickness;
        public int CellsX => Mathf.Max(linesAlongZ - 1, 1);
        public int CellsZ => Mathf.Max(linesAlongX - 1, 1);
        public float CellSize => (bounds.size.x - thickness) / CellsX;
    }

    [MenuItem("Blockforge/Setup Garage V2 Scene")]
    public static void Setup()
    {
        if (!File.Exists(BlockforgeScenes.GarageV2))
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                return;
            if (!AssetDatabase.CopyAsset(BlockforgeScenes.Garage, BlockforgeScenes.GarageV2))
            {
                Debug.LogError($"[GarageV2SceneSetup] Impossible de copier {BlockforgeScenes.Garage} vers {BlockforgeScenes.GarageV2}.");
                return;
            }
            AssetDatabase.Refresh();
            Debug.Log("[GarageV2SceneSetup] Garage_V2.unity cree par copie de Garage.unity.");
        }

        if (!BlockforgeScenes.OpenForSetup(BlockforgeScenes.GarageV2, out var scene))
            return;

        var model = BlockforgeScenes.LoadModelAsset(ModelPath);
        if (model == null)
        {
            Debug.LogError($"[GarageV2SceneSetup] Modele introuvable ou pas encore importe : {ModelPath}. " +
                           "Contrat : GLB (glTFast) a l'echelle 1 m, baie 31 x 31 x 31 m centree a l'origine, lignes " +
                           "BuildGrid_X_NN / BuildGrid_Y_NN (32 par axe), empties BuildBounds_Min/Max et PlayerSpawn. " +
                           "L'ancien vaisseau est conserve : la scene reste utilisable, relance le menu apres l'import.");
        }
        else
        {
            RemoveOldShip(scene);
            var ship = EnsureShip(scene, model);
            var grid = MeasureGrid(ship.transform);
            LogGrid(grid);
            if (grid.valid)
                EnsureCenterLine(ship.transform, grid);
            WireBuildSystem(ship.transform);
            PlacePlayer(ship.transform, grid);
        }

        BlockforgeScenes.RegisterAllInBuildSettings();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[GarageV2SceneSetup] Garage_V2 sauvegardee. Optionnel : Setup Garage UI puis Setup Garage Inventory " +
                  "(scene active) pour regenerer le HUD ; Blockforge > MainMenu -> Garage V2 pour y entrer depuis le menu.");
    }

    // =========================================================
    // VAISSEAU
    // =========================================================

    private static void RemoveOldShip(UnityEngine.SceneManagement.Scene scene)
    {
        var oldModel = AssetDatabase.LoadAssetAtPath<GameObject>(OldModelPath);
        int removed = 0;
        foreach (var root in scene.GetRootGameObjects())
        {
            bool isOld = root.name == "ShipGarage"
                || (oldModel != null && PrefabUtility.GetCorrespondingObjectFromOriginalSource(root) == oldModel);
            if (!isOld)
                continue;
            Object.DestroyImmediate(root);
            removed++;
        }
        if (removed > 0)
            Debug.Log($"[GarageV2SceneSetup] Ancien vaisseau retire ({removed} objet(s)).");
    }

    private static GameObject EnsureShip(UnityEngine.SceneManagement.Scene scene, GameObject model)
    {
        foreach (var root in scene.GetRootGameObjects())
        {
            if (PrefabUtility.GetCorrespondingObjectFromOriginalSource(root) == model)
            {
                root.name = ShipName;
                root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                root.transform.localScale = Vector3.one;
                Debug.Log("[GarageV2SceneSetup] Le Mothership est deja dans la scene (pose remise a l'identite).");
                return root;
            }
        }

        var instance = (GameObject)PrefabUtility.InstantiatePrefab(model, scene);
        instance.name = ShipName;
        instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        instance.transform.localScale = Vector3.one;
        instance.transform.SetAsFirstSibling();
        Debug.Log($"[GarageV2SceneSetup] {Path.GetFileName(ModelPath)} instancie a l'identite, echelle 1.");
        return instance;
    }

    // =========================================================
    // GRILLE
    // =========================================================

    private static bool IsGridLine(string name)
    {
        return name.StartsWith("gx") || name.StartsWith("gz")
            || name.StartsWith("BuildGrid_", System.StringComparison.OrdinalIgnoreCase);
    }

    // Meme mesure que GarageBuildController.ComputeGridFromRenderers
    private static GridInfo MeasureGrid(Transform ship)
    {
        var info = new GridInfo();
        bool has = false;
        foreach (var r in ship.GetComponentsInChildren<Renderer>(true))
        {
            if (!IsGridLine(r.gameObject.name))
                continue;

            var b = r.bounds;
            if (!has)
            {
                info.bounds = b;
                has = true;
            }
            else
            {
                info.bounds.Encapsulate(b);
            }

            float t = Mathf.Min(b.size.x, b.size.z);
            if (info.thickness <= 0f || t < info.thickness)
                info.thickness = t;

            if (b.size.x > b.size.z * 2f)
                info.linesAlongX++;
            else if (b.size.z > b.size.x * 2f)
                info.linesAlongZ++;
        }
        info.valid = has && info.linesAlongX >= 2 && info.linesAlongZ >= 2;
        return info;
    }

    private static void LogGrid(GridInfo grid)
    {
        if (!grid.valid)
        {
            Debug.LogError("[GarageV2SceneSetup] Lignes de grille introuvables sous le vaisseau (BuildGrid_X_*/BuildGrid_Y_* " +
                           "ou gx*/gz*) : le systeme de pose ne pourra pas mesurer ses cases.");
            return;
        }

        float cell = grid.CellSize;
        Debug.Log($"[GarageV2SceneSetup] Grille : {grid.linesAlongZ} lignes le long de Z + {grid.linesAlongX} le long de X " +
                  $"= {grid.CellsX} x {grid.CellsZ} cases, cellule ~ {cell:0.###} m, epaisseur {grid.thickness:0.###} m, " +
                  $"surface y = {grid.bounds.min.y:0.###}, centre ({grid.bounds.center.x:0.##}, {grid.bounds.center.z:0.##}).");

        if (grid.CellsX != RobotBlueprint.GridWidth || grid.CellsZ != RobotBlueprint.GridDepth)
        {
            Debug.LogWarning($"[GarageV2SceneSetup] Contrat {RobotBlueprint.GridWidth} x {RobotBlueprint.GridDepth} cases attendu " +
                             $"({RobotBlueprint.GridWidth + 1} lignes par axe), mesure {grid.CellsX} x {grid.CellsZ}.");
        }
        if (cell < 0.05f || cell > 20f)
            Debug.LogWarning($"[GarageV2SceneSetup] Cellule de {cell:0.###} m : probleme d'unites probable dans l'export.");
    }

    // Ligne centrale du miroir (GarageBuildController.DetectMirrorAxis cherche
    // un renderer dont le nom contient "center" : longue sur Z = miroir sur X).
    // Ajoutee comme enfant du vaisseau (GameObject ajoute a l'instance de prefab).
    private static void EnsureCenterLine(Transform ship, GridInfo grid)
    {
        if (ship.Find(CenterLineName) != null)
            return;

        var line = GameObject.CreatePrimitive(PrimitiveType.Cube);
        line.name = CenterLineName;
        Object.DestroyImmediate(line.GetComponent<Collider>());
        line.transform.SetParent(ship, true);
        float width = grid.thickness > 0f ? grid.thickness * 1.5f : 0.06f;
        line.transform.SetPositionAndRotation(
            new Vector3(grid.bounds.center.x, grid.bounds.max.y + 0.005f, grid.bounds.center.z), Quaternion.identity);
        line.transform.localScale = new Vector3(width, 0.01f, grid.bounds.size.z + 0.6f);

        var material = FindMaterial(ship, "perimeter");
        if (material == null)
            material = FindMaterial(ship, "BuildGrid_");
        if (material != null)
            line.GetComponent<Renderer>().sharedMaterial = material;

        Debug.Log("[GarageV2SceneSetup] Ligne centrale 'centerLine' ajoutee le long de Z (miroir sur X).");
    }

    private static Material FindMaterial(Transform root, string namePart)
    {
        foreach (var r in root.GetComponentsInChildren<Renderer>(true))
        {
            if (r.gameObject.name.IndexOf(namePart, System.StringComparison.OrdinalIgnoreCase) >= 0 && r.sharedMaterial != null)
                return r.sharedMaterial;
        }
        return null;
    }

    private static void WireBuildSystem(Transform ship)
    {
        var build = Object.FindAnyObjectByType<GarageBuildController>(FindObjectsInactive.Include);
        if (build == null)
        {
            Debug.LogWarning("[GarageV2SceneSetup] Aucun GarageBuildController (BuildSystem) dans la scene : lance " +
                             "Setup Garage UI puis Setup Garage Inventory avec Garage_V2 ouverte.");
            return;
        }
        build.gridRoot = ship;
        EditorUtility.SetDirty(build);
        Debug.Log("[GarageV2SceneSetup] BuildSystem.gridRoot recable sur le vaisseau (lignes BuildGrid_* + centerLine).");
    }

    // =========================================================
    // JOUEUR
    // =========================================================

    // Le rig du joueur regarde vers -Z local (camera tournee de 180 degres) :
    // son +Z doit pointer a l'oppose de la grille.
    private static void PlacePlayer(Transform ship, GridInfo grid)
    {
        var player = Object.FindAnyObjectByType<PlayerGarageController>(FindObjectsInactive.Include);
        if (player == null)
        {
            Debug.LogWarning("[GarageV2SceneSetup] Player introuvable : position de depart non ajustee.");
            return;
        }

        Vector3 center = grid.valid ? grid.bounds.center : Vector3.zero;
        var spawn = FindSpawnNode(ship);
        Vector3 position;
        string source;
        if (spawn != null)
        {
            position = spawn.position;
            source = $"noeud '{spawn.name}'";
        }
        else
        {
            float depth = grid.valid ? grid.bounds.size.z : 31f;
            position = new Vector3(center.x, (grid.valid ? grid.bounds.min.y : 0f) + 6f, center.z + depth * 0.5f + 5f);
            source = "repli devant la grille";
        }

        Vector3 away = position - center;
        away.y = 0f;
        Quaternion rotation = away.sqrMagnitude > 0.01f ? Quaternion.LookRotation(away.normalized) : Quaternion.identity;
        player.transform.SetPositionAndRotation(position, rotation);
        EditorUtility.SetDirty(player.transform);
        Debug.Log($"[GarageV2SceneSetup] Player place en {position} ({source}), face a la grille.");
    }

    private static Transform FindSpawnNode(Transform root)
    {
        Transform fallback = null;
        foreach (var t in root.GetComponentsInChildren<Transform>(true))
        {
            if (t == root)
                continue;
            if (string.Equals(t.name, "PlayerSpawn", System.StringComparison.OrdinalIgnoreCase))
                return t;
            if (fallback == null
                && t.name.StartsWith("spawn", System.StringComparison.OrdinalIgnoreCase)
                && t.GetComponent<Renderer>() == null)
            {
                fallback = t;
            }
        }
        return fallback;
    }

    // =========================================================
    // MENU PRINCIPAL -> GARAGE V1 / V2
    // =========================================================

    [MenuItem("Blockforge/MainMenu -> Garage V2")]
    public static void MainMenuToGarageV2()
    {
        SetMainMenuGarage("Garage_V2");
    }

    [MenuItem("Blockforge/MainMenu -> Garage (V1)")]
    public static void MainMenuToGarageV1()
    {
        SetMainMenuGarage("Garage");
    }

    private static void SetMainMenuGarage(string sceneName)
    {
        if (!BlockforgeScenes.OpenForSetup(BlockforgeScenes.MainMenu, out var scene))
            return;

        int count = 0;
        foreach (var menu in Object.FindObjectsByType<BuildMenuController>(FindObjectsInactive.Include))
        {
            menu.garageSceneName = sceneName;
            EditorUtility.SetDirty(menu);
            count++;
        }
        foreach (var menu in Object.FindObjectsByType<MainMenuController>(FindObjectsInactive.Include))
        {
            menu.garageSceneName = sceneName;
            EditorUtility.SetDirty(menu);
            count++;
        }

        BlockforgeScenes.RegisterAllInBuildSettings();
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[GarageV2SceneSetup] MainMenu -> scene garage '{sceneName}' ({count} controleur(s) mis a jour).");
    }
}
