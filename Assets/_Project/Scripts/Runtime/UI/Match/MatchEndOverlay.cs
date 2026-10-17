using System;
using System.Collections;
using System.Collections.Generic;
using Blockforge.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using O = BlockforgeTheme.Overlay;

// =========================================================
// OVERLAY DE FIN DE MATCH (artboards Victory / Defeat, theme hud)
// Un seul composant, Outcome pilote la variante : bande du haut,
// embleme de fond (trophy / skull a 16 %), le mot VICTORY / DEFEAT
// / DRAW, le score (seule occurrence sur l'overlay), la ligne
// mode · carte · temps, les quatre Match awards (plaques
// chanfreinees teintees de l'equipe du gagnant), et en bas le
// compte a rebours « Results in » avec Skip (Espace).
// Chronologie : 0 ms le voile monte en 140 ms ; 140-360 ms le bloc
// verdict glisse de 40 px et le mot se tamponne 1,1 -> 1 ; les
// awards arrivent en cascade de 400 ms, 40 ms d'ecart ; le compte
// a rebours part a 420 ms. Animations reduites : fondu de 140 ms,
// rien ne bouge.
// =========================================================

public sealed class MatchEndOverlay : MonoBehaviour
{
    public event Action Confirmed;   // Skip, Espace / Entree / A, ou fin des 5 s
    public MatchResult Result { get; private set; }

    private UIKit kit;
    private CanvasGroup root, verdictGroup, footerGroup;
    private RectTransform verdictRect, wordRect;
    private Image dim, band, emblem, emblemGlow;
    private TMP_Text countdown;
    private readonly List<(CanvasGroup group, RectTransform rect)> awardCards = new();
    private float remaining;
    private bool counting, done;

