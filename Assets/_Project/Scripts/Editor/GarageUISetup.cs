using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

// =========================================================
// SETUP DU HUD DE LA SCENE GARAGE
// Menu : Blockforge > Setup Garage UI
// - Panneau capacites (gauche) + slot courant
// - Barre d'outils 1-5 (bas) : pince, couleur, suppr...
// - Liste des commandes (droite)
// Ne touche a rien d'autre dans la scene.
// =========================================================

public static class GarageUISetup
{
    private const string ScenePath = "Assets/_Project/Scenes/Garage.unity";

    private static readonly Color PanelBg = new(0.04f, 0.07f, 0.12f, 0.88f);
    private static readonly Color CardBg = new(0.06f, 0.10f, 0.16f, 0.95f);
    private static readonly Color BarBg = new(0.10f, 0.15f, 0.22f, 1f);
    private static readonly Color Accent = new(0.23f, 0.51f, 0.96f);
    private static readonly Color ChipBg = new(0.08f, 0.12f, 0.18f, 0.92f);
    private static readonly Color TextMain = new(0.86f, 0.91f, 0.96f);
    private static readonly Color TextDim = new(0.55f, 0.63f, 0.72f);

    [MenuItem("Blockforge/Setup Garage UI")]
    public static void Setup()
    {
        if (TMP_Settings.instance == null)
        {
            Debug.LogError("[GarageUISetup] TextMeshPro n'est pas initialise. " +
                           "Window > TextMeshPro > Import TMP Essential Resources, puis relance.");
            return;
        }

        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
            return;

        var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        var oldCanvas = GameObject.Find("GarageCanvas");
        if (oldCanvas != null)
            Object.DestroyImmediate(oldCanvas);

        if (Object.FindFirstObjectByType<EventSystem>() == null)
        {
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<InputSystemUIInputModule>();
        }

        // ---------- Canvas ----------
        var canvasGo = new GameObject("GarageCanvas");
        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGo.AddComponent<GraphicRaycaster>();

        BuildStatsPanel(canvasGo.transform);
        BuildToolbar(canvasGo.transform);
        BuildControlsPanel(canvasGo.transform);

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("[GarageUISetup] HUD du garage genere (stats, outils, commandes).");
    }

    // =========================================================
    // PANNEAU CAPACITES (GAUCHE)
    // =========================================================

