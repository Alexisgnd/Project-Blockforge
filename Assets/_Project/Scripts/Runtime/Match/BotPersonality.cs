using UnityEngine;

// =========================================================
// PERSONNALITE ET NIVEAU D'UN BOT (varie pendant la partie)
// Chaque bot tire un style au debut du match :
// - Cautious    : garde ses distances, recule tot pour se
//                 regenerer, reste pres de ses allies
// - Balanced    : se bat a la portee de son arme
// - Aggressive  : fonce, recule tard, longues rafales
// - Opportunist : chasse les robots abimes et va les achever,
//                 meme loin ; recule s'il est lui-meme a terre
// Le niveau (Skill 0..1 : precision, reflexes, anticipation)
// et l'agressivite (0..1 : distance, recul, rafales) bougent
// pendant le match :
// - une vague lente propre a chaque bot (periode 70-140 s,
//   +/- 0,15) : il a des temps forts et des passages a vide ;
// - l'humeur : un kill la monte, une mort la baisse, elle
//   revient doucement a zero ;
// - le score : une equipe menee de 5 kills ou plus devient plus
//   agressive, une equipe tres en avance se relache un peu.
// Le style lui-meme change en cours de partie (StyleChanged) :
// - toutes les 2 a 4 min (2 a 3 fois par match), le bot en tire un autre, pousse par
//   le score (mene de 3 : Aggressive / Opportunist ; en avance
//   de 3 : Cautious / Balanced) ;
// - deux morts de suite : il passe Cautious ; deux kills de
//   suite : Aggressive ou Opportunist.
// BotBrain lit les valeurs derivees (portee voulue, seuil de
// recul, temps de reaction, erreur de visee, rafales).
// =========================================================

public enum BotStyle { Cautious, Balanced, Aggressive, Opportunist }

public sealed class BotPersonality
{
    public BotStyle Style { get; private set; }
    public float BaseSkill { get; private set; }
    public float Skill { get; private set; }
    public float Aggression { get; private set; }

    private float baseAggression;
    private float wavePeriod, wavePhase;
    private float mood;   // -0.3 .. +0.3
    private System.Random rng;
    private float nextShift = -1f;
    private int streak;   // > 0 kills de suite, < 0 morts de suite
    private int lastLead;

    public event System.Action<BotStyle, BotStyle> StyleChanged;

    public static BotPersonality Create(BotStyle style, System.Random rng)
    {
        var p = new BotPersonality
        {
            Style = style,
            rng = rng,
            BaseSkill = 0.35f + 0.45f * (float)rng.NextDouble(),
            wavePeriod = 70f + 70f * (float)rng.NextDouble(),
            wavePhase = (float)rng.NextDouble() * Mathf.PI * 2f,
            baseAggression = BaseAggressionOf(style),
        };
        p.Skill = p.BaseSkill;
        p.Aggression = p.baseAggression;
        return p;
    }

    private static float BaseAggressionOf(BotStyle style) => style switch
    {
        BotStyle.Cautious => 0.2f,
        BotStyle.Balanced => 0.45f,
        BotStyle.Aggressive => 0.8f,
        _ => 0.6f,
    };

    // Changement de style : jamais le meme, pousse par le score (teamLead)
    private void Shift(float time, int teamLead, string reason)
    {
        var weights = new float[4];
        for (int i = 0; i < 4; i++)
            weights[i] = 1f;
        if (teamLead <= -3) { weights[(int)BotStyle.Aggressive] += 2f; weights[(int)BotStyle.Opportunist] += 1.5f; }
        else if (teamLead >= 3) { weights[(int)BotStyle.Cautious] += 1.5f; weights[(int)BotStyle.Balanced] += 1.5f; }
        if (reason == "deaths") { weights[(int)BotStyle.Cautious] += 4f; weights[(int)BotStyle.Aggressive] = 0f; }
        if (reason == "kills") { weights[(int)BotStyle.Aggressive] += 3f; weights[(int)BotStyle.Opportunist] += 3f; weights[(int)BotStyle.Cautious] = 0f; }
        weights[(int)Style] = 0f;
        float total = 0f;
        foreach (var w in weights) total += w;
        float r = (float)rng.NextDouble() * total;
        var next = Style;
        for (int i = 0; i < 4; i++)
        {
            r -= weights[i];
            if (r <= 0f) { next = (BotStyle)i; break; }
        }
        var previous = Style;
        Style = next;
        baseAggression = BaseAggressionOf(next);
        streak = 0;
        nextShift = time + 120f + 120f * (float)rng.NextDouble();
        if (next != previous)
            StyleChanged?.Invoke(previous, next);
    }

