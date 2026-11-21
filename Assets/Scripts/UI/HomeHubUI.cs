using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Starfall.Levels;
using Starfall.Core;
using Starfall.Audio;
using Starfall.VFX;
using Starfall.UI.Theme;

// Rive aliases (namespace Starfall.UI.Rive exists, so the Rive package types are aliased via global::)
using RiveAsset = global::Rive.Asset;
using RiveWidget = global::Rive.Components.RiveWidget;
using RiveWidgetStatus = global::Rive.Components.WidgetStatus;
using RiveViewModelInstance = global::Rive.ViewModelInstance;
using RiveNumberProp = global::Rive.ViewModelInstanceNumberProperty;
using RiveStringProp = global::Rive.ViewModelInstanceStringProperty;
using RiveTriggerProp = global::Rive.ViewModelInstanceTriggerProperty;
using RiveReportedEvent = global::Rive.ReportedEvent;

namespace Starfall.UI
{
    /// <summary>
    /// HomeHubUI – Central Command Center for STARFALL: Alienwar.
    /// Provides 4 interactive modular tabs:
    /// 1. Missions (Sector & Wave deployment matrix)
    /// 2. Armory Shop (Permanent spaceship hangar upgrades)
    /// 3. Settings (Audio volume sliders, Screen Shake, Fullscreen, Flight Manual, Reset)
    /// 4. Pilot Dossier (Player progress & high scores)
    /// Plus clean Exit Game functionality.
    /// </summary>
    public class HomeHubUI : MonoBehaviour
    {
        public enum HubTab { Missions, Shop, Settings, Dossier }
        private HubTab currentTab = HubTab.Missions;

        // Header UI
        private TextMeshProUGUI creditsText;
        private TextMeshProUGUI callsignText;

        // Navigation Tab Buttons & Indicators
        private Button tabMissionsBtn;
        private Button tabShopBtn;
        private Button tabSettingsBtn;
        private Button tabDossierBtn;
        private Button tabQuitBtn;

        private Image tabMissionsBg;
        private Image tabShopBg;
        private Image tabSettingsBg;
        private Image tabDossierBg;

        // Panels
        private GameObject backgroundGo;
        private GameObject panelMissions;
        private GameObject panelShop;
        private GameObject panelSettings;
        private GameObject panelDossier;

        // Missions Panel Data
        private class SectorCardView
        {
            public int levelNumber;
            public GameObject cardGo;
            public Image cardBackground;
            public TextMeshProUGUI titleText;
            public TextMeshProUGUI statusText;
            public TextMeshProUGUI progressText;
            public Button deployButton;
            public TextMeshProUGUI deployButtonText;
            public GameObject waveMatrixContainer;
            public Button[] waveButtons = new Button[10];
            public TextMeshProUGUI[] waveButtonTexts = new TextMeshProUGUI[10];
            public Image[] waveButtonImages = new Image[10];
        }
        private SectorCardView[] sectorCards = new SectorCardView[4];

        // Fleet Hangar / Armory Shop Elements
        private int selectedHangarIndex = 0;
        private Button[] shipSelectorButtons = new Button[5];
        private Image[] shipSelectorBgs = new Image[5];
        private TextMeshProUGUI[] shipSelectorTexts = new TextMeshProUGUI[5];

        // Ship Showcase View (Left Column)
        private TextMeshProUGUI showcaseTitleText;
        private TextMeshProUGUI showcaseRoleText;
        private Image showcaseShipImage;
        private Image showcaseGlowImage;
        private TextMeshProUGUI showcaseHardpointText;
        private TextMeshProUGUI showcaseAbilityText;
        private TextMeshProUGUI showcaseStatusText;
        private Button showcaseActionBtn;
        private TextMeshProUGUI showcaseActionBtnText;
        private Image showcaseActionBtnBg;

        // Combat Attribute Bars (Right Column - Top)
        private TextMeshProUGUI statStrengthText;
        private TextMeshProUGUI statDamageText;
        private TextMeshProUGUI statFlexibilityText;
        private TextMeshProUGUI statAbilityText;

        // Per-Ship Tech Upgrade Cards (Right Column - Bottom)
        private class ShipTechCardView
        {
            public string upgradeKey;
            public string title;
            public string description;
            public TextMeshProUGUI levelText;
            public TextMeshProUGUI costText;
            public Button buyButton;
            public TextMeshProUGUI buyButtonText;
            public Image buyButtonImage;
        }
        private ShipTechCardView[] shipTechCards = new ShipTechCardView[4];

        // Settings Elements
        private Slider masterSlider;
        private TextMeshProUGUI masterValText;
        private Slider musicSlider;
        private TextMeshProUGUI musicValText;
        private Slider sfxSlider;
        private TextMeshProUGUI sfxValText;
        private Button shakeToggleBtn;
        private TextMeshProUGUI shakeToggleText;
        private Image shakeToggleImg;
        private Button fullscreenToggleBtn;
        private TextMeshProUGUI fullscreenToggleText;
        private Image fullscreenToggleImg;
        private Button resetDataBtn;

        // Dossier Elements
        private TextMeshProUGUI dossierStatsText;

        public static HomeHubUI Instance { get; private set; }

        public int SelectedHangarIndex => selectedHangarIndex;
        public HubTab CurrentTab => currentTab;
        public Canvas HubCanvas => GetComponentInChildren<Canvas>(true);

        private void Awake()
        {
            Instance = this;
            EnsureEventSystem();
            EnsureAudioManager();
            BuildUIHierarchy();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            ShutdownRiveHome();
        }

        private void Start()
        {
            // Start on the currently equipped warship so Unity panels and the Rive stats agree.
            int activeIdx = System.Array.IndexOf(FleetDatabase.AllShipIds, ShipUpgradesManager.GetActiveShipId());
            if (activeIdx >= 0) selectedHangarIndex = activeIdx;

            SwitchTab(HubTab.Missions);
            RefreshAllViews();
            StartCoroutine(InitRiveHome());
        }

        private void Update()
        {
            if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                if (kb.escapeKey.wasPressedThisFrame)
                {
                    if (currentTab != HubTab.Missions && !launchStarted)
                    {
                        if (vmTab != null) vmTab.Value = 0f;
                        SwitchTab(HubTab.Missions);
                    }
                }
                else if ((kb.spaceKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame || kb.numpadEnterKey.wasPressedThisFrame) && !launchStarted)
                {
                    if (currentTab == HubTab.Missions)
                    {
                        Debug.Log("[HomeHubUI] Launch requested via keyboard shortcut (Space/Enter).");
                        BeginLaunch(true);
                    }
                }
            }

            UpdateRiveHome();
        }

        private void EnsureEventSystem()
        {
            if (UnityEngine.EventSystems.EventSystem.current == null &&
                FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var esGo = new GameObject("EventSystem");
                esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
                esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }
        }

        private void EnsureAudioManager()
        {
            if (AudioManager.Instance == null && FindAnyObjectByType<AudioManager>() == null)
            {
                var audioGo = new GameObject("AudioManager");
                audioGo.AddComponent<AudioManager>();
            }
        }

        public void SwitchTab(HubTab tab)
        {
            // After the launch sequence starts the panels must stay hidden for the rest of the scene.
            if (launchStarted) return;

            currentTab = tab;

            if (riveBound)
            {
                // In Rive mode: Tab 0 (Missions) is drawn by Rive itself (mission card + Launch button).
                // Show procedural panelMissions only if showUnityPanelsOverRive is explicitly requested.
                if (panelMissions != null) panelMissions.SetActive(showUnityPanelsOverRive && tab == HubTab.Missions);
                // When user taps Fleet Hangar (Tab 1) or Armory (Tab 2), show the Shop panel (Fleet Hangar & Tech Upgrades)
                if (panelShop != null) panelShop.SetActive(tab == HubTab.Shop);
                if (panelSettings != null) panelSettings.SetActive(tab == HubTab.Settings);
                // When user taps Dossier (Tab 3), show the Dossier panel
                if (panelDossier != null) panelDossier.SetActive(tab == HubTab.Dossier);
            }
            else
            {
                if (panelMissions != null) panelMissions.SetActive(tab == HubTab.Missions);
                if (panelShop != null) panelShop.SetActive(tab == HubTab.Shop);
                if (panelSettings != null) panelSettings.SetActive(tab == HubTab.Settings);
                if (panelDossier != null) panelDossier.SetActive(tab == HubTab.Dossier);
            }

            // Keep pad ship visible on Missions tab; hide behind overlay panels on other tabs.
            if (shipHolder != null)
            {
                shipHolder.gameObject.SetActive(tab == HubTab.Missions);
            }

            UpdateTabButtonStyles();
            RefreshAllViews();
            AudioManager.Instance?.PlayPowerUp();
        }

        public void SwitchTabByName(string tabName)
        {
            if (string.IsNullOrEmpty(tabName)) return;
            string lower = tabName.Trim().ToLowerInvariant();
            if (lower.Contains("mission") || lower.Contains("level") || lower.Contains("play"))
            {
                SwitchTab(HubTab.Missions);
            }
            else if (lower.Contains("shop") || lower.Contains("hangar") || lower.Contains("armory"))
            {
                SwitchTab(HubTab.Shop);
            }
            else if (lower.Contains("setting") || lower.Contains("option"))
            {
                SwitchTab(HubTab.Settings);
            }
            else if (lower.Contains("dossier") || lower.Contains("stat") || lower.Contains("profile"))
            {
                SwitchTab(HubTab.Dossier);
            }
        }

        public void SelectHangarShip(int index)
        {
            if (FleetDatabase.AllShipIds == null || FleetDatabase.AllShipIds.Length == 0) return;
            selectedHangarIndex = Mathf.Clamp(index, 0, FleetDatabase.AllShipIds.Length - 1);
            AudioManager.Instance?.PlayPowerUp();
            RefreshShopTab();
        }

        public void NextHangarShip()
        {
            if (FleetDatabase.AllShipIds == null || FleetDatabase.AllShipIds.Length == 0) return;
            selectedHangarIndex = (selectedHangarIndex + 1) % FleetDatabase.AllShipIds.Length;
            AudioManager.Instance?.PlayPowerUp();
            RefreshShopTab();
        }

        public void PrevHangarShip()
        {
            if (FleetDatabase.AllShipIds == null || FleetDatabase.AllShipIds.Length == 0) return;
            selectedHangarIndex--;
            if (selectedHangarIndex < 0) selectedHangarIndex = FleetDatabase.AllShipIds.Length - 1;
            AudioManager.Instance?.PlayPowerUp();
            RefreshShopTab();
        }

        public void EquipSelectedShip()
        {
            if (FleetDatabase.AllShipIds == null || FleetDatabase.AllShipIds.Length == 0) return;
            if (selectedHangarIndex < 0 || selectedHangarIndex >= FleetDatabase.AllShipIds.Length) return;
            string shipId = FleetDatabase.AllShipIds[selectedHangarIndex];
            if (ShipUpgradesManager.IsShipUnlocked(shipId))
            {
                ShipUpgradesManager.SetActiveShipId(shipId);
                AudioManager.Instance?.PlayPowerUp();
                RefreshAllViews();
            }
            else
            {
                var spec = FleetDatabase.GetShip(shipId);
                if (LevelProgressManager.GetCredits() >= spec.creditPrice)
                {
                    if (ShipUpgradesManager.PurchaseShip(shipId))
                    {
                        AudioManager.Instance?.PlayPowerUp();
                        RefreshAllViews();
                    }
                }
            }
        }

        public void LaunchActiveMission(int level = -1, int wave = -1)
        {
            // The Rive LaunchPressed event can arrive on several consecutive frames: load the battle scene only once.
            if (battleSceneRequested) return;
            battleSceneRequested = true;

            if (level <= 0)
            {
                level = LevelProgressManager.GetHighestUnlockedLevel();
            }
            if (wave <= 0)
            {
                wave = LevelProgressManager.GetCurrentWave(level);
            }
            AudioManager.Instance?.PlayPowerUp();
            LevelProgressManager.LoadLevelAtWave(level, wave);
        }

        // =====================================================================
        // RIVE HOME SCREEN
        //   File: home_hangar.riv | Artboard: HomeHangar | State machine: Home_SM | ViewModel: HomeVM
        //   Rive draws the whole home UI; the ship itself is rendered by Unity (ShipHolder).
        // =====================================================================
        private const string RiveViewModelName = "HomeVM";
        private const string RiveLaunchEventName = "LaunchPressed";
        private const float LaunchSequenceSeconds = 1.0f;          // length of launch_sequence
        private const float RiveArtboardAspect = 1920f / 1080f;
        private const float ShipCenterU = 0.498f;                  // Rive-space 957 / 1920
        private const float ShipCenterV = 0.593f;                  // Rive-space 640 / 1080 (from top)
        private const float ShipAreaWidthFrac = 0.365f;            // ~700 / 1920
        private const float ShipAreaHeightFrac = 0.306f;           // ~330 / 1080
        private const float ShipHoverRiveUnits = 12f;              // hover amplitude in Rive units
        private const int ShipSortingOrder = 20;                   // above the Rive canvas (sorting order 10)

        [Tooltip("Debug: also show the legacy Unity (UGUI) tab panels on top of the Rive screen.")]
        public bool showUnityPanelsOverRive = false;

        private RiveWidget riveWidget;
        private RiveViewModelInstance homeVm;
        private RiveNumberProp vmTab, vmCredits, vmLevel, vmXpPct, vmShields, vmFirepower, vmSpeed, vmAbility;
        private RiveNumberProp vmShieldsPct, vmFirepowerPct, vmSpeedPct, vmAbilityPct;
        private RiveStringProp vmShipName, vmShipClass, vmSectorTitle, vmMissionTitle;
        private RiveTriggerProp vmDeploy;

        private bool riveBound;
        private bool launchStarted;
        private bool battleSceneRequested;
        private int lastRiveTab = -1;
        private string lastPushedShipId;
        private int lastScreenW, lastScreenH;

        private Transform shipHolder;
        private SpriteRenderer shipRenderer;
        private ShipHolderHover shipHover;
        private Starfall.Player.ShipEngineThrusters shipThrusters;

        public bool IsRiveBound => riveBound;

