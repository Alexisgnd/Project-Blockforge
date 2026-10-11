using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// =========================================================
// NOTIFICATIONS (design system : Notice)
// Usage depuis n'importe quelle scene, sans setup :
//   Toast.Success("Robot saved. Bay 02 is up to date.");
//   var t = Toast.Loading("Saving…"); ... t.Success("…") / t.Error("…");
// Rendu d'une Notice du DS : pictogramme + titre dans la
// couleur de la tonalite, sur le fond *-soft, bord de la meme
// tonalite (jamais la couleur seule). Le message est coupe a
// son premier point : "Couldn't save Hey. Can't reach the
// server." -> titre "Couldn't save Hey", corps "Can't reach
// the server." (ink-secondary).
// - Hote unique (DontDestroyOnLoad) : survit aux changements
//   de scene ; pile en haut au centre, sous la navigation.
// - Deux notices au plus a l'ecran (regle du DS) ; success et
//   info partent apres ~4 s, warning 6 s, danger 10 s ; un clic
//   ferme. Doublons visibles fusionnes.
// - Entree / sortie en fondu, 140 ms ease-out (MotionPanel).
// =========================================================

public enum ToastKind { Info, Success, Warning, Error, Loading }

public sealed class ToastHandle
{
    internal ToastHost.Entry entry;

    public bool IsVisible => entry != null && entry.root != null && !entry.closing;

    public void Info(string message) => ToastHost.Resolve(this, ToastKind.Info, message);
    public void Success(string message) => ToastHost.Resolve(this, ToastKind.Success, message);
    public void Warning(string message) => ToastHost.Resolve(this, ToastKind.Warning, message);
    public void Error(string message) => ToastHost.Resolve(this, ToastKind.Error, message);
    public void Dismiss() => ToastHost.Dismiss(this);
}

public static class Toast
{
    public static ToastHandle Show(string message, ToastKind kind = ToastKind.Info) => ToastHost.Show(message, kind);
    public static ToastHandle Info(string message) => Show(message, ToastKind.Info);
    public static ToastHandle Success(string message) => Show(message, ToastKind.Success);
    public static ToastHandle Warning(string message) => Show(message, ToastKind.Warning);
    public static ToastHandle Error(string message) => Show(message, ToastKind.Error);
    public static ToastHandle Loading(string message) => Show(message, ToastKind.Loading);
}

public class ToastHost : MonoBehaviour
{
    private const int MaxVisible = 2;
    private const float Width = 420f;
    private const float TopOffset = 80f + BlockforgeTheme.Space4; // sous la navigation (80 px)

    internal class Entry
    {
        public GameObject root;
        public CanvasGroup group;
        public RectTransform content;
        public DSElement element;
        public Image icon;
        public TMP_Text title;
        public TMP_Text body;
        public string message;
        public ToastKind kind;
        public float age;
        public float lifetime;
        public bool closing;
        public float closeAge;
        public ToastHandle handle;
    }

    private static ToastHost instance;
    private readonly List<Entry> entries = new();
    private RectTransform stack;

    private static float Anim => BlockforgeTheme.Duration(BlockforgeTheme.MotionPanel);

    // =========================================================
    // API INTERNE
    // =========================================================

    internal static ToastHandle Show(string message, ToastKind kind)
    {
        var host = Ensure();

        // Doublon deja affiche (ex. meme erreur reseau sur deux ecrans) : on le prolonge
        foreach (var existing in host.entries)
        {
            if (!existing.closing && existing.kind == kind && existing.message == message)
            {
                existing.age = Mathf.Min(existing.age, Anim);
                return existing.handle;
            }
        }

        var entry = host.Create(message, kind);
        var handle = new ToastHandle { entry = entry };
        entry.handle = handle;
        host.Trim();
        return handle;
    }

    internal static void Resolve(ToastHandle handle, ToastKind kind, string message)
    {
        if (handle?.entry == null || handle.entry.root == null || handle.entry.closing)
        {
            Show(message, kind); // la notice d'attente a deja disparu : on en ouvre une nouvelle
            return;
        }
        instance.Apply(handle.entry, kind, message);
        handle.entry.age = Anim; // deja visible : pas de nouvelle apparition
    }

