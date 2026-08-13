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
