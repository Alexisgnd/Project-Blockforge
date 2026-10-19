using System;
using UnityEngine;

// =========================================================
// FORGE LOADER : GEOMETRIE ET TIMELINE (pur, sans UI, teste)
// Le mark Blockforge : trois bras bleus autour d'un hexagone
// or, dans une boite de 300 x 326 unites (coordonnees SVG, y
// vers le bas). Toutes les positions et decalages ci-dessous
// sont dans ces unites ; ForgeLoader les met a l'echelle de
// sa taille (Size / 326).
//
// Un cycle dure 2 600 ms, une seule timeline, ease-out
// cubic-bezier(.2, .8, .2, 1) partout :
//   0-470      les bras glissent depuis l'exterieur et passent
//              de 0 a 1 d'opacite (310 ms chacun, decales de
//              0 / 80 / 160 ms) ; coeur a 55 %, or sombre, 90 %
//   470-680    VERROUILLAGE : le coeur claque a 112 % en or vif
//              (470-540) puis se pose a 100 % en or (540-680) ;
//              un anneau au pivot passe de .6 a 1.9, de 90 % a 0
//              d'opacite, trait de 6 a 1
//   680-1920   immobile ; le coeur respire une fois :
//              or -> #FFE58A a 1 560 ms -> or a 1 920 ms
//   1920-2290  les bras repartent de 30 % vers l'exterieur en
//              s'effacant ; le coeur descend a 70 %, s'assombrit
//              et s'efface
//   2290-2600  temps vide, puis boucle
// La boucle ne porte jamais la progression.
// Animations reduites : aucun deplacement, mark assemble, le
// coeur respire entre les deux ors sur une boucle de 2,4 s.
// =========================================================

public static class ForgeMarkGeometry
{
    public const float BoxWidth = 300f;
    public const float BoxHeight = 326f;
    public static readonly Vector2 Pivot = new(150f, 158f);

    public static readonly Vector2[] LeftArm =
    {
        new(128, 0), new(0, 74), new(0, 211), new(55, 179), new(55, 106), new(128, 64),
    };

    public static readonly Vector2[] RightArm =
    {
        new(172, 0), new(300, 74), new(300, 211), new(245, 179), new(245, 106), new(172, 64),
    };

    public static readonly Vector2[] BottomArm =
    {
        new(77, 218), new(20, 251), new(150, 326), new(280, 251), new(223, 218), new(150, 260),
    };

    public static readonly Vector2[] Core =
    {
        new(150, 94), new(206, 126), new(206, 190), new(150, 222), new(94, 190), new(94, 126),
    };

    // Rayon de base de l'anneau : la demi-hauteur de l'hexagone (222 - 158)
    public const float RingRadius = 64f;

    // Point de depart de chaque bras, le long de son axe vers l'exterieur
    public static readonly Vector2 LeftFrom = new(-70f, -40f);
    public static readonly Vector2 RightFrom = new(70f, -40f);
    public static readonly Vector2 BottomFrom = new(0f, 80f);
}

// Couleurs du logo (lues dans LoaderTheme.uss par ForgeLoader)
public struct ForgePalette
{
    public Color arm, core, dim, hot, breath;
}

public struct ArmPose
{
    public Vector2 offset; // unites de la boite
    public float alpha;
}

public struct MarkPose
{
    public ArmPose left, right, bottom;
    public float coreScale, coreAlpha;
    public Color coreColor;
    public bool ringVisible;
    public float ringScale, ringAlpha, ringStroke; // trait en unites, a l'echelle 1
    public float wordmark;     // 0..1 : opacite du wordmark (splash)
    public float wordmarkRise; // 0..1 : part de la montee restante (1 = 6 px plus bas)
}

public enum FinishMode
{
    Forward, // avancer jusqu'a la fin du verrouillage (680 ms)
    Hold,    // deja verrouille (respiration) : tenir tout de suite
    Reverse, // en train de se defaire : revenir a 1 920 ms
    Wrap,    // temps vide (mark invisible) : repartir de 0 jusqu'a 680 ms
}

