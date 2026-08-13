using UnityEngine;

// =========================================================
// PRESET DE ROBOT (donnees d'un slot du menu d'accueil)
// =========================================================

[CreateAssetMenu(menuName = "Blockforge/Robot Preset", fileName = "RobotPreset")]
public class RobotPreset : ScriptableObject
{
    [Header("Identite")]
    public string bayLabel = "BAY 01";
    public string robotName = "SCOUT";
    public string tier = "DÉBUTANT";

    [Header("Statistiques")]
    public int capacity = 1000;
    public int offensivePower = 1000;
    public int health = 2000;
    public int speedKmh = 150;
    public int energyPerSec = 400;
    public int weightKg = 5000;
    public int blockCount = 80;
}
