using UnityEngine;

// =========================================================
// COURBE DU FORGE LOADER : cubic-bezier(.2, .8, .2, 1)
// Toutes les courbes du loader sont cette ease-out. Deux formes :
// - Evaluate : la bezier exacte (resolution de x(s) = t par
//   Newton puis dichotomie), reference des tests ;
// - CreateCurve : sa replique en AnimationCurve (cles placees le
//   long du parametre de la bezier, tangentes = derivee exacte),
//   editable dans LoaderTheme et utilisee a l'execution.
// =========================================================

public static class ForgeEase
{
    public const float X1 = 0.2f, Y1 = 0.8f, X2 = 0.2f, Y2 = 1f;

    // Bezier exacte : y pour une abscisse t dans [0, 1]
    public static float Evaluate(float t)
    {
        if (t <= 0f) return 0f;
        if (t >= 1f) return 1f;
        return BezierY(SolveS(t));
    }

    // Replique en AnimationCurve (ecart < 0.005 avec 24 cles, verifie par les tests)
    public static AnimationCurve CreateCurve(int keys = 24)
    {
        keys = Mathf.Max(4, keys);
        var frames = new Keyframe[keys + 1];
        for (int i = 0; i <= keys; i++)
        {
            // Cles reparties sur le parametre : plus serrees en x au debut, la ou la courbe monte vite
            float s = (float)i / keys;
            float x = BezierX(s), y = BezierY(s);
            float slope = Slope(s);
            frames[i] = new Keyframe(x, y, slope, slope);
        }
        frames[0] = new Keyframe(0f, 0f, Y1 / X1, Y1 / X1);
        frames[keys] = new Keyframe(1f, 1f, 0f, 0f);
        var curve = new AnimationCurve(frames)
        {
            preWrapMode = WrapMode.Clamp,
            postWrapMode = WrapMode.Clamp,
        };
        return curve;
    }

    private static float BezierX(float s) => Cubic(s, X1, X2);
    private static float BezierY(float s) => Cubic(s, Y1, Y2);

    private static float Cubic(float s, float p1, float p2)
    {
        float u = 1f - s;
        return 3f * u * u * s * p1 + 3f * u * s * s * p2 + s * s * s;
    }

    private static float CubicDerivative(float s, float p1, float p2)
    {
        float u = 1f - s;
        return 3f * u * u * p1 + 6f * u * s * (p2 - p1) + 3f * s * s * (1f - p2);
    }

    // dy/dx au parametre s
    private static float Slope(float s)
    {
        float dx = CubicDerivative(s, X1, X2);
        float dy = CubicDerivative(s, Y1, Y2);
        return dx > 1e-6f ? dy / dx : 0f;
    }

    private static float SolveS(float x)
    {
        float s = x;
        for (int i = 0; i < 8; i++)
        {
            float err = BezierX(s) - x;
            if (Mathf.Abs(err) < 1e-6f)
                return s;
            float d = CubicDerivative(s, X1, X2);
            if (Mathf.Abs(d) < 1e-6f)
                break;
            s -= err / d;
        }

        float lo = 0f, hi = 1f;
        s = x;
        for (int i = 0; i < 32; i++)
        {
            float v = BezierX(s);
            if (Mathf.Abs(v - x) < 1e-6f)
                break;
            if (v < x) lo = s; else hi = s;
            s = 0.5f * (lo + hi);
        }
        return s;
    }
}