public readonly struct FinishPlan
{
    public readonly FinishMode mode;
    public readonly float targetMs;
    public readonly float distanceMs; // temps de timeline avant le verrouillage

    public FinishPlan(FinishMode mode, float targetMs, float distanceMs)
    {
        this.mode = mode;
        this.targetMs = targetMs;
        this.distanceMs = distanceMs;
    }
}

public static class ForgeTimeline
{
    public const float CycleMs = 2600f;
    public const float ArmSlideMs = 310f;
    public static readonly float[] ArmStaggerMs = { 0f, 80f, 160f }; // gauche, droite, bas
    public const float LockStartMs = 470f;
    public const float SnapEndMs = 540f;
    public const float LockEndMs = 680f;
    public const float BreathPeakMs = 1560f;
    public const float ExitStartMs = 1920f;
    public const float ExitEndMs = 2290f;
    public const float ReducedBreathMs = 2400f;

    public const float CoreInScale = 0.55f;
    public const float CoreInAlpha = 0.9f;
    public const float CoreSnapScale = 1.12f;
    public const float CoreOutScale = 0.7f;
    public const float ExitDrift = 0.3f;
    public const float RingFromScale = 0.6f, RingToScale = 1.9f;
    public const float RingFromAlpha = 0.9f;
    public const float RingFromStroke = 6f, RingToStroke = 1f;

    // Courbe utilisee (ForgeLoaderHost la remplace par l'AnimationCurve de LoaderTheme)
    public static Func<float, float> Ease = ForgeEase.Evaluate;

    private static float Phase(float ms, float from, float to) => Ease(Mathf.Clamp01((ms - from) / (to - from)));

    // Pose du mark a l'instant ms du cycle (ms ramene dans [0, 2600[)
    public static MarkPose Evaluate(float ms, in ForgePalette pal)
    {
        ms = Mathf.Repeat(ms, CycleMs);
        var pose = new MarkPose
        {
            left = Arm(ms, ForgeMarkGeometry.LeftFrom, ArmStaggerMs[0]),
            right = Arm(ms, ForgeMarkGeometry.RightFrom, ArmStaggerMs[1]),
            bottom = Arm(ms, ForgeMarkGeometry.BottomFrom, ArmStaggerMs[2]),
        };

        // ----- Coeur -----
        if (ms < LockStartMs)
        {
            pose.coreScale = CoreInScale;
            pose.coreAlpha = CoreInAlpha;
            pose.coreColor = pal.dim;
        }
        else if (ms < SnapEndMs)
        {
            float k = Phase(ms, LockStartMs, SnapEndMs);
            pose.coreScale = Mathf.LerpUnclamped(CoreInScale, CoreSnapScale, k);
            pose.coreAlpha = Mathf.Lerp(CoreInAlpha, 1f, k);
            pose.coreColor = Color.Lerp(pal.dim, pal.hot, k);
        }
        else if (ms < LockEndMs)
        {
            float k = Phase(ms, SnapEndMs, LockEndMs);
            pose.coreScale = Mathf.LerpUnclamped(CoreSnapScale, 1f, k);
            pose.coreAlpha = 1f;
            pose.coreColor = Color.Lerp(pal.hot, pal.core, k);
        }
        else if (ms < ExitStartMs)
        {
            pose.coreScale = 1f;
            pose.coreAlpha = 1f;
            pose.coreColor = ms < BreathPeakMs
                ? Color.Lerp(pal.core, pal.breath, Phase(ms, LockEndMs, BreathPeakMs))
                : Color.Lerp(pal.breath, pal.core, Phase(ms, BreathPeakMs, ExitStartMs));
        }
        else if (ms < ExitEndMs)
        {
            float k = Phase(ms, ExitStartMs, ExitEndMs);
            pose.coreScale = Mathf.Lerp(1f, CoreOutScale, k);
            pose.coreAlpha = 1f - k;
            pose.coreColor = Color.Lerp(pal.core, pal.dim, k);
        }
        else
        {
            pose.coreScale = CoreOutScale;
            pose.coreAlpha = 0f;
            pose.coreColor = pal.dim;
        }

        // ----- Anneau : une seule fois par cycle, pendant le verrouillage -----
        if (ms >= LockStartMs && ms < LockEndMs)
        {
            float k = Phase(ms, LockStartMs, LockEndMs);
            pose.ringVisible = true;
            pose.ringScale = Mathf.Lerp(RingFromScale, RingToScale, k);
            pose.ringAlpha = Mathf.Lerp(RingFromAlpha, 0f, k);
            pose.ringStroke = Mathf.Lerp(RingFromStroke, RingToStroke, k);
        }

        // ----- Wordmark (splash) : monte et apparait au verrouillage, s'efface en fin de cycle -----
        if (ms < LockStartMs)
        {
            pose.wordmark = 0f;
            pose.wordmarkRise = 1f;
        }
        else if (ms < LockEndMs)
        {
            float k = Phase(ms, LockStartMs, LockEndMs);
            pose.wordmark = k;
            pose.wordmarkRise = 1f - k;
        }
        else if (ms < ExitStartMs)
        {
            pose.wordmark = 1f;
        }
        else if (ms < ExitEndMs)
        {
            pose.wordmark = 1f - Phase(ms, ExitStartMs, ExitEndMs);
        }
        return pose;
    }

