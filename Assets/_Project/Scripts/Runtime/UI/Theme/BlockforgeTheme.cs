using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

// =========================================================
// JETONS DU DESIGN SYSTEM BLOCKFORGE
// Source : Branding/blockforge-design-system.zip -> tokens.json
// Classe partielle en trois fichiers :
// - BlockforgeTheme.Tokens.cs : GENERE depuis tokens.json par
//   Blockforge > Design System > Generate Theme Tokens :
//   couleurs dark + hud, Palette par theme, espacements,
//   rayons, controles, opacites, ombres, styles de texte ;
// - BlockforgeTheme.Layout.cs : metriques d'ecran (TopBar,
//   fin de match, resultats, hub), ecrites a la main ;
// - ce fichier : mouvement, reglage « reduire les
//   animations », formatage des nombres, alias historiques.
// Ne jamais ecrire une couleur, un espacement ou une duree
// d'UI en dur dans un ecran : passer par ces jetons.
// Les composants (DSElement), la typo (DSText), les formes
// (DSAssets, HudShapes, HudPolygon) et l'outil
// Blockforge > Apply Design System les lisent.
// L'ecran Boot (UI Toolkit) reprend les memes valeurs dans BootAuth.uss.
// =========================================================

public enum DSTheme { Dark, Hud }

public enum FontFamily { Sans, Condensed, Mono }

// Style de texte du DS : famille, taille, interligne, graisse, interlettrage (em)
public readonly struct TypeSpec
{
    public readonly FontFamily Family;
    public readonly float Size;
    public readonly float LineHeight;
    public readonly int Weight;
    public readonly float LetterSpacingEm;

    public TypeSpec(FontFamily family, float size, float lineHeight, int weight, float letterSpacingEm)
    {
        Family = family;
        Size = size;
        LineHeight = lineHeight;
        Weight = weight;
        LetterSpacingEm = letterSpacingEm;
    }

    // Meme style a une autre taille / graisse (titres des maquettes)
    public TypeSpec With(float size, int weight = 0, float letterSpacingEm = -1f) =>
        new(Family, size, size * LineHeight / Mathf.Max(1f, Size), weight > 0 ? weight : Weight, letterSpacingEm >= 0f ? letterSpacingEm : LetterSpacingEm);
}

// Ombre portee du DS (decalage, flou, couleur avec son alpha)
public readonly struct ShadowSpec
{
    public readonly float OffsetX, OffsetY, Blur;
    public readonly Color Color;

    public ShadowSpec(float offsetX, float offsetY, float blur, Color color, float alpha)
    {
        OffsetX = offsetX;
        OffsetY = offsetY;
        Blur = blur;
        color.a = alpha;
        Color = color;
    }
}

public static partial class BlockforgeTheme
{
    // ----- Mouvement (ease-out) -----
    public const float MotionHover = 0.08f;  // survol et focus
    public const float MotionPanel = 0.14f;  // panneaux, infobulles, notices, fondus
    public const float MotionScreen = 0.22f; // transitions d'ecran

