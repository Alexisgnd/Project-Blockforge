using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// =========================================================
// BARRE D'OUTILS DU GARAGE
// 1 Pince constructeur / 2 Arroseur couleur.
// (Supprimer/Pivoter/Miroir sont des touches, pas des outils.)
// Notifie le gameplay via ToolChanged.
// =========================================================

public class GarageToolbar : MonoBehaviour
{
    private static readonly Key[] DigitKeys = { Key.Digit1, Key.Digit2, Key.Digit3, Key.Digit4, Key.Digit5 };

    [Header("References (remplies par le setup)")]
    public Button[] buttons = new Button[2];
    public Image[] backgrounds = new Image[2];
    public Outline[] outlines = new Outline[2];

    [Header("Couleurs")]
    public Color normalBg = new(0.06f, 0.10f, 0.16f, 0.95f);
    public Color selectedBg = new(0.09f, 0.16f, 0.30f, 1f);

    public event Action<int> ToolChanged;
    public int SelectedIndex { get; private set; } = -1;

    private void Awake()
    {
        for (int i = 0; i < buttons.Length; i++)
        {
            int index = i;
            if (buttons[i] != null)
                buttons[i].onClick.AddListener(() => Select(index));
        }
    }

    private void Start()
    {
        Select(0);
    }

    private void Update()
    {
        var kb = Keyboard.current;
        if (kb == null)
            return;

        for (int i = 0; i < buttons.Length && i < DigitKeys.Length; i++)
        {
            if (kb[DigitKeys[i]].wasPressedThisFrame)
            {
                Select(i);
                break;
            }
        }
    }

    public void Select(int index)
    {
        if (index < 0 || index >= buttons.Length || index == SelectedIndex)
            return;

        SelectedIndex = index;

        for (int i = 0; i < buttons.Length; i++)
        {
            bool selected = i == index;
            if (backgrounds[i] != null)
                backgrounds[i].color = selected ? selectedBg : normalBg;
            if (outlines[i] != null)
                outlines[i].enabled = selected;
        }

        ToolChanged?.Invoke(index);
    }
}
