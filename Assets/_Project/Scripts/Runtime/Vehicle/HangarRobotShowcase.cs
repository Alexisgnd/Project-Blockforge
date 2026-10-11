using System.Collections.Generic;
using UnityEngine;

// =========================================================
// VITRINE DU ROBOT SELECTIONNE (menu d'accueil, hangar)
// Ecoute la selection de RobotSlotManager, assemble le
// blueprint du robot (RobotAssembler, comme la scene de
// test) sur la plateforme centrale et le fait tourner sur
// lui-meme avec un leger flottement.
// Mode vitrine : pas de Rigidbody ni de pilote, colliders
// retires. Les robots trop petits ou trop grands sont mis a
// l'echelle pour rester lisibles depuis la camera du menu.
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

    private Dictionary<string, BlockDefinition> defs;
    private Transform turntable;
    private GameObject current;
    private string currentRecordId;

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
        if (current == null)
            return;

        turntable.Rotate(0f, rotationSpeed * Time.deltaTime, 0f, Space.Self);
        float bob = Mathf.Sin(Time.time * bobSpeed) * bobAmplitude;
        turntable.localPosition = new Vector3(0f, hoverHeight + bob, 0f);
    }

    private void OnSelectionChanged(RobotRecord robot, int slotIndex)
    {
        Show(robot);
    }

    // =========================================================
    // ASSEMBLAGE
    // =========================================================

    public void Show(RobotRecord robot)
    {
        // Meme robot (rechargement des slots au retour sur l'accueil) : on garde
        // l'instance et son angle pour ne pas la voir "sauter"
        if (robot != null && current != null && robot.id == currentRecordId)
            return;

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
    }

    private void Clear()
    {
        if (current != null)
            Destroy(current);
        current = null;
        currentRecordId = null;
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
    }
}
