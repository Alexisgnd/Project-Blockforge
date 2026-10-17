using System;
using System.Collections.Generic;
using System.Threading.Tasks;

// =========================================================
// SERVICES DES ECRANS META
// Interfaces consommees par les ecrans ; MetaServices.* porte
// l'implementation courante (mock par defaut, cf. MockServices).
// Le vrai jeu branchera PocketBase / le serveur de match sans
// toucher aux ecrans.
// =========================================================

namespace Blockforge.Meta
{
    public interface IMatchResultSource
    {
        event Action<MatchResult> MatchEnded;
        MatchResult Last { get; }
    }

    public interface IWorkshopService
    {
        Task<IReadOnlyList<RobotListing>> Browse(WorkshopShelf shelf, CpuFilter cpuFilter, WorkshopSort sort, int page);
        int TotalCount(WorkshopShelf shelf, CpuFilter cpuFilter);
        int PageSize { get; }
        Task Download(string id);
    }

    public interface IStoreService
    {
        Task<IReadOnlyList<StoreItem>> Shelf(StoreShelf shelf);
        Task<Bundle> Featured();
        Task Buy(string id, int qty);
        Task BuyBundle(string id);
        // CPU de la piece dans le robot courant : (cpu actuel du robot, budget)
        (string robotName, int robotCpu, int cpuBudget) CurrentRobot { get; }
        event Action Changed;
    }

    public interface ITechTreeService
    {
        Task<IReadOnlyList<TechNode>> Nodes();
        Task Research(string id);
        int TechPoints { get; }
        event Action Changed;
    }

    public interface IWallet
    {
        int Credits { get; }
        int TechPoints { get; }
        event Action<int> CreditsChanged;
        event Action<int> TechPointsChanged;
    }

    // Portefeuille en memoire partage par les mocks (et point d'entree du debug)
    public sealed class Wallet : IWallet
    {
        private int credits, techPoints;

        public Wallet(int credits, int techPoints)
        {
            this.credits = credits;
            this.techPoints = techPoints;
        }

        public int Credits => credits;
        public int TechPoints => techPoints;
        public event Action<int> CreditsChanged;
        public event Action<int> TechPointsChanged;

        public bool TryDebitCredits(int amount)
        {
            if (amount < 0 || amount > credits)
                return false;
            credits -= amount;
            CreditsChanged?.Invoke(credits);
            return true;
        }

        public bool TryDebitTechPoints(int amount)
        {
            if (amount < 0 || amount > techPoints)
                return false;
            techPoints -= amount;
            TechPointsChanged?.Invoke(techPoints);
            return true;
        }

        public void AddCredits(int amount)
        {
            credits = Math.Max(0, credits + amount);
            CreditsChanged?.Invoke(credits);
        }

        public void AddTechPoints(int amount)
        {
            techPoints = Math.Max(0, techPoints + amount);
            TechPointsChanged?.Invoke(techPoints);
        }
    }

    public sealed class InsufficientFundsException : Exception
    {
        public InsufficientFundsException(string what) : base(what) { }
    }

    // Localisateur : les ecrans lisent ici, les mocks s'installent au premier acces
    public static class MetaServices
    {
        private static IMatchResultSource matchResults;
        private static IWorkshopService workshop;
        private static IStoreService store;
        private static ITechTreeService techTree;
        private static IWallet wallet;

        public static IMatchResultSource MatchResults { get => matchResults ??= MockServices.Instance.MatchResults; set => matchResults = value; }
        public static IWorkshopService Workshop { get => workshop ??= MockServices.Instance.Workshop; set => workshop = value; }
        public static IStoreService Store { get => store ??= MockServices.Instance.Store; set => store = value; }
        public static ITechTreeService TechTree { get => techTree ??= MockServices.Instance.TechTree; set => techTree = value; }
        public static IWallet Wallet { get => wallet ??= MockServices.Instance.Wallet; set => wallet = value; }

        public static void Reset()
        {
            matchResults = null;
            workshop = null;
            store = null;
            techTree = null;
            wallet = null;
            MockServices.Reset();
        }
    }
}