    public static MatchEndOverlay Show(Transform uiRoot, MatchResult result)
    {
        var kit = UIKit.Hud;
        var rt = kit.Rect(uiRoot, "MatchEndOverlay");
        UIKit.Stretch(rt);
        var overlay = rt.gameObject.AddComponent<MatchEndOverlay>();
        overlay.kit = kit;
        overlay.Result = result;
        overlay.Build();
        overlay.StartCoroutine(overlay.Play());
        return overlay;
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

        // Voile sur l'arene figee (bloque les clics vers le HUD)
        dim = kit.Image(rt, "Dim", BlockforgeTheme.WithAlpha(BlockforgeTheme.Scrim, O.DimAlpha));
        UIKit.Stretch(dim.rectTransform);
        dim.raycastTarget = true;

        // Bande du haut, couleur de l'equipe gagnante (aucune en cas d'egalite)
        band = kit.Image(rt, "Band", winColor);
        UIKit.TopStrip(band.rectTransform, 0f, O.Band);
        band.gameObject.SetActive(!draw);

        // Embleme de fond, derriere le texte : lueur douce puis pictogramme a 16 %
        if (!draw)
        {
            string icon = winner == Team.Enemy ? "skull" : "trophy";
            float glowScale = 1f + 2f * O.EmblemGlow / BlockforgeTheme.IconSize.Emblem;
            emblemGlow = kit.Icon(rt, "EmblemGlow", icon, BlockforgeTheme.IconSize.Emblem, BlockforgeTheme.WithAlpha(winColor, O.EmblemAlpha * O.EmblemGlowAlpha));
            UIKit.Place(emblemGlow.rectTransform, new Vector2(0.5f, 1f - O.EmblemY), Vector2.zero, new Vector2(BlockforgeTheme.IconSize.Emblem, BlockforgeTheme.IconSize.Emblem));
            emblemGlow.rectTransform.localScale = Vector3.one * glowScale;
            emblem = kit.Icon(rt, "Emblem", icon, BlockforgeTheme.IconSize.Emblem, BlockforgeTheme.WithAlpha(winColor, O.EmblemAlpha));
            UIKit.Place(emblem.rectTransform, new Vector2(0.5f, 1f - O.EmblemY), Vector2.zero, new Vector2(BlockforgeTheme.IconSize.Emblem, BlockforgeTheme.IconSize.Emblem));
        }

        // ---------- Bloc verdict ----------
        verdictRect = kit.Column(rt, "Verdict", O.VerdictGap, TextAnchor.UpperCenter, null, true, true, false);
        UIKit.TopStrip(verdictRect, O.VerdictTop, O.WordLine + O.VerdictGap * 3f + O.OverlineSize * 1.25f + O.ScoreSize + O.MetaSize * 1.4f);
        verdictGroup = kit.Group(verdictRect);

        var over = kit.Row(verdictRect, "Overline", BlockforgeTheme.Space3, TextAnchor.MiddleCenter);
        UIKit.Fixed(over, -1f, O.OverlineSize * 1.25f);
        if (!draw)
            kit.TeamMark(over, winner, BlockforgeTheme.IconSize.Md);
        var overText = kit.Caps(over, "Text", O.OverlineSize, 600, O.OverlineTracking, P.InkSecondary,
                                draw ? "No team wins" : MetaLabels.TeamName(winner) + " wins", TextAlignmentOptions.Center);
        UIKit.HudShadow(overText, true);

        var word = kit.Condensed(verdictRect, "Word", O.WordSize, 700, O.WordTracking, winColor,
                                 draw ? "Draw" : Result.Outcome == MatchOutcome.Victory ? "Victory" : "Defeat", TextAlignmentOptions.Center);
        word.lineSpacing = 0f;
        UIKit.Fixed(word, -1f, O.WordLine);
        UIKit.HudShadow(word, true);
        wordRect = word.rectTransform;

        var score = kit.Row(verdictRect, "Score", O.ScoreGap, TextAnchor.MiddleCenter);
        UIKit.Fixed(score, -1f, O.ScoreSize);
        var ally = kit.Mono(score, "Ally", O.ScoreSize, 600, P.TeamAlly, BlockforgeTheme.Num(Result.AllyScore), TextAlignmentOptions.Center);
        var dash = kit.Mono(score, "Dash", O.DashSize, 600, P.InkMuted, "–", TextAlignmentOptions.Midline);
        var enemy = kit.Mono(score, "Enemy", O.ScoreSize, 600, P.TeamEnemy, BlockforgeTheme.Num(Result.EnemyScore), TextAlignmentOptions.Center);
        UIKit.HudShadow(ally, true);
        UIKit.HudShadow(dash, true);
        UIKit.HudShadow(enemy, true);

        var meta = kit.Text(verdictRect, "Meta", BlockforgeTheme.Type.Body.With(O.MetaSize), P.InkSecondary,
                            $"{Result.ModeName} · {Result.MapName} · {BlockforgeTheme.Clock(Result.Duration)} played", TextAlignmentOptions.Center);
        UIKit.HudShadow(meta, true);

        // ---------- Match awards ----------
        var awards = kit.Column(rt, "Awards", O.AwardsGap, TextAnchor.UpperCenter, null, true, true, false);
        UIKit.TopStrip(awards, O.AwardsTop, BlockforgeTheme.Type.Label.LineHeight + O.AwardsGap + O.AwardHeight);
        var awardsTitle = kit.Caps(awards, "Title", BlockforgeTheme.Type.Label.Size, 600, 0.18f, P.InkMuted, "Match awards", TextAlignmentOptions.Center);
        UIKit.HudShadow(awardsTitle);
        var cards = kit.Row(awards, "Cards", O.AwardsGap, TextAnchor.MiddleCenter);
        UIKit.Fixed(cards, -1f, O.AwardHeight);
        foreach (AwardKind kind in Enum.GetValues(typeof(AwardKind)))
        {
            Award award = null;
            foreach (var a in Result.Awards)
                if (a.Kind == kind) award = a;
            BuildAwardCard(cards, kind, award);
        }

        // ---------- Pied : compte a rebours + Skip ----------
        var footer = kit.Row(rt, "Footer", O.FooterGap, TextAnchor.MiddleCenter);
        UIKit.BottomStrip(footer, O.FooterBottom, O.SkipHeight);
        footerGroup = kit.Group(footer);
        var resultsIn = kit.Caps(footer, "Label", BlockforgeTheme.Type.TitleSm.Size, 500, 0.1f, P.InkSecondary, "Results in", TextAlignmentOptions.Center);
        UIKit.HudShadow(resultsIn);
        countdown = kit.Mono(footer, "Countdown", O.CountdownSize, 600, P.InkPrimary, Mathf.CeilToInt(O.AutoAdvance).ToString(), TextAlignmentOptions.Center);
        UIKit.HudShadow(countdown);
        var skip = kit.Button(footer, "Skip", ButtonVariant.Ghost, "Skip", Confirm, null, O.SkipHeight);
        var cap = kit.KeyCap(skip.Rect.Find("Content"), "Space");
        cap.SetAsFirstSibling();
        skip.Rect.sizeDelta += new Vector2(cap.sizeDelta.x + BlockforgeTheme.Space2, 0f);
        UIKit.Fixed(skip.Rect, skip.Rect.sizeDelta.x, O.SkipHeight);
        UIKit.Focus(skip.Button);
        gameObject.AddComponent<TabFocusCycler>();
    }

