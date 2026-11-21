using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using Starfall.Player;
using Starfall.Core;
using Starfall.VFX;

namespace Starfall.UI
{
    /// <summary>
    /// Builds and manages the full mobile control overlay:
    ///   • Polished analog joystick (bottom-left, draggable anywhere)
    ///   • BOMB button  (bottom-right cluster)
    ///   • ABILITY button with cooldown arc fill
    ///   • AUTO-FIRE toggle
    ///   • PAUSE button
    ///   • Settings panel to show/hide individual buttons and reset layout
    ///   • Long-press any button to drag it to a preferred position (saved in PlayerPrefs)
    /// </summary>
    public class MobileControlsManager : MonoBehaviour
    {
        public static MobileControlsManager Instance { get; private set; }

        // ------------------------------------------------------------------ refs
        private Canvas rootCanvas;
        private CanvasGroup rootGroup;

        // Joystick
        private RectTransform joystickBase;
        private RectTransform joystickHandle;
        private Image joystickBaseImg;
        private Image joystickHandleImg;
        private Vector2 joystickInput;
        private bool joystickDragging;

        // Action buttons
        private DraggableButton bombBtn;
        private DraggableButton abilityBtn;
        private DraggableButton autoFireBtn;
        private DraggableButton pauseBtn;
        private Image abilityArcFill;
        private TextMeshProUGUI abilityLabel;
        private TextMeshProUGUI autoFireLabel;

        // Settings panel
        private GameObject settingsPanel;
        private bool settingsOpen;

        // Drag-to-move state
        private bool layoutEditMode;
        private TextMeshProUGUI editModeLabel;

        // Visibility toggles (PlayerPrefs keys)
        private const string KEY_BOMB    = "MCB_BombVisible";
        private const string KEY_ABILITY = "MCB_AbilityVisible";
        private const string KEY_AUTOFIRE= "MCB_AutoFireVisible";
        private const string KEY_PAUSE   = "MCB_PauseVisible";

        // Joystick radius in world-canvas units
        private float joystickRadius = 80f;

        public Vector2 JoystickInput => joystickInput;

