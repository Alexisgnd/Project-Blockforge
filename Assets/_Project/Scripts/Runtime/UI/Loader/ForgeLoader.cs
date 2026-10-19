using System;
using UnityEngine;
using UnityEngine.UIElements;

// =========================================================
// FORGE LOADER : LE MARK ANIME (element UI Toolkit reutilisable)
// Un seul element, quatre calques (bras gauche, droit, bas,
// coeur) + l'anneau fin du verrouillage, pilotes par une seule
// timeline en boucle (ForgeTimeline, 2 600 ms).
// - Size : hauteur rendue en px (largeur = Size x 300 / 326).
//   0 = taille lue dans la USS (--forge-size, LoaderTheme.uss).
//   Sous --forge-min-size (48 px) : seulement le glyph statique.
// - ReduceMotion : suit BlockforgeTheme.ReduceMotion (reglage du
//   joueur, defaut = systeme) tant qu'on ne le force pas.
// - Finish(onLocked) : un chargement reel vient de finir ; le
//   mark rejoint l'etat verrouille par le plus court (<= 680 ms,
//   jamais de coupe sur un mark a moitie assemble), s'y tient,
//   puis onLocked est appele (la transition peut partir).
// Couleurs du logo : LoaderTheme.uss (classe .forge-loader),
// jamais en dur ici ; elles n'appartiennent qu'au logo.
// =========================================================

[UxmlElement]
public partial class ForgeLoader : VisualElement
{
    public const string UssClass = "forge-loader";
    public const string GlyphClass = "forge-loader__glyph";
    public const string GlyphModeClass = "forge-loader--glyph";

    // Ralenti / accelere toutes les instances (scene LoaderPreview)
    public static float SpeedMultiplier = 1f;

    // Duree du retour de la couleur du coeur a l'or quand on tient la pose
    private const float HoldSettleMs = 160f;
    private const float FallbackSize = 84f;
    private const float FallbackMinSize = 48f;

    private static readonly CustomStyleProperty<Color> ArmProp = new("--forge-arm");
    private static readonly CustomStyleProperty<Color> CoreProp = new("--forge-core");
    private static readonly CustomStyleProperty<Color> DimProp = new("--forge-core-dim");
    private static readonly CustomStyleProperty<Color> HotProp = new("--forge-core-hot");
    private static readonly CustomStyleProperty<Color> BreathProp = new("--forge-core-breath");
    private static readonly CustomStyleProperty<Color> RingProp = new("--forge-ring");
    private static readonly CustomStyleProperty<float> SizeProp = new("--forge-size");
    private static readonly CustomStyleProperty<float> MinSizeProp = new("--forge-min-size");

    private enum State { Playing, Finishing, Held }

    private readonly ForgeLayer left, right, bottom, core, ring;
    private readonly VisualElement glyph;

    private ForgePalette palette;
    private Color ringColor;
    private float size, ussSize, minSize = FallbackMinSize;
    private bool? reduceOverride;

    private State state = State.Playing;
    private float t;         // temps dans le cycle (ms)
    private float reducedT;  // horloge de la respiration reduite
    private float holdT;
    private Color holdFrom;
    private FinishPlan plan;
    private Action onLocked;
    private float lastTick = -1f;
    private IVisualElementScheduledItem ticker;

    public event Action<ForgeLoader> PoseApplied; // le wordmark du splash suit le mark
    public event Action<ForgeLoader> CycleCompleted;

    public MarkPose Pose { get; private set; }
    public float TimeMs => t;
    public bool IsLocked => state == State.Held;
    public bool IsGlyph => ResolvedSize < minSize;
    public float ResolvedSize => size > 0f ? size : ussSize > 0f ? ussSize : FallbackSize;

    [UxmlAttribute("size")]
    public float Size
    {
        get => size;
        set
        {
            size = Mathf.Max(0f, value);
            ApplySize();
        }
    }

    public bool ReduceMotion
    {
        get => reduceOverride ?? BlockforgeTheme.ReduceMotion;
        set => reduceOverride = value;
    }

    // Revient au reglage global (BlockforgeTheme.ReduceMotion)
    public void FollowGlobalReduceMotion() => reduceOverride = null;

