using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// =========================================================
// WIDGETS PARTAGES DU DESIGN SYSTEM (construits par UIKit)
// Button, Badge, StatLine, Panel, Notice, CapacityMeter, KeyCap,
// Avatar, TeamMark, PartCard, SearchField, Select, Toggle,
// Stepper, SegmentTabs, ChipGroup, Pager, DisabledReason.
// Chaque widget est une petite classe qui garde ses parties et
// expose des setters ; les etats visuels sont portes par
// DSElement, les couleurs par la Palette du theme.
// =========================================================

public enum ButtonVariant { Primary, Secondary, Ghost, Danger }
public enum BadgeScale { Status, Category, Grade, Team }

// ---------- Button ----------

public sealed class ButtonWidget
{
    public RectTransform Rect;
    public Button Button;
    public DSElement Element;
    public TextMeshProUGUI Label;
    public Image Icon;
    private DisabledReason reason;

    public string Text { get => Label != null ? Label.text : ""; set { if (Label != null) Label.text = value; } }

    public bool Interactable
    {
        get => Button.interactable;
        set { Button.interactable = value; if (value) reason?.Clear(); }
    }

    // Desactive avec une raison (opacity-disabled + cadenas / raison au survol)
    public void Disable(string why)
    {
        Button.interactable = false;
        reason ??= Button.gameObject.AddComponent<DisabledReason>();
        reason.Set(why);
    }

    public void Enable()
    {
        Button.interactable = true;
        reason?.Clear();
    }
}

public sealed class ChoiceOption
{
    public string Id;
    public string Label;
    public string Icon;
    public int Count = -1;

    public ChoiceOption(string id, string label, string icon = null, int count = -1)
    {
        Id = id;
        Label = label;
        Icon = icon;
        Count = count;
    }
}

public static partial class DSWidgets
{
    private static DSRole RoleOf(ButtonVariant v) => v switch
    {
        ButtonVariant.Primary => DSRole.ButtonPrimary,
        ButtonVariant.Secondary => DSRole.ButtonSecondary,
        ButtonVariant.Danger => DSRole.ButtonDanger,
        _ => DSRole.ButtonGhost,
    };

    // Bouton du DS : hauteur 36 / 44 (ou sur mesure), libelle en capitales, pictogramme optionnel
    public static ButtonWidget Button(this UIKit kit, Transform parent, string name, ButtonVariant variant, string label,
                                      Action onClick = null, string icon = null, float height = BlockforgeTheme.ControlMd,
                                      bool fullWidth = false, bool iconRight = false)
    {
        var w = new ButtonWidget();
        w.Rect = kit.Rect(parent, name);
        var image = w.Rect.gameObject.AddComponent<Image>();
        image.raycastTarget = true;
        w.Button = UIKit.Clickable(w.Rect);

        float padX = height >= BlockforgeTheme.ControlLg ? BlockforgeTheme.Space6 : BlockforgeTheme.Space4;
        var row = kit.Row(w.Rect, "Content", BlockforgeTheme.Space2, TextAnchor.MiddleCenter, UIKit.Pad(padX, 0f));
        UIKit.Stretch(row);
        var style = height >= BlockforgeTheme.ControlLg ? DSTextStyle.ButtonLG : height <= BlockforgeTheme.ControlSm ? DSTextStyle.ButtonSM : DSTextStyle.ButtonMD;

        if (!string.IsNullOrEmpty(icon) && !iconRight)
            w.Icon = kit.Icon(row, "Icon", icon, BlockforgeTheme.IconSize.Lg, kit.P.InkSecondary);
        w.Label = kit.Text(row, "Label", style, kit.P.InkPrimary, label, TextAlignmentOptions.Center);
        if (!string.IsNullOrEmpty(icon) && iconRight)
            w.Icon = kit.Icon(row, "Icon", icon, BlockforgeTheme.IconSize.Lg, kit.P.InkSecondary);

        w.Element = kit.Element(w.Rect, RoleOf(variant), new TMP_Text[] { w.Label }, w.Icon != null ? new Graphic[] { w.Icon } : null);
        kit.Group(w.Rect); // opacity-disabled portee par DSElement

        float width = UIKit.PreferredWidth(w.Label) + 2f * padX + (w.Icon != null ? BlockforgeTheme.IconSize.Lg + BlockforgeTheme.Space2 : 0f);
        w.Rect.sizeDelta = new Vector2(width, height);
        var le = UIKit.Fixed(w.Rect, fullWidth ? -1f : width, height);
        if (fullWidth)
            le.flexibleWidth = 1f;
        if (onClick != null)
            w.Button.onClick.AddListener(() => onClick());
        return w;
    }

    // Bouton pictogramme seul (36 px, ou sm 28 / lg 44)
    public static ButtonWidget IconButton(this UIKit kit, Transform parent, string name, string icon, string accessibleLabel,
                                          Action onClick = null, float size = BlockforgeTheme.ControlMd)
    {
        var w = new ButtonWidget();
        w.Rect = kit.Rect(parent, name);
        var image = w.Rect.gameObject.AddComponent<Image>();
        image.raycastTarget = true;
        w.Button = UIKit.Clickable(w.Rect);
        float iconSize = size >= BlockforgeTheme.ControlLg ? BlockforgeTheme.IconSize.Xxl : size <= BlockforgeTheme.ControlSm ? BlockforgeTheme.IconSize.Md : BlockforgeTheme.IconSize.Xl;
        w.Icon = kit.Icon(w.Rect, "Icon", icon, iconSize, kit.P.InkSecondary);
        UIKit.Place(w.Icon.rectTransform, UIKit.Center, Vector2.zero, new Vector2(iconSize, iconSize));
        w.Element = kit.Element(w.Rect, DSRole.IconButton, null, new Graphic[] { w.Icon });
        kit.Group(w.Rect);
        w.Rect.sizeDelta = new Vector2(size, size);
        UIKit.Fixed(w.Rect, size, size);
        w.Rect.gameObject.name = name + " (" + accessibleLabel + ")";
        if (onClick != null)
            w.Button.onClick.AddListener(() => onClick());
        return w;
    }

    // ---------- Badge ----------

