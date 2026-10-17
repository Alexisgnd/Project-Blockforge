using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

// =========================================================
// IMPLEMENTATIONS MOCK DES SERVICES META
// Donnees : MockMetaData (asset Resources/Meta ou jeu par
// defaut en memoire). Un seul etat partage : le portefeuille,
// les pieces possedees, les noeuds recherches — rechercher un
// noeud le fait apparaitre dans le Store, acheter debite le
// portefeuille et incremente « Owned ».
// Les awards sont calcules ici (serveur dans le vrai jeu).
// =========================================================

namespace Blockforge.Meta
{
    internal static class MockText
    {
        // « 4 200 » saisi a la main -> milliers en espace fine du DS ; « 0.8 » inchange
        public static string Value(string v)
        {
            if (string.IsNullOrEmpty(v) || v.Contains(".")) return v;
            string digits = v.Replace(" ", "");
            return long.TryParse(digits, out long n) ? BlockforgeTheme.Num(n) : v;
        }

        // Nombres de 4 chiffres et plus dans une phrase
        public static string Sentence(string text) =>
            string.IsNullOrEmpty(text) ? text
            : System.Text.RegularExpressions.Regex.Replace(text, @"\b\d{1,3}(?: \d{3})+\b", m => Value(m.Value));
    }

    public sealed class MockServices
    {
        private static MockServices instance;
        public static MockServices Instance => instance ??= new MockServices(MockMetaData.Load());
        public static void Reset() => instance = null;

        public readonly MockMetaData Data;
        public readonly Wallet Wallet;
        public readonly MockMatchResultSource MatchResults;
        public readonly MockWorkshop Workshop;
        public readonly MockStore Store;
        public readonly MockTechTree TechTree;

        public MockServices(MockMetaData data)
        {
            Data = data;
            Wallet = new Wallet(data.credits, data.techPoints);
            MatchResults = new MockMatchResultSource(data);
            Store = new MockStore(data, Wallet);
            TechTree = new MockTechTree(data, Wallet, Store);
            Workshop = new MockWorkshop(data, Wallet);
        }
    }

    // ---------- Fin de match ----------

    public sealed class MockMatchResultSource : IMatchResultSource
    {
        private readonly MockMetaData data;
        public event Action<MatchResult> MatchEnded;
        public MatchResult Last { get; private set; }

        public MockMatchResultSource(MockMetaData data) { this.data = data; }

        public MatchResult Build(MatchOutcome outcome)
        {
            var players = new List<PlayerMatchStats>();
            int i = 0;
            foreach (var p in data.players)
                players.Add(new PlayerMatchStats("p" + i++, p.name, p.team, p.isLocal, p.isBot, p.kills, p.deaths, p.assists, p.damage, p.healing, p.score));

            int ally = data.allyScore, enemy = data.enemyScore;
            if (outcome == MatchOutcome.Defeat) (ally, enemy) = (Math.Min(ally, enemy), Math.Max(ally, enemy));
            else if (outcome == MatchOutcome.Victory) (ally, enemy) = (Math.Max(ally, enemy), Math.Min(ally, enemy));
            else enemy = ally;

            var rewards = new MatchRewards(
                outcome == MatchOutcome.Defeat ? data.rewardCredits / 2 : data.rewardCredits,
                outcome == MatchOutcome.Defeat ? data.rewardTechPoints / 2 : data.rewardTechPoints,
                data.rewardXp, data.level, data.xpInLevel, data.xpForNextLevel);

            return new MatchResult(data.modeName, data.mapName, TimeSpan.FromSeconds(data.durationSeconds), outcome,
                                   ally, enemy, players, MatchLogic.ComputeAwards(players), rewards);
        }

        // Menu debug : simule la fin de match
        public void Fire(MatchOutcome outcome)
        {
            Last = Build(outcome);
            MatchEnded?.Invoke(Last);
        }

        public void Fire(MatchResult result)
        {
            Last = result;
            MatchEnded?.Invoke(result);
        }
    }

    // ---------- Workshop ----------

    public sealed class MockWorkshop : IWorkshopService
    {
        private readonly MockMetaData data;
        private readonly Wallet wallet;
        public int PageSize => BlockforgeTheme.Workshop.PageSize;
        public readonly HashSet<string> Downloaded = new();

        public MockWorkshop(MockMetaData data, Wallet wallet) { this.data = data; this.wallet = wallet; }

