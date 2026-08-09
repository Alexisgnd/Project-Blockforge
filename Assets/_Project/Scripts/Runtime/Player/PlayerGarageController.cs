using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerGarageController : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Transform capsule;

    [Header("Movement")]
    [SerializeField] private float horizontalSpeed = 5f;
    [SerializeField] private float verticalSpeed = 4f;

    [Header("Movement Smoothing")]
    [SerializeField] private float acceleration = 8f;
    [SerializeField] private float deceleration = 10f;

    [Header("Boost")]
    [SerializeField] private float boostMultiplier = 3f;

    [Header("Mouse Look")]
    [SerializeField] private float mouseSensitivity = 0.12f;
    [SerializeField] private float maxLookUp = 80f;
    [SerializeField] private float maxLookDown = 80f;

    [Header("Cursor")]
    [SerializeField] private bool lockCursorOnStart = true;


    // =========================================================
    // DONNEES ACCESSIBLES PAR LA PINCE
    // =========================================================

    public Vector3 MoveInput { get; private set; }

    public Vector3 CurrentVelocity { get; private set; }

    public Vector2 LookDelta { get; private set; }

    public bool IsBoosting { get; private set; }


    public float CurrentMovementMultiplier =>
        IsBoosting ? boostMultiplier : 1f;


    public float CurrentEffectMultiplier =>
        IsBoosting ? boostMultiplier : 1f;


    public bool IsMoving =>
        CurrentVelocity.sqrMagnitude > 0.001f;


    public bool IsMovingHorizontally =>
        Mathf.Abs(MoveInput.x) > 0.01f ||
        Mathf.Abs(MoveInput.z) > 0.01f;


    private float cameraPitch;


    private void Start()
    {
        if (capsule == null)
        {
            capsule = transform.Find("Capsule");
        }

        if (lockCursorOnStart)
        {
            LockCursor();
        }
    }


    private void Update()
    {
        ReadInput();
        HandleMouseLook();
        HandleCursor();
        MovePlayer();
    }


    // =========================================================
    // INPUT
    // =========================================================

    private void ReadInput()
    {
        if (Keyboard.current == null)
        {
            MoveInput = Vector3.zero;
            IsBoosting = false;
            return;
        }


        float horizontal = 0f;
        float vertical = 0f;
        float forward = 0f;


        // =====================================================
        // AZERTY PHYSIQUE FORCE
        //
        // wKey = touche physique Z
        // aKey = touche physique Q
        // =====================================================

        // Z = avancer
        if (Keyboard.current.wKey.isPressed)
            forward += 1f;

        // S = reculer
        if (Keyboard.current.sKey.isPressed)
            forward -= 1f;

        // Q = gauche
        if (Keyboard.current.aKey.isPressed)
            horizontal -= 1f;

        // D = droite
        if (Keyboard.current.dKey.isPressed)
            horizontal += 1f;


        // Espace = monter
        if (Keyboard.current.spaceKey.isPressed)
            vertical += 1f;


        // Ctrl = descendre
        if (
            Keyboard.current.leftCtrlKey.isPressed ||
            Keyboard.current.rightCtrlKey.isPressed
        )
        {
            vertical -= 1f;
        }


        // Shift = boost x3
        IsBoosting =
            Keyboard.current.leftShiftKey.isPressed ||
            Keyboard.current.rightShiftKey.isPressed;


        MoveInput = new Vector3(
            horizontal,
            vertical,
            forward
        );


        if (MoveInput.sqrMagnitude > 1f)
        {
            MoveInput = MoveInput.normalized;
        }
    }


    // =========================================================
    // MOVEMENT
    // =========================================================

    private void MovePlayer()
    {
        float multiplier =
            CurrentMovementMultiplier;


        // Tes axes visuels sont inversés,
        // donc on conserve les inversions établies précédemment.

        Vector3 forwardDirection =
            -transform.forward * MoveInput.z;


        Vector3 horizontalDirection =
            -transform.right * MoveInput.x;


        Vector3 verticalDirection =
            Vector3.up * MoveInput.y;


        Vector3 targetVelocity =
            (
                forwardDirection * horizontalSpeed +
                horizontalDirection * horizontalSpeed +
                verticalDirection * verticalSpeed
            )
            * multiplier;


        float smoothingSpeed =
            MoveInput.sqrMagnitude > 0.01f
                ? acceleration
                : deceleration;


        CurrentVelocity = Vector3.Lerp(
            CurrentVelocity,
            targetVelocity,
            smoothingSpeed * Time.deltaTime
        );


        transform.position +=
            CurrentVelocity * Time.deltaTime;
    }


    // =========================================================
    // MOUSE LOOK
    // =========================================================

    private void HandleMouseLook()
    {
        LookDelta = Vector2.zero;


        if (Mouse.current == null)
            return;


        if (Cursor.lockState != CursorLockMode.Locked)
            return;


        LookDelta =
            Mouse.current.delta.ReadValue();


        float mouseX =
            LookDelta.x * mouseSensitivity;


        float mouseY =
            LookDelta.y * mouseSensitivity;


        // =====================================================
        // GAUCHE / DROITE
        // =====================================================

        transform.Rotate(
            0f,
            mouseX,
            0f,
            Space.World
        );


        // =====================================================
        // HAUT / BAS
        //
        // IMPORTANT :
        // souris vers le haut = pitch négatif
        // =====================================================

        cameraPitch += mouseY;


        cameraPitch = Mathf.Clamp(
            cameraPitch,
            -maxLookUp,
            maxLookDown
        );


        if (capsule != null)
        {
            capsule.localRotation =
                Quaternion.Euler(
                    cameraPitch,
                    0f,
                    0f
                );
        }
    }


    // =========================================================
    // CURSOR
    // =========================================================

    private void HandleCursor()
    {
        if (Keyboard.current != null &&
            Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            UnlockCursor();
        }


        if (
            Mouse.current != null &&
            Mouse.current.leftButton.wasPressedThisFrame &&
            Cursor.lockState != CursorLockMode.Locked
        )
        {
            LockCursor();
        }
    }


    private void LockCursor()
    {
        Cursor.lockState =
            CursorLockMode.Locked;

        Cursor.visible = false;
    }


    private void UnlockCursor()
    {
        Cursor.lockState =
            CursorLockMode.None;

        Cursor.visible = true;

        LookDelta = Vector2.zero;
    }


    private void OnApplicationFocus(bool hasFocus)
    {
        if (!hasFocus)
        {
            UnlockCursor();
        }
    }
}