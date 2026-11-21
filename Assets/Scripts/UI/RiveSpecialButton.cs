using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using TMPro;
using Rive;
using Rive.Components;
using Starfall.Player;

namespace Starfall.UI
{
    /// <summary>
    /// Controller for the Rive "Special Ability" / Bomb button (specialbutton.riv).
    /// Listens for the "activated" trigger from Rive, triggers OnAbilityUsed event & bomb,
    /// sets cooldown to 0 on activation, and smoothly eases it back to 100 over cooldownDuration.
    /// Equipped with a full-surface mobile touch overlay to guarantee 100% responsiveness on Android APK touchscreens.
    /// </summary>
    [DisallowMultipleComponent]
    public class RiveSpecialButton : MonoBehaviour, IPointerClickHandler, IPointerDownHandler
    {
        [Header("Components")]
        [Tooltip("The RiveWidget displaying specialbutton.riv.")]
        public RiveWidget widget;

        [Header("Cooldown Settings")]
        [Tooltip("Cooldown duration in seconds to recover from 0 to 100.")]
        [SerializeField] private float cooldownDuration = 8.0f;

        [Tooltip("Current cooldown progress (0 - 100). 100 = Ready.")]
        [SerializeField] private float currentCooldown = 100f;

        [Header("Position & Scaling (Canvas 1080x1920)")]
        [Tooltip("If true, automatically anchors the widget to bottom-right.")]
        public bool autoPositionAndScale = true;

        [Tooltip("Anchor position relative to Bottom-Right.")]
        public Vector2 buttonPosition = new Vector2(-150f, 160f);

        [Tooltip("Display size of the button widget. Default is 250x250.")]
        public Vector2 buttonSize = new Vector2(250f, 250f);

        [Header("Gameplay Integration")]
        [Tooltip("If true, triggers PlayerCombat.UseBomb() when the button is activated.")]
        [SerializeField] private bool triggerPlayerBomb = true;

        [Tooltip("If true, displays a visual flash indicator on screen when activated.")]
        [SerializeField] private bool showVisualPlaceholder = true;

        [Header("Events")]
        public UnityEvent OnAbilityUsed = new UnityEvent();

        private bool _isLoaded = false;
        private bool _isCoolingDown = false;
        private Coroutine _cooldownCoroutine;
        private ViewModelInstanceTriggerProperty _activatedTriggerProp;
        private GameObject _placeholderGo;
        private TextMeshProUGUI _placeholderText;

        // Mobile touch overlay components
        private GameObject _touchOverlayGo;
        private UnityEngine.UI.Button _touchOverlayBtn;
        private UnityEngine.UI.Image _touchOverlayImg;
        private float _lastActivationTime = -1f;

        public float Cooldown => currentCooldown;
        public bool IsReady => currentCooldown >= 99.9f && !_isCoolingDown;
        public bool IsLoaded => _isLoaded;
        public float CooldownDuration
        {
            get => cooldownDuration;
            set => cooldownDuration = Mathf.Max(0.1f, value);
        }

        private void Awake()
        {
            var rogueHealthHud = GetComponent<RiveHealthHUD>();
            if (rogueHealthHud != null)
            {
                Destroy(rogueHealthHud);
            }
            EnsureWidgetReference();
            ApplyLayout();
            EnsureTouchOverlay();
            CreateVisualPlaceholder();
            HideLegacyBombButtons();
        }

        private void HideLegacyBombButtons()
        {
            var oldBombBtn = GameObject.Find("BombBtn");
            if (oldBombBtn != null) oldBombBtn.SetActive(false);
            var hud = FindAnyObjectByType<GameplayHUD>();
            if (hud != null)
            {
                var bombBtn = hud.transform.Find("BombButton");
                if (bombBtn != null) bombBtn.gameObject.SetActive(false);
            }
        }

        private PlayerCombat _playerCombat;

        private void OnEnable()
        {
            EnsureWidgetReference();
            ApplyLayout();
            EnsureTouchOverlay();
            HookPlayerCombat();
            if (_isLoaded && widget != null && widget.StateMachine != null)
            {
                SubscribeToTrigger();
            }
        }

        private void OnDisable()
        {
            UnsubscribeFromTrigger();
            UnhookPlayerCombat();
            if (_cooldownCoroutine != null)
            {
                StopCoroutine(_cooldownCoroutine);
                _cooldownCoroutine = null;
            }
        }

