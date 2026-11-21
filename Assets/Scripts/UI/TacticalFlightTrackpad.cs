using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;

namespace Starfall.UI
{
    /// <summary>
    /// Tactical Military Analog Flight Trackpad for mobile ship maneuvering.
    /// Anchored in the bottom-left corner of the screen. Features:
    ///   • Outer tactical HUD reticle with cardinal bearing marks & concentric rings
    ///   • 4 Directional Chevron LEDs (Up, Down, Left, Right) that illuminate on deflection
    ///   • Metallic carbon-textured thumb puck with center crosshairs and status core
    ///   • Normalized analog vector output with smooth deadzone and spring-back
    ///   • Compatible with Rive flight_trackpad.riv when imported
    /// </summary>
    public class TacticalFlightTrackpad : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public enum TrackpadStyle
        {
            TacticalCircle,
            CyberHexagon,
            CleanCockpit
        }

        public static TacticalFlightTrackpad Instance { get; private set; }

        [Header("Trackpad Style & Sprites")]
        [SerializeField] private TrackpadStyle style = TrackpadStyle.TacticalCircle;
        [SerializeField] private Sprite customPadSprite;
        [SerializeField] private Sprite customPuckSprite;
        [SerializeField] private Sprite customPadGlowSprite;
        [SerializeField] private Sprite customPuckGlowSprite;

        [Header("Trackpad Dimensions")]
        [SerializeField] private float trackpadRadius = 85f;
        [SerializeField] private float deadZone = 0.08f;
        [SerializeField] private float returnSpeed = 24f;

        [Header("Colors & Styling")]
        [SerializeField] private Color outerRingColor = new Color(0.9f, 0.95f, 1f, 0.9f);
        [SerializeField] private Color padGlowIdleColor = new Color(0f, 0.85f, 1f, 0.22f);
        [SerializeField] private Color padGlowActiveColor = new Color(0f, 1f, 0.9f, 0.85f);
        [SerializeField] private Color puckNormalColor = new Color(0.95f, 0.95f, 1f, 0.95f);
        [SerializeField] private Color puckPressedColor = new Color(0.4f, 0.9f, 1f, 1f);
        [SerializeField] private Color chevronActiveColor = new Color(0f, 1f, 0.85f, 0.95f);
        [SerializeField] private Color chevronDimColor = new Color(0f, 0.85f, 1f, 0.25f);

        private RectTransform baseRect;
        private RectTransform puckRect;
        private Image baseImg;
        private Image baseGlowImg;
        private Image puckImg;
        private Image puckGlow;
        private TextMeshProUGUI[] chevronTexts; // 0=Up, 1=Right, 2=Down, 3=Left

        private Vector2 inputVector = Vector2.zero;
        private bool isPointerHeld = false;
        private Camera eventCamera;

        public Vector2 InputDirection => inputVector;
        public bool IsActive => isPointerHeld || inputVector.sqrMagnitude > 0.01f;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            BuildTrackpadUI();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static TacticalFlightTrackpad EnsureInstance(Transform parent)
        {
            if (Instance != null) return Instance;
            var existing = FindAnyObjectByType<TacticalFlightTrackpad>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            var go = new GameObject("TacticalFlightTrackpad");
            if (parent != null) go.transform.SetParent(parent, false);
            return go.AddComponent<TacticalFlightTrackpad>();
        }