        // ------------------------------------------------------------------ lifecycle
        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void Start()
        {
            // Only build on mobile or when simulating touch in editor
#if !UNITY_EDITOR
            if (!Application.isMobilePlatform) { gameObject.SetActive(false); return; }
#endif
            BuildControls();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ------------------------------------------------------------------ public API used by PlayerController / GameplayHUD
        public static Vector2 GetJoystickInput()
        {
            return Instance != null ? Instance.joystickInput : Vector2.zero;
        }

        // ------------------------------------------------------------------ build
        private void BuildControls()
        {
            rootCanvas = GetComponentInParent<Canvas>();

            // Clean up unwanted legacy buttons and joystick as requested by user
            DestroyExisting("JoystickBase");
            DestroyExisting("AbilityBtn");
            DestroyExisting("AutoFireBtn");
            DestroyExisting("BombBtn");

            PlayerPrefs.DeleteKey("MCB_JoyPos_x");
            PlayerPrefs.DeleteKey("MCB_JoyPos_y");
            PlayerPrefs.DeleteKey("MCB_AbilityPos_x");
            PlayerPrefs.DeleteKey("MCB_AbilityPos_y");
            PlayerPrefs.DeleteKey("MCB_AutoFirePos_x");
            PlayerPrefs.DeleteKey("MCB_AutoFirePos_y");
            PlayerPrefs.DeleteKey("MCB_BombPos_x");
            PlayerPrefs.DeleteKey("MCB_BombPos_y");
            PlayerPrefs.Save();

            joystickInput = Vector2.zero;
        }

        private void DestroyExisting(string childName)
        {
            var child = transform.Find(childName);
            if (child != null) Destroy(child.gameObject);
        }

        // ================================================================== JOYSTICK
        private void BuildJoystick()
        {
            if (transform.Find("JoystickBase") != null) return;

            var baseGo = new GameObject("JoystickBase");
            baseGo.transform.SetParent(transform, false);
            joystickBase = baseGo.AddComponent<RectTransform>();
            joystickBase.anchorMin = joystickBase.anchorMax = new Vector2(0f, 0f);
            joystickBase.pivot = new Vector2(0.5f, 0.5f);
            // Load saved position or default bottom-left
            Vector2 savedPos = LoadVec2("MCB_JoyPos", new Vector2(160f, 180f));
            joystickBase.anchoredPosition = savedPos;
            joystickBase.sizeDelta = new Vector2(160f, 160f);

            var padSprite = Resources.Load<Sprite>("MobileControls/pad_circle_tactical") ?? ProceduralSprites.Ring;
            var handleSprite = Resources.Load<Sprite>("MobileControls/nub_crosshair") ?? ProceduralSprites.SoftCircle;
            var glowSprite = Resources.Load<Sprite>("MobileControls/nub_glow_neon") ?? ProceduralSprites.Ring;

            // Outer ring background
            joystickBaseImg = baseGo.AddComponent<Image>();
            joystickBaseImg.sprite = padSprite;
            joystickBaseImg.color = Color.white;
            joystickBaseImg.raycastTarget = true;

            // Inner filled disc (slightly smaller)
            var discGo = new GameObject("Disc");
            discGo.transform.SetParent(baseGo.transform, false);
            var discRect = discGo.AddComponent<RectTransform>();
            discRect.anchorMin = discRect.anchorMax = new Vector2(0.5f, 0.5f);
            discRect.sizeDelta = new Vector2(140f, 140f);
            var discImg = discGo.AddComponent<Image>();
            discImg.color = new Color(0.1f, 0.3f, 0.6f, 0.18f);
            discImg.raycastTarget = false;

            // Cross-hair guides
            BuildJoystickGuide(baseGo.transform, true);
            BuildJoystickGuide(baseGo.transform, false);

            // Handle knob
            var handleGo = new GameObject("Handle");
            handleGo.transform.SetParent(baseGo.transform, false);
            joystickHandle = handleGo.AddComponent<RectTransform>();
            joystickHandle.anchorMin = joystickHandle.anchorMax = new Vector2(0.5f, 0.5f);
            joystickHandle.sizeDelta = new Vector2(64f, 64f);
            joystickHandleImg = handleGo.AddComponent<Image>();
            joystickHandleImg.sprite = handleSprite;
            joystickHandleImg.color = Color.white;
            joystickHandleImg.raycastTarget = false;

            // Handle highlight ring
            var glowGo = new GameObject("Glow");
            glowGo.transform.SetParent(handleGo.transform, false);
            var glR = glowGo.AddComponent<RectTransform>();
            glR.anchorMin = glR.anchorMax = new Vector2(0.5f, 0.5f);
            glR.sizeDelta = new Vector2(80f, 80f);
            var glImg = glowGo.AddComponent<Image>();
            glImg.sprite = glowSprite;
            glImg.color = new Color(0f, 0.9f, 1f, 0.5f);
            glImg.raycastTarget = false;

            // Touch handler
            var handler = baseGo.AddComponent<JoystickTouchHandler>();
            handler.Init(this, joystickBase, joystickHandle, joystickBaseImg, joystickHandleImg, joystickRadius);

            // Drag-to-move support (long press)
            AddDragReposition(baseGo, "MCB_JoyPos");
        }

        private void BuildJoystickGuide(Transform parent, bool horizontal)
        {
            var g = new GameObject(horizontal ? "GuideH" : "GuideV");
            g.transform.SetParent(parent, false);
            var r = g.AddComponent<RectTransform>();
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            r.sizeDelta = horizontal ? new Vector2(100f, 2f) : new Vector2(2f, 100f);
            var img = g.AddComponent<Image>();
            img.color = new Color(0.4f, 0.8f, 1f, 0.18f);
            img.raycastTarget = false;
        }

        // ================================================================== ACTION BUTTONS
        private void BuildBombButton()
        {
            Vector2 def = new Vector2(-170f, 130f); // bottom-right area
            bombBtn = BuildActionButton(
                "BombBtn", "BOMB", "BOMB",
                new Color(0.9f, 0.25f, 0.1f, 0.92f),
                new Color(1f, 0.45f, 0.2f, 0.70f),
                def, AnchorCorner.BottomRight,
                OnBombPressed, "MCB_BombPos"
            );
        }

        private void BuildAbilityButton()
        {
            Vector2 def = new Vector2(-150f, 325f);
            var go = BuildActionButtonGo(
                "AbilityBtn",
                new Color(0.08f, 0.15f, 0.35f, 0.95f),
                new Color(0.25f, 0.55f, 1f, 0.70f),
                def, AnchorCorner.BottomRight, "MCB_AbilityPos"
            );

            // Cooldown arc image (behind label)
            var arcGo = new GameObject("ArcFill");
            arcGo.transform.SetParent(go.transform, false);
            var arcRect = arcGo.AddComponent<RectTransform>();
            arcRect.anchorMin = Vector2.zero;
            arcRect.anchorMax = Vector2.one;
            arcRect.sizeDelta = Vector2.zero;
            abilityArcFill = arcGo.AddComponent<Image>();
            abilityArcFill.sprite = ProceduralSprites.Ring;
            abilityArcFill.type = Image.Type.Filled;
            abilityArcFill.fillMethod = Image.FillMethod.Radial360;
            abilityArcFill.fillOrigin = (int)Image.Origin360.Top;
            abilityArcFill.fillClockwise = true;
            abilityArcFill.fillAmount = 0f;
            abilityArcFill.color = new Color(0.25f, 0.55f, 1f, 0.80f);
            abilityArcFill.raycastTarget = false;

            // Label
            var lblGo = new GameObject("Label");
            lblGo.transform.SetParent(go.transform, false);
            var lblRect = lblGo.AddComponent<RectTransform>();
            lblRect.anchorMin = Vector2.zero;
            lblRect.anchorMax = Vector2.one;
            lblRect.offsetMin = new Vector2(4f, 4f);
            lblRect.offsetMax = new Vector2(-4f, -4f);
            abilityLabel = lblGo.AddComponent<TextMeshProUGUI>();
            abilityLabel.text = "<size=13><b>ABILITY</b></size>\n<color=#ffaa33><size=11>--s</size></color>";
            abilityLabel.fontSize = 12f;
            abilityLabel.alignment = TextAlignmentOptions.Center;
            abilityLabel.color = Color.white;
            abilityLabel.raycastTarget = false;

            var btn = go.GetComponent<Button>();
            if (btn == null) btn = go.AddComponent<Button>();
            btn.onClick.AddListener(OnAbilityPressed);

            abilityBtn = go.GetComponent<DraggableButton>();
        }

        private void BuildAutoFireButton()
        {
            Vector2 def = new Vector2(-310f, 140f);
            var go = BuildActionButtonGo(
                "AutoFireBtn",
                new Color(0.05f, 0.35f, 0.15f, 0.92f),
                new Color(0.1f, 0.8f, 0.4f, 0.70f),
                def, AnchorCorner.BottomRight, "MCB_AutoFirePos"
            );

            var lblGo = new GameObject("Label");
            lblGo.transform.SetParent(go.transform, false);
            var lblRect = lblGo.AddComponent<RectTransform>();
            lblRect.anchorMin = Vector2.zero;
            lblRect.anchorMax = Vector2.one;
            lblRect.offsetMin = new Vector2(4f, 4f);
            lblRect.offsetMax = new Vector2(-4f, -4f);
            autoFireLabel = lblGo.AddComponent<TextMeshProUGUI>();
            autoFireLabel.fontSize = 11f;
            autoFireLabel.alignment = TextAlignmentOptions.Center;
            autoFireLabel.color = Color.white;
            autoFireLabel.raycastTarget = false;
            RefreshAutoFireLabel();

            var btn = go.GetComponent<Button>();
            if (btn == null) btn = go.AddComponent<Button>();
            btn.onClick.AddListener(OnAutoFirePressed);
            autoFireBtn = go.GetComponent<DraggableButton>();
        }

        private void BuildPauseButton()
        {
            Vector2 def = new Vector2(-20f, -80f);
            pauseBtn = BuildActionButton(
                "PauseBtn", "PAUSE", "PAUSE",
                new Color(0.1f, 0.1f, 0.22f, 0.90f),
                new Color(0.5f, 0.5f, 0.65f, 0.55f),
                def, AnchorCorner.TopRight,
                OnPausePressed, "MCB_PausePos"
            );
            if (pauseBtn != null)
            {
                var r = pauseBtn.GetComponent<RectTransform>();
                r.sizeDelta = new Vector2(80f, 42f);
            }
        }

        private DraggableButton homeBtn;
        private void BuildHomeButton()
        {
            Vector2 def = new Vector2(-20f, -20f);
            homeBtn = BuildActionButton(
                "HomeBtn", "HOME", "HOME",
                new Color(0.1f, 0.1f, 0.22f, 0.90f),
                new Color(0.5f, 0.5f, 0.65f, 0.55f),
                def, AnchorCorner.TopRight,
                () => {
                    Time.timeScale = 1f;
                    Starfall.Levels.LevelProgressManager.LoadLevelSelect();
                }, "MCB_HomePos"
            );
            if (homeBtn != null)
            {
                var r = homeBtn.GetComponent<RectTransform>();
                r.sizeDelta = new Vector2(80f, 42f);
            }
        }

        private void BuildSettingsButton()
        {
            var go = new GameObject("SettingsBtn");
            go.transform.SetParent(transform, false);
            var rect = go.AddComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(10f, -10f);
            rect.sizeDelta = new Vector2(52f, 42f);

            var bg = go.AddComponent<Image>();
            bg.color = new Color(0.08f, 0.12f, 0.22f, 0.88f);
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(ToggleSettings);

            var lblGo = new GameObject("Text");
            lblGo.transform.SetParent(go.transform, false);
            var lR = lblGo.AddComponent<RectTransform>();
            lR.anchorMin = Vector2.zero; lR.anchorMax = Vector2.one; lR.sizeDelta = Vector2.zero;
            var t = lblGo.AddComponent<TextMeshProUGUI>();
            t.text = "OPT";
            t.fontSize = 13f;
            t.fontStyle = FontStyles.Bold;
            t.alignment = TextAlignmentOptions.Center;
            t.color = new Color(0.6f, 0.85f, 1f);
            t.raycastTarget = false;
        }

        private void BuildSettingsPanel()
        {
            settingsPanel = new GameObject("SettingsPanel");
            settingsPanel.transform.SetParent(transform, false);

            var overlay = settingsPanel.AddComponent<RectTransform>();
            overlay.anchorMin = Vector2.zero;
            overlay.anchorMax = Vector2.one;
            overlay.sizeDelta = Vector2.zero;

            // Dark overlay
            var bgImg = settingsPanel.AddComponent<Image>();
            bgImg.color = new Color(0f, 0f, 0f, 0.72f);

            // Center card
            var card = new GameObject("Card");
            card.transform.SetParent(settingsPanel.transform, false);
            var cardRect = card.AddComponent<RectTransform>();
            cardRect.anchorMin = cardRect.anchorMax = new Vector2(0.5f, 0.5f);
            cardRect.pivot = new Vector2(0.5f, 0.5f);
            cardRect.sizeDelta = new Vector2(420f, 500f);
            var cardBg = card.AddComponent<Image>();
            cardBg.color = new Color(0.06f, 0.09f, 0.18f, 0.98f);

            // Title
            MakeSettingsText(card.transform, "MOBILE CONTROLS", 22f, FontStyles.Bold,
                new Vector2(0.5f, 0.88f), new Vector2(380f, 36f), new Color(0.2f, 0.85f, 1f));

            // Subtitle
            MakeSettingsText(card.transform, "Long-press any button to reposition it", 12f,
                FontStyles.Normal, new Vector2(0.5f, 0.82f), new Vector2(380f, 22f),
                new Color(0.6f, 0.75f, 0.9f));

            // Toggles
            float yStart = 0.72f;
            float step   = 0.11f;
            BuildToggleRow(card.transform, "Bomb Button",       KEY_BOMB,    yStart);
            BuildToggleRow(card.transform, "Ability Button",    KEY_ABILITY, yStart - step);
            BuildToggleRow(card.transform, "Auto-Fire Toggle",  KEY_AUTOFIRE,yStart - step * 2);
            BuildToggleRow(card.transform, "Pause Button",      KEY_PAUSE,   yStart - step * 3);

            // Divider
            var divGo = new GameObject("Divider");
            divGo.transform.SetParent(card.transform, false);
            var divR = divGo.AddComponent<RectTransform>();
            divR.anchorMin = new Vector2(0.05f, 0.32f);
            divR.anchorMax = new Vector2(0.95f, 0.322f);
            divR.sizeDelta = Vector2.zero;
            divGo.AddComponent<Image>().color = new Color(0.3f, 0.45f, 0.65f, 0.5f);

            // Edit layout toggle
            BuildCardButton(card.transform, "EDIT LAYOUT", new Color(0.12f, 0.3f, 0.55f, 1f),
                new Vector2(0.5f, 0.25f), new Vector2(320f, 48f), ToggleEditMode);

            // Reset positions
            BuildCardButton(card.transform, "RESET POSITIONS", new Color(0.4f, 0.15f, 0.05f, 1f),
                new Vector2(0.5f, 0.13f), new Vector2(320f, 44f), ResetPositions);

            // Close
            BuildCardButton(card.transform, "CLOSE", new Color(0.1f, 0.22f, 0.1f, 1f),
                new Vector2(0.5f, 0.04f), new Vector2(320f, 40f), CloseSettings);

            settingsPanel.SetActive(false);
        }

        private void BuildEditModeLabel()
        {
            var go = new GameObject("EditModeLabel");
            go.transform.SetParent(transform, false);
            var r = go.AddComponent<RectTransform>();
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 1f);
            r.pivot = new Vector2(0.5f, 1f);
            r.anchoredPosition = new Vector2(0f, -60f);
            r.sizeDelta = new Vector2(500f, 36f);
            editModeLabel = go.AddComponent<TextMeshProUGUI>();
            editModeLabel.text = "<color=#ffdd33>DRAG BUTTONS TO REPOSITION  -  TAP OPTIONS TO FINISH</color>";
            editModeLabel.fontSize = 15f;
            editModeLabel.alignment = TextAlignmentOptions.Center;
            editModeLabel.raycastTarget = false;
            go.SetActive(false);
        }

