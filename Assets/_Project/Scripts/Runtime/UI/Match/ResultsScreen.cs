using System;
using System.Collections;
using System.Collections.Generic;
using Blockforge.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using R = BlockforgeTheme.Results;

// =========================================================
// ECRAN DE RESULTATS (artboard Results, theme hud)
// Plein ecran : degrade surface-raised -> surface-base, grille de
// 96 px a 6 % avec ~14 cellules qui pulsent, bande du haut aux
// couleurs du gagnant. De haut en bas : l'en-tete (Match results,
// trophee + VICTORY + le score — seule occurrence —, mode / carte /
// temps / vs bots), le podium (2e · 1er · 3e, bas alignes, le 1er
// plus large avec MVP ; liseres or / argent / bronze qui suivent les
// chanfreins, lueur et reflets dans le fill : PodiumMedalFX),
// le Scoreboard partage (1 400 px, trie par score, sans score dans
// sa plaque ni ses en-tetes), puis la plaque Rewards (credits, tech
// points, barre d'XP) avec « Back to the garage in 20 » et Continue.
// Pas de bouton « Play again », Echap ne fait rien.
// =========================================================

public sealed class ResultsScreen : MonoBehaviour
{
    public event Action Continued;   // Continue, confirm, ou fin des 20 s
    public MatchResult Result { get; private set; }

    private UIKit kit;
    private CanvasGroup root;
    private readonly List<(CanvasGroup group, RectTransform rect)> podium = new(); // ordre d'arrivee : 2e, 3e, 1er
    private ScoreboardWidget board;
    private TMP_Text creditsValue, techValue, xpLabel, countdown;
    private RectTransform xpFill, xpGain;
    private readonly List<(Image cell, float delay)> cells = new();
    private float remaining;
    private bool counting, done;

    private static Sprite gridSprite;

    public static ResultsScreen Show(Transform uiRoot, MatchResult result)
    {
        var kit = UIKit.Hud;
        var rt = kit.Rect(uiRoot, "ResultsScreen");
        UIKit.Stretch(rt);
        var screen = rt.gameObject.AddComponent<ResultsScreen>();
        screen.kit = kit;
        screen.Result = result;
        screen.Build();
        screen.StartCoroutine(screen.Play());
        return screen;
    }

    // =========================================================
    // CONSTRUCTION
    // =========================================================

    private void Build()
    {
        var P = kit.P;
        var rt = (RectTransform)transform;
        root = kit.Group(rt);
        root.alpha = 0f;

        bool draw = Result.Outcome == MatchOutcome.Draw;
        var winner = MatchLogic.Winner(Result);
        var winColor = draw ? P.InkPrimary : winner == Team.Enemy ? P.TeamEnemy : P.TeamAlly;

        // Fond : degrade vertical, grille, cellules animees, bande
        var backdrop = kit.Polygon(rt, "Backdrop");
        UIKit.Stretch(backdrop.rectTransform);
        backdrop.vertical = true;
        backdrop.SetColors(P.SurfaceRaised, P.SurfaceBase);
        backdrop.raycastTarget = true; // bloque les clics vers le HUD et l'overlay

        var grid = kit.Image(rt, "Grid", BlockforgeTheme.WithAlpha(P.InkPrimary, BlockforgeTheme.OverlayAlpha(R.GridAlpha)), GridSprite());
        grid.type = Image.Type.Tiled;
        UIKit.Stretch(grid.rectTransform);

        var cellsRoot = kit.Rect(rt, "Cells");
        UIKit.Stretch(cellsRoot);
        cellsRoot.gameObject.AddComponent<RectMask2D>();
        BuildCells(cellsRoot);

        var band = kit.Image(rt, "Band", winColor);
        UIKit.TopStrip(band.rectTransform, 0f, R.Band);
        band.gameObject.SetActive(!draw);

        // Pile verticale : en-tete, podium, tableau (flexible), pied
        var stack = kit.Column(rt, "Stack", R.Gap, TextAnchor.UpperCenter, UIKit.Pad(R.PadSide, R.PadSide, R.PadTop, R.PadBottom));
        UIKit.Stretch(stack);

        BuildHeader(stack, winner, winColor, draw);
        BuildPodium(stack);

        var boardHost = kit.Rect(stack, "Board");
        UIKit.Flexible(boardHost, 1f, 1f);
        board = ScoreboardWidget.Build(kit, boardHost, "Scoreboard", Result, ScoreboardColumn.Results, false, "Sorted by score", R.BoardWidth);
        UIKit.Place(board.Rect, UIKit.TopCenter, Vector2.zero, new Vector2(R.BoardWidth, 0f));
        UIKit.Fit(board.Rect, false, true);

        BuildFooter(stack);
        gameObject.AddComponent<TabFocusCycler>();
    }