    private void BuildAwardCard(Transform parent, AwardKind kind, Award award)
    {
        var P = kit.P;
        var winner = award?.PlayerId != null ? Result.Find(award.PlayerId) : null;
        bool has = winner != null && award.Value > 0;
        var teamColor = winner == null ? P.TeamNeutral : winner.Team == Team.Enemy ? P.TeamEnemy : P.TeamAlly;
        var plateColor = winner == null ? BlockforgeTheme.WithAlpha(P.TeamNeutral, 0f) : winner.Team == Team.Enemy ? P.PlateEnemy : P.PlateAlly;

        var plate = ChamferedPlate.Build(kit, parent, "Award_" + kind, PlateCorners.TopLeft, O.AwardChamfer, null, plateColor);
        UIKit.Fixed(plate.Rect, O.AwardWidth, O.AwardHeight);
        var group = kit.Group(plate.Rect);
        awardCards.Add((group, plate.Rect));

        var row = kit.Row(plate.Content, "Row", O.AwardsGap, TextAnchor.MiddleLeft, UIKit.Pad(O.AwardPadLeft, O.AwardPadRight, 0f, 0f));
        UIKit.Stretch(row);
        var tile = kit.Shape(row, "IconTile", DSRadius.Sm, P.SurfaceBase);
        UIKit.Fixed(tile, BlockforgeTheme.IconSize.Tile, BlockforgeTheme.IconSize.Tile);
        var icon = kit.Icon(tile.transform, "Icon", MetaLabels.Icon(kind), BlockforgeTheme.IconSize.Xl, P.InkSecondary);
        UIKit.Place(icon.rectTransform, UIKit.Center, Vector2.zero, new Vector2(BlockforgeTheme.IconSize.Xl, BlockforgeTheme.IconSize.Xl));

        var text = kit.Column(row, "Text", BlockforgeTheme.Gap.Tight, TextAnchor.MiddleLeft);
        UIKit.Flexible(text);
        var label = kit.Caps(text, "Label", O.AwardLabelSize, 600, O.AwardLabelTracking, P.InkMuted, MetaLabels.Of(kind));
        UIKit.HudShadow(label);
        var who = kit.Row(text, "Who", BlockforgeTheme.Space2, TextAnchor.MiddleLeft);
        UIKit.Fixed(who, -1f, BlockforgeTheme.Type.TitleMd.LineHeight);
        if (has)
        {
            kit.Avatar(who, "Avatar", O.AwardAvatar, winner.IsLocal ? P.Accent : teamColor, BlockforgeTheme.IconSize.Xs);
            var name = kit.Text(who, "Name", DSTextStyle.TitleMD, winner.IsLocal ? P.Accent : P.InkPrimary, winner.DisplayName);
            UIKit.Ellipsis(name);
            UIKit.Flexible(name);
            UIKit.HudShadow(name);
        }
        else
        {
            var none = kit.Text(who, "Name", DSTextStyle.TitleMD, P.InkMuted, "—");
            UIKit.HudShadow(none);
        }

        var value = kit.Mono(row, "Value", O.AwardValueSize, 600, has ? P.InkPrimary : P.InkMuted, has ? BlockforgeTheme.Num(award.Value) : "—", TextAlignmentOptions.MidlineRight);
        UIKit.HudShadow(value);
    }

