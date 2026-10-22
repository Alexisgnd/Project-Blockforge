using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// =========================================================
// RETOURS DE COMBAT DU JOUEUR LOCAL (design system, theme hud)
// Pose par RobotTestHUD.Bind sur le HUD de match (scene de test
// et TdmMatch), recree avec le robot a chaque reapparition.
// Ecoute CombatEvents.RobotHit et ne garde que les impacts
// du robot du joueur (attacker) :
// - Hitmarker centre sur le reticule : quatre traits courts sur
//   les diagonales, centre libre. Chaque trait est dessine deux
//   fois : un trait sombre plus epais (contour) puis le trait de
//   couleur par-dessus, extremites carrees : lisible sur neige,
//   sable ou ciel clair, jamais la couleur seule.
//   3 etats, distingues d'abord par longueur et epaisseur :
//   Hit (blanc), Point faible (warning : l'impact a fait tomber
//   au moins weakSpotMinBlocks blocs), Kill (ennemi).
//   Plusieurs impacts dans la meme frame : kill > point faible >
//   hit. Pleine opacite 60 ms, puis 1 -> 1,15 en s'effacant
//   (180 ms) ; un nouvel impact relance l'animation.
// - Chiffres de degats : PV reellement retires, JetBrains Mono
//   600, contour 2 px #0C1017 (huit copies decalees) + ombre
//   douce. +36 px a droite, 76 px au-dessus du reticule, montent
//   de 30 px et s'effacent en 600 ms. Kill : crane devant le
//   chiffre. Impacts sur une meme cible a moins de 300 ms : un
//   seul chiffre qui s'additionne (l'ancien reste un instant,
//   plus petit et pale). Milliers separes par une espace fine.
// Mouvements reduits (theme.reducedMotion) : fondus seulement.
// Tailles, durees : CombatFeedbackTheme ; couleurs : jetons hud.
// =========================================================

public class CombatFeedbackHUD : MonoBehaviour
{
    public enum HitKind { Hit = 0, WeakSpot = 1, Kill = 2 }

    // Dernier HUD actif (outils de debug de l'editeur)
    public static CombatFeedbackHUD Active { get; private set; }

    private CombatFeedbackTheme theme;
    private GameObject shooter;
    private RectTransform root;

    // ---------- Hitmarker ----------
    private RectTransform marker;
    private CanvasGroup markerGroup;
    private readonly RectTransform[] strokeOutline = new RectTransform[4];
    private readonly Image[] strokeColor = new Image[4];
    private float markerStart = -100f;
    private HitKind markerKind;
    private int markerFrame = -1;

    // Diagonales : haut droite, haut gauche, bas gauche, bas droite (y vers le haut)
    private static readonly Vector2[] Diagonals = { new(1f, 1f), new(-1f, 1f), new(-1f, -1f), new(1f, -1f) };
    // Contour des chiffres et du crane : huit copies decalees
    private static readonly Vector2[] OutlineDirs =
    {
        new(1f, 0f), new(-1f, 0f), new(0f, 1f), new(0f, -1f),
        new(0.7071f, 0.7071f), new(-0.7071f, 0.7071f), new(0.7071f, -0.7071f), new(-0.7071f, -0.7071f),
    };

    // ---------- Chiffres ----------
    private class Number
    {
        public RectTransform rect;
        public CanvasGroup group;
        public TextMeshProUGUI main, shadow;
        public readonly TextMeshProUGUI[] outline = new TextMeshProUGUI[8];
        public RectTransform skullRoot;
        public Image skull;
        public readonly Image[] skullOutline = new Image[8];
        public RobotHealth target;
        public float total, lastHit, born;
        public HitKind kind;
        public bool ghost;
    }

    private readonly List<Number> live = new();
    private readonly Stack<Number> pool = new();
    private Material shadowMaterial;

    // =========================================================
    // MISE EN PLACE
    // =========================================================

    public static CombatFeedbackHUD Attach(GameObject host, GameObject shooterRobot)
    {
        var hud = host.AddComponent<CombatFeedbackHUD>();
        hud.Bind(shooterRobot);
        return hud;
    }

    public void Bind(GameObject shooterRobot)
    {
        shooter = shooterRobot;
        theme = CombatFeedbackTheme.Load();
        BuildCanvas();
        BuildMarker();
    }

    private void OnEnable()
    {
        CombatEvents.RobotHit += OnRobotHit;
        Active = this;
    }

