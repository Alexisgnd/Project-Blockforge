using NUnit.Framework;
using UnityEngine;

// =========================================================
// TESTS DU FORGE LOADER (Play mode, Window > General > Test Runner)
// - courbe : l'AnimationCurve suit cubic-bezier(.2, .8, .2, 1) ;
// - timeline : verrouillage a 680 ms, decalage des bras visible,
//   un seul anneau par cycle (echantillonne a 60 fps), respiration,
//   sortie de 30 % ;
// - animations reduites : aucun deplacement, le coeur respire ;
// - fin de chargement : depuis n'importe quel instant du cycle, le
//   mark finit verrouille en 680 ms au plus, sans saut visible ;
// - session : etapes deduites de la progression, jamais de recul,
//   100 % seulement une fois le travail fini ; profil du mock.
// =========================================================

namespace Blockforge.Tests
{
    public class ForgeLoaderTests
    {
        private const float Frame = 1000f / 60f;

        private static readonly ForgePalette Pal = new()
        {
            arm = new Color(0f, 0.725f, 0.933f),
            core = new Color(1f, 0.827f, 0.294f),
            dim = new Color(0.353f, 0.29f, 0.086f),
            hot = new Color(1f, 0.949f, 0.69f),
            breath = new Color(1f, 0.898f, 0.541f),
        };

        [SetUp]
        public void UseExactEase() => ForgeTimeline.Ease = ForgeEase.Evaluate;

        // ---------- Courbe ----------

        [Test]
        public void Curve_MatchesCubicBezier()
        {
            var curve = ForgeEase.CreateCurve();
            for (int i = 0; i <= 200; i++)
            {
                float t = i / 200f;
                Assert.That(curve.Evaluate(t), Is.EqualTo(ForgeEase.Evaluate(t)).Within(0.006f), $"t = {t}");
            }
            Assert.That(ForgeEase.Evaluate(0.5f), Is.GreaterThan(0.8f), "ease-out : plus de 80 % a mi-course");
        }

        // ---------- Timeline ----------

        [Test]
        public void Lock_IsReachedAt680()
        {
            var p = ForgeTimeline.Evaluate(ForgeTimeline.LockEndMs, Pal);
            Assert.That(p.left.offset.magnitude, Is.LessThan(1e-3f));
            Assert.That(p.right.offset.magnitude, Is.LessThan(1e-3f));
            Assert.That(p.bottom.offset.magnitude, Is.LessThan(1e-3f));
            Assert.That(p.coreScale, Is.EqualTo(1f).Within(1e-3f));
            AssertColor(p.coreColor, Pal.core);

            var before = ForgeTimeline.Evaluate(ForgeTimeline.LockStartMs - 1f, Pal);
            Assert.That(before.coreScale, Is.EqualTo(ForgeTimeline.CoreInScale).Within(1e-4f));
            Assert.That(before.coreAlpha, Is.EqualTo(ForgeTimeline.CoreInAlpha).Within(1e-4f));
            AssertColor(before.coreColor, Pal.dim);

            var snap = ForgeTimeline.Evaluate(ForgeTimeline.SnapEndMs, Pal);
            Assert.That(snap.coreScale, Is.EqualTo(ForgeTimeline.CoreSnapScale).Within(1e-3f));
            AssertColor(snap.coreColor, Pal.hot);
        }

