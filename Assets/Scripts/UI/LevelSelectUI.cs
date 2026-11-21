using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Starfall.Levels;
using Starfall.UI.Theme;

namespace Starfall.UI
{
    /// <summary>
    /// LevelSelectUI – Mission deployment & replay terminal.
    /// Displays all campaign levels with unlocked status, current progress, high scores,
    /// interactive wave selection matrix (W1..W10), and continue button.
    /// </summary>
    public class LevelSelectUI : MonoBehaviour
    {
        private class LevelCardView
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

        private LevelCardView[] cards = new LevelCardView[4];
        [SerializeField] private Button resetButton;

        private void Awake()
        {
            EnsureEventSystem();
            EnsureUIHierarchy();
        }

        private void Start()
        {
            RefreshLevelCards();
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

        public void RefreshLevelCards()
        {
            for (int i = 0; i < cards.Length; i++)
            {
                var card = cards[i];
                if (card == null) continue;

                int lvl = card.levelNumber;
                bool isUnlocked = LevelProgressManager.IsLevelUnlocked(lvl);
                int highestWave = LevelProgressManager.GetHighestWave(lvl);
                int currentWave = LevelProgressManager.GetCurrentWave(lvl);
                int highScore = LevelProgressManager.GetHighScore(lvl);

                if (isUnlocked)
                {
                    card.cardBackground.color = new Color(0.08f, 0.12f, 0.22f, 0.95f);
                    card.statusText.text = $"STATUS: <color=#00ff88>OPERATIONAL</color>  BEST: <color=#00e5ff>{(highScore > 0 ? highScore.ToString("N0") : "---")}</color>";
                    card.progressText.text = $"PROGRESS: <color=#ffd700>WAVE {currentWave}/10</color> (CLEARED: <color=#00ff88>{highestWave}</color>)";

                    // Primary Action Button (Continue or Replay)
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

                    // Enable wave matrix
                    if (card.waveMatrixContainer != null)
                    {
                        card.waveMatrixContainer.SetActive(true);
                    }

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
                            // Unlocked & playable
                            btn.interactable = true;
                            btn.onClick.AddListener(() =>
                            {
                                LevelProgressManager.LoadLevelAtWave(lvl, waveNum);
                            });

                            if (waveNum == currentWave)
                            {
                                // Active current wave
                                btnImg.color = new Color(1f, 0.75f, 0.1f, 1f); // Gold
                                btnTxt.color = Color.black;
                                btnTxt.text = $"<b>{waveNum}</b>";
                            }
                            else
                            {
                                // Cleared wave
                                btnImg.color = new Color(0.1f, 0.6f, 0.35f, 0.9f); // Green
                                btnTxt.color = Color.white;
                                btnTxt.text = $"{waveNum}";
                            }
                        }
                        else
                        {
                            // Locked wave
                            btn.interactable = false;
                            btnImg.color = new Color(0.12f, 0.15f, 0.22f, 0.6f);
                            btnTxt.color = new Color(0.35f, 0.4f, 0.5f, 0.8f);
                            btnTxt.text = "-";
                        }
                    }
                }
                else
                {
                    // Locked Level
                    card.cardBackground.color = new Color(0.05f, 0.06f, 0.09f, 0.85f);
                    string reqLvl = $"Sector {lvl - 1}";
                    card.statusText.text = $"STATUS: <color=#ff4444>LOCKED (Defeat Boss in {reqLvl})</color>";
                    card.progressText.text = "SECTOR UNDISCOVERED";

                    card.deployButton.interactable = false;
                    card.deployButton.onClick.RemoveAllListeners();
                    if (card.deployButtonText != null)
                    {
                        card.deployButtonText.text = "LOCKED";
                    }

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

            if (resetButton != null)
            {
                resetButton.onClick.RemoveAllListeners();
                resetButton.onClick.AddListener(() =>
                {
                    LevelProgressManager.ResetProgress();
                    RefreshLevelCards();
                });
            }
        }

        private void EnsureUIHierarchy()
        {
            var canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
            {
                var canvasGo = new GameObject("LevelSelectCanvas");
                canvas = canvasGo.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                var scaler = canvasGo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;
                canvasGo.AddComponent<GraphicRaycaster>();
                transform.SetParent(canvasGo.transform, false);
            }

            // Clean existing procedural children if any
            for (int i = canvas.transform.childCount - 1; i >= 0; i--)
            {
                var child = canvas.transform.GetChild(i);
                if (child.gameObject != this.gameObject)
                {
                    DestroyImmediate(child.gameObject);
                }
            }

            // Dark space background
            var bgGo = new GameObject("Background");
            bgGo.transform.SetParent(canvas.transform, false);
            var bgRect = bgGo.AddComponent<RectTransform>();
            bgRect.anchorMin = Vector2.zero;
            bgRect.anchorMax = Vector2.one;
            bgRect.sizeDelta = Vector2.zero;
            var bgImg = bgGo.AddComponent<Image>();
            bgImg.color = new Color(0.03f, 0.04f, 0.08f, 1f);

            // Title
            var titleGo = new GameObject("TitleText");
            titleGo.transform.SetParent(canvas.transform, false);
            var titleRect = titleGo.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.5f, 0.93f);
            titleRect.anchorMax = new Vector2(0.5f, 0.93f);
            titleRect.sizeDelta = new Vector2(1200, 60);
            var titleText = titleGo.AddComponent<TextMeshProUGUI>();
            titleText.text = "STARFALL : COMMAND HQ";
            titleText.fontSize = 44;
            titleText.fontStyle = FontStyles.Bold;
            titleText.alignment = TextAlignmentOptions.Center;
            titleText.color = new Color(0f, 0.9f, 1f);

            // Subtitle
            var subGo = new GameObject("SubtitleText");
            subGo.transform.SetParent(canvas.transform, false);
            var subRect = subGo.AddComponent<RectTransform>();
            subRect.anchorMin = new Vector2(0.5f, 0.87f);
            subRect.anchorMax = new Vector2(0.5f, 0.87f);
            subRect.sizeDelta = new Vector2(1200, 36);
            var subText = subGo.AddComponent<TextMeshProUGUI>();
            subText.text = "SELECT AN UNLOCKED SECTOR OR JUMP DIRECTLY TO ANY CLEARED WAVE";
            subText.fontSize = 18;
            subText.alignment = TextAlignmentOptions.Center;
            subText.color = new Color(0.7f, 0.8f, 0.95f);

            // Cards container (Landscape 1920x1080)
            float[] cardY = { 0.74f, 0.54f, 0.34f, 0.14f };
            string[] titles = { "SECTOR 01: EARTH ORBIT", "SECTOR 02: ASTEROID BELT", "SECTOR 03: DEEP NEBULA", "SECTOR 04: WAR-TORN WRECKAGE" };
            string[] descs =
            {
                "10 Waves • Hazard: Asteroids • Boss: Invader Carrier",
                "10 Waves • Hazard: Debris Crossfire • Boss: Plasma Dreadnought",
                "10 Waves • Hazard: Nebula Swarms • Boss: Hive Queen Leviathan",
                "10 Waves • Hazard: Scorched Slag & Flak • Boss: Inferno Colossus"
            };

            for (int i = 0; i < 4; i++)
            {
                int lvl = i + 1;
                var card = new LevelCardView { levelNumber = lvl };

                var cardGo = new GameObject($"Card_Level_{lvl}");
                cardGo.transform.SetParent(canvas.transform, false);
                var cardRect = cardGo.AddComponent<RectTransform>();
                cardRect.anchorMin = new Vector2(0.5f, cardY[i]);
                cardRect.anchorMax = new Vector2(0.5f, cardY[i]);
                cardRect.sizeDelta = new Vector2(1280, 160);

                card.cardGo = cardGo;
                card.cardBackground = cardGo.AddComponent<Image>();
                SciFiUITheme.ApplyPanel(card.cardBackground);

                // Level title & desc
                var infoGo = new GameObject("InfoText");
                infoGo.transform.SetParent(cardGo.transform, false);
                var infoRect = infoGo.AddComponent<RectTransform>();
                infoRect.anchorMin = new Vector2(0.025f, 0.70f);
                infoRect.anchorMax = new Vector2(0.78f, 0.95f);
                infoRect.sizeDelta = Vector2.zero;
                card.titleText = infoGo.AddComponent<TextMeshProUGUI>();
                card.titleText.text = $"<size=26><b>{titles[i]}</b></size>   <size=16><color=#6699bb>{descs[i]}</color></size>";
                card.titleText.alignment = TextAlignmentOptions.MidlineLeft;

                // Status line
                var statusGo = new GameObject("StatusText");
                statusGo.transform.SetParent(cardGo.transform, false);
                var statusRect = statusGo.AddComponent<RectTransform>();
                statusRect.anchorMin = new Vector2(0.025f, 0.44f);
                statusRect.anchorMax = new Vector2(0.46f, 0.68f);
                statusRect.sizeDelta = Vector2.zero;
                card.statusText = statusGo.AddComponent<TextMeshProUGUI>();
                card.statusText.fontSize = 17;
                card.statusText.alignment = TextAlignmentOptions.MidlineLeft;

                // Progress line
                var progGo = new GameObject("ProgressText");
                progGo.transform.SetParent(cardGo.transform, false);
                var progRect = progGo.AddComponent<RectTransform>();
                progRect.anchorMin = new Vector2(0.48f, 0.44f);
                progRect.anchorMax = new Vector2(0.78f, 0.68f);
                progRect.sizeDelta = Vector2.zero;
                card.progressText = progGo.AddComponent<TextMeshProUGUI>();
                card.progressText.fontSize = 17;
                card.progressText.alignment = TextAlignmentOptions.MidlineLeft;

                // Deploy / Continue Button (Right side)
                var btnGo = new GameObject("DeployButton");
                btnGo.transform.SetParent(cardGo.transform, false);
                var btnRect = btnGo.AddComponent<RectTransform>();
                btnRect.anchorMin = new Vector2(0.81f, 0.15f);
                btnRect.anchorMax = new Vector2(0.975f, 0.85f);
                btnRect.sizeDelta = Vector2.zero;

                var btnImg = btnGo.AddComponent<Image>();
                card.deployButton = btnGo.AddComponent<Button>();
                SciFiUITheme.ApplyButton(card.deployButton, btnImg, isPrimary: true);
                var dAnim = btnGo.GetComponent<SciFiButtonAnimator>();
                if (dAnim != null) dAnim.PulseIdle = true;

                var btnTextGo = new GameObject("ButtonText");
                btnTextGo.transform.SetParent(btnGo.transform, false);
                var btnTextRect = btnTextGo.AddComponent<RectTransform>();
                btnTextRect.anchorMin = Vector2.zero;
                btnTextRect.anchorMax = Vector2.one;
                btnTextRect.sizeDelta = Vector2.zero;
                card.deployButtonText = btnTextGo.AddComponent<TextMeshProUGUI>();
                card.deployButtonText.text = "DEPLOY";
                card.deployButtonText.fontSize = 22;
                card.deployButtonText.fontStyle = FontStyles.Bold;
                card.deployButtonText.alignment = TextAlignmentOptions.Center;
                card.deployButtonText.color = Color.white;

                // Wave Matrix Row (W1 .. W10)
                var waveRowGo = new GameObject("WaveMatrixContainer");
                waveRowGo.transform.SetParent(cardGo.transform, false);
                var waveRowRect = waveRowGo.AddComponent<RectTransform>();
                waveRowRect.anchorMin = new Vector2(0.025f, 0.08f);
                waveRowRect.anchorMax = new Vector2(0.78f, 0.38f);
                waveRowRect.sizeDelta = Vector2.zero;
                card.waveMatrixContainer = waveRowGo;

                // Wave Row Label
                var waveLblGo = new GameObject("WaveRowLabel");
                waveLblGo.transform.SetParent(waveRowGo.transform, false);
                var waveLblRect = waveLblGo.AddComponent<RectTransform>();
                waveLblRect.anchorMin = new Vector2(0f, 0f);
                waveLblRect.anchorMax = new Vector2(0.10f, 1f);
                waveLblRect.sizeDelta = Vector2.zero;
                var waveLbl = waveLblGo.AddComponent<TextMeshProUGUI>();
                waveLbl.text = "<size=16><b>WAVES:</b></size>";
                waveLbl.alignment = TextAlignmentOptions.MidlineLeft;
                waveLbl.color = new Color(0.6f, 0.75f, 0.9f);

                // 10 Wave Buttons horizontally spaced
                float startX = 0.11f;
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
                    var wBtn = wBtnGo.AddComponent<Button>();
                    SciFiUITheme.ApplyButton(wBtn, wImg, isPrimary: false);

                    var wTxtGo = new GameObject("Text");
                    wTxtGo.transform.SetParent(wBtnGo.transform, false);
                    var wTxtRect = wTxtGo.AddComponent<RectTransform>();
                    wTxtRect.anchorMin = Vector2.zero;
                    wTxtRect.anchorMax = Vector2.one;
                    wTxtRect.sizeDelta = Vector2.zero;
                    var wTxt = wTxtGo.AddComponent<TextMeshProUGUI>();
                    wTxt.fontSize = 18;
                    wTxt.alignment = TextAlignmentOptions.Center;
                    wTxt.text = $"{waveNum}";

                    card.waveButtons[w] = wBtn;
                    card.waveButtonImages[w] = wImg;
                    card.waveButtonTexts[w] = wTxt;
                }

                cards[i] = card;
            }

