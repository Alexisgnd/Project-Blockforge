using System.Collections.Generic;
using UnityEngine;

// =========================================================
// PEINTURE DES BLOCS
// PlacedBlock.colorIndex : 0 = couleurs d'origine du modele,
// 1..12 = teinte de la palette du pistolet a peinture (les
// 12 cases de sa roue, memes valeurs que le .blend et que
// PaintSpraySetup : la couleur posee est celle de la case).
//
// Ce qui est peint : la "carrosserie" de chaque bloc, c'est a
// dire ses materiaux de blindage clairs et neutres (blanc,
// ceramique, perle, chassis texture...). Les pieces sombres,
// les metaux chromes, les accents colores et tout ce qui
// emet de la lumiere (neons cyan, plasma) gardent leur
// couleur : le bloc reste lisible une fois peint. Un bloc sans
// aucun materiau de ce type (tiges toutes en metal chrome...)
// peint son materiau le plus clair, pour que tout bloc reagisse.
//
// Les materiaux peints sont des copies partagees (une par
// materiau d'origine et par teinte) : tous les blocs rouges
// d'un robot utilisent les memes, le SRP Batcher reste actif.
// =========================================================

public static class BlockPalette
{
    public const int Count = 12;

    // Les 12 teintes de la roue du pistolet, dans l'ordre des
    // materiaux Palette_00..11 du .blend (tour du cercle chromatique)
    private static readonly Color[] Colors =
    {
        new(0.850f, 0.127f, 0.127f), new(0.850f, 0.489f, 0.127f),
        new(0.850f, 0.850f, 0.127f), new(0.489f, 0.850f, 0.127f),
        new(0.127f, 0.850f, 0.127f), new(0.127f, 0.850f, 0.489f),
        new(0.127f, 0.850f, 0.850f), new(0.127f, 0.489f, 0.850f),
        new(0.127f, 0.127f, 0.850f), new(0.489f, 0.127f, 0.850f),
        new(0.850f, 0.127f, 0.850f), new(0.850f, 0.127f, 0.489f),
    };

    public static bool IsPaint(int colorIndex) => colorIndex >= 1 && colorIndex <= Count;

    // colorIndex 1..12 -> teinte (blanc si hors palette)
    public static Color Get(int colorIndex)
    {
        return IsPaint(colorIndex) ? Colors[colorIndex - 1] : Color.white;
    }

    // Teinte de la palette la plus proche d'une couleur (case de la roue) -> 1..12
    public static int Nearest(Color color)
    {
        int best = 1;
        float bestDist = float.MaxValue;
        for (int i = 0; i < Colors.Length; i++)
        {
            float dr = Colors[i].r - color.r, dg = Colors[i].g - color.g, db = Colors[i].b - color.b;
            float d = dr * dr + dg * dg + db * db;
            if (d < bestDist)
            {
                bestDist = d;
                best = i + 1;
            }
        }
        return best;
    }
}

public static class BlockPaint
{
    // Proprietes de couleur connues (HDRP Lit/Unlit, glTFast, standard)
    private static readonly string[] ColorProperties = { "_BaseColor", "_UnlitColor", "_Color", "baseColorFactor" };
    private static readonly string[] EmissiveProperties = { "_EmissiveColor", "_EmissionColor", "emissiveFactor" };

    // Seuils de la "carrosserie" : clair, peu sature, pas trop chrome
    private const float MinLightness = 0.6f;
    private const float MaxSaturation = 0.25f;
    private const float MaxMetallic = 0.7f;

    private static readonly Dictionary<Material, bool> paintableCache = new();
    private static readonly Dictionary<(Material, int), Material> tintCache = new();

    // Peint (ou depeint avec 0) le visuel d'un bloc. Les materiaux
    // d'origine sont memorises a la premiere peinture. Renvoie le nombre
    // d'emplacements de materiaux peints (0 = depeint ou rien a peindre).
    public static int Apply(Transform visual, int colorIndex)
    {
        if (visual == null)
            return 0;

        if (!visual.TryGetComponent<BlockPaintState>(out var state))
        {
            if (!BlockPalette.IsPaint(colorIndex))
                return 0; // jamais peint et rien a peindre
            state = visual.gameObject.AddComponent<BlockPaintState>();
            state.Capture();
        }

        state.ColorIndex = colorIndex;
        int painted = 0;

        for (int r = 0; r < state.Renderers.Length; r++)
        {
            var renderer = state.Renderers[r];
            if (renderer == null)
                continue;
            var originals = state.Originals[r];
            var materials = new Material[originals.Length];
            for (int m = 0; m < originals.Length; m++)
            {
                bool paint = BlockPalette.IsPaint(colorIndex) && state.Paintable[r][m];
                materials[m] = paint ? Tinted(originals[m], colorIndex) : originals[m];
                if (paint)
                    painted++;
            }
            renderer.sharedMaterials = materials;
        }
        return painted;
    }

