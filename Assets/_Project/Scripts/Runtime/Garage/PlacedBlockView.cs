using UnityEngine;

// =========================================================
// MARQUEUR D'UN BLOC POSE SUR LA GRILLE
// cell = case visee a la pose (celle qui est sauvegardee) ;
// cells = toutes les cases de sa boite d'empreinte tournee.
// =========================================================

public class PlacedBlockView : MonoBehaviour
{
    public Vector3Int cell;
    public BlockDefinition definition;
    public Vector3Int[] cells;
}
