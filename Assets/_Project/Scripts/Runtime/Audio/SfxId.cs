// =========================================================
// EFFETS SONORES DU JEU (identifiants)
// Un identifiant par usage, pas par fichier : changer le son
// d'un usage = changer le clip dans SfxLibrary, zero code.
// Valeurs fixes (serialisees dans SfxLibrary) : ne jamais
// renumeroter, ajouter a la suite.
// =========================================================

public enum SfxId
{
    None = 0,
    MatchAlert = 1,         // 1 min, 30 s, 10 s restantes ; une equipe a un kill de la victoire
    RobotRegen = 2,         // le robot regarde commence a se regenerer
    WeaponNoEnergy = 3,     // tir refuse, plus assez d'energie (jamais superpose a lui-meme)
    GarageBlockPlace = 4,   // bloc pose dans le garage (miroir compris : un seul son)
    GarageBlockRemove = 5,  // bloc retire dans le garage (miroir compris : un seul son)
    RegenCountdown = 6,     // boucle : 5 dernieres secondes avant la regeneration
    RegenInterrupted = 7,   // attente de regeneration cassee par un degat (une fois)
    RespawnTick = 8,        // bip a chaque seconde du compte a rebours de reapparition
}
