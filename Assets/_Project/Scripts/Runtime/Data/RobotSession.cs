// =========================================================
// ROBOT EN COURS D'EDITION
// Transporte le robot choisi entre l'ecran "Choisir un slot"
// et la scene Garage.
// =========================================================

public static class RobotSession
{
    public static string RecordId;                          // null = pas encore sauvegarde
    public static int Slot;
    public static RobotBlueprint Blueprint = new();

    // Modifications non sauvegardees (nouveau robot, renommage, futurs blocs poses...)
    public static bool Dirty;

    // Scene garage a recharger quand un test (touche P) se termine, fixee par
    // le garage au moment de lancer le test.
    public static string ReturnSceneName = "Garage";

    // Plan du miroir de la grille (true : x' = largeur - 1 - x, la ligne
    // centrale court le long de Z). Fixe par le garage a partir de la ligne
    // centrale de son vaisseau ; relu pour reconstruire les blocs poses en
    // miroir hors du garage (RobotAssembler).
    public static bool MirrorAlongX = true;

    public static void StartNew(int slot)
    {
        RecordId = null;
        Slot = slot;
        Blueprint = new RobotBlueprint { name = $"ROBOT {slot + 1}" };
        Dirty = true; // jamais sauvegarde
    }

    public static void Load(RobotRecord record)
    {
        RecordId = record.id;
        Slot = record.slot;
        Blueprint = record.data ?? new RobotBlueprint();
        Blueprint.name = record.name;
        Dirty = false;
    }
}
