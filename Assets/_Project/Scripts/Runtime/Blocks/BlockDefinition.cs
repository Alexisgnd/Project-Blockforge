using UnityEngine;

namespace Blockforge.Blocks
{
    /// <summary>
    /// Définition data-driven d'un bloc. Un asset par type de bloc
    /// (créé via clic droit > Blockforge > Block Definition), rangé dans
    /// Assets/_Project/Data/Blocks. Le gameplay lit ces assets : ajouter
    /// un nouveau bloc au jeu ne demande aucun code.
    /// </summary>
    [CreateAssetMenu(menuName = "Blockforge/Block Definition", fileName = "Block_")]
    public class BlockDefinition : ScriptableObject
    {
        [Header("Identité")]
        [Tooltip("Identifiant stable utilisé dans les sauvegardes. Ne jamais le changer après publication.")]
        public string Id;
        public string DisplayName;
        [TextArea] public string Description;
        public BlockCategory Category;
        public Sprite Icon;

        [Header("Visuel & placement")]
        public GameObject Prefab;
        [Tooltip("Taille occupée sur la grille de construction, en cellules.")]
        public Vector3Int GridSize = Vector3Int.one;

        [Header("Stats")]
        [Min(0f)] public float Mass = 1f;
        [Min(1f)] public float MaxHealth = 100f;
        [Tooltip("Coût en ressources dans le garage.")]
        [Min(0)] public int Cost;
    }
}
