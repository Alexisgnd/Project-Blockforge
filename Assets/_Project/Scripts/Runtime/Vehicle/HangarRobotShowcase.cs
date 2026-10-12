using System.Collections.Generic;
using UnityEngine;

// =========================================================
// VITRINE DU ROBOT SELECTIONNE (menu d'accueil, hangar)
// Ecoute la selection de RobotSlotManager, assemble le
// blueprint du robot (RobotAssembler, comme la scene de
// test) sur la plateforme centrale et le fait tourner sur
// lui-meme avec un leger flottement.
// Changement de robot : transition animee.
// - Sortie : le robot accelere sa rotation, se contracte et
//   s'enfonce dans la plateforme.
// - Entree : le nouveau robot sort de la plateforme en
//   tournant vite, grandit avec un leger depassement puis se
//   cale (rotation qui ralentit jusqu'a la vitesse normale).
// Option "Reduce motion" : changement instantane.
// Mode vitrine : pas de Rigidbody ni de pilote, colliders
// retires. Les robots trop petits ou trop grands sont mis a
// l'echelle pour rester lisibles depuis la camera du menu.
// FocusPoint / DisplaySize servent au zoom de la camera
// (HangarCameraZoom).
// Setup : Blockforge > Setup MainMenu Robot Showcase.
// =========================================================

public class HangarRobotShowcase : MonoBehaviour
{
    [Header("References (remplies par le setup)")]
    public RobotSlotManager slotManager;
    [Tooltip("Catalogue des blocs (Data/Blocks) : le blueprint reference les blocs par nom d'asset")]
    public BlockDefinition[] blocks;

    [Header("Assemblage")]
    [Tooltip("Taille monde d'une case (1 m = echelle du garage et des blocs)")]
    public float cellSize = 1f;

    [Header("Mise a l'echelle d'affichage (plus grande dimension, m)")]
    [Tooltip("Un robot plus petit est agrandi jusqu'a cette taille")]
    public float minDisplaySize = 3f;
    [Tooltip("Un robot plus grand est reduit a cette taille (plateforme de 14 m de diametre)")]
    public float maxDisplaySize = 7f;

    [Header("Animation")]
    [Tooltip("Vitesse de rotation (degres / seconde)")]
    public float rotationSpeed = 20f;
    [Tooltip("Hauteur de flottement au-dessus de la plateforme (m)")]
    public float hoverHeight = 0.3f;
    public float bobAmplitude = 0.08f;
    public float bobSpeed = 1.2f;

    [Header("Transition entre robots")]
    [Tooltip("Duree de la sortie de l'ancien robot (s)")]
    public float exitDuration = 0.28f;
    [Tooltip("Duree de l'entree du nouveau robot (s)")]
    public float enterDuration = 0.6f;
    [Tooltip("Profondeur d'enfoncement dans la plateforme (m)")]
    public float sinkDepth = 1.2f;
    [Tooltip("Vitesse de rotation ajoutee au pic de la transition (degres / seconde)")]
    public float spinBoost = 720f;

    // Centre du robot affiche (monde) et sa taille d'affichage (m), pour la camera
    public Vector3 FocusPoint => transform.position + Vector3.up * (hoverHeight + focusHeight);
    public float DisplaySize { get; private set; } = 4f;

    private Dictionary<string, BlockDefinition> defs;

    // Catalogue des blocs par nom d'asset (aussi utilise pour les apercus de l'ecran Build)
    public IReadOnlyDictionary<string, BlockDefinition> Catalog => defs;
    private Transform turntable;

    private GameObject current;
    private string currentRecordId;
    private float currentScale = 1f;
    private float enterT = 1f;

    private GameObject leaving;
    private float leavingScale = 1f;
    private float exitT;

    private RobotRecord pending;
    private bool hasPending;

    private float spin;               // vitesse ajoutee (degres / seconde), retombe vers 0
    private float focusHeight = 2f;   // hauteur du centre du robot au-dessus du turntable

    private void Awake()
    {
        defs = new Dictionary<string, BlockDefinition>();
        if (blocks != null)
        {
            foreach (var def in blocks)
            {
                if (def != null)
                    defs[def.name] = def;
            }
        }

        turntable = new GameObject("Turntable").transform;
        turntable.SetParent(transform, false);

        if (slotManager == null)
            slotManager = FindAnyObjectByType<RobotSlotManager>();
    }

    private void OnEnable()
    {
        if (slotManager != null)
        {
            slotManager.SelectionChanged += OnSelectionChanged;
            if (slotManager.SelectedRobot != null)
                Show(slotManager.SelectedRobot);
        }
    }

    private void OnDisable()
    {
        if (slotManager != null)
            slotManager.SelectionChanged -= OnSelectionChanged;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        AnimateTransition(dt);

        if (current == null && leaving == null)
            return;

        // La rotation ajoutee par la transition retombe en douceur
        spin = Mathf.MoveTowards(spin, 0f, spinBoost * 1.6f * dt);
        turntable.Rotate(0f, (rotationSpeed + spin) * dt, 0f, Space.Self);
        float bob = Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
        turntable.localPosition = new Vector3(0f, hoverHeight + bob, 0f);
    }

