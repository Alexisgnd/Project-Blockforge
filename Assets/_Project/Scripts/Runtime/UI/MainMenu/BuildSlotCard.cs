using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// =========================================================
// CARTE D'UN SLOT DE CONSTRUCTION
// Deux etats : robot existant (apercu, stats, MODIFIER /
// SUPPRIMER) ou slot vide (+ CREER). L'apercu est une texture
// rendue par RobotThumbnailRenderer, affichee dans le puits
// "Cover" par une RawImage creee a la volee.
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
    // Taille du puits d'apercu en pixels d'ecran (pour dimensionner la texture)
    public Vector2 PreviewPixelSize
    {
        get
        {
            if (coverImage == null)
                return Vector2.zero;
            var rect = coverImage.rectTransform.rect;
            var canvas = coverImage.canvas;
            float scale = canvas != null ? canvas.scaleFactor : 1f;
            return rect.size * scale;
        }
    }

    // Affiche (ou retire, avec null) l'apercu 3D du robot dans le puits
    public void SetPreview(Texture texture)
    {
        if (coverImage == null)
            return;
        if (preview == null)
        {
            var go = new GameObject("Preview", typeof(RectTransform), typeof(RawImage));
            go.layer = coverImage.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(coverImage.transform, false);
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.one;       // 1 px : le bord du puits reste visible
            rect.offsetMax = -Vector2.one;
            preview = go.GetComponent<RawImage>();
            preview.raycastTarget = false;
        }
        preview.texture = texture;
        preview.enabled = texture != null;
    }

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

    private RawImage preview;
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
        // Energie pas encore calculee : ligne masquee (la carte garde sa taille)
        StatRowHider.HideRowOf(energyValue, false);

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
