using UnityEngine;

// =========================================================
// DEFINITION D'UN BLOC DE CONSTRUCTION (inventaire garage)
// =========================================================

public enum BlockCategory
{
    Chassis = 0,
    Mouvement = 1,
    Armes = 2,
    Defense = 3,
    Special = 4,
}

[CreateAssetMenu(menuName = "Blockforge/Block", fileName = "Block")]
public class BlockDefinition : ScriptableObject
{
    public static readonly string[] CategoryLabels =
        { "Châssis", "Mouvement", "Armes", "Défense", "Spécial" };

    [Header("Identite")]
    public string blockName = "CUBE";
    [Tooltip("Famille de blocs (ex : Roues, Laser, Lames de survol). Sert au regroupement et a la recherche.")]
    public string family = "";
    [TextArea] public string description = "Bloc de base. Solide et équilibré.";
    public BlockCategory category = BlockCategory.Chassis;

    [Header("Caracteristiques")]
    public int costCpu = 1;
    public string sizeLabel = "1x1x1";
    public int weightKg = 80;
    public int resistanceHp = 1000;

    [Header("Visuels (optionnels)")]
    public Sprite icon;
    [Tooltip("Modele 3D affiche sur la pince ; a defaut un placeholder est genere")]
    public GameObject previewPrefab;

    public string CategoryLabel => CategoryLabels[(int)category];
}