        private IEnumerator InitRiveHome()
        {
            riveWidget = FindAnyObjectByType<RiveWidget>();
            if (riveWidget == null)
            {
                Debug.LogWarning("[HomeHubUI] No RiveWidget found in HomeScene. The procedural Home Hub UI stays active.");
                yield break;
            }

            riveWidget.Fit = global::Rive.Fit.Contain; // 16:9 artboard scaled to fit; ship position is computed from this rect
            EnsureRiveCanvasUsesCamera();

            // Auto-heal artboard name if set to legacy "HomeHangar" (the artboard in home_hangar.riv is named "home_hangar")
            if (riveWidget.Status == RiveWidgetStatus.Error || riveWidget.ArtboardName == "HomeHangar" || string.IsNullOrEmpty(riveWidget.ArtboardName))
            {
                var asset = riveWidget.Asset ?? Resources.Load<RiveAsset>("Rive/home_hangar");
                if (asset != null)
                {
                    Debug.Log("[HomeHubUI] Resolving Rive artboard to 'home_hangar'...");
                    riveWidget.Load(asset, "home_hangar", "Home_SM");
                }
            }

            // Wait for the widget to load the artboard / state machine.
            float timer = 0f;
            while (timer < 10f && !(riveWidget.Status == RiveWidgetStatus.Loaded && riveWidget.StateMachine != null))
            {
                if (riveWidget.Status == RiveWidgetStatus.Error)
                {
                    var asset = riveWidget.Asset ?? Resources.Load<RiveAsset>("Rive/home_hangar");
                    if (asset != null && riveWidget.ArtboardName != "home_hangar")
                    {
                        Debug.Log("[HomeHubUI] Retrying Rive widget load with artboard 'home_hangar'...");
                        riveWidget.Load(asset, "home_hangar", "Home_SM");
                        yield return null;
                        continue;
                    }
                    break;
                }
                timer += Time.unscaledDeltaTime;
                yield return null;
            }

            if (riveWidget.Status != RiveWidgetStatus.Loaded || riveWidget.StateMachine == null)
            {
                Debug.LogError($"[HomeHubUI] Rive widget did not load (status: {riveWidget.Status}). Check that home_hangar.riv has artboard '{riveWidget.ArtboardName}' and state machine '{riveWidget.StateMachineName}'. Procedural UI stays active.");
                yield break;
            }

            if (!BindHomeViewModel())
            {
                yield break;
            }

            riveBound = true;
            ApplyUnityOverlayMode();

            vmTab.OnValueChanged += HandleRiveTabValue;
            if (vmDeploy != null)
            {
                vmDeploy.OnTriggered += HandleDeployTrigger;
                Debug.Log("[HomeHubUI] Subscribed to HomeVM.deploy trigger.");
            }
            riveWidget.OnRiveEventReported += OnRiveEvent;

            EnsureShipHolder();
            PushRiveData();
            LayoutShipHolder();

            // Apply whichever tab the Rive file starts on.
            OnRiveTabChanged(Mathf.RoundToInt(vmTab.Value));

            Debug.Log("<color=#00ff88><b>[HomeHubUI] Rive home screen bound: HomeVM instance created and data pushed.</b></color>");
        }

        /// <summary>Creates the HomeVM instance, binds it to the state machine and caches every property by name.</summary>
        private bool BindHomeViewModel()
        {
            var file = riveWidget.File;
            var viewModel = file != null ? file.GetViewModelByName(RiveViewModelName) : null;
            if (viewModel == null)
            {
                Debug.LogError($"[HomeHubUI] ViewModel '{RiveViewModelName}' was not found in home_hangar.riv. Procedural UI stays active.");
                return false;
            }

            homeVm = viewModel.CreateDefaultInstance() ?? viewModel.CreateInstance();
            if (homeVm == null)
            {
                Debug.LogError($"[HomeHubUI] Could not create an instance of ViewModel '{RiveViewModelName}'.");
                return false;
            }

            riveWidget.StateMachine.BindViewModelInstance(homeVm);

            var missing = new List<string>();
            RiveNumberProp Num(string n) { var p = homeVm.GetNumberProperty(n); if (p == null) missing.Add($"{n} (number)"); return p; }
            RiveStringProp Str(string n) { var p = homeVm.GetStringProperty(n); if (p == null) missing.Add($"{n} (string)"); return p; }
            RiveTriggerProp Trig(string n) { var p = homeVm.GetTriggerProperty(n); if (p == null) missing.Add($"{n} (trigger)"); return p; }

            vmTab = Num("tab");
            vmCredits = Num("credits");
            vmLevel = Num("level");
            vmXpPct = Num("xpPct");
            vmShields = Num("shields");
            vmFirepower = Num("firepower");
            vmSpeed = Num("speed");
            vmAbility = Num("ability");
            vmShieldsPct = Num("shieldsPct");
            vmFirepowerPct = Num("firepowerPct");
            vmSpeedPct = Num("speedPct");
            vmAbilityPct = Num("abilityPct");
            Num("alertLevel"); // present in the file, intentionally not driven yet
            vmShipName = Str("shipName");
            vmShipClass = Str("shipClass");
            vmSectorTitle = Str("sectorTitle");
            vmMissionTitle = Str("missionTitle");
            vmDeploy = Trig("deploy");

            if (missing.Count > 0)
            {
                var available = new List<string>();
                foreach (var p in viewModel.Properties) available.Add($"{p.Name} ({p.Type})");
                Debug.LogError($"[HomeHubUI] HomeVM is missing {missing.Count} expected propert{(missing.Count == 1 ? "y" : "ies")} (missing or wrong type): {string.Join(", ", missing)}.\nProperties found in the file: {string.Join(", ", available)}");
            }

            // The tab property is required to drive the panels; without it we cannot treat Rive as the UI.
            if (vmTab == null)
            {
                Debug.LogError("[HomeHubUI] HomeVM.tab not found - Rive tab sync disabled and procedural UI stays active.");
                return false;
            }
            return true;
        }

