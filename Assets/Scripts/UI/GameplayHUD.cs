using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using Starfall.Core;
using Starfall.Player;
using Starfall.Enemies;
using Starfall.Combat;
using Starfall.UI.Theme;

namespace Starfall.UI
{
    /// <summary>
    /// Displays Score, Shield, Hull, and Game Over state (Section 30 of GDD).
    /// </summary>
    public class GameplayHUD : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TextMeshProUGUI scoreText;
        [SerializeField] private TextMeshProUGUI shieldText;
        [SerializeField] private TextMeshProUGUI hullText;
        [SerializeField] private TextMeshProUGUI livesText;
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private TextMeshProUGUI finalScoreText;
        [SerializeField] private UnityEngine.UI.Button restartButton;
        [SerializeField] private UnityEngine.UI.Button levelSelectButton;
        [SerializeField] private UnityEngine.UI.Button homeButton;

        [Header("Wave UI")]
        [SerializeField] private TextMeshProUGUI waveText;
        [SerializeField] private TextMeshProUGUI waveAnnouncementText;
        [SerializeField] private float announcementDuration = 2f;

        [Header("Weapons & Bombs")]
        [SerializeField] private TextMeshProUGUI weaponLevelText;
        [SerializeField] private TextMeshProUGUI bombText;
        [SerializeField] private UnityEngine.UI.Button bombButton;


        [Header("Boss Health Bar UI")]
        [SerializeField] private GameObject bossBarContainer;
        [SerializeField] private TextMeshProUGUI bossNameText;
        [SerializeField] private UnityEngine.UI.Image bossBarFill;

        public static GameplayHUD Instance { get; private set; }

        private PlayerHealth playerHealth;
        private PlayerCombat playerCombat;

        private TextMeshProUGUI comboText;
        private UnityEngine.UI.Image comboTimerFill;
        private float comboPunch;
        private bool externalHudMode;
        private RiveSpecialButton _cachedRiveSpecial;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            EnsureEventSystem();
        }

        private void EnsureEventSystem()
        {
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null)
            {
                es = FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>(FindObjectsInactive.Include);
            }
            if (es == null)
            {
                var esGo = new GameObject("EventSystem");
                es = esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
            }

            if (!es.gameObject.activeSelf) es.gameObject.SetActive(true);
            if (!es.enabled) es.enabled = true;

#if ENABLE_INPUT_SYSTEM
            var standalone = es.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            if (standalone != null)
            {
                Destroy(standalone);
            }
            var uiModule = es.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            if (uiModule == null)
            {
                uiModule = es.gameObject.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                uiModule.AssignDefaultActions();
            }
            else if (uiModule.actionsAsset == null)
            {
                uiModule.AssignDefaultActions();
            }

            if (uiModule.actionsAsset != null && !uiModule.actionsAsset.enabled)
            {
                uiModule.actionsAsset.Enable();
            }
            uiModule.enabled = true;
#else
            if (es.GetComponent<UnityEngine.EventSystems.StandaloneInputModule>() == null)
            {
                es.gameObject.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            }
