using System;
using System.Collections.Generic;
using Blockforge.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using B = BlockforgeTheme.Board;

// =========================================================
// SCOREBOARD (composant Scoreboard du DS, theme hud)
// Le tableau de la touche Tab et celui de l'ecran de resultats :
// une plaque du haut (mode, carte · temps, et les deux scores
// quand showScores) puis deux tables cote a cote, l'equipe du
// joueur a gauche. Colonnes configurables ; chaque ligne porte
// un TeamMark + nom, des chiffres en mono alignes a droite ; la
// ligne du joueur local est teintee plate-self ; une ligne
// « Team » somme chaque colonne. Pas d'etat « a terre » sur
// l'ecran de resultats (ShowDown = false).
// Les lignes exposent leur CanvasGroup pour l'arrivee en cascade.
// =========================================================

public sealed class ScoreboardColumn
{
    public string Key, Label;
    public Func<PlayerMatchStats, int> Value;

    public ScoreboardColumn(string key, string label, Func<PlayerMatchStats, int> value)
    {
        Key = key;
        Label = label;
        Value = value;
    }

    public static readonly ScoreboardColumn Kills = new("kills", "Kills", p => p.Kills);
    public static readonly ScoreboardColumn Deaths = new("deaths", "Deaths", p => p.Deaths);
    public static readonly ScoreboardColumn Assists = new("assists", "Assists", p => p.Assists);
    public static readonly ScoreboardColumn Damage = new("damage", "Damage", p => p.Damage);
    public static readonly ScoreboardColumn Healing = new("healing", "Healing", p => p.Healing);
    public static readonly ScoreboardColumn Score = new("score", "Score", p => p.Score);

    public static ScoreboardColumn[] Results => new[] { Kills, Deaths, Assists, Damage, Score };
}

public sealed class ScoreboardWidget
{
    public RectTransform Rect;
    public ChamferedPlate Top;
    public TextMeshProUGUI ModeName, Meta, AllyScore, EnemyScore, Hint;
    public readonly List<CanvasGroup> Rows = new();   // toutes les lignes, equipe du joueur d'abord (cascade)
    public readonly List<RectTransform> RowRects = new();

    private readonly UIKit kit;
    private readonly float width;

    private ScoreboardWidget(UIKit kit, float width)
    {
        this.kit = kit;
        this.width = width;
    }

    public static ScoreboardWidget Build(UIKit kit, Transform parent, string name, MatchResult result, IReadOnlyList<ScoreboardColumn> columns,
                                         bool showScores, string hint = null, float width = BlockforgeTheme.Board.DefaultWidth,
                                         IComparer<PlayerMatchStats> order = null)
    {
        var w = new ScoreboardWidget(kit, width);
        w.Rect = kit.Column(parent, name, BlockforgeTheme.Space2, TextAnchor.UpperCenter, null, true, true, true);
        UIKit.Fixed(w.Rect, width, -1f);

        // ---------- Plaque du haut ----------
        w.Top = ChamferedPlate.Build(kit, w.Rect, "Top", PlateCorners.Top);
        UIKit.Fixed(w.Top.Rect, width, B.TopHeight);
        var topRow = kit.Row(w.Top.Content, "Row", 0f, TextAnchor.MiddleCenter);
        UIKit.Stretch(topRow);
        w.AllyScore = kit.Mono(topRow, "AllyScore", B.ScoreSize, 600, kit.P.TeamAlly, BlockforgeTheme.Num(result.AllyScore), TextAlignmentOptions.Center);
        UIKit.Fixed(w.AllyScore, B.TopSide, -1f);
        var mode = kit.Column(topRow, "Mode", BlockforgeTheme.Gap.Hair, TextAnchor.MiddleCenter);
        UIKit.Flexible(mode);
        w.ModeName = kit.Condensed(mode, "Name", B.ModeSize, 600, BlockforgeTheme.Type.DisplayMd.LetterSpacingEm, kit.P.InkPrimary, result.ModeName, TextAlignmentOptions.Center);
        w.Meta = kit.Mono(mode, "Meta", B.MetaSize, 500, kit.P.InkSecondary, result.MapName + " · " + BlockforgeTheme.Clock(result.Duration), TextAlignmentOptions.Center);
        w.EnemyScore = kit.Mono(topRow, "EnemyScore", B.ScoreSize, 600, kit.P.TeamEnemy, BlockforgeTheme.Num(result.EnemyScore), TextAlignmentOptions.Center);
        UIKit.Fixed(w.EnemyScore, B.TopSide, -1f);
        UIKit.HudShadow(w.ModeName);
        UIKit.HudShadow(w.AllyScore);
        UIKit.HudShadow(w.EnemyScore);
        // Le score n'apparait qu'une fois sur l'ecran de resultats : jamais ici
        w.AllyScore.gameObject.SetActive(showScores);
        w.EnemyScore.gameObject.SetActive(showScores);
        if (!showScores)
        {
            kit.Gap(topRow, B.TopSide).SetAsFirstSibling();
            kit.Gap(topRow, B.TopSide);
        }

        // ---------- Deux tables ----------
        var teams = kit.Row(w.Rect, "Teams", BlockforgeTheme.Space2, TextAnchor.UpperCenter, null, true, true, false);
        UIKit.Fixed(teams, width, -1f);
        float half = (width - BlockforgeTheme.Space2) * 0.5f;
        var allies = MatchLogic.Team(result.Players, Team.Ally);
        var enemies = MatchLogic.Team(result.Players, Team.Enemy);
        if (order != null)
        {
            allies.Sort(order);
            enemies.Sort(order);
        }
        var leftRows = new List<CanvasGroup>();
        var rightRows = new List<CanvasGroup>();
        var leftRects = new List<RectTransform>();
        var rightRects = new List<RectTransform>();
        w.BuildTeam(teams, Team.Ally, allies, columns, half, showScores, leftRows, leftRects);
        w.BuildTeam(teams, Team.Enemy, enemies, columns, half, showScores, rightRows, rightRects);
        // Cascade : les deux tables avancent ensemble, ligne par ligne
        for (int i = 0; i < Mathf.Max(leftRows.Count, rightRows.Count); i++)
        {
            if (i < leftRows.Count) { w.Rows.Add(leftRows[i]); w.RowRects.Add(leftRects[i]); }
            if (i < rightRows.Count) { w.Rows.Add(rightRows[i]); w.RowRects.Add(rightRects[i]); }
        }

        if (!string.IsNullOrEmpty(hint))
        {
            w.Hint = kit.Text(w.Rect, "Hint", DSTextStyle.Caption, kit.P.InkSecondary, hint, TextAlignmentOptions.Center);
            UIKit.Fixed(w.Hint, width, B.HintHeight);
            UIKit.HudShadow(w.Hint);
        }
        return w;
    }

