using System;

// =========================================================
// REGLES DE POSE COMMUNES (garage et presets des bots)
// - Rien ne se pose sur un propulseur : ni aile, ni gouvernail,
//   ni arme, ni cube. Le propulseur se fixe sur un support, mais
//   il n'en sert jamais lui-meme (GarageBuildController refuse la
//   pose, BotPresetDebugMenu le signale).
// - Une tige (famille Rod) ne porte que du chassis : arme, plaque,
//   roue, rotor, aile... ne s'accrochent jamais sur une tige.
// - Rotors : leur disque de pales doit rester libre (verifie par
//   BotPresetDebugMenu.Validate sur les presets).
// Les familles viennent de BlockDefinition.family (meme lecture que
// RobotLocomotion : "thruster", "rotor").
// =========================================================

public static class BlockMountRules
{
    public const string ThrusterRule = "Nothing can be mounted on a thruster.";
    public const string RodRule = "A rod only holds chassis blocks: mount this on a cube or another chassis block.";

    public static bool IsThruster(BlockDefinition def) => HasFamily(def, "thruster");

    public static bool IsRotor(BlockDefinition def) => HasFamily(def, "rotor");

    public static bool IsRod(BlockDefinition def) =>
        def != null && (def.family ?? "").IndexOf("rod", StringComparison.OrdinalIgnoreCase) >= 0;

    // Un bloc qui s'accroche a une face (tout sauf le chassis : orientToFace)
    public static bool IsMounted(BlockDefinition def) => def != null && def.orientToFace;

    // Un bloc peut-il servir de support a un autre ?
    public static bool CanSupport(BlockDefinition def) => def != null && !IsThruster(def);

    // Ce support peut-il porter ce bloc ? (une tige ne porte que du chassis)
    public static bool CanSupport(BlockDefinition support, BlockDefinition mounted) =>
        CanSupport(support) && !(IsRod(support) && IsMounted(mounted));

    private static bool HasFamily(BlockDefinition def, string family)
    {
        return def != null && def.category == BlockCategory.Mouvement
               && (def.family ?? "").IndexOf(family, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
