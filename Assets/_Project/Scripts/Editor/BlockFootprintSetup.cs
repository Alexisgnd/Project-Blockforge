using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// =========================================================
// RESYNCHRONISATION DES BLOCS SUR LA LISTE CANONIQUE
// Menu : Blockforge > Resync Block Definitions
// Rejoue GarageInventorySetup.EnsureBlocks() (empreintes,
// ancrages, stats, FBX, icones, bounds exacts), relie la
// liste "blocks" au GarageInventoryController si la scene
// Garage est ouverte (sans ouvrir ni sauvegarder de scene :
// le jeu d'assets ne change pas), puis ecrit le rapport de
// verification (menu Blockforge > Report Block Bounds).
// Tourne seul une fois par version de la liste canonique
// (marqueur Library/ nomme par le hash de
// GarageInventorySetup.cs) et a chaque reimport d'un FBX ou
// d'un prefab de bloc (BlockModelPostprocessor ci-dessous),
// pour que BlockDefinition.modelBounds ne soit jamais perime.
// =========================================================

[InitializeOnLoad]
public static class BlockFootprintSetup
{
    private const string CanonicalListPath = "Assets/_Project/Scripts/Editor/GarageInventorySetup.cs";
    public const string ModelsDir = "Assets/_Project/Art/Models/Blocks/";
    public const string PrefabsDir = "Assets/_Project/Art/Prefabs/Blocks/";
    private const string ReportPath = "Library/BlockBoundsReport.txt";

    // Marqueur derive du contenu de la liste canonique : toute modification de
    // GarageInventorySetup.cs relance le resync une fois, sans numero de
    // version a incrementer a la main.
    private static string AutoRunMarker
    {
        get
        {
            string text = File.Exists(CanonicalListPath) ? File.ReadAllText(CanonicalListPath) : "";
            using var md5 = MD5.Create();
            var hash = md5.ComputeHash(Encoding.UTF8.GetBytes(text));
            var sb = new StringBuilder();
            for (int i = 0; i < 6; i++)
                sb.Append(hash[i].ToString("x2"));
            return $"Library/BlockforgeBlocks-{sb}.done";
        }
    }

    static BlockFootprintSetup()
    {
        EditorApplication.delayCall += AutoRunOnce;
    }

    private static void AutoRunOnce()
    {
        // En Play mode les assets ne doivent pas bouger : on retentera au
        // prochain rechargement de domaine.
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;
        string marker = AutoRunMarker;
        if (File.Exists(marker))
            return;
        Run();
        File.WriteAllText(marker, "done");
    }

    [MenuItem("Blockforge/Resync Block Definitions")]
    public static void ResyncMenu()
    {
        Run();
        File.WriteAllText(AutoRunMarker, "done");
    }

    // Point d'entree batch (Unity -batchmode -executeMethod BlockFootprintSetup.BatchRunAll)
    public static void BatchRunAll() => ResyncMenu();

    private static void Run()
    {
        var blocks = GarageInventorySetup.EnsureBlocks();

        int multi = 0;
        foreach (var b in blocks)
        {
            if (b.footprint != Vector3Int.one)
                multi++;
        }

        // Scene Garage ouverte : la liste n'est mise a jour (et la scene marquee
        // modifiee) que si le jeu d'assets a change.
        var controllerInv = UnityEngine.Object.FindAnyObjectByType<GarageInventoryController>(FindObjectsInactive.Include);
        string sceneNote = "";
        if (controllerInv != null && !SameBlocks(controllerInv.blocks, blocks))
        {
            controllerInv.blocks = blocks;
            EditorUtility.SetDirty(controllerInv);
            EditorSceneManager.MarkSceneDirty(controllerInv.gameObject.scene);
            sceneNote = " ; liste de l'inventaire de la scene ouverte mise a jour (Ctrl+S)";
        }

        Debug.Log($"[BlockFootprintSetup] {blocks.Length} blocs resynchronises ({multi} multi-cases){sceneNote}.");
        ReportBounds();
    }

