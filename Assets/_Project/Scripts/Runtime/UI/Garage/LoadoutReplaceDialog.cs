using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// =========================================================
// DIALOGUE "REMPLACER UNE ARME OU UN MODULE" (garage)
// Ouvert par GarageBuildController quand la pose d'un bloc
// ajouterait un 4e type d'arme ou de module (RobotLoadout).
// Composant Dialog du DS, construit a la volee (comme Toast) :
// scrim, panneau radius-lg, titre display-md pose en question,
// une carte (PartCard) par type deja monte avec son icone, sa
// nature (arme / module) et sa quantite ; boutons a droite,
// l'action de confirmation en dernier et nommee avec son objet
// ("Replace Laser Wasp"). Echap = annuler, Entree = confirmer.
// Pendant l'ouverture : curseur libre, et les composants que
// les autres popups du garage coupent (GarageSaveController.
// disableWhileOpen : joueur, pince...) sont coupes aussi.
// IsOpen / ClosedThisFrame : les raccourcis du garage (Echap,
// Tab, pose au clic) s'en servent pour ne pas reagir au meme
// appui que le dialogue.
// =========================================================

public class LoadoutReplaceDialog : MonoBehaviour
{
    public static bool IsOpen => instance != null;
    public static bool ClosedThisFrame => lastCloseFrame == Time.frameCount;

    private static LoadoutReplaceDialog instance;
    private static int lastCloseFrame = -10;

    private Action<BlockDefinition> onReplace;
    private Action onCancel;
    private readonly List<(DSElement card, BlockDefinition def)> cards = new();
    private BlockDefinition selected;
    private Button confirmButton;
    private TMP_Text confirmLabel;
    private readonly List<Behaviour> disabledBehaviours = new();

    private const float DialogWidth = 600f;
    private const float CardWidth = 176f;
    private const float CardHeight = 156f;

    // options : types deja montes (RobotLoadout.Collect) ; incoming : le bloc qui attend d'etre pose
    public static void Open(List<RobotLoadout.Entry> options, BlockDefinition incoming,
                            Action<BlockDefinition> onReplace, Action onCancel = null)
    {
        if (instance != null)
            return; // un seul dialogue a la fois (regle du DS)

        var go = new GameObject("[LoadoutReplaceDialog]");
        instance = go.AddComponent<LoadoutReplaceDialog>();
        instance.onReplace = onReplace;
        instance.onCancel = onCancel;
        instance.Build(options, incoming);
        instance.Capture();
    }