        private List<RobotListing> All()
        {
            var list = new List<RobotListing>();
            foreach (var r in data.robots)
            {
                var parts = new Dictionary<Category, int>
                {
                    [Category.Structure] = r.blocks, [Category.Mobility] = r.movement,
                    [Category.Weapon] = r.weapons, [Category.Electronics] = r.electronics,
                };
                list.Add(new RobotListing(r.id, r.name, r.author, r.cpu, r.mass, r.topSpeedMph, r.health, r.downloads, r.price,
                                          r.grade, r.mainCategory, parts, r.ownedParts, r.totalParts, MockText.Sentence(r.description), r.updatedLabel));
            }
            return list;
        }

        private List<RobotListing> Filtered(WorkshopShelf shelf, CpuFilter cpu)
        {
            var list = All();
            list.RemoveAll(r => shelf switch
            {
                WorkshopShelf.Mine => !Downloaded.Contains(r.Id),
                WorkshopShelf.Friends => r.Downloads % 2 == 0,
                _ => false,
            });
            list.RemoveAll(r => cpu switch
            {
                CpuFilter.Under1000 => r.Cpu >= 1000,
                CpuFilter.From1000To1500 => r.Cpu < 1000 || r.Cpu > 1500,
                CpuFilter.From1500To2000 => r.Cpu < 1500 || r.Cpu > 2000,
                CpuFilter.FitsMyBay => r.Cpu > data.cpuBudget,
                _ => false,
            });
            return list;
        }

        public int TotalCount(WorkshopShelf shelf, CpuFilter cpuFilter) =>
            shelf is WorkshopShelf.Mine or WorkshopShelf.Friends ? Filtered(shelf, cpuFilter).Count
            : Math.Max(Filtered(shelf, cpuFilter).Count, data.workshopTotal);

        public int MineCount => Downloaded.Count;

        public Task<IReadOnlyList<RobotListing>> Browse(WorkshopShelf shelf, CpuFilter cpuFilter, WorkshopSort sort, int page)
        {
            var list = Filtered(shelf, cpuFilter);
            list.Sort((a, b) => sort switch
            {
                WorkshopSort.Cheapest => a.PriceCredits.CompareTo(b.PriceCredits),
                WorkshopSort.Newest => string.CompareOrdinal(b.Id, a.Id),
                WorkshopSort.BestRated => b.Health.CompareTo(a.Health),
                _ => b.Downloads.CompareTo(a.Downloads),
            });
            int start = Math.Max(0, page) * PageSize;
            var pageList = new List<RobotListing>();
            for (int i = start; i < list.Count && i < start + PageSize; i++)
                pageList.Add(list[i]);
            return Task.FromResult<IReadOnlyList<RobotListing>>(pageList);
        }

        public Task Download(string id)
        {
            foreach (var r in data.robots)
            {
                if (r.id != id)
                    continue;
                if (!wallet.TryDebitCredits(r.price))
                    throw new InsufficientFundsException("Not enough credits.");
                Downloaded.Add(id);
                r.downloads++;
                return Task.CompletedTask;
            }
            throw new KeyNotFoundException(id);
        }
    }

    // ---------- Store ----------

    public sealed class MockStore : IStoreService
    {
        private readonly MockMetaData data;
        private readonly Wallet wallet;
        public event Action Changed;

        public MockStore(MockMetaData data, Wallet wallet) { this.data = data; this.wallet = wallet; }

        public (string robotName, int robotCpu, int cpuBudget) CurrentRobot => (data.currentRobotName, data.currentRobotCpu, data.cpuBudget);

        private static StoreItem ToItem(MockMetaData.StoreItemSeed s)
        {
            var stats = new List<(string, string, float)>();
            foreach (var st in s.stats) stats.Add((st.label, MockText.Value(st.value), st.ratio));
            return new StoreItem(s.id, s.name, s.category, s.grade, s.cpu, s.price, s.owned, s.requiresResearch, stats, MockText.Sentence(s.description));
        }

        public StoreItem Find(string id)
        {
            foreach (var s in data.items)
                if (s.id == id) return ToItem(s);
            return null;
        }

        public Task<IReadOnlyList<StoreItem>> Shelf(StoreShelf shelf)
        {
            var list = new List<StoreItem>();
            foreach (var s in data.items)
            {
                bool keep = shelf switch
                {
                    StoreShelf.Blocks => s.category == Category.Structure,
                    StoreShelf.Movement => s.category == Category.Mobility,
                    StoreShelf.Weapons => s.category == Category.Weapon,
                    StoreShelf.Electronics => s.category == Category.Electronics,
                    StoreShelf.Cosmetics => s.category == Category.Cosmetic,
                    StoreShelf.Bundles => false,
                    _ => s.category == Category.Weapon, // Featured : le rayon mis en avant
                };
                if (keep)
                    list.Add(ToItem(s));
            }
            return Task.FromResult<IReadOnlyList<StoreItem>>(list);
        }

