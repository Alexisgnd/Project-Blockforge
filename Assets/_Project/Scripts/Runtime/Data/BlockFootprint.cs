using System.Collections.Generic;
using UnityEngine;

// =========================================================
// GEOMETRIE D'EMPREINTE D'UN BLOC (en cases)
// Partagee par le visuel (BlockPreviewFactory) et la pose
// (GarageBuildController). La boite d'un bloc croit a
// l'oppose de sa face d'ancrage depuis la case visee ; sur
// les autres axes elle est centree si le nombre de cases est
// impair, sinon elle deborde vers le cote positif.
// Repere : X = largeur, Y = hauteur, Z = profondeur (avant = +Z).
// =========================================================

public static class BlockFootprint
{
    // Empreinte garantie >= 1 case sur chaque axe
    public static Vector3Int Size(BlockDefinition def)
    {
        return Vector3Int.Max(def.footprint, Vector3Int.one);
    }

    // Decalage (en cases) de la case "min" de la boite par rapport a la case
    // visee, avant rotation. Exemples : aileron 1x2x1 pose (Bottom) -> (0,0,0),
    // roue 2x3x3 fixee par +X (Right) -> (-1,-1,-1), plaque 2x3x1 fixee a
    // l'arriere (Back) -> (0,-1,0).
    public static Vector3Int MinOffset(BlockDefinition def)
    {
        var size = Size(def);
        var min = new Vector3Int(Centered(size.x), Centered(size.y), Centered(size.z));
        switch (def.anchor)
        {
            case BlockAnchor.Bottom: min.y = 0; break;          // croit vers le haut
            case BlockAnchor.Right:  min.x = 1 - size.x; break; // moyeu sur +X : croit vers -X
            case BlockAnchor.Left:   min.x = 0; break;          // plaque sur -X : croit vers +X
            case BlockAnchor.Back:   min.z = 0; break;          // fixation a l'arriere : croit vers l'avant
            case BlockAnchor.Center: break;
        }
        return min;
    }

    // n cases centrees sur la case visee : 0 (1), 0 (2), -1 (3), -1 (4), -2 (5)...
    private static int Centered(int n) => -((n - 1) / 2);

    // Rotation d'un bloc pose : yaw par pas de 90 degres autour de la case
    // visee, le meme quaternion que celui applique au conteneur visuel.
    public static Quaternion Rotation(int rotation) => Quaternion.Euler(0f, rotation * 90f, 0f);

    // Decalage de case tourne par le yaw (une rotation de 90 degres autour
    // d'un centre de case renvoie toujours sur des centres de cases).
    public static Vector3Int Rotate(Vector3Int offset, int rotation)
    {
        return Vector3Int.RoundToInt(Rotation(rotation) * (Vector3)offset);
    }

    // Cases occupees par un bloc dont la case visee est anchorCell, tourne de
    // rotation. La case visee fait toujours partie de la boite.
    public static void GetCells(BlockDefinition def, Vector3Int anchorCell, int rotation, List<Vector3Int> result)
    {
        result.Clear();
        var size = Size(def);
        var min = MinOffset(def);
        var rot = Rotation(rotation);
        for (int x = 0; x < size.x; x++)
            for (int y = 0; y < size.y; y++)
                for (int z = 0; z < size.z; z++)
                {
                    var offset = new Vector3Int(min.x + x, min.y + y, min.z + z);
                    result.Add(anchorCell + Vector3Int.RoundToInt(rot * (Vector3)offset));
                }
    }

    public static Vector3Int[] Cells(BlockDefinition def, Vector3Int anchorCell, int rotation)
    {
        var list = new List<Vector3Int>();
        GetCells(def, anchorCell, rotation, list);
        return list.ToArray();
    }
}