    public static Color BadgeColor(this UIKit kit, BadgeScale scale, string value) => scale switch
    {
        BadgeScale.Team => value == "enemy" ? kit.P.TeamEnemy : value == "neutral" ? kit.P.TeamNeutral : kit.P.TeamAlly,
        BadgeScale.Grade => kit.GradeColor(Enum.TryParse<Blockforge.Meta.Grade>(value, true, out var g) ? g : Blockforge.Meta.Grade.Raw),
        BadgeScale.Category => kit.CategoryColor(Enum.TryParse<Blockforge.Meta.Category>(value, true, out var c) ? c : Blockforge.Meta.Category.Structure),
        _ => value switch
        {
            "success" => kit.P.Success,
            "warning" => kit.P.Warning,
            "danger" => kit.P.DangerInk,
            _ => kit.P.Accent,
        },
    };

    public static Color GradeColor(this UIKit kit, Blockforge.Meta.Grade g) => g switch
    {
        Blockforge.Meta.Grade.Forged => kit.P.GradeForged,
        Blockforge.Meta.Grade.Tempered => kit.P.GradeTempered,
        Blockforge.Meta.Grade.Hardened => kit.P.GradeHardened,
        Blockforge.Meta.Grade.Masterwork => kit.P.GradeMasterwork,
        _ => kit.P.GradeRaw,
    };

    public static Color CategoryColor(this UIKit kit, Blockforge.Meta.Category c) => c switch
    {
        Blockforge.Meta.Category.Mobility => kit.P.CategoryMobility,
        Blockforge.Meta.Category.Weapon => kit.P.CategoryWeapon,
        Blockforge.Meta.Category.Electronics => kit.P.CategoryElectronics,
        Blockforge.Meta.Category.Cosmetic => kit.P.CategoryCosmetic,
        _ => kit.P.CategoryStructure,
    };

    // Puce pilule : contour par defaut (texte et bord dans la couleur de l'echelle),
    // solid = fond plein et texte ink-on-bright (equipe, grade)
    public static RectTransform Badge(this UIKit kit, Transform parent, string name, BadgeScale scale, string value, string label,
                                      bool solid = false, string icon = null)
    {
        var color = kit.BadgeColor(scale, value);
        var row = kit.Row(parent, name, BlockforgeTheme.Space1, TextAnchor.MiddleCenter, UIKit.Pad(BlockforgeTheme.Space2, 0f));
        var fill = row.gameObject.AddComponent<Image>();
        fill.sprite = DSAssets.Shape(DSRadius.Pill, "fill");
        fill.type = Image.Type.Sliced;
        fill.color = solid ? color : kit.P.SurfaceInset;
        fill.raycastTarget = false;
        if (!solid)
        {
            var stroke = kit.Stroke(row, "Border", DSRadius.Pill, color);
            UIKit.Stretch(stroke.rectTransform);
            UIKit.IgnoreLayout(stroke);
        }
        var ink = solid ? kit.P.InkOnBright : color;
        if (!string.IsNullOrEmpty(icon))
            kit.Icon(row, "Icon", icon, BlockforgeTheme.IconSize.Sm, ink);
        var text = kit.Text(row, "Label", DSTextStyle.Label, ink, label, TextAlignmentOptions.Center);
        UIKit.Fixed(row, -1f, BlockforgeTheme.ControlSm);
        UIKit.Fit(row, true, false);
        return row;
    }

    // ---------- StatLine ----------

    public sealed class StatLineWidget
    {
        public RectTransform Rect;
        public TextMeshProUGUI Label, Value;
        public Image Fill;
        private readonly UIKit kit;

        public StatLineWidget(UIKit kit) { this.kit = kit; }

        public void Set(string value, string unit = null, float ratio = -1f)
        {
            Value.text = string.IsNullOrEmpty(unit) ? value : BlockforgeTheme.QtyMuted(value, unit);
            if (Fill != null)
            {
                Fill.rectTransform.anchorMax = new Vector2(Mathf.Clamp01(ratio), 1f);
                Fill.transform.parent.gameObject.SetActive(ratio >= 0f);
            }
        }
    }

    public static StatLineWidget StatLine(this UIKit kit, Transform parent, string name, string label, string value, string unit = null, float ratio = -1f)
    {
        var w = new StatLineWidget(kit);
        w.Rect = kit.Column(parent, name, BlockforgeTheme.Space1);
        var head = kit.Row(w.Rect, "Head", BlockforgeTheme.Space4, TextAnchor.MiddleLeft);
        w.Label = kit.Text(head, "Label", DSTextStyle.Caption, kit.P.InkSecondary, label);
        UIKit.Flexible(w.Label);
        w.Value = kit.Text(head, "Value", DSTextStyle.ReadoutMD, kit.P.InkPrimary, value, TextAlignmentOptions.MidlineRight);
        UIKit.Fixed(head, -1f, BlockforgeTheme.Type.ReadoutMd.LineHeight);
        var track = kit.Shape(w.Rect, "Track", DSRadius.Xs, kit.P.SurfaceInset);
        UIKit.Fixed(track, -1f, BlockforgeTheme.Space1);
        w.Fill = kit.Shape(track.transform, "Fill", DSRadius.Xs, kit.P.Accent);
        UIKit.Stretch(w.Fill.rectTransform);
        w.Set(value, unit, ratio);
        return w;
    }

    // ---------- Panel ----------

    public sealed class PanelWidget
    {
        public RectTransform Rect, Content;
        public TextMeshProUGUI Title, Subtitle;
        public DSElement Element;

        public void SetTitle(string title, string subtitle = null)
        {
            if (Title != null) Title.text = title;
            if (Subtitle != null)
            {
                Subtitle.text = subtitle ?? "";
                Subtitle.gameObject.SetActive(!string.IsNullOrEmpty(subtitle));
            }
        }
    }

