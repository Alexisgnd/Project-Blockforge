using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// =========================================================
// PART PICKER DANS LE GARAGE (pont avec le builder existant)
// Remplace la feuille d'inventaire uGUI (GarageInventoryController)
// par le PartPickerController en UI Toolkit, sans toucher au
// systeme de pose :
// - catalogue : les BlockDefinition de l'inventaire (toutes
//   possedees : pas encore de progression) converties en
//   PartDefinition (categorie, famille du PickerTheme, stats) ;
//   les blocs verrouilles (RobotLoadout.IsLocked) y sont, marques
//   locked : visibles avec un cadenas, jamais pris par la pince ;
// - budget : le CPU du robot en cours (GarageStatsHUD, une seule
//   reserve dans le jeu : toutes les pieces y puisent) ;
// - choisir une tuile = GarageInventoryController.SelectBlock :
//   la pince prend le bloc (GarageBuildController lit CurrentBlock) ;
// - Place / double clic / Entree / glisser : la pince garde le bloc
//   et le joueur revient au viewport (curseur verrouille) ;
// - pendant l'ouverture : HUD du builder (GarageCanvas) en fondu
//   jusqu'a 0, joueur, pince, raccourcis du garage (T, P, H, 1-2,
//   A / E) coupes ; ils reviennent une image apres la fermeture,
//   boutons de souris relaches (le clic qui ferme ne pose rien).
// - La feuille uGUI n'ecoute plus Tab (composant coupe), ses
//   donnees restent la source de la pince.
// Touche d'ouverture : voir openKey (Q du design en conflit avec
// annuler / le pas lateral du garage, cf. rapport).
// =========================================================

public class GaragePartPicker : MonoBehaviour
{
    [Header("References (remplies par le setup, retrouvees sinon)")]
    public GarageInventoryController inventory;
    public GarageBuildController build;
    public GarageStatsHUD statsHud;
    [Tooltip("HUD du builder (GarageCanvas) : masque pendant que le picker est ouvert")]
    public CanvasGroup builderHud;
    [Tooltip("Tuile d'inventaire existante du HUD (optionnelle) : un clic ouvre le picker")]
    public Button inventoryTile;

    [Header("Ouverture")]
    [Tooltip("Touche physique d'ouverture. Le Q du design est deja pris au garage (annuler, pas lateral AZERTY)")]
    public Key openKey = Key.Tab;

    [Header("Stats")]
    [Tooltip("Portee affichee des armes (m) : celle de WeaponFireController.maxRange")]
    public float weaponRangeMeters = 320f;

    private PartPickerController picker;
    private BlockInventoryAdapter catalog;
    private HudBudgetAdapter budget;
    private readonly List<MonoBehaviour> paused = new();
    private Coroutine resume;

    public PartPickerController Picker => picker;

    private void Start()
    {
        if (inventory == null) inventory = FindAnyObjectByType<GarageInventoryController>(FindObjectsInactive.Include);
        if (build == null) build = FindAnyObjectByType<GarageBuildController>(FindObjectsInactive.Include);
        if (statsHud == null) statsHud = FindAnyObjectByType<GarageStatsHUD>(FindObjectsInactive.Include);
        if (builderHud == null)
        {
            var canvas = GameObject.Find("GarageCanvas");
            if (canvas != null && !canvas.TryGetComponent(out builderHud))
                builderHud = canvas.AddComponent<CanvasGroup>();
        }
        if (inventory == null)
        {
            Debug.LogWarning("[GaragePartPicker] GarageInventoryController introuvable : picker inactif.");
            enabled = false;
            return;
        }

        picker = GetComponent<PartPickerController>();
        if (picker == null)
            picker = gameObject.AddComponent<PartPickerController>();
        picker.listenForOpenKey = false;

        catalog = new BlockInventoryAdapter(inventory.blocks, PickerTheme.Load(), weaponRangeMeters);
        budget = new HudBudgetAdapter(statsHud);
        picker.Bind(catalog, budget);

        picker.Selected += OnSelected;
        picker.Opened += OnOpened;
        picker.Closed += OnClosed;
        picker.VisibilityChanged += OnVisibility;

        if (inventoryTile != null)
            inventoryTile.onClick.AddListener(Open);

        // La feuille uGUI garde ses donnees mais n'ecoute plus Tab (apres son Start)
        StartCoroutine(RetireSheet());
    }

    private IEnumerator RetireSheet()
    {
        yield return null;
        if (inventory != null)
        {
            if (inventory.IsOpen)
                inventory.Close();
            inventory.enabled = false;
        }
    }

    private void OnDestroy()
    {
        if (picker != null)
        {
            picker.Selected -= OnSelected;
            picker.Opened -= OnOpened;
            picker.Closed -= OnClosed;
            picker.VisibilityChanged -= OnVisibility;
        }
        if (inventoryTile != null)
            inventoryTile.onClick.RemoveListener(Open);
    }