            // Reset progress button at the bottom
            var resetGo = new GameObject("ResetButton");
            resetGo.transform.SetParent(canvas.transform, false);
            var rRect = resetGo.AddComponent<RectTransform>();
            rRect.anchorMin = new Vector2(0.5f, 0.055f);
            rRect.anchorMax = new Vector2(0.5f, 0.055f);
            rRect.sizeDelta = new Vector2(280, 42);
            var rImg = resetGo.AddComponent<Image>();
            rImg.color = new Color(0.35f, 0.1f, 0.1f, 0.8f);
            resetButton = resetGo.AddComponent<Button>();

            var rTextGo = new GameObject("Text");
            rTextGo.transform.SetParent(resetGo.transform, false);
            var rTextRect = rTextGo.AddComponent<RectTransform>();
            rTextRect.anchorMin = Vector2.zero;
            rTextRect.anchorMax = Vector2.one;
            rTextRect.sizeDelta = Vector2.zero;
            var rText = rTextGo.AddComponent<TextMeshProUGUI>();
            rText.text = "RESET ALL PROGRESS";
            rText.fontSize = 17;
            rText.alignment = TextAlignmentOptions.Center;
            rText.color = new Color(0.9f, 0.55f, 0.55f);

            // Back to Home button at top left
            var homeGo = new GameObject("BackToHomeButton");
            homeGo.transform.SetParent(canvas.transform, false);
            var hRect = homeGo.AddComponent<RectTransform>();
            hRect.anchorMin = new Vector2(0.04f, 0.90f);
            hRect.anchorMax = new Vector2(0.18f, 0.96f);
            hRect.sizeDelta = Vector2.zero;
            var hImg = homeGo.AddComponent<Image>();
            hImg.color = new Color(0.08f, 0.14f, 0.24f, 0.95f);
            var homeBtn = homeGo.AddComponent<Button>();

            var hTextGo = new GameObject("Text");
            hTextGo.transform.SetParent(homeGo.transform, false);
            var htRect = hTextGo.AddComponent<RectTransform>();
            htRect.anchorMin = Vector2.zero;
            htRect.anchorMax = Vector2.one;
            htRect.sizeDelta = Vector2.zero;
            var hTmp = hTextGo.AddComponent<TextMeshProUGUI>();
            hTmp.text = "<b>< COMMAND HUB</b>";
            hTmp.fontSize = 18;
            hTmp.color = new Color(0f, 0.9f, 1f);
            hTmp.alignment = TextAlignmentOptions.Center;

            homeBtn.onClick.AddListener(() =>
            {
                LevelProgressManager.LoadHomeScene();
            });
        }
    }
}
