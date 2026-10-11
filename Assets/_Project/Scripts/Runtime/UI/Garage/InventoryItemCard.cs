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
    [Tooltip("Barre de grade de 3 px a gauche (grade-raw par defaut)")]
    public Image gradeBar;
    public Image iconImage;
    public TMP_Text nameText;
    public TMP_Text costText;

    public BlockDefinition Definition { get; private set; }

    public void Bind(BlockDefinition def, Action<BlockDefinition> onSelect)
    {
        Definition = def;
        nameText.text = def.blockName;
        costText.text = BlockforgeTheme.Qty(def.costCpu, "CPU");
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

    // PartCard : surface-selected + border-accent (porte par DSElement)
    public void SetSelected(bool selected)
    {
        if (TryGetComponent<DSElement>(out var element))
        {
            element.Selected = selected;
            return;
        }
        background.color = selected ? BlockforgeTheme.SurfaceSelected : BlockforgeTheme.SurfacePanel;
        if (outline != null)
            outline.enabled = selected;
    }
}