    public ForgeLoader()
    {
        AddToClassList(UssClass);
        pickingMode = PickingMode.Ignore;
        style.flexShrink = 0;

        left = AddLayer(new ForgeLayer(ForgeMarkGeometry.LeftArm));
        right = AddLayer(new ForgeLayer(ForgeMarkGeometry.RightArm));
        bottom = AddLayer(new ForgeLayer(ForgeMarkGeometry.BottomArm));
        core = AddLayer(new ForgeLayer(ForgeMarkGeometry.Core));
        ring = AddLayer(new ForgeLayer(null));

        // Mise a l'echelle du coeur et de l'anneau autour du pivot (150, 158)
        var origin = new TransformOrigin(
            Length.Percent(ForgeMarkGeometry.Pivot.x / ForgeMarkGeometry.BoxWidth * 100f),
            Length.Percent(ForgeMarkGeometry.Pivot.y / ForgeMarkGeometry.BoxHeight * 100f));
        core.style.transformOrigin = origin;
        ring.style.transformOrigin = origin;

        glyph = new VisualElement { name = "glyph", pickingMode = PickingMode.Ignore };
        glyph.AddToClassList(GlyphClass);
        glyph.style.position = Position.Absolute;
        glyph.style.left = glyph.style.top = glyph.style.right = glyph.style.bottom = 0;
        hierarchy.Add(glyph);

        palette = new ForgePalette { arm = Color.white, core = Color.white, dim = Color.gray, hot = Color.white, breath = Color.white };
        ringColor = Color.white;

        RegisterCallback<CustomStyleResolvedEvent>(OnStyleResolved);
        RegisterCallback<AttachToPanelEvent>(_ => StartTicking());
        RegisterCallback<DetachFromPanelEvent>(_ => StopTicking());
        ApplySize();
        ApplyPose();
    }

    private ForgeLayer AddLayer(ForgeLayer layer)
    {
        hierarchy.Add(layer);
        return layer;
    }

    // ---------- API ----------

    // Repart du debut du cycle (nouveau chargement)
    public void Restart()
    {
        state = State.Playing;
        t = 0f;
        reducedT = 0f;
        onLocked = null;
        lastTick = -1f;
        ApplyPose();
    }

    // Le chargement reel est fini : rejoindre l'etat verrouille, le tenir, puis appeler onLocked
    public void Finish(Action locked)
    {
        if (state == State.Held)
        {
            locked?.Invoke();
            return;
        }
        onLocked += locked;
        if (state == State.Finishing)
            return;

        if (IsGlyph || ReduceMotion)
        {
            EnterHeld(Pose.coreColor);
            return;
        }

        plan = ForgeTimeline.PlanFinish(t);
        switch (plan.mode)
        {
            case FinishMode.Hold:
                EnterHeld(Pose.coreColor);
                return;
            case FinishMode.Wrap:
                // Temps vide : le mark est invisible, repartir de 0 n'est pas une coupe
                t = 0f;
                state = State.Finishing;
                break;
            default:
                state = State.Finishing;
                break;
        }
    }

    // Avance la timeline de dtMs (public pour les tests et l'apercu)
    public void Advance(float dtMs)
    {
        if (dtMs > 0f)
            Step(dtMs);
        ApplyPose();
    }

    private void Step(float dtMs)
    {
        if (ReduceMotion || IsGlyph)
        {
            reducedT += dtMs;
            if (state == State.Finishing)
                EnterHeld(Pose.coreColor); // reglage bascule pendant une fin : tenir tout de suite
            else if (state == State.Held)
                holdT += dtMs;
            return;
        }

        switch (state)
        {
            case State.Playing:
                t += dtMs;
                while (t >= ForgeTimeline.CycleMs)
                {
                    t -= ForgeTimeline.CycleMs;
                    CycleCompleted?.Invoke(this);
                }
                break;

            case State.Finishing:
                if (plan.mode == FinishMode.Reverse)
                {
                    t -= dtMs;
                    if (t <= plan.targetMs)
                    {
                        t = plan.targetMs;
                        EnterHeld(palette.core);
                    }
                }
                else
                {
                    t += dtMs;
                    if (t >= plan.targetMs)
                    {
                        t = plan.targetMs;
                        EnterHeld(palette.core);
                    }
                }
                break;

            case State.Held:
                holdT += dtMs;
                break;
        }
    }

    private void EnterHeld(Color fromColor)
    {
        state = State.Held;
        holdT = 0f;
        holdFrom = fromColor;
        ApplyPose();
        var callback = onLocked;
        onLocked = null;
        callback?.Invoke();
    }

    // ---------- Horloge ----------

    private void StartTicking()
    {
        lastTick = -1f;
        if (ticker == null)
            ticker = schedule.Execute(Tick).Every(0);
        else
            ticker.Resume();
    }

    private void StopTicking() => ticker?.Pause();

    private void Tick(TimerState _)
    {
        float now = Time.realtimeSinceStartup;
        float dt = lastTick < 0f ? 0f : (now - lastTick) * 1000f * SpeedMultiplier;
        lastTick = now;
        Advance(Mathf.Min(dt, 250f)); // un accroc (activation de scene) ne saute pas de phase
    }

    // ---------- Rendu ----------