    private void BuildTeam(Transform parent, Team team, List<PlayerMatchStats> players, IReadOnlyList<ScoreboardColumn> columns,
                           float width, bool showScores, List<CanvasGroup> rows, List<RectTransform> rects)
    {
        bool enemy = team == Team.Enemy;
        var teamColor = enemy ? kit.P.TeamEnemy : kit.P.TeamAlly;
        var plate = ChamferedPlate.Build(kit, parent, enemy ? "Enemy" : "Ally", enemy ? PlateCorners.BottomRight : PlateCorners.BottomLeft);
        UIKit.Fixed(plate.Rect, width, -1f);
        // La colonne pilote la hauteur de la plaque (les polygones sont hors layout)
        var plateLayout = plate.Rect.gameObject.AddComponent<VerticalLayoutGroup>();
        plateLayout.childControlHeight = true;
        plateLayout.childControlWidth = true;
        plateLayout.childForceExpandWidth = true;
        plateLayout.childForceExpandHeight = false;
        var column = kit.Column(plate.Rect, "Layout", 0f, TextAnchor.UpperLeft, UIKit.Pad(0f, 0f, 0f, B.TeamPadBottom));

        // En-tete d'equipe : degrade plate -> transparent, TeamMark, nom, total (si scores)
        var head = kit.Row(column, "Head", BlockforgeTheme.Space2, TextAnchor.MiddleLeft, UIKit.Pad(B.HeadPadX, 0f));
        UIKit.Fixed(head, -1f, B.HeadHeight);
        var headTint = kit.Polygon(head, "Tint");
        UIKit.Stretch(headTint.rectTransform);
        UIKit.IgnoreLayout(headTint);
        var plateColor = enemy ? kit.P.PlateEnemy : kit.P.PlateAlly;
        headTint.SetColors(enemy ? BlockforgeTheme.WithAlpha(plateColor, 0f) : plateColor, enemy ? plateColor : BlockforgeTheme.WithAlpha(plateColor, 0f));
        if (enemy)
        {
            var headTotal = ShowTotal(head, players, showScores);
            kit.Spacer(head);
            var name = kit.Text(head, "Name", DSTextStyle.Label, kit.P.InkPrimary, MetaLabels.TeamName(team), TextAlignmentOptions.MidlineRight);
            UIKit.HudShadow(name);
            kit.TeamMark(head, team, BlockforgeTheme.IconSize.Sm);
        }
        else
        {
            kit.TeamMark(head, team, BlockforgeTheme.IconSize.Sm);
            var name = kit.Text(head, "Name", DSTextStyle.Label, kit.P.InkPrimary, MetaLabels.TeamName(team));
            UIKit.HudShadow(name);
            kit.Spacer(head);
            ShowTotal(head, players, showScores);
        }

        // Colonnes : joueur 46 %, le reste reparti
        int n = columns.Count;
        float playerW = width * B.PlayerColumn;
        float cellW = (width - playerW) / Mathf.Max(1, n);

        var header = kit.Row(column, "Columns", 0f, TextAnchor.MiddleLeft);
        UIKit.Fixed(header, -1f, B.ColumnHeadHeight);
        var playerHead = kit.Caps(header, "Player", BlockforgeTheme.Type.Label.Size - 1f, 600, BlockforgeTheme.Type.Label.LetterSpacingEm, kit.P.InkMuted, "Player");
        PadCell(playerHead, playerW);
        foreach (var c in columns)
        {
            var h = kit.Caps(header, c.Key, BlockforgeTheme.Type.Label.Size - 1f, 600, BlockforgeTheme.Type.Label.LetterSpacingEm, kit.P.InkMuted, c.Label, TextAlignmentOptions.MidlineRight);
            PadCell(h, cellW);
        }

        var sums = new int[n];
        for (int i = 0; i < players.Count; i++)
        {
            var p = players[i];
            var row = kit.Row(column, "Row_" + p.DisplayName, 0f, TextAnchor.MiddleLeft);
            UIKit.Fixed(row, -1f, B.RowHeight);
            var bg = row.gameObject.AddComponent<Image>();
            bg.raycastTarget = false;
            bg.color = p.IsLocal ? kit.P.PlateSelf : i % 2 == 0 ? BlockforgeTheme.WithAlpha(kit.P.InkPrimary, B.StripeAlpha) : Color.clear;
            var group = kit.Group(row);
            rows.Add(group);
            rects.Add(row);

            var who = kit.Row(row, "Who", BlockforgeTheme.Space2, TextAnchor.MiddleLeft, UIKit.Pad(B.CellPadX, 0f));
            UIKit.Fixed(who, playerW, B.RowHeight);
            kit.TeamMark(who, team, BlockforgeTheme.IconSize.Sm, p.IsLocal);
            var nameSpec = new TypeSpec(FontFamily.Sans, B.PlayerSize, B.RowHeight, p.IsLocal ? 600 : 500, 0f);
            var nameText = kit.Text(who, "Name", nameSpec, kit.P.InkPrimary, p.DisplayName);
            UIKit.Ellipsis(nameText);
            UIKit.Flexible(nameText);
            UIKit.HudShadow(nameText);
            for (int c = 0; c < n; c++)
            {
                int v = columns[c].Value(p);
                sums[c] += v;
                var cell = kit.Mono(row, columns[c].Key, B.CellSize, p.IsLocal ? 600 : 500, kit.P.InkPrimary, BlockforgeTheme.Num(v), TextAlignmentOptions.MidlineRight);
                PadCell(cell, cellW);
                UIKit.HudShadow(cell);
            }
        }

        // Pied : sommes
        var foot = kit.Row(column, "Team", 0f, TextAnchor.MiddleLeft);
        UIKit.Fixed(foot, -1f, B.RowHeight);
        var line = kit.Image(foot, "Line", BlockforgeTheme.WithAlpha(kit.P.InkPrimary, B.FooterLineAlpha));
        UIKit.TopStrip(line.rectTransform, 0f, BlockforgeTheme.ControlBorder);
        UIKit.IgnoreLayout(line);
        var teamLabel = kit.Text(foot, "Label", new TypeSpec(FontFamily.Sans, B.PlayerSize, B.RowHeight, 600, 0f), kit.P.InkSecondary, "Team");
        PadCell(teamLabel, playerW);
        for (int c = 0; c < n; c++)
        {
            var cell = kit.Mono(foot, columns[c].Key, B.CellSize, 600, kit.P.InkSecondary, BlockforgeTheme.Num(sums[c]), TextAlignmentOptions.MidlineRight);
            PadCell(cell, cellW);
        }
        var footGroup = kit.Group(foot);
        rows.Add(footGroup);
        rects.Add(foot);
    }

    private TextMeshProUGUI ShowTotal(Transform head, List<PlayerMatchStats> players, bool show)
    {
        int total = 0;
        foreach (var p in players) total += p.Score;
        var text = kit.Mono(head, "Total", BlockforgeTheme.Board.TotalSize, 600, kit.P.InkPrimary, BlockforgeTheme.Num(total));
        text.gameObject.SetActive(show);
        return text;
    }

    private static void PadCell(TMP_Text text, float width)
    {
        UIKit.Fixed(text, width, -1f);
        text.margin = new Vector4(BlockforgeTheme.Board.CellPadX, 0f, BlockforgeTheme.Board.CellPadX, 0f);
    }
}
