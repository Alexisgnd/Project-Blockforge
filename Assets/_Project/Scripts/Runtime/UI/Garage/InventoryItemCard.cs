using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// =========================================================
// CARTE D'UN BLOC DANS L'INVENTAIRE DU GARAGE
// =========================================================

public class InventoryItemCard : MonoBehaviour
{
    [Header("References (remplies par le setup)")]
    public Button button;
    public Image background;
    public Outline outline;
    public Image iconImage;
    public TMP_Text nameText;
    public TMP_Text costText;

    [Header("Couleurs")]
    public Color normalBg = new(0.06f, 0.10f, 0.16f, 0.95f);
    public Color selectedBg = new(0.09f, 0.16f, 0.30f, 1f);

    public BlockDefinition Definition { get; private set; }

    public void Bind(BlockDefinition def, Action<BlockDefinition> onSelect)
    {
        Definition = def;
        nameText.text = def.blockName;
        costText.text = $"{def.costCpu} CPU";
        if (def.icon != null)
        {
            // Blanc : la couleur de fond de la case teinterait le sprite
            iconImage.sprite = def.icon;
            iconImage.color = Color.white;
            iconImage.preserveAspect = true;
        }

        button.onClick.RemoveAllListeners();
        button.onClick.AddListener(() => onSelect?.Invoke(Definition));
    }

    public void SetSelected(bool selected)
    {
        background.color = selected ? selectedBg : normalBg;
        outline.enabled = selected;
    }
}
