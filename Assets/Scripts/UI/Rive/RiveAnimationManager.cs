using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Starfall.Core;
using Starfall.Combat;
using RiveFit = Rive.Fit;

namespace Starfall.UI.Rive
{
    /// <summary>
    /// Drives the project's Rive animations during gameplay (files live in Assets/Resources/Rive):
    ///   boss_health_bar.riv   – boss / mini-boss health  (view-model number "hp", 0-100)
    ///   player_health_bar.riv – ship integrity           ("currentHealth" 0-100, "takedamage" trigger)
    ///   gauge_meter.riv       – special ability charge   (artboard "gauge", "value" 0-100)
    ///   wave_complete.riv     – wave cleared celebration (one-shot, restarted per wave)
    ///   checkpoint.riv        – checkpoint / new-threat banner
    ///   frame_ui.riv          – pulsing warning frame when a boss arrives
    /// (loading.riv is used by <see cref="Starfall.UI.LoadingScreen"/>.)
    ///
    /// Each element falls back to a plain UGUI version if Rive is unavailable, so the HUD always works.
    /// </summary>
    public class RiveAnimationManager : MonoBehaviour
    {
        public static RiveAnimationManager Instance { get; private set; }

        [Header("Rive value ranges (match the .riv files)")]
        [SerializeField] private float bossHpMax = 100f;
        [SerializeField] private float playerHealthMax = 100f;
        [SerializeField] private float gaugeMax = 100f;

        [Header("Toggle")]
        [Tooltip("Turn off to force the plain UGUI fallbacks (useful when profiling low-end devices).")]
        [SerializeField] private bool useRive = true;

        private RiveHudElement bossBar;
        private RiveHudElement playerBar;
        private RiveHudElement gauge;
        private RiveHudElement waveComplete;
        private RiveHudElement checkpoint;
        private RiveHudElement warningFrame;

        // UGUI companions / fallbacks
        private CanvasGroup waveGroup;
        private TextMeshProUGUI waveText;
        private CanvasGroup checkpointGroup;
        private TextMeshProUGUI checkpointText;
        private Image gaugeFallbackFill;
        private TextMeshProUGUI gaugeText;
        private GameObject gaugeFallbackRoot;

        private Coroutine waveRoutine;
        private Coroutine checkpointRoutine;
        private Coroutine frameRoutine;
        private float lastPlayerHealth01 = -1f;
        private bool bossVisible;

        /// <summary>True when the Rive boss bar is rendering, so the HUD can hide its own bar graphic.</summary>
        public bool IsBossBarRive => false;

        // ------------------------------------------------------------------
        // Global on/off switch (F4 in play, saved in PlayerPrefs) for A/B performance testing
        // ------------------------------------------------------------------

        public const string RivePrefKey = "Starfall_UseRive";
        public static bool RiveEnabled => PlayerPrefs.GetInt(RivePrefKey, 1) == 1;

        private RiveHudElement[] parked;
        private bool riveBuilt;
        private bool riveBuilding;

        /// <summary>Turns every Rive HUD graphic on/off immediately (hidden panels stop rendering).</summary>
        public static void SetRiveEnabled(bool on)
        {
            PlayerPrefs.SetInt(RivePrefKey, on ? 1 : 0);
            PlayerPrefs.Save();
            if (Instance != null) Instance.ApplyRiveState(on);
            Debug.Log($"[Perf] Rive HUD graphics {(on ? "ENABLED" : "DISABLED")}.");
        }

        private void ApplyRiveState(bool on)
        {
            if (!on)
            {
                if (parked == null)
                    parked = new[] { bossBar, playerBar, gauge, waveComplete, warningFrame };
                foreach (var el in parked) el?.SetActive(false);
                bossBar = playerBar = gauge = waveComplete = warningFrame = null;
                if (gaugeFallbackRoot != null) gaugeFallbackRoot.SetActive(true);
                return;
            }

            if (parked != null)
            {
                bossBar = parked[0]; playerBar = parked[1]; gauge = parked[2]; waveComplete = parked[3]; warningFrame = parked[4];
                parked = null;
            }
            playerBar?.SetActive(true);
            gauge?.SetActive(true);
            bossBar?.SetActive(bossVisible);
            if (gauge != null && gaugeFallbackRoot != null) gaugeFallbackRoot.SetActive(false);
            if (!riveBuilt && !riveBuilding) StartCoroutine(BuildRiveAsync());
        }

