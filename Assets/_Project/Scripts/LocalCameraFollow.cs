using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

/// <summary>
/// Third-person orbit camera for the local player's capsule.
/// Mouse / right stick rotates around the player; movement input is made
/// relative to <see cref="Yaw"/> by NetworkBootstrap.
/// Click the game world to lock the cursor, Esc to release it. Clicks on UI don't lock.
/// </summary>
public class LocalCameraFollow : MonoBehaviour
{
    public static Transform Target;

    /// <summary>Current camera yaw in degrees (world space), used to orient movement input.</summary>
    public static float Yaw { get; private set; }

    [Header("Orbit")]
    [SerializeField] private float distance = 6f;
    [SerializeField] private float pivotHeight = 1.5f;
    [SerializeField] private float minPitch = -30f;
    [SerializeField] private float maxPitch = 70f;

    [Header("Sensitivity")]
    [SerializeField] private float mouseSensitivity = 0.15f;   // degrees per pixel
    [SerializeField] private float stickSensitivity = 180f;    // degrees per second
    [SerializeField] private bool invertY;

    [Header("Collision")]
    [SerializeField] private float collisionRadius = 0.25f;
    [SerializeField] private LayerMask collisionMask = ~0;

    private float _yaw;
    private float _pitch = 20f;
    private InputAction _lookAction;

    private void Start()
    {
        _lookAction = InputSystem.actions != null ? InputSystem.actions.FindAction("Player/Look") : null;
        _yaw = transform.eulerAngles.y;
        Yaw = _yaw;
    }

    private void Update()
    {
        // Cursor lock: click the game world to capture, Esc to release.
        // Clicks on UI (e.g. the Fusion Menu gear / in-game panel) leave the cursor free.
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame && !IsPointerOverUI())
            SetCursorLocked(true);
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
            SetCursorLocked(false);

        ReadLook();
    }

    private void ReadLook()
    {
        Vector2 delta = Vector2.zero;
        bool cursorLocked = Cursor.lockState == CursorLockMode.Locked;

        if (_lookAction != null)
        {
            var raw = _lookAction.ReadValue<Vector2>();
            bool fromGamepad = _lookAction.activeControl?.device is Gamepad;

            if (fromGamepad)
                delta = raw * (stickSensitivity * Time.deltaTime);
            else if (cursorLocked)
                delta = raw * mouseSensitivity;
        }
        else if (cursorLocked && Mouse.current != null)
        {
            delta = Mouse.current.delta.ReadValue() * mouseSensitivity;
        }

        _yaw += delta.x;
        _pitch += invertY ? delta.y : -delta.y;
        _pitch = Mathf.Clamp(_pitch, minPitch, maxPitch);
        Yaw = _yaw;
    }

    private void LateUpdate()
    {
        if (Target == null) return;

        var rotation = Quaternion.Euler(_pitch, _yaw, 0f);
        var pivot = Target.position + Vector3.up * pivotHeight;
        var back = rotation * Vector3.back;

        // Pull the camera in if something is between it and the player.
        float dist = distance;
        if (Physics.SphereCast(pivot, collisionRadius, back, out var hit, distance, collisionMask, QueryTriggerInteraction.Ignore)
            && hit.transform != Target)
        {
            dist = Mathf.Max(0.5f, hit.distance);
        }

        transform.SetPositionAndRotation(pivot + back * dist, rotation);
    }

    private static bool IsPointerOverUI()
    {
        // Only meaningful while the cursor is free; a locked cursor never "hovers" UI.
        return Cursor.lockState != CursorLockMode.Locked
            && EventSystem.current != null
            && EventSystem.current.IsPointerOverGameObject();
    }

    private static void SetCursorLocked(bool locked)
    {
        Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
        Cursor.visible = !locked;
    }

    private void OnDisable() => SetCursorLocked(false);
}
