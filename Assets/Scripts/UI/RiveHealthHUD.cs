using System.Collections;
using UnityEngine;
using Rive.Components;
using Starfall.Player;

/// <summary>
/// Connects the Rive Health & Shield HUD widget (alienwarhud.riv) to Unity gameplay.
/// Automatically ensures all required Rive canvas components (RiveCanvasRenderer, etc.)
/// are attached and running, binds to the Player's real-time Hull and Shield stats,
/// and supports manual testing via RiveHealthHUDTester.
/// </summary>
public class RiveHealthHUD : MonoBehaviour
{
    [Header("Components")]
    [Tooltip("The RiveWidget displaying alienwarhud.riv.")]
    public RiveWidget widget;

    [Header("Current Values (0 - 100)")]
    [SerializeField] private float _health = 100f;
    [SerializeField] private float _shield = 100f;
    [SerializeField] private float _heat = 0f;

    [Header("Position & Scaling")]
    [Tooltip("If true, automatically adjusts the widget RectTransform to a neat HUD size and position.")]
    public bool autoPositionAndScale = true;

    [Tooltip("Anchor position relative to Top-Left. Default is (25, -25).")]
    public Vector2 hudPosition = new Vector2(25f, -25f);

    [Tooltip("Display size of the HUD widget. Default (420, 206) preserves the ~2:1 aspect ratio.")]
    public Vector2 hudSize = new Vector2(420f, 206f);

    [Tooltip("Hides the old plain text SHIELD: |||||| and HULL: |||||| bars so they don't clutter underneath.")]
    public bool hidePlainTextBars = true;

    [Header("Appearance & Loading")]
    [Tooltip("Hides the Rive widget until the HealthHUD artboard has loaded, preventing the brief Rive loading logo from showing.")]
    public bool hideWhileLoading = true;

    [Tooltip("Duration in seconds to smoothly fade in the HUD once loaded. 0 for instant.")]
    [SerializeField] private float fadeInDuration = 0.15f;

    private bool _isLoaded = false;
    private bool _plainTextBarsHidden = false;
    private PlayerHealth _playerHealth;
    private float _playerSearchTimer = 0f;
    private CanvasGroup _canvasGroup;
    private Coroutine _fadeCoroutine;

    public float Health => _health;
    public float Shield => _shield;
    public float Heat => _heat;
    public bool IsLoaded => _isLoaded;

    private void Awake()
    {
        EnsureRiveComponents();
        if (Application.isPlaying && hideWhileLoading && !_isLoaded)
        {
            SetVisibility(0f);
        }
        ApplyLayout();
        HookPlayerHealth();

        // Preload Rive asset synchronously in Awake if assigned
        if (widget != null && widget.Asset != null && widget.Status == WidgetStatus.Uninitialized)
        {
            try
            {
                widget.Load(widget.Asset, "HealthHUD", "Statemachine 1");
            }
            catch { }
        }
    }

    private void OnEnable()
    {
        EnsureRiveComponents();
        if (Application.isPlaying)
        {
            if (hideWhileLoading && !_isLoaded)
            {
                SetVisibility(0f);
            }
            else
            {
                SetVisibility(1f);
            }
        }
        else
        {
            SetVisibility(1f);
        }
        ApplyLayout();
        HookPlayerHealth();
    }

    private void OnValidate()
    {
        if (autoPositionAndScale)
        {
            ApplyLayout();
        }
        if (!Application.isPlaying)
        {
            SetVisibility(1f);
        }
    }

