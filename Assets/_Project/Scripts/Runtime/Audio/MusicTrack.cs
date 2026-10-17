// =========================================================
// MUSIQUES DU JEU (identifiants)
// Un identifiant par usage, pas par morceau : changer le
// morceau d'un usage = changer le clip dans MusicLibrary,
// zero code.
// =========================================================

public enum MusicTrack
{
    None = 0,         // silence (fondu de sortie de la musique en cours)
    Menu = 1,         // Servo Horizon : Boot + menu principal
    Garage = 2,       // Workshop Pulse : construction du robot
    Matchmaking = 3,  // Launch Sequence : lancement d'une partie
    MapTest = 4,      // Testing Pad Alpha : terrain d'essai (P depuis le garage)
}