    private static bool SameBlocks(BlockDefinition[] a, BlockDefinition[] b)
    {
        if (a == null || b == null || a.Length != b.Length)
            return false;
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i])
                return false;
        }
        return true;
    }

    // =====================================================
    // RAPPORT : BOUNDS BRUTS ET VISUEL AJUSTE DE CHAQUE BLOC
    // Menu : Blockforge > Report Block Bounds
    // Ecrit Library/BlockBoundsReport.txt : pour chaque bloc,
    // les bounds du prefab (repere Unity) et la position des
    // noeuds reperes (moyeu, plaque, pied, bouche...) pour
    // verifier le cote des faces d'ancrage, puis le visuel
    // produit par BlockPreviewFactory avec une case de 1 m
    // pour les 4 rotations, compare aux cases occupees
    // (BlockFootprint.GetCells). Tout se passe dans une scene
    // de preview : la scene ouverte n'est pas touchee.
    // =====================================================

    // Orientations verifiees : face naturelle x 4 spins, puis chaque face
    // d'accroche (dessus, dessous, +X, -X, avant, arriere) sans spin
    private static readonly int[] CheckedRotations = { 0, 1, 2, 3, 4, 8, 12, 16, 20, 24 };

    // Noeuds dont la position aide a verifier le cote des faces d'ancrage
    private static readonly string[] LandmarkNames =
        { "mount", "plate", "hub", "foot", "tip", "muzzle", "emitter", "nozzle", "attach", "sole", "turret", "hinge" };

    [MenuItem("Blockforge/Report Block Bounds")]
    public static void ReportBounds()
    {
        var sb = new StringBuilder();
        int errors = 0, overhangs = 0, count = 0;
        const float fill = BlockPreviewFactory.DefaultFill;
        const float faceTolerance = (1f - fill) * 0.5f + 0.005f;
        var cells = new List<Vector3Int>();
        var half = Vector3.one * 0.5f;

        var preview = EditorSceneManager.NewPreviewScene();
        try
        {
            foreach (var guid in AssetDatabase.FindAssets("t:BlockDefinition"))
            {
                var def = AssetDatabase.LoadAssetAtPath<BlockDefinition>(AssetDatabase.GUIDToAssetPath(guid));
                if (def == null || def.previewPrefab == null)
                    continue;
                count++;

                // Bounds bruts du prefab (echelle 1, origine = pivot du FBX) et reperes
                var raw = (GameObject)PrefabUtility.InstantiatePrefab(def.previewPrefab, preview);
                raw.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                GarageInventorySetup.TryGetExactBounds(raw, out var rawBounds);
                string landmarks = Landmarks(raw);
                UnityEngine.Object.DestroyImmediate(raw);

                // Visuel ajuste pour les 4 spins naturels et les 6 faces d'accroche :
                // doit rester dans ses cases ; a l'orientation 0 la face d'ancrage
                // doit affleurer la boite (sauf echelle native, placee sur le pivot).
                bool inside = true;
                float faceGap = 0f, bottom = 0f;
                Bounds vb0 = default;
                var issues = new StringBuilder();
                foreach (int r in CheckedRotations)
                {
                    var visual = BlockPreviewFactory.CreateVisual(def, 1f, out float b);
                    SceneManager.MoveGameObjectToScene(visual, preview);
                    visual.transform.rotation = BlockFootprint.Rotation(def, r);
                    GarageInventorySetup.TryGetExactBounds(visual, out var vb);
                    UnityEngine.Object.DestroyImmediate(visual);

                    BlockFootprint.GetCells(def, Vector3Int.zero, r, cells);
                    Vector3Int cmin = cells[0], cmax = cells[0];
                    foreach (var c in cells)
                    {
                        cmin = Vector3Int.Min(cmin, c);
                        cmax = Vector3Int.Max(cmax, c);
                    }
                    var box = new Bounds();
                    box.SetMinMax((Vector3)cmin - half, (Vector3)cmax + half);

                    if (r == 0)
                    {
                        bottom = b;
                        vb0 = vb;
                        faceGap = FaceGap(def, vb, box);
                    }
                    if (!Contains(box, vb, 1e-3f))
                    {
                        inside = false;
                        issues.Append($" rot{r}: visuel [{F(vb.min)}..{F(vb.max)}] hors cases [{F(box.min)}..{F(box.max)}]");
                    }
                }

                bool faceOk = def.nativeScale || Mathf.Abs(faceGap) <= faceTolerance;
                string status;
                if (def.nativeScale && !inside) { status = "DEBORD"; overhangs++; }
                else if (!inside || !faceOk) { status = "ERREUR"; errors++; }
                else status = "OK";

                sb.AppendLine($"{status,-6} {def.name,-36} {def.SizeLabel,-6} {def.anchor,-6} " +
                              $"brut min{F(rawBounds.min)} max{F(rawBounds.max)} | " +
                              $"visuel [{F(vb0.min)}..{F(vb0.max)}] ecart face {faceGap:0.000} bas {bottom:0.00}{issues}");
                if (landmarks.Length > 0)
                    sb.AppendLine($"       reperes :{landmarks}");
            }
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(preview);
        }

        File.WriteAllText(ReportPath, sb.ToString());
        string summary = $"[BlockFootprintSetup] Rapport de {count} blocs : {errors} erreur(s), {overhangs} debord(s) natifs -> {ReportPath}";
        if (errors > 0) Debug.LogError(summary);
        else Debug.Log(summary);
    }

    private static bool Contains(Bounds outer, Bounds inner, float eps)
    {
        return inner.min.x >= outer.min.x - eps && inner.min.y >= outer.min.y - eps && inner.min.z >= outer.min.z - eps
            && inner.max.x <= outer.max.x + eps && inner.max.y <= outer.max.y + eps && inner.max.z <= outer.max.z + eps;
    }

    // Distance entre la face d'ancrage du visuel et la face correspondante de la boite
    private static float FaceGap(BlockDefinition def, Bounds visual, Bounds box)
    {
        return def.anchor switch
        {
            BlockAnchor.Bottom => visual.min.y - box.min.y,
            BlockAnchor.Right => box.max.x - visual.max.x,
            BlockAnchor.Left => visual.min.x - box.min.x,
            BlockAnchor.Back => visual.min.z - box.min.z,
            _ => 0f,
        };
    }

    // Positions (centre des renderers, sinon du transform) des noeuds reperes, 8 max
    private static string Landmarks(GameObject raw)
    {
        var sb = new StringBuilder();
        int shown = 0;
        foreach (var t in raw.GetComponentsInChildren<Transform>())
        {
            if (t == raw.transform)
                continue;
            bool match = false;
            foreach (var key in LandmarkNames)
            {
                if (t.name.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0) { match = true; break; }
            }
            if (!match)
                continue;
            var r = t.GetComponent<Renderer>();
            Vector3 p = r != null ? r.bounds.center : t.position;
            sb.Append($" {t.name}{F(p)}");
            if (++shown >= 8)
                break;
        }
        return sb.ToString();
    }

    private static string F(Vector3 v) => $"({v.x:0.00},{v.y:0.00},{v.z:0.00})";
}

// =========================================================
// Un FBX ou un prefab de bloc reimporte (re-export Blender,
// changement d'import par un MaterialSetup) invalide les
// bounds exacts stockes dans les BlockDefinition : resync
// automatique une fois le lot d'import termine.
// =========================================================
public class BlockModelPostprocessor : AssetPostprocessor
{
    private static bool scheduled;

    private static void OnPostprocessAllAssets(string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if (scheduled)
            return;
        if (!Touches(imported) && !Touches(moved) && !Touches(deleted))
            return;

        scheduled = true;
        EditorApplication.delayCall += () =>
        {
            scheduled = false;
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                return;
            BlockFootprintSetup.ResyncMenu();
        };
    }

    private static bool Touches(string[] paths)
    {
        foreach (var p in paths)
        {
            if ((p.StartsWith(BlockFootprintSetup.ModelsDir) && p.EndsWith(".fbx"))
                || (p.StartsWith(BlockFootprintSetup.PrefabsDir) && p.EndsWith(".prefab")))
                return true;
        }
        return false;
    }
}