    private void OnDisable()
    {
        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = null;
        }
        UnhookPlayerHealth();
    }

    private void OnDestroy()
    {
        if (_fadeCoroutine != null)
        {
            StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = null;
        }
        UnhookPlayerHealth();
    }

    /// <summary>
    /// Controls HUD visibility across CanvasGroup and UI RawImage components.
    /// </summary>
    public void SetVisibility(float alpha)
    {
        if (_canvasGroup == null && widget != null)
        {
            var panel = widget.GetComponentInParent<RivePanel>();
            if (panel != null)
            {
                _canvasGroup = panel.GetComponent<CanvasGroup>();
                if (_canvasGroup == null)
                {
                    _canvasGroup = panel.gameObject.AddComponent<CanvasGroup>();
                }
            }
        }

        if (_canvasGroup != null)
        {
            _canvasGroup.alpha = alpha;
        }

        // Also control CanvasRendererRawImage directly on the panel to ensure no rogue alpha bypass
        if (widget != null)
        {
            var panel = widget.GetComponentInParent<RivePanel>();
            if (panel != null)
            {
                var rawImg = panel.GetComponent<UnityEngine.UI.RawImage>();
                if (rawImg != null)
                {
                    var c = rawImg.color;
                    c.a = alpha;
                    rawImg.color = c;
                }
            }
        }
    }

    public void ApplyLayout()
    {
        if (widget == null)
            widget = GetComponent<RiveWidget>() ?? GetComponentInChildren<RiveWidget>();

        if (autoPositionAndScale && widget != null)
        {
            var rect = widget.GetComponent<RectTransform>();
            if (rect != null)
            {
                rect.anchorMin = new Vector2(0f, 1f);
                rect.anchorMax = new Vector2(0f, 1f);
                rect.pivot = new Vector2(0f, 1f);
                rect.anchoredPosition = hudPosition;
                rect.sizeDelta = hudSize;
            }
        }

        if (hidePlainTextBars && Application.isPlaying && !_plainTextBarsHidden)
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                var shieldText = canvas.transform.Find("ShieldText");
                if (shieldText != null && shieldText.gameObject.activeSelf) shieldText.gameObject.SetActive(false);
                var hullText = canvas.transform.Find("HullText");
                if (hullText != null && hullText.gameObject.activeSelf) hullText.gameObject.SetActive(false);
            }

            var hud = FindAnyObjectByType<Starfall.UI.GameplayHUD>();
            if (hud != null)
            {
                var s = hud.transform.Find("ShieldText");
                if (s != null && s.gameObject.activeSelf) s.gameObject.SetActive(false);
                var h = hud.transform.Find("HullText");
                if (h != null && h.gameObject.activeSelf) h.gameObject.SetActive(false);
            }

            var legacyBar = GameObject.Find("PlayerHealthBar");
            if (legacyBar != null && legacyBar.activeSelf) legacyBar.SetActive(false);

            _plainTextBarsHidden = true;
        }
    }

    /// <summary>
    /// Automatically checks for and adds any missing Rive canvas rendering components.
    /// A RivePanel on a Canvas MUST have a RiveCanvasRenderer to draw to the screen.
    /// </summary>
    public void EnsureRiveComponents()
    {
        if (widget == null)
        {
            widget = GetComponent<RiveWidget>();
            if (widget == null)
                widget = GetComponentInChildren<RiveWidget>();
        }

        if (widget == null) return;

        var panel = widget.GetComponentInParent<RivePanel>();
        if (panel != null && panel.GetComponentsInChildren<RiveWidget>(true).Length > 1)
        {
            var canvas = widget.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                var dedicatedPanelGo = new GameObject("Rive Panel - HealthHUD");
                dedicatedPanelGo.transform.SetParent(canvas.transform, false);
                var pr = dedicatedPanelGo.AddComponent<RectTransform>();
                pr.anchorMin = Vector2.zero;
                pr.anchorMax = Vector2.one;
                pr.offsetMin = Vector2.zero;
                pr.offsetMax = Vector2.zero;

                panel = dedicatedPanelGo.AddComponent<RivePanel>();
                var rcr = dedicatedPanelGo.AddComponent<RiveCanvasRenderer>();
                rcr.PointerInputMode = PointerInputMode.DisablePointerInput;
                _canvasGroup = dedicatedPanelGo.AddComponent<CanvasGroup>();
                _canvasGroup.alpha = 0f;

                widget.transform.SetParent(dedicatedPanelGo.transform, true);
            }
        }

        if (panel != null)
        {
            var canvasRenderer = panel.GetComponent<RiveCanvasRenderer>();
            if (canvasRenderer == null)
            {
                panel.gameObject.AddComponent<RiveCanvasRenderer>();
                Debug.Log($"[RiveHealthHUD] Added missing RiveCanvasRenderer to '{panel.gameObject.name}'.");
            }

            // Ensure the panel's RectTransform is anchored to fill or fit the canvas
            var panelRect = panel.GetComponent<RectTransform>();
            if (panelRect != null && panelRect.sizeDelta == Vector2.zero && panelRect.anchorMin == panelRect.anchorMax)
            {
                panelRect.anchorMin = Vector2.zero;
                panelRect.anchorMax = Vector2.one;
                panelRect.offsetMin = Vector2.zero;
                panelRect.offsetMax = Vector2.zero;
            }

            if (_canvasGroup == null)
            {
                _canvasGroup = panel.GetComponent<CanvasGroup>();
                if (_canvasGroup == null)
                {
                    _canvasGroup = panel.gameObject.AddComponent<CanvasGroup>();
                }
            }
        }
    }

    private IEnumerator Start()
    {
        EnsureRiveComponents();

        if (widget == null)
        {
            Debug.LogWarning("[RiveHealthHUD] No RiveWidget found on or under this GameObject.");
            yield break;
        }

        // Set HitTestBehavior to None so touches/clicks pass through to buttons and gameplay
        widget.HitTestBehavior = HitTestBehavior.None;

        // Start hidden if hideWhileLoading is enabled
        if (hideWhileLoading && !_isLoaded)
        {
            SetVisibility(0f);
        }

        Debug.Log("[RiveHealthHUD] Waiting for Rive StateMachine and ViewModelInstance to load...");
        // Wait until Rive finishes loading the asset and artboard
        float timeout = 8f;
        float timer = 0f;
        while (timer < timeout)
        {
            if (widget != null && widget.StateMachine != null)
            {
                if (widget.StateMachine.ViewModelInstance != null || widget.Status == WidgetStatus.Loaded)
                    break;
            }
            timer += Time.unscaledDeltaTime;
            yield return null;
        }

        if (widget != null && widget.StateMachine != null)
        {
            _isLoaded = true;
            Debug.Log("[RiveHealthHUD] Successfully loaded Rive StateMachine & ViewModel!");

            // Refresh with player's health if already known
            HookPlayerHealth();
            if (_playerHealth != null)
            {
                if (_playerHealth.MaxHull > 0)
                    _health = Mathf.Clamp01((float)_playerHealth.CurrentHull / _playerHealth.MaxHull) * 100f;
                if (_playerHealth.MaxShield > 0)
                    _shield = Mathf.Clamp01((float)_playerHealth.CurrentShield / _playerHealth.MaxShield) * 100f;
            }

            UpdateViewModel();

            // Advance state machine by 3.0s to fast-forward past the Rive watermark intro timeline
            try
            {
                widget.StateMachine.Advance(3.0f);
            }
            catch { }

            // Ensure old text bars are cleanly hidden
            ApplyLayout();

            // Wait 1 frame so Rive rasterizes the health bar artboard into the render target
            yield return null;

            // Health bar is now fully drawn and ready: display immediately with no logo flash
            SetVisibility(1f);
        }
        else
        {
            Debug.LogWarning("[RiveHealthHUD] Timed out waiting for Rive StateMachine to initialize.");
            SetVisibility(1f);
        }
    }

    private IEnumerator FadeInRoutine()
    {
        if (fadeInDuration <= 0f)
        {
            SetVisibility(1f);
            yield break;
        }

        float timer = 0f;
        while (timer < fadeInDuration)
        {
            timer += Time.unscaledDeltaTime;
            SetVisibility(Mathf.Clamp01(timer / fadeInDuration));
            yield return null;
        }
        SetVisibility(1f);
        _fadeCoroutine = null;
    }

    private void Update()
    {
        // If the player spawned after the HUD initialized, hook them dynamically
        if (_playerHealth == null)
        {
            _playerSearchTimer += Time.deltaTime;
            if (_playerSearchTimer >= 1.0f)
            {
                _playerSearchTimer = 0f;
                HookPlayerHealth();
            }
        }
    }

    private void HookPlayerHealth()
    {
        if (_playerHealth != null) return;

        _playerHealth = FindAnyObjectByType<PlayerHealth>();
        if (_playerHealth != null)
        {
            _playerHealth.OnHullChanged += HandleHullChanged;
            _playerHealth.OnShieldChanged += HandleShieldChanged;
            _playerHealth.OnDamageTaken += HandleDamageTaken;

            if (_playerHealth.MaxHull > 0)
                _health = Mathf.Clamp01((float)_playerHealth.CurrentHull / _playerHealth.MaxHull) * 100f;
            if (_playerHealth.MaxShield > 0)
                _shield = Mathf.Clamp01((float)_playerHealth.CurrentShield / _playerHealth.MaxShield) * 100f;

            if (_isLoaded) UpdateViewModel();
        }
    }

    private void UnhookPlayerHealth()
    {
        if (_playerHealth != null)
        {
            _playerHealth.OnHullChanged -= HandleHullChanged;
            _playerHealth.OnShieldChanged -= HandleShieldChanged;
            _playerHealth.OnDamageTaken -= HandleDamageTaken;
            _playerHealth = null;
        }
    }

    private void HandleHullChanged(int current, int max)
    {
        if (max <= 0) return;
        float newHealth = Mathf.Clamp01((float)current / max) * 100f;
        if (newHealth < _health)
        {
            FireTrigger("hit");
        }
        _health = newHealth;
        UpdateViewModel();
    }

    private void HandleShieldChanged(int current, int max)
    {
        if (max <= 0) return;
        float newShield = Mathf.Clamp01((float)current / max) * 100f;
        if (newShield < _shield)
        {
            FireTrigger("shieldHit");
        }
        _shield = newShield;
        UpdateViewModel();
    }

    private void HandleDamageTaken()
    {
        FireTrigger("hit");
    }

    // ---------------------------------------------------------------------
    // Manual controls (used by RiveHealthHUDTester and external scripts in test modes)
    // ---------------------------------------------------------------------

    public void DamageHealth(float amount)
    {
        if (_playerHealth != null) return; // PlayerHealth is authoritative during gameplay
        _health = Mathf.Clamp(_health - amount, 0f, 100f);
        SetPropertyValue("health", _health);
        FireTrigger("hit");
    }

    public void Heal(float amount)
    {
        if (_playerHealth != null) return; // PlayerHealth is authoritative during gameplay
        _health = Mathf.Clamp(_health + amount, 0f, 100f);
        SetPropertyValue("health", _health);
    }

    public void DamageShield(float amount)
    {
        if (_playerHealth != null) return; // PlayerHealth is authoritative during gameplay
        _shield = Mathf.Clamp(_shield - amount, 0f, 100f);
        SetPropertyValue("shield", _shield);
        FireTrigger("shieldHit");
    }

    public void RechargeShield(float amount)
    {
        if (_playerHealth != null) return; // PlayerHealth is authoritative during gameplay
        _shield = Mathf.Clamp(_shield + amount, 0f, 100f);
        SetPropertyValue("shield", _shield);
    }

    public void AddHeat(float amount)
    {
        _heat = Mathf.Clamp(_heat + amount, 0f, 100f);
        SetPropertyValue("heat", _heat);
    }

    public void CoolDown(float amount)
    {
        _heat = Mathf.Clamp(_heat - amount, 0f, 100f);
        SetPropertyValue("heat", _heat);
    }

    // ---------------------------------------------------------------------
    // Rive View Model & State Machine Property Dispatch
    // ---------------------------------------------------------------------

    public void UpdateViewModel()
    {
        if (!_isLoaded || widget == null || widget.StateMachine == null) return;

        SetPropertyValue("health", _health);
        SetPropertyValue("shield", _shield);
    }

    private void SetPropertyValue(string propertyName, float value)
    {
        if (widget == null || widget.StateMachine == null) return;

        var sm = widget.StateMachine;

        // 1. Try ViewModel property first
        if (sm.ViewModelInstance != null)
        {
            try
            {
                var prop = sm.ViewModelInstance.GetNumberProperty(propertyName);
                if (prop != null)
                {
                    prop.Value = value;
                    return;
                }
            }
            catch { }
        }

        // 2. Try State Machine input directly
        try
        {
            var input = sm.GetNumber(propertyName);
            if (input != null)
            {
                input.Value = value;
            }
        }
        catch { }
    }

    private void FireTrigger(string triggerName)
    {
        if (widget == null || widget.StateMachine == null) return;

        var sm = widget.StateMachine;

        // 1. Try ViewModel trigger
        if (sm.ViewModelInstance != null)
        {
            try
            {
                var prop = sm.ViewModelInstance.GetTriggerProperty(triggerName);
                if (prop != null)
                {
                    prop.Trigger();
                    return;
                }
            }
            catch { }
        }

        // 2. Try State Machine trigger
        try
        {
            var trigger = sm.GetTrigger(triggerName);
            if (trigger != null)
            {
                trigger.Fire();
            }
        }
        catch { }
    }
}

namespace Starfall.UI.Rive
{
    // Alias to allow using Starfall.UI.Rive.RiveHealthHUD if referenced elsewhere
    public class RiveHealthHUDProxy : global::RiveHealthHUD { }
}