    private static void BuildStatsPanel(Transform canvas)
    {
        var panel = CreatePanel(canvas, "StatsPanel", PanelBg);
        SetAnchors(panel, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
        panel.sizeDelta = new Vector2(400, 236);
        panel.anchoredPosition = new Vector2(30, -30);

        var hud = panel.gameObject.AddComponent<GarageStatsHUD>();

        var rows = new (string label, string fillField, string valueField)[]
        {
            ("CAPACITÉ", nameof(GarageStatsHUD.capacityFill), nameof(GarageStatsHUD.capacityValue)),
            ("SANTÉ", nameof(GarageStatsHUD.healthFill), nameof(GarageStatsHUD.healthValue)),
            ("VITESSE", nameof(GarageStatsHUD.speedFill), nameof(GarageStatsHUD.speedValue)),
            ("PUISSANCE", nameof(GarageStatsHUD.powerFill), nameof(GarageStatsHUD.powerValue)),
            ("POIDS", nameof(GarageStatsHUD.weightFill), nameof(GarageStatsHUD.weightValue)),
        };

        for (int i = 0; i < rows.Length; i++)
        {
            float y = -18f - i * 42f;

            var label = CreateText(panel, $"Row_{i}_Label", rows[i].label, 13, TextDim, TextAlignmentOptions.Left, FontStyles.Bold);
            SetAnchors(label, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
            label.sizeDelta = new Vector2(120, 30);
            label.anchoredPosition = new Vector2(18, y);

            // Barre : fond + remplissage (le script regle anchorMax.x du fill)
            var barBg = CreatePanel(panel, $"Row_{i}_BarBg", BarBg);
            SetAnchors(barBg, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
            barBg.sizeDelta = new Vector2(110, 8);
            barBg.anchoredPosition = new Vector2(146, y - 11);

            var fill = CreatePanel(barBg, "Fill", Accent);
            fill.anchorMin = Vector2.zero;
            fill.anchorMax = new Vector2(0.5f, 1);
            fill.offsetMin = Vector2.zero;
            fill.offsetMax = Vector2.zero;

            var value = CreateText(panel, $"Row_{i}_Value", "—", 14, TextMain, TextAlignmentOptions.Right, FontStyles.Bold);
            SetAnchors(value, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
            value.offsetMin = new Vector2(266, y - 30);
            value.offsetMax = new Vector2(-18, y);

            typeof(GarageStatsHUD).GetField(rows[i].fillField).SetValue(hud, fill);
            typeof(GarageStatsHUD).GetField(rows[i].valueField).SetValue(hud, value.GetComponent<TMP_Text>());
        }

        // ---------- Bandeau du slot courant ----------
        var slotBar = CreatePanel(canvas, "SlotBar", PanelBg);
        SetAnchors(slotBar, new Vector2(0, 1), new Vector2(0, 1), new Vector2(0, 1));
        slotBar.sizeDelta = new Vector2(400, 46);
        slotBar.anchoredPosition = new Vector2(30, -278);

        var slotText = CreateText(slotBar, "Text", "SLOT 1/3 – PRÉDATEUR MK1", 14, TextMain, TextAlignmentOptions.Left, FontStyles.Bold);
        SetAnchors(slotText, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f));
        slotText.offsetMin = new Vector2(18, 0);
        slotText.offsetMax = new Vector2(-60, 0);
        hud.slotText = slotText.GetComponent<TMP_Text>();

        var (editBtn, _) = CreateButton(slotBar, "EditButton", "EDIT", 11, ChipBg, TextMain);
        var editRect = editBtn.GetComponent<RectTransform>();
        SetAnchors(editRect, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(1, 0.5f));
        editRect.sizeDelta = new Vector2(46, 30);
        editRect.anchoredPosition = new Vector2(-10, 0);
    }

    // =========================================================
    // BARRE D'OUTILS (BAS)
    // =========================================================

    private static void BuildToolbar(Transform canvas)
    {
        var tools = new[] { "PINCE", "COULEUR" };

        var container = CreateUI(canvas, "Toolbar");
        SetAnchors(container, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
        container.sizeDelta = new Vector2(tools.Length * 78f, 86);
        container.anchoredPosition = new Vector2(0, 24);

        var toolbar = container.gameObject.AddComponent<GarageToolbar>();
        toolbar.buttons = new Button[tools.Length];
        toolbar.backgrounds = new Image[tools.Length];
        toolbar.outlines = new Outline[tools.Length];

        for (int i = 0; i < tools.Length; i++)
        {
            var slot = CreatePanel(container, $"Tool_{i + 1}", CardBg);
            SetAnchors(slot, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f));
            slot.sizeDelta = new Vector2(68, 68);
            slot.anchoredPosition = new Vector2((i - (tools.Length - 1) * 0.5f) * 78f, 6);

            var button = slot.gameObject.AddComponent<Button>();
            button.targetGraphic = slot.GetComponent<Image>();

            var outline = slot.gameObject.AddComponent<Outline>();
            outline.effectColor = Accent;
            outline.effectDistance = new Vector2(2, 2);
            outline.enabled = false;

            // Libelle en attendant les icones (glisse un sprite plus tard)
            var label = CreateText(slot, "Label", tools[i], 11, TextMain, TextAlignmentOptions.Center, FontStyles.Bold);
            SetAnchors(label, new Vector2(0, 0), new Vector2(1, 1), new Vector2(0.5f, 0.5f));
            label.offsetMin = new Vector2(2, 14);
            label.offsetMax = new Vector2(-2, -6);

            // Badge de touche (1-5)
            var badge = CreatePanel(slot, "KeyBadge", ChipBg);
            SetAnchors(badge, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0.5f, 0));
            badge.sizeDelta = new Vector2(22, 20);
            badge.anchoredPosition = new Vector2(0, -8);
            var badgeText = CreateText(badge, "Text", (i + 1).ToString(), 11, TextMain, TextAlignmentOptions.Center, FontStyles.Bold);
            StretchFull(badgeText);

            toolbar.buttons[i] = button;
            toolbar.backgrounds[i] = slot.GetComponent<Image>();
            toolbar.outlines[i] = outline;
        }
    }

    // =========================================================
    // COMMANDES (DROITE)
    // =========================================================

    private static void BuildControlsPanel(Transform canvas)
    {
        var groups = new (string action, string key)[][]
        {
            new[] { ("AVANCER", "Z"), ("RECULER", "S"), ("GAUCHE", "Q"), ("DROITE", "D") },
            new[] { ("INVENTAIRE", "TAB"), ("AJOUTER", "CLIC G"), ("SUPPRIMER", "CLIC D"), ("PIVOTER", "MOLETTE") },
            new[] { ("MONTER", "ESPACE"), ("DESCENDRE", "CTRL") },
            new[] { ("SAUVER & TESTER", "T"), ("RETOUR", "ESC") },
        };

        var container = CreateUI(canvas, "ControlsPanel");
        SetAnchors(container, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
        container.sizeDelta = new Vector2(280, 600);
        container.anchoredPosition = new Vector2(-30, -24);

        float y = 0f;
        foreach (var group in groups)
        {
            foreach (var (action, key) in group)
            {
                var label = CreateText(container, $"Ctrl_{action}", action, 14, TextMain, TextAlignmentOptions.Left, FontStyles.Bold);
                SetAnchors(label, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1));
                label.offsetMin = new Vector2(0, y - 26);
                label.offsetMax = new Vector2(-80, y);

                float chipW = Mathf.Max(34f, 20f + key.Length * 9f);
                var chip = CreatePanel(container, $"Key_{action}", ChipBg);
                SetAnchors(chip, new Vector2(1, 1), new Vector2(1, 1), new Vector2(1, 1));
                chip.sizeDelta = new Vector2(chipW, 24);
                chip.anchoredPosition = new Vector2(0, y - 1);
                var chipText = CreateText(chip, "Text", key, 11, TextMain, TextAlignmentOptions.Center, FontStyles.Bold);
                StretchFull(chipText);

                y -= 30f;
            }
            y -= 14f; // espace entre les groupes
        }
    }

    // =========================================================
    // HELPERS UI
    // =========================================================

    private static RectTransform CreateUI(Transform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        go.layer = LayerMask.NameToLayer("UI");
        return (RectTransform)go.transform;
    }

    private static RectTransform CreateUI(Component parent, string name) => CreateUI(parent.transform, name);

    private static RectTransform CreatePanel(Transform parent, string name, Color color)
    {
        var rect = CreateUI(parent, name);
        rect.gameObject.AddComponent<Image>().color = color;
        return rect;
    }

    private static RectTransform CreatePanel(Component parent, string name, Color color) => CreatePanel(parent.transform, name, color);

    private static RectTransform CreateText(Component parent, string name, string text, float size,
                                            Color color, TextAlignmentOptions align, FontStyles style)
    {
        var rect = CreateUI(parent.transform, name);
        var tmp = rect.gameObject.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = align;
        tmp.fontStyle = style;
        tmp.raycastTarget = false;
        return rect;
    }

    private static (Button, TMP_Text) CreateButton(Component parent, string name, string label, float fontSize,
                                                   Color bgColor, Color textColor)
    {
        var rect = CreatePanel(parent.transform, name, bgColor);
        var button = rect.gameObject.AddComponent<Button>();
        button.targetGraphic = rect.GetComponent<Image>();
        var textRect = CreateText(rect, "Text", label, fontSize, textColor, TextAlignmentOptions.Center, FontStyles.Bold);
        StretchFull(textRect);
        return (button, textRect.GetComponent<TMP_Text>());
    }

    private static void SetAnchors(RectTransform rect, Vector2 min, Vector2 max, Vector2 pivot)
    {
        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.pivot = pivot;
    }

    private static void StretchFull(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
