using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;
using Starfall.Core;
using Starfall.Levels;
using Starfall.Audio;

namespace Starfall.UI
{
    /// <summary>
    /// Tactical Sector Cleared UI.
    /// Displays mission clearance, star rating, final score,
    /// dynamic mission objectives breakdown with bounty credits earned,
    /// and an optional player-initiated transmission bonus.
    /// </summary>
    public class MissionCompleteUI : MonoBehaviour
    {
        [Header("Panel")]
        [SerializeField] private CanvasGroup canvasGroup;
        [SerializeField] private RectTransform panelRect;
        [SerializeField] private float animDuration = 0.6f;

        [Header("Text Elements (Legacy / Fallback)")]
        [SerializeField] private Text titleText;
        [SerializeField] private Text scoreText;
        [SerializeField] private Text starsText;

        [Header("Buttons")]
        [SerializeField] private Button warpButton;
        [SerializeField] private Button retryButton;

        [Header("TMP Dynamic Elements")]
        private TextMeshProUGUI tmpTitle;
        private TextMeshProUGUI tmpScore;
        private TextMeshProUGUI tmpStars;
        private TextMeshProUGUI tmpObjectives;
        private TextMeshProUGUI tmpCredits;
        private Button rewardedAdButton;
        private TextMeshProUGUI rewardedAdBtnText;
        private bool hasClaimedAdBonus = false;

        [Header("Star Thresholds")]
        [SerializeField] private int threeStarScore = 15000;
        [SerializeField] private int twoStarScore = 8000;

        private void Awake()
        {
            EnsureCanvasGroup();
            HideInstant();

            if (warpButton != null) warpButton.onClick.AddListener(OnWarp);
            if (retryButton != null) retryButton.onClick.AddListener(OnRetry);
        }

        private void EnsureCanvasGroup()
        {
            if (canvasGroup == null)
            {
                canvasGroup = GetComponent<CanvasGroup>();
                if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
            }
        }

        private void HideInstant()
        {
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 0f;
                canvasGroup.interactable = false;
                canvasGroup.blocksRaycasts = false;
            }
        }

        private void OnEnable()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnPlanetCleared += Show;