        private void OnDestroy()
        {
            UnsubscribeFromTrigger();
            UnhookPlayerCombat();
        }

        private float _combatCheckTimer = 0f;

        private void Update()
        {
            if (_playerCombat == null)
            {
                _combatCheckTimer += Time.deltaTime;
                if (_combatCheckTimer >= 0.5f)
                {
                    _combatCheckTimer = 0f;
                    HookPlayerCombat();
                }
            }
        }

        private void HookPlayerCombat()
        {
            if (_playerCombat == null)
            {
                _playerCombat = FindAnyObjectByType<PlayerCombat>();
            }
            if (_playerCombat != null)
            {
                _playerCombat.OnBombsChanged -= HandleBombsChanged;
                _playerCombat.OnBombsChanged += HandleBombsChanged;
                _playerCombat.OnAbilityCooldownChanged -= HandleAbilityCooldownChanged;
                _playerCombat.OnAbilityCooldownChanged += HandleAbilityCooldownChanged;
                HandleBombsChanged(_playerCombat.CurrentBombs);
                HandleAbilityCooldownChanged(_playerCombat.AbilityTimer, _playerCombat.AbilityCooldown);
            }
        }

        private void UnhookPlayerCombat()
        {
            if (_playerCombat != null)
            {
                _playerCombat.OnBombsChanged -= HandleBombsChanged;
                _playerCombat.OnAbilityCooldownChanged -= HandleAbilityCooldownChanged;
                _playerCombat = null;
            }
        }

        private void HandleAbilityCooldownChanged(float current, float max)
        {
            if (max <= 0f) return;
            float pct = Mathf.Clamp01(current / max) * 100f;
            SetCooldownPercent(pct);
        }

        public void SetCooldownPercent(float pct)
        {
            pct = Mathf.Clamp(pct, 0f, 100f);
            currentCooldown = pct;
            SetCooldownProperty(pct);
            bool ready = pct >= 99.5f;
            _isCoolingDown = !ready;
            UpdateTouchOverlayState(ready);
        }

        private void HandleBombsChanged(int bombCount)
        {
            // The button fires the ship's tactical ability (e.g. Supernova Flare)
            // as well as bombs, so we do not lock it when bombs reach 0.
        }

        private void EnsureTouchOverlay()
        {
            if (_touchOverlayGo == null)
            {
                var existing = transform.Find("TouchOverlay");
                if (existing != null)
                {
                    _touchOverlayGo = existing.gameObject;
                }
                else
                {
                    _touchOverlayGo = new GameObject("TouchOverlay", typeof(RectTransform));
                    _touchOverlayGo.transform.SetParent(transform, false);
                }

                var rect = _touchOverlayGo.GetComponent<RectTransform>();
                if (rect == null)
                {
                    rect = _touchOverlayGo.AddComponent<RectTransform>();
                }
                if (rect != null)
                {
                    rect.anchorMin = Vector2.zero;
                    rect.anchorMax = Vector2.one;
                    rect.offsetMin = Vector2.zero;
                    rect.offsetMax = Vector2.zero;
                    rect.pivot = new Vector2(0.5f, 0.5f);
                }

                _touchOverlayImg = _touchOverlayGo.GetComponent<UnityEngine.UI.Image>();
                if (_touchOverlayImg == null)
                {
                    _touchOverlayImg = _touchOverlayGo.AddComponent<UnityEngine.UI.Image>();
                }
                if (_touchOverlayImg != null)
                {
                    _touchOverlayImg.color = UnityEngine.Color.clear;
                    _touchOverlayImg.raycastTarget = true;
                }

                _touchOverlayBtn = _touchOverlayGo.GetComponent<UnityEngine.UI.Button>();
                if (_touchOverlayBtn == null)
                {
                    _touchOverlayBtn = _touchOverlayGo.AddComponent<UnityEngine.UI.Button>();
                }
                if (_touchOverlayBtn != null)
                {
                    _touchOverlayBtn.transition = UnityEngine.UI.Selectable.Transition.None;
                    _touchOverlayBtn.onClick.RemoveAllListeners();
                    _touchOverlayBtn.onClick.AddListener(OnTouchOverlayClicked);
                }
            }
        }

        private void UpdateTouchOverlayState(bool interactable)
        {
            if (_touchOverlayBtn != null)
            {
                _touchOverlayBtn.interactable = interactable;
            }
        }

