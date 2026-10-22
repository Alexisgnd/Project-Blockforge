using UnityEngine;
using UnityEngine.UI;

// =========================================================
// MEDAILLE DU PODIUM (ecran de resultats, theme hud)
// Pose sur une carte du podium (ChamferedPlate) : 1er or,
// 2e argent, 3e bronze.
// - Lisere : degrade vertical de la couleur de la medaille (clair
//   en haut, plus sombre en bas), il suit les chanfreins ; celui
//   de l'or respire doucement.
// - Fill : lueur verticale de la medaille (haut -> transparent)
//   sous le contenu, et un reflet en biais qui traverse la carte
//   de temps en temps (or plus souvent et plus fort que l'argent
//   et le bronze), plus quelques eclats en losange qui scintillent.
// - Mouvement reduit (BlockforgeTheme.ReduceMotion) : lisere et
//   lueur fixes, pas de reflet ni d'eclats.
// Le temps est le temps reel (l'arene est figee, timeScale 0).
// =========================================================

public sealed class PodiumMedalFX : MonoBehaviour
{
    public static readonly Color Gold = new(0.98f, 0.79f, 0.32f);
    public static readonly Color Silver = new(0.80f, 0.85f, 0.91f);
    public static readonly Color Bronze = new(0.86f, 0.55f, 0.32f);

    public static Color ForRank(int rank) => rank switch { 1 => Gold, 2 => Silver, _ => Bronze };

    private const float BandWidth = 46f, BandSlant = 90f;

    private ChamferedPlate plate;
    private Color medal;
    private int rank;
    private bool reduce;
    private RectTransform sheen, sheenArea;
    private HudPolygon sheenA, sheenB;
    private float period, sweep, phase, peak;
    private readonly System.Collections.Generic.List<(RectTransform rect, HudPolygon poly, float offset, float speed)> glints = new();

    public static PodiumMedalFX Attach(UIKit kit, ChamferedPlate plate, int rank)
    {
        var fx = plate.gameObject.AddComponent<PodiumMedalFX>();
        fx.Build(kit, plate, rank);
        return fx;
    }

    private void Build(UIKit kit, ChamferedPlate p, int r)
    {
        plate = p;
        rank = r;
        medal = ForRank(r);
        reduce = BlockforgeTheme.ReduceMotion;
        int contentIndex = plate.Content.GetSiblingIndex();

        // Lisere : degrade vertical clair -> sombre
        if (plate.Frame != null)
        {
            plate.Frame.vertical = true;
            plate.Frame.SetColors(Color.Lerp(medal, Color.white, 0.35f), Color.Lerp(medal, Color.black, 0.35f));
        }

        // Lueur de la medaille dans le fill (sous le contenu)
        var glow = kit.Polygon(plate.Rect, "MedalGlow");
        UIKit.Stretch(glow.rectTransform);
        glow.cutTopLeft = (plate.Corners & PlateCorners.TopLeft) != 0 ? plate.Chamfer : 0f;
        glow.cutTopRight = (plate.Corners & PlateCorners.TopRight) != 0 ? plate.Chamfer : 0f;
        glow.vertical = true;
        glow.SetColors(BlockforgeTheme.WithAlpha(medal, r == 1 ? 0.24f : 0.16f), BlockforgeTheme.WithAlpha(medal, 0f));
        glow.raycastTarget = false;
        UIKit.IgnoreLayout(glow);
        glow.transform.SetSiblingIndex(contentIndex);

        if (reduce)
            return;

        // Reflet en biais, decoupe sous la ligne des chanfreins
        sheenArea = kit.Rect(plate.Rect, "Sheen");
        UIKit.Stretch(sheenArea, 0f, plate.Chamfer, 0f, 0f);
        sheenArea.gameObject.AddComponent<RectMask2D>();
        UIKit.IgnoreLayout(sheenArea);
        sheenArea.SetSiblingIndex(contentIndex + 1);

        sheen = kit.Rect(sheenArea, "Band");
        sheen.anchorMin = new Vector2(0f, 0f);
        sheen.anchorMax = new Vector2(0f, 1f);
        sheen.pivot = new Vector2(0f, 0.5f);
        sheen.sizeDelta = new Vector2(2f * BandWidth + BandSlant, 0f);
        sheenA = Band(kit, sheen, "A", 0f);
        sheenB = Band(kit, sheen, "B", BandWidth);

        peak = r == 1 ? 0.30f : 0.20f;
        period = r == 1 ? 3.4f : r == 2 ? 4.4f : 5.0f;
        sweep = r == 1 ? 1.1f : 1.3f;
        phase = r == 1 ? 0.6f : r == 2 ? 1.6f : 2.4f;
        SetBand(0f);

        // Eclats en losange
        int count = r == 1 ? 4 : 2;
        for (int i = 0; i < count; i++)
        {
            var d = kit.Polygon(sheenArea, "Glint" + i);
            var rt = d.rectTransform;
            float size = r == 1 ? 9f : 7f;
            rt.anchorMin = rt.anchorMax = new Vector2(Random.Range(0.12f, 0.88f), Random.Range(0.25f, 0.9f));
            rt.sizeDelta = new Vector2(size, size);
            d.cutTopLeft = d.cutTopRight = d.cutBottomLeft = d.cutBottomRight = size * 0.5f;
            d.SetSolid(Color.Lerp(medal, Color.white, 0.6f));
            d.raycastTarget = false;
            glints.Add((rt, d, Random.Range(0f, 3f), Random.Range(0.35f, 0.6f)));
        }
    }