    // Panel du DS : surface-panel, 1 px border-subtle, radius-md, padding 16, titre display-md + sous-titre caption
    public static PanelWidget Panel(this UIKit kit, Transform parent, string name, string title = null, string subtitle = null, float padding = BlockforgeTheme.Space4)
    {
        var w = new PanelWidget();
        w.Rect = kit.Rect(parent, name);
        w.Element = kit.Element(w.Rect, DSRole.Panel);
        var column = kit.Column(w.Rect, "Layout", 0f, TextAnchor.UpperLeft, UIKit.Pad(padding));
        UIKit.Stretch(column);
        if (title != null)
        {
            var head = kit.Column(column, "Head", BlockforgeTheme.Space1);
            w.Title = kit.Text(head, "Title", DSTextStyle.DisplayMD, kit.P.InkPrimary, title);
            w.Subtitle = kit.Text(head, "Subtitle", DSTextStyle.Caption, kit.P.InkMuted, subtitle ?? "");
            w.Subtitle.gameObject.SetActive(!string.IsNullOrEmpty(subtitle));
            kit.Gap(column, BlockforgeTheme.Space5);
        }
        w.Content = kit.Column(column, "Content", BlockforgeTheme.Hub.DetailGap);
        UIKit.Flexible(w.Content, 1f, 1f);
        return w;
    }

    // ---------- Notice ----------

    public sealed class NoticeWidget
    {
        public RectTransform Rect;
        public TextMeshProUGUI Title, Body;
        public DSElement Element;
        public Image Icon;

        public void Set(DSTone tone, string title, string body, string icon = null)
        {
            Element.Tone = tone;
            Title.text = title ?? "";
            Title.gameObject.SetActive(!string.IsNullOrEmpty(title));
            Body.text = body ?? "";
            if (icon != null)
                Icon.sprite = DSAssets.Icon(icon);
        }
    }

    private static string NoticeIcon(DSTone tone) => tone switch
    {
        DSTone.Success => "success-circle",
        DSTone.Warning => "warning",
        DSTone.Danger => "danger",
        _ => "info",
    };

    public static NoticeWidget Notice(this UIKit kit, Transform parent, string name, DSTone tone, string body, string title = null)
    {
        var w = new NoticeWidget();
        w.Rect = kit.Row(parent, name, BlockforgeTheme.Space3, TextAnchor.UpperLeft, UIKit.Pad(BlockforgeTheme.Space3));
        w.Icon = kit.Icon(w.Rect, "Icon", NoticeIcon(tone), BlockforgeTheme.IconSize.Md, kit.P.Accent);
        var main = kit.Column(w.Rect, "Main", BlockforgeTheme.Space1);
        UIKit.Flexible(main);
        w.Title = kit.Text(main, "Title", DSTextStyle.BodyStrong, kit.P.InkPrimary, title ?? "", TextAlignmentOptions.TopLeft, true);
        w.Title.fontSize = BlockforgeTheme.Type.BodySm.Size;
        w.Title.gameObject.SetActive(!string.IsNullOrEmpty(title));
        w.Body = kit.Text(main, "Body", DSTextStyle.BodySM, kit.P.InkSecondary, body, TextAlignmentOptions.TopLeft, true);
        w.Element = kit.Element(w.Rect, DSRole.Notice, new TMP_Text[] { w.Title }, new Graphic[] { w.Icon }, tone);
        return w;
    }

    // ---------- CapacityMeter ----------

    public sealed class CapacityMeterWidget
    {
        public RectTransform Rect;
        public TextMeshProUGUI Label, Value, Note;
        public Image Fill, Over;
        public Image NoteIcon;
        private readonly UIKit kit;

        public CapacityMeterWidget(UIKit kit) { this.kit = kit; }

        public void Set(long value, long max, string note, string unit = null)
        {
            bool over = value > max;
            string v = BlockforgeTheme.Num(value) + " / " + BlockforgeTheme.Num(max) + (string.IsNullOrEmpty(unit) ? "" : " " + unit);
            Value.text = v;
            Value.color = over ? kit.P.DangerInk : kit.P.InkPrimary;
            float k = max <= 0 ? 0f : Mathf.Clamp01((float)Math.Min(value, max) / max);
            Fill.rectTransform.anchorMax = new Vector2(k, 1f);
            Over.gameObject.SetActive(over);
            Note.text = note ?? "";
            Note.color = over ? kit.P.DangerInk : kit.P.InkSecondary;
            NoteIcon.gameObject.SetActive(over);
            Note.transform.parent.gameObject.SetActive(!string.IsNullOrEmpty(note));
        }
    }

    public static CapacityMeterWidget CapacityMeter(this UIKit kit, Transform parent, string name, string label, long value, long max, string note, string unit = null)
    {
        var w = new CapacityMeterWidget(kit);
        w.Rect = kit.Column(parent, name, 0f);
        var head = kit.Row(w.Rect, "Head", BlockforgeTheme.Space4);
        w.Label = kit.Text(head, "Label", DSTextStyle.Label, kit.P.InkSecondary, label);
        UIKit.Flexible(w.Label);
        w.Value = kit.Text(head, "Value", DSTextStyle.ReadoutMD, kit.P.InkPrimary, "", TextAlignmentOptions.MidlineRight);
        UIKit.Fixed(head, -1f, BlockforgeTheme.Type.ReadoutMd.LineHeight);
        kit.Gap(w.Rect, BlockforgeTheme.Space2);
        var track = kit.Shape(w.Rect, "Track", DSRadius.Xs, kit.P.SurfaceInset);
        UIKit.Fixed(track, -1f, BlockforgeTheme.Space2);
        var stroke = kit.Stroke(track.transform, "Border", DSRadius.Xs, kit.P.BorderSubtle);
        UIKit.Stretch(stroke.rectTransform);
        w.Fill = kit.Shape(track.transform, "Fill", DSRadius.Xs, kit.P.Accent);
        UIKit.Stretch(w.Fill.rectTransform);
        w.Over = kit.Shape(track.transform, "Over", DSRadius.Xs, kit.P.Danger);
        UIKit.Stretch(w.Over.rectTransform);
        w.Over.rectTransform.anchorMin = new Vector2(0.9f, 0f);
        kit.Gap(w.Rect, BlockforgeTheme.Space2);
        var noteRow = kit.Row(w.Rect, "NoteRow", BlockforgeTheme.Space1);
        w.NoteIcon = kit.Icon(noteRow, "Icon", "warning", BlockforgeTheme.IconSize.Sm, kit.P.DangerInk);
        w.Note = kit.Text(noteRow, "Note", DSTextStyle.Caption, kit.P.InkSecondary, "");
        w.Set(value, max, note, unit);
        return w;
    }

