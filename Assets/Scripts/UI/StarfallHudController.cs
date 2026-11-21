using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Starfall.Core;
using Starfall.Player;
using RiveAsset = global::Rive.Asset;
using RiveFit = global::Rive.Fit;
using RiveWidget = global::Rive.Components.RiveWidget;
using RivePanel = global::Rive.Components.RivePanel;
using RiveCanvasRenderer = global::Rive.Components.RiveCanvasRenderer;
using RiveWidgetStatus = global::Rive.Components.WidgetStatus;
using RiveHitTest = global::Rive.Components.HitTestBehavior;
using RiveScaling = global::Rive.Components.LayoutScalingMode;
using RiveViewModelInstance = global::Rive.ViewModelInstance;
using RiveNumberProp = global::Rive.ViewModelInstanceNumberProperty;
using RiveTriggerProp = global::Rive.ViewModelInstanceTriggerProperty;

namespace Starfall.UI
{
    /// <summary>
    /// Drives the Rive gameplay HUD (Resources/Rive/starfall_hud.riv, artboard "HUD", view model "HudVM")
    /// from the live game, and routes its ability / bomb / pause buttons back into gameplay.
    /// While it is running, the legacy HUD readouts are hidden; if the .riv fails to load they stay.
    /// Installs itself only in the scenes listed in <see cref="EnabledScenes"/>.
    /// </summary>
    public class StarfallHudController : MonoBehaviour
    {
        private static readonly string[] EnabledScenes =
        {
            "Level1_EarthOrbit", "Level2_AsteroidBelt", "Level3_DeepNebula", "Level4_WarTornWreckage", "Level4_RiveTest"
        };

        private const string ResourcePath = "Rive/starfall_hud";
        private const string ArtboardName = "HUD";
        private const string StateMachineName = "Main";
        private const string ViewModelName = "HudVM";

        // Legacy Rive HUD pieces built by RiveAnimationManager ("Rive_" + element name).
        private static readonly string[] LegacyRiveRoots = { "Rive_PlayerHealthBar", "Rive_AbilityGauge" };

        public static StarfallHudController Instance { get; private set; }

        private RiveWidget widget;
        private RiveViewModelInstance vm;
        private readonly Dictionary<string, RiveNumberProp> numbers = new Dictionary<string, RiveNumberProp>();
        private readonly Dictionary<string, float> lastSent = new Dictionary<string, float>();
        private RiveTriggerProp hitTrigger, comboUpTrigger, useAbilityTrigger, useBombTrigger, pauseTrigger;

        private PlayerHealth health;
        private PlayerCombat combat;
        private bool ready;
        private float shownScore;
        private int lastMultiplier = 1;
        private float legacySweepTimer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void InstallHook()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            OnSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (Instance != null || System.Array.IndexOf(EnabledScenes, scene.name) < 0) return;
            Debug.Log($"[StarfallHud] Installing the Rive HUD in '{scene.name}'.");
            new GameObject("StarfallHudController").AddComponent<StarfallHudController>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private IEnumerator Start()
        {
            var asset = Resources.Load<RiveAsset>(ResourcePath);
            if (asset == null)
            {
                Debug.LogError($"[StarfallHud] '{ResourcePath}.riv' not found or not imported as a Rive asset. Legacy HUD stays.");
                yield break;
            }

            // GameplayHUD creates its canvas in Start; give it a frame.
            yield return null;
            BuildWidget(asset);

            float timer = 0f;
            while (timer < 10f && !(widget.Status == RiveWidgetStatus.Loaded && widget.StateMachine != null))
            {
                if (widget.Status == RiveWidgetStatus.Error) break;
                timer += Time.unscaledDeltaTime;
                yield return null;
            }
            if (widget.Status != RiveWidgetStatus.Loaded || widget.StateMachine == null)
            {
                Debug.LogError($"[StarfallHud] Rive widget did not load (status: {widget.Status}). Legacy HUD stays.");
                Destroy(widget.transform.parent.gameObject);
                yield break;
            }

            if (!BindViewModel()) yield break;

            HookGame();
            GameplayHUD.Instance?.SetExternalHudMode(true);
            HideLegacyRive();
            ready = true;
            Debug.Log("[StarfallHud] Rive HUD loaded and bound to HudVM.");
        }

