using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// =========================================================
// SPECTATEUR D'UN MATCH DE BOTS (scene de test Map_MARS_Spectate)
// Cree par TdmMatch quand spectatorOnly est coche : aucun robot
// n'est pilote par le joueur, les 10 robots sont des bots, et la
// camera est a lui :
// - Camera sur un bot : RobotFollowCamera (la camera de match,
//   souris pour tourner autour, clic droit pour zoomer) sur le
//   bot choisi ; fleches gauche / droite (ou molette) pour passer
//   au bot precedent / suivant dans l'ordre du tableau, equipe
//   bleue puis rouge. Quand il est detruit, la camera reste sur
//   l'epave, puis le suit a sa reapparition.
// - Camera libre : F. Deplacement ZQSD / WASD (touches physiques),
//   Espace monte, Ctrl descend, Shift x4, souris pour regarder.
//   F de nouveau : retour sur le bot.
// - Clic gauche : capture la souris ; Echap : la libere.
// HUD (theme hud du DS, regles du HUD de combat) : en bas au
// centre, une plaque surface-hud aux coins bas coupes avec la
// marque d'equipe, le nom du bot, son robot et son style, et les
// touches (KeyCap). Le reste de l'interface de match (rosters,
// score, compte a rebours, Tab, fin de match) est celui du match.
// =========================================================

public class BotSpectatorView : MonoBehaviour
{
    private const float Edge = 16f;

    private TdmMatch match;
    private RobotFollowCamera follow;
    private Camera cam;
    private readonly UIKit kit = UIKit.Hud;

    private readonly List<TdmParticipant> order = new();
    private int index;
    private bool free;
    private float yaw, pitch;
    private TdmParticipant shownBot;

    private TextMeshProUGUI title, subtitle, modeLabel;
    private Image mark;
    private RectTransform markHost;

    public static BotSpectatorView Create(TdmMatch match)
    {
        var go = new GameObject("[BotSpectator]", typeof(RectTransform));
        var view = go.AddComponent<BotSpectatorView>();
        view.match = match;
        view.follow = match.followCamera;
        view.cam = Camera.main;
        view.Build();
        return view;
    }

    private void Start()
    {
        // Equipe bleue (celle que le HUD met a gauche) puis rouge
        foreach (var p in match.Participants)
            if (match.IsAlly(p)) order.Add(p);
        foreach (var p in match.Participants)
            if (!match.IsAlly(p)) order.Add(p);
        match.Spawned += OnSpawned;
        RobotTestDriver.SetCursorLocked(true);
        Watch(0);
    }

    private void OnDestroy()
    {
        if (match != null)
            match.Spawned -= OnSpawned;
    }

    // ---------- Choix du bot ----------

    private TdmParticipant Current => order.Count > 0 ? order[Mathf.Clamp(index, 0, order.Count - 1)] : null;

    private void Watch(int i)
    {
        if (order.Count == 0)
            return;
        index = (i % order.Count + order.Count) % order.Count;
        free = false;
        if (follow != null)
        {
            follow.enabled = true;
            var p = Current;
            if (p != null && p.Robot != null)
                follow.SetTarget(p.Robot.transform);
        }
        Refresh();
    }

    private void OnSpawned(TdmParticipant p)
    {
        if (!free && p == Current && follow != null && p.Robot != null)
            follow.SetTarget(p.Robot.transform);
        if (p == shownBot)
            Refresh();
    }

    private void ToggleFree()
    {
        free = !free;
        if (free)
        {
            if (follow != null)
                follow.enabled = false;
            var e = cam != null ? cam.transform.eulerAngles : Vector3.zero;
            yaw = e.y;
            pitch = e.x > 180f ? e.x - 360f : e.x;
        }
        else
        {
            Watch(index);
        }
        Refresh();
    }

    // ---------- Entrees ----------

    private void Update()
    {
        var kb = Keyboard.current;
        var mouse = Mouse.current;
        if (kb == null)
            return;

        if (kb.escapeKey.wasPressedThisFrame)
            RobotTestDriver.SetCursorLocked(false);
        else if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
            RobotTestDriver.SetCursorLocked(true);

        if (kb.fKey.wasPressedThisFrame)
            ToggleFree();
        float wheel = mouse != null ? mouse.scroll.ReadValue().y : 0f;
        if (kb.rightArrowKey.wasPressedThisFrame || wheel < -0.1f)
            Watch(index + 1);
        else if (kb.leftArrowKey.wasPressedThisFrame || wheel > 0.1f)
            Watch(index - 1);

        if (free)
            FlyCamera(kb, mouse);
        else if (Time.frameCount % 30 == 0)
            Refresh();   // le style du bot change en cours de partie
        else if (follow != null && follow.target == null)
        {
            // L'epave a disparu : on reste sur place en attendant la reapparition
        }
    }