    // =========================================================
    // DEROULE
    // =========================================================

    private IEnumerator Play()
    {
        bool reduced = BlockforgeTheme.ReduceMotion;

        // Etat initial : tout invisible sauf le voile
        verdictGroup.alpha = 0f;
        footerGroup.alpha = 0f;
        foreach (var (group, _) in awardCards)
            group.alpha = 0f;
        float emblemAlpha = emblem != null ? emblem.color.a : 0f;
        float glowAlpha = emblemGlow != null ? emblemGlow.color.a : 0f;
        if (emblem != null) emblem.color = BlockforgeTheme.WithAlpha(emblem.color, 0f);
        if (emblemGlow != null) emblemGlow.color = BlockforgeTheme.WithAlpha(emblemGlow.color, 0f);

        // 0 -> 140 ms : l'arene s'assombrit
        yield return UIAnim.Fade(root, 1f, BlockforgeTheme.Fade(O.DimTime));

        if (reduced)
        {
            // Fondu de 140 ms, rien ne bouge
            StartCoroutine(UIAnim.Fade(verdictGroup, 1f, BlockforgeTheme.MotionPanel));
            foreach (var (group, _) in awardCards)
                StartCoroutine(UIAnim.Fade(group, 1f, BlockforgeTheme.MotionPanel));
            StartCoroutine(FadeEmblem(emblemAlpha, glowAlpha, BlockforgeTheme.MotionPanel));
            yield return UIAnim.Fade(footerGroup, 1f, BlockforgeTheme.MotionPanel);
        }
        else
        {
            // 140 -> 360 ms : le bloc verdict glisse de 40 px, le mot se tamponne, l'embleme apparait
            StartCoroutine(UIAnim.Scale(wordRect, O.WordStampFrom, 1f, O.VerdictTime));
            StartCoroutine(FadeEmblem(emblemAlpha, glowAlpha, O.VerdictTime));
            StartCoroutine(UIAnim.FadeSlide(verdictGroup, verdictRect, O.VerdictSlide, O.VerdictTime));
            // 400 ms : les awards en cascade, 40 ms d'ecart
            yield return UIAnim.Wait(O.AwardsDelay - O.VerdictDelay);
            for (int i = 0; i < awardCards.Count; i++)
            {
                StartCoroutine(UIAnim.FadeSlide(awardCards[i].group, awardCards[i].rect, O.VerdictSlide * 0.3f, O.AwardTime));
                yield return UIAnim.Wait(O.AwardStagger);
            }
            StartCoroutine(UIAnim.Fade(footerGroup, 1f, O.AwardTime));
        }

        // 420 ms : le compte a rebours part
        remaining = O.AutoAdvance;
        counting = true;
    }

    private IEnumerator FadeEmblem(float emblemTarget, float glowTarget, float duration)
    {
        if (emblem == null)
            yield break;
        yield return UIAnim.Lerp(duration, k =>
        {
            if (emblem != null) emblem.color = BlockforgeTheme.WithAlpha(emblem.color, emblemTarget * k);
            if (emblemGlow != null) emblemGlow.color = BlockforgeTheme.WithAlpha(emblemGlow.color, glowTarget * k);
        });
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
                Confirm();
                return;
            }
        }
        if (ConfirmPressed())
            Confirm();
    }

    public static bool ConfirmPressed()
    {
        var kb = Keyboard.current;
        var pad = Gamepad.current;
        return (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame))
            || (pad != null && pad.buttonSouth.wasPressedThisFrame);
    }

    private void Confirm()
    {
        if (done)
            return;
        done = true;
        counting = false;
        Confirmed?.Invoke();
    }

    public IEnumerator FadeOut(float duration) => UIAnim.Fade(root, 0f, BlockforgeTheme.Fade(duration));
}
