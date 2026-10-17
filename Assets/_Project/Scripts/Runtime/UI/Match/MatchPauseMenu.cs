using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// =========================================================
// MENU DE PAUSE DU MATCH (scene de test)
// ECHAP (RobotTestDriver) : voile + dialogue du design system,
// jeu fige (timeScale 0), curseur libre.
// - Resume (ECHAP aussi) : reprend
// - Options : sensibilite de la souris (- / +), axe vertical
//   inverse, animations reduites (BlockforgeTheme.ReduceMotion) ;
//   memorises dans PlayerPrefs, appliques a la camera de match
// - Back to builder : retour au garage (RobotSession.ReturnSceneName)
// - En bas a gauche de l'ecran, un rappel des touches
//   essentielles (KeyCap du DS)
// =========================================================

public class MatchPauseMenu : MonoBehaviour
{
    public const string SensitivityKey = "Blockforge.MouseSensitivity";
    public const string InvertYKey = "Blockforge.InvertY";

    public static bool IsOpen => instance != null;
    private static MatchPauseMenu instance;

    private RobotTestDriver driver;
    private GameObject mainView, optionsView;
    private TMP_Text sensitivityValue, invertLabel, motionLabel;

    public static float Sensitivity => PlayerPrefs.GetFloat(SensitivityKey, 0.12f);
    public static bool InvertY => PlayerPrefs.GetInt(InvertYKey, 0) == 1;

    public static void Open(RobotTestDriver owner)
    {
        if (instance != null)
            return;
        var go = new GameObject("[MatchPauseMenu]");
        instance = go.AddComponent<MatchPauseMenu>();
        instance.driver = owner;
        instance.Build();
        Time.timeScale = 0f;
        RobotTestDriver.SetCursorLocked(false);
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.escapeKey.wasPressedThisFrame)
        {
            if (optionsView != null && optionsView.activeSelf)
                ShowMain();
            else
                Resume();
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
            instance = null;
        Time.timeScale = 1f;
    }

    // ---------- Actions ----------

    private void Resume()
    {
        Time.timeScale = 1f;
        RobotTestDriver.SetCursorLocked(true);
        Destroy(gameObject);
    }

    private void BackToBuilder()
    {
        Time.timeScale = 1f;
        if (driver != null)
            driver.ReturnToGarage();
        Destroy(gameObject);
    }

    private void ShowMain()
    {
        mainView.SetActive(true);
        optionsView.SetActive(false);
    }

    private void ShowOptions()
    {
        mainView.SetActive(false);
        optionsView.SetActive(true);
        RefreshOptions();
    }

    private void ChangeSensitivity(float delta)
    {
        float v = Mathf.Clamp(Mathf.Round((Sensitivity + delta) * 100f) / 100f, 0.02f, 0.5f);
        PlayerPrefs.SetFloat(SensitivityKey, v);
        PlayerPrefs.Save();
        ApplyToCamera();
        RefreshOptions();
    }

    private void ToggleInvert()
    {
        PlayerPrefs.SetInt(InvertYKey, InvertY ? 0 : 1);
        PlayerPrefs.Save();
        ApplyToCamera();
        RefreshOptions();
    }

    private void ToggleMotion()
    {
        BlockforgeTheme.ReduceMotion = !BlockforgeTheme.ReduceMotion;
        RefreshOptions();
    }

    public static void ApplyToCamera()
    {
        var cam = FindAnyObjectByType<RobotFollowCamera>();
        if (cam == null)
            return;
        cam.mouseSensitivity = Sensitivity;
        cam.invertY = InvertY;
    }

    private void RefreshOptions()
    {
        if (sensitivityValue != null)
            sensitivityValue.text = Sensitivity.ToString("0.00");
        if (invertLabel != null)
            invertLabel.text = InvertY ? "On" : "Off";
        if (motionLabel != null)
            motionLabel.text = BlockforgeTheme.ReduceMotion ? "On" : "Off";
    }

    // ---------- Construction ----------

