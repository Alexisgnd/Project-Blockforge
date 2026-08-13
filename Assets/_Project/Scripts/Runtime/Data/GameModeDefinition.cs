using UnityEngine;

// =========================================================
// DEFINITION D'UN MODE DE JEU (ecran "Modes de jeu")
// =========================================================

[CreateAssetMenu(menuName = "Blockforge/Game Mode", fileName = "GameMode")]
public class GameModeDefinition : ScriptableObject
{
    [Header("Identite")]
    public string title = "MODE";
    [TextArea] public string tagline = "Description courte du mode.";
    [TextArea(3, 6)] public string description = "Description complete du mode.";
    public bool isCustom;

    [Header("Details")]
    public string teamsLabel = "4v4";
    public string durationLabel = "10-15 min";
    public string ticketsLabel = "1 000";
    public string respawnLabel = "Oui";
    public string teamSizeLabel = "4";

    [Header("Objectifs")]
    [TextArea] public string objectivePrimary = "Objectif principal.";
    [TextArea] public string objectiveSecondary = "Objectif secondaire.";

    [Header("Carte exemple")]
    public string mapName = "Outpost Valley";

    [Header("Navigation")]
    public string sceneName = "Arena";

    [Header("Visuels (optionnels, places plus tard)")]
    public Sprite icon;
    public Sprite coverImage;
    public Sprite mapImage;
}
