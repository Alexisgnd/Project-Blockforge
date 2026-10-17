using TMPro;
using UnityEngine;

// =========================================================
// STYLES DE TEXTE DU DESIGN SYSTEM (tokens.json > type)
// sans = Barlow, condensed = Barlow Condensed (titres d'ecran
// et de panneau), mono = JetBrains Mono (tous les chiffres).
// display-* et label sont rendus en CAPITALES par le style :
// le texte source s'ecrit en sentence case.
// =========================================================

public enum DSTextStyle
{
    DisplayXL,  // 48 / condensed 700 / caps : titre d'ecran, un seul par ecran
    DisplayLG,  // 34 / condensed 700 / caps : titre de section large
    DisplayMD,  // 24 / condensed 600 / caps : titre de panneau ou de dialogue
    TitleLG,    // 20 / 600 : nom de la piece ou du robot selectionne
    TitleMD,    // 16 / 600 : titre de carte, de groupe
    TitleSM,    // 14 / 600 : sous-titre dans un panneau
    Body,       // 15 / 400 : descriptions, texte de dialogue
    BodyStrong, // 15 / 600
    BodySM,     // 13 / 400 : listes denses, infobulles
    Caption,    // 12 / 400 : noms de stats, unites
    Label,      // 11 / 600 / caps / 0.09em : navigation, onglets, puces
    ReadoutLG,  // 24 / mono : LE chiffre principal d'un ecran
    ReadoutMD,  // 15 / mono : valeurs d'une liste de stats
    ReadoutSM,  // 12 / mono : chiffres dans une carte, une puce
    ButtonSM,   // 12 / 600 / caps / 0.04em
    ButtonMD,   // 13 / 600 / caps / 0.04em
    ButtonLG,   // 14 / 600 / caps / 0.04em
    Key,        // 11 / mono : touche d'un KeyHint
    KeyCorner,  // 9 / mono : raccourci dans le coin d'un IconButton
}

public static class DSText
{
    private readonly struct Spec
    {
        public readonly DSFont font;
        public readonly float size;
        public readonly float spacingEm;
        public readonly bool caps;

        public Spec(DSFont font, float size, float spacingEm = 0f, bool caps = false)
        {
            this.font = font;
            this.size = size;
            this.spacingEm = spacingEm;
            this.caps = caps;
        }
    }

    private static Spec Get(DSTextStyle style) => style switch
    {
        DSTextStyle.DisplayXL => new Spec(DSFont.CondensedBold, 48f, 0.02f, true),
        DSTextStyle.DisplayLG => new Spec(DSFont.CondensedBold, 34f, 0.02f, true),
        DSTextStyle.DisplayMD => new Spec(DSFont.CondensedSemiBold, 24f, 0.02f, true),
        DSTextStyle.TitleLG => new Spec(DSFont.SemiBold, 20f),
        DSTextStyle.TitleMD => new Spec(DSFont.SemiBold, 16f),
        DSTextStyle.TitleSM => new Spec(DSFont.SemiBold, 14f),
        DSTextStyle.Body => new Spec(DSFont.Regular, 15f),
        DSTextStyle.BodyStrong => new Spec(DSFont.SemiBold, 15f),
        DSTextStyle.BodySM => new Spec(DSFont.Regular, 13f),
        DSTextStyle.Caption => new Spec(DSFont.Regular, 12f),
        DSTextStyle.Label => new Spec(DSFont.SemiBold, 11f, 0.09f, true),
        DSTextStyle.ReadoutLG => new Spec(DSFont.Mono, 24f),
        DSTextStyle.ReadoutMD => new Spec(DSFont.Mono, 15f),
        DSTextStyle.ReadoutSM => new Spec(DSFont.Mono, 12f),
        DSTextStyle.ButtonSM => new Spec(DSFont.SemiBold, 12f, 0.04f, true),
        DSTextStyle.ButtonMD => new Spec(DSFont.SemiBold, 13f, 0.04f, true),
        DSTextStyle.ButtonLG => new Spec(DSFont.SemiBold, 14f, 0.04f, true),
        DSTextStyle.Key => new Spec(DSFont.Mono, 11f),
        DSTextStyle.KeyCorner => new Spec(DSFont.Mono, 9f),
        _ => new Spec(DSFont.Regular, 15f),
    };

    public static void Apply(TMP_Text text, DSTextStyle style)
    {
        if (text == null)
            return;
        var spec = Get(style);
        var font = DSAssets.Font(spec.font);
        if (font != null)
        {
            text.font = font;
            text.fontSharedMaterial = font.material;
        }
        text.fontSize = spec.size;
        text.enableAutoSizing = false;
        // La graisse vient du fichier de police : pas de faux gras ni d'italique
        text.fontStyle = spec.caps ? FontStyles.UpperCase : FontStyles.Normal;
        text.characterSpacing = spec.spacingEm * 100f; // TMP : 1/100 em
        text.wordSpacing = 0f;
        text.lineSpacing = 0f;
        text.richText = true;
    }

    public static void Apply(TMP_Text text, DSTextStyle style, Color color)
    {
        Apply(text, style);
        if (text != null)
            text.color = color;
    }

    public static bool IsCaps(DSTextStyle style) => Get(style).caps;
    public static float Size(DSTextStyle style) => Get(style).size;

    // ---------- Styles depuis les jetons (BlockforgeTheme.Type) ----------

    // Applique un TypeSpec du theme : famille + graisse -> police, taille,
    // interlettrage ; caps = capitales rendues par le style (display-*, label)
    public static void Apply(TMP_Text text, TypeSpec spec, Color color, bool caps = false)
    {
        if (text == null)
            return;
        var font = DSAssets.Font(DSAssets.FontFor(spec.Family, spec.Weight));
        if (font != null)
        {
            text.font = font;
            text.fontSharedMaterial = font.material;
        }
        text.fontSize = spec.Size;
        text.enableAutoSizing = false;
        text.fontStyle = caps ? FontStyles.UpperCase : FontStyles.Normal;
        text.characterSpacing = spec.LetterSpacingEm * 100f;
        text.wordSpacing = 0f;
        text.lineSpacing = 0f;
        text.richText = true;
        text.color = color;
    }

    // Chiffre en mono a une taille / graisse donnee (scores, prix, compteurs)
    public static void Mono(TMP_Text text, float size, int weight, Color color) =>
        Apply(text, new TypeSpec(FontFamily.Mono, size, size, weight, 0f), color);

    // Titre condense en capitales a une taille donnee (VICTORY, noms de rayon)
    public static void Condensed(TMP_Text text, float size, int weight, float trackingEm, Color color) =>
        Apply(text, new TypeSpec(FontFamily.Condensed, size, size, weight, trackingEm), color, true);

    // Libelle en capitales (over-lines) a une taille / un interlettrage donnes
    public static void Caps(TMP_Text text, float size, int weight, float trackingEm, Color color) =>
        Apply(text, new TypeSpec(FontFamily.Sans, size, size, weight, trackingEm), color, true);
}
