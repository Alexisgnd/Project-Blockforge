using System.Collections.Generic;
using System.Text;
using UnityEngine;

// =========================================================
// BILAN DE NAVIGATION D'UN BOT (sur tout le match, reapparitions
// comprises) — rempli par BotBrain, lu par TdmMatch a la fin :
// - temps ou il voulait avancer, et part de ce temps passee
//   immobile (< 1,2 m/s) ; l'endroit (case de 10 m) ou il a le
//   plus perdu de temps
// - deblocages par type : marche arriere, coup en avant,
//   soulevement (RobotRecovery), degagement d'un autre robot,
//   redressement (retourne), point de secours du NavMesh
// - crevasses (entrees) et temps sur un passage etroit
// - positions des soulevements (10 au plus)
// - repartition (derniere colonne) : temps en renfort (rejoint
//   l'allie le plus proche), temps en soutien (et nombre de
//   departs), sorties d'amas, changements de role / couloir
// Sortie : console [Bot] (donc Library/BlockforgeConsole.log) et,
// dans l'editeur, Library/BotNavReport.txt.
// =========================================================

public class BotNavStats
{
    public float wantedToMove, stalled, narrow, inPit;
    public float stalledSlope, stalledTilt;   // sommes ponderees par le temps (pente du sol, inclinaison du robot)
    public int reverses, forwards, lifts, contacts, flips, escapes, pits, jumps;
    public float reinforce, support;            // repartition : secondes en renfort / en soutien
    public int supports, declumps, roleChanges; // departs en soutien, sorties d'amas, changements de role
    public readonly List<Vector3> liftSpots = new();
    private readonly Dictionary<Vector3Int, float> stallSpots = new();

    public void Stall(Vector3 at, float dt, float groundSlope, float tilt)
    {
        stalled += dt;
        stalledSlope += groundSlope * dt;
        stalledTilt += tilt * dt;
        var cell = new Vector3Int(Mathf.RoundToInt(at.x / 10f), 0, Mathf.RoundToInt(at.z / 10f));
        stallSpots[cell] = (stallSpots.TryGetValue(cell, out float s) ? s : 0f) + dt;
    }

    public void Lift(Vector3 at)
    {
        lifts++;
        if (liftSpots.Count < 10)
            liftSpots.Add(at);
    }

    public bool WorstSpot(out Vector3 at, out float seconds)
    {
        at = Vector3.zero;
        seconds = 0f;
        foreach (var kv in stallSpots)
        {
            if (kv.Value > seconds)
            {
                seconds = kv.Value;
                at = new Vector3(kv.Key.x * 10f, 0f, kv.Key.z * 10f);
            }
        }
        return seconds > 0f;
    }

    public float StalledShare => wantedToMove > 0.5f ? stalled / wantedToMove : 0f;

    public static string Spot(Vector3 v) => $"({v.x:0}, {v.y:0}, {v.z:0})";

    public static string Report(IEnumerable<TdmParticipant> participants, float matchSeconds)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"[Bot] Bilan navigation ({matchSeconds:0} s) : bot | robot | voulait avancer | immobile (part) | pente du sol / inclinaison quand immobile | recul / avant / soulevement / contact / retourne / secours | sauts | crevasses | passage etroit | pire endroit | renfort / soutien (departs) / amas / roles");
        float totalStalled = 0f;
        int totalLifts = 0;
        foreach (var p in participants)
        {
            if (!p.IsBot || p.Nav == null)
                continue;
            var n = p.Nav;
            totalStalled += n.stalled;
            totalLifts += n.lifts;
            string worst = n.WorstSpot(out var at, out float sec) ? $"{Spot(at)} {sec:0} s" : "-";
            string slopes = n.stalled > 1f ? $"{n.stalledSlope / n.stalled,3:0} deg / {n.stalledTilt / n.stalled,3:0} deg" : "      -      ";
            sb.AppendLine($"  {p.Name,-14} | {p.RobotName,-14} | {n.wantedToMove,5:0} s | {n.stalled,4:0} s ({n.StalledShare:P0}) | {slopes} | " +
                          $"{n.reverses,2} / {n.forwards,2} / {n.lifts,2} / {n.contacts,2} / {n.flips,2} / {n.escapes,2} | {n.jumps,3} | {n.pits,2} ({n.inPit:0} s) | {n.narrow,4:0} s | {worst} | " +
                          $"{n.reinforce,4:0} s / {n.support,4:0} s ({n.supports,2}) / {n.declumps,2} / {n.roleChanges,2}");
            if (n.liftSpots.Count > 0)
            {
                sb.Append("      soulevements : ");
                for (int i = 0; i < n.liftSpots.Count; i++)
                    sb.Append(i > 0 ? ", " : "").Append(Spot(n.liftSpots[i]));
                sb.AppendLine();
            }
        }
        sb.Append($"  total : {totalStalled:0} s immobiles en voulant avancer, {totalLifts} soulevement(s).");
        return sb.ToString();
    }
}
