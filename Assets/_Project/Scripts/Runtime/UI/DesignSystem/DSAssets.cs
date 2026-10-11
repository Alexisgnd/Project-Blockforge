using System.Collections.Generic;
using TMPro;
using UnityEngine;

// =========================================================
// ASSETS DU DESIGN SYSTEM (Resources/DesignSystem/...)
// - Shapes : rectangles arrondis 9-slice generes par
//   Tools/DesignSystem/build_ds_assets.py (fond, contour 1 px,
//   anneau de focus, pointilles, ombres) ;
// - Icons  : les 70 pictogrammes du DS, blancs (a teinter) ;
// - Logo   : lockup horizontal et mark ;
// - Fonts  : Barlow, Barlow Condensed, JetBrains Mono (TMP),
//   crees par Blockforge > Design System > Build Font Assets.
// Charges a la demande et mis en cache.
// =========================================================

public enum DSRadius { None, Xs, Sm, Md, Lg, Pill }

public enum DSFont
{
    Regular,           // Barlow 400 : corps de texte
    Medium,            // Barlow 500
    SemiBold,          // Barlow 600 : libelles, boutons, titres
    Bold,              // Barlow 700
    CondensedSemiBold, // Barlow Condensed 600 : display-md, titres de panneau
    CondensedBold,     // Barlow Condensed 700 : display-xl / display-lg
    Mono,              // JetBrains Mono 500 : tous les chiffres (readout-*)
}

public static class DSAssets
{
    public const string Root = "DesignSystem";

    private static Dictionary<string, Sprite> shapes;
    private static readonly Dictionary<string, Sprite> icons = new();
    private static readonly Dictionary<DSFont, TMP_FontAsset> fonts = new();

    public static readonly string[] FontAssetNames =
    {
        "Barlow-Regular SDF", "Barlow-Medium SDF", "Barlow-SemiBold SDF", "Barlow-Bold SDF",
        "BarlowCondensed-SemiBold SDF", "BarlowCondensed-Bold SDF", "JetBrainsMono-Medium SDF",
    };

    // Apres generation des polices ou des sprites dans l'editeur
    public static void ClearCache()
    {
        shapes = null;
        icons.Clear();
        fonts.Clear();
    }

    // ---------- Formes ----------

    public static string RadiusKey(DSRadius radius) => radius switch
    {
        DSRadius.Xs => "xs",
        DSRadius.Sm => "sm",
        DSRadius.Md => "md",
        DSRadius.Lg => "lg",
        DSRadius.Pill => "pill",
        _ => null,
    };

    // kind : "fill", "stroke", "ring", "dash" ; null pour radius-none (Image sans sprite = rectangle net)
    public static Sprite Shape(DSRadius radius, string kind)
    {
        string key = RadiusKey(radius);
        return key == null ? null : Shape($"{key}_{kind}");
    }

    // Nom sans le suffixe d'import (_k4_b20) : "sm_fill", "shadow_panel"...
    public static Sprite Shape(string name)
    {
        if (shapes == null)
        {
            shapes = new Dictionary<string, Sprite>();
            foreach (var sprite in Resources.LoadAll<Sprite>(Root + "/Shapes"))
            {
                int cut = sprite.name.IndexOf("_k", System.StringComparison.Ordinal);
                shapes[cut > 0 ? sprite.name.Substring(0, cut) : sprite.name] = sprite;
            }
        }
        return shapes.TryGetValue(name, out var found) ? found : null;
    }

    // Marge de flou d'une ombre (px) : l'image d'ombre deborde d'autant de chaque cote
    public static float ShadowPadding(string name)
    {
        var sprite = Shape(name);
        if (sprite == null)
            return 0f;
        int p = sprite.name.LastIndexOf("_p", System.StringComparison.Ordinal);
        return p > 0 && float.TryParse(sprite.name.Substring(p + 2), out float pad) ? pad : 0f;
    }

    // ---------- Icones ----------

    public static Sprite Icon(string name)
    {
        if (string.IsNullOrEmpty(name))
            return null;
        if (!icons.TryGetValue(name, out var sprite))
        {
            sprite = Resources.Load<Sprite>($"{Root}/Icons/{name}");
            icons[name] = sprite;
            if (sprite == null)
                Debug.LogWarning($"[DesignSystem] Icone '{name}' introuvable (Resources/{Root}/Icons).");
        }
        return sprite;
    }

    public static Sprite Logo(string name = "logo-horizontal") => Resources.Load<Sprite>($"{Root}/Logo/{name}");

    // ---------- Polices ----------

    public static TMP_FontAsset Font(DSFont font)
    {
        if (!fonts.TryGetValue(font, out var asset) || asset == null)
        {
            asset = Resources.Load<TMP_FontAsset>($"{Root}/Fonts/{FontAssetNames[(int)font]}");
            if (asset == null)
                asset = TMP_Settings.defaultFontAsset; // polices pas encore generees
            fonts[font] = asset;
        }
        return asset;
    }
}
