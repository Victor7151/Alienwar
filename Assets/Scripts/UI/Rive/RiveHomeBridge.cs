using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Rive;
using Rive.Components;
using Starfall.Levels;
using Starfall.Audio;

namespace Starfall.UI
{
    /// <summary>
    /// RiveHomeBridge – Connects the Rive Landscape 16:9 Home Screen artboard ("HomeHangar", "Hangar_Background")
    /// and state machine ("Home_SM", "HomeHub_SM") to Unity gameplay and HomeHubUI.
    /// 
    /// Features:
    /// 1. Safe two-way communication: Dispatches Rive button clicks/events to HomeHubUI.
    /// 2. Live telemetry synchronization (Active Warship Index, Credits, Sector, Waves).
    /// 3. Parallax gyroscope / cursor tilt (GyroX, GyroY).
    /// 4. Graceful fallback: If .riv asset is still being authored, procedural UGUI remains 100% functional.
    /// 5. Full support for Android APK touch input and Desktop mouse input.
    /// </summary>
    [DisallowMultipleComponent]
    public class RiveHomeBridge : MonoBehaviour
    {
        public static RiveHomeBridge Instance { get; private set; }

        [Header("Rive Widget References")]
        [Tooltip("The RiveWidget displaying the Home Hangar artboard.")]
        public RiveWidget widget;

        [Tooltip("Optional direct reference to the .riv asset. If null, loads from Resources/Rive/home_hangar or home_station_hub.")]
        public Asset riveAsset;

        [Header("Artboard & State Machine")]
        [Tooltip("Name of the artboard in Rive (e.g. home_hangar, HomeHangar, or Home).")]
        public string artboardName = "home_hangar";

        [Tooltip("Name of the State Machine in Rive (e.g. Home_SM, HomeHub_SM, or State Machine 1).")]
        public string stateMachineName = "Home_SM";

        [Header("Display Options")]
        [Tooltip("If true, keeps the procedural UGUI canvas active on top of Rive so both can co-exist.")]
        public bool keepProceduralUIOverlay = true;

        [Tooltip("Enable subtle parallax tilt (-1.0 to +1.0) on GyroX and GyroY state machine inputs.")]
        public bool enableParallax = true;

        [Range(0.1f, 1f)]
        public float parallaxSensitivity = 0.45f;

        // Cached StateMachine Inputs
        private SMINumber _inputTab;
        private SMINumber _inputShipIndex;
        private SMINumber _inputCredits;
        private SMINumber _inputSector;
        private SMINumber _inputWave;
        private SMINumber _inputGyroX;
        private SMINumber _inputGyroY;
        private SMINumber _inputAlertLevel;

        private SMITrigger _triggerPlay;
        private SMITrigger _triggerShop;
        private SMITrigger _triggerHangar;
        private SMITrigger _triggerMissions;
        private SMITrigger _triggerSettings;
        private SMITrigger _triggerDossier;
        private SMITrigger _triggerQuit;
        private SMITrigger _triggerNextShip;
        private SMITrigger _triggerPrevShip;
        private SMITrigger _triggerEquipShip;

        // Cached ViewModel properties if ViewModel binding mode is active
        private ViewModelInstanceNumberProperty _vmTab;
        private ViewModelInstanceNumberProperty _vmShipIndex;
        private ViewModelInstanceNumberProperty _vmCredits;
        private ViewModelInstanceNumberProperty _vmSector;
        private ViewModelInstanceNumberProperty _vmWave;

        private bool _isLoaded = false;
        private int _lastSyncedCredits = -1;
        private int _lastSyncedShipIndex = -1;
        private int _lastSyncedTab = -1;
        private readonly HashSet<string> _missingInputs = new HashSet<string>();

        public bool IsLoaded => _isLoaded;

        private void Awake()
        {
            Instance = this;
            EnsureWidgetReference();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            StartCoroutine(InitializeRiveCoroutine());
        }

