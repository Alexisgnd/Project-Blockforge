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
    public int rotation;     // face d'accroche * 4 + quart de tour autour de cette face (BlockFootprint) ;
                             // 0-3 = orientation naturelle + lacet (anciens blueprints)
    public bool mirrored;    // pose en mode miroir : image du bloc par le plan central de la grille
    public int colorIndex;
}

[Serializable]
public class RobotBlueprint
{
    // Dimensions maximales de la baie de construction du garage : 31 x 31 x 31
    // blocs (largeur x profondeur x hauteur), contrat du vaisseau Mothership
    // (Garage_V2, cases de 1 m). Le nombre de cases reellement disponibles est
    // mesure sur les lignes du modele de la scene (l'ancien vaisseau n'en a
    // que 14 x 14) ; ces constantes bornent les coordonnees d'un blueprint.
    public const int GridWidth = 31;
    public const int GridDepth = 31;
    public const int GridHeight = 31;

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