    private void Build()
    {
        if (EventSystem.current == null)
        {
            var es = new GameObject("EventSystem", typeof(EventSystem), typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
            es.transform.SetParent(transform, false);
        }
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 900;
        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        gameObject.AddComponent<GraphicRaycaster>();

        var scrim = NewImage(transform, "Scrim");
        Stretch(scrim.rectTransform);
        Element(scrim.gameObject, DSRole.Scrim);

        mainView = BuildPanel("Dialog", 420f);
        Text(mainView.transform, "Title", DSTextStyle.DisplayMD, BlockforgeTheme.InkPrimary, "Paused");
        Text(mainView.transform, "Body", DSTextStyle.Body, BlockforgeTheme.InkSecondary, "Test run on hold. Your robot and the targets are frozen.");
        var stack = Column(mainView.transform, "Buttons");
        Button(stack.transform, "Resume", DSRole.ButtonPrimary, "Resume", Resume);
        Button(stack.transform, "Options", DSRole.ButtonSecondary, "Options", ShowOptions);
        // Lance depuis l'ecran Play : on quitte le match vers le menu, sinon on revient au garage
        bool fromMenu = RobotSession.ReturnSceneName == "MainMenu";
        Button(stack.transform, "Builder", DSRole.ButtonGhost, fromMenu ? "Leave match" : "Back to builder", BackToBuilder);

        optionsView = BuildPanel("Options", 460f);
        Text(optionsView.transform, "Title", DSTextStyle.DisplayMD, BlockforgeTheme.InkPrimary, "Options");
        sensitivityValue = OptionRow(optionsView.transform, "Mouse sensitivity", out var sensRow);
        Button(sensRow.transform, "Minus", DSRole.ButtonSecondary, "−", () => ChangeSensitivity(-0.02f), 44f);
        Button(sensRow.transform, "Plus", DSRole.ButtonSecondary, "+", () => ChangeSensitivity(0.02f), 44f);
        invertLabel = OptionRow(optionsView.transform, "Invert vertical aim", out var invRow);
        Button(invRow.transform, "Toggle", DSRole.ButtonSecondary, "Toggle", ToggleInvert);
        motionLabel = OptionRow(optionsView.transform, "Reduce animations", out var motionRow);
        Button(motionRow.transform, "Toggle", DSRole.ButtonSecondary, "Toggle", ToggleMotion);
        var back = Column(optionsView.transform, "Back");
        Button(back.transform, "Back", DSRole.ButtonGhost, "Back", ShowMain);
        optionsView.SetActive(false);
        RefreshOptions();

        BuildKeyHints();
    }

    private GameObject BuildPanel(string name, float width)
    {
        var panel = NewImage(transform, name);
        var rect = panel.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(width, 0f);
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
        return panel.gameObject;
    }

    private static GameObject Column(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        var layout = go.AddComponent<VerticalLayoutGroup>();
        layout.spacing = BlockforgeTheme.Space2;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        return go;
    }

    // Ligne d'option : libelle a gauche, valeur, puis les boutons a droite
    private static TMP_Text OptionRow(Transform parent, string label, out GameObject buttons)
    {
        var row = new GameObject(label, typeof(RectTransform));
        row.transform.SetParent(parent, false);
        var layout = row.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = BlockforgeTheme.Space2;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        var caption = Text(row.transform, "Label", DSTextStyle.Body, BlockforgeTheme.InkPrimary, label);
        caption.alignment = TextAlignmentOptions.MidlineLeft;
        caption.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        var value = Text(row.transform, "Value", DSTextStyle.BodyStrong, BlockforgeTheme.Accent, "");
        value.alignment = TextAlignmentOptions.MidlineRight;
        value.gameObject.AddComponent<LayoutElement>().minWidth = 48f;
        buttons = new GameObject("Buttons", typeof(RectTransform));
        buttons.transform.SetParent(row.transform, false);
        var bl = buttons.AddComponent<HorizontalLayoutGroup>();
        bl.spacing = BlockforgeTheme.Space1;
        bl.childControlWidth = true;
        bl.childControlHeight = true;
        bl.childForceExpandWidth = false;
        bl.childForceExpandHeight = false;
        return value;
    }

    // Rappel des touches en bas a gauche (KeyCap du DS)
    private void BuildKeyHints()
    {
        var plate = NewImage(transform, "KeyHints");
        var rect = plate.rectTransform;
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 0f);
        rect.anchoredPosition = new Vector2(24f, 24f);
        rect.sizeDelta = new Vector2(360f, 0f);
        var layout = plate.gameObject.AddComponent<VerticalLayoutGroup>();
        int pad = Mathf.RoundToInt(BlockforgeTheme.Space4);
        layout.padding = new RectOffset(pad, pad, pad, pad);
        layout.spacing = BlockforgeTheme.Space2;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        plate.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        Element(plate.gameObject, DSRole.PanelFloating);

        var title = Text(plate.transform, "Title", DSTextStyle.Label, BlockforgeTheme.InkMuted, "Controls");
        foreach (var (keys, what) in new[]
                 {
                     ("Z|Q|S|D", "Drive"), ("Shift", "Boost"), ("Space|Ctrl", "Up / down (rotors, thrusters)"),
                     ("Mouse", "Aim"), ("Left click", "Fire"), ("Right click", "Zoom"), ("1|2|3", "Weapon slot"), ("R", "Self-right"),
                     ("F5", "Next demo robot"), ("Esc", "This menu"),
                 })
        {
            var row = new GameObject(keys, typeof(RectTransform));
            row.transform.SetParent(plate.transform, false);
            var rl = row.AddComponent<HorizontalLayoutGroup>();
            rl.spacing = BlockforgeTheme.Space2;
            rl.childAlignment = TextAnchor.MiddleLeft;
            rl.childControlWidth = true;
            rl.childControlHeight = true;
            rl.childForceExpandWidth = false;
            rl.childForceExpandHeight = false;
            foreach (var key in keys.Split('|'))
            {
                var cap = NewImage(row.transform, "Key_" + key);
                var capLayout = cap.gameObject.AddComponent<HorizontalLayoutGroup>();
                capLayout.padding = new RectOffset(6, 6, 2, 2);
                capLayout.childAlignment = TextAnchor.MiddleCenter;
                capLayout.childControlWidth = true;
                capLayout.childControlHeight = true;
                var le = cap.gameObject.AddComponent<LayoutElement>();
                le.minHeight = 22f;
                le.minWidth = 22f;
                var t = Text(cap.transform, "Label", DSTextStyle.Key, BlockforgeTheme.InkPrimary, key);
                t.alignment = TextAlignmentOptions.Center;
                t.textWrappingMode = TextWrappingModes.NoWrap;
                Element(cap.gameObject, DSRole.KeyCap, t);
            }
            var desc = Text(row.transform, "What", DSTextStyle.BodySM, BlockforgeTheme.InkSecondary, what);
            desc.alignment = TextAlignmentOptions.MidlineLeft;
            desc.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1f;
        }
    }

    private static Button Button(Transform parent, string objectName, DSRole role, string label, UnityEngine.Events.UnityAction onClick, float minWidth = 96f)
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
        le.minWidth = minWidth;
        var text = Text(go.transform, "Label", DSTextStyle.ButtonMD, BlockforgeTheme.InkPrimary, label);
        text.alignment = TextAlignmentOptions.Center;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        Element(go, role, text);
        button.onClick.AddListener(onClick);
        return button;
    }

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
        img.raycastTarget = true;
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