    // ---------- KeyCap ----------

    public static RectTransform KeyCap(this UIKit kit, Transform parent, string key)
    {
        var rt = kit.Rect(parent, "Key_" + key);
        var text = kit.Text(rt, "Label", DSTextStyle.Key, kit.P.InkPrimary, key, TextAlignmentOptions.Center);
        UIKit.Stretch(text.rectTransform, BlockforgeTheme.Space1, 0f, BlockforgeTheme.Space1, 0f);
        kit.Element(rt, DSRole.KeyCap, new TMP_Text[] { text });
        float width = Mathf.Max(BlockforgeTheme.Controls.KeyCap, UIKit.PreferredWidth(text) + 2f * BlockforgeTheme.Space1);
        rt.sizeDelta = new Vector2(width, BlockforgeTheme.Controls.KeyCap);
        UIKit.Fixed(rt, width, BlockforgeTheme.Controls.KeyCap);
        return rt;
    }

    // ---------- Avatar ----------

    // Carre, radius 4, fond surface-base, bord 2 px couleur d'equipe (accent pour le joueur local), pictogramme user
    public static RectTransform Avatar(this UIKit kit, Transform parent, string name, float size, Color border, float iconSize = -1f)
    {
        var rt = kit.Rect(parent, name);
        rt.sizeDelta = new Vector2(size, size);
        UIKit.Fixed(rt, size, size);
        var frame = kit.Shape(rt, "Frame", DSRadius.Sm, border);
        UIKit.Stretch(frame.rectTransform);
        var ground = kit.Shape(rt, "Ground", DSRadius.Sm, kit.P.SurfaceBase);
        float b = size >= BlockforgeTheme.Space12 ? BlockforgeTheme.Controls.AvatarBorder : BlockforgeTheme.ControlBorder;
        UIKit.Stretch(ground.rectTransform, b, b, b, b);
        float icon = iconSize > 0f ? iconSize : Mathf.Round(size * 0.42f);
        var user = kit.Icon(rt, "User", "user", icon, border);
        UIKit.Place(user.rectTransform, UIKit.Center, Vector2.zero, new Vector2(icon, icon));
        UIKit.IgnoreLayout(user);
        UIKit.IgnoreLayout(frame);
        UIKit.IgnoreLayout(ground);
        return rt;
    }

    // ---------- TeamMark ----------

    public static Image TeamMark(this UIKit kit, Transform parent, Blockforge.Meta.Team team, float size, bool self = false, bool neutral = false)
    {
        string icon = neutral ? "neutral" : team == Blockforge.Meta.Team.Enemy ? "enemy" : "ally";
        var color = neutral ? kit.P.TeamNeutral : self ? kit.P.Accent : team == Blockforge.Meta.Team.Enemy ? kit.P.TeamEnemy : kit.P.TeamAlly;
        return kit.Icon(parent, "TeamMark", icon, size, color);
    }

    // ---------- PartCard ----------

    public sealed class PartCardWidget
    {
        public RectTransform Rect;
        public Button Button;
        public DSElement Element;
        public Image GradeBar, Lock, Thumb, CategoryIcon;
        public TextMeshProUGUI Name, Stat;
        public CanvasGroup Group;
        public bool Locked { get; private set; }

        public bool Selected { get => Element.Selected; set => Element.Selected = value; }

        public void SetLocked(bool locked)
        {
            Locked = locked;
            Lock.gameObject.SetActive(locked);
            Button.interactable = !locked;
        }
    }

    public static PartCardWidget PartCard(this UIKit kit, Transform parent, string name, string partName, Blockforge.Meta.Category category,
                                          Blockforge.Meta.Grade grade, string stat, Action onClick = null)
    {
        var w = new PartCardWidget();
        w.Rect = kit.Rect(parent, name);
        w.Rect.sizeDelta = new Vector2(BlockforgeTheme.TileMd, BlockforgeTheme.TileMd);
        UIKit.Fixed(w.Rect, BlockforgeTheme.TileMd, BlockforgeTheme.TileMd);
        w.Rect.gameObject.AddComponent<Image>().raycastTarget = true;
        w.Button = UIKit.Clickable(w.Rect);
        w.Group = kit.Group(w.Rect);

        var mask = w.Rect.gameObject.AddComponent<RectMask2D>();
        mask.enabled = true;

        // Barre de grade 3 px a gauche
        w.GradeBar = kit.Image(w.Rect, "Grade", kit.GradeColor(grade));
        UIKit.Place(w.GradeBar.rectTransform, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, new Vector2(BlockforgeTheme.Store.GradeBar, 0f));

        float pad = BlockforgeTheme.Space2;
        var column = kit.Column(w.Rect, "Layout", 0f, TextAnchor.UpperLeft, UIKit.Pad(pad + BlockforgeTheme.Store.GradeBar, pad, pad, pad));
        UIKit.Stretch(column);
        w.Thumb = kit.Shape(column, "Thumb", DSRadius.Xs, kit.P.SurfaceInset);
        UIKit.Fixed(w.Thumb, -1f, BlockforgeTheme.Controls.PartThumb);
        w.CategoryIcon = kit.Icon(w.Thumb.transform, "Icon", Blockforge.Meta.MetaLabels.Icon(category), BlockforgeTheme.IconSize.Xl, kit.P.InkSecondary);
        UIKit.Place(w.CategoryIcon.rectTransform, UIKit.Center, Vector2.zero, new Vector2(BlockforgeTheme.IconSize.Xl, BlockforgeTheme.IconSize.Xl));
        kit.Gap(column, BlockforgeTheme.Space1);
        w.Name = kit.Text(column, "Name", BlockforgeTheme.Type.Caption.With(BlockforgeTheme.Type.Caption.Size, 600), kit.P.InkPrimary, partName, TextAlignmentOptions.TopLeft, false, true);
        w.Name.overflowMode = TextOverflowModes.Ellipsis;
        UIKit.Fixed(w.Name, -1f, BlockforgeTheme.Type.Caption.LineHeight);
        w.Stat = kit.Text(column, "Stat", DSTextStyle.ReadoutSM, kit.P.InkMuted, stat);
        w.Stat.fontSize = BlockforgeTheme.Type.Label.Size;

        w.Lock = kit.Icon(w.Rect, "Lock", "lock", BlockforgeTheme.IconSize.Sm, kit.P.InkMuted);
        UIKit.Place(w.Lock.rectTransform, UIKit.TopRight, new Vector2(-BlockforgeTheme.Space1, -BlockforgeTheme.Space1), new Vector2(BlockforgeTheme.IconSize.Sm, BlockforgeTheme.IconSize.Sm));
        w.Lock.gameObject.SetActive(false);

        w.Element = kit.Element(w.Rect, DSRole.PartCard, new TMP_Text[] { w.Name }, new Graphic[] { w.CategoryIcon });
        if (onClick != null)
            w.Button.onClick.AddListener(() => onClick());
        return w;
    }