    // Reglage « reduire les animations » : plus aucun deplacement ni mise a
    // l'echelle, seuls des fondus courts subsistent (Fade) ; les durees de
    // mouvement (Duration) tombent a zero.
    private const string ReduceMotionKey = "Blockforge.ReduceMotion";
    public static bool ReduceMotion
    {
        get => PlayerPrefs.GetInt(ReduceMotionKey, 0) == 1;
        set { PlayerPrefs.SetInt(ReduceMotionKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    // Duree d'un deplacement / d'une mise a l'echelle : 0 en animations reduites
    public static float Duration(float seconds) => ReduceMotion ? 0f : seconds;

    // Duree d'un fondu : conservee mais ramenee a 140 ms en animations reduites
    public static float Fade(float seconds) => ReduceMotion ? Mathf.Min(seconds, MotionPanel) : seconds;

    // Courbe ease-out commune (cubique)
    public static float EaseOut(float t)
    {
        t = Mathf.Clamp01(t);
        float inv = 1f - t;
        return 1f - inv * inv * inv;
    }

    // =========================================================
    // NOMBRES : unite toujours presente, milliers groupes par une
    // espace fine (balise TMP : les polices n'ont pas U+2009)
    // =========================================================

    private static readonly CultureInfo En = CultureInfo.InvariantCulture;
    public const string ThinSpace = "<space=0.18em>";

    // 19036 -> "19 036"
    public static string Num(long value)
    {
        string digits = System.Math.Abs(value).ToString(En);
        var sb = new StringBuilder();
        if (value < 0)
            sb.Append('-');
        for (int i = 0; i < digits.Length; i++)
        {
            if (i > 0 && (digits.Length - i) % 3 == 0)
                sb.Append(ThinSpace);
            sb.Append(digits[i]);
        }
        return sb.ToString();
    }

    // 8.46 -> "8.5" (format "0.#"), milliers groupes
    public static string Num(float value, string format = "0.#")
    {
        string text = value.ToString(format, En);
        int dot = text.IndexOf('.');
        string whole = dot >= 0 ? text.Substring(0, dot) : text;
        string fraction = dot >= 0 ? text.Substring(dot) : "";
        return long.TryParse(whole, NumberStyles.AllowLeadingSign, En, out long w) ? Num(w) + fraction : text;
    }

    // "+1 240" / "-35"
    public static string Signed(long value) => value >= 0 ? "+" + Num(value) : Num(value);

    // Valeur + unite : "1 850 CPU", "19 036 kg"
    public static string Qty(long value, string unit) => $"{Num(value)} {unit}";
    public static string Qty(float value, string unit, string format = "0.#") => $"{Num(value, format)} {unit}";

    // Unite en ink-muted (StatLine, readouts)
    public static string QtyMuted(string value, string unit) =>
        $"{value}<space=0.25em><color=#{ColorUtility.ToHtmlStringRGB(InkMuted)}><size=80%>{unit}</size></color>";

    // "9:12" pour 9 min 12 s
    public static string Clock(System.TimeSpan span) => $"{(int)span.TotalMinutes}:{span.Seconds:00}";

    // Texte colore (balise TMP) : la couleur reste un jeton
    public static string Tint(string text, Color color) => $"<color=#{ColorUtility.ToHtmlStringRGBA(color)}>{text}</color>";

    // =========================================================
    // ALIAS DE L'ANCIENNE PALETTE (setups d'UI existants)
    // Proprietes et non champs : l'ordre d'initialisation entre
    // fichiers partiels n'est pas garanti.
    // =========================================================

    public static Color Background => SurfaceBase;
    public static Color Surface => SurfacePanel;
    public static Color SurfaceSunken => SurfaceInset;
    public static Color Border => BorderSubtle;
    public static Color AccentMuted => AccentSoft;
    public static Color OnAccent => InkOnAccent;
    public static Color TextPrimary => InkPrimary;
    public static Color TextSecondary => InkSecondary;
    public static Color TextMuted => InkMuted;
    public static Color TextDisabled => InkMuted;
    public static Color DangerMuted => DangerSoft;
    public static Color DangerText => DangerInk;

    public static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    // Les etats visuels sont portes par DSElement : la transition uGUI ne teinte plus rien.
    public static ColorBlock ButtonStates()
    {
        var block = ColorBlock.defaultColorBlock;
        block.normalColor = block.highlightedColor = block.selectedColor = block.pressedColor = Color.white;
        block.disabledColor = Color.white;
        block.colorMultiplier = 1f;
        block.fadeDuration = MotionHover;
        return block;
    }

    private static Color Hex(int rgb, int alpha = 0xFF) => new(
        ((rgb >> 16) & 0xFF) / 255f,
        ((rgb >> 8) & 0xFF) / 255f,
        (rgb & 0xFF) / 255f,
        alpha / 255f);
}