        private void BuildTrackpadUI()
        {
            if (baseRect != null) return;

            // Load professional Kenney Mobile Controls sprites
            LoadStyleSprites();

            // Base Rect in bottom-left
            baseRect = GetComponent<RectTransform>();
            if (baseRect == null) baseRect = gameObject.AddComponent<RectTransform>();

            baseRect.anchorMin = new Vector2(0f, 0f);
            baseRect.anchorMax = new Vector2(0f, 0f);
            baseRect.pivot = new Vector2(0.5f, 0.5f);
            baseRect.anchoredPosition = new Vector2(175f, 175f);
            baseRect.sizeDelta = new Vector2(trackpadRadius * 2.3f, trackpadRadius * 2.3f);

            // Outer Reticle Background Image (High-Resolution Military Pad)
            baseImg = gameObject.AddComponent<Image>();
            baseImg.sprite = customPadSprite;
            baseImg.color = outerRingColor;
            baseImg.raycastTarget = true;

            // Generous Touch Target Zone (allows thumbs to catch controller easily without requiring exact pixel precision)
            var touchZoneGo = new GameObject("TouchZone", typeof(RectTransform), typeof(Image));
            touchZoneGo.transform.SetParent(transform, false);
            var tzRect = touchZoneGo.GetComponent<RectTransform>();
            tzRect.anchorMin = new Vector2(0.5f, 0.5f);
            tzRect.anchorMax = new Vector2(0.5f, 0.5f);
            tzRect.pivot = new Vector2(0.5f, 0.5f);
            tzRect.anchoredPosition = Vector2.zero;
            tzRect.sizeDelta = new Vector2(340f, 340f);
            var tzImg = touchZoneGo.GetComponent<Image>();
            tzImg.color = Color.clear;
            tzImg.raycastTarget = true;
            touchZoneGo.transform.SetAsFirstSibling();

            // Reactive Outer Neon Glow Ring
            if (customPadGlowSprite != null)
            {
                var baseGlowGo = new GameObject("BaseGlow", typeof(RectTransform), typeof(Image));
                baseGlowGo.transform.SetParent(transform, false);
                var glowRect = baseGlowGo.GetComponent<RectTransform>();
                glowRect.anchorMin = Vector2.zero;
                glowRect.anchorMax = Vector2.one;
                glowRect.sizeDelta = Vector2.zero;
                baseGlowImg = baseGlowGo.GetComponent<Image>();
                baseGlowImg.sprite = customPadGlowSprite;
                baseGlowImg.color = padGlowIdleColor;
                baseGlowImg.raycastTarget = false;
            }

            // Directional Chevrons
            chevronTexts = new TextMeshProUGUI[4];
            chevronTexts[0] = CreateChevron("Chevron_Up", new Vector2(0f, trackpadRadius * 0.95f), 0f);
            chevronTexts[1] = CreateChevron("Chevron_Right", new Vector2(trackpadRadius * 0.95f, 0f), -90f);
            chevronTexts[2] = CreateChevron("Chevron_Down", new Vector2(0f, -trackpadRadius * 0.95f), 180f);
            chevronTexts[3] = CreateChevron("Chevron_Left", new Vector2(-trackpadRadius * 0.95f, 0f), 90f);

            // Center Thumb Puck (Tactical Crosshair Nub)
            var puckGo = new GameObject("Puck", typeof(RectTransform), typeof(Image));
            puckGo.transform.SetParent(transform, false);
            puckRect = puckGo.GetComponent<RectTransform>();
            puckRect.anchorMin = new Vector2(0.5f, 0.5f);
            puckRect.anchorMax = new Vector2(0.5f, 0.5f);
            puckRect.pivot = new Vector2(0.5f, 0.5f);
            puckRect.sizeDelta = new Vector2(80f, 80f);
            puckRect.anchoredPosition = Vector2.zero;

            puckImg = puckGo.GetComponent<Image>();
            puckImg.sprite = customPuckSprite;
            puckImg.color = puckNormalColor;
            puckImg.raycastTarget = false;

            // Puck Reactive Glow Overlay
            if (customPuckGlowSprite != null)
            {
                var glowGo = new GameObject("PuckGlow", typeof(RectTransform), typeof(Image));
                glowGo.transform.SetParent(puckGo.transform, false);
                var glowRect = glowGo.GetComponent<RectTransform>();
                glowRect.anchorMin = Vector2.zero;
                glowRect.anchorMax = Vector2.one;
                glowRect.sizeDelta = new Vector2(10f, 10f);
                puckGlow = glowGo.GetComponent<Image>();
                puckGlow.sprite = customPuckGlowSprite;
                puckGlow.color = new Color(0f, 0.9f, 1f, 0.45f);
                puckGlow.raycastTarget = false;
            }
        }

        private void LoadStyleSprites()
        {
            if (customPadSprite != null && customPuckSprite != null) return;

            switch (style)
            {
                case TrackpadStyle.CyberHexagon:
                    if (customPadSprite == null) customPadSprite = Resources.Load<Sprite>("MobileControls/pad_hexagon_tactical");
                    if (customPuckSprite == null) customPuckSprite = Resources.Load<Sprite>("MobileControls/nub_hexagon");
                    if (customPadGlowSprite == null) customPadGlowSprite = Resources.Load<Sprite>("MobileControls/pad_glow_amber");
                    if (customPuckGlowSprite == null) customPuckGlowSprite = Resources.Load<Sprite>("MobileControls/nub_glow_amber");
                    break;

                case TrackpadStyle.CleanCockpit:
                    if (customPadSprite == null) customPadSprite = Resources.Load<Sprite>("MobileControls/pad_circle_clean");
                    if (customPuckSprite == null) customPuckSprite = Resources.Load<Sprite>("MobileControls/nub_grip");
                    if (customPadGlowSprite == null) customPadGlowSprite = Resources.Load<Sprite>("MobileControls/pad_glow_neon");
                    if (customPuckGlowSprite == null) customPuckGlowSprite = Resources.Load<Sprite>("MobileControls/nub_glow_neon");
                    break;

                case TrackpadStyle.TacticalCircle:
                default:
                    if (customPadSprite == null) customPadSprite = Resources.Load<Sprite>("MobileControls/pad_circle_tactical");
                    if (customPuckSprite == null) customPuckSprite = Resources.Load<Sprite>("MobileControls/nub_crosshair");
                    if (customPadGlowSprite == null) customPadGlowSprite = Resources.Load<Sprite>("MobileControls/pad_glow_neon");
                    if (customPuckGlowSprite == null) customPuckGlowSprite = Resources.Load<Sprite>("MobileControls/nub_glow_neon");
                    break;
            }

            // Fallback safety if resources not yet re-indexed
            if (customPadSprite == null) customPadSprite = Starfall.VFX.ProceduralSprites.Ring;
            if (customPuckSprite == null) customPuckSprite = Starfall.VFX.ProceduralSprites.SoftCircle;
        }