        // ================================================================== HELPERS
        private enum AnchorCorner { BottomLeft, BottomRight, TopLeft, TopRight }

        private DraggableButton BuildActionButton(
            string name, string icon, string label,
            Color bgColor, Color borderColor,
            Vector2 anchoredPos, AnchorCorner corner,
            UnityEngine.Events.UnityAction onClick,
            string prefKey)
        {
            var go = BuildActionButtonGo(name, bgColor, borderColor, anchoredPos, corner, prefKey);

            var lblGo = new GameObject("Label");
            lblGo.transform.SetParent(go.transform, false);
            var lR = lblGo.AddComponent<RectTransform>();
            lR.anchorMin = Vector2.zero; lR.anchorMax = Vector2.one;
            lR.offsetMin = new Vector2(4f, 4f); lR.offsetMax = new Vector2(-4f, -4f);
            var t = lblGo.AddComponent<TextMeshProUGUI>();
            t.text = $"<size=20>{icon}</size>\n<size=11><b>{label}</b></size>";
            t.fontSize = 11f;
            t.alignment = TextAlignmentOptions.Center;
            t.color = Color.white;
            t.raycastTarget = false;

            var btn = go.GetComponent<Button>();
            if (btn == null) btn = go.AddComponent<Button>();
            btn.onClick.AddListener(onClick);

            return go.GetComponent<DraggableButton>();
        }

