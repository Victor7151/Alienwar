using UnityEngine;

namespace Starfall.UI
{
    /// <summary>
    /// Lightweight shim: PlayerController reads VirtualJoystick.Instance.InputDirection.
    /// The actual joystick touch logic now lives in MobileControlsManager / JoystickTouchHandler.
    /// </summary>
    public class VirtualJoystick : MonoBehaviour
    {
        public static VirtualJoystick Instance { get; private set; }

        /// <summary>Current normalised stick direction, forwarded from TacticalFlightTrackpad or MobileControlsManager.</summary>
        public Vector2 InputDirection =>
            TacticalFlightTrackpad.Instance != null && TacticalFlightTrackpad.Instance.IsActive
                ? TacticalFlightTrackpad.Instance.InputDirection
                : (MobileControlsManager.Instance != null
                    ? MobileControlsManager.Instance.JoystickInput
                    : Vector2.zero);

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }
    }
}