    internal static void Dismiss(ToastHandle handle)
    {
        if (handle?.entry != null)
            Close(handle.entry);
    }

    private static ToastHost Ensure()
    {
        if (instance != null)
            return instance;

        var go = new GameObject("[Notices]");
        DontDestroyOnLoad(go);
        instance = go.AddComponent<ToastHost>();
        instance.BuildCanvas();
        return instance;
    }

    // =========================================================
    // CONSTRUCTION
    // =========================================================

    private void BuildCanvas()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 1000; // au-dessus des menus, HUD et ecran Boot
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        stack = new GameObject("Stack", typeof(RectTransform)).GetComponent<RectTransform>();
        stack.SetParent(transform, false);
        stack.anchorMin = stack.anchorMax = new Vector2(0.5f, 1f);
        stack.pivot = new Vector2(0.5f, 1f);
        stack.anchoredPosition = new Vector2(0f, -TopOffset);
        stack.sizeDelta = new Vector2(Width, 0f);
        var layout = stack.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = BlockforgeTheme.Space2;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        stack.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private Entry Create(string message, ToastKind kind)
    {
        var entry = new Entry();

        // Conteneur (pile) -> carte Notice decalee pendant l'animation
        var root = new GameObject("Notice", typeof(RectTransform));
        root.transform.SetParent(stack, false);
        entry.root = root;
        entry.group = root.AddComponent<CanvasGroup>();
        entry.group.alpha = 0f;
        var rootLayout = root.AddComponent<VerticalLayoutGroup>();
        rootLayout.childControlWidth = true;
        rootLayout.childControlHeight = true;
        rootLayout.childForceExpandWidth = true;
        rootLayout.childForceExpandHeight = false;

        var card = new GameObject("Card", typeof(RectTransform), typeof(Image));
        card.transform.SetParent(root.transform, false);
        entry.content = (RectTransform)card.transform;
        var cardLayout = card.AddComponent<HorizontalLayoutGroup>();
        int pad = Mathf.RoundToInt(BlockforgeTheme.Space3);
        cardLayout.padding = new RectOffset(pad, pad, pad, pad);
        cardLayout.spacing = BlockforgeTheme.Space3;
        cardLayout.childAlignment = TextAnchor.UpperLeft;
        cardLayout.childControlWidth = true;
        cardLayout.childControlHeight = true;
        cardLayout.childForceExpandWidth = false;
        cardLayout.childForceExpandHeight = false;

        // Clic = fermer
        var button = card.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.onClick.AddListener(() => Close(entry));

        // Pictogramme de la tonalite (16 px, aligne sur la premiere ligne)
        var iconGo = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        iconGo.transform.SetParent(card.transform, false);
        entry.icon = iconGo.GetComponent<Image>();
        entry.icon.raycastTarget = false;
        entry.icon.preserveAspect = true;
        var iconLe = iconGo.AddComponent<LayoutElement>();
        iconLe.minWidth = iconLe.preferredWidth = 16f;
        iconLe.minHeight = iconLe.preferredHeight = 19f;

        // Colonne titre + corps
        var column = new GameObject("Main", typeof(RectTransform));
        column.transform.SetParent(card.transform, false);
        var columnLayout = column.AddComponent<VerticalLayoutGroup>();
        columnLayout.spacing = BlockforgeTheme.Space1;
        columnLayout.childControlWidth = true;
        columnLayout.childControlHeight = true;
        columnLayout.childForceExpandWidth = true;
        columnLayout.childForceExpandHeight = false;
        column.AddComponent<LayoutElement>().flexibleWidth = 1f;

        entry.title = CreateText(column.transform, "Title", DSTextStyle.BodySM);
        var semi = DSAssets.Font(DSFont.SemiBold);
        if (semi != null)
        {
            entry.title.font = semi;
            entry.title.fontSharedMaterial = semi.material;
        }
        entry.body = CreateText(column.transform, "Body", DSTextStyle.BodySM);
        entry.body.color = BlockforgeTheme.InkSecondary;

        // Fond *-soft, bord et encre de la tonalite : DSElement (role Notice)
        entry.element = card.AddComponent<DSElement>();
        entry.element.role = DSRole.Notice;
        entry.element.labels = new[] { entry.title };
        entry.element.icons = new Graphic[] { entry.icon };
        entry.element.EnsureParts();
        entry.element.ApplyShape();

        Apply(entry, kind, message);
        entries.Add(entry);
        return entry;
    }