    private void BuildHeader(Transform parent, Team winner, Color winColor, bool draw)
    {
        var P = kit.P;
        var header = kit.Row(parent, "Header", BlockforgeTheme.Space8, TextAnchor.MiddleLeft);
        UIKit.Fixed(header, -1f, R.HeaderHeight);

        var left = kit.Column(header, "Left", BlockforgeTheme.Space1, TextAnchor.UpperLeft, null, true, true, false);
        var overline = kit.Caps(left, "Overline", R.HeaderOverlineSize, 500, R.HeaderOverlineTracking, P.InkMuted, "Match results");
        UIKit.HudShadow(overline);
        var h1 = kit.Row(left, "Verdict", BlockforgeTheme.Space4, TextAnchor.LowerLeft);
        UIKit.Fixed(h1, -1f, R.WordLine);
        if (!draw)
            kit.Icon(h1, "Icon", winner == Team.Enemy ? "skull" : "trophy", R.WordIcon, winColor);
        var word = kit.Condensed(h1, "Word", R.WordSize, 700, R.WordTracking, winColor,
                                 draw ? "Draw" : Result.Outcome == MatchOutcome.Victory ? "Victory" : "Defeat", TextAlignmentOptions.BottomLeft);
        UIKit.HudShadow(word);
        // Le score n'apparait qu'ici : 24 px a droite du mot, meme ligne de base
        var score = kit.Row(h1, "Score", R.ScoreGap, TextAnchor.LowerLeft, UIKit.Pad(R.ScoreOffset - BlockforgeTheme.Space4, 0f, 0f, 0f));
        var ally = kit.Mono(score, "Ally", R.ScoreSize, 600, P.TeamAlly, BlockforgeTheme.Num(Result.AllyScore), TextAlignmentOptions.BottomLeft);
        var dash = kit.Mono(score, "Dash", R.DashSize, 600, P.InkMuted, "–", TextAlignmentOptions.MidlineLeft);
        var enemy = kit.Mono(score, "Enemy", R.ScoreSize, 600, P.TeamEnemy, BlockforgeTheme.Num(Result.EnemyScore), TextAlignmentOptions.BottomLeft);
        UIKit.HudShadow(ally);
        UIKit.HudShadow(dash);
        UIKit.HudShadow(enemy);

        kit.Spacer(header);

        var right = kit.Column(header, "Right", BlockforgeTheme.Space1, TextAnchor.UpperRight, null, true, true, false);
        var mode = kit.Condensed(right, "Mode", R.ModeSize, 600, BlockforgeTheme.Type.DisplayMd.LetterSpacingEm, P.InkPrimary, Result.ModeName, TextAlignmentOptions.MidlineRight);
        UIKit.HudShadow(mode);
        string vs = Result.VsBots ? " · vs bots" : "";
        var meta = kit.Text(right, "Meta", DSTextStyle.Body, P.InkSecondary, $"{Result.MapName} · {BlockforgeTheme.Clock(Result.Duration)} played{vs}", TextAlignmentOptions.MidlineRight);
        UIKit.HudShadow(meta);
    }

    private void BuildPodium(Transform parent)
    {
        var top3 = MatchLogic.Podium(Result.Players);
        var row = kit.Row(parent, "Podium", R.PodiumGap, TextAnchor.LowerCenter);
        // Ordre d'affichage 2e · 1er · 3e ; ordre d'arrivee 2e, 3e, 1er
        var second = top3.Count > 1 ? BuildPodiumCard(row, top3[1], 2) : null;
        var first = top3.Count > 0 ? BuildPodiumCard(row, top3[0], 1) : null;
        var third = top3.Count > 2 ? BuildPodiumCard(row, top3[2], 3) : null;
        if (second != null) podium.Add(second.Value);
        if (third != null) podium.Add(third.Value);
        if (first != null) podium.Add(first.Value);
    }

