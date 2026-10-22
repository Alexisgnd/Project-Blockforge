using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

// =========================================================
// PILOTAGE DU ROBOT DE TEST (entrees -> RobotLocomotion)
// Z / S : avancer / reculer, Q / D : tourner, Shift : boost,
// Espace / Ctrl : monter / descendre (rotors, propulseurs
// orientes vers le haut) ; Espace fait aussi sauter un robot a
// pattes d'insecte, R : redresser le robot (RobotRecovery :
// levitation, remise a plat, chute ; recharge de quelques
// secondes), clic gauche : tirer (WeaponFireController).
// Touches physiques W/S/A/D (Z/S/Q/D en AZERTY) ; les fleches
// marchent aussi.
// ECHAP ouvre le menu de pause (MatchPauseMenu : reprendre,
// options, retour au garage, rappel des touches) ; un clic
// gauche reverrouille le curseur s'il est libre.
// MatchGate.Frozen (compte a rebours du match contre les bots,
// fin de match) : le robot ne recoit plus d'ordres.
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
    private RobotRecovery recovery;

    private void Awake()
    {
        loco = GetComponent<RobotLocomotion>();
        recovery = GetComponent<RobotRecovery>();
    }

    private void Start()
    {
        recovery = GetComponent<RobotRecovery>();
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

        // Fin de match en cours : plus de pilotage ni de pause (Echap ne ramene pas a l'arene)
        if (MatchEndFlow.IsRunning)
        {
            Send(0f, 0f, 0f, false);
            return;
        }

        // ECHAP : menu de pause (reprendre, options, retour au garage)
        // Match termine (TdmMatch), en attente du verdict : plus de pause non plus
        if (TdmMatch.Instance != null && TdmMatch.Instance.Phase == TdmPhase.Ended)
        {
            Send(0f, 0f, 0f, false);
            return;
        }
        if (kb.escapeKey.wasPressedThisFrame && !MatchPauseMenu.IsOpen)
        {
            MatchPauseMenu.Open(this);
            Send(0f, 0f, 0f, false);
            return;
        }
        if (MatchPauseMenu.IsOpen)
        {
            Send(0f, 0f, 0f, false);
            return;
        }

        // Compte a rebours avant le match (TdmMatch) : le robot attend, la camera reste libre
        if (MatchGate.Frozen)
        {
            Send(0f, 0f, 0f, false);
            var m = Mouse.current;
            if (m != null && m.leftButton.wasPressedThisFrame && Cursor.lockState != CursorLockMode.Locked)
                SetCursorLocked(true);
            return;
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
        {
            // Redressement facon Robocraft (levitation + remise a plat) ; repli : teleportation
            if (recovery == null || !recovery.Begin())
            {
                if (recovery == null)
                    loco.Recover();
            }
        }

        // Curseur libre (menu, retour) : le robot ne recoit plus d'ordres
        if (Cursor.lockState != CursorLockMode.Locked)
            throttle = steer = lift = 0f;
        // Espace : saut des robots a pattes d'insecte (au sol, recharge courte)
        else if (kb.spaceKey.wasPressedThisFrame)
            loco.RequestJump();
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
        Time.timeScale = 1f;
        SetCursorLocked(false);
        SceneLoader.Load(scene);
    }

    public static void SetCursorLocked(bool locked)
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