    private static TMP_Text CreateText(Transform parent, string name, DSTextStyle style)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<TextMeshProUGUI>();
        DSText.Apply(text, style);
        text.alignment = TextAlignmentOptions.TopLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    private void Apply(Entry entry, ToastKind kind, string message)
    {
        entry.kind = kind;
        entry.message = message;
        entry.lifetime = kind switch
        {
            ToastKind.Error => 10f,
            ToastKind.Warning => 6f,
            ToastKind.Loading => 30f, // filet de securite si personne ne la resout
            _ => 4f,
        };
        if (kind != ToastKind.Loading)
            entry.age = Mathf.Min(entry.age, Anim);

        // "Resultat. Cause et remede." -> titre + corps
        string title = message ?? "";
        string body = "";
        int cut = title.IndexOf(". ", System.StringComparison.Ordinal);
        if (cut > 0)
        {
            body = title.Substring(cut + 2).Trim();
            title = title.Substring(0, cut);
        }
        else if (title.EndsWith("."))
        {
            title = title.Substring(0, title.Length - 1);
        }
        entry.title.text = title;
        entry.body.text = body;
        entry.body.gameObject.SetActive(body.Length > 0);

        var (tone, icon) = kind switch
        {
            ToastKind.Success => (DSTone.Success, "success-circle"),
            ToastKind.Warning => (DSTone.Warning, "warning"),
            ToastKind.Error => (DSTone.Danger, "danger"),
            ToastKind.Loading => (DSTone.Info, "timer"),
            _ => (DSTone.Info, "info"),
        };
        entry.icon.sprite = DSAssets.Icon(icon);
        entry.element.tone = tone;
        entry.element.RefreshImmediate();
    }

    // =========================================================
    // ANIMATION ET DUREE DE VIE
    // =========================================================

    private void Update()
    {
        float dt = Time.unscaledDeltaTime;
        float anim = Mathf.Max(0.0001f, Anim);
        for (int i = entries.Count - 1; i >= 0; i--)
        {
            var entry = entries[i];
            if (entry.root == null)
            {
                entries.RemoveAt(i);
                continue;
            }

            entry.age += dt;
            if (!entry.closing && entry.age >= entry.lifetime)
                Close(entry);

            float k;
            if (entry.closing)
            {
                entry.closeAge += dt;
                k = 1f - BlockforgeTheme.EaseOut(entry.closeAge / anim);
                if (entry.closeAge >= Anim)
                {
                    Destroy(entry.root);
                    entries.RemoveAt(i);
                    continue;
                }
            }
            else
            {
                k = BlockforgeTheme.EaseOut(entry.age / anim);
            }

            // Fondu seul : la carte est placee par les layout groups (ne pas la deplacer)
            entry.group.alpha = k;

            // En cours : le pictogramme respire doucement (le DS n'anime que l'attente)
            if (entry.kind == ToastKind.Loading && !BlockforgeTheme.ReduceMotion)
            {
                var c = entry.icon.color;
                c.a = 0.55f + 0.45f * Mathf.PingPong(entry.age * 1.6f, 1f);
                entry.icon.color = c;
            }
        }
    }

    private static void Close(Entry entry)
    {
        if (entry == null || entry.closing)
            return;
        entry.closing = true;
        entry.closeAge = 0f;
    }

    // Deux notices au plus : la plus ancienne (hors attente) part
    private void Trim()
    {
        int open = 0;
        foreach (var e in entries)
            if (!e.closing) open++;

        for (int i = 0; i < entries.Count && open > MaxVisible; i++)
        {
            if (!entries[i].closing && entries[i].kind != ToastKind.Loading)
            {
                Close(entries[i]);
                open--;
            }
        }
    }
}