    // Un materiau est de la carrosserie s'il est clair, neutre, mat ou
    // satine, opaque et sans emission.
    // Materiaux peints d'un visuel : la carrosserie, sinon le plus clair
    public static bool[][] PaintMask(Material[][] originals)
    {
        var mask = new bool[originals.Length][];
        bool any = false;
        Material lightest = null;
        float lightestValue = -1f;
        for (int r = 0; r < originals.Length; r++)
        {
            mask[r] = new bool[originals[r].Length];
            for (int m = 0; m < originals[r].Length; m++)
            {
                var material = originals[r][m];
                mask[r][m] = IsPaintable(material);
                any |= mask[r][m];
                float value = Lightness(material);
                if (value > lightestValue)
                {
                    lightestValue = value;
                    lightest = material;
                }
            }
        }
        if (!any && lightest != null)
        {
            for (int r = 0; r < originals.Length; r++)
                for (int m = 0; m < originals[r].Length; m++)
                    mask[r][m] = originals[r][m] == lightest;
        }
        return mask;
    }

    // Clarte (max RVB) d'un materiau opaque non emissif ; -1 sinon
    private static float Lightness(Material material)
    {
        if (material == null || IsEmissive(material) || material.renderQueue >= 2500)
            return -1f;
        string colorProperty = FirstProperty(material, ColorProperties);
        if (colorProperty == null)
            return -1f;
        Color c = material.GetColor(colorProperty);
        return Mathf.Max(c.r, Mathf.Max(c.g, c.b));
    }

    public static bool IsPaintable(Material material)
    {
        if (material == null)
            return false;
        if (paintableCache.TryGetValue(material, out bool cached))
            return cached;

        bool paintable = false;
        string colorProperty = FirstProperty(material, ColorProperties);
        if (colorProperty != null && !IsEmissive(material) && material.renderQueue < 2500)
        {
            Color c = material.GetColor(colorProperty);
            Color.RGBToHSV(c, out _, out float saturation, out float value);
            float metallic = material.HasProperty("_Metallic") ? material.GetFloat("_Metallic") : 0f;
            paintable = value >= MinLightness && saturation <= MaxSaturation && metallic <= MaxMetallic;
        }
        paintableCache[material] = paintable;
        return paintable;
    }

    private static Material Tinted(Material source, int colorIndex)
    {
        if (tintCache.TryGetValue((source, colorIndex), out var tinted) && tinted != null)
            return tinted;

        tinted = new Material(source) { name = $"{source.name} (paint {colorIndex:00})" };
        Color paint = BlockPalette.Get(colorIndex);
        foreach (var property in ColorProperties)
        {
            if (!tinted.HasProperty(property))
                continue;
            paint.a = tinted.GetColor(property).a;
            tinted.SetColor(property, paint);
        }
        tintCache[(source, colorIndex)] = tinted;
        return tinted;
    }

    // Emission : la PREMIERE propriete connue fait foi. HDRP Lit garde aussi
    // un _EmissionColor herite (blanc par defaut, pour le lightmapper) qui ne
    // dit rien de l'emission reelle, portee par _EmissiveColor.
    private static bool IsEmissive(Material material)
    {
        string property = FirstProperty(material, EmissiveProperties);
        if (property == null)
            return false;
        Color e = material.GetColor(property);
        return e.r + e.g + e.b > 1e-4f;
    }

    private static string FirstProperty(Material material, string[] properties)
    {
        foreach (var property in properties)
            if (material.HasProperty(property))
                return property;
        return null;
    }
}

// Materiaux d'origine d'un visuel de bloc (pour repeindre ou depeindre)
public class BlockPaintState : MonoBehaviour
{
    public int ColorIndex { get; set; }
    public Renderer[] Renderers { get; private set; }
    public Material[][] Originals { get; private set; }
    public bool[][] Paintable { get; private set; }

    public void Capture()
    {
        Renderers = GetComponentsInChildren<Renderer>(true);
        Originals = new Material[Renderers.Length][];
        for (int i = 0; i < Renderers.Length; i++)
            Originals[i] = Renderers[i].sharedMaterials;
        Paintable = BlockPaint.PaintMask(Originals);
    }
}