        private void OnValidate()
        {
            if (autoPositionAndScale)
            {
                ApplyLayout();
            }
        }

        public void ApplyLayout()
        {
            if (widget == null)
            {
                widget = GetComponent<RiveWidget>() ?? GetComponentInChildren<RiveWidget>();
            }

            if (autoPositionAndScale && widget != null)
            {
                var rect = widget.GetComponent<RectTransform>();
                if (rect != null)
                {
                    rect.anchorMin = new Vector2(1f, 0f);
                    rect.anchorMax = new Vector2(1f, 0f);
                    rect.pivot = new Vector2(0.5f, 0.5f);
                    rect.anchoredPosition = buttonPosition;
                    rect.sizeDelta = buttonSize;
                }
            }
        }

        private void EnsureWidgetReference()
        {
            if (widget == null)
            {
                widget = GetComponent<RiveWidget>();
                if (widget == null)
                {
                    widget = GetComponentInChildren<RiveWidget>();
                }
            }
        }

        private IEnumerator Start()
        {
            EnsureWidgetReference();

            if (widget == null)
            {
                Debug.LogWarning("[RiveSpecialButton] No RiveWidget found on or under this GameObject.");
                yield break;
            }

            // Ensure HitTestBehavior is Opaque so it receives touch/mouse clicks
            widget.HitTestBehavior = HitTestBehavior.Opaque;

            // Ensure parent RivePanel has multitouch and PointerInputMode enabled
            var panel = widget.GetComponentInParent<RivePanel>();
            if (panel != null)
            {
                var canvasRenderer = panel.GetComponent<RiveCanvasRenderer>();
                if (canvasRenderer != null)
                {
                    canvasRenderer.PointerInputMode = PointerInputMode.EnablePointerInput;
                }
            }

            // Wait until Rive finishes loading the asset, artboard and viewmodel
            float timeout = 8f;
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

            if (widget != null && widget.StateMachine != null)
            {
                _isLoaded = true;
                SubscribeToTrigger();

                HookPlayerCombat();
                var combat = _playerCombat != null ? _playerCombat : FindAnyObjectByType<PlayerCombat>();
                EnsureTouchOverlay();
                if (combat != null)
                {
                    cooldownDuration = combat.AbilityCooldown;
                    float pct = combat.AbilityCooldown > 0f ? Mathf.Clamp01(combat.AbilityTimer / combat.AbilityCooldown) * 100f : 100f;
                    currentCooldown = pct;
                    SetCooldownProperty(pct);
                    bool ready = pct >= 95f;
                    if (widget != null) widget.HitTestBehavior = ready ? HitTestBehavior.Opaque : HitTestBehavior.None;
                    UpdateTouchOverlayState(ready);
                }
                else
                {
                    currentCooldown = 100f;
                    SetCooldownProperty(100f);
                    if (widget != null) widget.HitTestBehavior = HitTestBehavior.Opaque;
                    UpdateTouchOverlayState(true);
                }
                Debug.Log($"[RiveSpecialButton] SpecialButton Rive widget initialized (cooldown = {currentCooldown}).");
            }
            else
            {
                Debug.LogWarning("[RiveSpecialButton] Timed out waiting for Rive StateMachine to initialize.");
            }
        }

        private void SubscribeToTrigger()
        {
            UnsubscribeFromTrigger();

            if (widget == null || widget.StateMachine == null) return;

            var sm = widget.StateMachine;
            if (sm.ViewModelInstance != null)
            {
                try
                {
                    _activatedTriggerProp = sm.ViewModelInstance.GetTriggerProperty("activated");
                    if (_activatedTriggerProp != null)
                    {
                        _activatedTriggerProp.OnTriggered += HandleActivated;
                        Debug.Log("[RiveSpecialButton] Subscribed to ViewModel 'activated' trigger.");
                    }
                }
                catch (Exception ex)
                {
                    Debug.LogWarning($"[RiveSpecialButton] Failed to bind to 'activated' trigger property: {ex.Message}");
                }
            }
        }

        private void UnsubscribeFromTrigger()
        {
            if (_activatedTriggerProp != null)
            {
                try
                {
                    _activatedTriggerProp.OnTriggered -= HandleActivated;
                }
                catch { }
                _activatedTriggerProp = null;
            }
        }