        /// <summary>Keeps the Rive canvas on a camera so the world-space ship + flame can be sorted above it.</summary>
        private void EnsureRiveCanvasUsesCamera()
        {
            var canvas = riveWidget.GetComponentInParent<Canvas>();
            var cam = Camera.main;
            if (canvas == null || cam == null) return;
            if (canvas.renderMode == RenderMode.ScreenSpaceOverlay)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
            }
            if (canvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                canvas.worldCamera = cam;
                canvas.planeDistance = 10f;
                canvas.sortingOrder = 10;
            }
        }

        private void ApplyUnityOverlayMode()
        {
            var canvas = GetComponentInChildren<Canvas>(true);
            if (canvas == null) return;

            // Keep the canvas active so child panels (Shop, Dossier, Settings) can be displayed when their tabs are chosen.
            canvas.gameObject.SetActive(true);

            // Hide legacy procedural chrome when Rive is bound, unless showUnityPanelsOverRive is true.
            foreach (var chrome in new[] { "Background", "HeaderBar", "NavBar" })
            {
                var t = canvas.transform.Find(chrome);
                if (t != null) t.gameObject.SetActive(!riveBound || showUnityPanelsOverRive);
            }

            // Sync the active panel state with the current tab
            SwitchTab(currentTab);
        }

        private void UpdateRiveHome()
        {
            if (!riveBound || launchStarted) return;

            // Fallback in case a value-changed callback is missed: react only when the value changes.
            if (vmTab != null) OnRiveTabChanged(Mathf.RoundToInt(vmTab.Value));

            // Fallback event polling: if an event occurred this frame, capture it even if delegate was skipped
            if (!launchStarted && riveWidget != null && riveWidget.StateMachine != null)
            {
                foreach (var evt in riveWidget.StateMachine.EnumerateReportedEvents())
                {
                    if (evt != null)
                    {
                        Debug.Log($"<color=yellow>[HomeHubUI] Polled Rive reported event: '{evt.Name}'</color>");
                        string lower = evt.Name.Trim().ToLowerInvariant();
                        if (lower == "launchpressed" || lower.Contains("launch") || lower.Contains("deploy") || lower.Contains("battle") || lower.Contains("play"))
                        {
                            BeginLaunch(false);
                            break;
                        }
                    }
                }
            }

            if (Screen.width != lastScreenW || Screen.height != lastScreenH) LayoutShipHolder();
        }

        private void HandleRiveTabValue(float value) => OnRiveTabChanged(Mathf.RoundToInt(value));

        private void HandleDeployTrigger()
        {
            Debug.Log("<color=yellow>[HomeHubUI] HomeVM.deploy trigger received from Rive graphic!</color>");
            BeginLaunch(false);
        }

        /// <summary>HomeVM.tab: 0 Missions, 1 Fleet Hangar, 2 Armory, 3 Dossier. Rive moves its own highlight line.</summary>
        private void OnRiveTabChanged(int tab)
        {
            if (tab == lastRiveTab || launchStarted) return;
            lastRiveTab = tab;

            switch (tab)
            {
                case 0: SwitchTab(HubTab.Missions); break;
                case 1: SwitchTab(HubTab.Shop); break;     // Fleet Hangar (ship showcase lives in the Shop panel)
                case 2: SwitchTab(HubTab.Shop); break;     // Armory (tech upgrades live in the Shop panel)
                case 3: SwitchTab(HubTab.Dossier); break;
                default: Debug.LogWarning($"[HomeHubUI] Unknown HomeVM.tab value {tab}."); break;
            }
        }

        private void OnRiveEvent(RiveReportedEvent evt)
        {
            if (evt == null || launchStarted) return;
            Debug.Log($"<color=yellow>[HomeHubUI] Rive event reported: '{evt.Name}'</color>");
            string lower = evt.Name.Trim().ToLowerInvariant();
            if (lower == "launchpressed" || lower.Contains("launch") || lower.Contains("deploy") || lower.Contains("battle") || lower.Contains("play"))
            {
                BeginLaunch(false); // Rive already started launch_sequence itself when the player tapped the button
            }
        }

        /// <summary>Launch from code / a Unity button: fires the Rive 'deploy' trigger, then loads the battle scene.</summary>
        public void RequestLaunch()
        {
            BeginLaunch(true);
        }

        private void BeginLaunch(bool fireDeployTrigger)
        {
            if (launchStarted) return; // LaunchPressed can fire on consecutive frames - handle it once only
            launchStarted = true;

            // Ensure ship is visible for launch animation
            if (shipHolder != null) shipHolder.gameObject.SetActive(true);

            if (fireDeployTrigger && riveBound && vmDeploy != null) vmDeploy.Trigger();

            // launch_sequence hides the Rive panels; keep the Unity ones hidden too and never re-enable them.
            SetProceduralUIVisible(false);
            StartCoroutine(LaunchRoutine());
        }

        private IEnumerator LaunchRoutine()
        {
            AudioManager.Instance?.PlayPowerUp();
            float wait = riveBound ? LaunchSequenceSeconds : 0f;
            if (riveBound) StartCoroutine(ShipLaunchEffect(wait));
            if (wait > 0f) yield return new WaitForSecondsRealtime(wait);
            LaunchActiveMission();
        }

        private IEnumerator ShipLaunchEffect(float duration)
        {
            var cam = Camera.main;
            if (shipHolder == null || cam == null) yield break;
            if (shipHover != null) shipHover.enabled = false;

            Vector3 from = shipHolder.position;
            float rise = cam.orthographicSize * 2.4f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.unscaledDeltaTime;
                float k = Mathf.Clamp01(t / duration);
                shipHolder.position = from + Vector3.up * (k * k * rise); // accelerate off the pad
                yield return null;
            }
        }

        // ---------------------------------------------------------------------
        // Game data -> HomeVM
        // ---------------------------------------------------------------------
        private void PushRiveData()
        {
            if (!riveBound || launchStarted) return;
            PushShipToRive();
            PushPlayerToRive();
            PushMissionToRive();
        }

        private void PushShipToRive()
        {
            string shipId = ShipUpgradesManager.GetActiveShipId();
            var spec = FleetDatabase.GetShip(shipId);
            if (spec == null) return;

            string shipName = string.IsNullOrWhiteSpace(spec.name) ? "WARSHIP" : spec.name.Trim();
            string shipClass = string.IsNullOrWhiteSpace(spec.role) ? "COMBAT CRAFT" : spec.role.Trim().ToUpperInvariant();

            SetRiveString(vmShipName, shipName);
            SetRiveString(vmShipClass, shipClass);

            // 1-10 combat ratings (the same values the Unity stat bars show).
            float maxStrength = 0f, maxDamage = 0f, maxFlex = 0f, maxAbility = 0f;
            foreach (var id in FleetDatabase.AllShipIds)
            {
                var s = FleetDatabase.GetShip(id);
                maxStrength = Mathf.Max(maxStrength, s.baseStrength);
                maxDamage = Mathf.Max(maxDamage, s.baseDamagePower);
                maxFlex = Mathf.Max(maxFlex, s.baseFlexibility);
                maxAbility = Mathf.Max(maxAbility, s.baseAbilityPower);
            }

            SetRiveNumber(vmShields, spec.baseStrength);
            SetRiveNumber(vmFirepower, spec.baseDamagePower);
            SetRiveNumber(vmSpeed, spec.baseFlexibility);
            SetRiveNumber(vmAbility, spec.baseAbilityPower);
            SetRiveNumber(vmShieldsPct, StatPercent(spec.baseStrength, maxStrength));
            SetRiveNumber(vmFirepowerPct, StatPercent(spec.baseDamagePower, maxDamage));
            SetRiveNumber(vmSpeedPct, StatPercent(spec.baseFlexibility, maxFlex));
            SetRiveNumber(vmAbilityPct, StatPercent(spec.baseAbilityPower, maxAbility));

            if (shipId != lastPushedShipId)
            {
                lastPushedShipId = shipId;
                ShowShipOnPad(spec);
            }
        }

        private void PushPlayerToRive()
        {
            SetRiveNumber(vmCredits, LevelProgressManager.GetCredits());

            GetPilotProgress(out int pilotLevel, out int xpPct);
            SetRiveNumber(vmLevel, pilotLevel);
            SetRiveNumber(vmXpPct, xpPct);
        }

        private void PushMissionToRive()
        {
            int sector = LevelProgressManager.GetHighestUnlockedLevel();
            int wave = LevelProgressManager.GetCurrentWave(sector);

            SetRiveString(vmSectorTitle, GetSectorTitle(sector));

            string mission = null;
            var objectives = MissionObjectiveManager.GenerateObjectives(sector);
            if (objectives != null && objectives.Count > 0 && !string.IsNullOrEmpty(objectives[0].title))
            {
                mission = objectives[0].title.ToUpperInvariant();
            }
            SetRiveString(vmMissionTitle, mission ?? $"WAVE {wave} OF 10");
        }

        private static float StatPercent(float value, float max)
        {
            if (max <= 0f) return 0f;
            return Mathf.Clamp(value / max * 100f, 0f, 100f);
        }

        private static void SetRiveNumber(RiveNumberProp prop, float value)
        {
            if (prop != null) prop.Value = value;
        }

        // Never write empty strings: an empty string makes the Rive text show its "Initial value".
        private static void SetRiveString(RiveStringProp prop, string value)
        {
            if (prop != null && !string.IsNullOrWhiteSpace(value)) prop.Value = value;
        }

        private static string GetSectorTitle(int level)
        {
            return level switch
            {
                1 => "SECTOR 01 • EARTH ORBIT",
                2 => "SECTOR 02 • ASTEROID BELT",
                3 => "SECTOR 03 • DEEP NEBULA",
                4 => "SECTOR 04 • WAR-TORN WRECKAGE",
                _ => "SECTOR 01 • EARTH ORBIT"
            };
        }

        /// <summary>
        /// The game has no pilot XP system, so pilot level is derived from campaign progress:
        /// 1 level per 4 cleared waves (boss waves of finished sectors count), xpPct = progress to the next level.
        /// </summary>
        private static void GetPilotProgress(out int level, out int xpPct)
        {
            int unlocked = LevelProgressManager.GetHighestUnlockedLevel();
            int cleared = 0;
            for (int l = 1; l <= unlocked; l++)
            {
                cleared += Mathf.Max(0, LevelProgressManager.GetHighestWave(l) - 1);
                if (l < unlocked) cleared += 1;
            }

            const int wavesPerLevel = 4;
            level = 1 + cleared / wavesPerLevel;
            xpPct = Mathf.Clamp(Mathf.RoundToInt((cleared % wavesPerLevel) / (float)wavesPerLevel * 100f), 0, 100);
        }

        // ---------------------------------------------------------------------
        // Ship display (Unity, drawn above the Rive canvas)
        // ---------------------------------------------------------------------
        private void EnsureShipHolder()
        {
            if (shipHolder != null) return;

            var holderGo = new GameObject("ShipHolder");
            shipHolder = holderGo.transform;
            shipHover = holderGo.AddComponent<ShipHolderHover>();

            var visualGo = new GameObject("ShipVisual");
            visualGo.transform.SetParent(shipHolder, false);
            shipRenderer = visualGo.AddComponent<SpriteRenderer>();
            shipRenderer.sortingOrder = ShipSortingOrder;

            // Existing tinted particle-system engine flame (children are created under ShipVisual).
            shipThrusters = visualGo.AddComponent<Starfall.Player.ShipEngineThrusters>();

            // Only show on Missions tab if rive is bound
            if (riveBound)
            {
                shipHolder.gameObject.SetActive(currentTab == HubTab.Missions);
            }
        }

        private void ShowShipOnPad(WarshipSpec spec)
        {
            EnsureShipHolder();
            shipRenderer.sprite = WarshipSpriteUtility.GetShipSprite(spec);
            LayoutShipHolder();

            if (shipThrusters != null) shipThrusters.ApplyShipColorIdentity(); // re-tint + re-position the flame for this hull
            StartCoroutine(MakeFlameFollowShipScale());
        }

        // The thruster particles are authored for the gameplay ship size; scale them with the (larger) pad ship.
        private IEnumerator MakeFlameFollowShipScale()
        {
            yield return null;
            yield return null;
            if (shipRenderer == null) yield break;
            foreach (var ps in shipRenderer.GetComponentsInChildren<ParticleSystem>(true))
            {
                var main = ps.main;
                main.scalingMode = ParticleSystemScalingMode.Hierarchy;
            }
        }

        /// <summary>
        /// Places ShipHolder at 49.8% / 59.3% of the Rive artboard and fits the ship into the
        /// ~36.5% x 30.6% dock-pad area. The artboard rect is derived from Fit.Contain (16:9, centered),
        /// so it is correct at any resolution / aspect ratio.
        /// </summary>
        private void LayoutShipHolder()
        {
            lastScreenW = Screen.width;
            lastScreenH = Screen.height;

            var cam = Camera.main;
            if (cam == null || shipHolder == null) return;

            float aspect = cam.aspect;
            float widthFrac, heightFrac; // artboard size as a fraction of the viewport
            if (aspect >= RiveArtboardAspect) { heightFrac = 1f; widthFrac = RiveArtboardAspect / aspect; }
            else { widthFrac = 1f; heightFrac = aspect / RiveArtboardAspect; }
            float x0 = (1f - widthFrac) * 0.5f;
            float y0 = (1f - heightFrac) * 0.5f;

            Vector3 viewportPoint = new Vector3(
                x0 + ShipCenterU * widthFrac,
                y0 + (1f - ShipCenterV) * heightFrac,
                Mathf.Abs(cam.transform.position.z));
            Vector3 world = cam.ViewportToWorldPoint(viewportPoint);
            world.z = 0f;

            float worldHeight = cam.orthographicSize * 2f;
            float worldWidth = worldHeight * aspect;
            float areaW = ShipAreaWidthFrac * widthFrac * worldWidth;
            float areaH = ShipAreaHeightFrac * heightFrac * worldHeight;

            if (shipRenderer != null && shipRenderer.sprite != null)
            {
                Vector2 size = shipRenderer.sprite.bounds.size;
                if (size.x > 0.0001f && size.y > 0.0001f)
                {
                    float scale = Mathf.Min(areaW / size.x, areaH / size.y);
                    shipRenderer.transform.localScale = Vector3.one * scale;
                }
            }

            if (!launchStarted)
            {
                shipHolder.position = world;
                if (shipHover != null)
                {
                    shipHover.Rebase();
                    shipHover.Rescale(ShipHoverRiveUnits / 1080f * heightFrac * worldHeight);
                }
            }
        }

        private void ShutdownRiveHome()
        {
            try
            {
                if (vmTab != null) vmTab.OnValueChanged -= HandleRiveTabValue;
                if (vmDeploy != null) vmDeploy.OnTriggered -= HandleDeployTrigger;
                if (riveWidget != null) riveWidget.OnRiveEventReported -= OnRiveEvent;
                homeVm?.Dispose();
            }
            catch (System.Exception)
            {
                // The widget may already have torn down the native state machine during scene unload.
            }
            homeVm = null;
            riveBound = false;
        }

        public void SetProceduralUIVisible(bool visible)
        {
            var canvas = GetComponentInChildren<Canvas>(true);
            if (canvas != null)
            {
                canvas.gameObject.SetActive(visible);
            }
        }

        public void SetBackgroundVisible(bool visible)
        {
            if (backgroundGo != null)
            {
                backgroundGo.SetActive(visible);
            }
        }

        private void UpdateTabButtonStyles()
        {
            if (tabMissionsBg != null) { tabMissionsBg.sprite = currentTab == HubTab.Missions ? SciFiUITheme.ButtonPrimary : SciFiUITheme.ButtonNormal; tabMissionsBg.type = Image.Type.Sliced; tabMissionsBg.color = Color.white; }
            if (tabShopBg != null) { tabShopBg.sprite = currentTab == HubTab.Shop ? SciFiUITheme.ButtonPrimary : SciFiUITheme.ButtonNormal; tabShopBg.type = Image.Type.Sliced; tabShopBg.color = Color.white; }
            if (tabSettingsBg != null) { tabSettingsBg.sprite = currentTab == HubTab.Settings ? SciFiUITheme.ButtonPrimary : SciFiUITheme.ButtonNormal; tabSettingsBg.type = Image.Type.Sliced; tabSettingsBg.color = Color.white; }
            if (tabDossierBg != null) { tabDossierBg.sprite = currentTab == HubTab.Dossier ? SciFiUITheme.ButtonPrimary : SciFiUITheme.ButtonNormal; tabDossierBg.type = Image.Type.Sliced; tabDossierBg.color = Color.white; }
        }

        public void RefreshAllViews()
        {
            // Refresh Header Credits
            int credits = LevelProgressManager.GetCredits();
            if (creditsText != null)
            {
                creditsText.text = $"* <b>{credits:N0}</b> CREDITS";

            }

            int unlocked = LevelProgressManager.GetHighestUnlockedLevel();
            if (callsignText != null)
            {
                callsignText.text = $"PILOT: <color=#00e5ff>COMMANDER VIPER-1</color>  |  SECTOR: <color=#ffd700>0{unlocked}/04</color>";
            }

            // Mirror the latest game data into the Rive HomeVM (no-op until bound)
            PushRiveData();

            // Refresh Active Tab
            switch (currentTab)
            {
                case HubTab.Missions:
                    RefreshMissionsTab();
                    break;
                case HubTab.Shop:
                    RefreshShopTab();
                    break;
                case HubTab.Settings:
                    RefreshSettingsTab();
                    break;
                case HubTab.Dossier:
                    RefreshDossierTab();
                    break;
            }
        }

        // ==================================================================
        // TAB 1: MISSIONS (SECTOR & WAVE SELECTION)
        // ==================================================================
        private void RefreshMissionsTab()
        {
            for (int i = 0; i < sectorCards.Length; i++)
            {
                var card = sectorCards[i];
                if (card == null) continue;

                int lvl = card.levelNumber;
                bool isUnlocked = LevelProgressManager.IsLevelUnlocked(lvl);
                int highestWave = LevelProgressManager.GetHighestWave(lvl);
                int currentWave = LevelProgressManager.GetCurrentWave(lvl);
                int highScore = LevelProgressManager.GetHighScore(lvl);

                if (isUnlocked)
                {
                    card.cardBackground.color = new Color(0.08f, 0.12f, 0.22f, 0.95f);
                    card.statusText.text = $"STATUS: <color=#00ff88>OPERATIONAL</color>   BEST: <color=#00e5ff>{(highScore > 0 ? highScore.ToString("N0") : "---")}</color>";

                    var objList = Starfall.Levels.MissionObjectiveManager.GenerateObjectives(lvl);
                    int completedCount = 0;
                    for (int o = 0; o < objList.Count; o++)
                    {
                        if (Starfall.Levels.MissionObjectiveManager.IsObjectiveCompleted(lvl, objList[o].id))
                            completedCount++;
                    }
                    string objTag = completedCount >= 3 ? "<color=#00ff88>3/3 [MAX]</color>" : $"<color=#00e5ff>{completedCount}/3</color>";
                    card.progressText.text = $"PROGRESS: <color=#ffd700>WAVE {currentWave}/10</color>   OBJECTIVES: {objTag}";

                    card.deployButton.interactable = true;
                    card.deployButton.onClick.RemoveAllListeners();
                    int resumeWave = currentWave;
                    card.deployButton.onClick.AddListener(() =>
                    {
                        LevelProgressManager.LoadLevelAtWave(lvl, resumeWave);
                    });

                    if (card.deployButtonText != null)
                    {
                        card.deployButtonText.text = currentWave > 1 ? $"CONTINUE\nWAVE {currentWave}" : "DEPLOY\nWAVE 1";
                    }

                    if (card.waveMatrixContainer != null) card.waveMatrixContainer.SetActive(true);

                    for (int w = 0; w < 10; w++)
                    {
                        int waveNum = w + 1;
                        var btn = card.waveButtons[w];
                        var btnTxt = card.waveButtonTexts[w];
                        var btnImg = card.waveButtonImages[w];
                        if (btn == null) continue;

                        btn.onClick.RemoveAllListeners();

                        if (waveNum <= highestWave)
                        {
                            btn.interactable = true;
                            btn.onClick.AddListener(() =>
                            {
                                LevelProgressManager.LoadLevelAtWave(lvl, waveNum);
                            });

                            if (waveNum == currentWave)
                            {
                                btnImg.color = new Color(1f, 0.75f, 0.1f, 1f); // Gold
                                btnTxt.color = Color.black;
                                btnTxt.text = $"<b>{waveNum}</b>";
                            }
                            else
                            {
                                btnImg.color = new Color(0.1f, 0.6f, 0.35f, 0.9f); // Emerald Green
                                btnTxt.color = Color.white;
                                btnTxt.text = $"{waveNum}";
                            }
                        }
                        else
                        {
                            btn.interactable = false;
                            btnImg.color = new Color(0.12f, 0.15f, 0.22f, 0.6f);
                            btnTxt.color = new Color(0.35f, 0.4f, 0.5f, 0.8f);
                            btnTxt.text = "-";
                        }
                    }
                }
                else
                {
                    card.cardBackground.color = new Color(0.05f, 0.06f, 0.09f, 0.85f);
                    string reqSector = $"Sector {lvl - 1}";
                    card.statusText.text = $"STATUS: <color=#ff4444>LOCKED (Defeat Boss in {reqSector})</color>";
                    card.progressText.text = "SECTOR UNDISCOVERED";

                    card.deployButton.interactable = false;
                    card.deployButton.onClick.RemoveAllListeners();
                    if (card.deployButtonText != null) card.deployButtonText.text = "LOCKED";

                    for (int w = 0; w < 10; w++)
                    {
                        if (card.waveButtons[w] != null) card.waveButtons[w].interactable = false;
                        if (card.waveButtonImages[w] != null) card.waveButtonImages[w].color = new Color(0.1f, 0.12f, 0.16f, 0.5f);
                        if (card.waveButtonTexts[w] != null)
                        {
                            card.waveButtonTexts[w].color = new Color(0.3f, 0.35f, 0.4f, 0.6f);
                            card.waveButtonTexts[w].text = "-";
                        }
                    }
                }
            }
        }

        // ==================================================================
        // TAB 2: ARMORY SHOP (HANGAR FLEET & UPGRADES)
        // ==================================================================
        private void RefreshShopTab()
        {
            int currentCredits = LevelProgressManager.GetCredits();
            string activeShipId = ShipUpgradesManager.GetActiveShipId();

            if (selectedHangarIndex < 0 || selectedHangarIndex >= FleetDatabase.AllShipIds.Length)
            {
                selectedHangarIndex = 0;
            }

            string selectedShipId = FleetDatabase.AllShipIds[selectedHangarIndex];
            WarshipSpec spec = FleetDatabase.GetShip(selectedShipId);
            bool isUnlocked = ShipUpgradesManager.IsShipUnlocked(selectedShipId);
            bool isActive = (selectedShipId == activeShipId);

            // 1. Update Fleet Carousel / Selection Buttons across top
            for (int i = 0; i < FleetDatabase.AllShipIds.Length; i++)
            {
                string sId = FleetDatabase.AllShipIds[i];
                var sSpec = FleetDatabase.GetShip(sId);
                bool sUnlocked = ShipUpgradesManager.IsShipUnlocked(sId);
                bool sActive = (sId == activeShipId);
                bool sSelected = (i == selectedHangarIndex);

                if (shipSelectorTexts[i] != null)
                {
                    string statusTag;
                    if (sActive) statusTag = "<color=#00ff88>[ACTIVE]</color>";
                    else if (sUnlocked) statusTag = "<color=#00e5ff>[OWNED]</color>";
                    else statusTag = $"<color=#ffd700>[{sSpec.creditPrice:N0} CR]</color>";

                    shipSelectorTexts[i].text = $"<size=12>{sSpec.name}</size>\n<size=11>{statusTag}</size>";
                }

                if (shipSelectorBgs[i] != null)
                {
                    if (sSelected)
                        shipSelectorBgs[i].color = new Color(0.12f, 0.45f, 0.85f, 1f); // Bright blue selected
                    else if (sActive)
                        shipSelectorBgs[i].color = new Color(0.08f, 0.32f, 0.20f, 0.9f); // Green for active
                    else if (sUnlocked)
                        shipSelectorBgs[i].color = new Color(0.10f, 0.16f, 0.28f, 0.85f); // Slate for owned
                    else
                        shipSelectorBgs[i].color = new Color(0.07f, 0.09f, 0.15f, 0.65f); // Dark for locked
                }
            }

            // 2. Update Left Showcase Panel
            if (showcaseTitleText != null)
            {
                string hexColor = ColorUtility.ToHtmlStringRGB(spec.hullColor);
                showcaseTitleText.text = $"<color=#{hexColor}><b>{spec.name}</b></color>";
            }

            if (showcaseRoleText != null)
            {
                showcaseRoleText.text = $"<color=#88bbdd>{spec.role.ToUpper()}</color>";
            }

            if (showcaseShipImage != null)
            {
                Sprite s = WarshipSpriteUtility.GetShipSprite(spec);
                if (s != null)
                {
                    showcaseShipImage.sprite = s;
                    showcaseShipImage.color = Color.white;
                }
            }

            if (showcaseGlowImage != null)
            {
                Color glow = spec.hullColor;
                glow.a = 0.22f;
                showcaseGlowImage.color = glow;
            }

            int hardpointCount = spec.hardpointOffsets != null ? spec.hardpointOffsets.Length : 2;
            string hardpointDetails = hardpointCount switch
            {
                2 => "• 2x Dual Wing Plasma Blasters (Balanced Convergence)",
                3 => "• 1x Heavy Center Cannon + 2x Outboard Vortex Pods",
                4 => "• 2x Solar Beam Emitters + 2x Wing Photon Blasters",
                _ => $"• {hardpointCount}x Synchronized Weapon Hardpoints"
            };

            if (showcaseHardpointText != null)
            {
                showcaseHardpointText.text =
                    $"<color=#ffd700><b>DEFAULT HARDPOINTS ({hardpointCount} FIRING CANNONS):</b></color>\n" +
                    $"<size=13><color=#aaccdd>{hardpointDetails}</color></size>";
            }

            if (showcaseAbilityText != null)
            {
                showcaseAbilityText.text =
                    $"<color=#00e5ff><b>ACTIVE ABILITY: {spec.abilityName}</b></color> <color=#ffd700>[KEY: E / HUD ICON]</color>\n" +
                    $"<size=13><color=#88bbcc>{spec.abilityDescription}</color></size>\n" +
                    $"<size=12><color=#6699aa>Recharge Cooldown: {spec.abilityCooldown:F0}s • Duration: {spec.abilityDuration:F1}s</color></size>";
            }

            if (showcaseStatusText != null)
            {
                if (isActive)
                    showcaseStatusText.text = "STATUS: <color=#00ff88>DEPLOYED & COMBAT READY</color>";
                else if (isUnlocked)
                    showcaseStatusText.text = "STATUS: <color=#00e5ff>COMMISSIONED (STANDBY IN HANGAR)</color>";
                else
                    showcaseStatusText.text = $"STATUS: <color=#ffaa00>UNCOMMISSIONED (REQUIRES {spec.creditPrice:N0} CR)</color>";
            }

            // Action Button (Equip / Commission / Active)
            if (showcaseActionBtn != null)
            {
                showcaseActionBtn.onClick.RemoveAllListeners();

                if (isActive)
                {
                    showcaseActionBtn.interactable = false;
                    if (showcaseActionBtnText != null) showcaseActionBtnText.text = "CURRENT WARSHIP (ACTIVE)";
                    if (showcaseActionBtnBg != null) showcaseActionBtnBg.color = new Color(0.12f, 0.45f, 0.25f, 0.75f);
                }
                else if (isUnlocked)
                {
                    showcaseActionBtn.interactable = true;
                    if (showcaseActionBtnText != null) showcaseActionBtnText.text = "EQUIP THIS WARSHIP";
                    if (showcaseActionBtnBg != null) showcaseActionBtnBg.color = new Color(0f, 0.65f, 0.95f, 1f);
                    showcaseActionBtn.onClick.AddListener(() =>
                    {
                        ShipUpgradesManager.SetActiveShipId(selectedShipId);
                        AudioManager.Instance?.PlayPowerUp();
                        RefreshAllViews();
                    });
                }
                else
                {
                    bool canAfford = currentCredits >= spec.creditPrice;
                    showcaseActionBtn.interactable = canAfford;
                    if (showcaseActionBtnText != null) showcaseActionBtnText.text = $"COMMISSION ({spec.creditPrice:N0} CR)";
                    if (showcaseActionBtnBg != null) showcaseActionBtnBg.color = canAfford ? new Color(1.0f, 0.6f, 0.0f, 1f) : new Color(0.3f, 0.35f, 0.4f, 0.6f);
                    showcaseActionBtn.onClick.AddListener(() =>
                    {
                        if (ShipUpgradesManager.PurchaseShip(selectedShipId))
                        {
                            AudioManager.Instance?.PlayPowerUp();
                            RefreshAllViews();
                        }
                    });
                }
            }

            // 3. Update Stat Rating Bars
            UpdateStatBar(statStrengthText, "STRENGTH", spec.baseStrength, 10, "#ff4444");
            UpdateStatBar(statDamageText, "DAMAGE POWER", spec.baseDamagePower, 10, "#ffaa00");
            UpdateStatBar(statFlexibilityText, "FLEXIBILITY", spec.baseFlexibility, 10, "#00ff88");
            UpdateStatBar(statAbilityText, "SPECIAL ABILITY", spec.baseAbilityPower, 10, "#00e5ff");

            // 4. Update 4 Per-Ship Upgrade Cards
            for (int i = 0; i < shipTechCards.Length; i++)
            {
                var card = shipTechCards[i];
                if (card == null) continue;

                int curTier = ShipUpgradesManager.GetShipUpgradeLevel(selectedShipId, card.upgradeKey);
                int maxTier = ShipUpgradesManager.MAX_SHIP_UPGRADE_TIER;
                int cost = ShipUpgradesManager.GetShipUpgradeCost(selectedShipId, card.upgradeKey);

                string pips = "";
                for (int t = 0; t < maxTier; t++)
                {
                    pips += (t < curTier) ? "<color=#00e5ff>[#]</color> " : "<color=#445566>[ ]</color> ";
                }

                if (curTier >= maxTier)
                {
                    card.levelText.text = $"<color=#ffd700>{pips}</color> <color=#00ff88>MAXED (TIER {curTier}/{maxTier})</color>";
                    card.costText.text = "<color=#ffd700>MAX TIER REACHED</color>";
                    card.buyButton.interactable = false;
                    card.buyButtonText.text = "MAXED";
                    card.buyButtonImage.color = new Color(0.2f, 0.25f, 0.32f, 0.7f);
                }
                else
                {
                    bool canAfford = isUnlocked && (currentCredits >= cost);
                    card.levelText.text = $"<color=#00e5ff>{pips}</color> TIER {curTier}/{maxTier}";
                    card.costText.text = $"PRICE: <color={(canAfford ? "#ffd700" : "#ff4444")}>{cost:N0} CR</color>";

                    card.buyButton.interactable = canAfford;
                    card.buyButtonText.text = isUnlocked ? "UPGRADE" : "LOCKED";
                    card.buyButtonImage.color = canAfford ? new Color(0f, 0.7f, 0.35f, 1f) : new Color(0.3f, 0.35f, 0.4f, 0.6f);

                    string uKey = card.upgradeKey;
                    card.buyButton.onClick.RemoveAllListeners();
                    card.buyButton.onClick.AddListener(() =>
                    {
                        if (ShipUpgradesManager.PurchaseShipUpgrade(selectedShipId, uKey))
                        {
                            AudioManager.Instance?.PlayPowerUp();
                            RefreshAllViews();
                        }
                    });
                }
            }
        }

        private void UpdateStatBar(TextMeshProUGUI tmp, string statName, int val, int maxVal, string colorHex)
        {
            if (tmp == null) return;
            string pips = "";
            for (int i = 0; i < maxVal; i++)
            {
                pips += (i < val) ? $"<color={colorHex}>|</color> " : "<color=#253548>:</color> ";
            }
            tmp.text = $"<b><color=#d8e4f0>{statName}</color></b>   {pips}   <color=#ffd700><b>{val * 10}%</b></color>";
        }

        // ==================================================================
        // TAB 3: SETTINGS
        // ==================================================================
        private void RefreshSettingsTab()
        {
            if (AudioManager.Instance != null)
            {
                if (masterSlider != null) masterSlider.SetValueWithoutNotify(AudioManager.Instance.MasterVolume);
                if (masterValText != null) masterValText.text = $"{Mathf.RoundToInt(AudioManager.Instance.MasterVolume * 100)}%";

                if (musicSlider != null) musicSlider.SetValueWithoutNotify(AudioManager.Instance.MusicVolume);
                if (musicValText != null) musicValText.text = $"{Mathf.RoundToInt(AudioManager.Instance.MusicVolume * 100)}%";

                if (sfxSlider != null) sfxSlider.SetValueWithoutNotify(AudioManager.Instance.SFXVolume);
                if (sfxValText != null) sfxValText.text = $"{Mathf.RoundToInt(AudioManager.Instance.SFXVolume * 100)}%";
            }

            bool shake = CameraShake.IsShakeEnabled;
            if (shakeToggleText != null) shakeToggleText.text = shake ? "SCREEN SHAKE: <color=#00ff88>ON</color>" : "SCREEN SHAKE: <color=#ff4444>OFF</color>";
            if (shakeToggleImg != null) shakeToggleImg.color = shake ? new Color(0.1f, 0.45f, 0.25f, 0.9f) : new Color(0.45f, 0.15f, 0.15f, 0.9f);

            bool isFull = Screen.fullScreen;
            if (fullscreenToggleText != null) fullscreenToggleText.text = isFull ? "DISPLAY: <color=#00e5ff>FULLSCREEN</color>" : "DISPLAY: <color=#ffaa00>WINDOWED</color>";
            if (fullscreenToggleImg != null) fullscreenToggleImg.color = isFull ? new Color(0.1f, 0.35f, 0.5f, 0.9f) : new Color(0.35f, 0.3f, 0.15f, 0.9f);
        }

        // ==================================================================
        // TAB 4: PILOT DOSSIER
        // ==================================================================
        private void RefreshDossierTab()
        {
            if (dossierStatsText == null) return;

            int unlocked = LevelProgressManager.GetHighestUnlockedLevel();
            int score1 = LevelProgressManager.GetHighScore(1);
            int score2 = LevelProgressManager.GetHighScore(2);
            int score3 = LevelProgressManager.GetHighScore(3);
            int score4 = LevelProgressManager.GetHighScore(4);
            int totalScore = score1 + score2 + score3 + score4;
            int totalCredits = LevelProgressManager.GetCredits();

            int clearedWaves1 = LevelProgressManager.GetHighestWave(1);
            int clearedWaves2 = LevelProgressManager.GetHighestWave(2);
            int clearedWaves3 = LevelProgressManager.GetHighestWave(3);
            int clearedWaves4 = LevelProgressManager.GetHighestWave(4);
            int totalCleared = clearedWaves1 + clearedWaves2 + clearedWaves3 + clearedWaves4;
            float progressPct = (totalCleared / 40f) * 100f;

            var activeSpec = ShipUpgradesManager.GetActiveShipSpec();
            string sId = activeSpec.id;
            int hullTier = ShipUpgradesManager.GetShipUpgradeLevel(sId, ShipUpgradesManager.UPGRADE_HULL);
            int wepTier = ShipUpgradesManager.GetShipUpgradeLevel(sId, ShipUpgradesManager.UPGRADE_WEAPONS);
            int spdTier = ShipUpgradesManager.GetShipUpgradeLevel(sId, ShipUpgradesManager.UPGRADE_SPEED);
            int abTier = ShipUpgradesManager.GetShipUpgradeLevel(sId, ShipUpgradesManager.UPGRADE_ABILITY);

            int effLives = ShipUpgradesManager.GetEffectiveMaxLives(sId);
            int effShields = ShipUpgradesManager.GetEffectiveShields(sId);
            float effSpeed = ShipUpgradesManager.GetEffectiveMoveSpeed(sId);
            float effDmg = ShipUpgradesManager.GetEffectiveDamageMultiplier(sId);
            float effCd = ShipUpgradesManager.GetEffectiveAbilityCooldown(sId);

            int hpCount = activeSpec.hardpointOffsets != null ? activeSpec.hardpointOffsets.Length : 2;

            dossierStatsText.text =
                $"<size=22><color=#ffd700><b>PILOT FLIGHT RECORD & FLEET DOSSIER</b></color></size>\n\n" +
                $"<b>ACTIVE FLAGSHIP:</b> <color=#{ColorUtility.ToHtmlStringRGB(activeSpec.hullColor)}><b>{activeSpec.name}</b></color> ({activeSpec.role})\n" +
                $"<b>OVERALL CAMPAIGN COMPLETION:</b> <color=#00e5ff>{progressPct:F0}%</color> ({totalCleared}/40 Waves Secured)\n" +
                $"<b>SECTORS OPERATIONAL:</b> <color=#00ff88>{unlocked} of 4</color>   |   <b>TOTAL CAMPAIGN SCORE:</b> <color=#ffd700>{totalScore:N0} PTS</color>\n" +
                $"<b>COMMAND STAR CREDITS:</b> <color=#ffd700>CR {totalCredits:N0}</color>\n\n" +
                $"<color=#00e5ff><b>ACTIVE SHIP SPECIFICATIONS & HANGAR UPGRADES:</b></color>\n" +
                $"• <b>Default Weapon Hardpoints:</b> {hpCount} Synchronized Cannons\n" +
                $"• <b>Special Tactical Ability:</b> {activeSpec.abilityName} ({effCd:F1}s cooldown)\n" +
                $"• <b>Combat Hull Rating:</b> Tier {hullTier}/5 ({effLives} Lives, {effShields} Max Shields)\n" +
                $"• <b>Weapon Amplification:</b> Tier {wepTier}/5 ({effDmg * 100f:F0}% Output Firepower)\n" +
                $"• <b>Propulsion Thrusters:</b> Tier {spdTier}/5 ({effSpeed:F1} Thrust Velocity)\n" +
                $"• <b>Ability Capacitors:</b> Tier {abTier}/5 (Recharge Accelerated)\n\n" +
                $"<color=#00e5ff><b>SECTOR RECONNAISSANCE INTELLIGENCE:</b></color>\n" +
                $"• <b>Sector 01 (Earth Orbit):</b> Wave {clearedWaves1}/10 Cleared | Record High: {score1:N0} PTS\n" +
                $"• <b>Sector 02 (Asteroid Belt):</b> Wave {clearedWaves2}/10 Cleared | Record High: {score2:N0} PTS\n" +
                $"• <b>Sector 03 (Deep Nebula):</b> Wave {clearedWaves3}/10 Cleared | Record High: {score3:N0} PTS\n" +
                $"• <b>Sector 04 (War-Torn Wreckage):</b> Wave {clearedWaves4}/10 Cleared | Record High: {score4:N0} PTS";
        }

        // ==================================================================
        // PROCEDURAL UI GENERATION (1920x1080)
        // ==================================================================
        private void BuildUIHierarchy()
        {
            var canvas = GetComponentInChildren<Canvas>();
            if (canvas == null)
            {
                var canvasGo = new GameObject("HomeHubCanvas");
                canvasGo.transform.SetParent(transform, false);
                canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 50;
                var scaler = canvasGo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
                canvasGo.AddComponent<GraphicRaycaster>();
            }

            // Purge previous children if any
            for (int i = canvas.transform.childCount - 1; i >= 0; i--)
            {
                DestroyImmediate(canvas.transform.GetChild(i).gameObject);
            }

            // Dark space background
            var bgGo = new GameObject("Background");
            bgGo.transform.SetParent(canvas.transform, false);
            var bgRect = bgGo.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = new Color(0.025f, 0.035f, 0.07f, 1f);
            backgroundGo = bgGo;

            // ========================
            // HEADER BAR (Top y=0.92)
            // ========================
            var headerGo = new GameObject("HeaderBar");
            headerGo.transform.SetParent(canvas.transform, false);
            var headerRect = headerGo.AddComponent<RectTransform>();
            headerRect.anchorMin = new Vector2(0.05f, 0.89f);
            headerRect.anchorMax = new Vector2(0.95f, 0.98f);
            headerRect.sizeDelta = Vector2.zero;
            var headerBg = headerGo.AddComponent<Image>();
            SciFiUITheme.ApplyGlass(headerBg, new Color(0.02f, 0.04f, 0.08f, 0.88f));

            // Logo & Title
            var titleGo = new GameObject("TitleText");
            titleGo.transform.SetParent(headerGo.transform, false);
            var titleRect = titleGo.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.015f, 0.1f);
            titleRect.anchorMax = new Vector2(0.35f, 0.9f);
            titleRect.sizeDelta = Vector2.zero;
            var titleTmp = titleGo.AddComponent<TextMeshProUGUI>();
            titleTmp.text = "<color=#ffd700>STARFALL</color> // <color=#00e5ff>COMMAND HUB</color>";
            titleTmp.fontSize = 28;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.alignment = TextAlignmentOptions.MidlineLeft;

            // Callsign & Sector Status Badge
            var callsignGo = new GameObject("CallsignBadge");
            callsignGo.transform.SetParent(headerGo.transform, false);
            var callsignRect = callsignGo.AddComponent<RectTransform>();
            callsignRect.anchorMin = new Vector2(0.32f, 0.12f);
            callsignRect.anchorMax = new Vector2(0.53f, 0.88f);
            callsignRect.sizeDelta = Vector2.zero;
            var csBg = callsignGo.AddComponent<Image>();
            SciFiUITheme.ApplyGlass(csBg, new Color(0.04f, 0.08f, 0.15f, 0.90f));

            var csTxtGo = new GameObject("Text");
            csTxtGo.transform.SetParent(callsignGo.transform, false);
            var cstr = csTxtGo.AddComponent<RectTransform>();
            cstr.anchorMin = Vector2.zero;
            cstr.anchorMax = Vector2.one;
            cstr.sizeDelta = Vector2.zero;
            callsignText = csTxtGo.AddComponent<TextMeshProUGUI>();
            callsignText.fontSize = 14;
            callsignText.alignment = TextAlignmentOptions.Center;

            // Fleet War (Commander Duel Prototype) Button
            var pvpBtnGo = new GameObject("FleetWarBtn");
            pvpBtnGo.transform.SetParent(headerGo.transform, false);
            var pvpRect = pvpBtnGo.AddComponent<RectTransform>();
            pvpRect.anchorMin = new Vector2(0.55f, 0.1f);
            pvpRect.anchorMax = new Vector2(0.72f, 0.9f);
            pvpRect.sizeDelta = Vector2.zero;

            var pvpImg = pvpBtnGo.AddComponent<Image>();
            var pvpBtn = pvpBtnGo.AddComponent<Button>();
            SciFiUITheme.ApplyButton(pvpBtn, pvpImg, isPrimary: true);
            var pvpAnim = pvpBtnGo.AddComponent<SciFiButtonAnimator>();
            pvpAnim.PulseIdle = true;

            var pvpTxtGo = new GameObject("Text");
            pvpTxtGo.transform.SetParent(pvpBtnGo.transform, false);
            var pvpTxtRect = pvpTxtGo.AddComponent<RectTransform>();
            pvpTxtRect.anchorMin = Vector2.zero;
            pvpTxtRect.anchorMax = Vector2.one;
            pvpTxtRect.sizeDelta = Vector2.zero;
            var pvpTxt = pvpTxtGo.AddComponent<TextMeshProUGUI>();
            pvpTxt.text = "<size=15><b>[ FLEET WAR ]</b></size>\n<size=11><color=#ffe880>COMMANDER DUEL</color></size>";
            pvpTxt.alignment = TextAlignmentOptions.Center;
            pvpTxt.color = Color.white;

            pvpBtn.onClick.AddListener(() =>
            {
                Starfall.Commander.CommanderModeLauncher.StartCommanderModeFromHub();
            });

            // Star Credits Badge
            var credGo = new GameObject("CreditsBadge");
            credGo.transform.SetParent(headerGo.transform, false);
            var credRect = credGo.AddComponent<RectTransform>();
            credRect.anchorMin = new Vector2(0.74f, 0.1f);
            credRect.anchorMax = new Vector2(0.99f, 0.9f);
            credRect.sizeDelta = Vector2.zero;
            var credBg = credGo.AddComponent<Image>();
            SciFiUITheme.ApplyPanel(credBg, new Color(0.06f, 0.10f, 0.18f, 0.95f));

            var credTextGo = new GameObject("CreditsText");
            credTextGo.transform.SetParent(credGo.transform, false);
            var credTextRect = credTextGo.AddComponent<RectTransform>();
            credTextRect.anchorMin = Vector2.zero;
            credTextRect.anchorMax = Vector2.one;
            credTextRect.sizeDelta = Vector2.zero;
            creditsText = credTextGo.AddComponent<TextMeshProUGUI>();
            creditsText.fontSize = 18;
            creditsText.color = new Color(1f, 0.85f, 0.2f);
            creditsText.alignment = TextAlignmentOptions.Center;

            // ========================
            // TAB NAVIGATION BAR (y=0.81)
            // ========================
            var navGo = new GameObject("NavBar");
            navGo.transform.SetParent(canvas.transform, false);
            var navRect = navGo.AddComponent<RectTransform>();
            navRect.anchorMin = new Vector2(0.05f, 0.80f);
            navRect.anchorMax = new Vector2(0.95f, 0.87f);
            navRect.sizeDelta = Vector2.zero;

            string[] tabLabels = { "MISSIONS", "ARMORY SHOP", "SETTINGS", "PILOT DOSSIER", "QUIT" };
            float tabWidth = 0.185f;
            float tabSpacing = 0.015f;

            for (int t = 0; t < 5; t++)
            {
                var tabBtnGo = new GameObject($"TabBtn_{t}");
                tabBtnGo.transform.SetParent(navGo.transform, false);
                var tRect = tabBtnGo.AddComponent<RectTransform>();
                float startX = t * (tabWidth + tabSpacing);
                tRect.anchorMin = new Vector2(startX, 0f);
                tRect.anchorMax = new Vector2(startX + tabWidth, 1f);
                tRect.sizeDelta = Vector2.zero;

                var tBg = tabBtnGo.AddComponent<Image>();
                var tBtn = tabBtnGo.AddComponent<Button>();
                SciFiUITheme.ApplyButton(tBtn, tBg, isPrimary: false);
                tabBtnGo.AddComponent<SciFiButtonAnimator>();

                var tTextGo = new GameObject("Label");
                tTextGo.transform.SetParent(tabBtnGo.transform, false);
                var tTextRect = tTextGo.AddComponent<RectTransform>();
                tTextRect.anchorMin = Vector2.zero;
                tTextRect.anchorMax = Vector2.one;
                tTextRect.sizeDelta = Vector2.zero;
                var tTmp = tTextGo.AddComponent<TextMeshProUGUI>();
                tTmp.text = $"<b>{tabLabels[t]}</b>";
                tTmp.fontSize = 15;
                tTmp.color = Color.white;
                tTmp.alignment = TextAlignmentOptions.Center;

                switch (t)
                {
                    case 0:
                        tabMissionsBtn = tBtn;
                        tabMissionsBg = tBg;
                        tBtn.onClick.AddListener(() => SwitchTab(HubTab.Missions));
                        break;
                    case 1:
                        tabShopBtn = tBtn;
                        tabShopBg = tBg;
                        tBtn.onClick.AddListener(() => SwitchTab(HubTab.Shop));
                        break;
                    case 2:
                        tabSettingsBtn = tBtn;
                        tabSettingsBg = tBg;
                        tBtn.onClick.AddListener(() => SwitchTab(HubTab.Settings));
                        break;
                    case 3:
                        tabDossierBtn = tBtn;
                        tabDossierBg = tBg;
                        tBtn.onClick.AddListener(() => SwitchTab(HubTab.Dossier));
                        break;
                    case 4:
                        tabQuitBtn = tBtn;
                        tBg.color = new Color(0.45f, 0.12f, 0.15f, 0.9f);
                        tBtn.onClick.AddListener(() =>
                        {
                            Debug.Log("[HomeHubUI] Quit Game requested.");
#if UNITY_EDITOR
                            UnityEditor.EditorApplication.isPlaying = false;
#else
                            Application.Quit();
#endif
                        });
                        break;
                }
            }

            // ========================
            // PANEL 1: MISSIONS
            // ========================
            BuildMissionsPanel(canvas);

            // ========================
            // PANEL 2: ARMORY SHOP
            // ========================
            BuildShopPanel(canvas);

            // ========================
            // PANEL 3: SETTINGS
            // ========================
            BuildSettingsPanel(canvas);

            // ========================
            // PANEL 4: PILOT DOSSIER
            // ========================
            BuildDossierPanel(canvas);
        }

        private void BuildMissionsPanel(Canvas canvas)
        {
            panelMissions = new GameObject("Panel_Missions");
            panelMissions.transform.SetParent(canvas.transform, false);
            var pRect = panelMissions.AddComponent<RectTransform>();
            pRect.anchorMin = new Vector2(0.05f, 0.05f);
            pRect.anchorMax = new Vector2(0.95f, 0.77f);
            pRect.sizeDelta = Vector2.zero;

            float[] cardY = { 0.87f, 0.62f, 0.37f, 0.12f };
            string[] titles = { "SECTOR 01: EARTH ORBIT", "SECTOR 02: ASTEROID BELT", "SECTOR 03: DEEP NEBULA", "SECTOR 04: WAR-TORN WRECKAGE" };
            string[] descs =
            {
                "10 Waves • 2 Reinforcement Sets • Boss: Invader Carrier",
                "10 Waves • 3-4 Reinforcement Sets • Boss: Plasma Dreadnought",
                "10 Waves • 4-5 Reinforcement Sets • Boss: Hive Queen Leviathan",
                "10 Waves • 4-6 Reinforcement Sets • Boss: Inferno Colossus Juggernaut"
            };

            for (int i = 0; i < 4; i++)
            {
                int lvl = i + 1;
                var card = new SectorCardView { levelNumber = lvl };

                var cardGo = new GameObject($"Card_Level_{lvl}");
                cardGo.transform.SetParent(panelMissions.transform, false);
                var cardRect = cardGo.AddComponent<RectTransform>();
                cardRect.anchorMin = new Vector2(0.5f, cardY[i]);
                cardRect.anchorMax = new Vector2(0.5f, cardY[i]);
                cardRect.sizeDelta = new Vector2(1700, 150);

                card.cardGo = cardGo;
                card.cardBackground = cardGo.AddComponent<Image>();
                SciFiUITheme.ApplyPanel(card.cardBackground, new Color(0.04f, 0.07f, 0.14f, 0.95f));

                // Title & desc
                var infoGo = new GameObject("InfoText");
                infoGo.transform.SetParent(cardGo.transform, false);
                var infoRect = infoGo.AddComponent<RectTransform>();
                infoRect.anchorMin = new Vector2(0.025f, 0.70f);
                infoRect.anchorMax = new Vector2(0.80f, 0.95f);
                infoRect.sizeDelta = Vector2.zero;
                card.titleText = infoGo.AddComponent<TextMeshProUGUI>();
                card.titleText.text = $"<size=18><b>{titles[i]}</b></size>   <size=13><color=#6699bb>{descs[i]}</color></size>";
                card.titleText.alignment = TextAlignmentOptions.MidlineLeft;

                // Status & Progress
                var statusGo = new GameObject("StatusText");
                statusGo.transform.SetParent(cardGo.transform, false);
                var statusRect = statusGo.AddComponent<RectTransform>();
                statusRect.anchorMin = new Vector2(0.025f, 0.44f);
                statusRect.anchorMax = new Vector2(0.46f, 0.68f);
                statusRect.sizeDelta = Vector2.zero;
                card.statusText = statusGo.AddComponent<TextMeshProUGUI>();
                card.statusText.fontSize = 14;
                card.statusText.alignment = TextAlignmentOptions.MidlineLeft;

                var progGo = new GameObject("ProgressText");
                progGo.transform.SetParent(cardGo.transform, false);
                var progRect = progGo.AddComponent<RectTransform>();
                progRect.anchorMin = new Vector2(0.48f, 0.44f);
                progRect.anchorMax = new Vector2(0.80f, 0.68f);
                progRect.sizeDelta = Vector2.zero;
                card.progressText = progGo.AddComponent<TextMeshProUGUI>();
                card.progressText.fontSize = 14;
                card.progressText.alignment = TextAlignmentOptions.MidlineLeft;

                // Deploy / Continue Button with Sci-Fi Button styling
                var btnGo = new GameObject("DeployButton");
                btnGo.transform.SetParent(cardGo.transform, false);
                var btnRect = btnGo.AddComponent<RectTransform>();
                btnRect.anchorMin = new Vector2(0.82f, 0.15f);
                btnRect.anchorMax = new Vector2(0.98f, 0.85f);
                btnRect.sizeDelta = Vector2.zero;

                var btnImg = btnGo.AddComponent<Image>();
                card.deployButton = btnGo.AddComponent<Button>();
                SciFiUITheme.ApplyButton(card.deployButton, btnImg, isPrimary: true);
                var deployAnim = btnGo.AddComponent<SciFiButtonAnimator>();
                deployAnim.PulseIdle = true;

                var btnTextGo = new GameObject("ButtonText");
                btnTextGo.transform.SetParent(btnGo.transform, false);
                var btnTextRect = btnTextGo.AddComponent<RectTransform>();
                btnTextRect.anchorMin = Vector2.zero;
                btnTextRect.anchorMax = Vector2.one;
                btnTextRect.sizeDelta = Vector2.zero;
                card.deployButtonText = btnTextGo.AddComponent<TextMeshProUGUI>();
                card.deployButtonText.text = "DEPLOY";
                card.deployButtonText.fontSize = 17;
                card.deployButtonText.fontStyle = FontStyles.Bold;
                card.deployButtonText.alignment = TextAlignmentOptions.Center;
                card.deployButtonText.color = Color.white;

                // Wave Matrix Row (W1 .. W10)
                var waveRowGo = new GameObject("WaveMatrixContainer");
                waveRowGo.transform.SetParent(cardGo.transform, false);
                var waveRowRect = waveRowGo.AddComponent<RectTransform>();
                waveRowRect.anchorMin = new Vector2(0.025f, 0.08f);
                waveRowRect.anchorMax = new Vector2(0.80f, 0.38f);
                waveRowRect.sizeDelta = Vector2.zero;
                card.waveMatrixContainer = waveRowGo;

                var waveLblGo = new GameObject("WaveRowLabel");
                waveLblGo.transform.SetParent(waveRowGo.transform, false);
                var waveLblRect = waveLblGo.AddComponent<RectTransform>();
                waveLblRect.anchorMin = new Vector2(0f, 0f);
                waveLblRect.anchorMax = new Vector2(0.10f, 1f);
                waveLblRect.sizeDelta = Vector2.zero;
                var waveLbl = waveLblGo.AddComponent<TextMeshProUGUI>();
                waveLbl.text = "<size=15><b>WAVES:</b></size>";
                waveLbl.alignment = TextAlignmentOptions.MidlineLeft;
                waveLbl.color = new Color(0.6f, 0.75f, 0.9f);

                float startX = 0.10f;
                float spacing = (1.0f - startX) / 10f;
                for (int w = 0; w < 10; w++)
                {
                    int waveNum = w + 1;
                    var wBtnGo = new GameObject($"WaveBtn_{waveNum}");
                    wBtnGo.transform.SetParent(waveRowGo.transform, false);
                    var wRect = wBtnGo.AddComponent<RectTransform>();
                    wRect.anchorMin = new Vector2(startX + w * spacing, 0.05f);
                    wRect.anchorMax = new Vector2(startX + (w + 0.88f) * spacing, 0.95f);
                    wRect.sizeDelta = Vector2.zero;

                    var wImg = wBtnGo.AddComponent<Image>();
                    wImg.sprite = SciFiUITheme.ButtonNormal;
                    wImg.type = Image.Type.Sliced;
                    var wBtn = wBtnGo.AddComponent<Button>();
                    wBtnGo.AddComponent<SciFiButtonAnimator>();

                    var wTextGo = new GameObject("Text");
                    wTextGo.transform.SetParent(wBtnGo.transform, false);
                    var wTextRect = wTextGo.AddComponent<RectTransform>();
                    wTextRect.anchorMin = Vector2.zero;
                    wTextRect.anchorMax = Vector2.one;
                    wTextRect.sizeDelta = Vector2.zero;
                    var wTxt = wTextGo.AddComponent<TextMeshProUGUI>();
                    wTxt.text = $"{waveNum}";
                    wTxt.fontSize = 17;
                    wTxt.alignment = TextAlignmentOptions.Center;
                    wTxt.color = Color.white;

                    card.waveButtons[w] = wBtn;
                    card.waveButtonTexts[w] = wTxt;
                    card.waveButtonImages[w] = wImg;
                }

                sectorCards[i] = card;
            }
        }

        private void BuildShopPanel(Canvas canvas)
        {
            panelShop = new GameObject("Panel_Shop");
            panelShop.transform.SetParent(canvas.transform, false);
            var pRect = panelShop.AddComponent<RectTransform>();
            pRect.anchorMin = new Vector2(0.05f, 0.05f);
            pRect.anchorMax = new Vector2(0.95f, 0.77f);
            pRect.sizeDelta = Vector2.zero;

            // ==============================================================
            // 1. TOP BAR: FLEET CAROUSEL SELECTOR (5 SHIPS)
            // ==============================================================
            var fleetBarGo = new GameObject("FleetSelectorBar");
            fleetBarGo.transform.SetParent(panelShop.transform, false);
            var fbRect = fleetBarGo.AddComponent<RectTransform>();
            fbRect.anchorMin = new Vector2(0.01f, 0.905f);
            fbRect.anchorMax = new Vector2(0.99f, 0.995f);
            fbRect.sizeDelta = Vector2.zero;

            float colWidth = 0.76f / 5f;
            for (int i = 0; i < 5; i++)
            {
                int shipIdx = i;
                var btnGo = new GameObject($"ShipBtn_{i}");
                btnGo.transform.SetParent(fleetBarGo.transform, false);
                var bRect = btnGo.AddComponent<RectTransform>();
                bRect.anchorMin = new Vector2(i * colWidth + 0.003f, 0.05f);
                bRect.anchorMax = new Vector2((i + 1) * colWidth - 0.003f, 0.95f);
                bRect.sizeDelta = Vector2.zero;

                var bg = btnGo.AddComponent<Image>();
                var btn = btnGo.AddComponent<Button>();
                SciFiUITheme.ApplyButton(btn, bg, isPrimary: false);
                btnGo.AddComponent<SciFiButtonAnimator>();

                var txtGo = new GameObject("Label");
                txtGo.transform.SetParent(btnGo.transform, false);
                var tRect = txtGo.AddComponent<RectTransform>();
                tRect.anchorMin = Vector2.zero;
                tRect.anchorMax = Vector2.one;
                tRect.sizeDelta = Vector2.zero;
                var txt = txtGo.AddComponent<TextMeshProUGUI>();
                txt.fontSize = 12;
                txt.alignment = TextAlignmentOptions.Center;
                txt.color = Color.white;

                btn.onClick.AddListener(() =>
                {
                    selectedHangarIndex = shipIdx;
                    AudioManager.Instance?.PlayPowerUp();
                    RefreshShopTab();
                });

                shipSelectorButtons[i] = btn;
                shipSelectorBgs[i] = bg;
                shipSelectorTexts[i] = txt;
            }

            // --- DEV / TEST CHEAT BUTTONS (Top-Right) ---
            var addCrBtnGo = new GameObject("TestAddCreditsBtn");
            addCrBtnGo.transform.SetParent(fleetBarGo.transform, false);
            var acRect = addCrBtnGo.AddComponent<RectTransform>();
            acRect.anchorMin = new Vector2(0.77f, 0.05f);
            acRect.anchorMax = new Vector2(0.88f, 0.95f);
            acRect.sizeDelta = Vector2.zero;
            var acBg = addCrBtnGo.AddComponent<Image>();
            acBg.color = new Color(0.55f, 0.38f, 0.05f, 0.95f);
            var acBtn = addCrBtnGo.AddComponent<Button>();
            var acTxtGo = new GameObject("Text");
            acTxtGo.transform.SetParent(addCrBtnGo.transform, false);
            var actr = acTxtGo.AddComponent<RectTransform>();
            actr.anchorMin = Vector2.zero;
            actr.anchorMax = Vector2.one;
            actr.sizeDelta = Vector2.zero;
            var acTxt = acTxtGo.AddComponent<TextMeshProUGUI>();
            acTxt.text = "<b>+50,000 CR</b>\n<size=10><color=#ffe066>[TEST CHEAT]</color></size>";
            acTxt.fontSize = 11;
            acTxt.alignment = TextAlignmentOptions.Center;
            acTxt.color = Color.white;
            acBtn.onClick.AddListener(() =>
            {
                LevelProgressManager.AddCredits(50000);
                AudioManager.Instance?.PlayPowerUp();
                RefreshAllViews();
            });

            var unlockAllBtnGo = new GameObject("TestUnlockAllBtn");
            unlockAllBtnGo.transform.SetParent(fleetBarGo.transform, false);
            var uaBtnRect = unlockAllBtnGo.AddComponent<RectTransform>();
            uaBtnRect.anchorMin = new Vector2(0.885f, 0.05f);
            uaBtnRect.anchorMax = new Vector2(0.995f, 0.95f);
            uaBtnRect.sizeDelta = Vector2.zero;
            var uaBg = unlockAllBtnGo.AddComponent<Image>();
            uaBg.color = new Color(0.1f, 0.45f, 0.35f, 0.95f);
            var uaBtn = unlockAllBtnGo.AddComponent<Button>();
            var uaTxtGo = new GameObject("Text");
            uaTxtGo.transform.SetParent(unlockAllBtnGo.transform, false);
            var uatr = uaTxtGo.AddComponent<RectTransform>();
            uatr.anchorMin = Vector2.zero;
            uatr.anchorMax = Vector2.one;
            uatr.sizeDelta = Vector2.zero;
            var uaTxt = uaTxtGo.AddComponent<TextMeshProUGUI>();
            uaTxt.text = "<b>UNLOCK ALL</b>\n<size=10><color=#80ffcc>[TEST CHEAT]</color></size>";
            uaTxt.fontSize = 11;
            uaTxt.alignment = TextAlignmentOptions.Center;
            uaTxt.color = Color.white;
            uaBtn.onClick.AddListener(() =>
            {
                foreach (var sId in FleetDatabase.AllShipIds)
                {
                    PlayerPrefs.SetInt($"Starfall_ShipUnlocked_{sId}", 1);
                }
                PlayerPrefs.Save();
                AudioManager.Instance?.PlayPowerUp();
                RefreshAllViews();
            });

            // ==============================================================
            // 2. LEFT COLUMN: WARSHIP SHOWCASE & COMMISSIONING
            // ==============================================================
            var leftColGo = new GameObject("LeftShowcaseCol");
            leftColGo.transform.SetParent(panelShop.transform, false);
            var lcRect = leftColGo.AddComponent<RectTransform>();
            lcRect.anchorMin = new Vector2(0.01f, 0.01f);
            lcRect.anchorMax = new Vector2(0.47f, 0.89f);
            lcRect.sizeDelta = Vector2.zero;

            var leftBg = leftColGo.AddComponent<Image>();
            SciFiUITheme.ApplyPanel(leftBg, new Color(0.04f, 0.06f, 0.12f, 0.95f));

            // Title & Role
            var titleGo = new GameObject("ShowcaseTitle");
            titleGo.transform.SetParent(leftColGo.transform, false);
            var titleRect = titleGo.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.04f, 0.92f);
            titleRect.anchorMax = new Vector2(0.96f, 0.985f);
            titleRect.sizeDelta = Vector2.zero;
            showcaseTitleText = titleGo.AddComponent<TextMeshProUGUI>();
            showcaseTitleText.fontSize = 20;
            showcaseTitleText.alignment = TextAlignmentOptions.MidlineLeft;

            var roleGo = new GameObject("ShowcaseRole");
            roleGo.transform.SetParent(leftColGo.transform, false);
            var roleRect = roleGo.AddComponent<RectTransform>();
            roleRect.anchorMin = new Vector2(0.04f, 0.86f);
            roleRect.anchorMax = new Vector2(0.96f, 0.92f);
            roleRect.sizeDelta = Vector2.zero;
            showcaseRoleText = roleGo.AddComponent<TextMeshProUGUI>();
            showcaseRoleText.fontSize = 12;
            showcaseRoleText.alignment = TextAlignmentOptions.MidlineLeft;

            // Viewport Box for Ship Silhouette
            var viewportGo = new GameObject("ShipViewport");
            viewportGo.transform.SetParent(leftColGo.transform, false);
            var vpRect = viewportGo.AddComponent<RectTransform>();
            vpRect.anchorMin = new Vector2(0.04f, 0.49f);
            vpRect.anchorMax = new Vector2(0.96f, 0.85f);
            vpRect.sizeDelta = Vector2.zero;
            var vpBg = viewportGo.AddComponent<Image>();
            SciFiUITheme.ApplyGlass(vpBg, new Color(0.02f, 0.04f, 0.08f, 0.92f));

            // Glow Aura behind ship
            var glowGo = new GameObject("GlowAura");
            glowGo.transform.SetParent(viewportGo.transform, false);
            var glowRect = glowGo.AddComponent<RectTransform>();
            glowRect.anchorMin = new Vector2(0.5f, 0.5f);
            glowRect.anchorMax = new Vector2(0.5f, 0.5f);
            glowRect.sizeDelta = new Vector2(240, 240);
            showcaseGlowImage = glowGo.AddComponent<Image>();

            // Ship Silhouette Image
            var shipImgGo = new GameObject("ShipSprite");
            shipImgGo.transform.SetParent(viewportGo.transform, false);
            var siRect = shipImgGo.AddComponent<RectTransform>();
            siRect.anchorMin = new Vector2(0.5f, 0.5f);
            siRect.anchorMax = new Vector2(0.5f, 0.5f);
            siRect.sizeDelta = new Vector2(200, 200);
            showcaseShipImage = shipImgGo.AddComponent<Image>();
            showcaseShipImage.preserveAspect = true;

            // Hardpoint Box
            var hpGo = new GameObject("HardpointBanner");
            hpGo.transform.SetParent(leftColGo.transform, false);
            var hpRect = hpGo.AddComponent<RectTransform>();
            hpRect.anchorMin = new Vector2(0.04f, 0.33f);
            hpRect.anchorMax = new Vector2(0.96f, 0.47f);
            hpRect.sizeDelta = Vector2.zero;
            var hpBg = hpGo.AddComponent<Image>();
            SciFiUITheme.ApplyGlass(hpBg, new Color(0.04f, 0.07f, 0.14f, 0.85f));

            var hpTextGo = new GameObject("HardpointText");
            hpTextGo.transform.SetParent(hpGo.transform, false);
            var hptRect = hpTextGo.AddComponent<RectTransform>();
            hptRect.anchorMin = new Vector2(0.03f, 0.05f);
            hptRect.anchorMax = new Vector2(0.97f, 0.95f);
            hptRect.sizeDelta = Vector2.zero;
            showcaseHardpointText = hpTextGo.AddComponent<TextMeshProUGUI>();
            showcaseHardpointText.fontSize = 12;
            showcaseHardpointText.alignment = TextAlignmentOptions.MidlineLeft;

            // Ability Box
            var abGo = new GameObject("AbilityBanner");
            abGo.transform.SetParent(leftColGo.transform, false);
            var abRect = abGo.AddComponent<RectTransform>();
            abRect.anchorMin = new Vector2(0.04f, 0.16f);
            abRect.anchorMax = new Vector2(0.96f, 0.31f);
            abRect.sizeDelta = Vector2.zero;
            var abBg = abGo.AddComponent<Image>();
            SciFiUITheme.ApplyGlass(abBg, new Color(0.04f, 0.07f, 0.14f, 0.85f));

            var abTextGo = new GameObject("AbilityText");
            abTextGo.transform.SetParent(abGo.transform, false);
            var abtRect = abTextGo.AddComponent<RectTransform>();
            abtRect.anchorMin = new Vector2(0.03f, 0.05f);
            abtRect.anchorMax = new Vector2(0.97f, 0.95f);
            abtRect.sizeDelta = Vector2.zero;
            showcaseAbilityText = abTextGo.AddComponent<TextMeshProUGUI>();
            showcaseAbilityText.fontSize = 12;
            showcaseAbilityText.alignment = TextAlignmentOptions.MidlineLeft;

            // Status & Commission Action Button
            var statusGo = new GameObject("StatusText");
            statusGo.transform.SetParent(leftColGo.transform, false);
            var stRect = statusGo.AddComponent<RectTransform>();
            stRect.anchorMin = new Vector2(0.04f, 0.02f);
            stRect.anchorMax = new Vector2(0.50f, 0.13f);
            stRect.sizeDelta = Vector2.zero;
            showcaseStatusText = statusGo.AddComponent<TextMeshProUGUI>();
            showcaseStatusText.fontSize = 12;
            showcaseStatusText.alignment = TextAlignmentOptions.MidlineLeft;

            var actBtnGo = new GameObject("ShowcaseActionBtn");
            actBtnGo.transform.SetParent(leftColGo.transform, false);
            var actRect = actBtnGo.AddComponent<RectTransform>();
            actRect.anchorMin = new Vector2(0.52f, 0.02f);
            actRect.anchorMax = new Vector2(0.96f, 0.13f);
            actRect.sizeDelta = Vector2.zero;
            showcaseActionBtnBg = actBtnGo.AddComponent<Image>();
            showcaseActionBtn = actBtnGo.AddComponent<Button>();
            SciFiUITheme.ApplyButton(showcaseActionBtn, showcaseActionBtnBg, isPrimary: true);
            actBtnGo.AddComponent<SciFiButtonAnimator>();

            var actTxtGo = new GameObject("Text");
            actTxtGo.transform.SetParent(actBtnGo.transform, false);
            var acttRect = actTxtGo.AddComponent<RectTransform>();
            acttRect.anchorMin = Vector2.zero;
            acttRect.anchorMax = Vector2.one;
            acttRect.sizeDelta = Vector2.zero;
            showcaseActionBtnText = actTxtGo.AddComponent<TextMeshProUGUI>();
            showcaseActionBtnText.fontSize = 13;
            showcaseActionBtnText.fontStyle = FontStyles.Bold;
            showcaseActionBtnText.alignment = TextAlignmentOptions.Center;
            showcaseActionBtnText.color = Color.white;

            // ==============================================================
            // 3. RIGHT COLUMN: SPECS & RETROFIT UPGRADES
            // ==============================================================
            var rightColGo = new GameObject("RightSpecsCol");
            rightColGo.transform.SetParent(panelShop.transform, false);
            var rcRect = rightColGo.AddComponent<RectTransform>();
            rcRect.anchorMin = new Vector2(0.49f, 0.01f);
            rcRect.anchorMax = new Vector2(0.99f, 0.89f);
            rcRect.sizeDelta = Vector2.zero;

            // Top: Combat Attribute Profile Card
            var statsCardGo = new GameObject("CombatRatingsCard");
            statsCardGo.transform.SetParent(rightColGo.transform, false);
            var scRect = statsCardGo.AddComponent<RectTransform>();
            scRect.anchorMin = new Vector2(0f, 0.58f);
            scRect.anchorMax = new Vector2(1f, 1f);
            scRect.sizeDelta = Vector2.zero;
            var scBg = statsCardGo.AddComponent<Image>();
            SciFiUITheme.ApplyPanel(scBg, new Color(0.04f, 0.06f, 0.12f, 0.95f));

            var stTitleGo = new GameObject("RatingsTitle");
            stTitleGo.transform.SetParent(statsCardGo.transform, false);
            var strRect = stTitleGo.AddComponent<RectTransform>();
            strRect.anchorMin = new Vector2(0.04f, 0.82f);
            strRect.anchorMax = new Vector2(0.96f, 0.97f);
            strRect.sizeDelta = Vector2.zero;
            var strTxt = stTitleGo.AddComponent<TextMeshProUGUI>();
            strTxt.text = "<size=16><color=#ffd700><b>WARSHIP COMBAT RATINGS</b></color></size>  <size=11><color=#88aacc>(1 TO 10)</color></size>";
            strTxt.alignment = TextAlignmentOptions.MidlineLeft;

            statStrengthText = CreateStatLine(statsCardGo, "StrengthLine", new Vector2(0.04f, 0.61f), new Vector2(0.96f, 0.79f));
            statDamageText = CreateStatLine(statsCardGo, "DamageLine", new Vector2(0.04f, 0.42f), new Vector2(0.96f, 0.60f));
            statFlexibilityText = CreateStatLine(statsCardGo, "FlexibilityLine", new Vector2(0.04f, 0.23f), new Vector2(0.96f, 0.41f));
            statAbilityText = CreateStatLine(statsCardGo, "AbilityLine", new Vector2(0.04f, 0.04f), new Vector2(0.96f, 0.22f));

            // Bottom: Per-Ship Tech Modifications (4 Cards in 2x2 grid)
            var upgradesAreaGo = new GameObject("UpgradesArea");
            upgradesAreaGo.transform.SetParent(rightColGo.transform, false);
            var uaRect = upgradesAreaGo.AddComponent<RectTransform>();
            uaRect.anchorMin = new Vector2(0f, 0f);
            uaRect.anchorMax = new Vector2(1f, 0.56f);
            uaRect.sizeDelta = Vector2.zero;

            string[] uKeys =
            {
                ShipUpgradesManager.UPGRADE_HULL,
                ShipUpgradesManager.UPGRADE_WEAPONS,
                ShipUpgradesManager.UPGRADE_SPEED,
                ShipUpgradesManager.UPGRADE_ABILITY
            };

            string[] uTitles =
            {
                "HULL PLATING",
                "WEAPON OVERCLOCK",
                "THRUSTER ENGINES",
                "ABILITY CORES"
            };

            string[] uDescs =
            {
                "+1 Life & +25 Shield HP per tier.",
                "+15% Direct Damage & bullet velocity.",
                "+10% Speed & Evasion per tier.",
                "-1.5s Ability Cooldown per tier."
            };

            Vector2[] minAnchors =
            {
                new Vector2(0f, 0.52f),
                new Vector2(0.515f, 0.52f),
                new Vector2(0f, 0.02f),
                new Vector2(0.515f, 0.02f)
            };
            Vector2[] maxAnchors =
            {
                new Vector2(0.485f, 0.98f),
                new Vector2(1f, 0.98f),
                new Vector2(0.485f, 0.48f),
                new Vector2(1f, 0.48f)
            };

            for (int i = 0; i < 4; i++)
            {
                var card = new ShipTechCardView
                {
                    upgradeKey = uKeys[i],
                    title = uTitles[i],
                    description = uDescs[i]
                };

                var cardGo = new GameObject($"UpgradeCard_{uKeys[i]}");
                cardGo.transform.SetParent(upgradesAreaGo.transform, false);
                var cRect = cardGo.AddComponent<RectTransform>();
                cRect.anchorMin = minAnchors[i];
                cRect.anchorMax = maxAnchors[i];
                cRect.sizeDelta = Vector2.zero;

                var bg = cardGo.AddComponent<Image>();
                SciFiUITheme.ApplyPanel(bg, new Color(0.04f, 0.07f, 0.13f, 0.95f));

                // Title & Desc
                var infoGo = new GameObject("Info");
                infoGo.transform.SetParent(cardGo.transform, false);
                var iRect = infoGo.AddComponent<RectTransform>();
                iRect.anchorMin = new Vector2(0.04f, 0.50f);
                iRect.anchorMax = new Vector2(0.96f, 0.94f);
                iRect.sizeDelta = Vector2.zero;
                var infoTmp = infoGo.AddComponent<TextMeshProUGUI>();
                infoTmp.text = $"<size=13><color=#00e5ff><b>{uTitles[i]}</b></color></size>\n<size=10><color=#88aacc>{uDescs[i]}</color></size>";
                infoTmp.alignment = TextAlignmentOptions.TopLeft;

                // Level Text
                var lvlGo = new GameObject("LevelText");
                lvlGo.transform.SetParent(cardGo.transform, false);
                var lRect = lvlGo.AddComponent<RectTransform>();
                lRect.anchorMin = new Vector2(0.04f, 0.25f);
                lRect.anchorMax = new Vector2(0.60f, 0.48f);
                lRect.sizeDelta = Vector2.zero;
                card.levelText = lvlGo.AddComponent<TextMeshProUGUI>();
                card.levelText.fontSize = 11;
                card.levelText.alignment = TextAlignmentOptions.MidlineLeft;

                // Cost Text
                var costGo = new GameObject("CostText");
                costGo.transform.SetParent(cardGo.transform, false);
                var csRect = costGo.AddComponent<RectTransform>();
                csRect.anchorMin = new Vector2(0.04f, 0.05f);
                csRect.anchorMax = new Vector2(0.60f, 0.25f);
                csRect.sizeDelta = Vector2.zero;
                card.costText = costGo.AddComponent<TextMeshProUGUI>();
                card.costText.fontSize = 11;
                card.costText.alignment = TextAlignmentOptions.MidlineLeft;

                // Buy Button
                var btnGo = new GameObject("BuyBtn");
                btnGo.transform.SetParent(cardGo.transform, false);
                var bRect = btnGo.AddComponent<RectTransform>();
                bRect.anchorMin = new Vector2(0.64f, 0.08f);
                bRect.anchorMax = new Vector2(0.96f, 0.48f);
                bRect.sizeDelta = Vector2.zero;

                card.buyButtonImage = btnGo.AddComponent<Image>();
                card.buyButton = btnGo.AddComponent<Button>();
                SciFiUITheme.ApplyButton(card.buyButton, card.buyButtonImage, isPrimary: true);
                btnGo.AddComponent<SciFiButtonAnimator>();

                var bTextGo = new GameObject("BtnText");
                bTextGo.transform.SetParent(btnGo.transform, false);
                var btRect = bTextGo.AddComponent<RectTransform>();
                btRect.anchorMin = Vector2.zero;
                btRect.anchorMax = Vector2.one;
                btRect.sizeDelta = Vector2.zero;
                card.buyButtonText = bTextGo.AddComponent<TextMeshProUGUI>();
                card.buyButtonText.fontSize = 12;
                card.buyButtonText.fontStyle = FontStyles.Bold;
                card.buyButtonText.alignment = TextAlignmentOptions.Center;
                card.buyButtonText.color = Color.white;

                shipTechCards[i] = card;
            }
        }

        private TextMeshProUGUI CreateStatLine(GameObject parent, string name, Vector2 anchorMin, Vector2 anchorMax)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.sizeDelta = Vector2.zero;
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.fontSize = 13;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            return tmp;
        }

        private void BuildSettingsPanel(Canvas canvas)
        {
            panelSettings = new GameObject("Panel_Settings");
            panelSettings.transform.SetParent(canvas.transform, false);
            var pRect = panelSettings.AddComponent<RectTransform>();
            pRect.anchorMin = new Vector2(0.05f, 0.05f);
            pRect.anchorMax = new Vector2(0.95f, 0.77f);
            pRect.sizeDelta = Vector2.zero;

            // Two columns: Left is Audio & System options, Right is Flight Controls manual
            var leftCard = new GameObject("Settings_Left");
            leftCard.transform.SetParent(panelSettings.transform, false);
            var lRect = leftCard.AddComponent<RectTransform>();
            lRect.anchorMin = new Vector2(0.04f, 0.05f);
            lRect.anchorMax = new Vector2(0.48f, 0.95f);
            lRect.sizeDelta = Vector2.zero;
            var leftCardImg = leftCard.AddComponent<Image>();
            SciFiUITheme.ApplyPanel(leftCardImg, new Color(0.04f, 0.07f, 0.14f, 0.95f));

            var lTitleGo = new GameObject("Title");
            lTitleGo.transform.SetParent(leftCard.transform, false);
            var ltRect = lTitleGo.AddComponent<RectTransform>();
            ltRect.anchorMin = new Vector2(0.06f, 0.88f);
            ltRect.anchorMax = new Vector2(0.94f, 0.98f);
            ltRect.sizeDelta = Vector2.zero;
            var ltTmp = lTitleGo.AddComponent<TextMeshProUGUI>();
            ltTmp.text = "<b><color=#00e5ff>SYSTEM AUDIO & VIDEO CONFIGURATION</color></b>";
            ltTmp.fontSize = 20;
            ltTmp.alignment = TextAlignmentOptions.MidlineLeft;

            // 1. Master Volume
            CreateVolumeRow(leftCard, "MASTER VOLUME", 0.72f, out masterSlider, out masterValText, (v) =>
            {
                AudioManager.Instance?.SetMasterVolume(v);
                if (masterValText != null) masterValText.text = $"{Mathf.RoundToInt(v * 100)}%";
            });

            // 2. Music Volume
            CreateVolumeRow(leftCard, "MUSIC VOLUME", 0.58f, out musicSlider, out musicValText, (v) =>
            {
                AudioManager.Instance?.SetMusicVolume(v);
                if (musicValText != null) musicValText.text = $"{Mathf.RoundToInt(v * 100)}%";
            });

            // 3. SFX Volume
            CreateVolumeRow(leftCard, "SFX VOLUME", 0.44f, out sfxSlider, out sfxValText, (v) =>
            {
                AudioManager.Instance?.SetSFXVolume(v);
                if (sfxValText != null) sfxValText.text = $"{Mathf.RoundToInt(v * 100)}%";
            });

            // 4. Screen Shake Button
            var shakeGo = new GameObject("ShakeToggle");
            shakeGo.transform.SetParent(leftCard.transform, false);
            var shRect = shakeGo.AddComponent<RectTransform>();
            shRect.anchorMin = new Vector2(0.06f, 0.28f);
            shRect.anchorMax = new Vector2(0.48f, 0.38f);
            shRect.sizeDelta = Vector2.zero;
            shakeToggleImg = shakeGo.AddComponent<Image>();
            shakeToggleBtn = shakeGo.AddComponent<Button>();

            var shTextGo = new GameObject("Text");
            shTextGo.transform.SetParent(shakeGo.transform, false);
            var shtRect = shTextGo.AddComponent<RectTransform>();
            shtRect.anchorMin = Vector2.zero;
            shtRect.anchorMax = Vector2.one;
            shtRect.sizeDelta = Vector2.zero;
            shakeToggleText = shTextGo.AddComponent<TextMeshProUGUI>();
            shakeToggleText.fontSize = 16;
            shakeToggleText.fontStyle = FontStyles.Bold;
            shakeToggleText.alignment = TextAlignmentOptions.Center;

            shakeToggleBtn.onClick.AddListener(() =>
            {
                CameraShake.IsShakeEnabled = !CameraShake.IsShakeEnabled;
                RefreshSettingsTab();
            });

            // 5. Fullscreen Toggle
            var fullGo = new GameObject("FullscreenToggle");
            fullGo.transform.SetParent(leftCard.transform, false);
            var fRect = fullGo.AddComponent<RectTransform>();
            fRect.anchorMin = new Vector2(0.52f, 0.28f);
            fRect.anchorMax = new Vector2(0.94f, 0.38f);
            fRect.sizeDelta = Vector2.zero;
            fullscreenToggleImg = fullGo.AddComponent<Image>();
            fullscreenToggleBtn = fullGo.AddComponent<Button>();

            var fTextGo = new GameObject("Text");
            fTextGo.transform.SetParent(fullGo.transform, false);
            var ftRect = fTextGo.AddComponent<RectTransform>();
            ftRect.anchorMin = Vector2.zero;
            ftRect.anchorMax = Vector2.one;
            ftRect.sizeDelta = Vector2.zero;
            fullscreenToggleText = fTextGo.AddComponent<TextMeshProUGUI>();
            fullscreenToggleText.fontSize = 16;
            fullscreenToggleText.fontStyle = FontStyles.Bold;
            fullscreenToggleText.alignment = TextAlignmentOptions.Center;

            fullscreenToggleBtn.onClick.AddListener(() =>
            {
                Screen.fullScreen = !Screen.fullScreen;
                RefreshSettingsTab();
            });

            // 6. Reset Data Button
            var resetGo = new GameObject("ResetDataBtn");
            resetGo.transform.SetParent(leftCard.transform, false);
            var rRect = resetGo.AddComponent<RectTransform>();
            rRect.anchorMin = new Vector2(0.06f, 0.10f);
            rRect.anchorMax = new Vector2(0.94f, 0.20f);
            rRect.sizeDelta = Vector2.zero;
            var rImg = resetGo.AddComponent<Image>();
            resetDataBtn = resetGo.AddComponent<Button>();
            SciFiUITheme.ApplyButton(resetDataBtn, rImg, isPrimary: false);
            resetGo.AddComponent<SciFiButtonAnimator>();

            var rTextGo = new GameObject("Text");
            rTextGo.transform.SetParent(resetGo.transform, false);
            var rtRect = rTextGo.AddComponent<RectTransform>();
            rtRect.anchorMin = Vector2.zero;
            rtRect.anchorMax = Vector2.one;
            rtRect.sizeDelta = Vector2.zero;
            var rtTmp = rTextGo.AddComponent<TextMeshProUGUI>();
            rtTmp.text = "RESET ALL PROGRESS & CREDITS (DEFAULT)";
            rtTmp.fontSize = 16;
            rtTmp.fontStyle = FontStyles.Bold;
            rtTmp.color = Color.white;
            rtTmp.alignment = TextAlignmentOptions.Center;

            resetDataBtn.onClick.AddListener(() =>
            {
                LevelProgressManager.ResetProgress();
                RefreshAllViews();
                AudioManager.Instance?.PlayExplosion(true);
            });

            // Right Column: Flight Controls Reference Guide
            var rightCard = new GameObject("Settings_Right");
            rightCard.transform.SetParent(panelSettings.transform, false);
            var riRect = rightCard.AddComponent<RectTransform>();
            riRect.anchorMin = new Vector2(0.52f, 0.05f);
            riRect.anchorMax = new Vector2(0.96f, 0.95f);
            riRect.sizeDelta = Vector2.zero;
            var rightCardImg = rightCard.AddComponent<Image>();
            SciFiUITheme.ApplyPanel(rightCardImg, new Color(0.04f, 0.07f, 0.14f, 0.95f));

            var flightTmpGo = new GameObject("FlightManualText");
            flightTmpGo.transform.SetParent(rightCard.transform, false);
            var fmRect = flightTmpGo.AddComponent<RectTransform>();
            fmRect.anchorMin = new Vector2(0.06f, 0.05f);
            fmRect.anchorMax = new Vector2(0.94f, 0.95f);
            fmRect.sizeDelta = Vector2.zero;
            var fmTmp = flightTmpGo.AddComponent<TextMeshProUGUI>();
            fmTmp.text =
                $"<size=22><b><color=#ffd700>FLIGHT CONTROLS & TACTICAL MANUAL</color></b></size>\n\n" +
                $"<color=#00e5ff><b>FLIGHT NAVIGATION:</b></color>\n" +
                $"• <b>WASD / Arrow Keys</b> : Maneuver Spaceship\n" +
                $"• Smooth lateral thrusters with boundary stabilizers\n\n" +
                $"<color=#00e5ff><b>WEAPONS & COMBAT:</b></color>\n" +
                $"• <b>Spacebar / Left-Click</b> : Rapid Primary Laser Battery\n" +
                $"• Continuous fire stream with escalating weapon tiers\n\n" +
                $"<color=#00e5ff><b>STARFALL SMART BOMBS:</b></color>\n" +
                $"• <b>B Key / Spacebar</b> : Deploy Tactical Screen Cleanser\n" +
                $"• Eradicates all standard enemies & neutralizes incoming projectiles\n\n" +
                $"<color=#00e5ff><b>SYSTEM & PAUSE:</b></color>\n" +
                $"• <b>ESC Key</b> : Pause Mission & Access Mission Menu\n\n" +
                $"<size=14><color=#6699bb>Tactical Tip: Upgrade the Tractor Beam in the Armory to magnetically vacuum fallen powerups and credits safely during intense firefights!</color></size>";
            fmTmp.alignment = TextAlignmentOptions.TopLeft;
        }

        private void CreateVolumeRow(GameObject parent, string label, float yAnchor, out Slider slider, out TextMeshProUGUI valText, UnityEngine.Events.UnityAction<float> onValueChanged)
        {
            var rowGo = new GameObject(label);
            rowGo.transform.SetParent(parent.transform, false);
            var rRect = rowGo.AddComponent<RectTransform>();
            rRect.anchorMin = new Vector2(0.06f, yAnchor - 0.04f);
            rRect.anchorMax = new Vector2(0.94f, yAnchor + 0.05f);
            rRect.sizeDelta = Vector2.zero;

            var lblGo = new GameObject("Label");
            lblGo.transform.SetParent(rowGo.transform, false);
            var lRect = lblGo.AddComponent<RectTransform>();
            lRect.anchorMin = new Vector2(0f, 0.5f);
            lRect.anchorMax = new Vector2(0.40f, 1f);
            lRect.sizeDelta = Vector2.zero;
            var lTmp = lblGo.AddComponent<TextMeshProUGUI>();
            lTmp.text = label;
            lTmp.fontSize = 16;
            lTmp.color = new Color(0.75f, 0.85f, 0.95f);
            lTmp.alignment = TextAlignmentOptions.MidlineLeft;

            var valGo = new GameObject("Val");
            valGo.transform.SetParent(rowGo.transform, false);
            var vRect = valGo.AddComponent<RectTransform>();
            vRect.anchorMin = new Vector2(0.82f, 0.5f);
            vRect.anchorMax = new Vector2(1f, 1f);
            vRect.sizeDelta = Vector2.zero;
            valText = valGo.AddComponent<TextMeshProUGUI>();
            valText.text = "100%";
            valText.fontSize = 16;
            valText.color = new Color(0f, 0.9f, 1f);
            valText.alignment = TextAlignmentOptions.MidlineRight;

            // Slider
            var sliderGo = new GameObject("Slider");
            sliderGo.transform.SetParent(rowGo.transform, false);
            var sRect = sliderGo.AddComponent<RectTransform>();
            sRect.anchorMin = new Vector2(0f, 0.05f);
            sRect.anchorMax = new Vector2(1f, 0.45f);
            sRect.sizeDelta = Vector2.zero;

            slider = sliderGo.AddComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 0.85f;

            var bg = sliderGo.AddComponent<Image>();
            bg.sprite = SciFiUITheme.GaugeTrack;
            bg.type = Image.Type.Sliced;
            bg.color = Color.white;

            var fillArea = new GameObject("Fill Area");
            fillArea.transform.SetParent(sliderGo.transform, false);
            var faRect = fillArea.AddComponent<RectTransform>();
            faRect.anchorMin = Vector2.zero;
            faRect.anchorMax = Vector2.one;
            faRect.sizeDelta = Vector2.zero;

            var fill = new GameObject("Fill");
            fill.transform.SetParent(fillArea.transform, false);
            var fRect = fill.AddComponent<RectTransform>();
            fRect.anchorMin = Vector2.zero;
            fRect.anchorMax = Vector2.one;
            fRect.sizeDelta = Vector2.zero;
            var fillImg = fill.AddComponent<Image>();
            fillImg.sprite = SciFiUITheme.GaugeFill;
            fillImg.type = Image.Type.Sliced;
            fillImg.color = new Color(0f, 0.85f, 1f, 1f);

            slider.fillRect = fRect;
            slider.targetGraphic = bg;
            slider.onValueChanged.AddListener(onValueChanged);
        }

        private void BuildDossierPanel(Canvas canvas)
        {
            panelDossier = new GameObject("Panel_Dossier");
            panelDossier.transform.SetParent(canvas.transform, false);
            var pRect = panelDossier.AddComponent<RectTransform>();
            pRect.anchorMin = new Vector2(0.05f, 0.05f);
            pRect.anchorMax = new Vector2(0.95f, 0.77f);
            pRect.sizeDelta = Vector2.zero;

            var cardGo = new GameObject("DossierCard");
            cardGo.transform.SetParent(panelDossier.transform, false);
            var cRect = cardGo.AddComponent<RectTransform>();
            cRect.anchorMin = new Vector2(0.12f, 0.05f);
            cRect.anchorMax = new Vector2(0.88f, 0.95f);
            cRect.sizeDelta = Vector2.zero;
            var dossierImg = cardGo.AddComponent<Image>();
            SciFiUITheme.ApplyPanel(dossierImg, new Color(0.04f, 0.07f, 0.14f, 0.95f));

            var textGo = new GameObject("StatsText");
            textGo.transform.SetParent(cardGo.transform, false);
            var tRect = textGo.AddComponent<RectTransform>();
            tRect.anchorMin = new Vector2(0.06f, 0.05f);
            tRect.anchorMax = new Vector2(0.94f, 0.95f);
            tRect.sizeDelta = Vector2.zero;
            dossierStatsText = textGo.AddComponent<TextMeshProUGUI>();
            dossierStatsText.fontSize = 20;
            dossierStatsText.alignment = TextAlignmentOptions.TopLeft;
        }
    }
}
