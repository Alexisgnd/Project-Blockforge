using System.Collections.Generic;
using UnityEngine;

// =========================================================
// ASSEMBLAGE D'UN ROBOT A PARTIR DE SON BLUEPRINT
// Transforme un RobotBlueprint (cases + orientations) en
// hierarchie de GameObjects hors du garage : un enfant par
// bloc (visuel BlockPreviewFactory + BoxCollider de la boite
// d'empreinte), pivot de la racine au centre XZ de
// l'empreinte, base du bloc le plus bas a y = 0.
// Les blocs poses en miroir reprennent la recette du garage
// (echelle -1 + rotation conjuguee, BlockFootprint) avec le
// plan memorise dans RobotSession.MirrorAlongX.
// Utilise par la scene de test ; le garage garde son propre
// SpawnView (grille mesuree, cases occupees).
// =========================================================

public static class RobotAssembler
{
    // cellSize = taille monde d'une case ; parent = null pour une racine de scene.
    // placedCount = blocs reellement construits (les blockId inconnus sont ignores).
    public static GameObject Build(RobotBlueprint blueprint, IReadOnlyDictionary<string, BlockDefinition> defs,
                                   float cellSize, Transform parent, out int placedCount, out int totalWeightKg)
    {
        var root = new GameObject(string.IsNullOrEmpty(blueprint.name) ? "Robot" : blueprint.name);
        if (parent != null)
            root.transform.SetParent(parent, false);

        placedCount = 0;
        totalWeightKg = 0;
        int unknown = 0;
        bool alongX = RobotSession.MirrorAlongX;
        bool hasBounds = false;
        Bounds bounds = default;

        foreach (var placed in blueprint.blocks)
        {
            if (string.IsNullOrEmpty(placed.blockId) || !defs.TryGetValue(placed.blockId, out var def) || def == null)
            {
                unknown++;
                continue;
            }

            // Racine du bloc : centre de la case visee, orientation du garage
            var block = new GameObject($"Block_{placed.x}_{placed.y}_{placed.z}");
            block.transform.SetParent(root.transform, false);
            var rotation = BlockFootprint.Rotation(def, placed.rotation);
            block.transform.localPosition = new Vector3(placed.x + 0.5f, placed.y + 0.5f, placed.z + 0.5f) * cellSize;
            block.transform.localRotation = placed.mirrored ? BlockFootprint.MirrorRotation(rotation, alongX) : rotation;
            if (placed.mirrored)
                block.transform.localScale = BlockFootprint.MirrorScale(alongX);

            // Collider = boite d'empreinte dans le repere local (tourne et se
            // reflechit avec la racine), meme marge de 2 % que le garage
            var box = BlockFootprint.LocalBox(def);
            var collider = block.AddComponent<BoxCollider>();
            collider.center = box.center * cellSize;
            collider.size = box.size * (cellSize * 0.98f);

            // Marqueur pour le futur gameplay (degats, stats) ; hors du garage
            // seule la case d'ancrage est connue.
            var view = block.AddComponent<PlacedBlockView>();
            view.cell = new Vector3Int(placed.x, placed.y, placed.z);
            view.definition = def;
            view.mirrored = placed.mirrored;
            view.cells = new[] { view.cell };

            var visual = BlockPreviewFactory.CreateVisual(def, cellSize, out _);
            visual.transform.SetParent(block.transform, false);

            // Encombrement : les 8 coins de la boite d'empreinte, ramenes dans
            // le repere de la racine (l'echelle -1 du miroir est prise en compte)
            Vector3 min = box.min * cellSize, max = box.max * cellSize;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? min.x : max.x,
                                         (i & 2) == 0 ? min.y : max.y,
                                         (i & 4) == 0 ? min.z : max.z);
                var p = root.transform.InverseTransformPoint(block.transform.TransformPoint(corner));
                if (!hasBounds)
                {
                    bounds = new Bounds(p, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(p);
                }
            }

            placedCount++;
            totalWeightKg += def.weightKg;
        }

        if (unknown > 0)
            Debug.LogWarning($"[RobotAssembler] {unknown} bloc(s) du blueprint ignores (blockId inconnu du catalogue).");

        // Pivot de la racine : centre XZ de l'empreinte, base du robot a y = 0
        if (hasBounds)
        {
            var offset = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
            foreach (Transform child in root.transform)
                child.localPosition -= offset;
        }

        return root;
    }
}