    private void OnDisable()
    {
        CombatEvents.RobotHit -= OnRobotHit;
        if (Active == this)
            Active = null;
        // HUD masque (robot detruit, fin de match) : on repart propre
        if (marker != null)
            marker.gameObject.SetActive(false);
        for (int i = live.Count - 1; i >= 0; i--)
            Release(live[i]);
        live.Clear();
    }

    private void OnDestroy()
    {
        if (shadowMaterial != null)
            Destroy(shadowMaterial);
    }

    private void BuildCanvas()
    {
        var go = new GameObject("CombatFeedback", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        go.transform.SetParent(transform, false);
        var canvas = go.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = theme.sortingOrder;
        var scaler = go.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(theme.referenceHeight * 16f / 9f, theme.referenceHeight);
        scaler.matchWidthOrHeight = 1f;   // mis a l'echelle sur la hauteur d'ecran
        root = (RectTransform)go.transform;
    }

    private void BuildMarker()
    {
        var go = new GameObject("Hitmarker", typeof(RectTransform), typeof(CanvasGroup));
        go.transform.SetParent(root, false);
        marker = (RectTransform)go.transform;
        Center(marker, new Vector2(theme.boxPixels, theme.boxPixels));
        markerGroup = go.GetComponent<CanvasGroup>();
        markerGroup.interactable = false;
        markerGroup.blocksRaycasts = false;
        // Les quatre contours d'abord, les traits de couleur par-dessus
        for (int i = 0; i < 4; i++)
            strokeOutline[i] = NewRect(marker, "StrokeOutline" + i).rectTransform;
        for (int i = 0; i < 4; i++)
            strokeColor[i] = NewRect(marker, "Stroke" + i);
        foreach (var rt in strokeOutline)
            rt.GetComponent<Image>().color = theme.OutlineColor;
        go.SetActive(false);
    }

    // =========================================================
    // EVENEMENTS
    // =========================================================

    private void OnRobotHit(RobotHitEvent e)
    {
        if (root == null || shooter == null || e.attacker != shooter || e.target == null)
            return;
        var kind = e.isKill ? HitKind.Kill
                 : e.detachedBlocks >= theme.weakSpotMinBlocks ? HitKind.WeakSpot
                 : HitKind.Hit;
        Show(e.target, e.damage, kind);
    }

    // Aussi utilise par le menu de debug de l'editeur (target peut etre null)
    public void Show(RobotHealth target, float damage, HitKind kind)
    {
        ShowMarker(kind);
        AddNumber(target, damage, kind);
    }

    // =========================================================
    // HITMARKER
    // =========================================================

    private void ShowMarker(HitKind kind)
    {
        // Plusieurs impacts dans la meme frame : on garde l'etat le plus fort
        if (Time.frameCount == markerFrame && kind < markerKind)
            kind = markerKind;
        markerFrame = Time.frameCount;
        markerKind = kind;
        markerStart = Time.unscaledTime;
        ApplyStroke(theme.StrokeFor(kind), theme.ColorFor(kind));
        marker.localScale = Vector3.one;
        markerGroup.alpha = 1f;
        marker.gameObject.SetActive(true);
    }

    // Trait de 'from' a 'to' le long de chaque diagonale (unites x/y), extremites
    // carrees : chaque rectangle deborde d'une demi-epaisseur a chaque bout
    private void ApplyStroke(CombatFeedbackTheme.Stroke s, Color color)
    {
        float k = theme.boxPixels / Mathf.Max(1f, theme.boxUnits);
        float mid = (s.from + s.to) * 0.5f * k;
        float length = (s.to - s.from) * k * 1.41421356f;
        for (int i = 0; i < 4; i++)
        {
            Vector2 d = Diagonals[i];
            float angle = d.x * d.y > 0f ? 45f : -45f;
            Vector2 pos = d * mid;

            var outline = strokeOutline[i];
            outline.anchoredPosition = pos;
            outline.sizeDelta = new Vector2(length + s.outlineWidth * k, s.outlineWidth * k);
            outline.localEulerAngles = new Vector3(0f, 0f, angle);

            var stroke = strokeColor[i].rectTransform;
            stroke.anchoredPosition = pos;
            stroke.sizeDelta = new Vector2(length + s.width * k, s.width * k);
            stroke.localEulerAngles = new Vector3(0f, 0f, angle);
            strokeColor[i].color = color;
        }
    }

    private void UpdateMarker(float now)
    {
        if (marker == null || !marker.gameObject.activeSelf)
            return;
        float t = now - markerStart;
        if (t >= theme.hitmarkerHold + theme.hitmarkerFade)
        {
            marker.gameObject.SetActive(false);
            return;
        }
        if (t <= theme.hitmarkerHold)
        {
            markerGroup.alpha = 1f;
            marker.localScale = Vector3.one;
            return;
        }
        float u = Mathf.Clamp01((t - theme.hitmarkerHold) / Mathf.Max(0.001f, theme.hitmarkerFade));
        markerGroup.alpha = 1f - u;
        float scale = theme.ReducedMotion ? 1f : Mathf.Lerp(1f, theme.hitmarkerScaleTo, u);
        marker.localScale = new Vector3(scale, scale, 1f);
    }

    // =========================================================
    // CHIFFRES DE DEGATS
    // =========================================================

    private void AddNumber(RobotHealth target, float damage, HitKind kind)
    {
        float now = Time.unscaledTime;
        // Fusion : meme cible, impact precedent il y a moins de mergeWindow
        if (target != null)
        {
            for (int i = live.Count - 1; i >= 0; i--)
            {
                var n = live[i];
                if (n.ghost || n.target != target || now - n.lastHit > theme.mergeWindow)
                    continue;
                // Fantome seulement s'il reste de la place : Get() pourrait sinon
                // recycler le chiffre qu'on est en train de mettre a jour
                if (theme.showMergedGhost && live.Count < theme.maxNumbers)
                {
                    SpawnGhost(n);
                    n.rect.SetAsLastSibling();   // le chiffre a jour reste devant son fantome
                }
                n.total += damage;
                if (kind > n.kind)
                    n.kind = kind;
                n.lastHit = now;
                n.born = now;
                Refresh(n);
                return;
            }
        }
        var fresh = Get();
        fresh.target = target;
        fresh.total = damage;
        fresh.kind = kind;
        fresh.ghost = false;
        fresh.lastHit = now;
        fresh.born = now;
        Refresh(fresh);
    }

    // L'ancien chiffre reste visible, plus petit et pale, pendant son fondu
    private void SpawnGhost(Number from)
    {
        var g = Get();
        g.target = null;
        g.total = from.total;
        g.kind = from.kind;
        g.ghost = true;
        g.lastHit = from.lastHit;
        g.born = from.born;
        Refresh(g);
    }

    private Number Get()
    {
        // Trop de chiffres : le plus ancien laisse sa place
        if (live.Count >= Mathf.Max(1, theme.maxNumbers))
        {
            Release(live[0]);
            live.RemoveAt(0);
        }
        var n = pool.Count > 0 ? pool.Pop() : NewNumber();
        n.rect.gameObject.SetActive(true);
        n.rect.SetAsLastSibling();
        live.Add(n);
        return n;
    }

    private void Release(Number n)
    {
        n.target = null;
        n.rect.gameObject.SetActive(false);
        pool.Push(n);
    }

    private void Refresh(Number n)
    {
        bool strong = n.kind != HitKind.Hit;
        float size = n.ghost ? theme.ghostSize : strong ? theme.strongNumberSize : theme.numberSize;
        Color color = theme.ColorFor(n.kind);
        string text = BlockforgeTheme.Num((long)Mathf.Max(1, Mathf.RoundToInt(n.total)));

        bool skull = n.kind == HitKind.Kill;
        float skullSize = n.ghost ? theme.ghostSize : theme.skullSize;
        n.skullRoot.gameObject.SetActive(skull);
        if (skull)
        {
            n.skullRoot.sizeDelta = new Vector2(skullSize, skullSize);
            n.skull.color = theme.KillColor;
        }
        float textX = skull ? skullSize + theme.skullGap : 0f;

        SetText(n.main, text, size, color, textX, 0f);
        SetText(n.shadow, text, size, theme.SoftShadowColor, textX, -theme.softShadowOffsetY);
        for (int i = 0; i < 8; i++)
        {
            Vector2 o = OutlineDirs[i] * theme.numberOutline;
            SetText(n.outline[i], text, size, theme.OutlineColor, textX + o.x, o.y);
        }
    }

    private static void SetText(TextMeshProUGUI t, string text, float size, Color color, float x, float y)
    {
        t.text = text;
        t.fontSize = size;
        t.color = color;
        t.rectTransform.anchoredPosition = new Vector2(x, y);
    }

    private void UpdateNumbers(float now)
    {
        for (int i = live.Count - 1; i >= 0; i--)
        {
            var n = live[i];
            float u = (now - n.born) / Mathf.Max(0.001f, theme.numberLife);
            if (u >= 1f)
            {
                Release(n);
                live.RemoveAt(i);
                continue;
            }
            float rise = theme.ReducedMotion ? 0f : theme.numberRise * (1f - (1f - u) * (1f - u));
            n.rect.anchoredPosition = new Vector2(theme.numberOffset.x, theme.numberOffset.y + rise);
            // Lisible tout de suite, s'efface surtout en fin de vie
            float alpha = 1f - u * u;
            n.group.alpha = n.ghost ? alpha * theme.ghostAlpha : alpha;
        }
    }

    private Number NewNumber()
    {
        var go = new GameObject("DamageNumber", typeof(RectTransform), typeof(CanvasGroup));
        go.transform.SetParent(root, false);
        var n = new Number { rect = (RectTransform)go.transform, group = go.GetComponent<CanvasGroup>() };
        n.group.interactable = false;
        n.group.blocksRaycasts = false;
        // Ancre au centre du reticule, pivot a gauche : le chiffre part vers la droite
        n.rect.anchorMin = n.rect.anchorMax = new Vector2(0.5f, 0.5f);
        n.rect.pivot = new Vector2(0f, 0.5f);
        n.rect.sizeDelta = new Vector2(240f, 48f);
        n.rect.anchoredPosition = theme.numberOffset;

        // Crane (kill) : huit copies sombres puis le crane en couleur ennemi
        var skullGo = new GameObject("Skull", typeof(RectTransform));
        skullGo.transform.SetParent(n.rect, false);
        n.skullRoot = (RectTransform)skullGo.transform;
        n.skullRoot.anchorMin = n.skullRoot.anchorMax = new Vector2(0f, 0.5f);
        n.skullRoot.pivot = new Vector2(0f, 0.5f);
        n.skullRoot.anchoredPosition = Vector2.zero;
        var skullSprite = DSAssets.Icon("skull");
        for (int i = 0; i < 8; i++)
        {
            var img = NewIcon(n.skullRoot, "SkullOutline" + i, skullSprite, theme.OutlineColor);
            img.rectTransform.anchoredPosition = OutlineDirs[i] * theme.numberOutline;
            n.skullOutline[i] = img;
        }
        n.skull = NewIcon(n.skullRoot, "SkullIcon", skullSprite, theme.KillColor);

        // Texte : ombre douce, huit copies de contour, puis le chiffre
        n.shadow = NewNumberText(n.rect, "Shadow");
        n.shadow.fontSharedMaterial = ShadowMaterial(n.shadow);
        for (int i = 0; i < 8; i++)
            n.outline[i] = NewNumberText(n.rect, "Outline" + i);
        n.main = NewNumberText(n.rect, "Value");
        go.SetActive(false);
        return n;
    }

    private TextMeshProUGUI NewNumberText(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<TextMeshProUGUI>();
        DSText.Mono(t, theme.numberSize, theme.numberWeight, theme.HitColor);
        t.alignment = TextAlignmentOptions.MidlineLeft;
        t.textWrappingMode = TextWrappingModes.NoWrap;
        t.overflowMode = TextOverflowModes.Overflow;
        t.raycastTarget = false;
        var rt = t.rectTransform;
        rt.anchorMin = new Vector2(0f, 0f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0f, 0.5f);
        rt.sizeDelta = Vector2.zero;
        return t;
    }

    // Ombre douce : meme police, bord du SDF adouci et epaissi (flou approche)
    private Material ShadowMaterial(TextMeshProUGUI sample)
    {
        if (shadowMaterial == null && sample.font != null && sample.font.material != null)
        {
            shadowMaterial = new Material(sample.font.material) { name = "DamageNumberShadow (instance)" };
            shadowMaterial.SetFloat(ShaderUtilities.ID_OutlineSoftness, theme.softShadowSoftness);
            shadowMaterial.SetFloat(ShaderUtilities.ID_FaceDilate, 0.25f);
        }
        return shadowMaterial != null ? shadowMaterial : sample.fontSharedMaterial;
    }

    private static Image NewIcon(Transform parent, string name, Sprite sprite, Color color)
    {
        var img = NewRect(parent, name);
        img.sprite = sprite;
        img.preserveAspect = true;
        img.color = color;
        var rt = img.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = Vector2.zero;
        return img;
    }

    // =========================================================
    // BOUCLE
    // =========================================================

    private void Update()
    {
        float now = Time.unscaledTime;
        UpdateMarker(now);
        UpdateNumbers(now);
    }

    // =========================================================
    // OUTILS
    // =========================================================

    private static Image NewRect(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.raycastTarget = false;
        var rt = img.rectTransform;
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        return img;
    }

    private static void Center(RectTransform rt, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = size;
    }
}