        private TextMeshProUGUI CreateChevron(string name, Vector2 pos, float rotation)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(transform, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(0.5f, 0.5f);
            r.anchorMax = new Vector2(0.5f, 0.5f);
            r.anchoredPosition = pos;
            r.localRotation = Quaternion.Euler(0f, 0f, rotation);
            r.sizeDelta = new Vector2(24f, 24f);

            var tmp = go.GetComponent<TextMeshProUGUI>();
            tmp.text = "^";
            tmp.fontSize = 18;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = chevronDimColor;
            tmp.raycastTarget = false;
            return tmp;
        }

        private void Update()
        {
            if (!isPointerHeld && puckRect != null)
            {
                puckRect.anchoredPosition = Vector2.Lerp(puckRect.anchoredPosition, Vector2.zero, returnSpeed * Time.unscaledDeltaTime);
                inputVector = Vector2.Lerp(inputVector, Vector2.zero, returnSpeed * Time.unscaledDeltaTime);
                if (inputVector.sqrMagnitude < 0.001f)
                {
                    inputVector = Vector2.zero;
                    puckRect.anchoredPosition = Vector2.zero;
                }
                UpdateChevronHighlights();

                if (baseGlowImg != null)
                {
                    baseGlowImg.color = Color.Lerp(baseGlowImg.color, padGlowIdleColor, returnSpeed * Time.unscaledDeltaTime);
                }
            }
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            isPointerHeld = true;
            eventCamera = eventData.pressEventCamera;
            if (puckImg != null) puckImg.color = puckPressedColor;
            if (puckGlow != null) puckGlow.color = new Color(0f, 1f, 0.9f, 0.9f);
            if (baseGlowImg != null) baseGlowImg.color = padGlowActiveColor;
            Starfall.Audio.AudioManager.Instance?.PlayFormationEntry(0.25f);
            OnDrag(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (baseRect == null || puckRect == null) return;

            Vector2 localPoint;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(baseRect, eventData.position, eventCamera, out localPoint))
            {
                float dist = localPoint.magnitude;
                Vector2 dir = dist > 0.001f ? (localPoint / dist) : Vector2.zero;

                float clampedDist = Mathf.Min(dist, trackpadRadius);
                puckRect.anchoredPosition = dir * clampedDist;

                float normalizedMag = Mathf.Clamp01(clampedDist / trackpadRadius);
                if (normalizedMag < deadZone)
                {
                    inputVector = Vector2.zero;
                }
                else
                {
                    // Linear-curved response: instant control for micro-dodges and full-speed travel
                    float curved = Mathf.Pow((normalizedMag - deadZone) / (1f - deadZone), 1.1f);
                    inputVector = dir * curved;
                }

                if (baseGlowImg != null)
                {
                    baseGlowImg.color = Color.Lerp(padGlowIdleColor, padGlowActiveColor, Mathf.Clamp01(normalizedMag + 0.2f));
                }

                UpdateChevronHighlights();
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            isPointerHeld = false;
            inputVector = Vector2.zero;
            if (puckRect != null) puckRect.anchoredPosition = Vector2.zero;
            if (puckImg != null) puckImg.color = puckNormalColor;
            if (puckGlow != null) puckGlow.color = new Color(0f, 0.9f, 1f, 0.45f);
            if (baseGlowImg != null) baseGlowImg.color = padGlowIdleColor;
            UpdateChevronHighlights();
        }

        private void UpdateChevronHighlights()
        {
            if (chevronTexts == null || chevronTexts.Length < 4) return;

            // Highlight chevrons based on inputVector directions
            SetChevronState(0, inputVector.y > 0.25f); // Up
            SetChevronState(1, inputVector.x > 0.25f); // Right
            SetChevronState(2, inputVector.y < -0.25f); // Down
            SetChevronState(3, inputVector.x < -0.25f); // Left
        }

        private void SetChevronState(int index, bool active)
        {
            if (chevronTexts == null || index < 0 || index >= chevronTexts.Length || chevronTexts[index] == null) return;
            chevronTexts[index].color = active ? chevronActiveColor : chevronDimColor;
        }
    }
}