    // ---------- SearchField ----------

    public sealed class SearchFieldWidget
    {
        public RectTransform Rect;
        public TMP_InputField Input;
        public DSElement Element;
        public event Action<string> Changed;
        internal void Raise(string v) => Changed?.Invoke(v);
    }

    public static SearchFieldWidget SearchField(this UIKit kit, Transform parent, string name, string placeholder, float width)
    {
        var w = new SearchFieldWidget();
        w.Rect = kit.Rect(parent, name);
        w.Rect.sizeDelta = new Vector2(width, BlockforgeTheme.ControlMd);
        UIKit.Fixed(w.Rect, width, BlockforgeTheme.ControlMd);
        w.Rect.gameObject.AddComponent<Image>().raycastTarget = true;

        var icon = kit.Icon(w.Rect, "Icon", "search", BlockforgeTheme.IconSize.Md, kit.P.InkMuted);
        UIKit.Place(icon.rectTransform, UIKit.MiddleLeft, new Vector2(BlockforgeTheme.Space3, 0f), new Vector2(BlockforgeTheme.IconSize.Md, BlockforgeTheme.IconSize.Md));

        var textArea = kit.Rect(w.Rect, "Text Area");
        UIKit.Stretch(textArea, BlockforgeTheme.Space3 + BlockforgeTheme.IconSize.Md + BlockforgeTheme.Space2, 0f, BlockforgeTheme.Space3, 0f);
        textArea.gameObject.AddComponent<RectMask2D>();
        var ph = kit.Text(textArea, "Placeholder", DSTextStyle.Body, kit.P.InkMuted, placeholder);
        UIKit.Stretch(ph.rectTransform);
        var text = kit.Text(textArea, "Text", DSTextStyle.Body, kit.P.InkPrimary, "");
        UIKit.Stretch(text.rectTransform);
        text.overflowMode = TextOverflowModes.Overflow;

        w.Input = w.Rect.gameObject.AddComponent<TMP_InputField>();
        w.Input.textViewport = textArea;
        w.Input.textComponent = text;
        w.Input.placeholder = ph;
        w.Input.caretColor = kit.P.Accent;
        w.Input.selectionColor = BlockforgeTheme.WithAlpha(kit.P.Accent, 0.35f);
        w.Input.transition = Selectable.Transition.None;
        w.Input.onValueChanged.AddListener(w.Raise);
        w.Element = kit.Element(w.Rect, DSRole.Field, new TMP_Text[] { text }, new Graphic[] { icon });
        return w;
    }

    // ---------- Select ----------

    public sealed class SelectWidget
    {
        public RectTransform Rect;
        public Button Button;
        public DSElement Element;
        public TextMeshProUGUI Value, LabelText;
        public List<ChoiceOption> Options;
        public string Selected;
        public event Action<string> Changed;
        internal UIKit Kit;
        internal RectTransform Popup;
        internal Transform PopupParent;

        public void Set(string id, bool notify = true)
        {
            Selected = id;
            foreach (var o in Options)
                if (o.Id == id) Value.text = o.Label;
            if (notify) Changed?.Invoke(id);
        }

        public void Toggle()
        {
            if (Popup != null) { Close(); return; }
            Open();
        }

        public void Close()
        {
            if (Popup != null) UnityEngine.Object.Destroy(Popup.gameObject);
            Popup = null;
        }

        public void Open()
        {
            var kit = Kit;
            var host = PopupParent != null ? PopupParent : Rect.parent;
            Popup = kit.Rect(host, "SelectPopup");
            var panel = kit.Element(Popup, DSRole.Dialog);
            var column = kit.Column(Popup, "Options", 0f, TextAnchor.UpperLeft, UIKit.Pad(BlockforgeTheme.Space1));
            UIKit.Stretch(column);
            Button first = null;
            foreach (var o in Options)
            {
                var opt = kit.Button(column, "Option_" + o.Id, ButtonVariant.Ghost, o.Label, null, null, BlockforgeTheme.ControlMd, true);
                opt.Label.fontStyle = FontStyles.Normal;
                DSText.Apply(opt.Label, DSTextStyle.Body, opt.Label.color);
                opt.Label.alignment = TextAlignmentOptions.MidlineLeft;
                opt.Element.Selected = o.Id == Selected;
                if (o.Id == Selected) opt.Element.role = DSRole.TabSegment;
                string id = o.Id;
                opt.Button.onClick.AddListener(() => { Set(id); Close(); UIKit.Focus(Button); });
                first ??= opt.Button;
            }
            // Sous le champ, meme largeur ; pousse au-dessus des voisins
            var size = new Vector2(Rect.rect.width, Options.Count * BlockforgeTheme.ControlMd + 2f * BlockforgeTheme.Space1);
            Popup.SetParent(host, false);
            Popup.anchorMin = Popup.anchorMax = Popup.pivot = UIKit.TopLeft;
            var corner = new Vector3[4];
            Rect.GetWorldCorners(corner);
            var local = host as RectTransform;
            Vector2 bottomLeft = local != null ? (Vector2)local.InverseTransformPoint(corner[0]) : Vector2.zero;
            Popup.localPosition = new Vector3(bottomLeft.x, bottomLeft.y - BlockforgeTheme.Space1, 0f);
            Popup.sizeDelta = size;
            Popup.SetAsLastSibling();
            UIKit.IgnoreLayout(Popup);
            Popup.gameObject.AddComponent<SelectPopupCloser>().Owner = this;
            UIKit.Focus(first);
        }
    }

