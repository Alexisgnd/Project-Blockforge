using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// =========================================================
// RESYNCHRONISATION DES BLOCS SUR LA LISTE CANONIQUE
// Menu : Blockforge > Resync Block Definitions
// Rejoue GarageInventorySetup.EnsureBlocks() (empreintes,
// ancrages, stats, FBX, icones) et relie la liste "blocks"
// au GarageInventoryController de la scene Garage, sans
// regenerer l'UI de l'inventaire.
// S'execute une seule fois automatiquement apres compilation
// (marqueur dans Library/) : migration des empreintes de la
// tache 05 (echelle et pivot).
// =========================================================

[InitializeOnLoad]
public static class BlockFootprintSetup
{
    private const string ScenePath = "Assets/_Project/Scenes/Garage.unity";
    private const string AutoRunMarker = "Library/BlockforgeBlockFootprints-v3.done";

    static BlockFootprintSetup()
    {
        EditorApplication.delayCall += AutoRunOnce;
    }

    private static void AutoRunOnce()
    {
        // En Play mode les modifications de scene seraient perdues :
        // on retentera au prochain rechargement de domaine.
        if (File.Exists(AutoRunMarker) || EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (Run())
            File.WriteAllText(AutoRunMarker, "done");
    }

    [MenuItem("Blockforge/Resync Block Definitions")]
    public static void ResyncMenu()
    {
        if (Run())
            File.WriteAllText(AutoRunMarker, "done");
    }

    // Point d'entree batch (Unity -batchmode -executeMethod BlockFootprintSetup.BatchRunAll)
    public static void BatchRunAll()
    {
        if (Run())
            File.WriteAllText(AutoRunMarker, "done");
    }

    // =====================================================
    // RAPPORT : BOUNDS BRUTS ET VISUEL AJUSTE DE CHAQUE BLOC
    // Menu : Blockforge > Report Block Bounds
    // Ecrit Library/BlockBoundsReport.txt : pour chaque bloc,
    // les bounds du prefab (repere Unity, pour verifier le
    // cote des faces d'ancrage) et le visuel produit par
    // BlockPreviewFactory avec une case de 1 m, compare a la
    // boite d'empreinte attendue.
    // =====================================================

    private const string ReportMarker = "Library/BlockforgeBlockBoundsReport-v4.done";
    private const string ReportPath = "Library/BlockBoundsReport.txt";

    // Noeuds dont la position aide a verifier le cote des faces d'ancrage
    private static readonly string[] LandmarkNames =
        { "mount", "plate", "hub", "foot", "tip", "muzzle", "emitter", "nozzle", "attach", "sole", "turret", "hinge" };

    [InitializeOnLoadMethod]
    private static void ReportOnce()
    {
        EditorApplication.delayCall += () =>
        {
            if (File.Exists(ReportMarker) || EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            // L'ordre des rappels n'est pas garanti : le rapport doit mesurer des
            // assets deja resynchronises (bounds exacts, ancrages a jour).
            if (!File.Exists(AutoRunMarker) && Run())
                File.WriteAllText(AutoRunMarker, "done");
            ReportBounds();
            File.WriteAllText(ReportMarker, "done");
        };
    }

    [MenuItem("Blockforge/Report Block Bounds")]
    public static void ReportBounds()
    {
        var sb = new System.Text.StringBuilder();
        int errors = 0, overhangs = 0, count = 0;
        const float fill = BlockPreviewFactory.DefaultFill;
        const float faceTolerance = (1f - fill) * 0.5f + 0.005f;

        foreach (var guid in AssetDatabase.FindAssets("t:BlockDefinition"))
        {
            var def = AssetDatabase.LoadAssetAtPath<BlockDefinition>(AssetDatabase.GUIDToAssetPath(guid));
            if (def == null || def.previewPrefab == null)
                continue;
            count++;

            // Bounds bruts du prefab (echelle 1, origine = pivot du FBX) et
            // position des noeuds reperes (moyeu, plaque, pied, bouche...)
            var raw = Object.Instantiate(def.previewPrefab);
            raw.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            GarageInventorySetup.TryGetExactBounds(raw, out var rawBounds);
            var landmarks = new System.Text.StringBuilder();
            int shown = 0;
            foreach (var t in raw.GetComponentsInChildren<Transform>())
            {
                string lower = t.name.ToLowerInvariant();
                bool match = false;
                foreach (var key in LandmarkNames)
                    if (lower.Contains(key)) { match = true; break; }
                if (!match || t == raw.transform)
                    continue;
                var r = t.GetComponent<Renderer>();
                Vector3 p = r != null ? r.bounds.center : t.position;
                landmarks.Append($" {t.name}{F(p)}");
                if (++shown >= 8)
                    break;
            }
            Object.DestroyImmediate(raw);

            // Visuel ajuste avec une case de 1 m : la boite attendue est en cases.
            // Mesure exacte (sommets) : renderer.bounds surestime les pieces tournees.
            var visual = BlockPreviewFactory.CreateVisual(def, 1f, out float bottom);
            GarageInventorySetup.TryGetExactBounds(visual, out var vb);
            Object.DestroyImmediate(visual);

            Vector3 boxMin = (Vector3)BlockFootprint.MinOffset(def) - Vector3.one * 0.5f;
            Vector3 boxMax = boxMin + BlockFootprint.Size(def);

            // Rotations 1..3 : le visuel tourne avec son conteneur, les cases
            // occupees (BlockFootprint.Cells) doivent toujours le contenir.
            var rotIssues = new System.Text.StringBuilder();
            for (int r = 1; r < 4 && !def.nativeScale; r++)
            {
                var rotated = BlockPreviewFactory.CreateVisual(def, 1f, out _);
                rotated.transform.rotation = BlockFootprint.Rotation(r);
                GarageInventorySetup.TryGetExactBounds(rotated, out var rb);
                Object.DestroyImmediate(rotated);

                var cells = BlockFootprint.Cells(def, Vector3Int.zero, r);
                Vector3Int cmin = cells[0], cmax = cells[0];
                foreach (var c in cells)
                {
                    cmin = Vector3Int.Min(cmin, c);
                    cmax = Vector3Int.Max(cmax, c);
                }
                Vector3 bmin = (Vector3)cmin - Vector3.one * 0.5f;
                Vector3 bmax = (Vector3)cmax + Vector3.one * 0.5f;
                bool rotInside = rb.min.x >= bmin.x - 1e-3f && rb.min.y >= bmin.y - 1e-3f && rb.min.z >= bmin.z - 1e-3f
                              && rb.max.x <= bmax.x + 1e-3f && rb.max.y <= bmax.y + 1e-3f && rb.max.z <= bmax.z + 1e-3f;
                if (!rotInside || cells.Length != BlockFootprint.Size(def).x * BlockFootprint.Size(def).y * BlockFootprint.Size(def).z)
                    rotIssues.Append($" rot{r}: visuel [{F(rb.min)}..{F(rb.max)}] hors cases [{F(bmin)}..{F(bmax)}]");
            }

            bool inside = vb.min.x >= boxMin.x - 1e-3f && vb.min.y >= boxMin.y - 1e-3f && vb.min.z >= boxMin.z - 1e-3f
                       && vb.max.x <= boxMax.x + 1e-3f && vb.max.y <= boxMax.y + 1e-3f && vb.max.z <= boxMax.z + 1e-3f;

            float faceGap = def.anchor switch
            {
                BlockAnchor.Bottom => vb.min.y - boxMin.y,
                BlockAnchor.Right => boxMax.x - vb.max.x,
                BlockAnchor.Left => vb.min.x - boxMin.x,
                BlockAnchor.Back => vb.min.z - boxMin.z,
                _ => 0f,
            };
            bool faceOk = Mathf.Abs(faceGap) <= faceTolerance;

            string status;
            if (def.nativeScale && !inside) { status = "DEBORD"; overhangs++; }
            else if (!inside || !faceOk || rotIssues.Length > 0) { status = "ERREUR"; errors++; }
            else status = "OK";

            sb.AppendLine($"{status,-6} {def.name,-36} {def.SizeLabel,-6} {def.anchor,-6} " +
                          $"brut min{F(rawBounds.min)} max{F(rawBounds.max)} | " +
                          $"boite [{F(boxMin)}..{F(boxMax)}] visuel [{F(vb.min)}..{F(vb.max)}] " +
                          $"ecart face {faceGap:0.000} bas {bottom:0.00}{rotIssues}");
            if (landmarks.Length > 0)
                sb.AppendLine($"       reperes :{landmarks}");
        }

        File.WriteAllText(ReportPath, sb.ToString());
        string summary = $"[BlockFootprintSetup] Rapport de {count} blocs : {errors} erreur(s), {overhangs} debord(s) natifs -> {ReportPath}";
        if (errors > 0) Debug.LogError(summary);
        else Debug.Log(summary);
    }

    private static string F(Vector3 v) => $"({v.x:0.00},{v.y:0.00},{v.z:0.00})";

    private static bool Run()
    {
        var blocks = GarageInventorySetup.EnsureBlocks();

        int multi = 0;
        foreach (var b in blocks)
        {
            if (b.footprint != Vector3Int.one)
                multi++;
        }

        // Scene Garage deja ouverte : mise a jour en place, sans sauvegarde
        // forcee (l'utilisateur peut avoir des modifications en cours).
        var controllerInv = Object.FindAnyObjectByType<GarageInventoryController>(FindObjectsInactive.Include);
        if (controllerInv != null)
        {
            controllerInv.blocks = blocks;
            EditorUtility.SetDirty(controllerInv);
            EditorSceneManager.MarkSceneDirty(controllerInv.gameObject.scene);
            Debug.Log($"[BlockFootprintSetup] {blocks.Length} blocs resynchronises ({multi} multi-cases) et relies " +
                      "a l'inventaire de la scene ouverte. Pense a sauvegarder la scene (Ctrl+S).");
            return true;
        }

        // Sinon : ouverture de la scene Garage, cablage puis sauvegarde.
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return false;

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        controllerInv = Object.FindAnyObjectByType<GarageInventoryController>(FindObjectsInactive.Include);
        if (controllerInv == null)
        {
            Debug.LogError("[BlockFootprintSetup] GarageInventoryController introuvable dans la scene " +
                           "Garage. Lance d'abord : Blockforge > Setup Garage Inventory.");
            return true; // les assets sont a jour, inutile de retenter en boucle
        }

        controllerInv.blocks = blocks;
        EditorUtility.SetDirty(controllerInv);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"[BlockFootprintSetup] {blocks.Length} blocs resynchronises ({multi} multi-cases) ; " +
                  "scene Garage sauvegardee.");
        return true;
    }
}
