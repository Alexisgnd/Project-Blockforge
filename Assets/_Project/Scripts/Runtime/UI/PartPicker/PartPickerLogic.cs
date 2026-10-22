using System;
using System.Collections.Generic;
using System.Linq;

// =========================================================
// PART PICKER : REGLES PURES (sans UI, testees en PlayMode)
// - Matches : recherche par nom, insensible a la casse, sur les
//   pieces possedees uniquement ;
// - Sort : Recent (dernieres utilisees de la session, puis les
//   plus recemment acquises), A – Z, By CPU (croissant),
//   By damage (decroissant ; pieces sans degats a la fin) ;
// - Suggest : mot le plus proche (distance d'edition) parmi les
//   mots des noms et des familles possedes, pour « Did you mean ».
// =========================================================

public static class PartPickerLogic
{
    public static bool Matches(PartDefinition part, string query)
    {
        if (part == null)
            return false;
        if (string.IsNullOrWhiteSpace(query))
            return true;
        return (part.name ?? "").IndexOf(query.Trim(), StringComparison.OrdinalIgnoreCase) >= 0;
    }

    public static List<PartDefinition> Search(IEnumerable<PartDefinition> owned, string query)
    {
        string q = (query ?? "").Trim();
        var result = owned.Where(p => Matches(p, q)).ToList();
        // Les noms qui commencent par la recherche d'abord, puis l'ordre alphabetique
        result.Sort((a, b) =>
        {
            bool sa = (a.name ?? "").StartsWith(q, StringComparison.OrdinalIgnoreCase);
            bool sb = (b.name ?? "").StartsWith(q, StringComparison.OrdinalIgnoreCase);
            if (sa != sb)
                return sa ? -1 : 1;
            return string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
        });
        return result;
    }

    // recent : ids des pieces utilisees pendant la session, la plus recente d'abord.
    // ownedOrder : rang d'acquisition (0 = la plus ancienne).
    public static void Sort(List<PartDefinition> parts, PickerSort sort, IList<string> recent,
                            IReadOnlyDictionary<string, int> ownedOrder)
    {
        switch (sort)
        {
            case PickerSort.Alphabetical:
                parts.Sort((a, b) => string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase));
                break;
            case PickerSort.ByCpu:
                parts.Sort((a, b) =>
                {
                    int c = a.cpuCost.CompareTo(b.cpuCost);
                    return c != 0 ? c : string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
                });
                break;
            case PickerSort.ByDamage:
                parts.Sort((a, b) =>
                {
                    float da = a.TryGetStat("damage", out var sa) ? sa.value : float.NegativeInfinity;
                    float db = b.TryGetStat("damage", out var sb) ? sb.value : float.NegativeInfinity;
                    int c = db.CompareTo(da);
                    return c != 0 ? c : string.Compare(a.name, b.name, StringComparison.OrdinalIgnoreCase);
                });
                break;
            default:
                parts.Sort((a, b) =>
                {
                    int ra = recent != null ? recent.IndexOf(a.id) : -1;
                    int rb = recent != null ? recent.IndexOf(b.id) : -1;
                    if (ra >= 0 || rb >= 0)
                    {
                        if (ra < 0) return 1;
                        if (rb < 0) return -1;
                        return ra.CompareTo(rb);
                    }
                    int oa = ownedOrder != null && ownedOrder.TryGetValue(a.id, out var x) ? x : -1;
                    int ob = ownedOrder != null && ownedOrder.TryGetValue(b.id, out var y) ? y : -1;
                    return ob.CompareTo(oa); // acquisition la plus recente d'abord
                });
                break;
        }
    }

    // Mot propose pour une recherche sans resultat, ou null
    public static string Suggest(IEnumerable<PartDefinition> owned, string query, float tolerance)
    {
        string q = (query ?? "").Trim().ToLowerInvariant();
        if (q.Length < 2)
            return null;

        var words = new HashSet<string>();
        foreach (var p in owned)
        {
            if (p == null)
                continue;
            AddWords(words, p.name);
            AddWords(words, p.family);
        }

        int limit = Math.Max(1, (int)Math.Round(q.Length * tolerance));
        string best = null;
        int bestDistance = int.MaxValue;
        foreach (var w in words)
        {
            if (w == q)
                continue;
            int d = Distance(q, w);
            // A egalite, le mot de longueur la plus proche puis l'ordre alphabetique
            if (d < bestDistance || (d == bestDistance && best != null &&
                                     (Math.Abs(w.Length - q.Length) < Math.Abs(best.Length - q.Length) ||
                                      (Math.Abs(w.Length - q.Length) == Math.Abs(best.Length - q.Length) && string.CompareOrdinal(w, best) < 0))))
            {
                best = w;
                bestDistance = d;
            }
        }
        return bestDistance <= limit ? best : null;
    }

    private static void AddWords(HashSet<string> words, string text)
    {
        if (string.IsNullOrEmpty(text))
            return;
        foreach (var raw in text.Split(new[] { ' ', '-', '_', '/', '·' }, StringSplitOptions.RemoveEmptyEntries))
        {
            string w = new string(raw.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
            if (w.Length >= 2)
                words.Add(w);
        }
    }

    // Distance de Levenshtein (insertion, suppression, substitution)
    public static int Distance(string a, string b)
    {
        if (a == b) return 0;
        if (a.Length == 0) return b.Length;
        if (b.Length == 0) return a.Length;
        var prev = new int[b.Length + 1];
        var cur = new int[b.Length + 1];
        for (int j = 0; j <= b.Length; j++)
            prev[j] = j;
        for (int i = 1; i <= a.Length; i++)
        {
            cur[0] = i;
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                cur[j] = Math.Min(Math.Min(cur[j - 1] + 1, prev[j] + 1), prev[j - 1] + cost);
            }
            (prev, cur) = (cur, prev);
        }
        return prev[b.Length];
    }

    // Budget de la piece dans sa reserve
    public static bool Fits(PartDefinition part, IRobotBudget budget, out int used, out int max)
    {
        used = budget != null ? budget.Used(part.cpuPool) : 0;
        max = budget != null ? budget.Max(part.cpuPool) : int.MaxValue;
        return budget == null || used + part.cpuCost <= max;
    }
}
