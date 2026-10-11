using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.UI;

// =========================================================
// JETONS DU DESIGN SYSTEM BLOCKFORGE
// Source : Branding/blockforge-design-system.zip -> tokens.json
// (theme "dark" pour les menus et le garage, BlockforgeTheme.Hud
// pour l'overlay de match). Ne jamais ecrire une couleur, un
// espacement ou une duree d'UI en dur : passer par ces jetons.
// Les composants (DSElement), la typo (DSText), les formes
// (DSAssets) et l'outil Blockforge > Apply Design System les lisent.
// L'ecran Boot (UI Toolkit) reprend les memes valeurs dans BootAuth.uss.
// =========================================================

public static class BlockforgeTheme
{
    // ----- Surfaces -----
    public static readonly Color SurfaceBase = Hex(0x171B24);
    public static readonly Color SurfaceRaised = Hex(0x1F2531);
    public static readonly Color SurfacePanel = Hex(0x242B36);
    public static readonly Color SurfaceInset = Hex(0x11151C);
    public static readonly Color SurfaceHover = Hex(0x2C3441);
    public static readonly Color SurfaceSelected = Hex(0x16394B);
    public static readonly Color Scrim = Hex(0x0B0E14, 0xCC);
    public static readonly Color SurfaceHud = Hex(0x171B24, 0xB8);

    // ----- Bordures et focus -----
    public static readonly Color BorderSubtle = Hex(0x303948);
    public static readonly Color BorderStrong = Hex(0x74829A);
    public static readonly Color BorderAccent = Hex(0x39B8E8);
    public static readonly Color FocusRing = Hex(0x7FDCFF);

    // ----- Encres (texte et icones) -----
    public static readonly Color InkPrimary = Hex(0xF2F5F7);
    public static readonly Color InkSecondary = Hex(0xB3BDCB);
    public static readonly Color InkMuted = Hex(0x959FB0);
    public static readonly Color InkOnAccent = Hex(0x04121A);
    public static readonly Color InkOnBright = Hex(0x0D1117);

    // ----- Accent (la seule couleur signature) -----
    public static readonly Color Accent = Hex(0x39B8E8);
    public static readonly Color AccentHover = Hex(0x63CDF5);
    public static readonly Color AccentPress = Hex(0x1F97C6);
    public static readonly Color AccentSoft = Hex(0x16394B);
    public static readonly Color PlateSelf = Hex(0x39B8E8, 0x5C);

    // ----- Statuts (toujours avec une icone et un mot) -----
    public static readonly Color Info = Hex(0x39B8E8);
    public static readonly Color Success = Hex(0x7FE8B8);
    public static readonly Color SuccessSoft = Hex(0x133326);
    public static readonly Color Warning = Hex(0xF0A93F);
    public static readonly Color WarningSoft = Hex(0x3A2A10);
    public static readonly Color Danger = Hex(0xC9332F);
    public static readonly Color DangerInk = Hex(0xF5756D);
    public static readonly Color DangerSoft = Hex(0x3A1614);

    // ----- Equipes (HUD de match uniquement) -----
    public static readonly Color TeamAlly = Hex(0x6BA2FF);
    public static readonly Color TeamAllySoft = Hex(0x152943);
    public static readonly Color PlateAlly = Hex(0x3E73D9, 0x66);
    public static readonly Color TeamEnemy = Hex(0xFF6F5E);
    public static readonly Color TeamEnemySoft = Hex(0x3A1A15);
    public static readonly Color PlateEnemy = Hex(0xD94839, 0x66);
    public static readonly Color TeamNeutral = Hex(0xC2CDDB);

    // ----- Categories de pieces (icone et puce, jamais un fond de tuile) -----
    public static readonly Color CategoryStructure = Hex(0xB0BCCD);
    public static readonly Color CategoryMobility = Hex(0x4FD8CF);
    public static readonly Color CategoryWeapon = Hex(0xF77AA1);
    public static readonly Color CategoryElectronics = Hex(0xBF99FF);
    public static readonly Color CategoryCosmetic = Hex(0xFFD166);