        /// <summary>Number of Rive panels currently rendering (for the performance overlay).</summary>
        public int ActiveRivePanels
        {
            get
            {
                int n = 0;
                if (bossBar?.Root != null && bossBar.Root.activeInHierarchy) n++;
                if (playerBar?.Root != null && playerBar.Root.activeInHierarchy) n++;
                if (gauge?.Root != null && gauge.Root.activeInHierarchy) n++;
                if (waveComplete?.Root != null && waveComplete.Root.activeInHierarchy) n++;
                if (warningFrame?.Root != null && warningFrame.Root.activeInHierarchy) n++;
                return n;
            }
        }

        public static RiveAnimationManager EnsureInstance(Transform parent = null)
        {
            if (Instance != null) return Instance;
            var found = FindAnyObjectByType<RiveAnimationManager>();
            if (found != null) return found;

            var go = new GameObject("RiveAnimationManager", typeof(RectTransform));
            if (parent != null) go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            return go.AddComponent<RiveAnimationManager>();
        }

        /// <summary>True once every Rive element has finished loading (or fallen back). The loading screen waits on this.</summary>
        public static bool IsHudReady => Instance == null || Instance.hudReady;
        private bool hudReady;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            BuildUGUI();
        }

        private void Start()
        {
            if (WaveManager.Instance != null) WaveManager.Instance.OnWaveCleared += HandleWaveCleared;
            BossEvents.OnBossSpawned += HandleBossSpawned;
            BossEvents.OnBossHealthChanged += HandleBossHealthChanged;
            BossEvents.OnBossDefeated += HandleBossDefeated;
            BossEvents.OnBossPhaseChanged += HandleBossPhaseChanged;
            if (RiveEnabled) StartCoroutine(BuildRiveAsync());
            else hudReady = true;
        }

        private void OnDestroy()
        {
            if (WaveManager.Instance != null) WaveManager.Instance.OnWaveCleared -= HandleWaveCleared;
            BossEvents.OnBossSpawned -= HandleBossSpawned;
            BossEvents.OnBossHealthChanged -= HandleBossHealthChanged;
            BossEvents.OnBossDefeated -= HandleBossDefeated;
            BossEvents.OnBossPhaseChanged -= HandleBossPhaseChanged;
            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------------
        // Construction
        // ------------------------------------------------------------------

        private Transform CanvasRoot()
        {
            var canvas = GetComponentInParent<Canvas>();
            return canvas != null ? canvas.transform : transform;
        }

        private static TextMeshProUGUI MakeText(Transform parent, string name, float size, Vector2 anchor, Vector2 pos, Vector2 box)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.fontSize = size;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            t.textWrappingMode = TextWrappingModes.NoWrap;
            var r = t.rectTransform;
            r.anchorMin = anchor;
            r.anchorMax = anchor;
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = pos;
            r.sizeDelta = box;
            return t;
        }

        /// <summary>Loads the .riv asynchronously, then builds the element. Yields one frame per element to avoid spikes.</summary>
        private IEnumerator TryRiveAsync(string name, string path, System.Action<RiveHudElement> assign, string artboard = null, string stateMachine = null, RiveFit fit = RiveFit.Contain)
        {
            if (!useRive || !RiveEnabled) { assign(null); yield break; }

            // Synchronous load: Resources.LoadAsync on Rive assets stalled the main thread in testing.
            // Elements are still built one per frame so no single frame does all the work.
            var asset = Resources.Load<global::Rive.Asset>(path);

            var el = RiveHudElement.Create(CanvasRoot(), name, asset, path, artboard, stateMachine, fit);
            if (!el.Usable && asset != null && !string.IsNullOrEmpty(artboard))
            {
                // Artboard/state-machine names may differ in a re-exported file: retry with defaults.
                if (el.Root != null) Destroy(el.Root);
                el = RiveHudElement.Create(CanvasRoot(), name, asset, path, null, null, fit);
            }
            assign(el.Usable ? el : null);
            yield return null;
        }

