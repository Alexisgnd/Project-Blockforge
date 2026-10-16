using UnityEngine;

// =========================================================
// MARQUEUR D'UN BLOC POSE SUR LA GRILLE
// cell = case visee a la pose (celle qui est sauvegardee) ;
// cells = toutes les cases de sa boite d'empreinte tournee ;
// colorIndex = peinture (0 = origine, 1..12 = BlockPalette) ;
// destroyed = detruit en match (RobotHealth), ignore par les armes
// et la locomotion.
// =========================================================

public class PlacedBlockView : MonoBehaviour
{
    public Vector3Int cell;
    public BlockDefinition definition;
    public bool mirrored;
    public Vector3Int[] cells;
    public int colorIndex;
    // Match : bloc detruit (RobotHealth) ; jamais sauvegarde
    [System.NonSerialized] public bool destroyed;
}