        private GameObject BuildActionButtonGo(
            string name, Color bgColor, Color borderColor,
            Vector2 anchoredPos, AnchorCorner corner, string prefKey)
        {
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);

            var rect = go.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(110f, 110f);
            switch (corner)
            {
                case AnchorCorner.BottomLeft:
                    rect.anchorMin = rect.anchorMax = Vector2.zero;
                    rect.pivot = Vector2.zero;
                    break;
                case AnchorCorner.BottomRight:
                    rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f);
                    rect.pivot = new Vector2(1f, 0f);
                    break;
                case AnchorCorner.TopLeft:
                    rect.anchorMin = rect.anchorMax = new Vector2(0f, 1f);
                    rect.pivot = new Vector2(0f, 1f);
                    break;
                case AnchorCorner.TopRight:
                    rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
                    rect.pivot = new Vector2(1f, 1f);
                    break;
            }
            rect.anchoredPosition = LoadVec2(prefKey, anchoredPos);

            // Background
            var bg = go.AddComponent<Image>();
            bg.color = bgColor;

            // Border ring
            var borderGo = new GameObject("Border");
            borderGo.transform.SetParent(go.transform, false);
            var bR = borderGo.AddComponent<RectTransform>();
            bR.anchorMin = Vector2.zero; bR.anchorMax = Vector2.one; bR.sizeDelta = Vector2.zero;
            var bImg = borderGo.AddComponent<Image>();
            bImg.sprite = ProceduralSprites.Ring;
            bImg.color = borderColor;
            bImg.raycastTarget = false;

