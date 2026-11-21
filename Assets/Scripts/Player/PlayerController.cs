using UnityEngine;
using UnityEngine.InputSystem;

namespace Starfall.Player
{
    /// <summary>
    /// Player ship controller providing dedicated controller movement (analog trackpad/virtual joystick,
    /// physical gamepad, and keyboard fallback for editor testing). Clamps ship to screen viewport.
    /// Screen drag/touch on the playfield is completely disabled as movement is controller-only.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Movement Settings")]
        [SerializeField] private float moveSpeed = 13.5f;
        [SerializeField] private float keyboardSpeed = 12f;
        [SerializeField] private Vector2 padding = new Vector2(0.6f, 0.6f);
        [SerializeField] private float tiltAngle = 16f;
        [SerializeField] private float tiltSpeed = 12f;

        private Camera mainCamera;
        private Vector2 minBounds;
        private Vector2 maxBounds;
        private Vector3 targetPosition;
        private Vector2 currentInput;
        private float currentTilt = 0f;

        public static PlayerController Instance { get; private set; }

        public bool IsMoving => currentInput.sqrMagnitude > 0.01f;
        public Vector2 CurrentInput => currentInput;

        private void Awake()
        {
            Instance = this;
            mainCamera = Camera.main;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            float effective = Starfall.Core.ShipUpgradesManager.GetEffectiveSpeed();
            if (effective > 0f)
            {
                moveSpeed = effective;
            }
            else if (keyboardSpeed > 0f)
            {
                moveSpeed = keyboardSpeed;
            }

            targetPosition = transform.position;
            UpdateScreenBounds();
            StartCoroutine(ArrivalFlyInRoutine());
        }

        private bool isArriving = false;

        private System.Collections.IEnumerator ArrivalFlyInRoutine()
        {
            isArriving = true;
            Vector3 finalPos = transform.position;
            Vector3 spawnPos = new Vector3(finalPos.x, finalPos.y - 3.2f, finalPos.z);
            transform.position = spawnPos;

            float duration = 0.75f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                transform.position = Vector3.Lerp(spawnPos, finalPos, t);
                targetPosition = transform.position;
                yield return null;
            }

            transform.position = finalPos;
            targetPosition = finalPos;
            isArriving = false;
        }

        public void ApplySpeedBoost(float multiplier, float duration)
        {
            StartCoroutine(SpeedBoostRoutine(multiplier, duration));
        }

        private System.Collections.IEnumerator SpeedBoostRoutine(float multiplier, float duration)
        {
            float origSpeed = moveSpeed;
            moveSpeed *= multiplier;
            yield return new WaitForSeconds(duration);
            moveSpeed = origSpeed;
        }

        public void UpdateScreenBounds()
        {
            if (mainCamera == null) mainCamera = Camera.main;
            if (mainCamera == null) return;

            float camDistance = Mathf.Abs(mainCamera.transform.position.z - transform.position.z);
            Vector3 bottomLeft = mainCamera.ViewportToWorldPoint(new Vector3(0, 0, camDistance));
            Vector3 topRight = mainCamera.ViewportToWorldPoint(new Vector3(1, 1, camDistance));

            minBounds = new Vector2(bottomLeft.x + padding.x, bottomLeft.y + padding.y);
            maxBounds = new Vector2(topRight.x - padding.x, topRight.y - padding.y);

            // Stay inside the combat lane derived from camera width:
            float lane = Starfall.Core.PlayfieldBounds.PlayHalfWidth - 0.1f;
            minBounds.x = Mathf.Max(minBounds.x, -lane);
            maxBounds.x = Mathf.Min(maxBounds.x, lane);

            // Allow movement across the entire playfield, up to just below the HUD margin at the top
            float topLimit = Starfall.Core.PlayfieldBounds.Top - Starfall.Core.PlayfieldBounds.HudTopMargin - 0.15f;
            maxBounds.y = Mathf.Min(topRight.y - padding.y, topLimit);
        }

        private void Update()
        {
            using var __perf = Starfall.Core.PerfMarkers.PlayerUpdate.Auto();
            if (Time.frameCount % 30 == 0) UpdateScreenBounds();
            if (isArriving) return;
            HandleInput();
            ClampAndApplyMovement();
            ApplyBankingTilt();
        }

        public float EffectiveSpeed => moveSpeed * (Starfall.Combat.TacticalPerkManager.Instance != null ? Starfall.Combat.TacticalPerkManager.Instance.MoveSpeedMultiplier : 1f);

        private void HandleInput()
        {
            Vector2 input = Vector2.zero;

            // 1. Tactical Flight Trackpad / Virtual Joystick (Primary Mobile Controller)
            if (Starfall.UI.TacticalFlightTrackpad.Instance != null && Starfall.UI.TacticalFlightTrackpad.Instance.IsActive)
            {
                input = Starfall.UI.TacticalFlightTrackpad.Instance.InputDirection;
            }
            else if (Starfall.UI.VirtualJoystick.Instance != null && Starfall.UI.VirtualJoystick.Instance.InputDirection.sqrMagnitude > 0.005f)
            {
                input = Starfall.UI.VirtualJoystick.Instance.InputDirection;
            }

            // 2. Physical Gamepad (left stick and d-pad)
            if (input.sqrMagnitude <= 0.005f && Gamepad.current != null)
            {
                Vector2 stick = Gamepad.current.leftStick.ReadValue();
                if (stick.sqrMagnitude > 0.01f)
                {
                    input = stick;
                }
                else
                {
                    Vector2 dpad = Gamepad.current.dpad.ReadValue();
                    if (dpad.sqrMagnitude > 0.01f) input = dpad;
                }
            }

            // 3. Keyboard (WASD / Arrow Keys) for Editor testing
            if (input.sqrMagnitude <= 0.005f && Keyboard.current != null)
            {
                if (Keyboard.current.wKey.isPressed || Keyboard.current.upArrowKey.isPressed) input.y += 1f;
                if (Keyboard.current.sKey.isPressed || Keyboard.current.downArrowKey.isPressed) input.y -= 1f;
                if (Keyboard.current.aKey.isPressed || Keyboard.current.leftArrowKey.isPressed) input.x -= 1f;
                if (Keyboard.current.dKey.isPressed || Keyboard.current.rightArrowKey.isPressed) input.x += 1f;
            }

            currentInput = input;
        }

        private void ClampAndApplyMovement()
        {
            if (currentInput.sqrMagnitude > 1f)
            {
                currentInput.Normalize();
            }

            if (currentInput.sqrMagnitude > 0.001f)
            {
                targetPosition += (Vector3)(currentInput * (EffectiveSpeed * Time.deltaTime));
            }

            targetPosition.x = Mathf.Clamp(targetPosition.x, minBounds.x, maxBounds.x);
            targetPosition.y = Mathf.Clamp(targetPosition.y, minBounds.y, maxBounds.y);
            targetPosition.z = 0f;

            // Direct, high-precision movement tracking
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, EffectiveSpeed * Time.deltaTime * 1.5f);
        }

        private void ApplyBankingTilt()
        {
            float horizontal = currentInput.x;
            float desiredTilt = Mathf.Clamp(-horizontal * tiltAngle, -tiltAngle, tiltAngle);
            currentTilt = Mathf.Lerp(currentTilt, desiredTilt, tiltSpeed * Time.deltaTime);
            transform.rotation = Quaternion.Euler(0f, 0f, currentTilt);
        }
    }
}