    private void ApplyPose()
    {
        MarkPose pose;
        if (state == State.Held)
        {
            pose = ForgeTimeline.Locked(palette);
            pose.coreColor = Color.Lerp(holdFrom, palette.core, ForgeTimeline.Ease(Mathf.Clamp01(holdT / HoldSettleMs)));
        }
        else if (ReduceMotion || IsGlyph)
        {
            pose = ForgeTimeline.Reduced(reducedT, palette);
        }
        else
        {
            pose = ForgeTimeline.Evaluate(t, palette);
        }
        Pose = pose;

        float k = ResolvedSize / ForgeMarkGeometry.BoxHeight;
        ApplyArm(left, pose.left, k);
        ApplyArm(right, pose.right, k);
        ApplyArm(bottom, pose.bottom, k);

        core.style.scale = new Scale(new Vector3(pose.coreScale, pose.coreScale, 1f));
        core.style.opacity = pose.coreAlpha;
        core.SetColor(pose.coreColor);

        if (pose.ringVisible)
        {
            ring.style.display = DisplayStyle.Flex;
            ring.style.scale = new Scale(new Vector3(pose.ringScale, pose.ringScale, 1f));
            ring.style.opacity = pose.ringAlpha;
            // Trait en unites du mark apres mise a l'echelle de l'anneau
            ring.SetRing(ringColor, pose.ringStroke / Mathf.Max(0.01f, pose.ringScale));
        }
        else
        {
            ring.style.display = DisplayStyle.None;
        }

        PoseApplied?.Invoke(this);
    }

    private static void ApplyArm(ForgeLayer layer, ArmPose arm, float k)
    {
        layer.style.translate = new Translate(arm.offset.x * k, arm.offset.y * k);
        layer.style.opacity = arm.alpha;
    }

    private void ApplySize()
    {
        float h = ResolvedSize;
        style.height = h;
        style.width = h * ForgeMarkGeometry.BoxWidth / ForgeMarkGeometry.BoxHeight;

        bool glyphMode = IsGlyph;
        EnableInClassList(GlyphModeClass, glyphMode);
        var layerDisplay = glyphMode ? DisplayStyle.None : DisplayStyle.Flex;
        left.style.display = right.style.display = bottom.style.display = core.style.display = layerDisplay;
        if (glyphMode)
            ring.style.display = DisplayStyle.None;
        glyph.style.display = glyphMode ? DisplayStyle.Flex : DisplayStyle.None;
        if (glyphMode && state == State.Finishing)
            EnterHeld(palette.core);
    }

    private void OnStyleResolved(CustomStyleResolvedEvent evt)
    {
        var s = evt.customStyle;
        if (s.TryGetValue(ArmProp, out var arm)) palette.arm = arm;
        if (s.TryGetValue(CoreProp, out var gold)) palette.core = gold;
        if (s.TryGetValue(DimProp, out var dim)) palette.dim = dim;
        if (s.TryGetValue(HotProp, out var hot)) palette.hot = hot;
        if (s.TryGetValue(BreathProp, out var breath)) palette.breath = breath;
        ringColor = s.TryGetValue(RingProp, out var rc) ? rc : palette.core;
        if (s.TryGetValue(SizeProp, out var ussPx)) ussSize = ussPx;
        if (s.TryGetValue(MinSizeProp, out var minPx)) minSize = minPx;

        left.SetColor(palette.arm);
        right.SetColor(palette.arm);
        bottom.SetColor(palette.arm);
        ApplySize();
        ApplyPose();
    }

    // =========================================================
    // CALQUE : un polygone du mark (ou l'anneau), dessine a la
    // taille de l'element avec Painter2D
    // =========================================================

    private sealed class ForgeLayer : VisualElement
    {
        private readonly Vector2[] polygon;
        private Color color = Color.white;
        private float stroke = 1f;

        public ForgeLayer(Vector2[] polygon)
        {
            this.polygon = polygon;
            pickingMode = PickingMode.Ignore;
            style.position = Position.Absolute;
            style.left = style.top = style.right = style.bottom = 0;
            generateVisualContent += Draw;
        }

        public void SetColor(Color c)
        {
            if (c == color)
                return;
            color = c;
            MarkDirtyRepaint();
        }

        public void SetRing(Color c, float strokeUnits)
        {
            if (c == color && Mathf.Abs(strokeUnits - stroke) < 0.01f)
                return;
            color = c;
            stroke = strokeUnits;
            MarkDirtyRepaint();
        }

        private void Draw(MeshGenerationContext mgc)
        {
            float k = contentRect.height / ForgeMarkGeometry.BoxHeight;
            if (k <= 0f || float.IsNaN(k))
                return;

            var p = mgc.painter2D;
            if (polygon == null)
            {
                p.strokeColor = color;
                p.lineWidth = Mathf.Max(0.5f, stroke * k);
                p.BeginPath();
                p.Arc(ForgeMarkGeometry.Pivot * k, ForgeMarkGeometry.RingRadius * k, Angle.Degrees(0f), Angle.Degrees(360f));
                p.ClosePath();
                p.Stroke();
                return;
            }

            p.fillColor = color;
            p.BeginPath();
            p.MoveTo(polygon[0] * k);
            for (int i = 1; i < polygon.Length; i++)
                p.LineTo(polygon[i] * k);
            p.ClosePath();
            p.Fill();
        }
    }
}
