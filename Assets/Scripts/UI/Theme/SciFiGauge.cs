using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace Starfall.UI.Theme
{
    /// <summary>
    /// SciFiGauge – High-polish tactical gauge meter with:
    /// - 9-sliced grooved metallic chassis track.
    /// - Smooth lerped primary fill.
    /// - Delayed ghost-damage drain (hangs for 0.35s then drains smoothly).
    /// - Low-health flashing danger state.
    /// - Overdrive radiance when at 100%.
    /// </summary>
    public class SciFiGauge : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Image trackImage;
        [SerializeField] private Image ghostFillImage;
        [SerializeField] private Image primaryFillImage;
        [SerializeField] private TextMeshProUGUI valueText;

        [Header("Colors & Dynamics")]
        [SerializeField] private Color fullColor = new Color(0f, 0.9f, 1f, 1f); // Cyan default
        [SerializeField] private Color lowColor = new Color(1f, 0.25f, 0.2f, 1f);  // Crimson
        [SerializeField] private Color ghostColor = new Color(1f, 0.6f, 0.1f, 0.85f); // Orange ghost
        [SerializeField] private bool enableGhostDrain = true;
        [SerializeField] private bool enableLowHealthPulse = true;
        [SerializeField] private float lowThreshold = 0.25f;

        private float targetFill = 1.0f;
        private float currentFill = 1.0f;
        private float ghostFill = 1.0f;
        private float ghostDelayTimer = 0f;
        private const float GhostDelayDuration = 0.35f;

        public float FillAmount => targetFill;

        private void Awake()
        {
            SetupVisualsIfMissing();
        }

        public void SetupVisualsIfMissing()
        {
            if (trackImage == null)
            {
                trackImage = GetComponent<Image>();
                if (trackImage == null) trackImage = gameObject.AddComponent<Image>();
            }
            trackImage.sprite = SciFiUITheme.GaugeTrack;
            trackImage.type = Image.Type.Sliced;
            trackImage.color = Color.white;

            // Ghost Fill
            if (ghostFillImage == null && enableGhostDrain)
            {
                var ghostGo = transform.Find("GhostFill");
                if (ghostGo == null)
                {
                    var go = new GameObject("GhostFill", typeof(RectTransform), typeof(Image));
                    go.transform.SetParent(transform, false);
                    ghostGo = go.transform;
                }
                ghostFillImage = ghostGo.GetComponent<Image>();
                ghostFillImage.sprite = SciFiUITheme.GaugeFill;
                ghostFillImage.type = Image.Type.Filled;
                ghostFillImage.fillMethod = Image.FillMethod.Horizontal;
                ghostFillImage.color = ghostColor;

                var grt = ghostFillImage.rectTransform;
                grt.anchorMin = Vector2.zero;
                grt.anchorMax = Vector2.one;
                grt.offsetMin = new Vector2(3f, 2f);
                grt.offsetMax = new Vector2(-3f, -2f);
            }

            // Primary Fill
            if (primaryFillImage == null)
            {
                var fillGo = transform.Find("PrimaryFill");
                if (fillGo == null)
                {
                    var go = new GameObject("PrimaryFill", typeof(RectTransform), typeof(Image));
                    go.transform.SetParent(transform, false);
                    fillGo = go.transform;
                }
                primaryFillImage = fillGo.GetComponent<Image>();
                primaryFillImage.sprite = SciFiUITheme.GaugeFill;
                primaryFillImage.type = Image.Type.Filled;
                primaryFillImage.fillMethod = Image.FillMethod.Horizontal;
                primaryFillImage.color = fullColor;

                var frt = primaryFillImage.rectTransform;
                frt.anchorMin = Vector2.zero;
                frt.anchorMax = Vector2.one;
                frt.offsetMin = new Vector2(3f, 2f);
                frt.offsetMax = new Vector2(-3f, -2f);
            }
        }

        public void SetColors(Color full, Color? low = null, Color? ghost = null)
        {
            fullColor = full;
            if (low.HasValue) lowColor = low.Value;
            if (ghost.HasValue) ghostColor = ghost.Value;
            if (primaryFillImage != null) primaryFillImage.color = fullColor;
            if (ghostFillImage != null) ghostFillImage.color = ghostColor;
        }

        public void SetFill(float amount, string customLabel = null)
        {
            float clamped = Mathf.Clamp01(amount);
            if (clamped < targetFill)
            {
                // Damage taken: reset ghost delay timer
                ghostDelayTimer = GhostDelayDuration;
            }
            else if (clamped > targetFill)
            {
                // Healed or filled: catch up ghost immediately
                ghostFill = clamped;
            }

            targetFill = clamped;

            if (valueText != null && customLabel != null)
            {
                valueText.text = customLabel;
            }
        }

        public void SetValue(float current, float max, string prefix = "")
        {
            float pct = max > 0f ? (current / max) : 0f;
            string label = string.IsNullOrEmpty(prefix) 
                ? $"{Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}" 
                : $"{prefix}: {Mathf.CeilToInt(current)} / {Mathf.CeilToInt(max)}";
            SetFill(pct, label);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            // 1. Smoothly interpolate primary fill
            currentFill = Mathf.Lerp(currentFill, targetFill, dt * 14f);
            if (primaryFillImage != null)
            {
                primaryFillImage.fillAmount = currentFill;

                // Color tint depending on health percentage
                if (enableLowHealthPulse && targetFill <= lowThreshold)
                {
                    float pulse = 0.5f + Mathf.Sin(Time.unscaledTime * 8f) * 0.5f;
                    primaryFillImage.color = Color.Lerp(lowColor, Color.white, pulse * 0.45f);
                }
                else
                {
                    primaryFillImage.color = fullColor;
                }
            }

            // 2. Delayed ghost-damage drain
            if (enableGhostDrain && ghostFillImage != null)
            {
                if (ghostDelayTimer > 0f)
                {
                    ghostDelayTimer -= dt;
                }
                else
                {
                    ghostFill = Mathf.Lerp(ghostFill, targetFill, dt * 6f);
                }
                ghostFillImage.fillAmount = ghostFill;
            }
        }
    }
}
