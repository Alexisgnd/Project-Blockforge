using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// =========================================================
// COMPTE A REBOURS AVANT LE MATCH (theme hud du DS)
// Affiche pendant TdmPhase.Countdown (15 s), les robots poses et
// geles (MatchGate), la camera libre ; aucun autre element du
// HUD de combat n'est visible. Compose avec les regles du HUD et
// la plaque « Respawn in » du canvas Spectate :
// - en haut au centre, une plaque surface-hud aux coins hauts
//   coupes (hud-chamfer) : le mode et la carte en label, « Match
//   starts in » + le temps en mono (passe en accent dans les 3
//   dernieres secondes : seule la valeur change), et une jauge
//   de 6 px qui se remplit des deux bords vers le centre ;
// - dessous, l'objectif en une phrase ;
// - en bas au centre, les touches utiles (KeyCap du DS) sur une
//   plaque aux coins bas coupes.
// Au depart : « Fight » en accent 0,9 s, puis fondu de 220 ms et
// l'ecran se detruit (le HUD de combat apparait en dessous).
// Animations reduites : fondus seulement.
// =========================================================

public class MatchCountdownUI : MonoBehaviour
{
    private const float Edge = 16f;
    private const float PlateWidth = 420f, PlateHeight = 120f, GaugeWidth = 320f, GaugeHeight = 6f;

    private TdmMatch match;
    private readonly UIKit kit = UIKit.Hud;
    private CanvasGroup group;
    private TextMeshProUGUI caption, figure;
    private RectTransform gaugeLeft, gaugeRight;
    private int shownSeconds = -1;
    private bool finishing;

    public static MatchCountdownUI Create(TdmMatch match)
    {
        var go = new GameObject("[MatchCountdown]", typeof(RectTransform));
        var ui = go.AddComponent<MatchCountdownUI>();
        ui.match = match;
        ui.Build();
        return ui;
    }

    private TextMeshProUGUI HudText(Transform parent, string name, FontFamily family, float size, int weight, Color color, string value,
                                    TextAlignmentOptions align, bool caps = false, float tracking = 0f)
    {
        var t = kit.Text(parent, name, new TypeSpec(family, size, size, weight, tracking), color, value, align, caps);
        UIKit.HudShadow(t);
        return t;
    }