    private (CanvasGroup, RectTransform)? BuildPodiumCard(Transform parent, PlayerMatchStats p, int rank)
    {
        var P = kit.P;
        bool isFirst = rank == 1;
        bool enemy = p.Team == Team.Enemy;
        var teamColor = enemy ? P.TeamEnemy : P.TeamAlly;
        var plateColor = enemy ? P.PlateEnemy : P.PlateAlly;

        // Lisere or / argent / bronze sur les trois cartes (PodiumMedalFX : degrade, lueur et reflets du fill)
        var medal = PodiumMedalFX.ForRank(rank);
        var plate = ChamferedPlate.Build(kit, parent, "Rank" + rank, PlateCorners.Top, R.PodiumChamfer, null, plateColor, true,
                                         true, medal, R.PodiumFrame);
        PodiumMedalFX.Attach(kit, plate, rank);
        float width = isFirst ? R.PodiumFirstWidth : R.PodiumWidth;
        UIKit.Fixed(plate.Rect, width, -1f);
        var group = kit.Group(plate.Rect);

        // La colonne pilote la hauteur ; rang et MVP en absolu dans Content
        var layout = plate.Rect.gameObject.AddComponent<VerticalLayoutGroup>();
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        var column = kit.Column(plate.Rect, "Layout", R.PodiumItemGap, TextAnchor.UpperCenter,
                                UIKit.Pad(R.PodiumPad, R.PodiumPad, isFirst ? R.PodiumFirstPadTop : R.PodiumPad, R.PodiumPadBottom), true, true, false);

        var rankText = kit.Condensed(plate.Content, "Rank", R.RankSize, 700, 0f, medal, rank.ToString());
        UIKit.Place(rankText.rectTransform, UIKit.TopLeft, new Vector2(R.RankX, -R.RankY), new Vector2(R.RankSize, R.RankSize));
        UIKit.HudShadow(rankText);
        if (isFirst)
        {
            var mvp = kit.Badge(plate.Content, "MVP", BadgeScale.Status, "success", "MVP", false, "trophy");
            mvp.anchorMin = mvp.anchorMax = mvp.pivot = UIKit.TopRight;
            mvp.anchoredPosition = new Vector2(-R.MvpBadgeInset, -R.MvpBadgeInset);
        }

        float avatarSize = isFirst ? R.AvatarFirst : R.Avatar;
        kit.Avatar(column, "Avatar", avatarSize, isFirst ? medal : teamColor, isFirst ? R.AvatarIconFirst : R.AvatarIcon);

        var name = kit.Text(column, "Name", BlockforgeTheme.Type.TitleLg.With(isFirst ? R.NameFirstSize : R.NameSize, 600), isFirst ? medal : P.InkPrimary, p.DisplayName, TextAlignmentOptions.Center);
        UIKit.Fixed(name, -1f, Mathf.Ceil((isFirst ? R.NameFirstSize : R.NameSize) * 1.35f));
        UIKit.HudShadow(name);

        var badgeHost = kit.Row(column, "BadgeRow", 0f, TextAnchor.MiddleCenter);
        UIKit.Fixed(badgeHost, -1f, BlockforgeTheme.ControlSm);
        kit.Badge(badgeHost, "Team", BadgeScale.Team, enemy ? "enemy" : "ally", MetaLabels.TeamName(p.Team) + (p.IsLocal ? " · you" : ""), true);

        var score = kit.Row(column, "Score", BlockforgeTheme.Space2, TextAnchor.LowerCenter);
        UIKit.Fixed(score, -1f, R.ScoreValueSize);
        var scoreValue = kit.Mono(score, "Value", R.ScoreValueSize, 700, P.InkPrimary, BlockforgeTheme.Num(p.Score), TextAlignmentOptions.BottomLeft);
        UIKit.HudShadow(scoreValue);
        var scoreLabel = kit.Caps(score, "Label", BlockforgeTheme.Type.Label.Size, 500, 0.1f, P.InkMuted, "score", TextAlignmentOptions.BottomLeft);
        scoreLabel.margin = new Vector4(0f, 0f, 0f, BlockforgeTheme.Space2);

        // Bande de stats : K / D / A et degats (pas de serie)
        var strip = kit.Row(column, "Stats", R.StripGap, TextAnchor.UpperCenter, UIKit.Pad(0f, 0f, R.StripPadTop, 0f));
        var hair = kit.Image(strip, "Hairline", BlockforgeTheme.WithAlpha(P.InkPrimary, R.HairlineAlpha));
        UIKit.TopStrip(hair.rectTransform, 0f, BlockforgeTheme.ControlBorder);
        UIKit.IgnoreLayout(hair);
        strip.GetComponent<HorizontalLayoutGroup>().childForceExpandWidth = true;
        PodiumStat(strip, $"{p.Kills} / {p.Deaths} / {p.Assists}", "K / D / A");
        PodiumStat(strip, BlockforgeTheme.Num(p.Damage), "damage");

        return (group, plate.Rect);
    }

