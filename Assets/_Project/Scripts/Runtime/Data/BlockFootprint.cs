using System.Collections.Generic;
using UnityEngine;

// =========================================================
// GEOMETRIE D'EMPREINTE ET D'ORIENTATION D'UN BLOC (en cases)
// Partagee par le visuel (BlockPreviewFactory), la pose
// (GarageBuildController) et le rapport de verification
// (BlockFootprintSetup).
// - Boite : croit a l'oppose de la face d'ancrage depuis la
//   case visee ; sur les autres axes, centree si le nombre de
//   cases est impair, sinon deborde vers le cote positif.
// - Orientation (PlacedBlock.rotation = face * 4 + spin) :
//   face 0 = orientation naturelle du modele (chassis, anciens
//   blueprints), faces 1..6 = face d'ancrage plaquee contre la
//   face visee du support (dessus, dessous, +X, -X, avant,
//   arriere) ; spin = quart de tour autour de cette face.
// Repere Unity : X = largeur, Y = hauteur, Z = profondeur
// (avant = +Z). L'import FBX inverse X par rapport a Blender.
// =========================================================

public static class BlockFootprint
{
    // Face visee du support (normale du support vers la case du bloc).
    // Index 0 = pas d'alignement (orientation naturelle, spin autour de Y).
    public const int NaturalFace = 0;
    public static readonly Vector3Int[] FaceNormals =
    {
        Vector3Int.up,                                       // 0 : naturel (axe de spin = Y)
        Vector3Int.up, Vector3Int.down,                      // 1, 2 : pose dessus / suspendu dessous
        Vector3Int.right, Vector3Int.left,                   // 3, 4 : flancs
        Vector3Int.forward, Vector3Int.back,                 // 5, 6 : avant / arriere
    };

    // Empreinte garantie >= 1 case sur chaque axe
    public static Vector3Int Size(BlockDefinition def)
    {
        return Vector3Int.Max(def.footprint, Vector3Int.one);
    }

    // Decalage (en cases) de la case "min" de la boite par rapport a la case
    // visee, avant rotation. Exemples : aileron 1x2x1 pose (Bottom) -> (0,0,0),
    // roue 2x3x3 fixee par le moyeu en -X (Left) -> (0,-1,-1), plaque 2x3x1
    // fixee a l'arriere (Back) -> (0,-1,0).
    public static Vector3Int MinOffset(BlockDefinition def)
    {
        var size = Size(def);
        var min = new Vector3Int(Centered(size.x), Centered(size.y), Centered(size.z));
        switch (def.anchor)
        {
            case BlockAnchor.Bottom: min.y = 0; break;          // croit vers le haut
            case BlockAnchor.Right:  min.x = 1 - size.x; break; // fixation sur +X : croit vers -X
            case BlockAnchor.Left:   min.x = 0; break;          // fixation sur -X : croit vers +X
            case BlockAnchor.Back:   min.z = 0; break;          // fixation a l'arriere : croit vers l'avant
            case BlockAnchor.Center: break;
        }
        return min;
    }

    // Boite d'empreinte en unites de case, dans le repere local du bloc
    // (origine = centre de la case visee, avant rotation) : la case (i,j,k)
    // couvre [i-0.5, i+0.5] sur chaque axe.
    public static Bounds LocalBox(BlockDefinition def)
    {
        var size = Size(def);
        var min = MinOffset(def);
        var center = new Vector3(min.x + (size.x - 1) * 0.5f, min.y + (size.y - 1) * 0.5f, min.z + (size.z - 1) * 0.5f);
        return new Bounds(center, size);
    }

    // n cases centrees sur la case visee : 0 (1), 0 (2), -1 (3), -1 (4), -2 (5)...
    private static int Centered(int n) => -((n - 1) / 2);

    // Normale sortante (locale) de la face d'ancrage du modele
    public static Vector3 AnchorNormal(BlockAnchor anchor)
    {
        return anchor switch
        {
            BlockAnchor.Right => Vector3.right,
            BlockAnchor.Left => Vector3.left,
            BlockAnchor.Back => Vector3.back,
            _ => Vector3.down, // Bottom, Center
        };
    }

    // =====================================================
    // ORIENTATION : rotation = face * 4 + spin
    // =====================================================

    public static int Compose(int face, int spin) => Mathf.Clamp(face, 0, FaceNormals.Length - 1) * 4 + Wrap4(spin);
    public static int FaceOf(int rotation) => Mathf.Clamp(rotation / 4, 0, FaceNormals.Length - 1);
    public static int SpinOf(int rotation) => Wrap4(rotation);
    private static int Wrap4(int v) => ((v % 4) + 4) % 4;

    // Index de face (1..6) pour une normale de face visee ; 0 si inconnue
    public static int FaceIndex(Vector3Int normal)
    {
        for (int i = 1; i < FaceNormals.Length; i++)
        {
            if (FaceNormals[i] == normal)
                return i;
        }
        return NaturalFace;
    }

    // Quaternion applique au bloc (conteneur visuel, racine posee). Face 0 :
    // orientation naturelle tournee de spin quarts de tour autour de Y (le
    // yaw historique). Faces 1..6 : la face d'ancrage du modele est plaquee
    // contre la face visee (sa normale locale envoyee sur -n), puis spin
    // quarts de tour autour de n. Toutes ces rotations sont des multiples
    // de 90 degres : les cases tournees restent entieres.
    public static Quaternion Rotation(BlockDefinition def, int rotation)
    {
        int face = FaceOf(rotation);
        int spin = SpinOf(rotation);
        Vector3 n = FaceNormals[face];
        if (face == NaturalFace)
            return Quaternion.AngleAxis(spin * 90f, Vector3.up);

        Vector3 a = AnchorNormal(def.anchor);
        Vector3 target = -n;
        float dot = Vector3.Dot(a, target);
        Quaternion align;
        if (dot > 0.99f)
            align = Quaternion.identity;
        else if (dot < -0.99f)
            align = Quaternion.AngleAxis(180f, Mathf.Abs(a.y) > 0.5f ? Vector3.forward : Vector3.up);
        else
            align = Quaternion.FromToRotation(a, target); // 90 degres autour de a x target, axe aligne
        return Quaternion.AngleAxis(spin * 90f, n) * align;
    }

    // Cases occupees par un bloc dont la case visee est anchorCell, avec
    // l'orientation rotation. La case visee fait toujours partie de la boite.
    public static void GetCells(BlockDefinition def, Vector3Int anchorCell, int rotation, List<Vector3Int> result)
    {
        result.Clear();
        var size = Size(def);
        var min = MinOffset(def);
        var rot = Rotation(def, rotation);
        for (int x = 0; x < size.x; x++)
            for (int y = 0; y < size.y; y++)
                for (int z = 0; z < size.z; z++)
                {
                    var offset = new Vector3(min.x + x, min.y + y, min.z + z);
                    result.Add(anchorCell + Vector3Int.RoundToInt(rot * offset));
                }
    }
}
