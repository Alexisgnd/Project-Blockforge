using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Blockforge.Meta;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;

// =========================================================
// TESTS D'UI DE FIN DE MATCH (Play mode, scene vide)
// - Victory et Defeat : meme composant, meme hierarchie ; seuls
//   bande, embleme, mot, couleurs et gagnants des awards changent ;
// - MatchEnded cache tout le HUD avant l'overlay ;
// - le score n'apparait qu'une fois sur l'ecran de resultats ;
// - le lisere du 1er suit les chanfreins (2 px partout) et la
//   carte est opaque ;
// - animations reduites : rien ne bouge, tout apparait.
// =========================================================

namespace Blockforge.Tests
{
    public class MatchEndUITests
    {
        private GameObject canvasGo;
        private bool reduceMotion;

        [SetUp]
        public void SetUp()
        {
            reduceMotion = BlockforgeTheme.ReduceMotion;
            MetaServices.Reset();
            canvasGo = new GameObject("TestCanvas", typeof(RectTransform));
            UIKit.ScreenCanvas(canvasGo, BlockforgeTheme.Order.MatchEnd);
        }

        [TearDown]
        public void TearDown()
        {
            BlockforgeTheme.ReduceMotion = reduceMotion;
            Time.timeScale = 1f;
            Object.Destroy(canvasGo);
            foreach (var flow in Object.FindObjectsByType<MatchEndFlow>())
                Object.Destroy(flow.gameObject);
            MetaServices.Reset();
        }

        private static MatchResult Result(MatchOutcome outcome) => MockServices.Instance.MatchResults.Build(outcome);

        private static List<string> Paths(Transform root)
        {
            var list = new List<string>();
            void Walk(Transform t, string prefix)
            {
                foreach (Transform c in t)
                {
                    string p = prefix + "/" + c.name;
                    list.Add(p);
                    Walk(c, p);
                }
            }
            Walk(root, "");
            return list;
        }

        [UnityTest]
        public IEnumerator VictoryAndDefeat_ShareOneStructure()
        {
            var victory = MatchEndOverlay.Show(canvasGo.transform, Result(MatchOutcome.Victory));
            var defeat = MatchEndOverlay.Show(canvasGo.transform, Result(MatchOutcome.Defeat));
            yield return null;

            CollectionAssert.AreEqual(Paths(victory.transform), Paths(defeat.transform), "Same prefab: same hierarchy for both outcomes");

            var vBand = victory.transform.Find("Band").GetComponent<UnityEngine.UI.Image>().color;
            var dBand = defeat.transform.Find("Band").GetComponent<UnityEngine.UI.Image>().color;
            Assert.AreEqual(BlockforgeTheme.Hud.TeamAlly, vBand);
            Assert.AreEqual(BlockforgeTheme.Hud.TeamEnemy, dBand);

            Assert.AreEqual("trophy", victory.transform.Find("Emblem").GetComponent<UnityEngine.UI.Image>().sprite.name);
            Assert.AreEqual("skull", defeat.transform.Find("Emblem").GetComponent<UnityEngine.UI.Image>().sprite.name);

            Assert.AreEqual("Victory", victory.transform.Find("Verdict/Word").GetComponent<TMP_Text>().text);
            Assert.AreEqual("Defeat", defeat.transform.Find("Verdict/Word").GetComponent<TMP_Text>().text);
        }

        [UnityTest]
        public IEnumerator Draw_HasNoBandAndNoEmblem()
        {
            var draw = MatchEndOverlay.Show(canvasGo.transform, Result(MatchOutcome.Draw));
            yield return null;
            Assert.IsFalse(draw.transform.Find("Band").gameObject.activeSelf);
            Assert.IsNull(draw.transform.Find("Emblem"));
            var word = draw.transform.Find("Verdict/Word").GetComponent<TMP_Text>();
            Assert.AreEqual("Draw", word.text);
            Assert.AreEqual(BlockforgeTheme.Hud.InkPrimary, word.color);
        }

        [UnityTest]
        public IEnumerator Overlay_ShowsTheScoreOnce()
        {
            var overlay = MatchEndOverlay.Show(canvasGo.transform, Result(MatchOutcome.Victory));
            yield return null;
            var texts = overlay.GetComponentsInChildren<TMP_Text>(false).Select(t => t.text).ToList();
            Assert.AreEqual(1, texts.Count(t => t == "25"));
            Assert.AreEqual(1, texts.Count(t => t == "19"));
        }

        [UnityTest]
        public IEnumerator MatchEnded_HidesEveryHudElementBeforeTheOverlay()
        {
            var hud = new GameObject("MatchHUD_Test", typeof(RectTransform));
            UIKit.ScreenCanvas(hud, BlockforgeTheme.Order.Hud);
            new GameObject("Reticle", typeof(RectTransform)).transform.SetParent(hud.transform);
            var flow = MatchEndFlow.Install(hud);
            yield return null;

            MockServices.Instance.MatchResults.Fire(MatchOutcome.Victory);
            Assert.IsFalse(hud.activeSelf, "HUD hidden on the same frame as MatchEnded");
            Assert.IsNotNull(flow.GetComponentInChildren<MatchEndOverlay>(), "Overlay shown after the HUD is hidden");
            Assert.AreEqual(0f, Time.timeScale, "Arena frozen");
            Object.Destroy(hud);
        }