    private void PodiumStat(Transform parent, string value, string label)
    {
        var col = kit.Column(parent, "Stat_" + label, BlockforgeTheme.Gap.Hair, TextAnchor.UpperCenter, null, true, true, true);
        UIKit.Flexible(col);
        var v = kit.Mono(col, "Value", R.StatSize, 600, kit.P.InkPrimary, value, TextAlignmentOptions.Center);
        UIKit.HudShadow(v);
        kit.Caps(col, "Label", R.StatLabelSize, 500, 0.1f, kit.P.InkMuted, label, TextAlignmentOptions.Center);
    }

    private void BuildFooter(Transform parent)
    {
        var P = kit.P;
        var rewards = Result.Rewards;
        var plate = ChamferedPlate.Build(kit, parent, "Footer", PlateCorners.Bottom, R.FooterChamfer);
        UIKit.Fixed(plate.Rect, -1f, R.FooterHeight);
        var row = kit.Row(plate.Content, "Row", R.FooterGap, TextAnchor.MiddleLeft, UIKit.Pad(R.FooterPadX, R.FooterPadY));
        UIKit.Stretch(row);

        var title = kit.Caps(row, "Title", R.HeaderOverlineSize, 500, R.HeaderOverlineTracking, P.InkMuted, "Rewards");
        UIKit.HudShadow(title);
        creditsValue = Reward(row, "credits", P.GradeMasterwork, "Credits");
        techValue = Reward(row, "electronics", P.Accent, "Tech points");

        // Barre d'XP : Level · +XP · dans le niveau, piste trapeze 6 px
        var xp = kit.Column(row, "Xp", BlockforgeTheme.Gap.Small, TextAnchor.UpperLeft);
        UIKit.Fixed(xp, R.XpWidth, -1f);
        var xpHead = kit.Row(xp, "Head", BlockforgeTheme.Space2, TextAnchor.MiddleLeft);
        UIKit.Fixed(xpHead, -1f, BlockforgeTheme.Type.Label.LineHeight);
        var level = kit.Caps(xpHead, "Level", R.XpLabelSize, 500, 0.08f, P.InkMuted, "Level " + BlockforgeTheme.Num(rewards.Level));
        kit.Spacer(xpHead);
        xpLabel = kit.Mono(xpHead, "Progress", R.XpLabelSize, 500, P.InkSecondary, "", TextAlignmentOptions.MidlineRight);
        var track = ChamferedPlate.Trapezoid(kit, xp, "Track", P.SurfaceBase, BlockforgeTheme.Space1);
        UIKit.Fixed(track, -1f, R.XpTrack);
        var mask = track.gameObject.AddComponent<RectMask2D>();
        var fill = kit.Image(track.transform, "Fill", P.Accent);
        UIKit.Stretch(fill.rectTransform);
        xpFill = fill.rectTransform;
        var gain = kit.Image(track.transform, "Gain", BlockforgeTheme.WithAlpha(P.AccentHover, R.XpGainAlpha));
        UIKit.Stretch(gain.rectTransform);
        xpGain = gain.rectTransform;
        SetXp(0f);

        kit.Spacer(row);

        var back = kit.Row(row, "Back", BlockforgeTheme.Space1, TextAnchor.MiddleRight);
        var backText = kit.Text(back, "Label", BlockforgeTheme.Type.BodySm.With(R.CountdownSize, 500), P.InkMuted, "Back to the garage in");
        countdown = kit.Mono(back, "Countdown", R.CountdownSize, 500, P.InkSecondary, Mathf.CeilToInt(R.AutoAdvance).ToString());
        UIKit.HudShadow(backText);
        UIKit.HudShadow(countdown);

        var cont = kit.Button(row, "Continue", ButtonVariant.Primary, "Continue", Continue, "chevron-right", R.ContinueHeight, false, true);
        cont.Icon.color = P.InkOnAccent;
        UIKit.Focus(cont.Button);
    }

