using System.Collections.Generic;
using System.Linq;
using Blockforge.Meta;
using NUnit.Framework;
using UnityEngine;

// =========================================================
// TESTS DES REGLES META (Play mode, Window > General > Test Runner)
// - awards : meilleur de chaque colonne, egalite -> score le plus
//   haut, valeur 0 -> personne ;
// - podium : top 3 des deux equipes par score ;
// - connecteurs de l'arbre : toutes les combinaisons de voisins ;
// - portefeuille : debit a l'achat et a la recherche, refus sans
//   debit si les fonds manquent, recherche -> voisin ouvert et
//   piece au Store.
// =========================================================

namespace Blockforge.Tests
{
    public class MetaLogicTests
    {
        private static PlayerMatchStats P(string id, Team team, int kills = 0, int deaths = 0, int assists = 0, int damage = 0, int healing = 0, int score = 0, bool local = false) =>
            new(id, id, team, local, !local, kills, deaths, assists, damage, healing, score);

        private static MockServices Fresh()
        {
            var data = ScriptableObject.CreateInstance<MockMetaData>();
            data.FillDefaults();
            return new MockServices(data);
        }

        // ---------- Awards ----------

        [Test]
        public void Awards_PickTheBestOfEachColumn()
        {
            var players = new List<PlayerMatchStats>
            {
                P("a", Team.Ally, kills: 7, damage: 100, healing: 5, assists: 1, score: 10),
                P("b", Team.Enemy, kills: 2, damage: 900, healing: 0, assists: 6, score: 20),
                P("c", Team.Ally, kills: 1, damage: 50, healing: 300, assists: 2, score: 5),
            };
            var awards = MatchLogic.ComputeAwards(players).ToDictionary(a => a.Kind);
            Assert.AreEqual("b", awards[AwardKind.BestDamage].PlayerId);
            Assert.AreEqual(900, awards[AwardKind.BestDamage].Value);
            Assert.AreEqual("c", awards[AwardKind.BestHealing].PlayerId);
            Assert.AreEqual("a", awards[AwardKind.BestKills].PlayerId);
            Assert.AreEqual("b", awards[AwardKind.BestAssists].PlayerId);
        }

        [Test]
        public void Awards_TieGoesToTheHigherScore()
        {
            var players = new List<PlayerMatchStats>
            {
                P("low", Team.Ally, kills: 5, score: 100),
                P("high", Team.Enemy, kills: 5, score: 900),
                P("mid", Team.Ally, kills: 5, score: 500),
            };
            var kills = MatchLogic.ComputeAwards(players).Single(a => a.Kind == AwardKind.BestKills);
            Assert.AreEqual("high", kills.PlayerId);
            Assert.AreEqual(5, kills.Value);
        }

        [Test]
        public void Awards_NobodyQualifiesWhenEveryValueIsZero()
        {
            var players = new List<PlayerMatchStats> { P("a", Team.Ally, kills: 3, score: 1), P("b", Team.Enemy, score: 2) };
            var healing = MatchLogic.ComputeAwards(players).Single(a => a.Kind == AwardKind.BestHealing);
            Assert.IsNull(healing.PlayerId);
            Assert.AreEqual(0, healing.Value);
        }

        // ---------- Podium ----------

        [Test]
        public void Podium_TakesTheTopThreeAcrossBothTeams()
        {
            var players = new List<PlayerMatchStats>
            {
                P("ally1", Team.Ally, score: 2180, local: true),
                P("ally2", Team.Ally, score: 1640),
                P("ally3", Team.Ally, score: 1410),
                P("enemy1", Team.Enemy, score: 1520),
                P("enemy2", Team.Enemy, score: 1150),
            };
            var podium = MatchLogic.Podium(players).Select(p => p.PlayerId).ToArray();
            CollectionAssert.AreEqual(new[] { "ally1", "ally2", "enemy1" }, podium);
        }

        [Test]
        public void Podium_LosingTeamCanTakeFirstPlace()
        {
            var players = new List<PlayerMatchStats>
            {
                P("winner", Team.Ally, score: 800),
                P("loser", Team.Enemy, score: 3000),
            };
            var podium = MatchLogic.Podium(players);
            Assert.AreEqual(2, podium.Count);
            Assert.AreEqual("loser", podium[0].PlayerId);
        }

        [Test]
        public void Podium_DefaultMockMatchPutsAnEnemyThird()
        {
            var result = Fresh().MatchResults.Build(MatchOutcome.Victory);
            var podium = MatchLogic.Podium(result.Players);
            // Joueur local du mock : le vrai pseudo de la session
            Assert.IsTrue(podium[0].IsLocal);
            Assert.AreEqual(SessionService.DisplayName, podium[0].DisplayName);
            Assert.AreEqual("Necro_ciclone", podium[1].DisplayName);
            Assert.AreEqual("EoS_Keegardhard", podium[2].DisplayName);
            Assert.AreEqual(Team.Enemy, podium[2].Team);
        }

        // ---------- Connecteurs ----------