    // Ferme la liste sur Echap ou clic ailleurs
    public sealed class SelectPopupCloser : MonoBehaviour
    {
        public SelectWidget Owner;

        private void Update()
        {
            var kb = UnityEngine.InputSystem.Keyboard.current;
            var mouse = UnityEngine.InputSystem.Mouse.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame) { Owner.Close(); UIKit.Focus(Owner.Button); return; }
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                var rt = (RectTransform)transform;
                if (!RectTransformUtility.RectangleContainsScreenPoint(rt, mouse.position.ReadValue(), null) &&
                    !RectTransformUtility.RectangleContainsScreenPoint(Owner.Rect, mouse.position.ReadValue(), null))
                    Owner.Close();
            }
        }
    }

    public static SelectWidget Select(this UIKit kit, Transform parent, string name, List<ChoiceOption> options, string value, float width, string label = null)
    {
        var w = new SelectWidget { Kit = kit, Options = options };
        var column = kit.Column(parent, name, BlockforgeTheme.Space1);
        UIKit.Fixed(column, width, -1f);
        if (!string.IsNullOrEmpty(label))
            w.LabelText = kit.Text(column, "Label", DSTextStyle.Caption, kit.P.InkSecondary, label);
        w.Rect = kit.Rect(column, "Field");
        w.Rect.gameObject.AddComponent<Image>().raycastTarget = true;
        UIKit.Fixed(w.Rect, width, BlockforgeTheme.ControlMd);
        w.Button = UIKit.Clickable(w.Rect);
        w.Value = kit.Text(w.Rect, "Value", DSTextStyle.Body, kit.P.InkPrimary, "");
        UIKit.Stretch(w.Value.rectTransform, BlockforgeTheme.Space3, 0f, BlockforgeTheme.Space3 + BlockforgeTheme.IconSize.Md + BlockforgeTheme.Space2, 0f);
        var chevron = kit.Icon(w.Rect, "Chevron", "chevron-down", BlockforgeTheme.IconSize.Md, kit.P.InkMuted);
        UIKit.Place(chevron.rectTransform, UIKit.MiddleRight, new Vector2(-BlockforgeTheme.Space3, 0f), new Vector2(BlockforgeTheme.IconSize.Md, BlockforgeTheme.IconSize.Md));
        w.Element = kit.Element(w.Rect, DSRole.Field, new TMP_Text[] { w.Value }, new Graphic[] { chevron });
        w.Button.onClick.AddListener(w.Toggle);
        w.Set(value, false);
        return w;
    }

    // ---------- Toggle ----------

    public sealed class ToggleWidget
    {
        public RectTransform Rect;
        public Button Button;
        public Image Track, TrackBorder, Thumb;
        public TextMeshProUGUI Label, State;
        public bool Checked { get; private set; }
        public event Action<bool> Changed;
        internal UIKit Kit;

        public void Set(bool value, bool notify = true)
        {
            Checked = value;
            Track.color = value ? Kit.P.AccentSoft : Kit.P.SurfaceInset;
            TrackBorder.color = value ? Kit.P.Accent : Kit.P.BorderStrong;
            Thumb.color = value ? Kit.P.Accent : Kit.P.InkSecondary;
            float inset = BlockforgeTheme.Controls.ToggleThumbInset;
            Thumb.rectTransform.anchoredPosition = new Vector2(value ? BlockforgeTheme.Controls.ToggleWidth - BlockforgeTheme.Controls.ToggleThumb - inset : inset, 0f);
            State.text = value ? "On" : "Off";
            if (notify) Changed?.Invoke(value);
        }
    }

    public static ToggleWidget Toggle(this UIKit kit, Transform parent, string name, string label, bool value)
    {
        var w = new ToggleWidget { Kit = kit };
        w.Rect = kit.Row(parent, name, BlockforgeTheme.Space3, TextAnchor.MiddleLeft);
        w.Rect.gameObject.AddComponent<Image>().color = Color.clear;
        w.Button = UIKit.Clickable(w.Rect);
        w.Label = kit.Text(w.Rect, "Label", DSTextStyle.Body, kit.P.InkPrimary, label);
        var track = kit.Rect(w.Rect, "Track");
        UIKit.Fixed(track, BlockforgeTheme.Controls.ToggleWidth, BlockforgeTheme.Controls.ToggleHeight);
        w.Track = kit.Shape(track, "Fill", DSRadius.Sm, kit.P.SurfaceInset);
        UIKit.Stretch(w.Track.rectTransform);
        w.TrackBorder = kit.Stroke(track, "Border", DSRadius.Sm, kit.P.BorderStrong);
        UIKit.Stretch(w.TrackBorder.rectTransform);
        w.Thumb = kit.Shape(track, "Thumb", DSRadius.Xs, kit.P.InkSecondary);
        UIKit.Place(w.Thumb.rectTransform, UIKit.MiddleLeft, new Vector2(BlockforgeTheme.Controls.ToggleThumbInset, 0f), new Vector2(BlockforgeTheme.Controls.ToggleThumb, BlockforgeTheme.Controls.ToggleThumb));
        w.State = kit.Text(w.Rect, "State", DSTextStyle.ReadoutSM, kit.P.InkMuted, "Off");
        UIKit.Fixed(w.State, BlockforgeTheme.Controls.ToggleHeight, -1f);
        var focus = kit.Element(track, DSRole.None);
        w.Button.onClick.AddListener(() => w.Set(!w.Checked));
        UIKit.Fixed(w.Rect, -1f, BlockforgeTheme.ControlMd);
        w.Set(value, false);
        return w;
    }

    // ---------- Stepper ----------

    public sealed class StepperWidget
    {
        public RectTransform Rect;
        public ButtonWidget Minus, Plus;
        public TextMeshProUGUI ValueText;
        public int Value { get; private set; }
        public int Min = 1, Max = 99;
        public event Action<int> Changed;

        public void Set(int v, bool notify = true)
        {
            Value = Mathf.Clamp(v, Min, Max);
            ValueText.text = BlockforgeTheme.Num(Value);
            Minus.Interactable = Value > Min;
            Plus.Interactable = Value < Max;
            if (notify) Changed?.Invoke(Value);
        }
    }

    public static StepperWidget Stepper(this UIKit kit, Transform parent, string name, int value, int min = 1, int max = 99)
    {
        var w = new StepperWidget { Min = min, Max = max };
        w.Rect = kit.Row(parent, name, 0f, TextAnchor.MiddleCenter);
        kit.Element(w.Rect, DSRole.Stepper);
        w.Minus = kit.IconButton(w.Rect, "Minus", "remove", "Fewer", () => w.Set(w.Value - 1));
        w.ValueText = kit.Mono(w.Rect, "Value", BlockforgeTheme.Store.StepperValueSize, 600, kit.P.InkPrimary, "", TextAlignmentOptions.Center);
        UIKit.Fixed(w.ValueText, BlockforgeTheme.Store.StepperValueWidth, BlockforgeTheme.ControlMd);
        w.Plus = kit.IconButton(w.Rect, "Plus", "add", "More", () => w.Set(w.Value + 1));
        UIKit.Fixed(w.Rect, -1f, BlockforgeTheme.ControlMd);
        UIKit.Fit(w.Rect, true, false);
        w.Set(value, false);
        return w;
    }

    // ---------- SegmentTabs / ChipGroup ----------

    public sealed class ChoiceGroupWidget
    {
        public RectTransform Rect;
        public readonly List<ButtonWidget> Buttons = new();
        public readonly List<ChoiceOption> Options = new();
        public string Selected;
        public event Action<string> Changed;

        public void Set(string id, bool notify = true)
        {
            Selected = id;
            for (int i = 0; i < Buttons.Count; i++)
                Buttons[i].Element.Selected = Options[i].Id == id;
            if (notify) Changed?.Invoke(id);
        }

        public void SetCount(string id, int count)
        {
            for (int i = 0; i < Options.Count; i++)
            {
                if (Options[i].Id != id) continue;
                Options[i].Count = count;
                Buttons[i].Label.text = count >= 0 ? Options[i].Label + " " + BlockforgeTheme.Tint(BlockforgeTheme.Num(count), Buttons[i].Label.color) : Options[i].Label;
            }
        }

        public void Cycle(int delta)
        {
            int index = Options.FindIndex(o => o.Id == Selected);
            if (index < 0) return;
            int next = (index + delta + Options.Count) % Options.Count;
            Set(Options[next].Id);
            UIKit.Focus(Buttons[next].Button);
        }
    }

    private static ChoiceGroupWidget Choices(UIKit kit, Transform parent, string name, List<ChoiceOption> options, string value, DSRole role,
                                             float height, float padX, float gap, DSTextStyle style)
    {
        var w = new ChoiceGroupWidget();
        w.Rect = kit.Row(parent, name, gap, TextAnchor.MiddleLeft);
        foreach (var o in options)
        {
            var rt = kit.Rect(w.Rect, "Choice_" + o.Id);
            rt.gameObject.AddComponent<Image>().raycastTarget = true;
            var button = UIKit.Clickable(rt);
            var row = kit.Row(rt, "Content", BlockforgeTheme.Space2, TextAnchor.MiddleCenter, UIKit.Pad(padX, 0f));
            UIKit.Stretch(row);
            Image icon = null;
            if (!string.IsNullOrEmpty(o.Icon))
                icon = kit.Icon(row, "Icon", o.Icon, BlockforgeTheme.IconSize.Md, kit.P.InkSecondary);
            var label = kit.Text(row, "Label", style, kit.P.InkSecondary, o.Label, TextAlignmentOptions.Center);
            var element = kit.Element(rt, role, new TMP_Text[] { label }, icon != null ? new Graphic[] { icon } : null);
            kit.Group(rt);
            var bw = new ButtonWidget { Rect = rt, Button = button, Element = element, Label = label, Icon = icon };
            w.Buttons.Add(bw);
            w.Options.Add(o);
            string id = o.Id;
            button.onClick.AddListener(() => w.Set(id));
            float width = UIKit.PreferredWidth(label) + 2f * padX + (icon != null ? BlockforgeTheme.IconSize.Md + BlockforgeTheme.Space2 : 0f);
            if (o.Count >= 0)
                width += BlockforgeTheme.Space3 * 2f;
            UIKit.Fixed(rt, width, height);
            if (o.Count >= 0)
                w.SetCount(o.Id, o.Count);
        }
        UIKit.Fixed(w.Rect, -1f, height);
        UIKit.Fit(w.Rect, true, false);
        w.Set(value, false);
        return w;
    }

    // Onglets segment (filtres d'un ecran) : control-md, label en capitales
    public static ChoiceGroupWidget SegmentTabs(this UIKit kit, Transform parent, string name, List<ChoiceOption> options, string value) =>
        Choices(kit, parent, name, options, value, DSRole.TabSegment, BlockforgeTheme.ControlMd, BlockforgeTheme.Space3, BlockforgeTheme.Space1, DSTextStyle.Label);

    // Puces de filtre pilule 32 px : border-strong, selectionnee = accent + surface-selected
    public static ChoiceGroupWidget ChipGroup(this UIKit kit, Transform parent, string name, List<ChoiceOption> options, string value) =>
        Choices(kit, parent, name, options, value, DSRole.ChipFilter, BlockforgeTheme.Hub.ChipHeight, BlockforgeTheme.Hub.ChipPadX, BlockforgeTheme.Hub.ChipGap, DSTextStyle.BodySM);

    // ---------- Pager ----------

    public sealed class PagerWidget
    {
        public RectTransform Rect;
        public TextMeshProUGUI Summary, Position;
        public ButtonWidget Prev, Next;
        public int Page { get; private set; }
        public int Pages { get; private set; } = 1;
        public event Action<int> Changed;

        public void Set(int page, int pages, string summary)
        {
            Pages = Mathf.Max(1, pages);
            Page = Mathf.Clamp(page, 0, Pages - 1);
            Summary.text = summary;
            Position.text = BlockforgeTheme.Num(Page + 1) + " / " + BlockforgeTheme.Num(Pages);
            Prev.Interactable = Page > 0;
            Next.Interactable = Page < Pages - 1;
        }

        internal void Move(int delta)
        {
            int target = Mathf.Clamp(Page + delta, 0, Pages - 1);
            if (target == Page) return;
            Changed?.Invoke(target);
        }
    }

    public static PagerWidget Pager(this UIKit kit, Transform parent, string name)
    {
        var w = new PagerWidget();
        w.Rect = kit.Row(parent, name, BlockforgeTheme.Space2, TextAnchor.MiddleLeft);
        w.Summary = kit.Text(w.Rect, "Summary", DSTextStyle.BodySM, kit.P.InkMuted, "");
        UIKit.Flexible(w.Summary);
        w.Prev = kit.IconButton(w.Rect, "Prev", "chevron-left", "Previous page", () => w.Move(-1), BlockforgeTheme.ControlSm);
        w.Position = kit.Mono(w.Rect, "Position", BlockforgeTheme.Hub.PagerTextSize, 500, kit.P.InkSecondary, "1 / 1", TextAlignmentOptions.Center);
        w.Next = kit.IconButton(w.Rect, "Next", "chevron-right", "Next page", () => w.Move(1), BlockforgeTheme.ControlSm);
        UIKit.Fixed(w.Rect, -1f, BlockforgeTheme.ControlSm);
        return w;
    }
}

