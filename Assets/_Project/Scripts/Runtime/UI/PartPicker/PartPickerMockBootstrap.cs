using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;

// =========================================================
// BANC D'ESSAI DU PART PICKER (scene vide)
// Pose sur un objet d'une scene vide (PartPickerPreview) : cree ce
// qu'il manque (EventSystem, camera, lumiere), le picker, et le
// relie aux donnees simulees (PickerMockCatalog). Q ouvre ; poser
// une piece la compte dans le budget simule ; les evenements
// (outil, pose, epingle, Store) sont journalises.
// =========================================================

public class PartPickerMockBootstrap : MonoBehaviour
{
    [Tooltip("Ouvre le picker au lancement (sinon : Q)")]
    public bool openOnStart = true;

    private PartPickerController picker;
    private MockRobotBudget budget;

    private void Awake()
    {
        if (FindAnyObjectByType<EventSystem>() == null)
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        if (Camera.main == null)
        {
            var cam = new GameObject("Main Camera", typeof(Camera)) { tag = "MainCamera" };
            cam.transform.SetPositionAndRotation(new Vector3(0f, 2f, -6f), Quaternion.Euler(12f, 0f, 0f));
        }
        if (FindAnyObjectByType<Light>() == null)
        {
            var sun = new GameObject("Sun", typeof(Light));
            sun.GetComponent<Light>().type = LightType.Directional;
            sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
        }

        picker = GetComponent<PartPickerController>();
        if (picker == null)
            picker = gameObject.AddComponent<PartPickerController>();
    }

    private void Start()
    {
        var catalog = PickerMockCatalog.Load();
        budget = new MockRobotBudget(catalog);
        picker.Bind(new MockInventory(catalog.owned.Select(m => m.part), catalog.notOwnedCount), budget);
        picker.Selected += p => Debug.Log($"[PartPicker] Outil de pose : {p.name}");
        picker.PlaceRequested += (p, keepOpen) =>
        {
            budget.Add(p.cpuPool, p.cpuCost);
            Debug.Log($"[PartPicker] Pose : {p.name} ({p.cpuCost} CPU {p.cpuPool}){(keepOpen ? ", fenetre gardee ouverte" : "")}");
        };
        picker.PinRequested += p => Debug.Log($"[PartPicker] Epingle a la barre rapide : {p.name}");
        picker.StoreRequested += q => Debug.Log(q == null ? "[PartPicker] Ouvre le Store" : $"[PartPicker] Recherche dans le Store : {q}");
        if (openOnStart)
            picker.Open();
    }
}