        /// <summary>
        /// Explicit touch/click handler wired to Unity UI Button for 100% reliable Android APK mobile touch input.
        /// </summary>
        public void OnTouchOverlayClicked()
        {
            if (Starfall.Core.WaveManager.IsStealthWaveActive)
            {
                Debug.LogWarning("[RiveSpecialButton] Cannot activate: Stealth mode is active!");
                return;
            }

            var combat = _playerCombat != null ? _playerCombat : FindAnyObjectByType<PlayerCombat>();
            if (combat == null) return;

            if (_isCoolingDown || currentCooldown < 90f)
            {
                Debug.Log($"[RiveSpecialButton] Ability cooling down ({currentCooldown:F0}%).");
                return;
            }

            // Debounce check so Rive's internal trigger and this button don't double fire
            if (Time.unscaledTime - _lastActivationTime < 0.4f)
            {
                return;
            }
            _lastActivationTime = Time.unscaledTime;

            Starfall.Audio.HapticFeedback.SelectionClick();
            FireRiveTrigger();
            ExecuteActivation(combat);
        }

        /// <summary>
        /// Triggers the Rive animation visually.
        /// </summary>
        private void FireRiveTrigger()
        {
            if (widget == null || widget.StateMachine == null) return;
            var sm = widget.StateMachine;
            try
            {
                if (_activatedTriggerProp != null)
                {
                    _activatedTriggerProp.Trigger();
                }
                else if (sm.ViewModelInstance != null)
                {
                    var trig = sm.ViewModelInstance.GetTriggerProperty("activated");
                    trig?.Trigger();
                }
            }
            catch { }
            try
            {
                var trigInput = sm.GetTrigger("activated");
                trigInput?.Fire();
            }
            catch { }
        }

        private void ExecuteActivation(PlayerCombat combat)
        {
            Debug.Log("<color=#00e5ff><b>[RiveSpecialButton] SPECIAL ABILITY ACTIVATED!</b></color>");

            // 1. Raise UnityEvent
            OnAbilityUsed?.Invoke();

            // 2. Trigger Player Warship Special Ability (Supernova Flare, etc.)
            if (combat != null)
            {
                combat.ActivateSpecialAbility();

                // 3. Also detonate bomb if available
                if (triggerPlayerBomb && combat.CurrentBombs > 0)
                {
                    combat.UseBomb();
                }
            }

            // 4. Visual feedback hook
            if (showVisualPlaceholder)
            {
                FlashPlaceholderBanner();
            }

            // 5. Begin cooldown
            StartCooldown();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            OnTouchOverlayClicked();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (IsReady)
            {
                OnTouchOverlayClicked();
            }
        }

        /// <summary>
        /// Called when Rive fires the "activated" trigger.
        /// </summary>
        private void HandleActivated()
        {
            if (Starfall.Core.WaveManager.IsStealthWaveActive)
            {
                Debug.LogWarning("[RiveSpecialButton] Cannot activate: Stealth mode is active!");
                return;
            }

            var combat = _playerCombat != null ? _playerCombat : FindAnyObjectByType<PlayerCombat>();
            if (combat == null) return;

            if (_isCoolingDown || currentCooldown < 90f) return;

            // Debounce check
            if (Time.unscaledTime - _lastActivationTime < 0.4f) return;
            _lastActivationTime = Time.unscaledTime;

            ExecuteActivation(combat);
        }

        /// <summary>
        /// Starts easing cooldown from 0 back to 100 over cooldownDuration.
        /// </summary>
        public void StartCooldown()
        {
            if (_cooldownCoroutine != null)
            {
                StopCoroutine(_cooldownCoroutine);
                _cooldownCoroutine = null;
            }

            var combat = _playerCombat != null ? _playerCombat : FindAnyObjectByType<PlayerCombat>();
            if (combat != null)
            {
                _playerCombat = combat;
                cooldownDuration = combat.AbilityCooldown;
                _isCoolingDown = true;
                currentCooldown = 0f;
                SetCooldownProperty(0f);
                UpdateTouchOverlayState(false);
                return;
            }

            UpdateTouchOverlayState(false);
            _cooldownCoroutine = StartCoroutine(CooldownRoutine());
        }