    // ----- Grades de forge (barre de 3 px et libelle) -----
    public static readonly Color GradeRaw = Hex(0x9AA7B8);
    public static readonly Color GradeForged = Hex(0x62C48A);
    public static readonly Color GradeTempered = Hex(0x62AEF5);
    public static readonly Color GradeHardened = Hex(0xCB92F5);
    public static readonly Color GradeMasterwork = Hex(0xF0B23C);

    // ----- Theme "hud" : overlay de match (arene claire et mouvante) -----
    public static class Hud
    {
        // ----- Surfaces -----
        public static readonly Color SurfaceBase = Hex(0x0C1017);
        public static readonly Color SurfaceRaised = Hex(0x141A24);
        public static readonly Color SurfacePanel = Hex(0x1A212C);
        public static readonly Color SurfaceInset = Hex(0x080B10);
        public static readonly Color SurfaceHover = Hex(0x232C3A);
        public static readonly Color SurfaceSelected = Hex(0x123244);
        public static readonly Color Scrim = Hex(0x05080C, 0xE6);
        public static readonly Color SurfaceHud = Hex(0x0C1017, 0xA6);

        // ----- Bordures et focus -----
        public static readonly Color BorderSubtle = Hex(0x3A4557);
        public static readonly Color BorderStrong = Hex(0x76859C);
        public static readonly Color BorderAccent = Hex(0x4ECBF7);
        public static readonly Color FocusRing = Hex(0x9AE6FF);

        // ----- Encres (texte et icones) -----
        public static readonly Color InkPrimary = Hex(0xFFFFFF);
        public static readonly Color InkSecondary = Hex(0xC8D2DE);
        public static readonly Color InkMuted = Hex(0xA6B2C2);
        public static readonly Color InkOnAccent = Hex(0x04121A);
        public static readonly Color InkOnBright = Hex(0x0D1117);

        // ----- Accent (la seule couleur signature) -----
        public static readonly Color Accent = Hex(0x4ECBF7);
        public static readonly Color AccentHover = Hex(0x7DDCFF);
        public static readonly Color AccentPress = Hex(0x2AA9DC);
        public static readonly Color AccentSoft = Hex(0x12303F);
        public static readonly Color PlateSelf = Hex(0x4ECBF7, 0x5C);

        // ----- Statuts (toujours avec une icone et un mot) -----
        public static readonly Color Info = Hex(0x4ECBF7);
        public static readonly Color Success = Hex(0x96F0C8);
        public static readonly Color SuccessSoft = Hex(0x0F2C20);
        public static readonly Color Warning = Hex(0xFFBB55);
        public static readonly Color WarningSoft = Hex(0x33240C);
        public static readonly Color Danger = Hex(0xD93B36);
        public static readonly Color DangerInk = Hex(0xFF8A82);
        public static readonly Color DangerSoft = Hex(0x331211);

        // ----- Equipes (HUD de match uniquement) -----
        public static readonly Color TeamAlly = Hex(0x85B4FF);
        public static readonly Color TeamAllySoft = Hex(0x10223C);
        public static readonly Color PlateAlly = Hex(0x4C8DF6, 0x66);
        public static readonly Color TeamEnemy = Hex(0xFF8271);
        public static readonly Color TeamEnemySoft = Hex(0x331612);
        public static readonly Color PlateEnemy = Hex(0xFF6F5E, 0x59);
        public static readonly Color TeamNeutral = Hex(0xD4DDE8);

        // ----- Categories de pieces (icone et puce, jamais un fond de tuile) -----
        public static readonly Color CategoryStructure = Hex(0xC3CDDB);
        public static readonly Color CategoryMobility = Hex(0x6EE4DC);
        public static readonly Color CategoryWeapon = Hex(0xFF8FB0);
        public static readonly Color CategoryElectronics = Hex(0xCFAAFF);
        public static readonly Color CategoryCosmetic = Hex(0xFFDD85);

        // ----- Grades de forge (barre de 3 px et libelle) -----
        public static readonly Color GradeRaw = Hex(0xADB8C6);
        public static readonly Color GradeForged = Hex(0x7AD6A0);
        public static readonly Color GradeTempered = Hex(0x7BBCFF);
        public static readonly Color GradeHardened = Hex(0xD7A6FF);
        public static readonly Color GradeMasterwork = Hex(0xFFC456);
    }

