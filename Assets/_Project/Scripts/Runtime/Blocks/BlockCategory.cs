namespace Blockforge.Blocks
{
    /// <summary>
    /// Grandes familles de blocs, utilisées pour le tri dans l'inventaire
    /// de construction et pour les règles de gameplay.
    /// </summary>
    public enum BlockCategory
    {
        Structure,   // Cubes d'armure, poutres, plaques
        Movement,    // Roues, chenilles, thrusters, ailes
        Weapon,      // Canons, lasers, lance-missiles
        Utility,     // Générateurs, modules spéciaux, cosmétiques fonctionnels
        Cosmetic     // Purement visuel
    }
}
