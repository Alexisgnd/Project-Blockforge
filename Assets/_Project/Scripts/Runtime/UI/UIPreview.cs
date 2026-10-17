using Blockforge.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

// =========================================================
// BANC D'ESSAI DES ECRANS (scene UIPreview)
// Ouvre chacun des six ecrans avec les donnees mock, dans une
// scene vide : 1 Victory, 2 Defeat, 3 Results, 4 Workshop,
// 5 Store, 6 Tech Tree ; 0 Draw ; M bascule ReduceMotion et
// rejoue l'ecran courant. Pour les verifications de cadrage,
// choisir 1920 x 1080, 2560 x 1080 et 1280 x 800 dans le menu
// de taille de la Game view (ajoutees par Setup UIPreview Scene).
// Le bandeau d'aide en bas a gauche se masque avec H.
// =========================================================

public sealed class UIPreview : MonoBehaviour
{
    public enum Screen { Victory, Defeat, Draw, Results, Workshop, Store, TechTree }

    public Screen startScreen = Screen.Victory;

    private RectTransform root, stage, hint;
    private Screen current;

    private void Start()
    {
        UIKit.EnsureEventSystem();
        var go = new GameObject("PreviewCanvas", typeof(RectTransform));
        UIKit.ScreenCanvas(go, BlockforgeTheme.Order.Hub);
        root = (RectTransform)go.transform;

        // Arene de substitution derriere l'overlay : un fond sombre et chaud
        var arena = UIKit.Hud.Polygon(root, "ArenaStandIn");
        UIKit.Stretch(arena.rectTransform);
        arena.vertical = true;
        arena.SetColors(BlockforgeTheme.Hud.TeamEnemySoft, BlockforgeTheme.Hud.SurfaceRaised);

        stage = UIKit.Hud.Rect(root, "Stage");
        UIKit.Stretch(stage);

        var kit = UIKit.Dark;
        hint = kit.Rect(root, "Hint");
        kit.Element(hint, DSRole.PanelFloating);
        var text = kit.Text(hint, "Text", DSTextStyle.Caption, kit.P.InkSecondary,
                            "1 Victory · 2 Defeat · 0 Draw · 3 Results · 4 Workshop · 5 Store · 6 Tech Tree · M motion · H hide");
        UIKit.Stretch(text.rectTransform, BlockforgeTheme.Space3, 0f, BlockforgeTheme.Space3, 0f);
        UIKit.Place(hint, UIKit.BottomLeft, new Vector2(BlockforgeTheme.Space4, BlockforgeTheme.Space4),
                    new Vector2(UIKit.PreferredWidth(text) + 2f * BlockforgeTheme.Space3, BlockforgeTheme.ControlMd));
        hint.SetAsLastSibling();

        Open(startScreen);
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null) return;
        if (kb.digit1Key.wasPressedThisFrame) Open(Screen.Victory);
        if (kb.digit2Key.wasPressedThisFrame) Open(Screen.Defeat);
        if (kb.digit0Key.wasPressedThisFrame) Open(Screen.Draw);
        if (kb.digit3Key.wasPressedThisFrame) Open(Screen.Results);
        if (kb.digit4Key.wasPressedThisFrame) Open(Screen.Workshop);
        if (kb.digit5Key.wasPressedThisFrame) Open(Screen.Store);
        if (kb.digit6Key.wasPressedThisFrame) Open(Screen.TechTree);
        if (kb.hKey.wasPressedThisFrame) hint.gameObject.SetActive(!hint.gameObject.activeSelf);
        if (kb.mKey.wasPressedThisFrame)
        {
            BlockforgeTheme.ReduceMotion = !BlockforgeTheme.ReduceMotion;
            Toast.Info(BlockforgeTheme.ReduceMotion ? "Reduce motion on." : "Reduce motion off.");
            Open(current);
        }
    }

    public void Open(Screen screen)
    {
        current = screen;
        Time.timeScale = 1f;
        for (int i = stage.childCount - 1; i >= 0; i--)
            Destroy(stage.GetChild(i).gameObject);

        var mock = MockServices.Instance.MatchResults;
        switch (screen)
        {
            case Screen.Victory:
            case Screen.Defeat:
            case Screen.Draw:
            {
                var outcome = screen == Screen.Victory ? MatchOutcome.Victory : screen == Screen.Defeat ? MatchOutcome.Defeat : MatchOutcome.Draw;
                var overlay = MatchEndOverlay.Show(stage, mock.Build(outcome));
                overlay.Confirmed += () => Open(Screen.Results);
                break;
            }
            case Screen.Results:
            {
                var results = ResultsScreen.Show(stage, mock.Build(MatchOutcome.Victory));
                results.Continued += () => Open(Screen.Workshop);
                break;
            }
            default:
            {
                var kit = UIKit.Dark;
                var hub = kit.Rect(stage, "Hub");
                UIKit.Stretch(hub);
                var section = screen == Screen.Workshop ? HubSection.Workshop : screen == Screen.Store ? HubSection.Store : HubSection.TechTree;
                var bar = TopBar.Build(kit, hub, section, "freejam_chris", MetaServices.Wallet.Credits);
                MetaServices.Wallet.CreditsChanged += c => { if (bar != null) bar.SetCredits(c); };
                var host = HubScreenHost.Create(hub, bar, BlockforgeTheme.TopBar.Height);
                bar.transform.SetAsLastSibling();
                bar.Selected += s =>
                {
                    if (host.Handles(s)) host.Show(s);
                    else Toast.Info($"{TopBar.LabelOf(s)} lives in the main menu.");
                };
                host.Show(section);
                break;
            }
        }
        hint.SetAsLastSibling();
    }
}