        private void BuildWidget(RiveAsset asset)
        {
            // Sit at the back of the gameplay canvas so announcements and modals draw over the HUD.
            Canvas canvas = GameplayHUD.Instance != null ? GameplayHUD.Instance.GetComponentInParent<Canvas>() : null;
            if (canvas != null) canvas = canvas.rootCanvas;
            if (canvas == null)
            {
                var canvasGo = new GameObject("StarfallHud Canvas", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
                canvas = canvasGo.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }

            var root = new GameObject("Rive_StarfallHud", typeof(RectTransform));
            root.transform.SetParent(canvas.transform, false);
            root.transform.SetAsFirstSibling();
            var rt = (RectTransform)root.transform;
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            root.AddComponent<RivePanel>();
            root.AddComponent<RiveCanvasRenderer>();

            var widgetGo = new GameObject("Widget", typeof(RectTransform));
            widgetGo.transform.SetParent(root.transform, false);
            var wr = (RectTransform)widgetGo.transform;
            wr.anchorMin = Vector2.zero;
            wr.anchorMax = Vector2.one;
            wr.offsetMin = Vector2.zero;
            wr.offsetMax = Vector2.zero;

            widget = widgetGo.AddComponent<RiveWidget>();
            // Layout fit keeps each panel pinned to its screen edge on any aspect ratio;
            // reference-size scaling keeps the 1920x1080 design proportional to the screen.
            widget.Fit = RiveFit.Layout;
            widget.ScalingMode = RiveScaling.ReferenceArtboardSize;
            // Only the HUD's own buttons take touches; everything else reaches the flight controls.
            widget.HitTestBehavior = RiveHitTest.Translucent;
            widget.Load(asset, ArtboardName, StateMachineName);
        }

        private bool BindViewModel()
        {
            var viewModel = widget.File != null ? widget.File.GetViewModelByName(ViewModelName) : null;
            if (viewModel == null)
            {
                Debug.LogError($"[StarfallHud] View model '{ViewModelName}' not found in starfall_hud.riv. Legacy HUD stays.");
                return false;
            }
            vm = viewModel.CreateDefaultInstance() ?? viewModel.CreateInstance();
            if (vm == null)
            {
                Debug.LogError($"[StarfallHud] Could not create an instance of '{ViewModelName}'. Legacy HUD stays.");
                return false;
            }
            widget.StateMachine.BindViewModelInstance(vm);

            var missing = new List<string>();
            foreach (var n in new[] { "hull", "shield", "lives", "maxLives", "score", "comboMultiplier", "comboTime",
                                      "weaponLevel", "bombs", "wave", "totalWaves", "abilityCooldown" })
            {
                var p = vm.GetNumberProperty(n);
                if (p != null) numbers[n] = p; else missing.Add($"{n} (number)");
            }
            RiveTriggerProp Trig(string n) { var p = vm.GetTriggerProperty(n); if (p == null) missing.Add($"{n} (trigger)"); return p; }
            hitTrigger = Trig("hit");
            comboUpTrigger = Trig("comboUp");
            useAbilityTrigger = Trig("useAbility");
            useBombTrigger = Trig("useBomb");
            pauseTrigger = Trig("pause");

            if (missing.Count > 0)
                Debug.LogWarning("[StarfallHud] HudVM is missing: " + string.Join(", ", missing));

            if (useAbilityTrigger != null) useAbilityTrigger.OnTriggered += OnAbilityPressed;
            if (useBombTrigger != null) useBombTrigger.OnTriggered += OnBombPressed;
            if (pauseTrigger != null) pauseTrigger.OnTriggered += OnPausePressed;
            return true;
        }

        private void HookGame()
        {
            health = FindAnyObjectByType<PlayerHealth>(FindObjectsInactive.Include);
            combat = FindAnyObjectByType<PlayerCombat>(FindObjectsInactive.Include);
            if (health != null) health.OnDamageTaken += OnPlayerHit;
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnComboChanged += OnComboChanged;
                shownScore = GameManager.Instance.CurrentScore;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (useAbilityTrigger != null) useAbilityTrigger.OnTriggered -= OnAbilityPressed;
            if (useBombTrigger != null) useBombTrigger.OnTriggered -= OnBombPressed;
            if (pauseTrigger != null) pauseTrigger.OnTriggered -= OnPausePressed;
            if (health != null) health.OnDamageTaken -= OnPlayerHit;
            if (GameManager.Instance != null) GameManager.Instance.OnComboChanged -= OnComboChanged;
        }

        // ------------------------------------------------------------------ game -> HUD

        private void Update()
        {
            if (!ready) return;

            if (health != null)
            {
                Send("hull", health.MaxHull > 0 ? 100f * health.CurrentHull / health.MaxHull : 0f);
                // Stacked shields above one full layer still read as a full bar.
                Send("shield", health.MaxShield > 0 ? Mathf.Min(100f, 100f * health.CurrentShield / health.MaxShield) : 0f);
                Send("lives", health.CurrentLives);
                Send("maxLives", health.MaxLives);
            }

            if (combat != null)
            {
                Send("weaponLevel", combat.WeaponLevel);
                Send("bombs", combat.CurrentBombs);
                Send("abilityCooldown", combat.IsAbilityReady ? 1f
                    : combat.AbilityCooldown > 0f ? Mathf.Clamp01(combat.AbilityTimer / combat.AbilityCooldown) : 1f);
            }

            var gm = GameManager.Instance;
            if (gm != null)
            {
                // Count the score up instead of jumping, fast enough to keep pace with big bonuses.
                float target = gm.CurrentScore;
                shownScore = Mathf.MoveTowards(shownScore, target, Mathf.Max(300f, Mathf.Abs(target - shownScore) * 8f) * Time.unscaledDeltaTime);
                Send("score", Mathf.Round(shownScore));
                Send("comboMultiplier", gm.ComboMultiplier);
                Send("comboTime", gm.ComboTimeLeft01);
            }

            var waves = WaveManager.Instance;
            if (waves != null)
            {
                Send("wave", waves.CurrentWaveNumber);
                Send("totalWaves", waves.TotalWaves);
            }

            // Legacy HUD pieces are created asynchronously and re-shown after a revive.
            legacySweepTimer -= Time.unscaledDeltaTime;
            if (legacySweepTimer <= 0f)
            {
                legacySweepTimer = 1f;
                HideLegacyRive();
            }
        }

        private void Send(string name, float value)
        {
            if (!numbers.TryGetValue(name, out var prop)) return;
            if (lastSent.TryGetValue(name, out var last) && Mathf.Approximately(last, value)) return;
            lastSent[name] = value;
            prop.Value = value;
        }

        private void OnPlayerHit() => hitTrigger?.Trigger();

        private void OnComboChanged(int kills, int multiplier)
        {
            if (multiplier > lastMultiplier && multiplier > 1) comboUpTrigger?.Trigger();
            lastMultiplier = multiplier;
        }

        // ------------------------------------------------------------------ HUD -> game

        private void OnAbilityPressed()
        {
            if (PauseMenu.IsPaused) return;
            combat?.ActivateSpecialAbility();
        }

        private void OnBombPressed()
        {
            if (PauseMenu.IsPaused) return;
            combat?.UseBomb();
        }

        private void OnPausePressed() => PauseMenu.Instance?.Toggle();

        private static void HideLegacyRive()
        {
            foreach (var name in LegacyRiveRoots)
            {
                var go = GameObject.Find(name);
                if (go != null) go.SetActive(false);
            }
            foreach (var b in FindObjectsByType<RiveSpecialButton>())
                if (b.gameObject.activeSelf) b.gameObject.SetActive(false);
            foreach (var h in FindObjectsByType<RiveHealthHUD>())
                if (h.gameObject.activeSelf) h.gameObject.SetActive(false);
        }
    }
}
