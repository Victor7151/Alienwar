using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;

namespace Starfall.UI
{
    /// <summary>
    /// Persistent loading overlay used for every scene change: a lightweight UGUI spinner,
    /// progress bar and rotating tactical tip while the next scene loads asynchronously.
    /// </summary>
    public class LoadingScreen : MonoBehaviour
    {
        private static LoadingScreen instance;

        private static readonly string[] Tips =
        {
            "TIP: Taking damage breaks your combo. Clean play scores up to x5.",
            "TIP: Green Gunners show a laser sight before firing — move off the line.",
            "TIP: Red Chargers lock on mid-flash. Sidestep late, not early.",
            "TIP: Kill blue Shielders first — they halve damage to everything around them.",
            "TIP: Curtain volleys always leave a gap. Look for the dark lane.",
            "TIP: Losing a ship costs a weapon level. Bombs are cheaper than lives.",
            "TIP: Clear a set quickly for a SWIFT CLEAR bonus.",
            "TIP: Finish a wave without a hit for a FLAWLESS bonus.",
            "TIP: Pink Blinkers fire the moment they reappear.",
            "TIP: Purple mines burst into a ring — stay out of their radius."
        };

        private CanvasGroup group;
        private Image progressFill;
        private TextMeshProUGUI tipText;
        private TextMeshProUGUI titleText;
        private RectTransform spinner;
        private RectTransform spinnerInner;
        private bool busy;

        /// <summary>
        /// Build the overlay (and parse loading.riv) once at startup so the first "Launch" press
        /// doesn't stall while it is created.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Prewarm() => GetOrCreate();

        /// <summary>Loads a scene behind the loading overlay.</summary>
        public static void LoadScene(string sceneName, string title = null)
        {
            var screen = GetOrCreate();
            if (screen.busy) return;
            screen.StartCoroutine(screen.LoadRoutine(sceneName, title));
        }

        private static LoadingScreen GetOrCreate()
        {
            if (instance != null) return instance;

            var go = new GameObject("LoadingScreen", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
            DontDestroyOnLoad(go);
            instance = go.AddComponent<LoadingScreen>();
            instance.Build();
            return instance;
        }

        private void Build()
        {
            var canvas = GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 1000;
            var scaler = GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            group = GetComponent<CanvasGroup>();
            group.alpha = 0f;
            group.blocksRaycasts = false;

            var bg = new GameObject("Backdrop", typeof(RectTransform), typeof(Image));
            bg.transform.SetParent(transform, false);
            var bgImg = bg.GetComponent<Image>();
            bgImg.color = new Color(0.02f, 0.03f, 0.07f, 1f);
            Stretch(bgImg.rectTransform);

            // Lightweight UGUI spinner (the Rive loader was removed: parsing/rendering it cost frames on low-end devices).
            spinner = MakeRing("Spinner", 150f, new Color(0.2f, 0.9f, 1f, 0.9f));
            spinnerInner = MakeRing("SpinnerInner", 96f, new Color(1f, 0.55f, 1f, 0.7f));

            titleText = MakeText("Title", 44f, new Vector2(0.5f, 0.72f), new Vector2(900f, 80f));
            tipText = MakeText("Tip", 22f, new Vector2(0.5f, 0.3f), new Vector2(900f, 90f));
            tipText.color = new Color(0.7f, 0.85f, 1f);
            tipText.textWrappingMode = TextWrappingModes.Normal;

            var track = new GameObject("ProgressTrack", typeof(RectTransform), typeof(Image));
            track.transform.SetParent(transform, false);
            var tImg = track.GetComponent<Image>();
            tImg.color = new Color(1f, 1f, 1f, 0.12f);
            var trt = tImg.rectTransform;
            trt.anchorMin = trt.anchorMax = new Vector2(0.5f, 0.38f);
            trt.sizeDelta = new Vector2(520f, 8f);

            var fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
            fill.transform.SetParent(track.transform, false);
            progressFill = fill.GetComponent<Image>();
            progressFill.color = new Color(0.2f, 0.9f, 1f);
            var frt = progressFill.rectTransform;
            frt.anchorMin = Vector2.zero;
            frt.anchorMax = new Vector2(0f, 1f);
            frt.offsetMin = Vector2.zero;
            frt.offsetMax = Vector2.zero;

            gameObject.SetActive(true);
        }

        private RectTransform MakeRing(string name, float size, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(transform, false);
            var img = go.GetComponent<Image>();
            img.sprite = Starfall.VFX.ProceduralSprites.Ring;
            img.color = color;
            img.raycastTarget = false;
            // Partial arc so the rotation is visible.
            img.type = Image.Type.Filled;
            img.fillMethod = Image.FillMethod.Radial360;
            img.fillAmount = 0.7f;
            var rt = img.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.55f);
            rt.sizeDelta = new Vector2(size, size);
            return rt;
        }

        private void Update()
        {
            if (group == null || group.alpha <= 0f || spinner == null) return;
            float dt = Time.unscaledDeltaTime;
            spinner.Rotate(0f, 0f, -240f * dt);
            spinnerInner.Rotate(0f, 0f, 360f * dt);
        }

        private TextMeshProUGUI MakeText(string name, float size, Vector2 anchor, Vector2 box)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(transform, false);
            var t = go.GetComponent<TextMeshProUGUI>();
            t.fontSize = size;
            t.fontStyle = FontStyles.Bold;
            t.alignment = TextAlignmentOptions.Center;
            t.raycastTarget = false;
            var rt = t.rectTransform;
            rt.anchorMin = rt.anchorMax = anchor;
            rt.sizeDelta = box;
            return t;
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private IEnumerator LoadRoutine(string sceneName, string title)
        {
            busy = true;
            Time.timeScale = 1f;
            group.blocksRaycasts = true;
            titleText.text = ""; // Removed title
            tipText.text = ""; // Removed tips
            progressFill.rectTransform.anchorMax = new Vector2(0f, 1f);
            
            // Skip the fade-in delay
            group.alpha = 1f;

            var op = SceneManager.LoadSceneAsync(sceneName);
            if (op == null)
            {
                Debug.LogError($"[LoadingScreen] Scene '{sceneName}' is not in Build Settings.");
                busy = false;
                group.alpha = 0f;
                group.blocksRaycasts = false;
                yield break;
            }
            
            // Immediately allow the scene to activate
            op.allowSceneActivation = true;

            // Only wait for actual loading to complete, no artificial delays
            while (!op.isDone)
            {
                progressFill.rectTransform.anchorMax = new Vector2(op.progress / 0.9f, 1f);
                yield return null;
            }

            // Skip the fade-out delay and "calm frames" settling delay
            group.alpha = 0f;
            group.blocksRaycasts = false;
            busy = false;
        }
    }
}