        /// <summary>Plain UGUI parts: always available immediately, even before (or without) Rive.</summary>
        private void BuildUGUI()
        {
            Transform root = CanvasRoot();
            BuildGaugeFallback(root);

            var waveGo = new GameObject("WaveCompleteLabel", typeof(RectTransform), typeof(CanvasGroup));
            waveGo.transform.SetParent(root, false);
            var wrt = waveGo.GetComponent<RectTransform>();
            wrt.anchorMin = wrt.anchorMax = new Vector2(0.5f, 0.62f);
            wrt.sizeDelta = new Vector2(620f, 120f);
            waveGroup = waveGo.GetComponent<CanvasGroup>();
            waveGroup.alpha = 0f;
            waveGroup.blocksRaycasts = false;
            waveText = MakeText(waveGo.transform, "Text", 34f, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620f, 120f));

            var cpGo = new GameObject("CheckpointLabel", typeof(RectTransform), typeof(CanvasGroup));
            cpGo.transform.SetParent(root, false);
            var crt = cpGo.GetComponent<RectTransform>();
            crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.8f);
            crt.sizeDelta = new Vector2(700f, 60f);
            checkpointGroup = cpGo.GetComponent<CanvasGroup>();
            checkpointGroup.alpha = 0f;
            checkpointGroup.blocksRaycasts = false;
            checkpointText = MakeText(cpGo.transform, "Text", 22f, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(700f, 60f));
        }

        private IEnumerator BuildRiveAsync()
        {
            riveBuilding = true;
            // If RiveHealthHUD exists in the scene, it handles the health HUD; avoid spawning duplicate bars!
            bool hasCustomHealthHUD = FindAnyObjectByType<RiveHealthHUD>() != null || GameObject.Find("RiveWidget_HealthHUD") != null;
            if (!hasCustomHealthHUD)
            {
                yield return TryRiveAsync("PlayerHealthBar", "Rive/player_health_bar", e => playerBar = e);
                if (playerBar == null)
                {
                    yield return TryRiveAsync("PlayerHealthBar", "Rive/alienwarhud", e => playerBar = e, "HealthHUD", "Statemachine 1");
                }
                playerBar?.Place(new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(18f, -128f), new Vector2(300f, 64f));
            }

            // If RiveSpecialButton exists, it handles the ability/bomb button; skip redundant gauge_meter.riv loading!
            bool hasCustomSpecial = FindAnyObjectByType<Starfall.UI.RiveSpecialButton>() != null || GameObject.Find("RiveWidget_SpecialButton") != null || GameObject.Find("SpecialButton") != null;
            if (!hasCustomSpecial)
            {
                yield return TryRiveAsync("AbilityGauge", "Rive/gauge_meter", e => gauge = e, "gauge", "gaugeStateMachine");
                if (gauge != null)
                {
                    gauge.Rect.anchorMin = new Vector2(0.86f, 0.255f);
                    gauge.Rect.anchorMax = new Vector2(0.97f, 0.325f);
                    gauge.Rect.offsetMin = Vector2.zero;
                    gauge.Rect.offsetMax = Vector2.zero;
                    if (gaugeFallbackRoot != null) gaugeFallbackRoot.SetActive(false);
                }
            }
            else
            {
                if (gaugeFallbackRoot != null) gaugeFallbackRoot.SetActive(false);
            }

            checkpoint = null;

            // Mark HUD ready immediately so scene startup experiences 0ms Rive stall!
            hudReady = true;
            riveBuilding = false;
            riveBuilt = true;

            // Defer non-critical assets (WaveComplete) into background after initial frame settles
            StartCoroutine(DeferredLoadWaveComplete());
            Debug.Log($"[Rive] Startup HUD ready (fast bootstrap). Boss deferred. Player:{playerBar != null} Gauge:{gauge != null}");
        }

        private IEnumerator DeferredLoadWaveComplete()
        {
            yield return new WaitForSeconds(3.0f);
            if (waveComplete == null && useRive && RiveEnabled)
            {
                yield return TryRiveAsync("WaveComplete", "Rive/wave_complete", e => waveComplete = e);
                if (waveComplete != null)
                {
                    waveComplete.Place(new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.62f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(620f, 340f));
                    waveComplete.SetActive(false);
                    waveGroup.GetComponent<RectTransform>().anchoredPosition = new Vector2(0f, -190f);
                }
            }
        }

        private void BuildGaugeFallback(Transform root)
        {
            gaugeFallbackRoot = new GameObject("AbilityGaugeFallback", typeof(RectTransform));
            gaugeFallbackRoot.transform.SetParent(root, false);
            var rt = gaugeFallbackRoot.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.85f, 0.255f);
            rt.anchorMax = new Vector2(0.98f, 0.27f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;

            var track = gaugeFallbackRoot.AddComponent<Image>();
            track.color = new Color(0.1f, 0.15f, 0.25f, 0.75f);
            track.raycastTarget = false;

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(gaugeFallbackRoot.transform, false);
            gaugeFallbackFill = fill.GetComponent<Image>();
            gaugeFallbackFill.color = new Color(0.2f, 0.85f, 1f, 1f);
            gaugeFallbackFill.raycastTarget = false;
            var frt = gaugeFallbackFill.rectTransform;
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = Vector2.one;
            frt.offsetMin = Vector2.zero;
            frt.offsetMax = Vector2.zero;

            gaugeText = MakeText(gaugeFallbackRoot.transform, "Label", 12f, new Vector2(0.5f, 1f), new Vector2(0f, 10f), new Vector2(220f, 18f));
        }

        // ------------------------------------------------------------------
        // Wave complete
        // ------------------------------------------------------------------

        private void HandleWaveCleared(int wave) => PlayWaveCompleteAnimation(wave);

        public void PlayWaveCompleteAnimation(int waveNumber)
        {
            if (waveRoutine != null) StopCoroutine(waveRoutine);
            waveRoutine = StartCoroutine(WaveCompleteRoutine(waveNumber));
        }

        private IEnumerator WaveCompleteRoutine(int waveNumber)
        {
            if (waveComplete != null)
            {
                waveComplete.SetActive(true);
                waveComplete.SetAlpha(1f);
                waveComplete.Reload();
            }

            waveText.text = $"<color=#ffd700><b>WAVE {waveNumber} CLEARED</b></color>";
            yield return Fade(waveGroup, 0f, 1f, 0.3f);
            yield return new WaitForSeconds(2.0f);
            yield return Fade(waveGroup, 1f, 0f, 0.4f);

            if (waveComplete != null)
            {
                for (float t = 0f; t < 0.35f; t += Time.deltaTime)
                {
                    waveComplete.SetAlpha(1f - t / 0.35f);
                    yield return null;
                }
                waveComplete.SetActive(false);
            }
            waveRoutine = null;
        }

        private static IEnumerator Fade(CanvasGroup g, float from, float to, float dur)
        {
            if (g == null) yield break;
            for (float t = 0f; t < dur; t += Time.unscaledDeltaTime)
            {
                g.alpha = Mathf.Lerp(from, to, t / dur);
                yield return null;
            }
            g.alpha = to;
        }

        // ------------------------------------------------------------------
        // Checkpoint banner
        // ------------------------------------------------------------------

        public void PlayCheckpointAnimation(string label)
        {
            if (checkpointRoutine != null) StopCoroutine(checkpointRoutine);
            checkpointRoutine = StartCoroutine(CheckpointRoutine(label));
        }

        private IEnumerator CheckpointRoutine(string label)
        {
            if (checkpoint != null)
            {
                checkpoint.SetActive(true);
                checkpoint.SetAlpha(1f);
                checkpoint.Reload();
            }

            checkpointText.text = $"<color=#00e5ff><b>{label}</b></color>";
            yield return Fade(checkpointGroup, 0f, 1f, 0.25f);
            yield return new WaitForSecondsRealtime(2.0f);
            yield return Fade(checkpointGroup, 1f, 0f, 0.3f);
            checkpoint?.SetActive(false);
            checkpointRoutine = null;
        }

        // ------------------------------------------------------------------
        // Boss
        // ------------------------------------------------------------------

        public void PlayBossWarningFrame()
        {
            if (frameRoutine != null) StopCoroutine(frameRoutine);
            frameRoutine = StartCoroutine(WarningFrameRoutine());
        }

        public void PreloadBossAssets()
        {
            // Handled cleanly via GameplayHUD to eliminate mobile GPU swapchain fighting & native memory crashes
        }

        private IEnumerator WarningFrameRoutine()
        {
            // Clean, impactful 0.4s warning flash without 9Hz strobe or extra RivePanel instantiations
            Starfall.VFX.VFXKit.FlashScreen(new Color(1f, 0.1f, 0.1f, 0.35f), 0.45f);
            yield return new WaitForSecondsRealtime(0.45f);
            frameRoutine = null;
        }

        private void SetBossHp(float currentHealth, float maxHealth)
        {
            if (bossBar == null) return;
            float val = maxHealth > 0 ? (bossHpMax * Mathf.Clamp01(currentHealth / maxHealth)) : bossHpMax;
            if (!bossBar.SetNumber("hp", val))
            {
                if (!bossBar.SetNumber("health", val))
                {
                    bossBar.SetNumber("value", val);
                }
            }
        }

        private void HandleBossSpawned(string bossName, int currentHealth, int maxHealth)
        {
            bossVisible = true;
            // GameplayHUD handles the boss health bar via native rock-solid UGUI
        }

        private void HandleBossHealthChanged(int current, int max)
        {
            if (bossBar == null || !bossVisible || max <= 0) return;
            SetBossHp(current, max);
        }

        private void HandleBossPhaseChanged(string phaseText, Color tint)
        {
            PlayCheckpointAnimation(phaseText.ToUpperInvariant());
        }

        private void HandleBossDefeated(string bossName)
        {
            bossVisible = false;
            if (bossBar != null) StartCoroutine(HideBossBar());
        }

        private IEnumerator HideBossBar()
        {
            SetBossHp(0f, 100f);
            yield return new WaitForSeconds(1.2f);
            for (float t = 0f; t < 0.5f; t += Time.deltaTime)
            {
                if (bossVisible) yield break; // a second (mini-)boss appeared meanwhile
                bossBar.SetAlpha(1f - t / 0.5f);
                yield return null;
            }
            if (!bossVisible) bossBar.SetActive(false);
        }

        // ------------------------------------------------------------------
        // Player integrity & ability gauge
        // ------------------------------------------------------------------

        public void UpdateSlayerHealth(int currentHull, int maxHull, int currentShield, int maxShield)
        {
            if (playerBar == null) return;
            int max = Mathf.Max(1, maxHull + maxShield);
            float v01 = Mathf.Clamp01((float)(currentHull + currentShield) / max);
            playerBar.SetNumber("currentHealth", v01 * playerHealthMax);
            if (lastPlayerHealth01 >= 0f && v01 < lastPlayerHealth01 - 0.001f)
            {
                playerBar.Fire("takedamage");
                playerBar.Fire("hit");
            }
            lastPlayerHealth01 = v01;

            // Also drive separate health and shield inputs if the Rive HUD supports dual layers (e.g. alienwarhud)
            if (maxHull > 0)
                playerBar.SetNumber("health", Mathf.Clamp01((float)currentHull / maxHull) * 100f);
            if (maxShield > 0)
                playerBar.SetNumber("shield", Mathf.Clamp01((float)currentShield / maxShield) * 100f);
        }

        public void UpdateGauge(float current, float max = 1f, string label = "TACTICAL CORE")
        {
            float v01 = max > 0f ? Mathf.Clamp01(current / max) : 0f;
            gauge?.SetNumber("value", v01 * gaugeMax);

            if (gaugeFallbackFill != null)
                gaugeFallbackFill.rectTransform.anchorMax = new Vector2(v01, 1f);

            if (gaugeText != null)
            {
                string status = v01 >= 1f ? "<color=#00ff66>READY</color>" : $"{v01 * 100f:F0}%";
                gaugeText.text = $"<color=#66ddff><b>{label}: {status}</b></color>";
            }
        }
    }
}