    private void FlyCamera(Keyboard kb, Mouse mouse)
    {
        if (cam == null)
            return;
        bool locked = Cursor.lockState == CursorLockMode.Locked;
        if (locked && mouse != null)
        {
            Vector2 d = mouse.delta.ReadValue() * MatchPauseMenu.Sensitivity;
            yaw += d.x;
            pitch = Mathf.Clamp(pitch + (MatchPauseMenu.InvertY ? d.y : -d.y), -85f, 85f);
        }
        var rot = Quaternion.Euler(pitch, yaw, 0f);

        Vector3 move = Vector3.zero;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed) move += Vector3.forward;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed) move -= Vector3.forward;
        if (kb.dKey.isPressed) move += Vector3.right;
        if (kb.aKey.isPressed) move -= Vector3.right;
        if (kb.spaceKey.isPressed) move += Vector3.up;
        if (kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed) move -= Vector3.up;
        float speed = (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed) ? 120f : 30f;
        var t = cam.transform;
        t.rotation = rot;
        t.position += rot * move.normalized * (speed * Time.unscaledDeltaTime);
    }

    // ---------- HUD ----------

    private TextMeshProUGUI HudText(Transform parent, string name, FontFamily family, float size, int weight, Color color, string value,
                                    TextAlignmentOptions align, bool caps = false, float tracking = 0f)
    {
        var t = kit.Text(parent, name, new TypeSpec(family, size, size, weight, tracking), color, value, align, caps);
        UIKit.HudShadow(t);
        return t;
    }

    private void Build()
    {
        UIKit.ScreenCanvas(gameObject, BlockforgeTheme.Order.Hud + 1);
        GetComponent<GraphicRaycaster>().enabled = false;
        var root = (RectTransform)transform;
        var P = kit.P;

        // Plaque du bot regarde
        var plate = ChamferedPlate.Build(kit, root, "Watching", PlateCorners.Bottom, BlockforgeTheme.HudChamfer, P.SurfaceHud);
        UIKit.Place(plate.Rect, UIKit.BottomCenter, new Vector2(0f, Edge + 48f), new Vector2(440f, 64f));
        var c = plate.Content;
        markHost = kit.Rect(c, "Mark");
        UIKit.Place(markHost, UIKit.MiddleLeft, new Vector2(Edge, 0f), new Vector2(24f, 24f));
        modeLabel = HudText(c, "Mode", FontFamily.Sans, 11f, 600, P.InkSecondary, "Spectating", TextAlignmentOptions.MidlineLeft, true, 0.18f);
        UIKit.Place(modeLabel.rectTransform, UIKit.TopLeft, new Vector2(Edge + 34f, -10f), new Vector2(380f, 14f));
        title = HudText(c, "Name", FontFamily.Sans, 18f, 700, P.InkPrimary, "", TextAlignmentOptions.MidlineLeft);
        UIKit.Place(title.rectTransform, UIKit.TopLeft, new Vector2(Edge + 34f, -24f), new Vector2(380f, 22f));
        subtitle = HudText(c, "Robot", FontFamily.Mono, 12f, 500, P.InkSecondary, "", TextAlignmentOptions.MidlineLeft);
        UIKit.Place(subtitle.rectTransform, UIKit.TopLeft, new Vector2(Edge + 34f, -44f), new Vector2(380f, 14f));

        // Touches
        var keys = ChamferedPlate.Build(kit, root, "Keys", PlateCorners.Bottom, BlockforgeTheme.HudChamfer, P.SurfaceHud);
        UIKit.Place(keys.Rect, UIKit.BottomCenter, new Vector2(0f, Edge), new Vector2(0f, 40f));
        var layout = keys.Rect.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.spacing = BlockforgeTheme.Space2;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.padding = UIKit.Pad(BlockforgeTheme.Space4, BlockforgeTheme.Space4, BlockforgeTheme.Space2, BlockforgeTheme.Space2);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        UIKit.Fit(keys.Rect, true, false);

        var kb = Keyboard.current;
        string Key(UnityEngine.InputSystem.Controls.KeyControl k, string fallback) =>
            k != null && !string.IsNullOrEmpty(k.displayName) ? k.displayName.ToUpperInvariant() : fallback;
        void Group(string label, params string[] ks)
        {
            foreach (var k in ks)
                kit.KeyCap(keys.Rect, k);
            var t = HudText(keys.Rect, "Label_" + label, FontFamily.Sans, 12f, 500, P.InkSecondary, label, TextAlignmentOptions.MidlineLeft, true, 0.08f);
            UIKit.Fixed(t, UIKit.PreferredWidth(t) + 2f, BlockforgeTheme.Controls.KeyCap);
            kit.Gap(keys.Rect, BlockforgeTheme.Space3);
        }
        Group("Switch bot", "←", "→");
        Group("Free camera", "F");
        Group("Fly", Key(kb?.wKey, "W"), Key(kb?.aKey, "A"), Key(kb?.sKey, "S"), Key(kb?.dKey, "D"), "Space", "Ctrl");
        Group("Fast", "Shift");
        Group("Scores", "Tab");
    }

    private void Refresh()
    {
        var p = Current;
        shownBot = p;
        if (mark != null)
            Destroy(mark.gameObject);
        mark = null;
        if (free)
        {
            modeLabel.text = "Free camera";
            title.text = p != null ? $"Press F to return to {p.Name}" : "";
            subtitle.text = "";
            return;
        }
        if (p == null)
            return;
        modeLabel.text = "Spectating";
        title.text = p.Name;
        string style = p.Personality != null ? p.Personality.Style.ToString() : "";
        subtitle.text = string.IsNullOrEmpty(style) ? p.RobotName : $"{p.RobotName} · {style}";
        mark = kit.TeamMark(markHost, match.TeamOf(p), 22f);
    }
}