        private IEnumerator CooldownRoutine()
        {
            _isCoolingDown = true;
            currentCooldown = 0f;
            SetCooldownProperty(0f);
            UpdateTouchOverlayState(false);

            float elapsed = 0f;
            while (elapsed < cooldownDuration)
            {
                elapsed += Time.deltaTime;
                float progress = Mathf.Clamp01(elapsed / cooldownDuration);
                
                // Smooth easing curve
                currentCooldown = Mathf.SmoothStep(0f, 100f, progress);
                SetCooldownProperty(currentCooldown);
                yield return null;
            }

            var combat = _playerCombat != null ? _playerCombat : FindAnyObjectByType<PlayerCombat>();
            if (combat != null && combat.CurrentBombs <= 0)
            {
                currentCooldown = 0f;
                SetCooldownProperty(0f);
                if (widget != null) widget.HitTestBehavior = HitTestBehavior.None;
                UpdateTouchOverlayState(false);
                _isCoolingDown = false;
                _cooldownCoroutine = null;
                Debug.Log("[RiveSpecialButton] Cooldown finished but bomb count is 0 - waiting for power-up.");
            }
            else
            {
                currentCooldown = 100f;
                SetCooldownProperty(100f);
                if (widget != null) widget.HitTestBehavior = HitTestBehavior.Opaque;
                UpdateTouchOverlayState(true);
                _isCoolingDown = false;
                _cooldownCoroutine = null;
                Debug.Log("[RiveSpecialButton] Special Ability cooldown complete - READY!");
            }
        }

        /// <summary>
        /// Instantly resets cooldown to 100 (ready).
        /// </summary>
        public void ResetCooldown()
        {
            if (_cooldownCoroutine != null)
            {
                StopCoroutine(_cooldownCoroutine);
                _cooldownCoroutine = null;
            }
            _isCoolingDown = false;
            currentCooldown = 100f;
            SetCooldownProperty(100f);
            Debug.Log("[RiveSpecialButton] Cooldown reset to 100.");
        }

        /// <summary>
        /// Directly sets the cooldown value (0 - 100).
        /// </summary>
        public void SetCooldown(float value)
        {
            currentCooldown = Mathf.Clamp(value, 0f, 100f);
            SetCooldownProperty(currentCooldown);
        }

        private void SetCooldownProperty(float value)
        {
            if (widget == null || widget.StateMachine == null) return;

            var sm = widget.StateMachine;

            // 1. Try ViewModel property first
            if (sm.ViewModelInstance != null)
            {
                try
                {
                    var prop = sm.ViewModelInstance.GetNumberProperty("cooldown");
                    if (prop != null)
                    {
                        prop.Value = value;
                        return;
                    }
                }
                catch { }
            }

            // 2. Fallback to StateMachine input
            try
            {
                var input = sm.GetNumber("cooldown");
                if (input != null)
                {
                    input.Value = value;
                }
            }
            catch { }
        }

        // ---------------------------------------------------------------------
        // Test Visual Placeholder
        // ---------------------------------------------------------------------

        private void CreateVisualPlaceholder()
        {
            if (_placeholderGo != null) return;

            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null) return;

            _placeholderGo = new GameObject("SpecialAbilityFlashBanner", typeof(RectTransform));
            _placeholderGo.transform.SetParent(canvas.transform, false);

            var rect = _placeholderGo.GetComponent<RectTransform>();
            if (rect == null) rect = _placeholderGo.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 100f);
            rect.sizeDelta = new Vector2(500f, 100f);

            _placeholderText = _placeholderGo.AddComponent<TextMeshProUGUI>();
            _placeholderText.text = "<color=#00e5ff><b>ABILITY ACTIVATED!</b></color>";
            _placeholderText.fontSize = 38;
            _placeholderText.alignment = TextAlignmentOptions.Center;
            _placeholderText.raycastTarget = false;

            _placeholderGo.SetActive(false);
        }

        public void FlashPlaceholderBanner()
        {
            if (_placeholderGo == null) CreateVisualPlaceholder();
            if (_placeholderGo != null)
            {
                StopCoroutine("FlashRoutine");
                StartCoroutine(FlashRoutine());
            }
        }

        private IEnumerator FlashRoutine()
        {
            _placeholderGo.SetActive(true);
            if (_placeholderText != null)
            {
                var c = _placeholderText.color;
                c.a = 1f;
                _placeholderText.color = c;
            }

            float duration = 1.2f;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                if (_placeholderText != null)
                {
                    var c = _placeholderText.color;
                    c.a = Mathf.Clamp01(1f - (elapsed / duration));
                    _placeholderText.color = c;
                }
                yield return null;
            }

            _placeholderGo.SetActive(false);
        }
    }
}