        [Test]
        public void Arms_AreStaggered_AndInPlaceByLock()
        {
            var p = ForgeTimeline.Evaluate(120f, Pal);
            Assert.That(p.left.alpha, Is.GreaterThan(p.right.alpha));
            Assert.That(p.right.alpha, Is.GreaterThan(p.bottom.alpha));
            Assert.That(p.bottom.alpha, Is.EqualTo(0f), "le bras du bas part a 160 ms");

            var start = ForgeTimeline.Evaluate(0f, Pal);
            Assert.That(start.left.offset, Is.EqualTo(ForgeMarkGeometry.LeftFrom));
            Assert.That(start.right.offset, Is.EqualTo(ForgeMarkGeometry.RightFrom));
            Assert.That(start.bottom.offset, Is.EqualTo(ForgeMarkGeometry.BottomFrom));

            var locked = ForgeTimeline.Evaluate(ForgeTimeline.LockStartMs, Pal);
            Assert.That(locked.left.alpha, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(locked.bottom.alpha, Is.EqualTo(1f).Within(1e-4f));
            Assert.That(locked.bottom.offset.magnitude, Is.LessThan(1e-3f));
        }

        [Test]
        public void Ring_FiresOncePerCycle_At60Fps()
        {
            int fires = 0;
            bool was = false;
            for (float ms = 0f; ms < ForgeTimeline.CycleMs * 3f; ms += Frame)
            {
                var p = ForgeTimeline.Evaluate(ms, Pal);
                if (p.ringVisible && !was)
                    fires++;
                was = p.ringVisible;
            }
            Assert.That(fires, Is.EqualTo(3));

            var first = ForgeTimeline.Evaluate(ForgeTimeline.LockStartMs, Pal);
            Assert.That(first.ringScale, Is.EqualTo(ForgeTimeline.RingFromScale).Within(1e-3f));
            Assert.That(first.ringAlpha, Is.EqualTo(ForgeTimeline.RingFromAlpha).Within(1e-3f));
            Assert.That(first.ringStroke, Is.EqualTo(ForgeTimeline.RingFromStroke).Within(1e-3f));
            var late = ForgeTimeline.Evaluate(ForgeTimeline.LockEndMs - 0.01f, Pal);
            Assert.That(late.ringScale, Is.EqualTo(ForgeTimeline.RingToScale).Within(0.01f));
            Assert.That(late.ringAlpha, Is.LessThan(0.01f));
        }

        [Test]
        public void Core_BreathesOnce_ThenExitDrifts30Percent()
        {
            AssertColor(ForgeTimeline.Evaluate(ForgeTimeline.BreathPeakMs, Pal).coreColor, Pal.breath);
            AssertColor(ForgeTimeline.Evaluate(ForgeTimeline.ExitStartMs - 0.01f, Pal).coreColor, Pal.core, 0.01f);

            var end = ForgeTimeline.Evaluate(ForgeTimeline.ExitEndMs, Pal);
            Assert.That(Vector2.Distance(end.left.offset, ForgeMarkGeometry.LeftFrom * 0.3f), Is.LessThan(1e-3f));
            Assert.That(end.left.alpha, Is.EqualTo(0f).Within(1e-4f));
            Assert.That(end.coreScale, Is.EqualTo(ForgeTimeline.CoreOutScale).Within(1e-3f));
            Assert.That(end.coreAlpha, Is.EqualTo(0f).Within(1e-4f));

            var empty = ForgeTimeline.Evaluate(2450f, Pal);
            Assert.That(empty.coreAlpha + empty.left.alpha + empty.right.alpha + empty.bottom.alpha, Is.EqualTo(0f));
        }

        [Test]
        public void ReducedMotion_NeverTranslates_AndBreathes()
        {
            float minK = 1f, maxK = 0f;
            for (float ms = 0f; ms < 5000f; ms += Frame)
            {
                var p = ForgeTimeline.Reduced(ms, Pal);
                Assert.That(p.left.offset, Is.EqualTo(Vector2.zero));
                Assert.That(p.right.offset, Is.EqualTo(Vector2.zero));
                Assert.That(p.bottom.offset, Is.EqualTo(Vector2.zero));
                Assert.That(p.coreScale, Is.EqualTo(1f));
                Assert.That(p.ringVisible, Is.False);
                float k = Mathf.InverseLerp(Pal.core.b, Pal.breath.b, p.coreColor.b);
                minK = Mathf.Min(minK, k);
                maxK = Mathf.Max(maxK, k);
            }
            Assert.That(minK, Is.LessThan(0.01f));
            Assert.That(maxK, Is.GreaterThan(0.99f));
            AssertColor(ForgeTimeline.Reduced(ForgeTimeline.ReducedBreathMs * 0.5f, Pal).coreColor, Pal.breath);
        }

        // ---------- Fin de chargement ----------

        [Test]
        public void FinishPlan_IsNeverMoreThan680msAway()
        {
            for (float ms = 0f; ms < ForgeTimeline.CycleMs; ms += 5f)
                Assert.That(ForgeTimeline.PlanFinish(ms).distanceMs, Is.LessThanOrEqualTo(ForgeTimeline.LockEndMs), $"ms = {ms}");
        }

        [Test]
        public void Finish_FromAnyPointOfTheCycle_EndsLocked_WithoutCut()
        {
            for (float start = 0f; start < ForgeTimeline.CycleMs; start += 23f)
            {
                var loader = new ForgeLoader { ReduceMotion = false, Size = 190f };
                loader.Restart();
                loader.Advance(start);

                bool locked = false;
                loader.Finish(() => locked = true);
                var previous = loader.Pose;
                float elapsed = 0f;
                while (!locked && elapsed < 2000f)
                {
                    loader.Advance(Frame);
                    elapsed += Frame;
                    var pose = loader.Pose;
                    // Pas de saut visible : un bras visible ne bondit pas d'une frame a l'autre
                    if (previous.left.alpha > 0.05f && pose.left.alpha > 0.05f)
                        Assert.That(Vector2.Distance(previous.left.offset, pose.left.offset), Is.LessThan(25f), $"start = {start}");
                    if (previous.bottom.alpha > 0.05f && pose.bottom.alpha > 0.05f)
                        Assert.That(Vector2.Distance(previous.bottom.offset, pose.bottom.offset), Is.LessThan(25f), $"start = {start}");
                    previous = pose;
                }

                Assert.That(locked, Is.True, $"start = {start}");
                Assert.That(loader.IsLocked, Is.True);
                Assert.That(elapsed, Is.LessThanOrEqualTo(ForgeTimeline.LockEndMs + Frame), $"start = {start}");
                var held = loader.Pose;
                Assert.That(held.left.offset.magnitude + held.right.offset.magnitude + held.bottom.offset.magnitude, Is.LessThan(1e-3f));
                Assert.That(held.left.alpha * held.right.alpha * held.bottom.alpha, Is.EqualTo(1f).Within(1e-4f));
                Assert.That(held.coreScale, Is.EqualTo(1f).Within(1e-3f));
            }
        }

        [Test]
        public void Finish_InReducedMotion_HoldsAtOnce()
        {
            var loader = new ForgeLoader { ReduceMotion = true, Size = 84f };
            loader.Advance(900f);
            bool locked = false;
            loader.Finish(() => locked = true);
            Assert.That(locked, Is.True);
            Assert.That(loader.Pose.left.offset, Is.EqualTo(Vector2.zero));
        }

        [Test]
        public void BelowMinimumSize_ShowsGlyph_AndFinishesAtOnce()
        {
            var loader = new ForgeLoader { ReduceMotion = false, Size = 40f };
            Assert.That(loader.IsGlyph, Is.True);
            bool locked = false;
            loader.Finish(() => locked = true);
            Assert.That(locked, Is.True);
        }

        // ---------- Session et mock ----------

        [Test]
        public void Session_StepsFollowProgress_AndNeverRegress()
        {
            string[] steps = { "Entering the garage", "Loading your bays", "Placing the robot", "Ready" };
            Assert.That(LoadingSession.StepAt(steps, 0f), Is.EqualTo(steps[0]));
            Assert.That(LoadingSession.StepAt(steps, 0.5f), Is.EqualTo(steps[1]));
            Assert.That(LoadingSession.StepAt(steps, 0.99f), Is.EqualTo(steps[2]));
            Assert.That(LoadingSession.StepAt(steps, 1f), Is.EqualTo(steps[3]));

            var s = LoadingService.Begin(LoadContext.Silent, steps);
            s.Report(0.6f);
            s.Report(0.3f);
            Assert.That(s.Progress, Is.EqualTo(0.6f).Within(1e-5f), "pas de recul");
            s.Report(1f);
            Assert.That(s.Progress, Is.LessThan(1f), "100 % seulement quand c'est fini");
            s.MarkLoaded();
            Assert.That(s.Progress, Is.EqualTo(1f));
            Assert.That(s.Step, Is.EqualTo("Ready"));
            Assert.That(s.CanTransition, Is.True, "sans affichage, rien ne retient la transition");
            s.End();
        }

        [Test]
        public void MockProfile_IsFastThenSlowThenStallsThenQuick()
        {
            var p = new MockLoadProfile();
            Assert.That(MockLoad.ProgressAt(p, p.fastSeconds), Is.EqualTo(p.fastTo).Within(1e-3f));
            float slowEnd = p.fastSeconds + p.slowSeconds;
            Assert.That(MockLoad.ProgressAt(p, slowEnd), Is.EqualTo(p.slowTo).Within(1e-3f));
            Assert.That(MockLoad.ProgressAt(p, slowEnd + p.stallSeconds * 0.5f), Is.EqualTo(p.slowTo));
            Assert.That(MockLoad.ProgressAt(p, p.TotalSeconds), Is.EqualTo(1f));
            float fastRate = p.fastTo / p.fastSeconds, slowRate = (p.slowTo - p.fastTo) / p.slowSeconds;
            Assert.That(fastRate, Is.GreaterThan(slowRate * 4f));
        }

        private static void AssertColor(Color actual, Color expected, float tolerance = 1e-3f)
        {
            Assert.That(actual.r, Is.EqualTo(expected.r).Within(tolerance));
            Assert.That(actual.g, Is.EqualTo(expected.g).Within(tolerance));
            Assert.That(actual.b, Is.EqualTo(expected.b).Within(tolerance));
        }
    }
}