    private static ArmPose Arm(float ms, Vector2 from, float stagger)
    {
        float start = stagger, end = stagger + ArmSlideMs;
        if (ms < LockStartMs)
        {
            if (ms < start)
                return new ArmPose { offset = from, alpha = 0f };
            float k = Phase(ms, start, end);
            return new ArmPose { offset = Vector2.LerpUnclamped(from, Vector2.zero, k), alpha = k };
        }
        if (ms < ExitStartMs)
            return new ArmPose { offset = Vector2.zero, alpha = 1f };
        if (ms < ExitEndMs)
        {
            float k = Phase(ms, ExitStartMs, ExitEndMs);
            return new ArmPose { offset = from * (ExitDrift * k), alpha = 1f - k };
        }
        return new ArmPose { offset = from * ExitDrift, alpha = 0f };
    }

    // Pose tenue quand un chargement se termine : mark assemble, coeur or a 100 %
    public static MarkPose Locked(in ForgePalette pal)
    {
        var pose = Evaluate(LockEndMs, pal);
        pose.ringVisible = false;
        pose.coreColor = pal.core;
        pose.wordmark = 1f;
        pose.wordmarkRise = 0f;
        return pose;
    }

    // Animations reduites : aucun deplacement ; le coeur respire or <-> #FFE58A sur 2,4 s
    public static MarkPose Reduced(float ms, in ForgePalette pal)
    {
        var pose = Locked(pal);
        float p = Mathf.Repeat(ms, ReducedBreathMs) / ReducedBreathMs;
        float tri = p < 0.5f ? p * 2f : (1f - p) * 2f;
        pose.coreColor = Color.Lerp(pal.core, pal.breath, Ease(tri));
        return pose;
    }

    // Fin de chargement au milieu d'un cycle : chemin le plus court vers l'etat
    // verrouille, sans jamais couper sur un mark a moitie assemble (<= 680 ms)
    public static FinishPlan PlanFinish(float ms)
    {
        ms = Mathf.Repeat(ms, CycleMs);
        if (ms < LockEndMs)
            return new FinishPlan(FinishMode.Forward, LockEndMs, LockEndMs - ms);
        if (ms < ExitStartMs)
            return new FinishPlan(FinishMode.Hold, ms, 0f);
        if (ms < ExitEndMs)
            return new FinishPlan(FinishMode.Reverse, ExitStartMs, ms - ExitStartMs);
        return new FinishPlan(FinishMode.Wrap, LockEndMs, LockEndMs);
    }
}
