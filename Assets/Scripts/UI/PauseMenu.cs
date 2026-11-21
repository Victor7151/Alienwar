using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Starfall.Core;
using Starfall.UI.Theme;

namespace Starfall.UI
{
    /// <summary>
    /// In-game pause system. Builds its own Screen-Space-Overlay canvas (sorting order 500)
    /// so the pause button and menu are always visible on top of the HUD, Rive panels and
    /// mobile controls on every platform.
    ///  • Pause button (top-right, thumb-sized for mobile)
    ///  • Pause panel: RESUME / RESTART / MISSION HQ (home)
    ///  • ESC key and the Android hardware/gesture BACK button toggle pause
    ///  • Auto-pauses when the app is sent to background on mobile
    /// </summary>
    public class PauseMenu : MonoBehaviour
    {
        public static PauseMenu Instance { get; private set; }
        public static bool IsPaused { get; private set; }

        private GameObject pauseButtonGo;
        private GameObject panelGo;
        private GameObject confirmGo;
        private TextMeshProUGUI confirmText;
        private System.Action pendingConfirm;
        private float timeScaleBeforePause = 1f;

        /// <summary>Creates the pause menu once per gameplay scene.</summary>
        public static PauseMenu Ensure()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("PauseMenu");
            return go.AddComponent<PauseMenu>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            IsPaused = false;
            EnsureEventSystem();
            Build();
        }