        [TestCase(TechState.Researched, TechState.Researched, ConnectorState.Done)]
        [TestCase(TechState.Researched, TechState.Available, ConnectorState.Reached)]
        [TestCase(TechState.Researched, TechState.Locked, ConnectorState.Reached)]
        [TestCase(TechState.Available, TechState.Researched, ConnectorState.Idle)]
        [TestCase(TechState.Available, TechState.Available, ConnectorState.Idle)]
        [TestCase(TechState.Available, TechState.Locked, ConnectorState.Idle)]
        [TestCase(TechState.Locked, TechState.Researched, ConnectorState.Idle)]
        [TestCase(TechState.Locked, TechState.Available, ConnectorState.Idle)]
        [TestCase(TechState.Locked, TechState.Locked, ConnectorState.Idle)]
        public void Connector_StateForEveryNeighbourCombination(TechState left, TechState right, ConnectorState expected)
        {
            Assert.AreEqual(expected, MatchLogic.Connector(left, right));
        }

        // ---------- Portefeuille ----------

        [Test]
        public void Store_BuyDebitsTheWalletAndRaisesOwned()
        {
            var mock = Fresh();
            int before = mock.Wallet.Credits;
            var item = mock.Store.Find("plasma-launcher");
            mock.Store.Buy("plasma-launcher", 2).Wait();
            Assert.AreEqual(before - item.PriceCredits * 2, mock.Wallet.Credits);
            Assert.AreEqual(item.Owned + 2, mock.Store.Find("plasma-launcher").Owned);
        }

        [Test]
        public void Store_BuyWithoutEnoughCreditsLeavesTheWalletAlone()
        {
            var mock = Fresh();
            mock.Wallet.TryDebitCredits(mock.Wallet.Credits - 10);
            int before = mock.Wallet.Credits;
            var ex = Assert.Throws<System.AggregateException>(() => mock.Store.Buy("rail-cannon", 1).Wait());
            Assert.IsInstanceOf<InsufficientFundsException>(ex.InnerException);
            Assert.AreEqual(before, mock.Wallet.Credits);
        }

        [Test]
        public void Store_ItemThatNeedsResearchCannotBeBought()
        {
            var mock = Fresh();
            int before = mock.Wallet.Credits;
            Assert.Throws<System.AggregateException>(() => mock.Store.Buy("ion-distorter", 1).Wait());
            Assert.AreEqual(before, mock.Wallet.Credits);
        }

        [Test]
        public void Research_DebitsTechPointsUnlocksTheNextNodeAndStocksTheStore()
        {
            var mock = Fresh();
            int before = mock.Wallet.TechPoints;
            Assert.IsTrue(mock.Store.Find("shield-module").RequiresResearch);

            mock.TechTree.Research("shield-module").Wait();

            var nodes = mock.TechTree.Nodes().Result.ToDictionary(n => n.Id);
            Assert.AreEqual(TechState.Researched, nodes["shield-module"].State);
            Assert.AreEqual(TechState.Available, nodes["t-repair-module"].State);
            Assert.AreEqual(before - nodes["shield-module"].CostTechPoints, mock.Wallet.TechPoints);
            Assert.IsFalse(mock.Store.Find("shield-module").RequiresResearch);
            // Le connecteur radar -> shield passe a accent, shield -> repair a border-strong
            Assert.AreEqual(ConnectorState.Done, MatchLogic.Connector(nodes["radar"].State, nodes["shield-module"].State));
            Assert.AreEqual(ConnectorState.Reached, MatchLogic.Connector(nodes["shield-module"].State, nodes["t-repair-module"].State));
        }

        [Test]
        public void Research_LockedNodeIsRefusedWithoutDebit()
        {
            var mock = Fresh();
            int before = mock.Wallet.TechPoints;
            Assert.Throws<System.AggregateException>(() => mock.TechTree.Research("ion-distorter").Wait());
            Assert.AreEqual(before, mock.Wallet.TechPoints);
        }

        [Test]
        public void Research_WithoutEnoughTechPointsIsRefusedWithoutDebit()
        {
            var mock = Fresh();
            mock.Wallet.TryDebitTechPoints(mock.Wallet.TechPoints - 100);
            var ex = Assert.Throws<System.AggregateException>(() => mock.TechTree.Research("rail-cannon").Wait());
            Assert.IsInstanceOf<InsufficientFundsException>(ex.InnerException);
            Assert.AreEqual(100, mock.Wallet.TechPoints);
        }

        // ---------- Formats ----------

        [Test]
        public void Numbers_UseAThinSpaceForThousands()
        {
            Assert.AreEqual("18" + BlockforgeTheme.ThinSpace + "420", BlockforgeTheme.Num(18420));
            Assert.AreEqual("+1" + BlockforgeTheme.ThinSpace + "240", BlockforgeTheme.Signed(1240));
            Assert.AreEqual("9:12", BlockforgeTheme.Clock(System.TimeSpan.FromSeconds(552)));
        }
    }
}