    // ----- Espacements (px a la resolution de reference 1920x1080) -----
    public const float Space1 = 4f;   // icone / libelle, valeur / unite
    public const float Space2 = 8f;   // padding des petits controles, ecart entre tuiles
    public const float Space3 = 12f;  // padding horizontal d'un bouton md, d'une ligne de liste
    public const float Space4 = 16f;  // padding d'un panneau, ecart entre lignes de stats
    public const float Space5 = 20f;  // titre de panneau -> contenu
    public const float Space6 = 24f;  // entre deux panneaux d'une meme region
    public const float Space8 = 32f;  // marge d'ecran, viewport -> panneaux
    public const float Space12 = 48f; // entre grandes regions (ecrans larges)

    // ----- Rayons -----
    public const float RadiusNone = 0f;  // panneau contre un bord d'ecran, viewport
    public const float RadiusXs = 2f;    // pistes de jauge, remplissages, puits fins
    public const float RadiusSm = 3f;    // boutons, champs, onglets, tuiles (defaut)
    public const float RadiusMd = 6f;    // cartes et panneaux flottants
    public const float RadiusLg = 10f;   // baies de robot et dialogues uniquement
    public const float RadiusPill = 999f; // puces, raccourcis, compteurs

    // ----- Controles -----
    public const float ControlSm = 28f;  // puces, boutons d'outil, filtres
    public const float ControlMd = 36f;  // bouton, onglet, champ par defaut
    public const float ControlLg = 44f;  // action principale de l'ecran (Play, Save and test)
    public const float ControlBorder = 1f;
    public const float ControlFocusWidth = 2f;
    public const float FocusOffset = Space1;
    public const float TileMd = 96f;
    public const float HudChamfer = 10f;
    public const float HudSlot = 56f;

    // ----- Opacites -----
    public const float OpacityDisabled = 0.38f; // toujours avec un cadenas et une raison
    public const float OpacityGhost = 0.7f;     // element auxiliaire au repos (aide des commandes)
    public const float OpacityGrid = 0.14f;     // grille et guides du garage

    // ----- Mouvement (ease-out) -----
    public const float MotionHover = 0.08f;  // survol et focus
    public const float MotionPanel = 0.14f;  // panneaux, infobulles, notices
    public const float MotionScreen = 0.22f; // transitions d'ecran

    // Reglage "reduire les animations" : ramene toutes les durees a zero
    private const string ReduceMotionKey = "Blockforge.ReduceMotion";
    public static bool ReduceMotion
    {
        get => PlayerPrefs.GetInt(ReduceMotionKey, 0) == 1;
        set { PlayerPrefs.SetInt(ReduceMotionKey, value ? 1 : 0); PlayerPrefs.Save(); }
    }

    public static float Duration(float seconds) => ReduceMotion ? 0f : seconds;

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

    // Valeur + unite : "1 850 CPU", "19 036 kg"
    public static string Qty(long value, string unit) => $"{Num(value)} {unit}";
    public static string Qty(float value, string unit, string format = "0.#") => $"{Num(value, format)} {unit}";

    // Unite en ink-muted (StatLine, readouts)
    public static string QtyMuted(string value, string unit) =>
        $"{value}<space=0.25em><color=#{ColorUtility.ToHtmlStringRGB(InkMuted)}><size=80%>{unit}</size></color>";

    // =========================================================
    // ALIAS DE L'ANCIENNE PALETTE (setups d'UI existants)
    // =========================================================

    public static readonly Color Background = SurfaceBase;
    public static readonly Color Surface = SurfacePanel;
    public static readonly Color SurfaceSunken = SurfaceInset;
    public static readonly Color Border = BorderSubtle;
    public static readonly Color AccentMuted = AccentSoft;
    public static readonly Color OnAccent = InkOnAccent;
    public static readonly Color TextPrimary = InkPrimary;
    public static readonly Color TextSecondary = InkSecondary;
    public static readonly Color TextMuted = InkMuted;
    public static readonly Color TextDisabled = InkMuted;
    public static readonly Color DangerMuted = DangerSoft;
    public static readonly Color DangerText = DangerInk;

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