        private void EnsureEventSystem()
        {
            if (UnityEngine.EventSystems.EventSystem.current == null &&
                FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var esGo = new GameObject("EventSystem");
                esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();

#if ENABLE_INPUT_SYSTEM
                var uiModule = esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                uiModule.AssignDefaultActions();
#else
                esGo.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
#endif
                Debug.Log("[PauseMenu] Ensured EventSystem with InputSystemUIInputModule for mobile touch.");
            }
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
                if (IsPaused)
                {
                    IsPaused = false;
                    AudioListener.pause = false;
                }
            }
        }

        // ------------------------------------------------------------------ input
        private void Update()
        {
            // ESC on keyboard == Android BACK button (Input System maps back to Escape)
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && (kb.escapeKey.wasPressedThisFrame || kb.pKey.wasPressedThisFrame))
            {
                if (confirmGo != null && confirmGo.activeSelf) { CancelConfirm(); return; }
                Toggle();
            }

            // Hide the pause button when the game is over (game-over panel has its own buttons)
            bool canPause = CanPause();
            if (pauseButtonGo != null && pauseButtonGo.activeSelf != (canPause || IsPaused))
                pauseButtonGo.SetActive(canPause || IsPaused);
        }

        private void OnApplicationPause(bool paused)
        {
            // Phone call / home button / notification shade: freeze the run
            if (paused && !IsPaused && CanPause()) Pause();
        }

        private void OnApplicationFocus(bool focus)
        {
#if !UNITY_EDITOR
            if (!focus && !IsPaused && CanPause()) Pause();
#endif
        }

        private bool CanPause()
        {
            var gm = GameManager.Instance;
            if (gm != null && (gm.CurrentState == GameState.GameOver || gm.CurrentState == GameState.Victory))
                return false;
            return true;
        }

        // ------------------------------------------------------------------ API
        public void Toggle()
        {
            if (IsPaused) Resume();
            else if (CanPause()) Pause();
        }

        public void Pause()
        {
            if (IsPaused) return;
            IsPaused = true;
            // Keep 0 when a modal (perk choice, directives) already froze time, so Resume doesn't unfreeze it.
            timeScaleBeforePause = Time.timeScale;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            panelGo.SetActive(true);
            confirmGo.SetActive(false);
        }

        public void Resume()
        {
            if (!IsPaused) return;
            IsPaused = false;
            Time.timeScale = timeScaleBeforePause;
            AudioListener.pause = false;
            panelGo.SetActive(false);
            confirmGo.SetActive(false);
        }

        private void DoRestart()
        {
            IsPaused = false;
            AudioListener.pause = false;
            Time.timeScale = 1f;
            if (GameManager.Instance != null) GameManager.Instance.RestartGame();
            else UnityEngine.SceneManagement.SceneManager.LoadScene(
                UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex);
        }

        private void DoHome()
        {
            IsPaused = false;
            AudioListener.pause = false;
            Time.timeScale = 1f;
            Starfall.Levels.LevelProgressManager.LoadHomeScene();
        }

        private void AskConfirm(string message, System.Action onYes)
        {
            pendingConfirm = onYes;
            confirmText.text = message;
            confirmGo.SetActive(true);
        }

        private void CancelConfirm()
        {
            pendingConfirm = null;
            confirmGo.SetActive(false);
        }

        // ------------------------------------------------------------------ build UI
        private void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 500;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();

            BuildPauseButton();
            BuildPanel();
            BuildConfirm();
            panelGo.SetActive(false);
            confirmGo.SetActive(false);
        }

        private void BuildPauseButton()
        {
            pauseButtonGo = new GameObject("PauseButton", typeof(RectTransform));
            pauseButtonGo.transform.SetParent(transform, false);
            var rt = (RectTransform)pauseButtonGo.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-36f, -30f);
            rt.sizeDelta = new Vector2(80f, 80f);

            var img = pauseButtonGo.AddComponent<Image>();
            var btn = pauseButtonGo.AddComponent<Button>();
            btn.onClick.AddListener(Toggle);
            SciFiUITheme.ApplyButton(btn, img, isPrimary: false);
            img.color = new Color(0.12f, 0.20f, 0.35f, 0.95f);

            // Two vertical bars = universal pause icon (no font glyph dependency)
            for (int i = 0; i < 2; i++)
            {
                var bar = new GameObject("Bar" + i, typeof(RectTransform), typeof(Image));
                bar.transform.SetParent(pauseButtonGo.transform, false);
                var br = (RectTransform)bar.transform;
                br.anchorMin = br.anchorMax = br.pivot = new Vector2(0.5f, 0.5f);
                br.anchoredPosition = new Vector2(i == 0 ? -10f : 10f, 0f);
                br.sizeDelta = new Vector2(10f, 36f);
                var bi = bar.GetComponent<Image>();
                bi.color = new Color(0.00f, 0.90f, 1.00f, 1f);
                bi.raycastTarget = false;
            }
        }

        private void BuildPanel()
        {
            panelGo = CreateDimmer("PausePanel");

            var card = CreateCard(panelGo.transform, new Vector2(620f, 580f));
            CreateLabel(card, "MISSION PAUSED", 48f, new Color(0.00f, 0.90f, 1.00f), new Vector2(0f, 210f), new Vector2(560f, 70f), FontStyles.Bold);
            CreateLabel(card, "Systems on standby, Commander", 20f, new Color(0.6f, 0.75f, 0.90f), new Vector2(0f, 155f), new Vector2(560f, 35f), FontStyles.Italic);

            var resumeBtn = CreateMenuButton(card, "RESUME FLIGHT", new Vector2(0f, 55f), Color.white, Resume, isPrimary: true);
            var anim = resumeBtn.GetComponent<SciFiButtonAnimator>();
            if (anim != null) anim.PulseIdle = true;

            CreateMenuButton(card, "RESTART MISSION", new Vector2(0f, -55f), new Color(1.00f, 0.82f, 0.15f),
                () => AskConfirm("Restart this mission?\nCurrent progress will be lost.", DoRestart), isPrimary: false);
            CreateMenuButton(card, "RETURN TO HQ", new Vector2(0f, -165f), new Color(0.95f, 0.40f, 0.50f),
                () => AskConfirm("Abort mission and return to HQ?", DoHome), isPrimary: false);
        }

        private void BuildConfirm()
        {
            confirmGo = CreateDimmer("ConfirmPanel");
            var card = CreateCard(confirmGo.transform, new Vector2(600f, 360f));
            confirmText = CreateLabel(card, "", 30f, Color.white, new Vector2(0f, 60f), new Vector2(540f, 140f), FontStyles.Bold);

            var yes = CreateMenuButton(card, "CONFIRM", new Vector2(-140f, -95f), new Color(1.00f, 0.30f, 0.35f), () =>
            {
                var a = pendingConfirm;
                pendingConfirm = null;
                a?.Invoke();
            }, isPrimary: false);
            ((RectTransform)yes.transform).sizeDelta = new Vector2(230f, 80f);

            var no = CreateMenuButton(card, "CANCEL", new Vector2(140f, -95f), Color.white, CancelConfirm, isPrimary: true);
            ((RectTransform)no.transform).sizeDelta = new Vector2(230f, 80f);
        }

        // ------------------------------------------------------------------ helpers
        private GameObject CreateDimmer(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            go.GetComponent<Image>().color = new Color(0.01f, 0.02f, 0.05f, 0.82f); // blocks taps to the game
            return go;
        }

        private Transform CreateCard(Transform parent, Vector2 size)
        {
            var go = new GameObject("Card", typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = size;
            var img = go.GetComponent<Image>();
            SciFiUITheme.ApplyPanel(img);
            return go.transform;
        }

        private TextMeshProUGUI CreateLabel(Transform parent, string text, float size, Color color,
                                            Vector2 pos, Vector2 box, FontStyles style)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = box;
            var t = go.AddComponent<TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.fontStyle = style;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            return t;
        }

        private GameObject CreateMenuButton(Transform parent, string label, Vector2 pos, Color color, UnityEngine.Events.UnityAction onClick, bool isPrimary = false)
        {
            var go = new GameObject(label + "_Btn", typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(460f, 84f); // large touch target
            var img = go.GetComponent<Image>();
            var btn = go.GetComponent<Button>();
            btn.onClick.AddListener(onClick);
            SciFiUITheme.ApplyButton(btn, img, isPrimary: isPrimary);
            if (!isPrimary) img.color = color;
            CreateLabel(go.transform, label, 28f, Color.white, Vector2.zero, rt.sizeDelta, FontStyles.Bold);
            return go;
        }

        private static void SetButtonColors(Button btn)
        {
            var c = btn.colors;
            c.highlightedColor = new Color(1.15f, 1.15f, 1.15f, 1f);
            c.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            c.fadeDuration = 0.05f;
            btn.colors = c;
        }
    }
}
