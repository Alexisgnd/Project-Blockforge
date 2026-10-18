using UnityEngine;

// =========================================================
// DEFINITION D'UN MODE DE JEU (ecran Play)
// Un asset par mode (Data/GameModes), lu par l'ecran Play :
// la ligne de la liste (titre, accroche), la fiche du mode
// selectionne (description, trois chiffres, carte) et l'ecran
// Versus (titre, resume, taille des equipes).
// Les libelles sont en anglais, sentence case ; les chiffres
// portent leur unite (design system).
// =========================================================

[CreateAssetMenu(menuName = "Blockforge/Game Mode", fileName = "GameMode")]
public class GameModeDefinition : ScriptableObject
{
    [Header("Identite")]
    public string title = "Team Deathmatch";
    [Tooltip("Ligne sous le titre dans la liste : « 5 vs 5 · first to 25 · about 10 min »")]
    public string summary = "5 vs 5 · first to 25 · about 10 min";
    [TextArea(3, 6)] public string description = "Two teams of five. Destroy enemy robots to score.";
    [Tooltip("Pictogramme du DS (Resources/DesignSystem/Icons) : skull, objective, group…")]
    public string iconName = "skull";

    [Header("Chiffres de la fiche (readout)")]
    public string playersLabel = "5 vs 5";
    public string targetLabel = "25 kills";
    public string timeLabel = "10:00";

    [Header("Match")]
    [Tooltip("Robots par equipe (le joueur compris) ; les sieges vides sont pris par des bots")]
    [Range(1, 8)] public int teamSize = 5;
    [Tooltip("Nom de la carte affiche dans la fiche et sur l'ecran Versus")]
    public string mapName = "Hex valley";
    [Tooltip("Resume sous le titre de l'ecran Versus : « first to 25 · 10:00 »")]
    public string versusSummary = "first to 25 · 10:00";

    [Header("Navigation")]
    [Tooltip("Scene chargee au lancement (doit etre dans la liste du build)")]
    public string sceneName = "Map_MARS";

    [Header("Visuels (optionnels)")]
    public Sprite icon;
    public Sprite mapImage;
}