    private void OnSelectionChanged(RobotRecord robot, int slotIndex)
    {
        Show(robot);
    }

    // =========================================================
    // SELECTION -> TRANSITION
    // =========================================================

    public void Show(RobotRecord robot)
    {
        string id = robot?.id;

        // Meme robot (rechargement des slots au retour sur l'accueil) : on garde
        // l'instance et son angle pour ne pas la voir "sauter"
        if (!hasPending && leaving == null && current != null && id == currentRecordId)
            return;
        if (hasPending && pending?.id == id)
            return;

        if (BlockforgeTheme.ReduceMotion)
        {
            DestroyLeaving();
            Clear();
            hasPending = false;
            Build(robot, animate: false);
            return;
        }

        if (current != null)
        {
            // L'ancien robot sort ; le nouveau sera construit a la fin de la sortie
            DestroyLeaving();
            leaving = current;
            leavingScale = current.transform.localScale.x;
            exitT = 0f;
            current = null;
            currentRecordId = null;
            spin = Mathf.Max(spin, spinBoost * 0.5f);
        }

        pending = robot;
        hasPending = true;
        if (leaving == null)
            BuildPending();
    }

    private void AnimateTransition(float dt)
    {
        // ---------- Sortie ----------
        if (leaving != null)
        {
            exitT = Mathf.Min(1f, exitT + dt / Mathf.Max(0.01f, exitDuration));
            float e = exitT * exitT * exitT;                 // ease-in : part doucement, file a la fin
            spin = Mathf.Max(spin, spinBoost * e);
            leaving.transform.localScale = Vector3.one * leavingScale * Mathf.Lerp(1f, 0.05f, e);
            leaving.transform.localPosition = new Vector3(0f, -sinkDepth * e, 0f);
            if (exitT >= 1f)
            {
                DestroyLeaving();
                if (hasPending)
                    BuildPending();
            }
        }

        // ---------- Entree ----------
        if (current != null && enterT < 1f)
        {
            enterT = Mathf.Min(1f, enterT + dt / Mathf.Max(0.01f, enterDuration));
            float s = EaseOutBack(enterT);
            float rise = 1f - Mathf.Pow(1f - enterT, 3f);     // ease-out
            current.transform.localScale = Vector3.one * currentScale * Mathf.Max(0.001f, s);
            current.transform.localPosition = new Vector3(0f, -sinkDepth * (1f - rise), 0f);
        }
    }

    private void BuildPending()
    {
        var robot = pending;
        pending = null;
        hasPending = false;
        Build(robot, animate: true);
    }

    // =========================================================
    // ASSEMBLAGE
    // =========================================================

    private void Build(RobotRecord robot, bool animate)
    {
        Clear();
        if (robot == null || robot.data == null || robot.data.blocks == null || robot.data.blocks.Count == 0)
            return;

        var blueprint = robot.data;
        if (string.IsNullOrEmpty(blueprint.name))
            blueprint.name = robot.name;

        current = RobotAssembler.Build(blueprint, defs, cellSize, turntable, out int placed, out _);
        currentRecordId = robot.id;
        current.name = $"Showcase_{robot.name}";

        if (placed == 0)
        {
            Clear();
            return;
        }

        // Vitrine : aucune physique (le menu n'a pas besoin des colliders)
        foreach (var collider in current.GetComponentsInChildren<Collider>(true))
            Destroy(collider);

        FitToDisplaySize(current.transform);
        currentScale = current.transform.localScale.x;

        if (animate)
        {
            enterT = 0f;
            spin = spinBoost;
            current.transform.localScale = Vector3.one * 0.001f;
            current.transform.localPosition = new Vector3(0f, -sinkDepth, 0f);
        }
        else
        {
            enterT = 1f;
        }
    }

    private void Clear()
    {
        if (current != null)
            Destroy(current);
        current = null;
        currentRecordId = null;
    }

    private void DestroyLeaving()
    {
        if (leaving != null)
            Destroy(leaving);
        leaving = null;
    }

    // RobotAssembler pose le pivot au centre XZ de l'empreinte, base a y = 0 :
    // une echelle uniforme sur la racine garde le robot centre et pose.
    private void FitToDisplaySize(Transform root)
    {
        var renderers = root.GetComponentsInChildren<Renderer>();
        if (renderers.Length == 0)
            return;

        var bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        float size = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
        if (size <= 0.001f)
            return;

        float scale = 1f;
        if (size < minDisplaySize)
            scale = minDisplaySize / size;
        else if (size > maxDisplaySize)
            scale = maxDisplaySize / size;

        root.localScale = Vector3.one * scale;
        DisplaySize = size * scale;
        // Centre vertical du robot au-dessus de sa base (turntable), a l'echelle
        focusHeight = (bounds.center.y - root.position.y) * scale;
    }

    // Grandit, depasse legerement la taille finale, puis s'y cale
    private static float EaseOutBack(float t)
    {
        const float c1 = 1.4f;
        const float c3 = c1 + 1f;
        float u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }
}
