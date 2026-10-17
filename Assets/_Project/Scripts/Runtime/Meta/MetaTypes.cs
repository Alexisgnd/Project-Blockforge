using System;
using System.Collections.Generic;

// =========================================================
// MODELE DES ECRANS « META » (fin de match, Workshop, Store,
// Tech Tree) : types C# purs, sans MonoBehaviour. Les services
// (IMatchResultSource, IWorkshopService, IStoreService,
// ITechTreeService, IWallet) les produisent ; les ecrans les
// consomment sans savoir si la source est le serveur ou un mock.
// =========================================================

namespace System.Runtime.CompilerServices
{
    // Requis par les records positionnels (accesseurs init) sous Unity
    internal static class IsExternalInit { }
}

namespace Blockforge.Meta
{
    public enum Team { Ally, Enemy }                     // Ally = l'equipe du joueur local, toujours a gauche
    public enum MatchOutcome { Victory, Defeat, Draw }
    public enum AwardKind { BestDamage, BestHealing, BestKills, BestAssists }

    // Categories de pieces du design system (couleur category-*)
    public enum Category { Structure, Mobility, Weapon, Electronics, Cosmetic }

    // Grades de forge (barre de 3 px, puce grade-*)
    public enum Grade { Raw, Forged, Tempered, Hardened, Masterwork }

    public enum TechState { Researched, Available, Locked }

    public record PlayerMatchStats(string PlayerId, string DisplayName, Team Team, bool IsLocal, bool IsBot,
                                   int Kills, int Deaths, int Assists, int Damage, int Healing, int Score);

    public record Award(AwardKind Kind, string PlayerId, int Value);

    public record MatchRewards(int Credits, int TechPoints, int Xp, int Level, int XpInLevel, int XpForNextLevel);

    public record MatchResult(string ModeName, string MapName, TimeSpan Duration, MatchOutcome Outcome,
                              int AllyScore, int EnemyScore, IReadOnlyList<PlayerMatchStats> Players,
                              IReadOnlyList<Award> Awards, MatchRewards Rewards)
    {
        public bool VsBots
        {
            get
            {
                foreach (var p in Players)
                    if (!p.IsLocal && !p.IsBot) return false;
                return true;
            }
        }

        public PlayerMatchStats Find(string playerId)
        {
            foreach (var p in Players)
                if (p.PlayerId == playerId) return p;
            return null;
        }
    }

    public record RobotListing(string Id, string Name, string Author, int Cpu, int Mass, int TopSpeedMph, int Health,
                               int Downloads, int PriceCredits, Grade Grade, Category MainCategory,
                               IReadOnlyDictionary<Category, int> PartCounts, int OwnedPartCount, int TotalPartCount,
                               string Description, string UpdatedLabel = "updated 2 days ago");

    public record StoreItem(string Id, string Name, Category Category, Grade Grade, int Cpu, int PriceCredits, int Owned,
                            bool RequiresResearch, IReadOnlyList<(string label, string value, float ratio)> Stats,
                            string Description);

    public record Bundle(string Id, string Name, string Description, int PriceCredits, int WasPriceCredits, TimeSpan EndsIn,
                         IReadOnlyList<(Category category, int count)> Contents);

    public record TechNode(string Id, string Name, Category Lane, int Tier, Grade Grade, TechState State, int CostTechPoints,
                           string RequiresId, string UnlocksId, int StorePriceAfter, string Description,
                           IReadOnlyList<(string label, string value, float ratio)> Stats);

    // Rayons du Workshop et tris proposes par l'ecran
    public enum WorkshopShelf { Featured, New, TopRated, Friends, Mine }
    public enum WorkshopSort { MostDownloaded, BestRated, Newest, Cheapest }
    public enum CpuFilter { Any, Under1000, From1000To1500, From1500To2000, FitsMyBay }

    public enum StoreShelf { Featured, Blocks, Movement, Weapons, Electronics, Cosmetics, Bundles }
    public enum StoreSort { ByGrade, PriceLowToHigh, Newest }

    public static class MetaLabels
    {
        public static string Of(Category c) => c switch
        {
            Category.Structure => "Structure",
            Category.Mobility => "Movement",
            Category.Weapon => "Weapons",
            Category.Electronics => "Electronics",
            _ => "Cosmetics",
        };

        // Singulier pour une fiche : « Weapon · Hardened »
        public static string Singular(Category c) => c switch
        {
            Category.Structure => "Block",
            Category.Mobility => "Movement",
            Category.Weapon => "Weapon",
            Category.Electronics => "Electronics",
            _ => "Cosmetic",
        };

        public static string Icon(Category c) => c switch
        {
            Category.Structure => "structure",
            Category.Mobility => "mobility",
            Category.Weapon => "weapon",
            Category.Electronics => "electronics",
            _ => "cosmetic",
        };

        public static string Of(Grade g) => g switch
        {
            Grade.Raw => "Raw",
            Grade.Forged => "Forged",
            Grade.Tempered => "Tempered",
            Grade.Hardened => "Hardened",
            _ => "Masterwork",
        };

        public static string Roman(int tier) => tier switch { 1 => "I", 2 => "II", 3 => "III", 4 => "IV", 5 => "V", _ => tier.ToString() };

        public static string Of(AwardKind k) => k switch
        {
            AwardKind.BestDamage => "Best damage",
            AwardKind.BestHealing => "Best healing",
            AwardKind.BestKills => "Best kills",
            _ => "Best assists",
        };

        public static string Icon(AwardKind k) => k switch
        {
            AwardKind.BestDamage => "weapon",
            AwardKind.BestHealing => "health",
            AwardKind.BestKills => "skull",
            _ => "group",
        };

        public static string TeamName(Team t) => t == Team.Ally ? "Blue team" : "Red team";
    }
}