    private void Build()
    {
        UIKit.ScreenCanvas(gameObject, BlockforgeTheme.Order.Hud + 2);
        GetComponent<GraphicRaycaster>().enabled = false;
        var root = (RectTransform)transform;
        var P = kit.P;
        group = kit.Group(root);
        group.alpha = 0f;
        group.blocksRaycasts = false;

        // ---------- Plaque du haut ----------
        var plate = ChamferedPlate.Build(kit, root, "Countdown", PlateCorners.Top, BlockforgeTheme.HudChamfer, P.SurfaceHud);
        UIKit.Place(plate.Rect, UIKit.TopCenter, new Vector2(0f, -Edge), new Vector2(PlateWidth, PlateHeight));
        var c = plate.Content;

        var meta = HudText(c, "Mode", FontFamily.Sans, 12f, 600, P.InkSecondary, $"{match.ModeTitle} · {match.MapName}",
                           TextAlignmentOptions.Center, true, 0.18f);
        UIKit.Place(meta.rectTransform, UIKit.TopCenter, new Vector2(0f, -14f), new Vector2(PlateWidth - 40f, 16f));

        caption = HudText(c, "Caption", FontFamily.Sans, 12f, 500, P.InkSecondary, "Match starts in", TextAlignmentOptions.MidlineRight, true, 0.14f);
        UIKit.Place(caption.rectTransform, UIKit.TopCenter, new Vector2(-62f, -38f), new Vector2(200f, 44f));
        figure = HudText(c, "Time", FontFamily.Mono, 40f, 600, P.InkPrimary, "0:15", TextAlignmentOptions.MidlineLeft);
        UIKit.Place(figure.rectTransform, UIKit.TopCenter, new Vector2(84f, -38f), new Vector2(150f, 44f));

        var track = kit.Polygon(c, "Gauge");
        UIKit.Place(track.rectTransform, UIKit.TopCenter, new Vector2(0f, -(PlateHeight - 20f - GaugeHeight)), new Vector2(GaugeWidth, GaugeHeight));
        track.cutBottomLeft = track.cutBottomRight = 4f;
        track.SetSolid(P.SurfaceBase);
        gaugeLeft = kit.Image(track.rectTransform, "FillLeft", P.Accent).rectTransform;
        UIKit.Place(gaugeLeft, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        gaugeRight = kit.Image(track.rectTransform, "FillRight", P.Accent).rectTransform;
        UIKit.Place(gaugeRight, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);

        // ---------- Objectif ----------
        var goal = HudText(root, "Objective", FontFamily.Sans, 15f, 500, P.InkPrimary,
                           $"Destroy enemy robots. The first team to {BlockforgeTheme.Num(match.ScoreToWin)} wins.", TextAlignmentOptions.Center);
        UIKit.Place(goal.rectTransform, UIKit.TopCenter, new Vector2(0f, -(Edge + PlateHeight + BlockforgeTheme.Space3)), new Vector2(700f, 22f));

        // ---------- Touches ----------
        BuildKeys(root);

        StartCoroutine(UIAnim.Fade(group, 1f, BlockforgeTheme.Fade(BlockforgeTheme.MotionPanel)));
        match.PhaseChanged += OnPhase;
    }

    private void BuildKeys(RectTransform root)
    {
        var P = kit.P;
        var plate = ChamferedPlate.Build(kit, root, "Keys", PlateCorners.Bottom, BlockforgeTheme.HudChamfer, P.SurfaceHud);
        UIKit.Place(plate.Rect, UIKit.BottomCenter, new Vector2(0f, Edge), new Vector2(0f, 40f));
        var layout = plate.Rect.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = BlockforgeTheme.Space2;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.padding = UIKit.Pad(BlockforgeTheme.Space4, BlockforgeTheme.Space4, BlockforgeTheme.Space2, BlockforgeTheme.Space2);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        UIKit.Fit(plate.Rect, true, false);

        var kb = Keyboard.current;
        string Key(UnityEngine.InputSystem.Controls.KeyControl k, string fallback) =>
            k != null && !string.IsNullOrEmpty(k.displayName) ? k.displayName.ToUpperInvariant() : fallback;
        string up = Key(kb?.wKey, "W"), left = Key(kb?.aKey, "A"), down = Key(kb?.sKey, "S"), right = Key(kb?.dKey, "D");

        void Group(string label, params string[] keys)
        {
            foreach (var k in keys)
                kit.KeyCap(plate.Rect, k);
            var t = HudText(plate.Rect, "Label_" + label, FontFamily.Sans, 12f, 500, P.InkSecondary, label, TextAlignmentOptions.MidlineLeft, true, 0.08f);
            UIKit.Fixed(t, UIKit.PreferredWidth(t) + 2f, BlockforgeTheme.Controls.KeyCap);
            kit.Gap(plate.Rect, BlockforgeTheme.Space3);
        }
        Group("Drive", up, left, down, right);
        Group("Aim", "Mouse");
        Group("Fire", "LMB");
        Group("Weapons", "1", "2", "3");
        Group("Scores", "Tab");
    }

    private void OnDestroy()
    {
        if (match != null)
            match.PhaseChanged -= OnPhase;
    }

    private void OnPhase(TdmPhase phase)
    {
        if (phase != TdmPhase.Countdown && !finishing)
        {
            finishing = true;
            StartCoroutine(Finish(phase == TdmPhase.Live));
        }
    }

    private IEnumerator Finish(bool started)
    {
        if (started)
        {
            var P = kit.P;
            caption.text = "Match started";
            figure.font = DSAssets.Font(DSAssets.FontFor(FontFamily.Condensed, 700));
            figure.fontSharedMaterial = figure.font != null ? figure.font.material : figure.fontSharedMaterial;
            UIKit.HudShadow(figure);
            figure.text = "FIGHT";
            figure.color = P.Accent;
            gaugeLeft.sizeDelta = new Vector2(GaugeWidth * 0.5f, 0f);
            gaugeRight.sizeDelta = new Vector2(GaugeWidth * 0.5f, 0f);
            yield return new WaitForSeconds(0.9f);
        }
        yield return UIAnim.Fade(group, 0f, BlockforgeTheme.Fade(BlockforgeTheme.MotionScreen));
        Destroy(gameObject);
    }

    private void Update()
    {
        if (match == null || finishing || match.Phase != TdmPhase.Countdown)
            return;
        float left = match.CountdownLeft;
        int seconds = Mathf.CeilToInt(left);
        if (seconds != shownSeconds)
        {
            shownSeconds = seconds;
            figure.text = BlockforgeTheme.Clock(System.TimeSpan.FromSeconds(seconds));
            figure.color = seconds <= 3 ? kit.P.Accent : kit.P.InkPrimary;
        }
        // La jauge se remplit des deux bords vers le centre
        float k = match.CountdownSeconds > 0f ? Mathf.Clamp01(1f - left / match.CountdownSeconds) : 1f;
        gaugeLeft.sizeDelta = new Vector2(GaugeWidth * 0.5f * k, 0f);
        gaugeRight.sizeDelta = new Vector2(GaugeWidth * 0.5f * k, 0f);
    }
}
