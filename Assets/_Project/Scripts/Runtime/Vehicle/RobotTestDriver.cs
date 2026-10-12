using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// =========================================================
// PILOTAGE DU ROBOT DE TEST (entrees -> RobotLocomotion)
// Z / S : avancer / reculer, Q / D : tourner, Shift : boost,
// Espace / Ctrl : monter / descendre (rotors, propulseurs
// orientes vers le haut), R : remettre le robot sur ses roues.
// Touches physiques W/S/A/D (Z/S/Q/D en AZERTY) ; les fleches
// marchent aussi.
// ECHAP libere le curseur ; un second ECHAP (curseur libre)
// revient au garage (RobotSession.ReturnSceneName) ; un clic
// gauche reverrouille le curseur.
// La physique (suspensions, adherence, portance...) est dans
// RobotLocomotion, les animations dans RobotPartAnimator.
// =========================================================

[RequireComponent(typeof(RobotLocomotion))]
public class RobotTestDriver : MonoBehaviour
{
    [Header("Navigation")]
    [Tooltip("Scene garage rechargee si RobotSession.ReturnSceneName est vide (scene lancee directement)")]
    public string fallbackReturnScene = "Garage";
    public bool lockCursorOnStart = true;

    private RobotLocomotion loco;

    private void Awake()
    {
        loco = GetComponent<RobotLocomotion>();
    }

    private void Start()
    {
        if (lockCursorOnStart)
            SetCursorLocked(true);
    }

    private void Update()
    {
        float throttle = 0f, steer = 0f, lift = 0f;
        bool boost = false;

        var kb = Keyboard.current;
        if (kb == null)
        {
            Send(throttle, steer, lift, boost);
            return;
        }

        if (kb.escapeKey.wasPressedThisFrame)
        {
            if (Cursor.lockState == CursorLockMode.Locked)
            {
                SetCursorLocked(false);
            }
            else
            {
                ReturnToGarage();
                return;
            }
        }

        var mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
            SetCursorLocked(true);

        // AZERTY physique : wKey = Z, aKey = Q
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed) throttle += 1f;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed) throttle -= 1f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) steer += 1f;
        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) steer -= 1f;
        boost = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
        if (kb.spaceKey.isPressed) lift += 1f;
        if (kb.leftCtrlKey.isPressed || kb.rightCtrlKey.isPressed) lift -= 1f;
        if (kb.rKey.wasPressedThisFrame)
            loco.Recover();

        // Curseur libre (menu, retour) : le robot ne recoit plus d'ordres
        if (Cursor.lockState != CursorLockMode.Locked)
            throttle = steer = lift = 0f;
        Send(throttle, steer, lift, boost);
    }

    private void Send(float throttle, float steer, float lift, bool boost)
    {
        loco.throttle = throttle;
        loco.steer = steer;
        loco.lift = lift;
        loco.boost = boost;
    }

    public void ReturnToGarage()
    {
        string scene = string.IsNullOrEmpty(RobotSession.ReturnSceneName) ? fallbackReturnScene : RobotSession.ReturnSceneName;
        SetCursorLocked(false);
        SceneManager.LoadScene(scene);
    }

    private static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
            SetCursorLocked(false);
    }
}
