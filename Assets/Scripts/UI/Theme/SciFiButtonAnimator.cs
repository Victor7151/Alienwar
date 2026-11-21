using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using Starfall.Audio;

namespace Starfall.UI.Theme
{
    /// <summary>
    /// Adds AAA tactile micro-interactions to any UI Button:
    /// - Smooth hover spring-scale (1.04x) with audio feedback.
    /// - Snappy press compression (0.95x).
    /// - Optional pulsing idle glow on prominent call-to-action buttons.
    /// </summary>
    [RequireComponent(typeof(Button))]
    public class SciFiButtonAnimator : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        [Header("Animation Settings")]
        [SerializeField] private float hoverScale = 1.04f;
        [SerializeField] private float pressScale = 0.95f;
        [SerializeField] private float scaleSpeed = 16f;
        [SerializeField] private bool pulseIdle = false;

        private Vector3 initialScale;
        private Vector3 targetScale;
        private bool isHovered = false;
        private bool isPressed = false;
        private Button button;

        public bool PulseIdle
        {
            get => pulseIdle;
            set => pulseIdle = value;
        }

        private void Awake()
        {
            button = GetComponent<Button>();
            initialScale = transform.localScale;
            targetScale = initialScale;
        }

        private void OnEnable()
        {
            targetScale = initialScale;
            transform.localScale = initialScale;
            isHovered = false;
            isPressed = false;
        }

        private void Update()
        {
            // Smoothly lerp towards target scale
            float dt = Time.unscaledDeltaTime;
            transform.localScale = Vector3.Lerp(transform.localScale, targetScale, dt * scaleSpeed);

            // Optional breathing pulse when idle on prominent action buttons
            if (pulseIdle && !isHovered && !isPressed && button != null && button.interactable)
            {
                float pulse = 1.0f + Mathf.Sin(Time.unscaledTime * 3.5f) * 0.025f;
                transform.localScale = initialScale * pulse;
            }
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (button != null && !button.interactable) return;
            isHovered = true;
            if (!isPressed)
            {
                targetScale = initialScale * hoverScale;
            }
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            isHovered = false;
            if (!isPressed)
            {
                targetScale = initialScale;
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (button != null && !button.interactable) return;
            isPressed = true;
            targetScale = initialScale * pressScale;
            AudioManager.Instance?.PlayLaserShoot(1.25f);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            isPressed = false;
            targetScale = isHovered ? (initialScale * hoverScale) : initialScale;
        }
    }
}
