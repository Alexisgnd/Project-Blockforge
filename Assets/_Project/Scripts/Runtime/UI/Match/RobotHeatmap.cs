using System.Collections.Generic;
using UnityEngine;

// =========================================================
// HEATMAP DES BLOCS D'UN ROBOT (HUD de match)
// Texture vue de dessus (avant en haut) : une case par case de
// grille occupee (PlacedBlockView.cells, projetees en XZ). Quand
// plusieurs blocs se superposent (etages, roue sous un cube), la
// case prend l'etat du bloc vivant le plus abime : vert > 66 %,
// jaune > 33 %, rouge > 0 ; rouge sombre barre d'une croix rouge
// vif seulement quand tous ses blocs sont detruits.
// Build() mesure le robot ; Redraw() repeint quand BlockChanged
// a mis Dirty. Utilisee pour le joueur (etat du robot) et pour
// le robot ennemi (panneau du haut).
// =========================================================

public class RobotHeatmap
{
    public Texture2D Texture { get; private set; }
    public float Aspect { get; private set; } = 1f;
    public bool Dirty { get; set; } = true;

    private readonly Dictionary<Vector2Int, List<RobotHealth.Block>> cells = new();
    private Color32[] pixels;
    private int cellPx = 14, minX, minZ, cols, rows;

    public bool Build(RobotLocomotion loco, RobotHealth health, int cellPixels = 14)
    {
        cellPx = cellPixels;
        cells.Clear();
        if (loco == null)
            return false;
        int maxX = int.MinValue, maxZ = int.MinValue;
        minX = int.MaxValue;
        minZ = int.MaxValue;
        foreach (var view in loco.GetComponentsInChildren<PlacedBlockView>())
        {
            var block = health != null ? health.Get(view) : null;
            if (block == null)
                block = new RobotHealth.Block { view = view, max = 1f, hp = 1f };
            var occupied = view.cells != null && view.cells.Length > 0 ? view.cells : new[] { view.cell };
            foreach (var c in occupied)
            {
                var key = new Vector2Int(c.x, c.z);
                if (!cells.TryGetValue(key, out var list))
                    cells[key] = list = new List<RobotHealth.Block>();
                if (!list.Contains(block))
                    list.Add(block);
                minX = Mathf.Min(minX, c.x); maxX = Mathf.Max(maxX, c.x);
                minZ = Mathf.Min(minZ, c.z); maxZ = Mathf.Max(maxZ, c.z);
            }
        }
        if (cells.Count == 0)
            return false;
        cols = maxX - minX + 1;
        rows = maxZ - minZ + 1;
        if (Texture != null)
            Object.Destroy(Texture);
        Texture = new Texture2D(cols * cellPx, rows * cellPx, TextureFormat.RGBA32, false)
        {
            name = "RobotHeatmap",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };
        pixels = new Color32[Texture.width * Texture.height];
        Aspect = (float)cols / rows;
        Dirty = true;
        Redraw();
        return true;
    }

    public void Redraw()
    {
        if (Texture == null)
            return;
        Dirty = false;
        System.Array.Clear(pixels, 0, pixels.Length);
        Color32 green = BlockforgeTheme.Hud.Success, yellow = BlockforgeTheme.Hud.Warning, red = BlockforgeTheme.Hud.DangerInk;
        Color32 dead = new Color32(70, 14, 14, 255), cross = new Color32(255, 70, 60, 255);
        int cell = cellPx;
        foreach (var (key, list) in cells)
        {
            // Etat de la case : bloc vivant le plus abime ; detruite si tous le sont
            float worst = 2f;
            foreach (var b in list)
            {
                if (!b.Destroyed)
                    worst = Mathf.Min(worst, b.Ratio);
            }
            bool destroyed = worst > 1f;
            Color32 tone = destroyed ? dead : worst > 0.66f ? green : worst > 0.33f ? yellow : red;
            int ox = (key.x - minX) * cell, oy = (key.y - minZ) * cell;
            for (int y = 1; y < cell; y++)
            {
                for (int x = 1; x < cell; x++)
                {
                    bool onCross = destroyed && (Mathf.Abs(x - y) <= 1 || Mathf.Abs(x - (cell - y)) <= 1);
                    pixels[(oy + y) * Texture.width + ox + x] = onCross ? cross : tone;
                }
            }
        }
        Texture.SetPixels32(pixels);
        Texture.Apply();
    }

    public void Release()
    {
        if (Texture != null)
            Object.Destroy(Texture);
        Texture = null;
    }
}
