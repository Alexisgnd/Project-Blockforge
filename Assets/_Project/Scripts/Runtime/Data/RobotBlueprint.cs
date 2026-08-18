using System;
using System.Collections.Generic;

// =========================================================
// BLUEPRINT D'UN ROBOT (contenu du champ "data" PocketBase)
// La liste des blocs se remplira quand le systeme de
// construction sera en place.
// =========================================================

[Serializable]
public class PlacedBlock
{
    public string blockId;   // nom de l'asset BlockDefinition (ex: "Block_01_Cube")
    public int x;
    public int y;
    public int z;
    public int rotation;     // pas de 90 degres (0-3)
    public int colorIndex;
}

[Serializable]
public class RobotBlueprint
{
    // Dimensions de la grille de construction du garage (v2 : 14x14)
    public const int GridWidth = 14;
    public const int GridDepth = 14;
    public const int GridHeight = 50; // hauteur max en blocs

    public string name = "NOUVEAU ROBOT";
    public List<PlacedBlock> blocks = new();
}

// Stats calculees, stockees dans le champ "stats" PocketBase
// pour l'affichage des menus sans parser le blueprint.
[Serializable]
public class RobotStats
{
    public int cpu;
    public int health;
    public int speedKmh;
    public int energy;
    public int weightKg;
}