            if (LevelManager.Instance != null)
                LevelManager.Instance.OnMissionCompleted += HandleMissionCompleted;
        }

        private void OnDisable()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.OnPlanetCleared -= Show;

            if (LevelManager.Instance != null)
                LevelManager.Instance.OnMissionCompleted -= HandleMissionCompleted;
        }

        private void HandleMissionCompleted(MissionData mission, int credits)
        {
            int lvl = LevelManager.Instance != null ? LevelManager.Instance.CurrentLevelNumber : 1;
            Show(lvl);
        }

        private void Show(int levelNumber)
        {
            int score = GameManager.Instance != null ? GameManager.Instance.CurrentScore : 0;
            hasClaimedAdBonus = false;

            // Compute dynamic star rating based on score and completed objectives
            int completedObjs = MissionObjectiveManager.Instance != null 
                ? MissionObjectiveManager.Instance.CompletedObjectivesCount 
                : 1;

            int stars = 1;
            if (score >= threeStarScore || completedObjs >= 3) stars = 3;
            else if (score >= twoStarScore || completedObjs >= 2) stars = 2;

            BuildDynamicVisuals(levelNumber, score, stars);
            StartCoroutine(AnimateIn());
        }

        private void BuildDynamicVisuals(int levelNumber, int score, int stars)
        {
            // Build or refresh modal card inside this GameObject
            var existingContainer = transform.Find("ModalContainer");
            if (existingContainer != null) Destroy(existingContainer.gameObject);

            var containerGo = new GameObject("ModalContainer", typeof(RectTransform));
            containerGo.transform.SetParent(transform, false);
            var cRect = containerGo.GetComponent<RectTransform>();
            cRect.anchorMin = new Vector2(0.5f, 0.5f);
            cRect.anchorMax = new Vector2(0.5f, 0.5f);
            cRect.pivot = new Vector2(0.5f, 0.5f);
            cRect.sizeDelta = new Vector2(700f, 510f);

            var bg = containerGo.AddComponent<Image>();
            Starfall.UI.Theme.SciFiUITheme.ApplyPanel(bg);

            // Header Banner
            var headerGo = new GameObject("Header", typeof(RectTransform), typeof(TextMeshProUGUI));
            headerGo.transform.SetParent(containerGo.transform, false);
            var hRect = headerGo.GetComponent<RectTransform>();
            hRect.anchorMin = new Vector2(0f, 1f);
            hRect.anchorMax = new Vector2(1f, 1f);
            hRect.pivot = new Vector2(0.5f, 1f);
            hRect.anchoredPosition = new Vector2(0f, -20f);
            hRect.sizeDelta = new Vector2(0f, 40f);

            tmpTitle = headerGo.GetComponent<TextMeshProUGUI>();
            string sectorName = levelNumber switch
            {
                2 => "SECTOR 02: ASTEROID BELT",
                3 => "SECTOR 03: DEEP NEBULA",
                4 => "SECTOR 04: WAR-TORN WRECKAGE",
                _ => "SECTOR 01: EARTH ORBIT"
            };
            tmpTitle.text = $"<color=#00E5FF><b>/// {sectorName} SECURED ///</b></color>";
            tmpTitle.fontSize = 23;
            tmpTitle.fontStyle = FontStyles.Bold;
            tmpTitle.alignment = TextAlignmentOptions.Center;

            // Score & Star Rating
            var scoreGo = new GameObject("ScoreAndStars", typeof(RectTransform), typeof(TextMeshProUGUI));
            scoreGo.transform.SetParent(containerGo.transform, false);
            var sRect = scoreGo.GetComponent<RectTransform>();
            sRect.anchorMin = new Vector2(0f, 1f);
            sRect.anchorMax = new Vector2(1f, 1f);
            sRect.pivot = new Vector2(0.5f, 1f);
            sRect.anchoredPosition = new Vector2(0f, -62f);
            sRect.sizeDelta = new Vector2(0f, 32f);

            tmpScore = scoreGo.GetComponent<TextMeshProUGUI>();
            string starGlyphs = stars == 3 
                ? "<color=#FFD700>[3/3 STARS]</color> <size=13><color=#00FF88>[ELITE CLEARED]</color></size>" 
                : (stars == 2 
                    ? "<color=#FFD700>[2/3 STARS]</color> <size=13><color=#00E5FF>[COMMENDED]</color></size>" 
                    : "<color=#FFD700>[1/3 STARS]</color> <size=13><color=#88AACC>[QUALIFIED]</color></size>");
            tmpScore.text = $"TELEMETRY SCORE: <color=#FFD700><b>{score:N0} PTS</b></color>   •   RATING: <b>{starGlyphs}</b>";
            tmpScore.fontSize = 16;
            tmpScore.alignment = TextAlignmentOptions.Center;

            // Objectives Container
            var objBoxGo = new GameObject("ObjectivesBox", typeof(RectTransform), typeof(Image));
            objBoxGo.transform.SetParent(containerGo.transform, false);
            var obRect = objBoxGo.GetComponent<RectTransform>();
            obRect.anchorMin = new Vector2(0.06f, 0.30f);
            obRect.anchorMax = new Vector2(0.94f, 0.83f);
            obRect.sizeDelta = Vector2.zero;
            var obBg = objBoxGo.GetComponent<Image>();
            Starfall.UI.Theme.SciFiUITheme.ApplyGlass(obBg);

            var objTextGo = new GameObject("ObjectivesText", typeof(RectTransform), typeof(TextMeshProUGUI));
            objTextGo.transform.SetParent(objBoxGo.transform, false);
            var otRect = objTextGo.GetComponent<RectTransform>();
            otRect.anchorMin = Vector2.zero;
            otRect.anchorMax = Vector2.one;
            otRect.offsetMin = new Vector2(16f, 12f);
            otRect.offsetMax = new Vector2(-16f, -12f);

            tmpObjectives = objTextGo.GetComponent<TextMeshProUGUI>();
            tmpObjectives.fontSize = 13;
            tmpObjectives.lineSpacing = 7;
            tmpObjectives.color = Color.white;

            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<size=14><color=#00E5FF><b>MISSION OBJECTIVES TELEMETRY:</b></color></size>");

            var objList = MissionObjectiveManager.Instance != null 
                ? MissionObjectiveManager.Instance.ActiveObjectives 
                : MissionObjectiveManager.GenerateObjectives(levelNumber);

            int totalCreditsEarned = 0;
            foreach (var obj in objList)
            {
                if (obj.isCompleted)
                {
                    sb.AppendLine($"<color=#00FF88><b>[OK]</b> {obj.title}</color>  —  <color=#FFD700>+{obj.starCreditsReward} Credits</color>");
                    totalCreditsEarned += obj.starCreditsReward;
                }
                else
                {
                    sb.AppendLine($"<color=#667788>[ ] {obj.title}</color>  —  <size=11><color=#8899AA>{obj.description}</color></size>");
                }
            }

            int currentTotalCredits = LevelManager.Instance != null ? LevelManager.Instance.StarCredits : totalCreditsEarned;
            sb.AppendLine($"\nTOTAL BOUNTY EARNED: <color=#00FF88><b>+{totalCreditsEarned} CREDITS</b></color> (Fleet Bank: {currentTotalCredits:N0} CR)");
            tmpObjectives.text = sb.ToString();

            // Optional Rewarded Ad Transmission Button (Non-intrusive / player choice)
            var adBtnGo = new GameObject("RewardedAdBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            adBtnGo.transform.SetParent(containerGo.transform, false);
            var adRect = adBtnGo.GetComponent<RectTransform>();
            adRect.anchorMin = new Vector2(0.06f, 0.17f);
            adRect.anchorMax = new Vector2(0.94f, 0.27f);
            adRect.sizeDelta = Vector2.zero;

            var adImg = adBtnGo.GetComponent<Image>();
            rewardedAdButton = adBtnGo.GetComponent<Button>();
            Starfall.UI.Theme.SciFiUITheme.ApplyButton(rewardedAdButton, adImg, isPrimary: false);
            adImg.color = new Color(0.18f, 0.95f, 0.65f, 0.95f);

            var adTextGo = new GameObject("AdText", typeof(RectTransform), typeof(TextMeshProUGUI));
            adTextGo.transform.SetParent(adBtnGo.transform, false);
            var atRect = adTextGo.GetComponent<RectTransform>();
            atRect.anchorMin = Vector2.zero;
            atRect.anchorMax = Vector2.one;
            atRect.sizeDelta = Vector2.zero;

            rewardedAdBtnText = adTextGo.GetComponent<TextMeshProUGUI>();
            rewardedAdBtnText.text = "<b>[OPTIONAL INTEL] TRANSMISSION DECRYPTION (+150 STAR CREDITS)</b>";
            rewardedAdBtnText.fontSize = 12;
            rewardedAdBtnText.fontStyle = FontStyles.Bold;
            rewardedAdBtnText.alignment = TextAlignmentOptions.Center;
            rewardedAdBtnText.color = new Color(0.4f, 1f, 0.75f);

            rewardedAdButton.onClick.AddListener(OnClaimRewardedAdTransmission);

            // Bottom Nav Buttons Row (Warp Next Sector & Return to HQ)
            var navRowGo = new GameObject("NavRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            navRowGo.transform.SetParent(containerGo.transform, false);
            var nRect = navRowGo.GetComponent<RectTransform>();
            nRect.anchorMin = new Vector2(0.06f, 0.04f);
            nRect.anchorMax = new Vector2(0.94f, 0.15f);
            nRect.sizeDelta = Vector2.zero;

            var layout = navRowGo.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 15f;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            // Return to HQ Button
            CreateNavButton(navRowGo.transform, "RETURN TO HQ", new Color(0.70f, 0.82f, 0.98f), () =>
            {
                LevelProgressManager.LoadLevelSelect();
            }, isPrimary: false);

            // Warp Next Sector / Victory Button
            string nextLabel = (levelNumber < 4) ? "WARP TO NEXT SECTOR  >>" : "CAMPAIGN VICTORY [HQ]";
            var warpBtn = CreateNavButton(navRowGo.transform, nextLabel, Color.white, OnWarp, isPrimary: true);
            var warpAnim = warpBtn.GetComponent<Starfall.UI.Theme.SciFiButtonAnimator>();
            if (warpAnim != null) warpAnim.PulseIdle = true;
        }

        private GameObject CreateNavButton(Transform parent, string label, Color color, UnityEngine.Events.UnityAction action, bool isPrimary = false)
        {
            var btnGo = new GameObject("NavBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(parent, false);
            var img = btnGo.GetComponent<Image>();
            var btn = btnGo.GetComponent<Button>();
            btn.onClick.AddListener(action);

            Starfall.UI.Theme.SciFiUITheme.ApplyButton(btn, img, isPrimary: isPrimary);
            if (!isPrimary) img.color = color;

            var tGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            tGo.transform.SetParent(btnGo.transform, false);
            var tr = tGo.GetComponent<RectTransform>();
            tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.sizeDelta = Vector2.zero;
            var tmp = tGo.GetComponent<TextMeshProUGUI>();
            tmp.text = $"<b>{label}</b>";
            tmp.fontSize = 13;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            return btnGo;
        }

        private void OnClaimRewardedAdTransmission()
        {
            if (hasClaimedAdBonus) return;
            hasClaimedAdBonus = true;

            // Award 150 Star Credits cleanly
            if (LevelManager.Instance != null)
            {
                LevelManager.Instance.AddStarCredits(150);
            }

            AudioManager.Instance?.PlayPowerUp();
            if (rewardedAdBtnText != null)
            {
                rewardedAdBtnText.text = "<color=#00FF88><b>TRANSMISSION LOGGED! +150 CREDITS CREDITED TO FLEET BANK</b></color>";
            }
            if (rewardedAdButton != null)
            {
                rewardedAdButton.interactable = false;
            }
        }

        private IEnumerator AnimateIn()
        {
            EnsureCanvasGroup();
            canvasGroup.interactable = false;
            canvasGroup.blocksRaycasts = false;

            float elapsed = 0f;
            while (elapsed < animDuration)
            {
                elapsed += Time.unscaledDeltaTime;
                canvasGroup.alpha = Mathf.SmoothStep(0f, 1f, elapsed / animDuration);
                yield return null;
            }

            canvasGroup.alpha = 1f;
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        private void OnWarp()
        {
            int currentLvl = LevelManager.Instance != null ? LevelManager.Instance.CurrentLevelNumber : 1;
            int nextLvl = currentLvl + 1;
            Debug.Log($"[MissionCompleteUI] Advancing from Sector {currentLvl}...");

            if (nextLvl <= 4)
            {
                LevelProgressManager.LoadLevel(nextLvl);
            }
            else
            {
                LevelProgressManager.LoadLevelSelect();
            }
        }

        private void OnRetry()
        {
            if (GameManager.Instance != null)
                GameManager.Instance.RestartGame();
        }
    }
}