// =========================================================
// RAISON D'UNE ACTION INDISPONIBLE : cadenas + infobulle au survol
// (regle du DS : l'opacite seule n'est pas un message)
// =========================================================

public sealed class DisabledReason : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, ISelectHandler, IDeselectHandler
{
    private string reason;
    private Image lockIcon;
    private RectTransform tip;

    public void Set(string why)
    {
        reason = why;
        if (lockIcon == null)
        {
            var kit = UIKit.Dark;
            var element = GetComponent<DSElement>();
            if (element != null)
                kit = element.theme == DSTheme.Hud ? UIKit.Hud : UIKit.Dark;
            lockIcon = kit.Icon(transform, "LockReason", "lock", BlockforgeTheme.IconSize.Sm, kit.P.InkMuted);
            UIKit.Place(lockIcon.rectTransform, UIKit.TopRight, new Vector2(-BlockforgeTheme.Space1, -BlockforgeTheme.Space1), new Vector2(BlockforgeTheme.IconSize.Sm, BlockforgeTheme.IconSize.Sm));
            UIKit.IgnoreLayout(lockIcon);
        }
        lockIcon.gameObject.SetActive(true);
    }

    public void Clear()
    {
        reason = null;
        if (lockIcon != null) lockIcon.gameObject.SetActive(false);
        Hide();
    }