    private void Update()
    {
        budget?.Poll();
        if (picker == null || picker.IsOpen || resume != null)
            return;

        var kb = Keyboard.current;
        if (kb != null && openKey != Key.None && kb[openKey].wasPressedThisFrame && CanOpen())
            Open();
    }

    // Pas par-dessus une popup du garage (curseur libre) ni le dialogue de remplacement
    private bool CanOpen() =>
        !LoadoutReplaceDialog.IsOpen && !LoadoutReplaceDialog.ClosedThisFrame && Cursor.lockState == CursorLockMode.Locked;

    public void Open()
    {
        if (picker == null || picker.IsOpen)
            return;
        if (inventory.CurrentBlock != null && !RobotLoadout.IsLocked(inventory.CurrentBlock))
            picker.SetCurrent(inventory.CurrentBlock.name);
        picker.Open();
    }

    private void OnSelected(PartDefinition part)
    {
        var def = catalog.Lookup(part.id);
        // Bloc verrouille : jamais dans la pince (le picker ne le selectionne deja pas)
        if (def != null && !RobotLoadout.IsLocked(def) && def != inventory.CurrentBlock)
            inventory.SelectBlock(def);
    }

    private void OnOpened()
    {
        if (resume != null)
        {
            StopCoroutine(resume);
            resume = null;
        }
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        PauseGameplay();
    }

    private void OnClosed()
    {
        if (resume != null)
            StopCoroutine(resume);
        resume = StartCoroutine(ResumeGameplay());
    }

    private void OnVisibility(float shown)
    {
        if (builderHud == null)
            return;
        builderHud.alpha = 1f - shown;
        bool hudLive = shown <= 0f;
        builderHud.interactable = hudLive;
        builderHud.blocksRaycasts = hudLive;
    }

    // ---------- Gameplay coupe pendant l'ouverture ----------

    private void PauseGameplay()
    {
        paused.Clear();
        var candidates = new List<MonoBehaviour>();
        if (inventory.disableWhileOpen != null)
            candidates.AddRange(inventory.disableWhileOpen);
        candidates.Add(FindAnyObjectByType<GarageSaveController>());
        candidates.Add(FindAnyObjectByType<GarageToolSwitcher>());
        candidates.Add(FindAnyObjectByType<GarageControlsHint>());
        candidates.Add(build != null ? build.GetComponent<GarageHistory>() : null);
        foreach (var behaviour in candidates.Where(b => b != null).Distinct())
        {
            if (!behaviour.enabled)
                continue;
            behaviour.enabled = false;
            paused.Add(behaviour);
        }
    }

    // Une image plus tard et boutons relaches : Echap, Entree ou le double clic qui
    // ferment le picker ne sont pas repris par le garage (menu, pose d'un bloc)
    private IEnumerator ResumeGameplay()
    {
        yield return null;
        var mouse = Mouse.current;
        while (mouse != null && (mouse.leftButton.isPressed || mouse.rightButton.isPressed))
            yield return null;
        yield return null;

        foreach (var behaviour in paused)
        {
            if (behaviour != null)
                behaviour.enabled = true;
        }
        paused.Clear();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        resume = null;
    }

    // =========================================================
    // ADAPTATEURS
    // =========================================================

    // Catalogue du garage -> pieces du picker (toutes possedees ; verrouillees = locked)
    private sealed class BlockInventoryAdapter : IInventory
    {
        private readonly List<PartDefinition> parts = new();
        private readonly Dictionary<string, BlockDefinition> byId = new();

        public BlockInventoryAdapter(IEnumerable<BlockDefinition> blocks, PickerTheme theme, float weaponRange)
        {
            // Modules et arme healer verrouilles dans cette version : affiches avec un cadenas (Convert)
            var defs = (blocks ?? Enumerable.Empty<BlockDefinition>()).Where(b => b != null).ToList();
            foreach (var def in defs)
            {
                if (byId.ContainsKey(def.name))
                    continue;
                byId[def.name] = def;
                parts.Add(Convert(def, weaponRange));
            }
            PickerMockCatalog.ApplyWeaponRatios(parts);
        }

        public BlockDefinition Lookup(string id) => id != null && byId.TryGetValue(id, out var def) ? def : null;

        public bool IsOwned(string id) => id != null && byId.ContainsKey(id);
        public IReadOnlyList<PartDefinition> Owned => parts;
        public int NotOwnedCount => 0;
        public event System.Action Changed { add { } remove { } }

