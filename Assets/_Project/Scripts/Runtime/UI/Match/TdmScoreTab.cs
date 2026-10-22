using System.Collections.Generic;
using Blockforge.Meta;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// =========================================================
// SCOREBOARD DE LA TOUCHE TAB (composant Scoreboard du DS)
// Tant que Tab est maintenue pendant le match (vivant ou
// spectateur) : le ScoreboardWidget du DS au centre de l'ecran,
// sur un voile scrim — plaque du haut (mode, carte · temps
// ecoule, les deux scores), puis une table par equipe, l'equipe
// du joueur a gauche, classee par score (kills x 100, assists x
// 50, degats / 10), la ligne du joueur en plate-self. Rafraichi
// deux fois par seconde tant qu'il est ouvert. Pas d'ouverture
// pendant le compte a rebours ni apres la fin (l'ecran de
// resultats prend le relais).
// =========================================================

public class TdmScoreTab : MonoBehaviour
{
    private TdmMatch match;
    private readonly UIKit kit = UIKit.Hud;
    private RectTransform root;
    private CanvasGroup group;
    private ScoreboardWidget board;
    private float nextRefresh;
    private bool open;

    private static readonly IComparer<PlayerMatchStats> ByScore =
        Comparer<PlayerMatchStats>.Create((a, b) => b.Score != a.Score ? b.Score.CompareTo(a.Score) : b.Kills.CompareTo(a.Kills));

    public static TdmScoreTab Create(TdmMatch match)
    {
        var go = new GameObject("[TdmScoreTab]", typeof(RectTransform));
        var tab = go.AddComponent<TdmScoreTab>();
        tab.match = match;
        tab.Build();
        return tab;
    }

    private void Build()
    {
        UIKit.ScreenCanvas(gameObject, BlockforgeTheme.Order.Hud + 5);
        GetComponent<GraphicRaycaster>().enabled = false;
        root = (RectTransform)transform;
        group = kit.Group(root);
        group.alpha = 0f;
        group.blocksRaycasts = false;
        var scrim = kit.Image(root, "Scrim", BlockforgeTheme.WithAlpha(kit.P.Scrim, 0.55f));
        UIKit.Stretch(scrim.rectTransform);
    }

    private void Update()
    {
        if (match == null)
            return;
        var kb = Keyboard.current;
        bool held = kb != null && kb.tabKey.isPressed && match.Phase == TdmPhase.Live
                    && !MatchPauseMenu.IsOpen && !MatchEndFlow.IsRunning;
        if (held && !open)
        {
            open = true;
            Rebuild();
            group.alpha = 1f;
        }
        else if (!held && open)
        {
            open = false;
            group.alpha = 0f;
        }
        if (open && Time.unscaledTime >= nextRefresh)
            Rebuild();
    }

    private void Rebuild()
    {
        nextRefresh = Time.unscaledTime + 0.5f;
        if (board != null && board.Rect != null)
            Destroy(board.Rect.gameObject);
        var snapshot = match.BuildResult(MatchOutcome.Draw);
        board = ScoreboardWidget.Build(kit, root, "Scoreboard", snapshot, ScoreboardColumn.Results, true,
                                       "Sorted by score · release Tab to close", BlockforgeTheme.Board.DefaultWidth, ByScore);
        UIKit.Place(board.Rect, UIKit.Center, Vector2.zero, new Vector2(BlockforgeTheme.Board.DefaultWidth, 0f));
        UIKit.Fit(board.Rect, false, true);
    }
}