#endif
            Debug.Log("[GameplayHUD] EventSystem and InputSystemUIInputModule verified.");
        }

        private void Start()
        {
            OrganizeHUDLayout();
            Starfall.UI.Rive.RiveAnimationManager.EnsureInstance(transform);
            TacticalFlightTrackpad.EnsureInstance(transform);

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnScoreChanged += UpdateScore;
                GameManager.Instance.OnGameStateChanged += OnGameStateChanged;
                GameManager.Instance.OnComboChanged += UpdateCombo;
                UpdateScore(GameManager.Instance.CurrentScore);
            }

            if (WaveManager.Instance != null)
            {
                WaveManager.Instance.OnWaveStarted += OnWaveStarted;
                WaveManager.Instance.OnWaveCleared += OnWaveCleared;
                WaveManager.Instance.OnBossFightStarted += OnBossFightStarted;
            }

            // Hook unified boss events
            BossEvents.OnBossSpawned += HandleBossSpawned;
            BossEvents.OnBossHealthChanged += HandleBossHealthChanged;
            BossEvents.OnBossPhaseChanged += HandleBossPhaseChanged;
            BossEvents.OnBossDefeated += HandleBossDefeated;

            if (Starfall.Levels.LevelManager.Instance != null)
            {
                Starfall.Levels.LevelManager.Instance.OnMissionCompleted += OnMissionCompleted;
            }

            // Legacy boss fallback
            InvaderCarrier.OnPhaseChanged += OnBossPhaseChanged;

            FindAndHookPlayer();

            if (gameOverPanel != null)
            {
                gameOverPanel.SetActive(false);
                for (int i = 0; i < gameOverPanel.transform.childCount; i++)
                {
                    gameOverPanel.transform.GetChild(i).gameObject.SetActive(false);
                }
            }

            PauseMenu.Ensure(); // Pause button + Resume / Restart / Return-to-HQ menu
            EnsureControlsButton();
            EnsureDirectivesUI();
            EnsureMobileControls();

            if (PlayerPrefs.GetInt("Starfall_ShowDirectives", 1) == 1)
            {
                ShowDirectivesModal(true);
            }

            if (waveAnnouncementText != null)
                waveAnnouncementText.gameObject.SetActive(false);

            if (bombButton != null)
                bombButton.onClick.AddListener(OnBombButtonClicked);

            EnsureAbilityButton();
            EnsureComboUI();
            EnsureObjectivesHUD();
        }

        // ------------------------------------------------------------------
        // Combo readout (top-right, under the wave counter)
        // ------------------------------------------------------------------

        private void EnsureComboUI()
        {
            if (comboText != null) return;
            var go = new GameObject("ComboText", typeof(RectTransform));
            go.transform.SetParent(transform, false);
            comboText = go.AddComponent<TextMeshProUGUI>();
            if (scoreText != null) comboText.font = scoreText.font;
            comboText.fontSize = 22f;
            comboText.fontStyle = FontStyles.Bold;
            comboText.alignment = TextAlignmentOptions.Right;
            comboText.raycastTarget = false;
            var rt = comboText.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-20f, -146f);
            rt.sizeDelta = new Vector2(280f, 30f);

            var barGo = new GameObject("ComboTimer", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            barGo.transform.SetParent(go.transform, false);
            comboTimerFill = barGo.GetComponent<UnityEngine.UI.Image>();
            comboTimerFill.sprite = SciFiUITheme.GaugeFill;
            comboTimerFill.type = UnityEngine.UI.Image.Type.Sliced;
            comboTimerFill.color = new Color(1f, 0.45f, 0.95f, 0.95f);
            comboTimerFill.raycastTarget = false;
            var br = comboTimerFill.rectTransform;
            br.anchorMin = new Vector2(1f, 0f);
            br.anchorMax = new Vector2(1f, 0f);
            br.pivot = new Vector2(1f, 1f);
            br.anchoredPosition = new Vector2(0f, -2f);
            br.sizeDelta = new Vector2(160f, 6f);

            go.SetActive(false);
        }

        // ------------------------------------------------------------------
        // Mission Objectives Widget (top-right, underneath combo readout)
        // ------------------------------------------------------------------
        private GameObject objectivesContainer;
        private TextMeshProUGUI objectivesText;

        private void EnsureObjectivesHUD()
        {
            if (objectivesContainer != null) return;

            // Ensure MissionObjectiveManager is instantiated
            Starfall.Levels.MissionObjectiveManager.EnsureInstance(transform);

            objectivesContainer = new GameObject("ObjectivesWidget", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            objectivesContainer.transform.SetParent(transform, false);

            var rt = objectivesContainer.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-15f, -175f);
            rt.sizeDelta = new Vector2(330f, 110f);

            var bg = objectivesContainer.GetComponent<UnityEngine.UI.Image>();
            SciFiUITheme.ApplyGlass(bg);
            bg.raycastTarget = false;

            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleGo.transform.SetParent(objectivesContainer.transform, false);
            var tr = titleGo.GetComponent<RectTransform>();
            tr.anchorMin = new Vector2(0f, 1f);
            tr.anchorMax = new Vector2(1f, 1f);
            tr.pivot = new Vector2(0.5f, 1f);
            tr.anchoredPosition = new Vector2(0f, -4f);
            tr.sizeDelta = new Vector2(0f, 20f);

            var titleTmp = titleGo.GetComponent<TextMeshProUGUI>();
            titleTmp.text = "<color=#00E5FF><b>MISSION OBJECTIVES</b></color>";
            titleTmp.fontSize = 11;
            titleTmp.fontStyle = FontStyles.Bold;
            titleTmp.alignment = TextAlignmentOptions.Center;
            titleTmp.raycastTarget = false;

            var textGo = new GameObject("Body", typeof(RectTransform), typeof(TextMeshProUGUI));
            textGo.transform.SetParent(objectivesContainer.transform, false);
            var br = textGo.GetComponent<RectTransform>();
            br.anchorMin = Vector2.zero;
            br.anchorMax = Vector2.one;
            br.offsetMin = new Vector2(14f, 8f);
            br.offsetMax = new Vector2(-14f, -24f);

            objectivesText = textGo.GetComponent<TextMeshProUGUI>();
            objectivesText.fontSize = 10.5f;
            objectivesText.lineSpacing = 2;
            objectivesText.textWrappingMode = TextWrappingModes.Normal;
            objectivesText.alignment = TextAlignmentOptions.Left;
            objectivesText.raycastTarget = false;

            if (Starfall.Levels.MissionObjectiveManager.Instance != null)
            {
                Starfall.Levels.MissionObjectiveManager.Instance.OnObjectiveUpdated += (obj) => UpdateObjectivesHUD();
                Starfall.Levels.MissionObjectiveManager.Instance.OnObjectiveCompleted += (obj) => UpdateObjectivesHUD();
            }

            UpdateObjectivesHUD();
        }

        public void UpdateObjectivesHUD()
        {
            if (objectivesText == null) return;
            var manager = Starfall.Levels.MissionObjectiveManager.Instance;
            if (manager == null || manager.ActiveObjectives == null || manager.ActiveObjectives.Count == 0)
            {
                objectivesText.text = "<color=#778899>No active objectives</color>";
                return;
            }

            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < manager.ActiveObjectives.Count; i++)
            {
                var obj = manager.ActiveObjectives[i];
                sb.AppendLine(obj.GetStatusString());
            }
            objectivesText.text = sb.ToString().TrimEnd();
        }

        private void UpdateCombo(int kills, int multiplier)
        {
            if (comboText == null) return;
            bool show = kills >= 3 && !externalHudMode;
            comboText.gameObject.SetActive(show);
            if (!show) return;

            string color = multiplier >= 5 ? "#ff4dff" : multiplier >= 3 ? "#ffa64d" : "#ffe066";
            comboText.text = multiplier > 1
                ? $"<color={color}>COMBO x{multiplier}</color>  <size=16><color=#cccccc>{kills} CHAIN</color></size>"
                : $"<size=16><color=#cccccc>{kills} CHAIN</color></size>";
            comboPunch = 1f;
        }

        private void LateUpdate()
        {
            using var __perf = Starfall.Core.PerfMarkers.HudUpdate.Auto();
            if (comboText == null || !comboText.gameObject.activeSelf) return;
            comboPunch = Mathf.MoveTowards(comboPunch, 0f, Time.unscaledDeltaTime * 6f);
            comboText.rectTransform.localScale = Vector3.one * (1f + 0.18f * comboPunch);
            if (comboTimerFill != null && GameManager.Instance != null)
                comboTimerFill.rectTransform.sizeDelta = new Vector2(160f * GameManager.Instance.ComboTimeLeft01, 4f);
        }

        private void EnsureHomeButton()
        {
            if (homeButton != null)
            {
                homeButton.onClick.RemoveAllListeners();
                homeButton.onClick.AddListener(OnLevelSelectClicked);
                return;
            }

            var existingHome = transform.Find("HQHomeButton")?.GetComponent<UnityEngine.UI.Button>();
            if (existingHome != null)
            {
                homeButton = existingHome;
                homeButton.onClick.RemoveAllListeners();
                homeButton.onClick.AddListener(OnLevelSelectClicked);
                return;
            }

            // Procedurally create Home / HQ button in the top-right corner
            var btnGo = new GameObject("HQHomeButton");
            btnGo.transform.SetParent(transform, false);
            var rect = btnGo.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-20f, -16f);
            rect.sizeDelta = new Vector2(88f, 38f);

            var img = btnGo.AddComponent<UnityEngine.UI.Image>();
            homeButton = btnGo.AddComponent<UnityEngine.UI.Button>();
            SciFiUITheme.ApplyButton(homeButton, img, isPrimary: false);
            img.color = new Color(0.12f, 0.22f, 0.38f, 0.95f);
            homeButton.onClick.AddListener(OnLevelSelectClicked);

            var txtGo = new GameObject("Text");
            txtGo.transform.SetParent(btnGo.transform, false);
            var txtRect = txtGo.AddComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.sizeDelta = Vector2.zero;

            var txt = txtGo.AddComponent<TextMeshProUGUI>();
            txt.text = "<b>HQ</b>";
            txt.fontSize = 15;
            txt.fontStyle = FontStyles.Bold;
            txt.alignment = TextAlignmentOptions.Center;
            txt.color = new Color(0f, 0.90f, 1f);
        }

        private GameObject directivesModal;
        private TextMeshProUGUI directivesToggleText;
        private UnityEngine.UI.Button controlsButton;
        private bool wasTimePausedByDirectives = false;

        private void EnsureControlsButton()
        {
            if (controlsButton != null) return;

            var existing = transform.Find("ControlsButton")?.GetComponent<UnityEngine.UI.Button>();
            if (existing != null)
            {
                controlsButton = existing;
                controlsButton.onClick.RemoveAllListeners();
                controlsButton.onClick.AddListener(ToggleDirectivesModal);
                return;
            }

            var btnGo = new GameObject("ControlsButton");
            btnGo.transform.SetParent(transform, false);
            var rect = btnGo.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-124f, -16f);
            rect.sizeDelta = new Vector2(98f, 38f);

            var img = btnGo.AddComponent<UnityEngine.UI.Image>();
            controlsButton = btnGo.AddComponent<UnityEngine.UI.Button>();
            SciFiUITheme.ApplyButton(controlsButton, img, isPrimary: false);
            img.color = new Color(0.18f, 0.16f, 0.30f, 0.95f);
            controlsButton.onClick.AddListener(ToggleDirectivesModal);

            var txtGo = new GameObject("Text");
            txtGo.transform.SetParent(btnGo.transform, false);
            var txtRect = txtGo.AddComponent<RectTransform>();
            txtRect.anchorMin = Vector2.zero;
            txtRect.anchorMax = Vector2.one;
            txtRect.sizeDelta = Vector2.zero;

            var txt = txtGo.AddComponent<TextMeshProUGUI>();
            txt.text = "<b>? KEYS</b>";
            txt.fontSize = 14;
            txt.fontStyle = FontStyles.Bold;
            txt.alignment = TextAlignmentOptions.Center;
            txt.color = new Color(1f, 0.85f, 0.2f);
        }

        private void EnsureDirectivesUI()
        {
            if (directivesModal != null) return;

            var existing = transform.Find("DirectivesModal");
            if (existing != null)
            {
                directivesModal = existing.gameObject;
                return;
            }

            directivesModal = new GameObject("DirectivesModal");
            directivesModal.transform.SetParent(transform, false);
            var overlayRect = directivesModal.AddComponent<RectTransform>();
            overlayRect.anchorMin = Vector2.zero;
            overlayRect.anchorMax = Vector2.one;
            overlayRect.sizeDelta = Vector2.zero;

            var overlayBg = directivesModal.AddComponent<UnityEngine.UI.Image>();
            overlayBg.color = new Color(0.01f, 0.02f, 0.05f, 0.88f);

            // Allow tapping anywhere on the dark background to close the modal
            var overlayBtn = directivesModal.AddComponent<UnityEngine.UI.Button>();
            overlayBtn.onClick.AddListener(CloseDirectivesModal);

            // Center Frame Panel (740 x 530)
            var panelGo = new GameObject("Panel");
            panelGo.transform.SetParent(directivesModal.transform, false);
            var panelRect = panelGo.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.sizeDelta = new Vector2(740f, 530f);

            var panelBg = panelGo.AddComponent<UnityEngine.UI.Image>();
            SciFiUITheme.ApplyPanel(panelBg);

            // Header Banner
            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(panelGo.transform, false);
            var tr = titleGo.AddComponent<RectTransform>();
            tr.anchorMin = new Vector2(0.05f, 0.84f);
            tr.anchorMax = new Vector2(0.95f, 0.98f);
            tr.sizeDelta = Vector2.zero;
            var tTmp = titleGo.AddComponent<TextMeshProUGUI>();
            tTmp.text = "<size=23><color=#ffd700><b>MISSION FLIGHT DIRECTIVES</b></color></size>\n<size=12><color=#88bbdd>WARSHIP SYSTEM CONTROLS & COMBAT PROTOCOLS</color></size>";
            tTmp.alignment = TextAlignmentOptions.Center;

            // Directives Details
            var bodyGo = new GameObject("DirectivesText");
            bodyGo.transform.SetParent(panelGo.transform, false);
            var br = bodyGo.AddComponent<RectTransform>();
            br.anchorMin = new Vector2(0.06f, 0.20f);
            br.anchorMax = new Vector2(0.94f, 0.83f);
            br.sizeDelta = Vector2.zero;
            var bTmp = bodyGo.AddComponent<TextMeshProUGUI>();
            bTmp.fontSize = 13;
            bTmp.text =
                "<b><color=#00e5ff>1. FLIGHT PROPULSION & EVASION:</color></b>\n" +
                "• <b>[W] [A] [S] [D]</b>  or  <b>[ARROW KEYS]</b>  or  <b>[MOUSE DRAG]</b>\n" +
                "<color=#88bbdd>Thrust and bank your starfighter across space to dodge alien projectiles.</color>\n\n" +
                "<b><color=#00ff88>2. PRIMARY WEAPON CANNONS:</color></b>\n" +
                "• <b>[AUTOMATIC CONVERGENCE FIRE]</b>\n" +
                "<color=#88bbdd>Synchronized wing and center cannons continuously fire during flight.</color>\n\n" +
                "<b><color=#d444ff>3. TACTICAL SCREEN BOMB:</color></b>\n" +
                "• <b>[SPACEBAR]</b>  or  <b>[B KEY]</b>  or  <b>[RIGHT MOUSE CLICK]</b>\n" +
                "<color=#88bbdd>Detonates EMP warhead. Cancels all enemy bullets & blasts screen targets.</color>\n\n" +
                "<b><color=#ffaa00>4. SPECIAL WARSHIP ABILITY:</color></b>\n" +
                "• <b>[E KEY]</b>  or  <b>[HUD ABILITY BUTTON]</b>\n" +
                "<color=#88bbdd>Engages ship tactical power (Phase Shift / EMP Surge / Overdrive / Shield Burst).</color>\n\n" +
                "<b><color=#66aacc>5. COMMAND HQ / DIRECTIVES:</color></b>\n" +
                "• <b>[ESC KEY / ANDROID BACK / ❚❚ BUTTON]</b> Pause Menu (Resume / Restart / HQ)   •   <b>[H KEY]</b> Toggle Directives";

            // Bottom Left: Show before launch checkbox button
            var toggleBtnGo = new GameObject("ToggleShowBtn");
            toggleBtnGo.transform.SetParent(panelGo.transform, false);
            var tgr = toggleBtnGo.AddComponent<RectTransform>();
            tgr.anchorMin = new Vector2(0.06f, 0.05f);
            tgr.anchorMax = new Vector2(0.48f, 0.16f);
            tgr.sizeDelta = Vector2.zero;
            var tgImg = toggleBtnGo.AddComponent<UnityEngine.UI.Image>();
            var tgBtn = toggleBtnGo.AddComponent<UnityEngine.UI.Button>();
            SciFiUITheme.ApplyButton(tgBtn, tgImg, isPrimary: false);
            tgImg.color = new Color(0.12f, 0.18f, 0.30f, 0.95f);
            tgBtn.onClick.AddListener(ToggleShowDirectivesPreference);

            var tgTextGo = new GameObject("Text");
            tgTextGo.transform.SetParent(toggleBtnGo.transform, false);
            var tgtRect = tgTextGo.AddComponent<RectTransform>();
            tgtRect.anchorMin = Vector2.zero;
            tgtRect.anchorMax = Vector2.one;
            tgtRect.sizeDelta = Vector2.zero;
            directivesToggleText = tgTextGo.AddComponent<TextMeshProUGUI>();
            directivesToggleText.fontSize = 12;
            directivesToggleText.alignment = TextAlignmentOptions.Center;
            UpdateDirectivesToggleText();

            // Bottom Right: Launch Button
            var launchBtnGo = new GameObject("LaunchBtn");
            launchBtnGo.transform.SetParent(panelGo.transform, false);
            var lbr = launchBtnGo.AddComponent<RectTransform>();
            lbr.anchorMin = new Vector2(0.52f, 0.05f);
            lbr.anchorMax = new Vector2(0.94f, 0.16f);
            lbr.sizeDelta = Vector2.zero;
            var lbImg = launchBtnGo.AddComponent<UnityEngine.UI.Image>();
            var lbBtn = launchBtnGo.AddComponent<UnityEngine.UI.Button>();
            SciFiUITheme.ApplyButton(lbBtn, lbImg, isPrimary: true);
            lbBtn.onClick.AddListener(CloseDirectivesModal);
            var lbAnim = launchBtnGo.GetComponent<SciFiButtonAnimator>();
            if (lbAnim != null) lbAnim.PulseIdle = true;

            var lbTextGo = new GameObject("Text");
            lbTextGo.transform.SetParent(launchBtnGo.transform, false);
            var lbtRect = lbTextGo.AddComponent<RectTransform>();
            lbtRect.anchorMin = Vector2.zero;
            lbtRect.anchorMax = Vector2.one;
            lbtRect.sizeDelta = Vector2.zero;
            var lbTxt = lbTextGo.AddComponent<TextMeshProUGUI>();
            lbTxt.text = "<b>COMMENCE MISSION [SPACE]</b>";
            lbTxt.fontSize = 13;
            lbTxt.fontStyle = FontStyles.Bold;
            lbTxt.alignment = TextAlignmentOptions.Center;
            lbTxt.color = Color.white;

            directivesModal.SetActive(false);
        }

        private void UpdateDirectivesToggleText()
        {
            if (directivesToggleText == null) return;
            bool show = PlayerPrefs.GetInt("Starfall_ShowDirectives", 1) == 1;
            directivesToggleText.text = show
                ? "<color=#00ff88>[ON] SHOW BEFORE LAUNCH</color>"
                : "<color=#88aacc>[OFF] SHOW BEFORE LAUNCH</color>";
        }

        public void ToggleShowDirectivesPreference()
        {
            bool show = PlayerPrefs.GetInt("Starfall_ShowDirectives", 1) == 1;
            PlayerPrefs.SetInt("Starfall_ShowDirectives", show ? 0 : 1);
            PlayerPrefs.Save();
            UpdateDirectivesToggleText();
        }

        public void ShowDirectivesModal(bool pauseTime = true)
        {
            EnsureDirectivesUI();
            if (directivesModal != null)
            {
                directivesModal.SetActive(true);
                // Ensure the modal renders on top of everything else (like Mobile Controls)
                directivesModal.transform.SetAsLastSibling();
                
                if (pauseTime)
                {
                    wasTimePausedByDirectives = true;
                    Time.timeScale = 0f;
                }
            }
        }

        public void CloseDirectivesModal()
        {
            if (directivesModal != null)
            {
                directivesModal.SetActive(false);
            }

            if (wasTimePausedByDirectives)
            {
                wasTimePausedByDirectives = false;
                Time.timeScale = 1f;
            }
        }

        public void ToggleDirectivesModal()
        {
            EnsureDirectivesUI();
            if (directivesModal != null && directivesModal.activeSelf)
            {
                CloseDirectivesModal();
            }
            else
            {
                ShowDirectivesModal(true);
            }
        }

        private int currentLivesCached = 3;

        private void Update()
        {
            using var __perf = Starfall.Core.PerfMarkers.HudUpdate.Auto();
            // If Directives modal is active, Space / Enter / Escape starts/resumes mission
            if (directivesModal != null && directivesModal.activeSelf)
            {
                var keyboard = UnityEngine.InputSystem.Keyboard.current;
                if (keyboard != null && (keyboard.spaceKey.wasPressedThisFrame || keyboard.enterKey.wasPressedThisFrame || keyboard.escapeKey.wasPressedThisFrame))
                {
                    CloseDirectivesModal();
                    return;
                }
            }
            else
            {
                var keyboard = UnityEngine.InputSystem.Keyboard.current;
                if (keyboard != null && keyboard.hKey.wasPressedThisFrame)
                {
                    ToggleDirectivesModal();
                    return;
                }
            }

            // Keyboard shortcut to restart when Game Over screen is active
            if (gameOverPanel != null && gameOverPanel.activeSelf)
            {
                var keyboard = UnityEngine.InputSystem.Keyboard.current;
                if (keyboard != null)
                {
                    if (keyboard.spaceKey.wasPressedThisFrame ||
                        keyboard.enterKey.wasPressedThisFrame ||
                        keyboard.numpadEnterKey.wasPressedThisFrame ||
                        keyboard.rKey.wasPressedThisFrame)
                    {
                        OnRestartClicked();
                        return;
                    }
                }
            }
        }

        private void FindAndHookPlayer()
        {
            var playerObj = GameObject.FindWithTag("Player");
            if (playerObj == null)
            {
                playerObj = GameObject.Find("PlayerShip");
            }

            if (playerObj != null)
            {
                playerHealth = playerObj.GetComponent<PlayerHealth>();
                if (playerHealth != null)
                {
                    playerHealth.OnShieldChanged += UpdateShield;
                    playerHealth.OnHullChanged += UpdateHull;
                    playerHealth.OnLivesChanged += UpdateLives;
                    currentLivesCached = playerHealth.CurrentLives;
                    UpdateLives(playerHealth.CurrentLives, playerHealth.MaxLives);
                    UpdateShield(playerHealth.CurrentShield, playerHealth.MaxShield);
                    UpdateHull(playerHealth.CurrentHull, playerHealth.MaxHull);
                }

                playerCombat = playerObj.GetComponent<PlayerCombat>();
                if (playerCombat != null)
                {
                    playerCombat.OnWeaponLevelChanged += UpdateWeaponLevel;
                    playerCombat.OnBombsChanged += UpdateBombs;
                    playerCombat.OnAbilityCooldownChanged += UpdateAbilityUI;
                    UpdateWeaponLevel(playerCombat.WeaponLevel);
                    UpdateBombs(playerCombat.CurrentBombs);
                    UpdateAbilityUI(playerCombat.AbilityTimer, playerCombat.AbilityCooldown);
                }
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnScoreChanged -= UpdateScore;
                GameManager.Instance.OnGameStateChanged -= OnGameStateChanged;
                GameManager.Instance.OnComboChanged -= UpdateCombo;
            }

            if (WaveManager.Instance != null)
            {
                WaveManager.Instance.OnWaveStarted -= OnWaveStarted;
                WaveManager.Instance.OnWaveCleared -= OnWaveCleared;
                WaveManager.Instance.OnBossFightStarted -= OnBossFightStarted;
            }

            InvaderCarrier.OnPhaseChanged -= OnBossPhaseChanged;

            // BossEvents unsubscription
            BossEvents.OnBossSpawned -= HandleBossSpawned;
            BossEvents.OnBossHealthChanged -= HandleBossHealthChanged;
            BossEvents.OnBossPhaseChanged -= HandleBossPhaseChanged;
            BossEvents.OnBossDefeated -= HandleBossDefeated;

            if (Starfall.Levels.LevelManager.Instance != null)
            {
                Starfall.Levels.LevelManager.Instance.OnMissionCompleted -= OnMissionCompleted;
            }

            if (playerHealth != null)
            {
                playerHealth.OnShieldChanged -= UpdateShield;
                playerHealth.OnHullChanged -= UpdateHull;
                playerHealth.OnLivesChanged -= UpdateLives;
            }

            if (playerCombat != null)
            {
                playerCombat.OnWeaponLevelChanged -= UpdateWeaponLevel;
                playerCombat.OnBombsChanged -= UpdateBombs;
                playerCombat.OnAbilityCooldownChanged -= UpdateAbilityUI;
            }
        }

        private void OrganizeHUDLayout()
        {
            // 1. Ensure livesText exists as its own dedicated row
            if (livesText == null)
            {
                var existingLives = transform.Find("LivesText")?.GetComponent<TextMeshProUGUI>();
                if (existingLives != null)
                {
                    livesText = existingLives;
                }
                else
                {
                    var livesGo = new GameObject("LivesText");
                    livesGo.transform.SetParent(transform, false);
                    livesGo.AddComponent<RectTransform>();
                    livesText = livesGo.AddComponent<TextMeshProUGUI>();
                    if (shieldText != null)
                    {
                        livesText.font = shieldText.font;
                        livesText.material = shieldText.material;
                    }
                }
            }

            // 2. Format Top-Left player status column: Lives, Shield, Hull, Weapon
            if (livesText != null)
            {
                var rt = livesText.rectTransform;
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(25f, -15f);
                rt.sizeDelta = new Vector2(240f, 26f);
                livesText.fontSize = 20f;
                livesText.fontStyle = FontStyles.Bold;
                livesText.alignment = TextAlignmentOptions.Left;
            }

            bool hasRiveHealth = FindAnyObjectByType<RiveHealthHUD>() != null || GameObject.Find("RiveWidget_HealthHUD") != null;

            if (shieldText != null)
            {
                if (hasRiveHealth)
                {
                    shieldText.gameObject.SetActive(false);
                }
                else
                {
                    var rt = shieldText.rectTransform;
                    rt.anchorMin = new Vector2(0f, 1f);
                    rt.anchorMax = new Vector2(0f, 1f);
                    rt.pivot = new Vector2(0f, 1f);
                    rt.anchoredPosition = new Vector2(25f, -44f);
                    rt.sizeDelta = new Vector2(240f, 26f);
                    shieldText.fontSize = 20f;
                    shieldText.fontStyle = FontStyles.Bold;
                    shieldText.alignment = TextAlignmentOptions.Left;
                }
            }

            if (hullText != null)
            {
                if (hasRiveHealth)
                {
                    hullText.gameObject.SetActive(false);
                }
                else
                {
                    var rt = hullText.rectTransform;
                    rt.anchorMin = new Vector2(0f, 1f);
                    rt.anchorMax = new Vector2(0f, 1f);
                    rt.pivot = new Vector2(0f, 1f);
                    rt.anchoredPosition = new Vector2(25f, -73f);
                    rt.sizeDelta = new Vector2(240f, 26f);
                    hullText.fontSize = 20f;
                    hullText.fontStyle = FontStyles.Bold;
                    hullText.alignment = TextAlignmentOptions.Left;
                }
            }

            if (weaponLevelText != null)
            {
                var rt = weaponLevelText.rectTransform;
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = hasRiveHealth ? new Vector2(25f, -125f) : new Vector2(25f, -102f);
                rt.sizeDelta = new Vector2(260f, 26f);
                weaponLevelText.fontSize = 17f;
                weaponLevelText.fontStyle = FontStyles.Bold;
                weaponLevelText.alignment = TextAlignmentOptions.Left;
            }

            if (bombText != null)
            {
                var rt = bombText.rectTransform;
                rt.anchorMin = new Vector2(0f, 1f);
                rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = hasRiveHealth ? new Vector2(25f, -152f) : new Vector2(25f, -130f);
                rt.sizeDelta = new Vector2(260f, 26f);
                bombText.fontSize = 17f;
                bombText.fontStyle = FontStyles.Bold;
                bombText.alignment = TextAlignmentOptions.Left;
            }

            // 3. Format Top-Center: Score text
            if (scoreText != null)
            {
                var rt = scoreText.rectTransform;
                rt.anchorMin = new Vector2(0.5f, 1f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -12f);
                rt.sizeDelta = new Vector2(320f, 32f);
                scoreText.fontSize = 24f;
                scoreText.fontStyle = FontStyles.Bold;
                scoreText.alignment = TextAlignmentOptions.Center;
            }

            // 4. Format Top-Right: Wave text
            if (waveText != null)
            {
                var rt = waveText.rectTransform;
                rt.anchorMin = new Vector2(1f, 1f);
                rt.anchorMax = new Vector2(1f, 1f);
                rt.pivot = new Vector2(1f, 1f);
                rt.anchoredPosition = new Vector2(-20f, -112f);
                rt.sizeDelta = new Vector2(280f, 30f);
                waveText.fontSize = 18f;
                waveText.alignment = TextAlignmentOptions.Right;
            }
        }

        public void UpdateLives(int current, int max)
        {
            currentLivesCached = current;
            if (livesText != null)
            {
                string livesPrefix = $"x{current}";
                int layers = playerHealth != null ? playerHealth.ShieldLayers : 0;
                if (layers > 1)
                    livesText.text = $"LIVES: <color=#ff3366>{livesPrefix}</color>  <color=#00e5ff>SHIELD x{layers}</color>";
                else
                    livesText.text = $"LIVES: <color=#ff3366>{livesPrefix}</color>";
            }
            else if (shieldText != null && playerHealth != null)
            {
                // Refresh shieldText so lives show up
                UpdateShield(playerHealth.CurrentShield, playerHealth.MaxShield);
            }
        }

        public void UpdateScore(int score)
        {
            if (scoreText != null)
            {
                scoreText.text = $"SCORE: {score:N0}";
            }
        }

        public void UpdateShield(int current, int max)
        {
            int layers = max > 0 ? Mathf.CeilToInt((float)current / max) : 0;

            if (shieldText != null)
            {
                int barCount = Mathf.Clamp(current, 0, 8);
                string bars = new string('|', barCount);
                string layerSuffix = layers > 1 ? $" <color=#ffd700>(x{layers})</color>" : "";
                if (livesText == null)
                {
                    string livesStr = $"x{currentLivesCached}";
                    shieldText.text = $"LIVES: <color=#ff3366>{livesStr}</color>  SHIELD: <color=#00e5ff>{bars}</color>{layerSuffix}";
                }
                else
                {
                    if (current > 8)
                        shieldText.text = $"SHIELD: <color=#00e5ff>{current} [{bars}+]</color>{layerSuffix}";
                    else
                        shieldText.text = $"SHIELD: <color=#00e5ff>{bars}</color>{layerSuffix}";
                }
            }

            if (livesText != null)
            {
                string livesPrefix = $"x{currentLivesCached}";
                if (layers > 1)
                    livesText.text = $"LIVES: <color=#ff3366>{livesPrefix}</color>  <color=#00e5ff>SHIELD x{layers}</color>";
                else
                    livesText.text = $"LIVES: <color=#ff3366>{livesPrefix}</color>";
            }

            if (Starfall.UI.Rive.RiveAnimationManager.Instance != null && playerHealth != null)
            {
                Starfall.UI.Rive.RiveAnimationManager.Instance.UpdateSlayerHealth(playerHealth.CurrentHull, playerHealth.MaxHull, current, max);
            }
        }

        public void UpdateHull(int current, int max)
        {
            if (hullText != null)
            {
                int barCount = Mathf.Clamp(current, 0, 8);
                string bars = new string('|', barCount);
                if (current > 8)
                    hullText.text = $"HULL: <color=#00ff66>{current} [{bars}+]</color>";
                else
                    hullText.text = $"HULL: <color=#00ff66>{bars}</color>";
            }

            if (Starfall.UI.Rive.RiveAnimationManager.Instance != null && playerHealth != null)
            {
                Starfall.UI.Rive.RiveAnimationManager.Instance.UpdateSlayerHealth(current, max, playerHealth.CurrentShield, playerHealth.MaxShield);
            }
        }

        private GameObject scifiGameOverModalRoot;

        private void OnGameStateChanged(GameState state)
        {
            if (state == GameState.GameOver || state == GameState.Victory)
            {
                Time.timeScale = 0f; // Freeze game simulation
                ShowGameOverModal(state);
            }
        }

        private void ShowGameOverModal(GameState state)
        {
            EnsureEventSystem();
            SetGameplayControlsVisible(false);

            if (gameOverPanel == null)
            {
                gameOverPanel = new GameObject("GameOverModalRoot", typeof(RectTransform));
                gameOverPanel.transform.SetParent(transform, false);
                var r = (RectTransform)gameOverPanel.transform;
                r.anchorMin = Vector2.zero;
                r.anchorMax = Vector2.one;
                r.offsetMin = Vector2.zero;
                r.offsetMax = Vector2.zero;
            }

            // Guaranteed sorting and raycast priority over all in-game HUD & Rive layers
            var canvas = gameOverPanel.GetComponent<Canvas>();
            if (canvas == null) canvas = gameOverPanel.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            canvas.sortingOrder = 600; // Above PauseMenu (500) and Rive panels

            var raycaster = gameOverPanel.GetComponent<GraphicRaycaster>();
            if (raycaster == null) gameOverPanel.AddComponent<GraphicRaycaster>();

            gameOverPanel.transform.SetAsLastSibling();
            gameOverPanel.SetActive(true);

            // Deactivate any legacy scene objects inside gameOverPanel
            for (int i = 0; i < gameOverPanel.transform.childCount; i++)
            {
                var child = gameOverPanel.transform.GetChild(i);
                if (scifiGameOverModalRoot == null || child != scifiGameOverModalRoot.transform)
                {
                    child.gameObject.SetActive(false);
                }
            }

            if (scifiGameOverModalRoot != null)
            {
                Destroy(scifiGameOverModalRoot);
            }

            // Build Cybernetic Sci-Fi Modal Root
            scifiGameOverModalRoot = new GameObject("SciFiGameOverModalRoot", typeof(RectTransform));
            scifiGameOverModalRoot.transform.SetParent(gameOverPanel.transform, false);
            var rootRt = (RectTransform)scifiGameOverModalRoot.transform;
            rootRt.anchorMin = Vector2.zero;
            rootRt.anchorMax = Vector2.one;
            rootRt.offsetMin = Vector2.zero;
            rootRt.offsetMax = Vector2.zero;

            // 1. Fullscreen Dimming Glass Backdrop
            var bgGo = new GameObject("Backdrop", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            bgGo.transform.SetParent(scifiGameOverModalRoot.transform, false);
            var bgr = bgGo.GetComponent<RectTransform>();
            bgr.anchorMin = Vector2.zero; bgr.anchorMax = Vector2.one;
            bgr.offsetMin = Vector2.zero; bgr.offsetMax = Vector2.zero;
            var bgImg = bgGo.GetComponent<UnityEngine.UI.Image>();
            bgImg.color = new Color(0.015f, 0.03f, 0.06f, 0.92f);
            bgImg.raycastTarget = true;

            // 2. Central Modal Card (680 x 580)
            var cardGo = new GameObject("ModalCard", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            cardGo.transform.SetParent(scifiGameOverModalRoot.transform, false);
            var cardR = cardGo.GetComponent<RectTransform>();
            cardR.anchorMin = new Vector2(0.5f, 0.5f);
            cardR.anchorMax = new Vector2(0.5f, 0.5f);
            cardR.pivot = new Vector2(0.5f, 0.5f);
            cardR.anchoredPosition = Vector2.zero;
            cardR.sizeDelta = new Vector2(680f, 580f);
            var cardImg = cardGo.GetComponent<UnityEngine.UI.Image>();
            SciFiUITheme.ApplyPanel(cardImg, new Color(0.06f, 0.10f, 0.18f, 0.98f));

            // Corner Brackets for AAA aesthetic
            AddCornerBracket(cardGo.transform, new Vector2(0f, 1f), new Vector2(18f, -18f), 0f);
            AddCornerBracket(cardGo.transform, new Vector2(1f, 1f), new Vector2(-18f, -18f), -90f);
            AddCornerBracket(cardGo.transform, new Vector2(1f, 0f), new Vector2(-18f, 18f), -180f);
            AddCornerBracket(cardGo.transform, new Vector2(0f, 0f), new Vector2(18f, 18f), -270f);

            // 3. Top Hazard Tape Header Banner
            var bannerGo = new GameObject("HeaderBanner", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            bannerGo.transform.SetParent(cardGo.transform, false);
            var br = bannerGo.GetComponent<RectTransform>();
            br.anchorMin = new Vector2(0.5f, 0.5f);
            br.anchorMax = new Vector2(0.5f, 0.5f);
            br.pivot = new Vector2(0.5f, 0.5f);
            br.anchoredPosition = new Vector2(0f, 224f);
            br.sizeDelta = new Vector2(620f, 68f);
            var bImg = bannerGo.GetComponent<UnityEngine.UI.Image>();
            bImg.sprite = (state == GameState.GameOver) ? SciFiUITheme.HazardTapeRed : SciFiUITheme.HazardTapeYellow;
            bImg.type = UnityEngine.UI.Image.Type.Sliced;
            bImg.color = (state == GameState.GameOver) ? new Color(1f, 0.22f, 0.25f, 0.95f) : new Color(0f, 0.90f, 1f, 0.95f);

            var headerTextGo = new GameObject("HeaderText", typeof(RectTransform), typeof(TextMeshProUGUI));
            headerTextGo.transform.SetParent(bannerGo.transform, false);
            var htr = headerTextGo.GetComponent<RectTransform>();
            htr.anchorMin = Vector2.zero; htr.anchorMax = Vector2.one;
            htr.offsetMin = Vector2.zero; htr.offsetMax = Vector2.zero;
            var hTmp = headerTextGo.GetComponent<TextMeshProUGUI>();
            hTmp.alignment = TextAlignmentOptions.Center;
            hTmp.color = Color.white;
            if (state == GameState.GameOver)
            {
                hTmp.text = "<size=24><b>CRITICAL FAILURE // MISSION FAILED</b></size>\n<size=12><color=#FFAAAA>HULL INTEGRITY COMPROMISED • TELEMETRY TRANSMITTED</color></size>";
            }
            else
            {
                hTmp.text = "<size=24><b>SECTOR SECURED // MISSION COMPLETE</b></size>\n<size=12><color=#C8F5FF>HOSTILE ARMADA NEUTRALIZED • HYPERSPACE VECTOR ENGAGED</color></size>";
            }

            // 4. Tactical Debrief Section Box
            var debriefGo = new GameObject("DebriefBox", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            debriefGo.transform.SetParent(cardGo.transform, false);
            var dbr = debriefGo.GetComponent<RectTransform>();
            dbr.anchorMin = new Vector2(0.5f, 0.5f);
            dbr.anchorMax = new Vector2(0.5f, 0.5f);
            dbr.pivot = new Vector2(0.5f, 0.5f);
            dbr.anchoredPosition = new Vector2(0f, 76f);
            dbr.sizeDelta = new Vector2(610f, 155f);
            var dImg = debriefGo.GetComponent<UnityEngine.UI.Image>();
            SciFiUITheme.ApplyGlass(dImg, new Color(0.035f, 0.07f, 0.13f, 0.92f));

            int lvl = Starfall.Levels.LevelManager.Instance != null ? Starfall.Levels.LevelManager.Instance.CurrentLevelNumber : 2;
            string sectorName = lvl switch
            {
                1 => "SECTOR 01 // EARTH ORBIT",
                2 => "SECTOR 02 // ASTEROID BELT",
                3 => "SECTOR 03 // DEEP NEBULA",
                4 => "SECTOR 04 // WAR-TORN WRECKAGE",
                _ => $"SECTOR {lvl:00}"
            };
            string waveInfo = WaveManager.Instance != null ? $"WAVE {WaveManager.Instance.CurrentWaveNumber} / {WaveManager.Instance.TotalWaves}" : "COMBAT LOGGED";
            int currentScore = GameManager.Instance != null ? GameManager.Instance.CurrentScore : 0;
            int highScore = PlayerPrefs.GetInt("Starfall_HighScore", 0);
            bool isNewHigh = currentScore > highScore;
            if (isNewHigh) PlayerPrefs.SetInt("Starfall_HighScore", currentScore);

            string highLine = isNewHigh
                ? "<size=13><color=#00FF88><b>>> NEW ALL-TIME PERSONAL RECORD! <<</b></color></size>"
                : $"<size=12><color=#778899>RECORD: {Mathf.Max(highScore, currentScore):N0} PTS</color></size>";

            var dTxtGo = new GameObject("DebriefText", typeof(RectTransform), typeof(TextMeshProUGUI));
            dTxtGo.transform.SetParent(debriefGo.transform, false);
            var dtr = dTxtGo.GetComponent<RectTransform>();
            dtr.anchorMin = Vector2.zero; dtr.anchorMax = Vector2.one;
            dtr.offsetMin = new Vector2(16f, 10f); dtr.offsetMax = new Vector2(-16f, -10f);
            var dTmp = dTxtGo.GetComponent<TextMeshProUGUI>();
            dTmp.alignment = TextAlignmentOptions.Center;
            dTmp.text = $"<size=12><color=#66BBDD>{sectorName}</color>   •   <color=#00E5FF>{waveInfo}</color></size>\n" +
                        $"<size=13><color=#99BBDD>TACTICAL COMBAT SCORE</color></size>\n" +
                        $"<size=34><color=#FFD126><b>{currentScore:N0}</b></color></size> <size=16><color=#FFD126>PTS</color></size>\n" +
                        highLine;

            // 5. Action Buttons (Non-overlapping, proportional spacing)
            // Button 1: Retry / Play Again
            CreateModalButton(cardGo.transform,
                name: "RetryBtn",
                label: (state == GameState.Victory ? "<b>PLAY AGAIN</b>" : "<b>RETRY MISSION</b>"),
                sublabel: null,
                pos: new Vector2(0f, -44f),
                size: new Vector2(460f, 52f),
                tint: Color.white,
                isPrimary: true,
                pulse: true,
                onClick: OnRestartClicked);

            // Button 2: Emergency Reinforcements (+1 Life) - only on GameOver
            if (state == GameState.GameOver)
            {
                CreateModalButton(cardGo.transform,
                    name: "EmergencyLifeBtn",
                    label: "<b>EMERGENCY REINFORCEMENTS (+1 LIFE)</b>",
                    sublabel: "<size=10><color=#99FFCC>INSTANT FIELD REVIVE • FULL SHIELDS • 3S INVULNERABILITY</color></size>",
                    pos: new Vector2(0f, -112f),
                    size: new Vector2(460f, 50f),
                    tint: new Color(0.12f, 0.85f, 0.50f, 0.95f),
                    isPrimary: false,
                    pulse: true,
                    onClick: RevivePlayer);
            }

            // Button 3: Return to Command HQ
            CreateModalButton(cardGo.transform,
                name: "HomeBtn",
                label: "<b>RETURN TO COMMAND HQ</b>",
                sublabel: null,
                pos: new Vector2(0f, (state == GameState.GameOver ? -180f : -128f)),
                size: new Vector2(460f, 50f),
                tint: new Color(0.20f, 0.32f, 0.50f, 0.95f),
                isPrimary: false,
                pulse: false,
                onClick: OnReturnToHQClicked);
        }

        private void CreateModalButton(Transform parent, string name, string label, string sublabel, Vector2 pos, Vector2 size, Color tint, bool isPrimary, bool pulse, System.Action onClick)
        {
            var btnGo = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button));
            btnGo.transform.SetParent(parent, false);
            var r = btnGo.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0.5f, 0.5f);
            r.anchorMax = new Vector2(0.5f, 0.5f);
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = pos;
            r.sizeDelta = size;

            var img = btnGo.GetComponent<UnityEngine.UI.Image>();
            var btn = btnGo.GetComponent<UnityEngine.UI.Button>();
            SciFiUITheme.ApplyButton(btn, img, isPrimary: isPrimary);
            if (!isPrimary) img.color = tint;

            var anim = btnGo.GetComponent<SciFiButtonAnimator>();
            if (anim != null) anim.PulseIdle = pulse;

            btn.onClick.AddListener(() =>
            {
                Starfall.Audio.AudioManager.Instance?.PlayPlayerLaser();
                onClick?.Invoke();
            });

            var txtGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            txtGo.transform.SetParent(btnGo.transform, false);
            var tr = txtGo.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one;
            tr.offsetMin = new Vector2(8f, 2f); tr.offsetMax = new Vector2(-8f, -2f);
            var tmp = txtGo.GetComponent<TextMeshProUGUI>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.Normal;
            tmp.color = Color.white;
            tmp.fontSize = string.IsNullOrEmpty(sublabel) ? 18f : 14f;
            tmp.text = string.IsNullOrEmpty(sublabel) ? label : $"{label}\n{sublabel}";
        }

        private void AddCornerBracket(Transform parent, Vector2 anchor, Vector2 pos, float zRot)
        {
            var go = new GameObject("CornerBracket", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = anchor;
            r.anchorMax = anchor;
            r.pivot = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = pos;
            r.sizeDelta = new Vector2(16f, 16f);
            r.localEulerAngles = new Vector3(0f, 0f, zRot);
            var img = go.GetComponent<UnityEngine.UI.Image>();
            img.sprite = SciFiUITheme.CornerBracket;
            img.color = new Color(0f, 0.90f, 1f, 0.85f);
            img.raycastTarget = false;
        }

        private void SetGameplayControlsVisible(bool visible)
        {
            // Tactical flight trackpad
            if (TacticalFlightTrackpad.Instance != null)
            {
                TacticalFlightTrackpad.Instance.gameObject.SetActive(visible);
            }

            // Rive HUD and Special Ability button
            if (Starfall.UI.Rive.RiveAnimationManager.Instance != null)
            {
                Starfall.UI.Rive.RiveAnimationManager.Instance.gameObject.SetActive(visible);
            }
            var customRiveSpecial = FindAnyObjectByType<RiveSpecialButton>();
            if (customRiveSpecial != null)
            {
                customRiveSpecial.gameObject.SetActive(visible);
            }

            // Mobile controls if present
            var mobileMgr = MobileControlsManager.Instance ?? FindAnyObjectByType<MobileControlsManager>();
            if (mobileMgr != null)
            {
                mobileMgr.gameObject.SetActive(visible);
            }

            // Pause button on PauseMenu
            if (PauseMenu.Instance != null)
            {
                var pb = PauseMenu.Instance.transform.Find("PauseButton");
                if (pb != null) pb.gameObject.SetActive(visible && !externalHudMode);
            }

            // In-game HUD widgets
            if (objectivesContainer != null) objectivesContainer.SetActive(visible);
            if (comboText != null && !visible) comboText.gameObject.SetActive(false);
            if (controlsButton != null) controlsButton.gameObject.SetActive(visible);
            if (homeButton != null) homeButton.gameObject.SetActive(visible);
            if (bombButton != null) bombButton.gameObject.SetActive(visible && !externalHudMode);
        }

        /// <summary>
        /// Hands the stat readouts (score, hull, shield, lives, wave, weapon, bombs, combo) and the
        /// bomb / pause buttons over to an external HUD such as the Rive StarfallHudController.
        /// Announcements, objectives, the boss bar and the game-over modal stay here.
        /// </summary>
        public void SetExternalHudMode(bool enabled)
        {
            externalHudMode = enabled;
            bool show = !enabled;
            foreach (var t in new[] { scoreText, shieldText, hullText, livesText, waveText, weaponLevelText, bombText })
                if (t != null) t.gameObject.SetActive(show);
            if (comboText != null && enabled) comboText.gameObject.SetActive(false);
            if (bombButton != null) bombButton.gameObject.SetActive(show);
            if (PauseMenu.Instance != null)
            {
                var pb = PauseMenu.Instance.transform.Find("PauseButton");
                if (pb != null) pb.gameObject.SetActive(show);
            }

            // Drop Home / Controls below the external HUD's top-right panel.
            float y = enabled ? -196f : -16f;
            if (homeButton != null) ((RectTransform)homeButton.transform).anchoredPosition = new Vector2(-20f, y);
            if (controlsButton != null) ((RectTransform)controlsButton.transform).anchoredPosition = new Vector2(-124f, y);
        }

        private void RevivePlayer()
        {
            if (scifiGameOverModalRoot != null) scifiGameOverModalRoot.SetActive(false);
            if (gameOverPanel != null) gameOverPanel.SetActive(false);

            SetGameplayControlsVisible(true);
            Time.timeScale = 1f;

            if (playerHealth == null)
            {
                playerHealth = FindAnyObjectByType<PlayerHealth>(FindObjectsInactive.Include);
            }

            if (playerHealth != null)
            {
                playerHealth.Revive(1);
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.SetState(GameState.Playing);
            }

            Starfall.Audio.AudioManager.Instance?.PlayPowerUp();
            ShowAnnouncement("EMERGENCY REINFORCEMENTS ENGAGED!", new Color(0.2f, 1f, 0.5f));
        }

        public void OnRestartClicked()
        {
            Time.timeScale = 1f;
            if (GameManager.Instance != null)
            {
                GameManager.Instance.RestartGame();
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
            }
        }

        public void OnReturnToHQClicked()
        {
            Time.timeScale = 1f;
            Starfall.Levels.LevelProgressManager.LoadHomeScene();
        }

        public void OnLevelSelectClicked()
        {
            Time.timeScale = 1f;
            Starfall.Levels.LevelProgressManager.LoadLevelSelect();
        }

        private void OnWaveStarted(int waveNumber, int total)
        {
            int levelNum = Starfall.Levels.LevelManager.Instance != null ? Starfall.Levels.LevelManager.Instance.CurrentLevelNumber : 1;
            if (waveText != null)
            {
                waveText.color = Color.white;
                waveText.text = $"LEVEL {levelNum}  •  WAVE {waveNumber} / {total}";
            }

            ShowAnnouncement($"WAVE {waveNumber}", new Color(0f, 0.9f, 1f));
        }

        private void OnWaveCleared(int waveNumber)
        {
            ShowAnnouncement("WAVE CLEAR!", new Color(0.4f, 1f, 0.4f));
        }

        private void OnBossFightStarted()
        {
            // Handled via HandleBossSpawned, but keep fallback
            if (waveText != null)
            {
                waveText.text = "!! BOSS INCOMING !!";
                waveText.color = new Color(1f, 0.2f, 0.2f);
            }
        }

        private void OnBossPhaseChanged(int phase)
        {
            // Handled via HandleBossPhaseChanged, legacy fallback
            string[] phaseMessages = { "", "PHASE 1 – TURRETS!", "PHASE 2 – SCOUT BAY OPEN!", "PHASE 3 – CORE EXPOSED!" };
            Color[] phaseColors = { Color.white, new Color(0.2f, 0.8f, 1f), new Color(1f, 0.65f, 0.1f), new Color(1f, 0.25f, 0.25f) };

            if (phase >= 1 && phase <= 3)
                ShowAnnouncement(phaseMessages[phase], phaseColors[phase]);
        }

        private void EnsureBossBarUI()
        {
            Transform existing = transform.Find("BossBarContainer");
            if (existing != null)
            {
                bossBarContainer = existing.gameObject;
                bossBarFill = existing.Find("Fill")?.GetComponent<UnityEngine.UI.Image>();
                bossNameText = existing.Find("BossName")?.GetComponent<TextMeshProUGUI>();
            }
            else
            {
                // Procedurally create Boss Bar UI
                bossBarContainer = new GameObject("BossBarContainer");
                bossBarContainer.transform.SetParent(transform, false);

                var rect = bossBarContainer.AddComponent<RectTransform>();

                // Dark background frame
                var bgImg = bossBarContainer.AddComponent<UnityEngine.UI.Image>();
                bgImg.sprite = SciFiUITheme.GaugeTrack;
                bgImg.type = UnityEngine.UI.Image.Type.Sliced;
                bgImg.color = new Color(0.12f, 0.16f, 0.25f, 0.95f);

                // Inner fill
                var fillGo = new GameObject("Fill");
                fillGo.transform.SetParent(bossBarContainer.transform, false);
                var fillRect = fillGo.AddComponent<RectTransform>();
                fillRect.anchorMin = Vector2.zero;
                fillRect.anchorMax = Vector2.one;
                fillRect.offsetMin = new Vector2(2f, 2f);
                fillRect.offsetMax = new Vector2(-2f, -2f);

                bossBarFill = fillGo.AddComponent<UnityEngine.UI.Image>();
                bossBarFill.sprite = SciFiUITheme.GaugeFill;
                bossBarFill.type = UnityEngine.UI.Image.Type.Filled;
                bossBarFill.fillMethod = UnityEngine.UI.Image.FillMethod.Horizontal;
                bossBarFill.fillOrigin = 0; // Left
                bossBarFill.fillAmount = 1f;
                bossBarFill.color = new Color(1f, 0.22f, 0.25f);

                // Boss name text above the bar
                var nameGo = new GameObject("BossName");
                nameGo.transform.SetParent(bossBarContainer.transform, false);
                nameGo.AddComponent<RectTransform>();

                bossNameText = nameGo.AddComponent<TextMeshProUGUI>();
                bossNameText.text = "BOSS";

                bossBarContainer.SetActive(false);
            }

            if (bossBarContainer != null)
            {
                var br = bossBarContainer.GetComponent<RectTransform>();
                if (br != null)
                {
                    br.anchorMin = new Vector2(0.5f, 1f);
                    br.anchorMax = new Vector2(0.5f, 1f);
                    br.pivot = new Vector2(0.5f, 1f);
                    br.anchoredPosition = new Vector2(0f, -72f);
                    br.sizeDelta = new Vector2(460f, 20f);
                }
            }

            if (bossNameText != null)
            {
                var nr = bossNameText.rectTransform;
                nr.anchorMin = new Vector2(0.5f, 1f);
                nr.anchorMax = new Vector2(0.5f, 1f);
                nr.pivot = new Vector2(0.5f, 0f);
                nr.anchoredPosition = new Vector2(0f, 3f);
                nr.sizeDelta = new Vector2(460f, 22f);
                bossNameText.alignment = TextAlignmentOptions.Center;
                bossNameText.fontSize = 17f;
                bossNameText.fontStyle = FontStyles.Bold;
                bossNameText.color = new Color(1f, 0.85f, 0.3f);
            }
        }

        private void HandleBossSpawned(string bossName, int currentHp, int maxHp)
        {
            EnsureBossBarUI();
            if (bossBarContainer != null)
            {
                bossBarContainer.SetActive(true);
            }

            if (bossNameText != null)
            {
                bossNameText.text = bossName.ToUpper();
            }

            if (bossBarFill != null)
            {
                bossBarFill.fillAmount = maxHp > 0 ? Mathf.Clamp01((float)currentHp / maxHp) : 1f;
            }

            // When the Rive boss bar is live, keep only the name label here so bars don't stack.
            bool riveBar = Starfall.UI.Rive.RiveAnimationManager.Instance != null && Starfall.UI.Rive.RiveAnimationManager.Instance.IsBossBarRive;
            if (bossBarContainer != null)
            {
                var bg = bossBarContainer.GetComponent<UnityEngine.UI.Image>();
                if (bg != null) bg.enabled = !riveBar;
            }
            if (bossBarFill != null) bossBarFill.enabled = !riveBar;

            if (waveText != null)
            {
                waveText.text = "!! BOSS INCOMING !!";
                waveText.color = new Color(1f, 0.25f, 0.25f);
            }

            string bName = string.IsNullOrEmpty(bossName) ? "BOSS" : bossName.ToUpperInvariant();
            ShowAnnouncement($"WARNING: {bName}!", new Color(1f, 0.2f, 0.2f));
        }

        private void HandleBossHealthChanged(int currentHp, int maxHp)
        {
            if (bossBarFill != null)
            {
                bossBarFill.fillAmount = maxHp > 0 ? Mathf.Clamp01((float)currentHp / maxHp) : 0f;
            }
        }

        private void HandleBossPhaseChanged(string phaseName, Color alertColor)
        {
            ShowAnnouncement(phaseName.ToUpper(), alertColor);
        }

        private void HandleBossDefeated(string bossName)
        {
            if (bossBarContainer != null)
            {
                bossBarContainer.SetActive(false);
            }

            string title = string.IsNullOrEmpty(bossName) ? "BOSS" : bossName.ToUpper();
            ShowAnnouncement($"{title} DESTROYED!", new Color(0.2f, 1f, 0.35f));
        }

        private void OnMissionCompleted(Starfall.Levels.MissionData mission, int earnedCredits)
        {
            string name = mission != null ? mission.DisplayName : "MISSION";
            ShowAnnouncement($"{name.ToUpper()} CLEARED! +{earnedCredits} CREDITS", new Color(0.3f, 1f, 0.5f));
        }

        public void ShowAnnouncement(string message, Color color)
        {
            if (waveAnnouncementText == null) return;
            StopCoroutine(nameof(AnnouncementRoutine));
            StartCoroutine(AnnouncementRoutine(message, color));
        }

        private IEnumerator AnnouncementRoutine(string message, Color color)
        {
            waveAnnouncementText.text = message;
            waveAnnouncementText.color = color;
            waveAnnouncementText.gameObject.SetActive(true);

            float elapsed = 0f;
            while (elapsed < announcementDuration)
            {
                float alpha = 1f - Mathf.Pow(elapsed / announcementDuration, 2f);
                var c = color;
                c.a = alpha;
                waveAnnouncementText.color = c;
                elapsed += Time.deltaTime;
                yield return null;
            }

            waveAnnouncementText.gameObject.SetActive(false);
        }

        public void UpdateWeaponLevel(int level)
        {
            if (weaponLevelText != null)
            {
                int maxPips = 5;
                int filled = Mathf.Clamp(level, 0, maxPips);
                string pips = new string('|', filled);
                string emptyPips = new string('.', Mathf.Max(0, maxPips - filled));
                weaponLevelText.text = $"WEAPON: <color=#FFD126>LV.{level}</color> <color=#00E5FF>[{pips}</color><color=#335577>{emptyPips}]</color>";
            }
        }

        public void UpdateBombs(int bombs)
        {
            if (bombText != null)
            {
                string icons = bombs <= 4 ? new string('|', bombs) : $"{bombs}";
                bombText.text = $"BOMBS: <color=#D444FF><b>[{icons}]</b></color>";
            }
        }

        public void OnBombButtonClicked()
        {
            if (playerCombat != null)
            {
                playerCombat.UseBomb();
            }
        }

        private void EnsureMobileControls()
        {
            // Spawn the new MobileControlsManager which handles joystick, bomb, ability,
            // auto-fire, pause, and the drag-to-reposition settings panel.
            if (MobileControlsManager.Instance != null) return;

            var existing = transform.Find("MobileControls");
            if (existing != null && existing.GetComponent<MobileControlsManager>() != null) return;

            var go = new GameObject("MobileControls");
            go.transform.SetParent(transform, false);
            // Stretch to fill the HUD canvas
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            go.AddComponent<MobileControlsManager>();

            // Keep a VirtualJoystick singleton shim so PlayerController works unchanged
            if (VirtualJoystick.Instance == null)
            {
                var vjGo = new GameObject("VirtualJoystick_Shim");
                vjGo.transform.SetParent(go.transform, false);
                vjGo.AddComponent<VirtualJoystick>();
            }
        }

        private void EnsureAbilityButton()
        {
            // Destroy any legacy rectangular SpecialAbilityButton bar so only Rive Special Button is shown
            var existing = transform.Find("SpecialAbilityButton");
            if (existing != null)
            {
                Destroy(existing.gameObject);
            }
        }

        public void UpdateAbilityUI(float current, float max)
        {
            if (max <= 0f) return;
            float ratio = Mathf.Clamp01(current / max);

            string shipAbility = (playerCombat != null && playerCombat.ActiveShip != null)
                ? playerCombat.ActiveShip.abilityName : "ABILITY";

            // Forward to Rive Special Button
            if (_cachedRiveSpecial == null)
            {
                _cachedRiveSpecial = FindAnyObjectByType<RiveSpecialButton>();
            }
            if (_cachedRiveSpecial != null)
            {
                _cachedRiveSpecial.SetCooldownPercent(ratio * 100f);
            }

            // Forward to the mobile controls manager
            MobileControlsManager.Instance?.UpdateAbilityCooldown(current, max, shipAbility);

            if (Starfall.UI.Rive.RiveAnimationManager.Instance != null)
                Starfall.UI.Rive.RiveAnimationManager.Instance.UpdateGauge(current, max, shipAbility);
        }

        public void OnAbilityButtonClicked()
        {
            if (playerCombat != null)
            {
                playerCombat.ActivateSpecialAbility();
            }
        }
    }
}