    private static HudPolygon Band(UIKit kit, RectTransform parent, string name, float x)
    {
        var poly = kit.Polygon(parent, name);
        var rt = poly.rectTransform;
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(0f, 1f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.anchoredPosition = new Vector2(x, 0f);
        rt.sizeDelta = new Vector2(BandWidth + BandSlant, 0f);
        poly.slantLeft = BandSlant;
        poly.slantRight = BandSlant;
        poly.raycastTarget = false;
        return poly;
    }

    private void SetBand(float alpha)
    {
        var c = Color.Lerp(medal, Color.white, 0.55f);
        sheenA.SetColors(BlockforgeTheme.WithAlpha(c, 0f), BlockforgeTheme.WithAlpha(c, alpha));
        sheenB.SetColors(BlockforgeTheme.WithAlpha(c, alpha), BlockforgeTheme.WithAlpha(c, 0f));
    }

    private void Update()
    {
        float t = Time.unscaledTime;

        // L'or respire
        if (rank == 1 && plate.Frame != null && !reduce)
        {
            float k = 0.5f + 0.5f * Mathf.Sin(t * 2.2f);
            plate.Frame.SetColors(Color.Lerp(medal, Color.white, 0.25f + 0.25f * k), Color.Lerp(medal, Color.black, 0.4f - 0.15f * k));
        }
        if (reduce || sheen == null)
            return;

        // Reflet : traverse la carte en sweep secondes, toutes les period secondes
        float local = Mathf.Repeat(t + phase, period);
        float width = sheenArea.rect.width;
        if (local < sweep)
        {
            float u = local / sweep;
            float eased = u * u * (3f - 2f * u);
            float span = width + sheen.sizeDelta.x;
            sheen.anchoredPosition = new Vector2(-sheen.sizeDelta.x + eased * span, 0f);
            SetBand(peak * Mathf.Sin(u * Mathf.PI));
            if (!sheen.gameObject.activeSelf) sheen.gameObject.SetActive(true);
        }
        else if (sheen.gameObject.activeSelf)
            sheen.gameObject.SetActive(false);

        // Eclats : apparaissent et s'eteignent chacun a son rythme
        foreach (var (rect, poly, offset, speed) in glints)
        {
            float g = Mathf.Repeat(t * speed + offset, 1f);
            float a = g < 0.22f ? Mathf.Sin(g / 0.22f * Mathf.PI) : 0f;
            rect.localScale = Vector3.one * (0.4f + 0.6f * a);
            rect.localRotation = Quaternion.Euler(0f, 0f, 45f * g);
            var c = poly.color; c.a = a * 0.9f; poly.color = c;
        }
    }
}
