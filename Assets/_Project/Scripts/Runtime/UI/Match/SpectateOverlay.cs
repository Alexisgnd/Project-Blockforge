using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.UI;

// =========================================================
// ROBOT DETRUIT, SPECTATEUR ET REAPPARITION (canvas « Blockforge
// — Spectate », theme hud du DS)
// Le robot du joueur est detruit (TdmMatch.Died) :
// 1. 0-2 s : vignette team-enemy qui s'efface en 600 ms, voile
//    leger ; carte du tueur au centre (« Robot destroyed », nom en
//    condense team-enemy, arme · distance) ; en bas a gauche le
//    bloc sante devient « Destroyed » (crane danger-ink) ; en bas
//    au centre « Spectating in 2 » et Espace pour passer.
// 2. Spectateur : camera du coequipier vivant le plus proche
//    (RobotFollowCamera), deux bandes accent de 4 px en haut et en
//    bas (« ce n'est pas ta camera »), etiquette « Spectating »
//    sous le score ; barre du spectateur en bas : Q / E (touches
//    de la disposition du clavier) passent d'un coequipier vivant a
//    l'autre dans l'ordre du roster, nom, robot, ligne de vie,
//    « Teammate 2 of 3 alive » ; « No teammate alive » quand
//    personne : la camera garde sa position. Au-dessus, « Respawn
//    in 0:08 » et une jauge qui se remplit des bords vers le
//    centre.
// 3. Pret : le bouton Respawn (accent, Espace) remplace le
//    compte a rebours, « Auto-respawn in 5 · at your team's
//    spawn » ; a 0 le match fait reapparaitre le robot.
// Reapparition (TdmMatch.Spawned du joueur) ou fin de match :
// tout se cache. Respawns illimites en Team Deathmatch : pas de
// compteur de vies. Echap reste le menu (RobotTestDriver).
// Animations reduites : fondus seulement.
// =========================================================

public class SpectateOverlay : MonoBehaviour
{
    private const float Edge = 16f;
    private const float KillCardSeconds = 2f;
    private const float GaugeWidth = 280f, GaugeHeight = 6f;

    private enum State { Hidden, Destroyed, Spectating }

    private TdmMatch match;
    private readonly UIKit kit = UIKit.Hud;
    private RectTransform root;
    private State state = State.Hidden;
    private float diedAt;

    // Groupes
    private CanvasGroup rootGroup, deathGroup, spectateGroup, countGroup, readyGroup;
    private Image vignette, veil;

    // Carte du tueur
    private TextMeshProUGUI killerName, killerWeapon, spectatingIn;
    private Image killerMark;
    private RectTransform killerWeaponRow;

    // Spectateur
    private TextMeshProUGUI watchName, watchRobot, watchPct, watchCount, countFigure, autoText;
    private RectTransform watchFill, gaugeLeft, gaugeRight, watchInfo;
    private Image watchMark, watchAvatarFrame;
    private TdmParticipant watched;
    private float watchedLostAt = -1f;
    private int shownCount = -1, shownAuto = -1;