    // =========================================================
    // ENTREES
    // =========================================================

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null)
            return;
        if (kb.escapeKey.wasPressedThisFrame)
            Cancel();
        else if ((kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) && selected != null)
            Confirm();
    }

    private void Select(BlockDefinition def)
    {
        selected = def;
        foreach (var (card, d) in cards)
            card.Selected = d == def;
        confirmButton.interactable = def != null;
        confirmLabel.text = def != null ? $"Replace {def.blockName}" : "Replace";
    }

    private void Confirm()
    {
        if (selected == null)
            return;
        var picked = selected;
        var callback = onReplace;
        Close();
        callback?.Invoke(picked);
    }

    private void Cancel()
    {
        var callback = onCancel;
        Close();
        callback?.Invoke();
    }

    private void Close()
    {
        Release();
        lastCloseFrame = Time.frameCount;
        instance = null;
        Destroy(gameObject);
    }

    // Curseur libre + coupe ce que coupent les popups du garage
    private void Capture()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        var toDisable = new List<Behaviour>();
        var save = FindAnyObjectByType<GarageSaveController>();
        if (save != null && save.disableWhileOpen != null)
            toDisable.AddRange(save.disableWhileOpen);
        // Le joueur reverrouille le curseur au clic : toujours coupe
        toDisable.AddRange(FindObjectsByType<PlayerGarageController>(FindObjectsInactive.Exclude));
        foreach (var b in toDisable)
        {
            if (b != null && b.enabled && !disabledBehaviours.Contains(b))
            {
                b.enabled = false;
                disabledBehaviours.Add(b);
            }
        }
    }

    private void Release()
    {
        foreach (var b in disabledBehaviours)
        {
            if (b != null)
                b.enabled = true;
        }
        disabledBehaviours.Clear();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            Release();
            instance = null;
        }
    }

    // =========================================================
    // CONSTRUCTION (composant Dialog du design system)
    // =========================================================

    private void Build(List<RobotLoadout.Entry> options, BlockDefinition incoming)
    {
        if (EventSystem.current == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem),
                                    typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            es.transform.SetParent(transform, false);
        }

        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900; // au-dessus du garage, sous les notices (1000)
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        // Voile
        var scrim = NewImage(transform, "Scrim");
        Stretch(scrim.rectTransform);
        Element(scrim.gameObject, DSRole.Scrim);

        // Panneau
        var panel = NewImage(transform, "Dialog");
        var panelRect = panel.rectTransform;
        panelRect.anchorMin = panelRect.anchorMax = panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(DialogWidth, 0f);
        var layout = panel.gameObject.AddComponent<VerticalLayoutGroup>();
        int pad = Mathf.RoundToInt(BlockforgeTheme.Space6);
        layout.padding = new RectOffset(pad, pad, pad, pad);
        layout.spacing = BlockforgeTheme.Space4;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        panel.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        Element(panel.gameObject, DSRole.Dialog);

        Text(panel.transform, "Title", DSTextStyle.DisplayMD, BlockforgeTheme.InkPrimary,
             "Replace a weapon or module?");
        string incomingName = incoming != null ? incoming.blockName : "this part";
        string incomingKind = incoming != null ? RobotLoadout.KindLabel(RobotLoadout.Kind(incoming)).ToLowerInvariant() : "part";
        Text(panel.transform, "Body", DSTextStyle.Body, BlockforgeTheme.InkSecondary,
             $"Your robot can carry {RobotLoadout.MaxTypes} types of weapons and modules. To add the {incomingKind} " +
             $"{incomingName}, pick one to remove: all of its blocks go, whatever their number. You can undo with A.");

        // Cartes des types montes
        var row = new GameObject("Options", typeof(RectTransform));
        row.transform.SetParent(panel.transform, false);
        var rowLayout = row.AddComponent<HorizontalLayoutGroup>();
        rowLayout.spacing = BlockforgeTheme.Space2;
        rowLayout.childAlignment = TextAnchor.MiddleCenter;
        rowLayout.childControlWidth = true;
        rowLayout.childControlHeight = true;
        rowLayout.childForceExpandWidth = false;
        rowLayout.childForceExpandHeight = false;
        foreach (var option in options)
            BuildCard(row.transform, option);

        // Actions a droite, confirmation en dernier
        var actions = new GameObject("Actions", typeof(RectTransform));
        actions.transform.SetParent(panel.transform, false);
        var actionsLayout = actions.AddComponent<HorizontalLayoutGroup>();
        actionsLayout.spacing = BlockforgeTheme.Space2;
        actionsLayout.childAlignment = TextAnchor.MiddleRight;
        actionsLayout.childControlWidth = true;
        actionsLayout.childControlHeight = true;
        actionsLayout.childForceExpandWidth = false;
        actionsLayout.childForceExpandHeight = false;

        var cancel = BuildButton(actions.transform, "Cancel", DSRole.ButtonGhost, "Cancel", out _);
        cancel.onClick.AddListener(Cancel);
        confirmButton = BuildButton(actions.transform, "Confirm", DSRole.ButtonPrimary, "Replace", out confirmLabel);
        confirmButton.onClick.AddListener(Confirm);
        Select(null);

        LayoutRebuilder.ForceRebuildLayoutImmediate(panelRect);
    }

    private void BuildCard(Transform parent, RobotLoadout.Entry entry)
    {
        var go = new GameObject("Card_" + entry.definition.name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.preferredWidth = le.minWidth = CardWidth;
        le.preferredHeight = le.minHeight = CardHeight;
        var button = go.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        var def = entry.definition;
        button.onClick.AddListener(() => Select(def));

        var layout = go.AddComponent<VerticalLayoutGroup>();
        int pad = Mathf.RoundToInt(BlockforgeTheme.Space3);
        layout.padding = new RectOffset(pad, pad, pad, pad);
        layout.spacing = BlockforgeTheme.Space1;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;

        // Apercu : icone du bloc, sinon pictogramme de sa nature
        var well = NewImage(go.transform, "Preview");
        var wellLe = well.gameObject.AddComponent<LayoutElement>();
        wellLe.preferredHeight = wellLe.minHeight = 72f;
        Element(well.gameObject, DSRole.Inset);
        var icon = NewImage(well.transform, "Icon");
        icon.preserveAspect = true;
        icon.sprite = def.icon != null ? def.icon : DSAssets.Icon(RobotLoadout.KindIcon(entry.kind));
        icon.color = def.icon != null ? Color.white : BlockforgeTheme.CategoryWeapon;
        var iconRect = icon.rectTransform;
        iconRect.anchorMin = iconRect.anchorMax = iconRect.pivot = new Vector2(0.5f, 0.5f);
        iconRect.sizeDelta = new Vector2(64f, 64f);

        var nameText = Text(go.transform, "Name", DSTextStyle.TitleSM, BlockforgeTheme.InkPrimary, def.blockName);
        nameText.alignment = TextAlignmentOptions.Center;
        nameText.textWrappingMode = TextWrappingModes.NoWrap;
        nameText.overflowMode = TextOverflowModes.Ellipsis;
        string plural = entry.count > 1 ? "blocks" : "block";
        var caption = Text(go.transform, "Caption", DSTextStyle.Caption, BlockforgeTheme.InkMuted,
                           $"{RobotLoadout.KindLabel(entry.kind)} · {entry.count} {plural}");
        caption.alignment = TextAlignmentOptions.Center;

        var element = Element(go, DSRole.PartCard, nameText);
        cards.Add((element, def));
    }

    private static Button BuildButton(Transform parent, string objectName, DSRole role, string label, out TMP_Text text)
    {
        var go = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var button = go.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        var layout = go.AddComponent<HorizontalLayoutGroup>();
        int padX = Mathf.RoundToInt(BlockforgeTheme.Space4);
        layout.padding = new RectOffset(padX, padX, 0, 0);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = le.preferredHeight = BlockforgeTheme.ControlMd;
        le.minWidth = 96f;
        text = Text(go.transform, "Label", DSTextStyle.ButtonMD, BlockforgeTheme.InkPrimary, label);
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        Element(go, role, text);
        return button;
    }

    // ---------- Outils ----------

    private static DSElement Element(GameObject go, DSRole role, params TMP_Text[] labels)
    {
        var element = go.GetComponent<DSElement>();
        if (element == null)
            element = go.AddComponent<DSElement>();
        element.role = role;
        element.labels = labels ?? new TMP_Text[0];
        element.EnsureParts();
        element.ApplyShape();
        element.RefreshImmediate();
        return element;
    }

    private static Image NewImage(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var img = go.GetComponent<Image>();
        img.raycastTarget = false;
        return img;
    }

    private static TMP_Text Text(Transform parent, string name, DSTextStyle style, Color color, string value)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var text = go.AddComponent<TextMeshProUGUI>();
        DSText.Apply(text, style, color);
        text.text = value;
        text.alignment = TextAlignmentOptions.TopLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return text;
    }

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