    public void OnPointerEnter(PointerEventData e) => Show();
    public void OnPointerExit(PointerEventData e) => Hide();
    public void OnSelect(BaseEventData e) => Show();
    public void OnDeselect(BaseEventData e) => Hide();

    private void Show()
    {
        if (string.IsNullOrEmpty(reason) || tip != null)
            return;
        var element = GetComponent<DSElement>();
        var kit = element != null && element.theme == DSTheme.Hud ? UIKit.Hud : UIKit.Dark;
        var canvas = GetComponentInParent<Canvas>();
        var host = canvas != null ? canvas.transform : transform.parent;
        tip = kit.Rect(host, "Tooltip");
        kit.Element(tip, DSRole.PanelFloating);
        var text = kit.Text(tip, "Text", DSTextStyle.BodySM, kit.P.InkPrimary, reason, TextAlignmentOptions.MidlineLeft);
        UIKit.Stretch(text.rectTransform, BlockforgeTheme.Space3, BlockforgeTheme.Space2, BlockforgeTheme.Space3, BlockforgeTheme.Space2);
        float w = UIKit.PreferredWidth(text) + 2f * BlockforgeTheme.Space3;
        var corners = new Vector3[4];
        ((RectTransform)transform).GetWorldCorners(corners);
        var local = (RectTransform)host;
        Vector2 top = local.InverseTransformPoint((corners[1] + corners[2]) * 0.5f);
        tip.anchorMin = tip.anchorMax = tip.pivot = UIKit.BottomCenter;
        tip.localPosition = new Vector3(top.x, top.y + BlockforgeTheme.Space2, 0f);
        tip.sizeDelta = new Vector2(w, BlockforgeTheme.Controls.TooltipHeight);
        tip.SetAsLastSibling();
        UIKit.IgnoreLayout(tip);
    }

    private void Hide()
    {
        if (tip != null) Destroy(tip.gameObject);
        tip = null;
    }

    private void OnDisable() => Hide();
}

// =========================================================
// TAB / MAJ+TAB : cycle des controles d'un ecran dans l'ordre de
// la hierarchie (les fleches et le stick sont geres par uGUI)
// =========================================================

public sealed class TabFocusCycler : MonoBehaviour
{
    private readonly List<Selectable> buffer = new();

    private void Update()
    {
        var kb = UnityEngine.InputSystem.Keyboard.current;
        if (kb == null || !kb.tabKey.wasPressedThisFrame)
            return;
        bool back = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
        buffer.Clear();
        GetComponentsInChildren(false, buffer);
        buffer.RemoveAll(s => !s.IsInteractable() || !s.gameObject.activeInHierarchy || s.navigation.mode == Navigation.Mode.None);
        if (buffer.Count == 0 || EventSystem.current == null)
            return;
        var current = EventSystem.current.currentSelectedGameObject;
        int index = current != null ? buffer.FindIndex(s => s.gameObject == current) : -1;
        int next = index < 0 ? (back ? buffer.Count - 1 : 0) : (index + (back ? -1 : 1) + buffer.Count) % buffer.Count;
        EventSystem.current.SetSelectedGameObject(buffer[next].gameObject);
    }
}