        [UnityTest]
        public IEnumerator Results_ShowsTheScoreOnlyInTheHeader()
        {
            var results = ResultsScreen.Show(canvasGo.transform, Result(MatchOutcome.Victory));
            yield return null;
            var visible = results.GetComponentsInChildren<TMP_Text>(false);
            var board = results.transform.Find("Stack/Board");
            // Hors du tableau (dont le pied « Team » somme les kills, egaux au score en TDM)
            var ally = visible.Where(t => t.text == "25" && !t.transform.IsChildOf(board)).ToList();
            var enemy = visible.Where(t => t.text == "19" && !t.transform.IsChildOf(board)).ToList();
            Assert.AreEqual(1, ally.Count, "Ally score once");
            Assert.AreEqual(1, enemy.Count, "Enemy score once");
            Assert.IsTrue(ally[0].transform.IsChildOf(results.transform.Find("Stack/Header")));
            // Ni dans la plaque du haut du tableau, ni dans les en-tetes d'equipe
            Assert.IsFalse(board.Find("Scoreboard/Top/Content/Row/AllyScore").gameObject.activeSelf);
            Assert.IsFalse(board.Find("Scoreboard/Top/Content/Row/EnemyScore").gameObject.activeSelf);
            foreach (var total in board.GetComponentsInChildren<TMP_Text>(true).Where(t => t.name == "Total"))
                Assert.IsFalse(total.gameObject.activeSelf, "No team total in the headers");
            // Pas de « Play again »
            Assert.IsFalse(visible.Any(t => t.text.ToLowerInvariant().Contains("play again")));
        }

        [UnityTest]
        public IEnumerator Results_MvpFrameFollowsTheChamfersAndTheCardIsOpaque()
        {
            var results = ResultsScreen.Show(canvasGo.transform, Result(MatchOutcome.Victory));
            yield return null;
            var first = results.transform.Find("Stack/Podium/Rank1").GetComponent<ChamferedPlate>();
            Assert.IsNotNull(first.Frame, "1st card has a frame");
            Assert.IsNotNull(first.Opaque, "1st card has an opaque ground under its translucent fill");
            Assert.AreEqual(1f, first.Opaque.colorA.a, "Opaque ground");

            var frame = first.Frame.rectTransform;
            var w = BlockforgeTheme.Results.PodiumFrame;
            Assert.AreEqual(new Vector2(-w, -w), frame.offsetMin);
            Assert.AreEqual(new Vector2(w, w), frame.offsetMax);
            // Chanfrein exterieur = chanfrein + w (racine 2 - 1) : epaisseur constante de w le long de la coupe
            float expected = BlockforgeTheme.Results.PodiumChamfer + w * (Mathf.Sqrt(2f) - 1f);
            Assert.AreEqual(expected, first.Frame.cutTopLeft, 1e-4f);
            Assert.AreEqual(expected, first.Frame.cutTopRight, 1e-4f);
            Assert.AreEqual(0f, first.Frame.cutBottomLeft);
            // Le lisere est dessine derriere la carte
            Assert.Less(first.Frame.transform.GetSiblingIndex(), first.Opaque.transform.GetSiblingIndex());

            Assert.IsNull(results.transform.Find("Stack/Podium/Rank2").GetComponent<ChamferedPlate>().Frame, "Only the 1st has a frame");
        }

        [UnityTest]
        public IEnumerator ReduceMotion_NothingMovesEverythingAppears()
        {
            BlockforgeTheme.ReduceMotion = true;
            var overlay = MatchEndOverlay.Show(canvasGo.transform, Result(MatchOutcome.Victory));
            var verdict = (RectTransform)overlay.transform.Find("Verdict");
            var word = (RectTransform)overlay.transform.Find("Verdict/Word");
            var start = verdict.anchoredPosition;
            float t = 0f;
            while (t < 0.6f)
            {
                Assert.AreEqual(start, verdict.anchoredPosition, "Verdict never slides");
                Assert.AreEqual(Vector3.one, word.localScale, "Word never stamps");
                t += Time.unscaledDeltaTime;
                yield return null;
            }
            Assert.AreEqual(1f, overlay.GetComponent<CanvasGroup>().alpha, 1e-3f);
            Assert.AreEqual(1f, verdict.GetComponent<CanvasGroup>().alpha, 1e-3f);
            foreach (Transform card in overlay.transform.Find("Awards/Cards"))
                Assert.AreEqual(1f, card.GetComponent<CanvasGroup>().alpha, 1e-3f, card.name + " visible");
        }
    }
}
