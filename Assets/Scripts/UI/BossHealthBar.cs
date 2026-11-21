using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using Starfall.Enemies;

namespace Starfall.UI
{
    /// <summary>
    /// BossHealthBar – slides in from the top of the screen when the boss spawns.
    /// Displays the boss name, a smooth animated health bar, and a phase indicator.
    /// Slides out and hides after the boss is defeated.
    /// </summary>
    public class BossHealthBar : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private RectTransform panelRect;
        [SerializeField] private Image healthFillImage;
        [SerializeField] private Text bossNameText;
        [SerializeField] private Text phaseText;

        [Header("Animation")]
        [SerializeField] private float slideInDuration = 0.6f;
        [SerializeField] private float slideOutDuration = 0.5f;
        [SerializeField] private Vector2 hiddenAnchoredPos = new Vector2(0f, 120f);
        [SerializeField] private Vector2 visibleAnchoredPos = new Vector2(0f, -20f);

        [Header("Colors")]
        [SerializeField] private Color phase1Color = new Color(0.2f, 0.8f, 1f);   // cyan
        [SerializeField] private Color phase2Color = new Color(1f, 0.65f, 0.1f);  // orange
        [SerializeField] private Color phase3Color = new Color(1f, 0.25f, 0.25f); // red

        private float targetFill = 1f;
        private float currentFill = 1f;
        private Coroutine slideCoroutine;

        private void Awake()
        {
            // Start hidden
            if (panelRect != null)
            {
                panelRect.anchoredPosition = hiddenAnchoredPos;
                var bg = panelRect.GetComponent<Image>();
                if (bg != null) Starfall.UI.Theme.SciFiUITheme.ApplyGlass(bg, new Color(0.08f, 0.02f, 0.02f, 0.90f));
            }

            if (healthFillImage != null)
            {
                healthFillImage.sprite = Starfall.UI.Theme.SciFiUITheme.GaugeFill;
                healthFillImage.type = Image.Type.Filled;
                healthFillImage.fillMethod = Image.FillMethod.Horizontal;
                healthFillImage.fillAmount = 1f;
            }
        }

        private void OnEnable()
        {
            InvaderCarrier.OnHealthChanged += HandleHealthChanged;
            InvaderCarrier.OnPhaseChanged  += HandlePhaseChanged;
            InvaderCarrier.OnBossDied      += HandleBossDied;
        }

        private void OnDisable()
        {
            InvaderCarrier.OnHealthChanged -= HandleHealthChanged;
            InvaderCarrier.OnPhaseChanged  -= HandlePhaseChanged;
            InvaderCarrier.OnBossDied      -= HandleBossDied;
        }

        private void Update()
        {
            // Smooth health bar drain
            if (!Mathf.Approximately(currentFill, targetFill))
            {
                currentFill = Mathf.MoveTowards(currentFill, targetFill, Time.deltaTime * 0.8f);
                if (healthFillImage != null)
                    healthFillImage.fillAmount = currentFill;
            }
        }

        private void HandleHealthChanged(int current, int max)
        {
            targetFill = (max > 0) ? (float)current / max : 0f;

            // Slide in on first health event (boss just spawned)
            if (panelRect != null &&
                Mathf.Approximately(panelRect.anchoredPosition.y, hiddenAnchoredPos.y))
            {
                SlideIn();
            }
        }

        private void HandlePhaseChanged(int phase)
        {
            if (phaseText != null)
                phaseText.text = $"PHASE {phase}";

            if (healthFillImage == null) return;

            Color c = phase switch
            {
                2 => phase2Color,
                3 => phase3Color,
                _ => phase1Color,
            };
            healthFillImage.color = c;
        }

        private void HandleBossDied()
        {
            if (phaseText != null) phaseText.text = "DEFEATED";
            SlideOut();
        }

        private void SlideIn()
        {
            if (slideCoroutine != null) StopCoroutine(slideCoroutine);
            slideCoroutine = StartCoroutine(SlidePanel(visibleAnchoredPos, slideInDuration));
        }

        private void SlideOut()
        {
            if (slideCoroutine != null) StopCoroutine(slideCoroutine);
            slideCoroutine = StartCoroutine(SlidePanel(hiddenAnchoredPos, slideOutDuration));
        }

        private IEnumerator SlidePanel(Vector2 targetPos, float duration)
        {
            if (panelRect == null) yield break;

            Vector2 startPos = panelRect.anchoredPosition;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
                panelRect.anchoredPosition = Vector2.Lerp(startPos, targetPos, t);
                yield return null;
            }

            panelRect.anchoredPosition = targetPos;
            slideCoroutine = null;
        }

        /// <summary>Called by setup script to set the boss name displayed on the bar.</summary>
        public void SetBossName(string name)
        {
            if (bossNameText != null)
                bossNameText.text = name;
        }
    }
}
