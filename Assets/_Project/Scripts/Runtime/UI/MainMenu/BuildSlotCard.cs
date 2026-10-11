using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// =========================================================
// CARTE D'UN SLOT DE CONSTRUCTION
// Deux etats : robot existant (stats + MODIFIER / SUPPRIMER)
// ou slot vide (+ CREER).
// =========================================================

// Donnees d'affichage d'un slot (remplies depuis PocketBase)
public struct BuildSlotData
{
    public bool occupied;
    public string robotName;
    public string tierLabel;
    public int health;
    public int speedKmh;
    public int energy;
    public float massT;
}

public class BuildSlotCard : MonoBehaviour
{
    [Header("Commun (rempli par le setup)")]
    public TMP_Text numberText;
    public TMP_Text titleText;

    [Header("Etat robot existant")]
    public GameObject filledGroup;
    public TMP_Text tierText;
    public Image coverImage;
    public TMP_Text healthValue;
    public TMP_Text speedValue;
    public TMP_Text energyValue;
    public TMP_Text massValue;
    public Button modifyButton;
    public Button deleteButton;

    [Header("Etat slot vide")]
    public GameObject emptyGroup;
    public Button createButton;

    private int slotIndex;
    private Action<int> onModify;
    private Action<int> onCreate;
    private Action<int> onDelete;

    public void Bind(int index, BuildSlotData data, Action<int> modifyCallback, Action<int> createCallback,
                     Action<int> deleteCallback = null)
    {
        slotIndex = index;
        onModify = modifyCallback;
        onCreate = createCallback;
        onDelete = deleteCallback;

        numberText.text = $"Bay {index + 1:00}";

        filledGroup.SetActive(data.occupied);
        emptyGroup.SetActive(!data.occupied);

        if (data.occupied)
        {
            titleText.text = data.robotName;
            tierText.text = data.tierLabel;
            healthValue.text = BlockforgeTheme.QtyMuted(BlockforgeTheme.Num(data.health), "HP");
            speedValue.text = BlockforgeTheme.QtyMuted(BlockforgeTheme.Num(data.speedKmh), "km/h");
            energyValue.text = BlockforgeTheme.Num(data.energy);
            massValue.text = BlockforgeTheme.QtyMuted(BlockforgeTheme.Num(data.massT), "t");
        }
        else
        {
            titleText.text = "Free bay";
        }

        modifyButton.onClick.RemoveAllListeners();
        modifyButton.onClick.AddListener(() => onModify?.Invoke(slotIndex));
        createButton.onClick.RemoveAllListeners();
        createButton.onClick.AddListener(() => onCreate?.Invoke(slotIndex));

        if (deleteButton != null)
        {
            deleteButton.gameObject.SetActive(data.occupied && onDelete != null);
            deleteButton.onClick.RemoveAllListeners();
            deleteButton.onClick.AddListener(() => onDelete?.Invoke(slotIndex));
        }
    }
}