        private static PartDefinition Convert(BlockDefinition def, float weaponRange)
        {
            var category = CategoryOf(def);
            var part = new PartDefinition
            {
                id = def.name,
                name = def.blockName,
                category = category,
                family = FamilyOf(def, category),
                description = def.description,
                thumbnail = def.icon,
                previewPrefab = def.previewPrefab,
                cpuCost = def.costCpu,
                cpuPool = CpuPool.Base, // une seule reserve CPU dans le jeu
                locked = RobotLoadout.IsLocked(def),
                lockedReason = RobotLoadout.LockedReason,
            };

            switch (category)
            {
                case PartCategory.Blocks:
                    int faces = def.orientToFace ? 1 : 6; // un bloc de chassis accepte une piece sur ses six faces
                    part.stats.Add(new PartStat("cpu", "Base CPU", def.costCpu, def.category == BlockCategory.Chassis ? "per cube" : "CPU", "0"));
                    part.stats.Add(new PartStat("mass", "Mass", def.weightKg, "kg", "0"));
                    part.stats.Add(new PartStat("connections", "Connections", faces, faces == 1 ? "face" : "faces", "0"));
                    break;
                case PartCategory.Weapons:
                    var fire = WeaponFireController.Resolve(def);
                    part.stats.Add(new PartStat("damage", "Damage", fire.damage, "per shot", "0"));
                    part.stats.Add(new PartStat("fireRate", "Fire rate", fire.rate, "/s", "0.0"));
                    part.stats.Add(new PartStat("range", "Range", weaponRange, "m", "0"));
                    part.stats.Add(new PartStat("cpu", "CPU", def.costCpu, "CPU", "0"));
                    part.stats.Add(new PartStat("mass", "Mass", def.weightKg, "kg", "0"));
                    break;
                default:
                    part.stats.Add(new PartStat("cpu", "CPU", def.costCpu, "CPU", "0"));
                    part.stats.Add(new PartStat("mass", "Mass", def.weightKg, "kg", "0"));
                    break;
            }
            return part;
        }

        private static PartCategory CategoryOf(BlockDefinition def)
        {
            switch (def.category)
            {
                case BlockCategory.Mouvement: return PartCategory.Movement;
                case BlockCategory.Armes: return PartCategory.Weapons;
                case BlockCategory.Special: return PartCategory.Electronics;
                case BlockCategory.Defense:
                    // Nano : Blinder (IEM) = module, Mender / Constructor = arme healer ; electroplates = blindage
                    switch (RobotLoadout.Kind(def))
                    {
                        case LoadoutKind.Module: return PartCategory.Electronics;
                        case LoadoutKind.Weapon: return PartCategory.Weapons;
                        default: return PartCategory.Blocks;
                    }
                default: return PartCategory.Blocks;
            }
        }

        // Famille du jeu -> libelle des sous-filtres du PickerTheme
        private static string FamilyOf(BlockDefinition def, PartCategory category)
        {
            string f = ((def.family ?? "") + " " + def.name).ToLowerInvariant();
            switch (category)
            {
                case PartCategory.Blocks:
                    if (f.Contains("electroplate")) return "Armour";
                    if (f.Contains("rod")) return "Rods";
                    if (f.Contains("corner")) return "Corners";
                    if (f.Contains("slope") || f.Contains("cone") || f.Contains("pyramid")) return "Slopes";
                    return "Cubes";
                case PartCategory.Movement:
                    if (f.Contains("wheel")) return "Wheels";
                    if (f.Contains("leg")) return "Legs";
                    if (f.Contains("track")) return "Tracks";
                    if (f.Contains("hover")) return "Hover";
                    if (f.Contains("thruster")) return "Thrusters";
                    if (f.Contains("rotor")) return "Rotors";
                    if (f.Contains("rudder")) return "Rudders";
                    return def.family;
                case PartCategory.Weapons:
                    if (f.Contains("laser")) return "Lasers";
                    if (f.Contains("plasma")) return "Plasma";
                    if (f.Contains("rail")) return "Rail";
                    if (f.Contains("tesla")) return "Tesla";
                    if (f.Contains("flak")) return "Flak";
                    return "Support";
                case PartCategory.Electronics:
                    if (f.Contains("radar") || f.Contains("sensor")) return "Sensors";
                    if (f.Contains("shield")) return "Shields";
                    return "Modules";
                default:
                    return def.family;
            }
        }
    }

    // CPU du robot en cours (GarageStatsHUD, mis a jour par GarageBuildController)
    private sealed class HudBudgetAdapter : IRobotBudget
    {
        private readonly GarageStatsHUD hud;
        private int lastUsed = int.MinValue, lastMax = int.MinValue;

        public HudBudgetAdapter(GarageStatsHUD hud) { this.hud = hud; }

        public event System.Action Changed;

        public int Used(CpuPool pool) => hud != null ? hud.capacity : 0;
        public int Max(CpuPool pool) => hud != null ? hud.capacityMax : int.MaxValue;

        public void Poll()
        {
            if (hud == null || (hud.capacity == lastUsed && hud.capacityMax == lastMax))
                return;
            lastUsed = hud.capacity;
            lastMax = hud.capacityMax;
            Changed?.Invoke();
        }
    }
}
