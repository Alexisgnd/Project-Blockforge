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

// Face du modele plaquee sur sa boite d'empreinte. La boite croit a
// l'oppose de cette face depuis la case visee (sur les autres axes :
// centree si le nombre de cases est impair, sinon vers le cote positif).
public enum BlockAnchor
{
    Bottom = 0,  // base posee au fond de la boite (defaut : chassis, armes, propulseurs...)
    Right = 1,   // fixation cote +X (roues : moyeu)
    Left = 2,    // fixation cote -X (pattes d'insecte : plaque de hanche)
    Back = 3,    // fixation a l'arriere, -Z (electroplates, lames de survol, disque de bouclier)
    Center = 4,  // centre dans la boite
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
    public int weightKg = 80;
    public int resistanceHp = 1000;

    [Header("Encombrement sur la grille")]
    [Tooltip("Empreinte en cases : largeur (X), hauteur (Y), profondeur (Z). Mesuree au repos pour les " +
             "blocs animes : un radar deplie ou une lame de Tesla en action deborderont de leur boite.")]
    public Vector3Int footprint = Vector3Int.one;
    [Tooltip("Face du modele plaquee sur la boite d'empreinte ; la boite croit a l'oppose depuis la case visee.")]
    public BlockAnchor anchor = BlockAnchor.Bottom;
    [Tooltip("Garde l'echelle native du FBX (1 m = 1 case) au lieu d'ajuster le modele dans sa boite. " +
             "Pour les tiges, dont la geometrie relie des centres de cases et deborde legerement.")]
    public bool nativeScale;

    [Header("Visuels (optionnels)")]
    public Sprite icon;
    [Tooltip("Modele 3D affiche sur la pince ; a defaut un placeholder est genere")]
    public GameObject previewPrefab;

    public string CategoryLabel => CategoryLabels[(int)category];

    // Etiquette "TAILLE" de la fiche inventaire (largeur x hauteur x profondeur, en cases)
    public string SizeLabel => $"{footprint.x}x{footprint.y}x{footprint.z}";
}