    // teamLead = kills de son equipe - kills adverses
    public void Tick(float time, float deltaTime, int teamLead)
    {
        lastLead = teamLead;
        if (nextShift < 0f)
            nextShift = time + 90f + 120f * (float)rng.NextDouble();
        else if (time >= nextShift)
            Shift(time, teamLead, "time");
        mood = Mathf.MoveTowards(mood, 0f, 0.008f * deltaTime);
        float wave = 0.15f * Mathf.Sin(time * Mathf.PI * 2f / wavePeriod + wavePhase);
        Skill = Mathf.Clamp(BaseSkill + wave + mood * 0.5f, 0.1f, 1f);
        float pressure = teamLead <= -5 ? 0.2f : teamLead >= 7 ? -0.1f : 0f;
        Aggression = Mathf.Clamp01(baseAggression + mood + pressure + wave * 0.5f);
    }

    public void OnKill()
    {
        mood = Mathf.Min(0.3f, mood + 0.12f);
        streak = streak > 0 ? streak + 1 : 1;
        if (streak >= 2 && Style != BotStyle.Aggressive && Style != BotStyle.Opportunist)
            Shift(Time.time, lastLead, "kills");
    }

    public void OnDeath()
    {
        mood = Mathf.Max(-0.3f, mood - 0.12f);
        streak = streak < 0 ? streak - 1 : -1;
        if (streak <= -2 && Style != BotStyle.Cautious)
            Shift(Time.time, lastLead, "deaths");
    }

    // ---------- Valeurs derivees ----------

    // Vie restante (0..1) sous laquelle le bot decroche pour se regenerer
    public float RetreatAt => Style switch
    {
        BotStyle.Cautious => 0.45f,
        BotStyle.Opportunist => 0.28f,
        BotStyle.Aggressive => 0.1f,
        _ => 0.22f,
    } * (1.25f - 0.5f * Aggression);

    // Poids des cibles abimees dans le choix de la cible
    public float Opportunism => Style switch
    {
        BotStyle.Opportunist => 3f,
        BotStyle.Aggressive => 1.2f,
        BotStyle.Balanced => 1f,
        _ => 0.6f,
    };

    // Multiplie la distance de combat de l'arme : prudent = plus loin
    public float RangeFactor => Mathf.Lerp(1.35f, 0.7f, Aggression);

    // Delai entre "je le vois" et le premier tir (s)
    public float ReactionTime => Mathf.Lerp(0.95f, 0.18f, Skill);

    // Rayon de l'erreur de visee a 50 m (m)
    public float AimError => Mathf.Lerp(2.6f, 0.3f, Skill);

    // Part de la vitesse de la cible anticipee
    public float LeadAccuracy => Mathf.Lerp(0.2f, 0.95f, Skill);

    public float BurstSeconds => Mathf.Lerp(0.6f, 1.6f, Aggression);
    public float PauseSeconds => Mathf.Lerp(0.9f, 0.3f, Aggression);

    // Hauteur de vol au-dessus du sol (robots a rotors)
    public float FlyHeight => Mathf.Lerp(18f, 10f, Aggression);

    public string Describe() => $"{Style} skill {Skill:0.00} aggr {Aggression:0.00}";
}
