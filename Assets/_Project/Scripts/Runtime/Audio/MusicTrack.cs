// =========================================================
// MUSIQUES DU JEU (identifiants)
// Un identifiant par usage, pas par morceau : changer le
// morceau d'un usage = changer le clip dans MusicLibrary,
// zero code. Valeurs fixes (serialisees dans MusicLibrary) :
// ne jamais renumeroter, ajouter a la suite.
// =========================================================

public enum MusicTrack
{
    None = 0,             // silence (fondu de sortie de la musique en cours)
    Menu = 1,             // Servo Horizon : Boot + menu principal
    Garage = 2,           // Workshop Pulse : construction du robot
    Matchmaking = 3,      // Launch Sequence : lancement d'une partie
    MapTest = 4,          // Testing Pad Alpha : terrain d'essai (P depuis le garage)
    MarsCombat = 5,       // Mars Combat Loop : musique de la carte Mars
    MarsProtocol = 6,     // Mars Protocol : derniere action (apres la premiere alarme), depart a 1:25
    RobotSpawn = 7,       // jingle (une fois) : apparition du robot du joueur
    FinalTransition = 8,  // jingle (une fois) : passage de MarsCombat a MarsProtocol
}