            // Drag-to-reposition
            var draggable = go.AddComponent<DraggableButton>();
            draggable.Init(prefKey, this);

            return go;
        }

        // Settings helpers
        private void MakeSettingsText(Transform parent, string text, float size,
            FontStyles style, Vector2 anchor, Vector2 sizeDelta, Color color)
        {
            var go = new GameObject("SettingsText_" + text.Substring(0, Mathf.Min(6, text.Length)));
            go.transform.SetParent(parent, false);
            var r = go.AddComponent<RectTransform>();
            r.anchorMin = r.anchorMax = anchor;
            r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = sizeDelta;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.fontStyle = style;
            t.alignment = TextAlignmentOptions.Center;
            t.color = color;
            t.raycastTarget = false;
        }

        private void BuildToggleRow(Transform parent, string label, string prefKey, float anchorY)
        {
            var rowGo = new GameObject("Toggle_" + prefKey);
            rowGo.transform.SetParent(parent, false);
            var rowRect = rowGo.AddComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0.05f, anchorY - 0.04f);
            rowRect.anchorMax = new Vector2(0.95f, anchorY + 0.05f);
            rowRect.sizeDelta = Vector2.zero;
            rowGo.AddComponent<Image>().color = new Color(0.1f, 0.16f, 0.28f, 0.7f);
            var btn = rowGo.AddComponent<Button>();
            btn.onClick.AddListener(() => ToggleVisibility(prefKey));

            var lGo = new GameObject("Label");
            lGo.transform.SetParent(rowGo.transform, false);
            var lR = lGo.AddComponent<RectTransform>();
            lR.anchorMin = new Vector2(0.05f, 0f);
            lR.anchorMax = new Vector2(0.75f, 1f);
            lR.sizeDelta = Vector2.zero;
            var t = lGo.AddComponent<TextMeshProUGUI>();
            t.text = label;
            t.fontSize = 14f;
            t.alignment = TextAlignmentOptions.Left;
            t.color = Color.white;
            t.raycastTarget = false;

            var chkGo = new GameObject("Check");
            chkGo.transform.SetParent(rowGo.transform, false);
            var chkR = chkGo.AddComponent<RectTransform>();
            chkR.anchorMin = new Vector2(0.78f, 0.1f);
            chkR.anchorMax = new Vector2(0.97f, 0.9f);
            chkR.sizeDelta = Vector2.zero;
            var chkImg = chkGo.AddComponent<Image>();
            bool visible = PlayerPrefs.GetInt(prefKey, 1) == 1;
            chkImg.color = visible ? new Color(0.1f, 0.9f, 0.5f, 1f) : new Color(0.3f, 0.35f, 0.45f, 0.8f);
            var chkTGo = new GameObject("CheckText");
            chkTGo.transform.SetParent(chkGo.transform, false);
            var chkTR = chkTGo.AddComponent<RectTransform>();
            chkTR.anchorMin = Vector2.zero; chkTR.anchorMax = Vector2.one; chkTR.sizeDelta = Vector2.zero;
            var chkT = chkTGo.AddComponent<TextMeshProUGUI>();
            chkT.text = visible ? "ON" : "OFF";
            chkT.fontSize = 13f;
            chkT.fontStyle = FontStyles.Bold;
            chkT.alignment = TextAlignmentOptions.Center;
            chkT.color = Color.white;
            chkT.raycastTarget = false;
        }

        private void BuildCardButton(Transform parent, string text, Color color,
            Vector2 anchor, Vector2 size, UnityEngine.Events.UnityAction onClick)
        {
            var go = new GameObject("CardBtn_" + text.Substring(0, Mathf.Min(6, text.Length)));
            go.transform.SetParent(parent, false);
            var r = go.AddComponent<RectTransform>();
            r.anchorMin = r.anchorMax = anchor;
            r.pivot = new Vector2(0.5f, 0.5f);
            r.sizeDelta = size;
            var img = go.AddComponent<Image>();
            img.color = color;
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(onClick);

            var tGo = new GameObject("Text");
            tGo.transform.SetParent(go.transform, false);
            var tR = tGo.AddComponent<RectTransform>();
            tR.anchorMin = Vector2.zero; tR.anchorMax = Vector2.one; tR.sizeDelta = Vector2.zero;
            var t = tGo.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = 14f;
            t.fontStyle = FontStyles.Bold;
            t.alignment = TextAlignmentOptions.Center;
            t.color = Color.white;
            t.raycastTarget = false;
        }

        private void AddDragReposition(GameObject go, string prefKey)
        {
            var draggable = go.GetComponent<DraggableButton>();
            if (draggable == null) draggable = go.AddComponent<DraggableButton>();
            draggable.Init(prefKey, this);
        }

        // ================================================================== ACTIONS
        private void OnBombPressed()
        {
            var combat = FindAnyObjectByType<PlayerCombat>();
            combat?.UseBomb();
        }

        private void OnAbilityPressed()
        {
            var combat = FindAnyObjectByType<PlayerCombat>();
            combat?.ActivateSpecialAbility();
        }

        private void OnAutoFirePressed()
        {
            var combat = FindAnyObjectByType<PlayerCombat>();
            if (combat == null) return;
            combat.AutoFire = !combat.AutoFire;
            RefreshAutoFireLabel();
        }

        private void OnPausePressed()
        {
            PauseMenu.Ensure().Toggle();
        }

        private void RefreshAutoFireLabel()
        {
            if (autoFireLabel == null) return;
            var combat = FindAnyObjectByType<PlayerCombat>();
            bool on = combat == null || combat.AutoFire;
            autoFireLabel.text = on
                ? "<color=#00ff88><size=13><b>AUTO ON</b></size></color>"
                : "<color=#ff6644><size=13><b>AUTO OFF</b></size></color>";
        }

        // ================================================================== SETTINGS
        private void ToggleSettings()
        {
            settingsOpen = !settingsOpen;
            settingsPanel.SetActive(settingsOpen);

            // Rebuild toggles so they reflect current state
            if (settingsOpen) RebuildToggles();
        }

        private void CloseSettings()
        {
            settingsOpen = false;
            settingsPanel.SetActive(false);
        }

        private void ToggleVisibility(string prefKey)
        {
            bool current = PlayerPrefs.GetInt(prefKey, 1) == 1;
            PlayerPrefs.SetInt(prefKey, current ? 0 : 1);
            PlayerPrefs.Save();
            ApplyVisibilityPrefs();
            RebuildToggles();
        }

        private void ApplyVisibilityPrefs()
        {
            SetBtnVisible(bombBtn,     PlayerPrefs.GetInt(KEY_BOMB,     1) == 1);
            SetBtnVisible(abilityBtn,  PlayerPrefs.GetInt(KEY_ABILITY,  1) == 1);
            SetBtnVisible(autoFireBtn, PlayerPrefs.GetInt(KEY_AUTOFIRE, 1) == 1);
            SetBtnVisible(pauseBtn,    PlayerPrefs.GetInt(KEY_PAUSE,    1) == 1);
        }

        private void SetBtnVisible(DraggableButton btn, bool visible)
        {
            if (btn != null) btn.gameObject.SetActive(visible);
        }

        private void RebuildToggles()
        {
            // Re-color the checkmarks
            var card = settingsPanel.transform.Find("Card");
            if (card == null) return;

            RefreshToggle(card, KEY_BOMB);
            RefreshToggle(card, KEY_ABILITY);
            RefreshToggle(card, KEY_AUTOFIRE);
            RefreshToggle(card, KEY_PAUSE);
        }

        private void RefreshToggle(Transform card, string key)
        {
            bool on = PlayerPrefs.GetInt(key, 1) == 1;
            var toggle = card.Find("Toggle_" + key);
            if (toggle == null) return;
            var check = toggle.Find("Check");
            if (check == null) return;
            var img = check.GetComponent<Image>();
            if (img != null) img.color = on ? new Color(0.1f, 0.9f, 0.5f, 1f) : new Color(0.3f, 0.35f, 0.45f, 0.8f);
            var t = check.GetComponentInChildren<TextMeshProUGUI>();
            if (t != null) t.text = on ? "ON" : "OFF";
        }

        private void ToggleEditMode()
        {
            layoutEditMode = !layoutEditMode;
            DraggableButton.EditMode = layoutEditMode;
            if (editModeLabel != null) editModeLabel.gameObject.SetActive(layoutEditMode);
            CloseSettings();
        }

        private void ResetPositions()
        {
            PlayerPrefs.DeleteKey("MCB_JoyPos");
            PlayerPrefs.DeleteKey("MCB_BombPos");
            PlayerPrefs.DeleteKey("MCB_AbilityPos");
            PlayerPrefs.DeleteKey("MCB_AutoFirePos");
            PlayerPrefs.DeleteKey("MCB_PausePos");
            PlayerPrefs.Save();
            // Reload scene or just set default positions
            ApplyDefaultPosition((RectTransform)transform.Find("JoystickBase"),   new Vector2(160f, 180f));
            ApplyDefaultPosition(GetBtnRect(bombBtn),              new Vector2(-150f, 160f));
            ApplyDefaultPosition(GetBtnRect(abilityBtn),           new Vector2(-150f, 325f));
            ApplyDefaultPosition(GetBtnRect(autoFireBtn),          new Vector2(-310f, 140f));
            ApplyDefaultPosition(GetBtnRect(pauseBtn),             new Vector2(-36f, -30f));
            CloseSettings();
        }

        private RectTransform GetBtnRect(DraggableButton b) => b != null ? b.GetComponent<RectTransform>() : null;

        private void ApplyDefaultPosition(RectTransform rect, Vector2 pos)
        {
            if (rect != null) rect.anchoredPosition = pos;
        }

        private void LoadPositions() { /* positions loaded per-element in Build methods */ }

        // ================================================================== PlayerPrefs helpers
        public static void SaveVec2(string key, Vector2 v)
        {
            PlayerPrefs.SetFloat(key + "_x", v.x);
            PlayerPrefs.SetFloat(key + "_y", v.y);
            PlayerPrefs.Save();
        }

        public static Vector2 LoadVec2(string key, Vector2 def)
        {
            if (!PlayerPrefs.HasKey(key + "_x")) return def;
            return new Vector2(PlayerPrefs.GetFloat(key + "_x"), PlayerPrefs.GetFloat(key + "_y"));
        }

        public bool IsInEditMode => layoutEditMode;

        // ================================================================== Joystick value pump
        // Called by JoystickTouchHandler
        public void SetJoystickInput(Vector2 v) => joystickInput = v;
    }

    // ======================================================================
    // JoystickTouchHandler – receives Unity touch/pointer events
    // ======================================================================
    public class JoystickTouchHandler : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private MobileControlsManager mgr;
        private RectTransform baseRect;
        private RectTransform handle;
        private Image baseImg;
        private Image handleImg;
        private float radius;
        private bool active;

        public void Init(MobileControlsManager m, RectTransform b, RectTransform h,
            Image bImg, Image hImg, float r)
        {
            mgr = m; baseRect = b; handle = h; baseImg = bImg; handleImg = hImg; radius = r;
        }

        public void OnPointerDown(PointerEventData e)
        {
            if (mgr.IsInEditMode) return;
            active = true;
            baseImg.color = new Color(0.2f, 0.65f, 1f, 0.40f);
            Move(e);
        }

        public void OnDrag(PointerEventData e)
        {
            if (!active || mgr.IsInEditMode) return;
            Move(e);
        }

        public void OnPointerUp(PointerEventData e)
        {
            active = false;
            mgr.SetJoystickInput(Vector2.zero);
            handle.anchoredPosition = Vector2.zero;
            baseImg.color = new Color(0.15f, 0.55f, 1f, 0.25f);
            handleImg.color = new Color(0.3f, 0.75f, 1f, 0.92f);
        }

        private void Move(PointerEventData e)
        {
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(
                baseRect, e.position, e.pressEventCamera, out Vector2 local)) return;

            Vector2 delta = Vector2.ClampMagnitude(local, radius);
            handle.anchoredPosition = delta;

            Vector2 input = delta / radius;
            mgr.SetJoystickInput(input);

            float intensity = input.magnitude;
            handleImg.color = Color.Lerp(
                new Color(0.3f, 0.75f, 1f, 0.92f),
                new Color(0.9f, 0.95f, 1f, 1f),
                intensity);
        }
    }

    // ======================================================================
    // DraggableButton – long-press to enter drag mode, saves position
    // ======================================================================
    public class DraggableButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
    {
        public static bool EditMode;

        private string prefKey;
        private MobileControlsManager mgr;
        private RectTransform rect;
        private Button btn;
        private bool pressing;
        private bool dragging;
        private float pressTimer;
        private const float LongPressSec = 0.6f;
        private Vector2 dragStartPointer;
        private Vector2 dragStartPos;
        private Image bgImage;
        private Color normalColor;

        public void Init(string key, MobileControlsManager m)
        {
            prefKey = key;
            mgr = m;
            rect = GetComponent<RectTransform>();
            btn = GetComponent<Button>();
            bgImage = GetComponent<Image>();
            if (bgImage != null) normalColor = bgImage.color;
        }

        public void SetInteractable(bool v) { if (btn != null) btn.interactable = v; }

        public void OnPointerDown(PointerEventData e)
        {
            pressing = true;
            pressTimer = 0f;
            dragStartPointer = e.position;
            dragStartPos = rect != null ? rect.anchoredPosition : Vector2.zero;
        }

        private void Update()
        {
            if (!pressing) return;
            pressTimer += Time.unscaledDeltaTime;
            if (pressTimer >= LongPressSec && !dragging)
            {
                dragging = true;
                // Visual: tint to show drag mode
                if (bgImage != null) bgImage.color = new Color(
                    normalColor.r + 0.2f, normalColor.g + 0.2f, normalColor.b + 0.2f, 1f);
            }
        }

        public void OnDrag(PointerEventData e)
        {
            if (!dragging && !EditMode) return;
            if (rect == null) return;
            Vector2 delta = e.position - dragStartPointer;
            // Convert screen delta to canvas delta
            Canvas c = GetComponentInParent<Canvas>();
            if (c != null) delta /= c.scaleFactor;
            rect.anchoredPosition = dragStartPos + delta;
        }

        public void OnPointerUp(PointerEventData e)
        {
            if (dragging)
            {
                // Save position
                if (rect != null) MobileControlsManager.SaveVec2(prefKey, rect.anchoredPosition);
                if (bgImage != null) bgImage.color = normalColor;
            }
            else
            {
                // Fire the button click if it was just a tap
                if (!EditMode && btn != null && btn.interactable)
                {
                    btn.onClick.Invoke();
                }
            }
            pressing = false;
            dragging = false;
            pressTimer = 0f;
        }
    }
}