    private TMP_Text Reward(Transform parent, string icon, Color color, string label)
    {
        var row = kit.Row(parent, "Reward_" + label, BlockforgeTheme.Space3, TextAnchor.MiddleLeft);
        var tile = kit.Shape(row, "Tile", DSRadius.Sm, kit.P.SurfaceBase);
        UIKit.Fixed(tile, R.RewardIcon, R.RewardIcon);
        var ic = kit.Icon(tile.transform, "Icon", icon, BlockforgeTheme.IconSize.Xl, color);
        UIKit.Place(ic.rectTransform, UIKit.Center, Vector2.zero, new Vector2(BlockforgeTheme.IconSize.Xl, BlockforgeTheme.IconSize.Xl));
        var text = kit.Column(row, "Text", BlockforgeTheme.Gap.Hair, TextAnchor.MiddleLeft, null, true, true, false);
        var value = kit.Mono(text, "Value", R.RewardValueSize, 600, color, "+0");
        UIKit.HudShadow(value);
        kit.Caps(text, "Label", R.RewardLabelSize, 500, 0.08f, kit.P.InkMuted, label);
        return value;
    }

    // k : part du gain deja remplie (0 -> 1)
    private void SetXp(float k)
    {
        var r = Result.Rewards;
        float max = Mathf.Max(1, r.XpForNextLevel);
        float after = Mathf.Clamp01(r.XpInLevel / max);
        float before = Mathf.Clamp01((r.XpInLevel - r.Xp) / max);
        float shown = Mathf.Lerp(before, after, k);
        xpFill.anchorMax = new Vector2(before, 1f);
        xpGain.anchorMin = new Vector2(before, 0f);
        xpGain.anchorMax = new Vector2(shown, 1f);
        long gained = (long)Mathf.Round(r.Xp * k);
        xpLabel.text = $"{BlockforgeTheme.Signed(gained)} XP · {BlockforgeTheme.Num(r.XpInLevel - r.Xp + gained)} / {BlockforgeTheme.Num(r.XpForNextLevel)}";
    }

    // ---------- Fond ----------

    private static Sprite GridSprite()
    {
        if (gridSprite != null)
            return gridSprite;
        int size = (int)BlockforgeTheme.Results.Grid;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "ResultsGrid", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat, hideFlags = HideFlags.DontSave };
        var px = new Color32[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
                px[y * size + x] = x == 0 || y == size - 1 ? new Color32(255, 255, 255, 255) : new Color32(255, 255, 255, 0);
        tex.SetPixels32(px);
        tex.Apply(false, true);
        gridSprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
        gridSprite.name = tex.name;
        return gridSprite;
    }

    // Positions (left, top, delai) de l'artboard : blanc au tiers central, ally a gauche, enemy a droite
    private static readonly (int col, int row, float delay)[] CellSeeds =
    {
        (1, 2, 0f), (3, 1, 1.4f), (2, 5, 2.6f), (5, 7, 3.3f), (7, 3, 4.1f), (10, 1, 0.8f), (12, 6, 2.0f),
        (14, 2, 1.1f), (16, 4, 2.9f), (18, 8, 3.9f), (15, 9, 0.5f), (9, 9, 4.7f), (13, 1, 5.2f), (6, 1, 5.6f),
    };