    public static SpectateOverlay Create(TdmMatch match)
    {
        var go = new GameObject("[SpectateOverlay]", typeof(RectTransform));
        var ui = go.AddComponent<SpectateOverlay>();
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

    private static CanvasGroup Hide(CanvasGroup g)
    {
        g.alpha = 0f;
        g.interactable = false;
        g.blocksRaycasts = false;
        return g;
    }

    // =========================================================
    // CONSTRUCTION
    // =========================================================

    private void Build()
    {
        UIKit.ScreenCanvas(gameObject, BlockforgeTheme.Order.Hud + 3);
        GetComponent<GraphicRaycaster>().enabled = false;
        root = (RectTransform)transform;
        rootGroup = Hide(kit.Group(root));
        var P = kit.P;

        // ---------- Impact : vignette ennemie et voile ----------
        vignette = kit.Image(root, "Vignette", BlockforgeTheme.WithAlpha(P.TeamEnemy, 0f), HudShapes.Vignette());
        vignette.type = Image.Type.Simple;
        UIKit.Stretch(vignette.rectTransform);
        veil = kit.Image(root, "Veil", new Color(11f / 255f, 14f / 255f, 20f / 255f, 0f));
        UIKit.Stretch(veil.rectTransform);

        BuildDestroyedBlock();
        BuildDeathPhase();
        BuildSpectatePhase();

        match.Died += OnDied;
        match.Spawned += OnSpawned;
        match.PhaseChanged += OnPhase;
    }

    private void OnDestroy()
    {
        if (match == null)
            return;
        match.Died -= OnDied;
        match.Spawned -= OnSpawned;
        match.PhaseChanged -= OnPhase;
    }

    // Bas gauche : le bloc sante devient « Destroyed »
    private void BuildDestroyedBlock()
    {
        var P = kit.P;
        var plate = ChamferedPlate.Build(kit, root, "Destroyed", PlateCorners.TopRight, BlockforgeTheme.HudChamfer, P.SurfaceHud);
        UIKit.Place(plate.Rect, UIKit.BottomLeft, new Vector2(Edge, Edge), new Vector2(176f, 60f));
        var badge = kit.Image(plate.Content, "Badge", P.SurfaceBase, HudShapes.Disc());
        badge.type = Image.Type.Simple;
        UIKit.Place(badge.rectTransform, UIKit.MiddleLeft, new Vector2(8f, 0f), new Vector2(44f, 44f));
        var skull = kit.Icon(badge.rectTransform, "Skull", "skull", 20f, P.DangerInk);
        UIKit.Place(skull.rectTransform, UIKit.Center, Vector2.zero, new Vector2(20f, 20f));
        var word = HudText(plate.Content, "Word", FontFamily.Condensed, 22f, 700, P.DangerInk, "Destroyed", TextAlignmentOptions.MidlineLeft, true, 0.03f);
        UIKit.Place(word.rectTransform, UIKit.MiddleLeft, new Vector2(8f + 44f + 14f, 0f), new Vector2(110f, 30f));
    }

    // 0-2 s : carte du tueur et « Spectating in »
    private void BuildDeathPhase()
    {
        var P = kit.P;
        var phase = kit.Rect(root, "DeathPhase");
        UIKit.Stretch(phase);
        deathGroup = Hide(kit.Group(phase));

        // Carte du tueur, au centre (coins coupes, degrade plate-enemy vers le bas)
        var card = ChamferedPlate.Build(kit, phase, "KillCard", PlateCorners.All, 16f, P.SurfaceHud, P.PlateEnemy, tintVertical: true);
        UIKit.Place(card.Rect, UIKit.Center, Vector2.zero, new Vector2(0f, 0f));
        var layout = card.Rect.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.spacing = 14f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.padding = UIKit.Pad(56f, 56f, 26f, 24f);
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;
        UIKit.Fit(card.Rect, true, true);

        var over = HudText(card.Rect, "Over", FontFamily.Sans, 12f, 600, P.InkSecondary, "Robot destroyed", TextAlignmentOptions.Center, true, 0.2f);
        UIKit.Fixed(over, -1f, 14f);

        var row = kit.Row(card.Rect, "Killer", 18f, TextAnchor.MiddleCenter);
        var avatar = kit.Rect(row, "Avatar");
        UIKit.Fixed(avatar, 44f, 44f);
        var frame = kit.Shape(avatar, "Frame", DSRadius.Sm, P.TeamEnemy);
        UIKit.Stretch(frame.rectTransform);
        var ground = kit.Shape(avatar, "Ground", DSRadius.Sm, P.SurfaceBase);
        UIKit.Stretch(ground.rectTransform, 2f, 2f, 2f, 2f);
        var user = kit.Icon(avatar, "User", "user", 22f, P.InkSecondary);
        UIKit.Place(user.rectTransform, UIKit.Center, Vector2.zero, new Vector2(22f, 22f));
        var nameRow = kit.Row(row, "Name", 10f, TextAnchor.MiddleLeft);
        killerMark = kit.Icon(nameRow, "Mark", "enemy", 14f, P.TeamEnemy);
        killerName = HudText(nameRow, "Name", FontFamily.Condensed, 44f, 700, P.TeamEnemy, "", TextAlignmentOptions.MidlineLeft, true, 0.03f);
        UIKit.Fixed(killerName, -1f, 46f);

        var weaponRow = kit.Row(card.Rect, "Weapon", 10f, TextAnchor.MiddleCenter);
        killerWeaponRow = weaponRow;
        kit.Icon(weaponRow, "Icon", "weapon", 16f, P.CategoryWeapon);
        killerWeapon = HudText(weaponRow, "Label", FontFamily.Sans, 16f, 500, P.InkSecondary, "", TextAlignmentOptions.MidlineLeft);
        UIKit.Fixed(killerWeapon, -1f, 20f);

        // Bas centre : « Spectating in 2 » [Espace] Skip
        var bottom = kit.Row(phase, "SpectatingIn", 20f, TextAnchor.MiddleCenter);
        UIKit.Place(bottom, UIKit.BottomCenter, new Vector2(0f, Edge + 12f), new Vector2(0f, 36f));
        UIKit.Fit(bottom, true, false);
        var label = HudText(bottom, "Label", FontFamily.Sans, 13f, 500, P.InkSecondary, "Spectating in", TextAlignmentOptions.MidlineLeft, true, 0.1f);
        UIKit.Fixed(label, UIKit.PreferredWidth(label) + 2f, 36f);
        spectatingIn = HudText(bottom, "Seconds", FontFamily.Mono, 26f, 600, P.InkPrimary, "2", TextAlignmentOptions.Center);
        UIKit.Fixed(spectatingIn, 22f, 36f);
        var skip = kit.Row(bottom, "Skip", 10f, TextAnchor.MiddleLeft, UIKit.Pad(14f, 14f, 0f, 0f));
        kit.KeyCap(skip, "Space");
        var skipLabel = HudText(skip, "Label", FontFamily.Sans, 12f, 600, P.InkSecondary, "Skip", TextAlignmentOptions.MidlineLeft, true, 0.06f);
        UIKit.Fixed(skipLabel, UIKit.PreferredWidth(skipLabel) + 2f, 36f);
    }

    // Spectateur : bandes, etiquette, compte a rebours ou bouton, barre du coequipier
    private void BuildSpectatePhase()
    {
        var P = kit.P;
        var phase = kit.Rect(root, "SpectatePhase");
        UIKit.Stretch(phase);
        spectateGroup = Hide(kit.Group(phase));

        // Bandes accent : « ce n'est pas ta camera »
        var top = kit.Image(phase, "BandTop", BlockforgeTheme.WithAlpha(P.Accent, 0.7f));
        UIKit.TopStrip(top.rectTransform, 0f, 4f);
        var bottomBand = kit.Image(phase, "BandBottom", BlockforgeTheme.WithAlpha(P.Accent, 0.7f));
        UIKit.BottomStrip(bottomBand.rectTransform, 0f, 4f);

        // Etiquette sous le score
        var tag = kit.Polygon(phase, "SpectatingTag");
        float tagW = 150f;
        UIKit.Place(tag.rectTransform, UIKit.TopCenter, new Vector2(-5f, -70f), new Vector2(tagW - 10f, 26f));
        tag.slantLeft = 10f;
        tag.slantRight = -10f;
        tag.SetSolid(P.SurfaceHud);
        var eye = kit.Icon(phase, "Eye", "visible", 14f, P.Accent);
        UIKit.Place(eye.rectTransform, UIKit.TopCenter, new Vector2(-48f, -76f), new Vector2(14f, 14f));
        var tagText = HudText(phase, "SpectatingLabel", FontFamily.Sans, 12f, 600, P.InkSecondary, "Spectating", TextAlignmentOptions.MidlineLeft, true, 0.18f);
        UIKit.Place(tagText.rectTransform, UIKit.TopCenter, new Vector2(16f, -70f), new Vector2(110f, 26f));

        // Barre du spectateur (bas centre)
        var bar = ChamferedPlate.Build(kit, phase, "SpectatorBar", PlateCorners.Bottom, 14f, P.SurfaceHud, P.PlateAlly);
        bar.Tint.SetColors(P.PlateAlly, BlockforgeTheme.WithAlpha(P.PlateAlly, 0.15f));
        UIKit.Place(bar.Rect, UIKit.BottomCenter, new Vector2(0f, Edge + 8f), new Vector2(0f, 72f));
        var barLayout = bar.Rect.gameObject.AddComponent<HorizontalLayoutGroup>();
        barLayout.spacing = 16f;
        barLayout.childAlignment = TextAnchor.MiddleCenter;
        barLayout.padding = UIKit.Pad(14f, 14f, 10f, 10f);
        barLayout.childControlWidth = true;
        barLayout.childControlHeight = true;
        barLayout.childForceExpandWidth = false;
        barLayout.childForceExpandHeight = false;
        UIKit.Fit(bar.Rect, true, false);

        string qKey = KeyLabel("q", Keyboard.current?.qKey);
        string eKey = KeyLabel("e", Keyboard.current?.eKey);
        var prev = kit.Row(bar.Rect, "Previous", 8f, TextAnchor.MiddleCenter, UIKit.Pad(10f, 10f, 0f, 0f));
        kit.KeyCap(prev, qKey);
        kit.Icon(prev, "Chevron", "chevron-left", 18f, P.InkSecondary);

        var avatar = kit.Rect(bar.Rect, "Avatar");
        UIKit.Fixed(avatar, 52f, 52f);
        watchAvatarFrame = kit.Shape(avatar, "Frame", DSRadius.Sm, P.TeamAlly);
        UIKit.Stretch(watchAvatarFrame.rectTransform);
        var ground = kit.Shape(avatar, "Ground", DSRadius.Sm, P.SurfaceBase);
        UIKit.Stretch(ground.rectTransform, 2f, 2f, 2f, 2f);
        var user = kit.Icon(avatar, "User", "user", 24f, P.InkSecondary);
        UIKit.Place(user.rectTransform, UIKit.Center, Vector2.zero, new Vector2(24f, 24f));

        watchInfo = kit.Column(bar.Rect, "Info", 5f, TextAnchor.MiddleLeft);
        UIKit.Fixed(watchInfo, 320f, 52f);
        var nameRow = kit.Row(watchInfo, "NameRow", 10f, TextAnchor.MiddleLeft);
        UIKit.Fixed(nameRow, 320f, 28f);
        watchMark = kit.Icon(nameRow, "Mark", "ally", 12f, P.TeamAlly);
        watchName = HudText(nameRow, "Name", FontFamily.Condensed, 26f, 700, P.InkPrimary, "", TextAlignmentOptions.MidlineLeft, true, 0.03f);
        UIKit.Fixed(watchName, -1f, 28f);
        watchRobot = HudText(nameRow, "Robot", FontFamily.Sans, 14f, 400, P.InkSecondary, "", TextAlignmentOptions.MidlineLeft);
        UIKit.Fixed(watchRobot, -1f, 28f);

        var healthRow = kit.Row(watchInfo, "HealthRow", 10f, TextAnchor.MiddleLeft);
        UIKit.Fixed(healthRow, 320f, 16f);
        var track = kit.Image(healthRow, "Track", P.SurfaceBase);
        UIKit.Fixed(track, 270f, 4f);
        watchFill = kit.Image(track.rectTransform, "Fill", P.Success).rectTransform;
        UIKit.Place(watchFill, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        watchPct = HudText(healthRow, "Pct", FontFamily.Mono, 14f, 600, P.InkSecondary, "", TextAlignmentOptions.MidlineLeft);
        UIKit.Fixed(watchPct, 40f, 16f);

        var next = kit.Row(bar.Rect, "Next", 8f, TextAnchor.MiddleCenter, UIKit.Pad(10f, 10f, 0f, 0f));
        kit.Icon(next, "Chevron", "chevron-right", 18f, P.InkSecondary);
        kit.KeyCap(next, eKey);

        var sep = kit.Image(bar.Rect, "Separator", new Color(242f / 255f, 245f / 255f, 247f / 255f, 0.14f));
        UIKit.Fixed(sep, 1f, 36f);
        watchCount = HudText(bar.Rect, "Count", FontFamily.Sans, 12f, 500, P.InkSecondary, "", TextAlignmentOptions.MidlineLeft, true, 0.08f);
        UIKit.Fixed(watchCount, 210f, 36f);

        // Compte a rebours de reapparition (au-dessus de la barre)
        var count = ChamferedPlate.Build(kit, phase, "RespawnIn", PlateCorners.Top, BlockforgeTheme.HudChamfer, P.SurfaceHud);
        UIKit.Place(count.Rect, UIKit.BottomCenter, new Vector2(0f, Edge + 8f + 72f + 10f), new Vector2(328f, 62f));
        countGroup = kit.Group(count.Rect);
        var caption = HudText(count.Content, "Caption", FontFamily.Sans, 12f, 500, P.InkSecondary, "Respawn in", TextAlignmentOptions.MidlineRight, true, 0.14f);
        UIKit.Place(caption.rectTransform, UIKit.TopCenter, new Vector2(-46f, -8f), new Vector2(140f, 30f));
        countFigure = HudText(count.Content, "Time", FontFamily.Mono, 28f, 600, P.InkPrimary, "0:10", TextAlignmentOptions.MidlineLeft);
        UIKit.Place(countFigure.rectTransform, UIKit.TopCenter, new Vector2(64f, -8f), new Vector2(90f, 30f));
        var gauge = kit.Polygon(count.Content, "Gauge");
        UIKit.Place(gauge.rectTransform, UIKit.BottomCenter, new Vector2(0f, 12f), new Vector2(GaugeWidth, GaugeHeight));
        gauge.cutBottomLeft = gauge.cutBottomRight = 4f;
        gauge.SetSolid(P.SurfaceBase);
        gaugeLeft = kit.Image(gauge.rectTransform, "FillLeft", P.Accent).rectTransform;
        UIKit.Place(gaugeLeft, new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(0f, 0.5f), Vector2.zero, Vector2.zero);
        gaugeRight = kit.Image(gauge.rectTransform, "FillRight", P.Accent).rectTransform;
        UIKit.Place(gaugeRight, new Vector2(1f, 0f), new Vector2(1f, 1f), new Vector2(1f, 0.5f), Vector2.zero, Vector2.zero);

        // Pret : bouton Respawn + reapparition automatique
        var ready = kit.Column(phase, "Ready", 8f, TextAnchor.MiddleCenter, null, true, true, false);
        UIKit.Place(ready, UIKit.BottomCenter, new Vector2(0f, Edge + 8f + 72f + 10f), new Vector2(520f, 70f));
        readyGroup = Hide(kit.Group(ready));
        var button = kit.Shape(ready, "Respawn", DSRadius.Sm, P.Accent);
        var buttonLayout = button.gameObject.AddComponent<HorizontalLayoutGroup>();
        buttonLayout.spacing = 12f;
        buttonLayout.childAlignment = TextAnchor.MiddleCenter;
        buttonLayout.padding = UIKit.Pad(28f, 28f, 0f, 0f);
        buttonLayout.childControlWidth = true;
        buttonLayout.childControlHeight = true;
        buttonLayout.childForceExpandWidth = false;
        buttonLayout.childForceExpandHeight = false;
        UIKit.Fixed(button, 230f, 48f);
        kit.Icon(button.rectTransform, "Play", "play", 18f, P.InkOnAccent);
        var word = kit.Text(button.rectTransform, "Label", new TypeSpec(FontFamily.Sans, 15f, 15f, 600, 0.04f), P.InkOnAccent, "Respawn",
                            TextAlignmentOptions.MidlineLeft, true);
        UIKit.Fixed(word, UIKit.PreferredWidth(word) + 2f, 48f);
        kit.KeyCap(button.rectTransform, "Space");
        autoText = HudText(ready, "Auto", FontFamily.Sans, 12f, 500, P.InkSecondary, "", TextAlignmentOptions.Center, true, 0.08f);
        UIKit.Fixed(autoText, 520f, 14f);
    }

    // Lettre affichee par la disposition du clavier (AZERTY : la touche physique A affiche Q)
    private static string KeyLabel(string letter, KeyControl fallback)
    {
        var kb = Keyboard.current;
        var k = kb != null ? kb.FindKeyOnCurrentKeyboardLayout(letter) : null;
        if (k == null)
            k = fallback;
        string label = k != null && !string.IsNullOrEmpty(k.displayName) ? k.displayName : letter;
        return label.ToUpperInvariant();
    }

    private static KeyControl LayoutKey(string letter, KeyControl fallback)
    {
        var kb = Keyboard.current;
        var k = kb != null ? kb.FindKeyOnCurrentKeyboardLayout(letter) : null;
        return k ?? fallback;
    }

    // =========================================================
    // EVENEMENTS
    // =========================================================

    private void OnDied(TdmParticipant victim, TdmParticipant killer)
    {
        if (victim.IsLocal)
        {
            ShowDeath(victim, killer);
            return;
        }
        // Le coequipier regarde vient d'etre detruit : on passe au suivant un peu apres
        if (state == State.Spectating && victim == watched)
            watchedLostAt = Time.time;
    }

    private void OnSpawned(TdmParticipant p)
    {
        if (p.IsLocal && state != State.Hidden)
            HideAll();
    }

    private void OnPhase(TdmPhase phase)
    {
        if (phase == TdmPhase.Ended)
            HideAll();
    }

    private void ShowDeath(TdmParticipant victim, TdmParticipant killer)
    {
        state = State.Destroyed;
        diedAt = Time.time;
        watched = null;
        var P = kit.P;
        if (killer != null)
        {
            killerName.text = killer.Name;
            killerName.color = P.TeamEnemy;
            killerMark.enabled = true;
            string weapon = string.IsNullOrEmpty(victim.LastKillerWeapon) ? "Unknown weapon" : victim.LastKillerWeapon;
            killerWeapon.text = $"{weapon} · {BlockforgeTheme.Qty(Mathf.RoundToInt(victim.LastKillerDistance), "m")}";
            killerWeaponRow.gameObject.SetActive(true);
        }
        else
        {
            killerName.text = "Destroyed";
            killerName.color = P.DangerInk;
            killerMark.enabled = false;
            killerWeaponRow.gameObject.SetActive(false);
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate((RectTransform)killerName.transform.parent.parent.parent);

        rootGroup.alpha = 1f;
        deathGroup.alpha = 1f;
        spectateGroup.alpha = 0f;
        readyGroup.alpha = 0f;
        shownCount = shownAuto = -1;
    }

    private void HideAll()
    {
        state = State.Hidden;
        rootGroup.alpha = 0f;
        deathGroup.alpha = 0f;
        spectateGroup.alpha = 0f;
        readyGroup.alpha = 0f;
        vignette.color = BlockforgeTheme.WithAlpha(vignette.color, 0f);
        veil.color = BlockforgeTheme.WithAlpha(veil.color, 0f);
        watched = null;
    }

    private void EnterSpectate()
    {
        state = State.Spectating;
        deathGroup.alpha = 0f;
        veil.color = BlockforgeTheme.WithAlpha(veil.color, 0f);
        StartCoroutine(UIAnim.Fade(spectateGroup, 1f, BlockforgeTheme.Fade(BlockforgeTheme.MotionScreen)));
        Watch(Nearest());
    }

    // =========================================================
    // CAMERA DU SPECTATEUR
    // =========================================================

    private List<TdmParticipant> AliveMates()
    {
        var list = new List<TdmParticipant>();
        foreach (var p in match.Roster(true))
        {
            if (!p.IsLocal && p.Alive)
                list.Add(p);
        }
        return list;
    }

    private TdmParticipant Nearest()
    {
        TdmParticipant best = null;
        float bestD = float.MaxValue;
        Vector3 from = match.Local != null ? match.Local.Position : Vector3.zero;
        foreach (var p in AliveMates())
        {
            float d = Vector3.Distance(from, p.Position);
            if (d < bestD) { bestD = d; best = p; }
        }
        return best;
    }

    private void Cycle(int step)
    {
        var mates = AliveMates();
        if (mates.Count == 0)
        {
            Watch(null);
            return;
        }
        int i = watched != null ? mates.IndexOf(watched) : -1;
        i = i < 0 ? 0 : (i + step + mates.Count) % mates.Count;
        Watch(mates[i]);
    }

    private void Watch(TdmParticipant p)
    {
        watched = p;
        watchedLostAt = -1f;
        var cam = match.followCamera;
        if (p != null && p.Robot != null && cam != null)
        {
            cam.SetTarget(p.Robot.transform);
            MatchPauseMenu.ApplyToCamera();
        }
        // Personne : la camera garde sa derniere position (cible inchangee)
        RefreshWatched();
    }

    private void RefreshWatched()
    {
        var mates = AliveMates();
        if (watched == null || !watched.Alive)
        {
            watchName.text = "No teammate alive";
            watchRobot.text = "";
            watchPct.text = "";
            watchFill.sizeDelta = Vector2.zero;
            watchCount.text = "";
            watchMark.enabled = false;
            return;
        }
        watchMark.enabled = true;
        watchName.text = watched.Name;
        watchRobot.text = watched.RobotName;
        int index = mates.IndexOf(watched) + 1;
        watchCount.text = $"Teammate {Tint(index)} of {Tint(mates.Count)} alive";
    }

    private string Tint(int value) => BlockforgeTheme.Tint(BlockforgeTheme.Num(value), kit.P.InkPrimary);

    // =========================================================
    // BOUCLE
    // =========================================================

    private void Update()
    {
        if (match == null || state == State.Hidden)
            return;
        var kb = Keyboard.current;
        bool pause = MatchPauseMenu.IsOpen || MatchEndFlow.IsRunning;
        float since = Time.time - diedAt;

        // Vignette de l'impact : 45 % qui s'efface en 600 ms ; voile pendant la carte du tueur
        float v = Mathf.Clamp01(1f - since / 0.6f) * 0.45f;
        vignette.color = BlockforgeTheme.WithAlpha(vignette.color, v);

        if (state == State.Destroyed)
        {
            veil.color = BlockforgeTheme.WithAlpha(veil.color, 0.18f);
            spectatingIn.text = BlockforgeTheme.Num(Mathf.Max(1, Mathf.CeilToInt(KillCardSeconds - since)));
            bool skip = !pause && kb != null && kb.spaceKey.wasPressedThisFrame;
            if (since >= KillCardSeconds || skip)
                EnterSpectate();
            return;
        }

        // ---------- Spectateur ----------
        if (!pause && kb != null)
        {
            var q = LayoutKey("q", kb.qKey);
            var e = LayoutKey("e", kb.eKey);
            if (q != null && q.wasPressedThisFrame) Cycle(-1);
            else if (e != null && e.wasPressedThisFrame) Cycle(1);
        }
        if (watched != null && !watched.Alive && watchedLostAt > 0f && Time.time - watchedLostAt > 1f)
            Watch(Nearest());
        else if (watched == null)
        {
            // Un coequipier est revenu : on le suit
            var near = Nearest();
            if (near != null)
                Watch(near);
        }
        if (watched != null && watched.Alive)
        {
            float f = watched.HealthFraction;
            watchFill.sizeDelta = new Vector2(270f * f, 0f);
            watchPct.text = BlockforgeTheme.Num(Mathf.RoundToInt(f * 100f)) + "%";
            if (Time.frameCount % 15 == 0)
                RefreshWatched();
        }

        // ---------- Reapparition ----------
        var local = match.Local;
        if (local == null)
            return;
        float now = Time.time;
        if (now < local.RespawnReadyAt)
        {
            countGroup.alpha = 1f;
            readyGroup.alpha = 0f;
            float left = local.RespawnReadyAt - now;
            int s = Mathf.CeilToInt(left);
            if (s != shownCount)
            {
                shownCount = s;
                countFigure.text = BlockforgeTheme.Clock(System.TimeSpan.FromSeconds(s));
            }
            float k = Mathf.Clamp01(1f - left / Mathf.Max(0.1f, match.RespawnSeconds));
            gaugeLeft.sizeDelta = new Vector2(GaugeWidth * 0.5f * k, 0f);
            gaugeRight.sizeDelta = new Vector2(GaugeWidth * 0.5f * k, 0f);
        }
        else
        {
            countGroup.alpha = 0f;
            readyGroup.alpha = 1f;
            int auto = Mathf.Max(0, Mathf.CeilToInt(local.AutoRespawnAt - now));
            if (auto != shownAuto)
            {
                shownAuto = auto;
                autoText.text = $"Auto-respawn in {Tint(auto)} · at your team's spawn";
            }
            if (!pause && kb != null && kb.spaceKey.wasPressedThisFrame)
                match.RequestRespawn();
        }
    }
}
