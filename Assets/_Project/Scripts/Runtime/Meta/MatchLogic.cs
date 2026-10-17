using System.Collections.Generic;

// =========================================================
// REGLES DE FIN DE MATCH (pures, testees)
// - Awards : meilleur de chaque colonne ; egalite -> le joueur
//   au score le plus haut ; valeur 0 -> personne (la carte
//   affiche un tiret). Calcules cote serveur dans le vrai jeu,
//   ici pour le mock.
// - Podium : les 3 meilleurs scores des DEUX equipes (un joueur
//   de l'equipe perdante peut y figurer).
// - Connecteurs de l'arbre : accent si les deux bouts sont
//   recherches, border-strong si seul le bout gauche l'est,
//   border-subtle sinon.
// =========================================================

namespace Blockforge.Meta
{
    public enum ConnectorState { Idle, Reached, Done }

    public static class MatchLogic
    {
        public static List<Award> ComputeAwards(IReadOnlyList<PlayerMatchStats> players)
        {
            var awards = new List<Award>(4);
            foreach (AwardKind kind in System.Enum.GetValues(typeof(AwardKind)))
            {
                PlayerMatchStats best = null;
                int bestValue = 0;
                foreach (var p in players)
                {
                    int v = ValueOf(p, kind);
                    if (v <= 0)
                        continue;
                    if (best == null || v > bestValue || (v == bestValue && p.Score > best.Score))
                    {
                        best = p;
                        bestValue = v;
                    }
                }
                awards.Add(new Award(kind, best?.PlayerId, bestValue));
            }
            return awards;
        }

        public static int ValueOf(PlayerMatchStats p, AwardKind kind) => kind switch
        {
            AwardKind.BestDamage => p.Damage,
            AwardKind.BestHealing => p.Healing,
            AwardKind.BestKills => p.Kills,
            _ => p.Assists,
        };

        // Tri par score decroissant ; a score egal, les degats departagent, puis le nom (stable)
        public static List<PlayerMatchStats> SortedByScore(IEnumerable<PlayerMatchStats> players)
        {
            var list = new List<PlayerMatchStats>(players);
            list.Sort((a, b) =>
            {
                int c = b.Score.CompareTo(a.Score);
                if (c != 0) return c;
                c = b.Damage.CompareTo(a.Damage);
                return c != 0 ? c : string.CompareOrdinal(a.DisplayName, b.DisplayName);
            });
            return list;
        }

        public static List<PlayerMatchStats> Podium(IReadOnlyList<PlayerMatchStats> players)
        {
            var sorted = SortedByScore(players);
            if (sorted.Count > 3)
                sorted.RemoveRange(3, sorted.Count - 3);
            return sorted;
        }

        public static List<PlayerMatchStats> Team(IReadOnlyList<PlayerMatchStats> players, Team team)
        {
            var list = new List<PlayerMatchStats>();
            foreach (var p in players)
                if (p.Team == team) list.Add(p);
            return SortedByScore(list);
        }

        public static Team Winner(MatchResult result) => result.Outcome == MatchOutcome.Defeat ? Meta.Team.Enemy : Meta.Team.Ally;

        public static ConnectorState Connector(TechState left, TechState right)
        {
            if (left != TechState.Researched)
                return ConnectorState.Idle;
            return right == TechState.Researched ? ConnectorState.Done : ConnectorState.Reached;
        }
    }
}