        public Task<Bundle> Featured()
        {
            var b = data.bundle;
            var contents = new List<(Category, int)>
            {
                (Category.Weapon, b.weapons), (Category.Electronics, b.modules), (Category.Structure, b.blocks), (Category.Cosmetic, b.paintSets),
            };
            return Task.FromResult(new Bundle(b.id, b.name, MockText.Sentence(b.description), b.price, b.wasPrice, TimeSpan.FromHours(b.endsInHours), contents));
        }

        public Task Buy(string id, int qty)
        {
            foreach (var s in data.items)
            {
                if (s.id != id)
                    continue;
                if (s.requiresResearch)
                    throw new InvalidOperationException("Research first.");
                if (!wallet.TryDebitCredits(s.price * Math.Max(1, qty)))
                    throw new InsufficientFundsException("Not enough credits.");
                s.owned += Math.Max(1, qty);
                Changed?.Invoke();
                return Task.CompletedTask;
            }
            throw new KeyNotFoundException(id);
        }

        public Task BuyBundle(string id)
        {
            if (!wallet.TryDebitCredits(data.bundle.price))
                throw new InsufficientFundsException("Not enough credits.");
            foreach (var s in data.items)
            {
                if (s.id == "plasma-launcher") s.owned += data.bundle.weapons;
                if (s.id == "shield-module") { s.owned += data.bundle.modules; s.requiresResearch = false; }
                if (s.id == "hardened-plate") s.owned += data.bundle.blocks;
                if (s.id == "ember-paint") s.owned += data.bundle.paintSets;
            }
            Changed?.Invoke();
            return Task.CompletedTask;
        }

        // Un noeud recherche met sa piece en rayon
        internal void Unlock(string itemId)
        {
            foreach (var s in data.items)
                if (s.id == itemId) s.requiresResearch = false;
            Changed?.Invoke();
        }
    }

    // ---------- Tech Tree ----------

    public sealed class MockTechTree : ITechTreeService
    {
        private readonly MockMetaData data;
        private readonly Wallet wallet;
        private readonly MockStore store;
        public event Action Changed;

        public MockTechTree(MockMetaData data, Wallet wallet, MockStore store)
        {
            this.data = data;
            this.wallet = wallet;
            this.store = store;
        }

        public int TechPoints => wallet.TechPoints;

        private MockMetaData.TechNodeSeed Seed(string id)
        {
            foreach (var n in data.nodes)
                if (n.id == id) return n;
            return null;
        }

        private MockMetaData.TechNodeSeed Neighbour(MockMetaData.TechNodeSeed n, int delta)
        {
            foreach (var o in data.nodes)
                if (o.lane == n.lane && o.tier == n.tier + delta) return o;
            return null;
        }

        public Task<IReadOnlyList<TechNode>> Nodes()
        {
            var list = new List<TechNode>();
            foreach (var n in data.nodes)
            {
                var stats = new List<(string, string, float)>();
                foreach (var st in n.stats) stats.Add((st.label, MockText.Value(st.value), st.ratio));
                var prev = Neighbour(n, -1);
                var next = Neighbour(n, +1);
                list.Add(new TechNode(n.id, n.name, n.lane, n.tier, n.grade, n.state, n.cost, prev?.id, next?.id, n.storePrice, n.description, stats));
            }
            return Task.FromResult<IReadOnlyList<TechNode>>(list);
        }

        public Task Research(string id)
        {
            var n = Seed(id);
            if (n == null)
                throw new KeyNotFoundException(id);
            if (n.state == TechState.Researched)
                return Task.CompletedTask;
            if (n.state == TechState.Locked)
                throw new InvalidOperationException("Research the part before it.");
            if (!wallet.TryDebitTechPoints(n.cost))
                throw new InsufficientFundsException("Not enough tech points.");
            n.state = TechState.Researched;
            var next = Neighbour(n, +1);
            if (next != null && next.state == TechState.Locked)
                next.state = TechState.Available;
            store.Unlock(n.id);
            Changed?.Invoke();
            return Task.CompletedTask;
        }
    }
}
