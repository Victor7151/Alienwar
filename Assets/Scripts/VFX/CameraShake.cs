using UnityEngine;

namespace Starfall.VFX
{
    /// <summary>
    /// Trauma-based 2D camera shake. Overlapping requests keep the strongest one instead of
    /// a small hit cancelling a big explosion mid-shake. Uses smooth noise rather than
    /// per-frame random jumps, which reads better and is kinder on mobile screens.
    /// Respects the user's Screen Shake toggle in System Settings.
    /// </summary>
    public class CameraShake : MonoBehaviour
    {
        public static CameraShake Instance { get; private set; }

        [SerializeField] private float maxOffset = 0.6f;
        [SerializeField] private float maxRoll = 1.6f;

        private Vector3 initialPosition;
        private Quaternion initialRotation;
        private float magnitude;
        private float remaining;
        private float totalDuration;
        private float seed;

        public static bool IsShakeEnabled
        {
            get => PlayerPrefs.GetInt("Starfall_ScreenShake", 1) == 1;
            set
            {
                PlayerPrefs.SetInt("Starfall_ScreenShake", value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;
            initialPosition = transform.localPosition;
            initialRotation = transform.localRotation;
            seed = Random.value * 100f;
        }

        public void Shake(float duration = 0.4f, float strength = 0.35f)
        {
            if (!IsShakeEnabled) return;

            // Keep whichever shake is currently stronger; extend the duration if needed.
            float currentStrength = totalDuration > 0f ? magnitude * (remaining / totalDuration) : 0f;
            if (strength >= currentStrength)
            {
                magnitude = strength;
                remaining = Mathf.Max(duration, remaining);
                totalDuration = remaining;
            }
            else
            {
                remaining = Mathf.Max(remaining, duration);
            }
        }

        private void LateUpdate()
        {
            if (remaining <= 0f) return;

            remaining -= Time.unscaledDeltaTime;
            if (remaining <= 0f)
            {
                remaining = 0f;
                totalDuration = 0f;
                transform.localPosition = initialPosition;
                transform.localRotation = initialRotation;
                return;
            }

            float fade = remaining / totalDuration;
            float offset = Mathf.Min(maxOffset, magnitude) * fade;
            float t = Time.unscaledTime * 28f;
            float x = (Mathf.PerlinNoise(seed, t) * 2f - 1f) * offset;
            float y = (Mathf.PerlinNoise(seed + 17f, t) * 2f - 1f) * offset;
            float roll = (Mathf.PerlinNoise(seed + 41f, t) * 2f - 1f) * maxRoll * Mathf.Min(1f, magnitude) * fade;

            transform.localPosition = initialPosition + new Vector3(x, y, 0f);
            transform.localRotation = initialRotation * Quaternion.Euler(0f, 0f, roll);
        }
    }
}