        private void EnsureWidgetReference()
        {
            if (widget == null)
            {
                widget = GetComponent<RiveWidget>() ?? GetComponentInChildren<RiveWidget>(true);
            }

            if (widget != null)
            {
                widget.HitTestBehavior = HitTestBehavior.Opaque;
                var panel = widget.GetComponentInParent<RivePanel>();
                if (panel != null)
                {
                    panel.MultiTouch = RivePanel.MultiTouchSupport.Enabled;
                    var canvasRenderer = panel.GetComponent<RiveCanvasRenderer>();
                    if (canvasRenderer != null)
                    {
                        canvasRenderer.PointerInputMode = PointerInputMode.EnablePointerInput;
                    }
                }
            }
        }

        private IEnumerator InitializeRiveCoroutine()
        {
            EnsureWidgetReference();

            // 1. Locate Rive asset
            Asset targetAsset = riveAsset;
            if (targetAsset == null && widget != null)
            {
                targetAsset = widget.Asset;
            }

            if (targetAsset == null)
            {
                // Candidate resource paths
                string[] candidatePaths = new string[]
                {
                    "Rive/home_hangar",
                    "Rive/home_station_hub",
                    "Rive/alienwarhome",
                    "Rive/home_screen",
                    "Rive/HomeHangar",
                    "Rive/home",
                    "Rive/homepage",
                    "Rive/main_menu",
                    "Rive/menu"
                };

                foreach (var path in candidatePaths)
                {
                    var loaded = Resources.Load<Asset>(path);
                    if (loaded != null)
                    {
                        targetAsset = loaded;
                        Debug.Log($"[RiveHomeBridge] Auto-loaded Rive asset from '{path}'.");
                        break;
                    }
                }
            }

            if (widget == null || targetAsset == null)
            {
                Debug.Log("<color=#ffd700>[RiveHomeBridge] Ready for Rive asset! Place 'home_hangar.riv' (or 'home.riv') in Assets/Resources/Rive/ or assign to Inspector. Procedural Home Hub UI is currently active.</color>");
                UpdateProceduralUIVisibility();
                yield break;
            }

            // 2. Configure widget settings & load asset
            try
            {
                widget.Fit = Fit.Cover;
                widget.HitTestBehavior = HitTestBehavior.Opaque;
                bool loadedExplicit = false;
                if (!string.IsNullOrEmpty(artboardName) && !string.IsNullOrEmpty(stateMachineName))
                {
                    try
                    {
                        widget.Load(targetAsset, artboardName, stateMachineName);
                        loadedExplicit = true;
                    }
                    catch (Exception ex)
                    {
                        Debug.LogWarning($"[RiveHomeBridge] Could not load explicit artboard '{artboardName}'/'{stateMachineName}': {ex.Message}. Falling back to default artboard.");
                    }
                }
                
                if (!loadedExplicit)
                {
                    widget.Load(targetAsset);
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RiveHomeBridge] Exception calling widget.Load: {ex.Message}");
            }

            // Wait for widget initialization or timeout after 5 seconds
            float timeout = 5f;
            float timer = 0f;
            while (timer < timeout)
            {
                if (widget != null && widget.StateMachine != null)
                {
                    if (widget.StateMachine.ViewModelInstance != null || widget.Status == WidgetStatus.Loaded)
                    {
                        break;
                    }
                }
                timer += Time.unscaledDeltaTime;
                yield return null;
            }

            // If explicit artboard didn't initialize StateMachine, try default artboard
            if ((widget == null || widget.StateMachine == null) && targetAsset != null)
            {
                try
                {
                    Debug.Log("[RiveHomeBridge] Explicit artboard had no active StateMachine, loading default artboard...");
                    widget.Load(targetAsset);
                }
                catch { }

                timer = 0f;
                while (timer < 2f)
                {
                    if (widget != null && widget.StateMachine != null) break;
                    timer += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            if (widget != null && widget.StateMachine != null)
            {
                _isLoaded = true;
                CacheStateMachineInputs();
                SyncAllGameStateToRive();
                UpdateProceduralUIVisibility();
                Debug.Log("<color=#00ff88><b>[RiveHomeBridge] Rive Home Hangar State Machine initialized and synchronized!</b></color>");
            }
            else
            {
                Debug.LogWarning("[RiveHomeBridge] Rive StateMachine did not initialize in time. Procedural UI remains active.");
                UpdateProceduralUIVisibility();
            }
        }

        private void UpdateProceduralUIVisibility()
        {
            if (HomeHubUI.Instance != null)
            {
                if (_isLoaded && !keepProceduralUIOverlay)
                {
                    HomeHubUI.Instance.SetProceduralUIVisible(false);
                }
                else if (_isLoaded && keepProceduralUIOverlay)
                {
                    HomeHubUI.Instance.SetProceduralUIVisible(true);
                    HomeHubUI.Instance.SetBackgroundVisible(false); // Seamlessly reveal Rive animation underneath!
                }
                else
                {
                    HomeHubUI.Instance.SetProceduralUIVisible(true);
                    HomeHubUI.Instance.SetBackgroundVisible(true);
                }
            }
        }

        private void CacheStateMachineInputs()
        {
            _missingInputs.Clear();
            if (widget == null || widget.StateMachine == null) return;

            var sm = widget.StateMachine;

            // Log all detected inputs in State Machine for easy designer verification
            string inputsList = "";
            try
            {
                foreach (var inp in sm.Inputs())
                {
                    if (inp != null)
                    {
                        string typeStr = inp.IsBoolean ? "bool" : (inp.IsNumber ? "number" : (inp.IsTrigger ? "trigger" : "unknown"));
                        inputsList += $"{inp.Name} ({typeStr}), ";
                    }
                }
                Debug.Log($"<color=#00e5ff>[RiveHomeBridge] Connected! Artboard='{widget.ArtboardName}', SM='{widget.StateMachineName}'. Detected Inputs: [ {inputsList.TrimEnd(',', ' ')} ]</color>");
            }
            catch { }

            // Cache standard StateMachine inputs
            _inputTab = GetNumberInput(sm, "Tab", "Number_Tab", "CurrentTab");
            _inputShipIndex = GetNumberInput(sm, "ShipIndex", "Number_ShipIndex", "Ship");
            _inputCredits = GetNumberInput(sm, "Credits", "Number_Credits", "StarCredits");
            _inputSector = GetNumberInput(sm, "CurrentSector", "Number_Sector", "Sector");
            _inputWave = GetNumberInput(sm, "CurrentWave", "Number_Wave", "Wave");
            _inputGyroX = GetNumberInput(sm, "GyroX", "ParallaxX");
            _inputGyroY = GetNumberInput(sm, "GyroY", "ParallaxY");
            _inputAlertLevel = GetNumberInput(sm, "AlertLevel", "Number_Alert");

            _triggerPlay = GetTriggerInput(sm, "Trigger_Play", "DeployTrigger", "Play", "Deploy");
            _triggerShop = GetTriggerInput(sm, "Trigger_Shop", "Shop", "Armory");
            _triggerHangar = GetTriggerInput(sm, "Trigger_Hangar", "Hangar", "Fleet");
            _triggerMissions = GetTriggerInput(sm, "Trigger_Missions", "Missions", "Sectors");
            _triggerSettings = GetTriggerInput(sm, "Trigger_Settings", "Settings");
            _triggerDossier = GetTriggerInput(sm, "Trigger_Dossier", "Dossier", "Stats");
            _triggerQuit = GetTriggerInput(sm, "Trigger_Quit", "Quit", "Exit");
            _triggerNextShip = GetTriggerInput(sm, "Trigger_NextShip", "NextShip", "Next");
            _triggerPrevShip = GetTriggerInput(sm, "Trigger_PrevShip", "PrevShip", "Prev");
            _triggerEquipShip = GetTriggerInput(sm, "Trigger_EquipShip", "EquipShip", "Equip");

            // Cache ViewModel properties if present
            if (sm.ViewModelInstance != null)
            {
                var vm = sm.ViewModelInstance;
                try { _vmTab = vm.GetNumberProperty("Tab"); } catch { }
                try { _vmShipIndex = vm.GetNumberProperty("ShipIndex"); } catch { }
                try { _vmCredits = vm.GetNumberProperty("Credits"); } catch { }
                try { _vmSector = vm.GetNumberProperty("CurrentSector"); } catch { }
                try { _vmWave = vm.GetNumberProperty("CurrentWave"); } catch { }
            }
        }

        private SMINumber GetNumberInput(StateMachine sm, params string[] names)
        {
            foreach (var n in names)
            {
                try
                {
                    foreach (var input in sm.Inputs())
                    {
                        if (input != null && input.IsNumber && input.Name.Equals(n, StringComparison.OrdinalIgnoreCase))
                        {
                            return sm.GetNumber(input.Name);
                        }
                    }
                }
                catch { }
            }
            return null;
        }

        private SMITrigger GetTriggerInput(StateMachine sm, params string[] names)
        {
            foreach (var n in names)
            {
                try
                {
                    foreach (var input in sm.Inputs())
                    {
                        if (input != null && input.IsTrigger && input.Name.Equals(n, StringComparison.OrdinalIgnoreCase))
                        {
                            return sm.GetTrigger(input.Name);
                        }
                    }
                }
                catch { }
            }
            return null;
        }

        private void Update()
        {
            // Toggle overlay mode dynamically (useful in editor and builds)
            if (Input.GetKeyDown(KeyCode.H) || Input.GetKeyDown(KeyCode.F1))
            {
                ToggleProceduralOverlay();
            }

            if (!_isLoaded || widget == null || widget.StateMachine == null) return;

            // 1. Poll Reported Events from Rive
            PollRiveEvents();

            // 2. Parallax Gyro / Mouse tilt
            if (enableParallax)
            {
                UpdateParallax();
            }

            // 3. Keep game telemetry in sync
            SyncTelemetryIfChanged();
        }

        private void PollRiveEvents()
        {
            if (widget == null || widget.StateMachine == null) return;

            var sm = widget.StateMachine;
            try
            {
                foreach (var evt in sm.EnumerateReportedEvents())
                {
                    if (evt != null)
                    {
                        string evtName = evt.Name;
                        evt.Dispose();
                        if (!string.IsNullOrEmpty(evtName))
                        {
                            HandleRiveEvent(evtName);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[RiveHomeBridge] Error reading Rive events: {ex.Message}");
            }
        }

        /// <summary>
        /// Handles click / tap events reported by the Rive State Machine.
        /// </summary>
        public void HandleRiveEvent(string eventName)
        {
            Debug.Log($"<color=#00e5ff>[RiveHomeBridge] Rive Event Triggered: <b>{eventName}</b></color>");

            string lower = eventName.Trim().ToLowerInvariant();

            // 1. Specific Sector / Mission Launches
            if (lower.Contains("sector1") || lower.Contains("sector_1") || lower.Contains("level1") || lower.Contains("level_1") || lower.Contains("earth"))
            {
                HomeHubUI.Instance?.LaunchActiveMission(1, 1);
                return;
            }
            if (lower.Contains("sector2") || lower.Contains("sector_2") || lower.Contains("level2") || lower.Contains("level_2") || lower.Contains("asteroid"))
            {
                HomeHubUI.Instance?.LaunchActiveMission(2, 1);
                return;
            }
            if (lower.Contains("sector3") || lower.Contains("sector_3") || lower.Contains("level3") || lower.Contains("level_3") || lower.Contains("nebula"))
            {
                HomeHubUI.Instance?.LaunchActiveMission(3, 1);
                return;
            }
            if (lower.Contains("sector4") || lower.Contains("sector_4") || lower.Contains("level4") || lower.Contains("level_4") || lower.Contains("wreckage"))
            {
                HomeHubUI.Instance?.LaunchActiveMission(4, 1);
                return;
            }

            // 2. Generic Mission Launch / Deploy (Active Sector & Wave)
            if (lower.Contains("play") || lower.Contains("deploy") || lower.Contains("launch") || lower.Contains("battle") || lower.Contains("start"))
            {
                HomeHubUI.Instance?.LaunchActiveMission();
                return;
            }

            // 3. Tab Navigation
            if (lower.Contains("mission") || lower.Contains("sector") || lower.Contains("campaign"))
            {
                HomeHubUI.Instance?.SwitchTab(HomeHubUI.HubTab.Missions);
                SetTabInput(0);
                return;
            }

            if (lower.Contains("hangar") || lower.Contains("fleet"))
            {
                HomeHubUI.Instance?.SwitchTab(HomeHubUI.HubTab.Shop);
                SetTabInput(1);
                return;
            }

            if (lower.Contains("shop") || lower.Contains("armory") || lower.Contains("upgrade"))
            {
                HomeHubUI.Instance?.SwitchTab(HomeHubUI.HubTab.Shop);
                SetTabInput(2);
                return;
            }

            if (lower.Contains("setting") || lower.Contains("option"))
            {
                HomeHubUI.Instance?.SwitchTab(HomeHubUI.HubTab.Settings);
                SetTabInput(3);
                return;
            }

            if (lower.Contains("dossier") || lower.Contains("stat") || lower.Contains("profile") || lower.Contains("pilot"))
            {
                HomeHubUI.Instance?.SwitchTab(HomeHubUI.HubTab.Dossier);
                SetTabInput(4);
                return;
            }

            // 4. Specific Warship Selections
            if (lower.Contains("viper") || lower.Equals("ship0") || lower.Equals("ship_0"))
            {
                HomeHubUI.Instance?.SelectHangarShip(0);
                return;
            }
            if (lower.Contains("tempest") || lower.Equals("ship1") || lower.Equals("ship_1"))
            {
                HomeHubUI.Instance?.SelectHangarShip(1);
                return;
            }
            if (lower.Contains("phoenix") || lower.Contains("solaris") || lower.Equals("ship2") || lower.Equals("ship_2"))
            {
                HomeHubUI.Instance?.SelectHangarShip(2);
                return;
            }
            if (lower.Contains("specter") || lower.Contains("phantom") || lower.Equals("ship3") || lower.Equals("ship_3"))
            {
                HomeHubUI.Instance?.SelectHangarShip(3);
                return;
            }
            if (lower.Contains("titan") || lower.Contains("crimson") || lower.Equals("ship4") || lower.Equals("ship_4"))
            {
                HomeHubUI.Instance?.SelectHangarShip(4);
                return;
            }

            // 5. Warship Carousel Controls
            if (lower.Contains("nextship") || lower.Equals("next"))
            {
                HomeHubUI.Instance?.NextHangarShip();
                return;
            }

            if (lower.Contains("prevship") || lower.Equals("prev"))
            {
                HomeHubUI.Instance?.PrevHangarShip();
                return;
            }

            if (lower.Contains("equip") || lower.Contains("commission"))
            {
                HomeHubUI.Instance?.EquipSelectedShip();
                return;
            }

            // 6. UI Toggle
            if (lower.Contains("toggleoverlay") || lower.Contains("togglehud"))
            {
                ToggleProceduralOverlay();
                return;
            }

            // 7. Exit / Quit
            if (lower.Contains("quit") || lower.Contains("exit"))
            {
                AudioManager.Instance?.PlayPowerUp();
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#else
                Application.Quit();
#endif
                return;
            }
        }

        private void UpdateParallax()
        {
            float normX = 0f;
            float normY = 0f;

            // Parallax based on mouse cursor or touch center offset
            if (Input.touchCount > 0)
            {
                var touch = Input.GetTouch(0);
                normX = (touch.position.x - Screen.width * 0.5f) / (Screen.width * 0.5f);
                normY = (touch.position.y - Screen.height * 0.5f) / (Screen.height * 0.5f);
            }
            else
            {
                normX = (Input.mousePosition.x - Screen.width * 0.5f) / (Screen.width * 0.5f);
                normY = (Input.mousePosition.y - Screen.height * 0.5f) / (Screen.height * 0.5f);
            }

            normX = Mathf.Clamp(normX, -1f, 1f) * parallaxSensitivity;
            normY = Mathf.Clamp(normY, -1f, 1f) * parallaxSensitivity;

            if (_inputGyroX != null) _inputGyroX.Value = normX;
            if (_inputGyroY != null) _inputGyroY.Value = normY;
        }

        private void SyncTelemetryIfChanged()
        {
            // Credits
            int credits = LevelProgressManager.GetCredits();
            if (credits != _lastSyncedCredits)
            {
                _lastSyncedCredits = credits;
                SetNumberProp(_inputCredits, _vmCredits, credits);
            }

            // Ship index
            int shipIdx = HomeHubUI.Instance != null ? HomeHubUI.Instance.SelectedHangarIndex : 0;
            if (shipIdx != _lastSyncedShipIndex)
            {
                _lastSyncedShipIndex = shipIdx;
                SetNumberProp(_inputShipIndex, _vmShipIndex, shipIdx);
            }

            // Tab index
            if (HomeHubUI.Instance != null)
            {
                int tabIdx = (int)HomeHubUI.Instance.CurrentTab;
                if (tabIdx != _lastSyncedTab)
                {
                    _lastSyncedTab = tabIdx;
                    SetTabInput(tabIdx);
                }
            }
        }

        public void SyncAllGameStateToRive()
        {
            int credits = LevelProgressManager.GetCredits();
            _lastSyncedCredits = credits;
            SetNumberProp(_inputCredits, _vmCredits, credits);

            int unlockedSector = LevelProgressManager.GetHighestUnlockedLevel();
            SetNumberProp(_inputSector, _vmSector, unlockedSector);

            int currentWave = LevelProgressManager.GetCurrentWave(unlockedSector);
            SetNumberProp(_inputWave, _vmWave, currentWave);

            int shipIdx = HomeHubUI.Instance != null ? HomeHubUI.Instance.SelectedHangarIndex : 0;
            _lastSyncedShipIndex = shipIdx;
            SetNumberProp(_inputShipIndex, _vmShipIndex, shipIdx);

            int tabIdx = HomeHubUI.Instance != null ? (int)HomeHubUI.Instance.CurrentTab : 0;
            _lastSyncedTab = tabIdx;
            SetTabInput(tabIdx);

            if (_inputAlertLevel != null)
            {
                _inputAlertLevel.Value = 0f; // 0 = Standard Cyan Ambient
            }
        }

        public void SetTabInput(int tabIndex)
        {
            SetNumberProp(_inputTab, _vmTab, tabIndex);
        }

        public void SetShipIndexInput(int shipIndex)
        {
            _lastSyncedShipIndex = shipIndex;
            SetNumberProp(_inputShipIndex, _vmShipIndex, shipIndex);
        }

        public void FireDeployAnimation()
        {
            if (_triggerPlay != null)
            {
                _triggerPlay.Fire();
            }
        }

        private void SetNumberProp(SMINumber smNum, ViewModelInstanceNumberProperty vmNum, float val)
        {
            if (smNum != null)
            {
                smNum.Value = val;
            }
            if (vmNum != null)
            {
                try { vmNum.Value = val; } catch { }
            }
        }

        /// <summary>
        /// Toggles between full Rive UI mode and procedural UGUI overlay mode.
        /// </summary>
        public void ToggleProceduralOverlay()
        {
            keepProceduralUIOverlay = !keepProceduralUIOverlay;
            UpdateProceduralUIVisibility();
        }
    }
}
