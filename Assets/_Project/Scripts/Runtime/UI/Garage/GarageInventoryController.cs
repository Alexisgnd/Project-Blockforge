using System;
using System.Collections.Generic;
using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// =========================================================
// INVENTAIRE DU GARAGE (fenetre animee en bas d'ecran)
// TAB : ouvre/ferme. Filtres par categorie + recherche.
// Le gameplay s'abonne a BlockSelected pour la pince.
// =========================================================

public class GarageInventoryController : MonoBehaviour
{
    private static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

    [Header("Animation")]
    public RectTransform sheet;
    public float animationDuration = 0.25f;
    [Tooltip("La barre d'outils remonte avec le panneau")]
    public RectTransform liftWhileOpen;
    [Tooltip("Zone cliquable au-dessus du panneau qui referme l'inventaire")]
    public GameObject clickAway;

    [Header("Donnees")]
    public BlockDefinition[] blocks;

    [Header("Grille")]
    public InventoryItemCard cardTemplate;
    public RectTransform gridParent;

    [Header("Onglets categories (-1 = TOUT)")]
    public Button[] tabButtons;
    public Image[] tabBackgrounds;
    public TMP_Text[] tabLabels;
    public int[] tabCategories;
    public Color tabActiveBg = new(0.09f, 0.16f, 0.30f, 1f);
    public Color tabTextActive = new(0.86f, 0.91f, 0.96f);
    public Color tabTextInactive = new(0.48f, 0.56f, 0.66f);

    [Header("Recherche")]
    public TMP_InputField searchField;

    [Header("Fiche detail")]
    public TMP_Text detailName;
    public TMP_Text detailDescription;
    public TMP_Text detailType;
    public TMP_Text detailSize;
    public TMP_Text detailWeight;
    public TMP_Text detailResistance;
    public TMP_Text detailCost;

    [Header("Capacite")]
    public TMP_Text capacityText;
    public int capacityUsed = 680;
    public int capacityMax = 1000;

    [Header("Desactives pendant l'ouverture (joueur, pince...)")]
    public MonoBehaviour[] disableWhileOpen;

    public event Action<BlockDefinition> BlockSelected;
    public bool IsOpen { get; private set; }
    public BlockDefinition CurrentBlock => selected;

    private readonly List<InventoryItemCard> cards = new();
    private BlockDefinition selected;
    private int currentCategory = -1;
    private float sheetHeight;
    private float animT; // 0 ferme -> 1 ouvert
    private Vector2 liftBasePos;

    private void Awake()
    {
        sheetHeight = sheet.sizeDelta.y;
        if (liftWhileOpen != null)
            liftBasePos = liftWhileOpen.anchoredPosition;

        // Cartes instanciees depuis le template (desactive)
        foreach (var def in blocks)
        {
            if (def == null)
                continue;
            var card = Instantiate(cardTemplate, gridParent);
            card.gameObject.SetActive(true);
            card.Bind(def, OnCardClicked);
            cards.Add(card);
        }

        if (clickAway != null && clickAway.TryGetComponent<Button>(out var clickAwayButton))
            clickAwayButton.onClick.AddListener(Close);

        for (int i = 0; i < tabButtons.Length; i++)
        {
            int index = i;
            tabButtons[i].onClick.AddListener(() => SelectCategory(tabCategories[index]));
        }

        if (searchField != null)
            searchField.onValueChanged.AddListener(_ => ApplyFilter());
    }

    private void Start()
    {
        SelectCategory(-1);
        if (cards.Count > 0)
            SelectBlock(cards[0].Definition);
        RefreshCapacity();
        SetOpenImmediate(false);
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb != null && kb.tabKey.wasPressedThisFrame)
            Toggle();

        // Animation du panneau (temps non-scale pour rester fluide)
        float target = IsOpen ? 1f : 0f;
        if (!Mathf.Approximately(animT, target))
        {
            animT = Mathf.MoveTowards(animT, target, Time.unscaledDeltaTime / Mathf.Max(0.01f, animationDuration));
            float eased = Mathf.SmoothStep(0f, 1f, animT);
            sheet.anchoredPosition = new Vector2(0f, Mathf.Lerp(-sheetHeight, 0f, eased));
            if (liftWhileOpen != null)
                liftWhileOpen.anchoredPosition = liftBasePos + new Vector2(0f, sheetHeight * eased);
        }
    }

    // =========================================================
    // OUVERTURE / FERMETURE
    // =========================================================

    public void Toggle()
    {
        if (IsOpen) Close();
        else Open();
    }

    public void Open()
    {
        IsOpen = true;
        if (clickAway != null)
            clickAway.SetActive(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        SetGameplayEnabled(false);
    }

    public void Close()
    {
        IsOpen = false;
        if (clickAway != null)
            clickAway.SetActive(false);
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        SetGameplayEnabled(true);
    }

    private void SetOpenImmediate(bool open)
    {
        IsOpen = open;
        animT = open ? 1f : 0f;
        sheet.anchoredPosition = new Vector2(0f, open ? 0f : -sheetHeight);
        if (liftWhileOpen != null)
            liftWhileOpen.anchoredPosition = liftBasePos + new Vector2(0f, open ? sheetHeight : 0f);
        if (clickAway != null)
            clickAway.SetActive(open);
    }

    private void SetGameplayEnabled(bool enabled)
    {
        foreach (var behaviour in disableWhileOpen)
        {
            if (behaviour != null)
                behaviour.enabled = enabled;
        }
    }

    // =========================================================
    // FILTRES / SELECTION
    // =========================================================

    public void SelectCategory(int category)
    {
        currentCategory = category;

        for (int i = 0; i < tabButtons.Length; i++)
        {
            bool active = tabCategories[i] == category;
            tabBackgrounds[i].color = active ? tabActiveBg : Color.clear;
            tabLabels[i].color = active ? tabTextActive : tabTextInactive;
        }

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        string search = searchField != null ? searchField.text.Trim().ToUpperInvariant() : "";

        foreach (var card in cards)
        {
            bool categoryOk = currentCategory < 0 || (int)card.Definition.category == currentCategory;
            bool searchOk = search.Length == 0 || card.Definition.blockName.ToUpperInvariant().Contains(search);
            card.gameObject.SetActive(categoryOk && searchOk);
        }
    }

    // Clic sur une carte : selectionne le bloc puis referme la fenetre
    private void OnCardClicked(BlockDefinition def)
    {
        SelectBlock(def);
        if (IsOpen)
            Close();
    }

    public void SelectBlock(BlockDefinition def)
    {
        selected = def;

        foreach (var card in cards)
            card.SetSelected(card.Definition == def);

        detailName.text = def.blockName;
        detailDescription.text = def.description;
        detailType.text = def.CategoryLabel;
        detailSize.text = def.sizeLabel;
        detailWeight.text = $"{def.weightKg.ToString("N0", Fr)} kg";
        detailResistance.text = $"{def.resistanceHp.ToString("N0", Fr)} HP";
        detailCost.text = $"{def.costCpu} CPU";

        BlockSelected?.Invoke(def);
    }

    public void RefreshCapacity()
    {
        capacityText.text = $"CAPACITÉ UTILISÉE : <color=#3B82F6>{capacityUsed.ToString("N0", Fr)} / " +
                            $"{capacityMax.ToString("N0", Fr)} CPU</color>";
    }
}