    private void BuildCells(Transform parent)
    {
        var P = kit.P;
        int columns = Mathf.CeilToInt(BlockforgeTheme.ReferenceWidth / R.Grid);
        for (int i = 0; i < CellSeeds.Length && i < R.CellCount; i++)
        {
            var (col, row, delay) = CellSeeds[i];
            float third = (float)col / columns;
            var color = third < 1f / 3f ? P.TeamAlly : third > 2f / 3f ? P.TeamEnemy : P.InkPrimary;
            var cell = kit.Image(parent, "Cell_" + i, BlockforgeTheme.WithAlpha(color, 0f));
            UIKit.Place(cell.rectTransform, UIKit.TopLeft, new Vector2(col * R.Grid, -row * R.Grid), new Vector2(R.Grid, R.Grid));
            cells.Add((cell, delay));
        }
    }

    private IEnumerator PulseCells()
    {
        if (BlockforgeTheme.ReduceMotion)
        {
            foreach (var (cell, _) in cells)
                cell.color = BlockforgeTheme.WithAlpha(cell.color, BlockforgeTheme.OverlayAlpha(R.CellStatic));
            yield break;
        }
        float t0 = Time.unscaledTime;
        while (true)
        {
            float now = Time.unscaledTime - t0;
            foreach (var (cell, delay) in cells)
            {
                float phase = Mathf.Repeat(now - delay, R.CellLoop) / R.CellLoop; // 0 -> 7 % a 40 %, plateau jusqu'a 60 %, retour a 0
                float a = phase < 0.4f ? phase / 0.4f : phase < 0.6f ? 1f : 1f - (phase - 0.6f) / 0.4f;
                cell.color = BlockforgeTheme.WithAlpha(cell.color, BlockforgeTheme.OverlayAlpha(R.CellPeak) * Mathf.SmoothStep(0f, 1f, a));
            }
            yield return null;
        }
    }

    // =========================================================
    // DEROULE
    // =========================================================

    private IEnumerator Play()
    {
        StartCoroutine(PulseCells());
        foreach (var (group, _) in podium) group.alpha = 0f;
        foreach (var g in board.Rows) g.alpha = 0f;

        yield return UIAnim.Fade(root, 1f, BlockforgeTheme.Fade(BlockforgeTheme.MotionScreen));

        // Podium : 2e, 3e, 1er, 80 ms d'ecart
        foreach (var (group, rect) in podium)
        {
            StartCoroutine(UIAnim.FadeSlide(group, rect, -R.PodiumRise, R.PodiumTime));
            yield return UIAnim.Wait(BlockforgeTheme.Duration(R.PodiumStagger));
        }
        // Lignes du tableau de haut en bas, 30 ms d'ecart
        foreach (var g in board.Rows)
        {
            StartCoroutine(UIAnim.Fade(g, 1f, BlockforgeTheme.Fade(R.RowTime)));
            yield return UIAnim.Wait(BlockforgeTheme.Duration(R.RowStagger));
        }
        // Recompenses : compteurs sur 600 ms, puis la barre d'XP sur 600 ms
        StartCoroutine(UIAnim.CountUp(Result.Rewards.Credits, R.CountUp, v => creditsValue.text = BlockforgeTheme.Signed(v)));
        yield return UIAnim.CountUp(Result.Rewards.TechPoints, R.CountUp, v => techValue.text = BlockforgeTheme.Signed(v));
        yield return UIAnim.Lerp(R.XpFill, SetXp);

        remaining = R.AutoAdvance;
        counting = true;
    }

    private void Update()
    {
        if (done)
            return;
        if (counting)
        {
            remaining -= Time.unscaledDeltaTime;
            countdown.text = Mathf.Max(0, Mathf.CeilToInt(remaining)).ToString();
            if (remaining <= 0f)
            {
                Continue();
                return;
            }
        }
        // Confirm = action principale ; Echap ne fait rien (pas de retour a l'arene)
        if (MatchEndOverlay.ConfirmPressed())
            Continue();
    }

    private void Continue()
    {
        if (done)
            return;
        done = true;
        counting = false;
        Continued?.Invoke();
    }

    public IEnumerator FadeOut(float duration) => UIAnim.Fade(root, 0f, BlockforgeTheme.Fade(duration));
}
