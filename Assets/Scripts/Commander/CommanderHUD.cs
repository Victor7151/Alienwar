using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using Starfall.Core;
using Starfall.UI.Theme;

namespace Starfall.Commander
{
    /// <summary>
    /// Tactical HUD for Fleet Commander battle mode.
    /// Features:
    /// 1. Dual Base Core Health Bars (Opponent at top, Player at bottom).
    /// 2. Match Timer & Overtime announcement banner.
    /// 3. High-tech Command Energy Gauge (0 to 10 with animated fill).
    /// 4. 4 Interactive Fleet Deployment Cards with hotkey badges [1, 2, 3, 4] and energy cost indicators.
    /// 5. Victory / Defeat Modal with Rematch and Return to Hangar.
    /// </summary>
    public class CommanderHUD : MonoBehaviour
    {
        private Canvas hudCanvas;

        // Top Elements (Opponent)
        private Image opponentHpFill;
        private TextMeshProUGUI opponentHpText;
        private TextMeshProUGUI matchTimerText;
        private GameObject overtimeBanner;

        // Bottom Elements (Player & Deck)
        private Image playerHpFill;
        private TextMeshProUGUI playerHpText;
        private Image energyFill;
        private TextMeshProUGUI energyText;

        // 4 Deployment Cards
        private struct CardView
        {
            public Button button;
            public Image background;
            public Image glowBorder;
            public TextMeshProUGUI nameText;
            public TextMeshProUGUI costText;
            public TextMeshProUGUI keyBadgeText;
            public Image energyDimmer;
        }
        private CardView[] cardViews = new CardView[4];

        // End Modal
        private GameObject endModal;
        private TextMeshProUGUI endTitleText;
        private TextMeshProUGUI endSubText;

        private void Awake()
        {
            EnsureCanvas();
            BuildHUDLayout();
        }

        private void Start()
        {
            if (CommanderBattleManager.Instance != null)
            {
                CommanderBattleManager.Instance.OnPlayerEnergyChanged += HandleEnergyChanged;
                CommanderBattleManager.Instance.OnPlayerCoreHPChanged += HandlePlayerHpChanged;
                CommanderBattleManager.Instance.OnOpponentCoreHPChanged += HandleOpponentHpChanged;
                CommanderBattleManager.Instance.OnTimerTick += HandleTimerTick;
                CommanderBattleManager.Instance.OnOvertimeStarted += HandleOvertimeStarted;
                CommanderBattleManager.Instance.OnMatchEnded += HandleMatchEnded;

                // Initial populate
                RefreshCardsData();
                HandlePlayerHpChanged(CommanderBattleManager.Instance.PlayerCoreHP, CommanderBattleManager.Instance.MaxCoreHealth);
                HandleOpponentHpChanged(CommanderBattleManager.Instance.OpponentCoreHP, CommanderBattleManager.Instance.MaxCoreHealth);
                HandleEnergyChanged(CommanderBattleManager.Instance.PlayerEnergy, CommanderBattleManager.Instance.MaxEnergy);
            }
        }

        private void EnsureCanvas()
        {
            hudCanvas = GetComponentInParent<Canvas>();
            if (hudCanvas == null)
            {
                var canvasGo = new GameObject("CommanderHUD_Canvas");
                hudCanvas = canvasGo.AddComponent<Canvas>();
                hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
                hudCanvas.sortingOrder = 50;

                var scaler = canvasGo.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                scaler.matchWidthOrHeight = 0.5f;

                canvasGo.AddComponent<GraphicRaycaster>();
                transform.SetParent(canvasGo.transform, false);
            }
        }

        private void BuildHUDLayout()
        {
            // Root rect
            var rect = gameObject.AddComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;

            BuildTopOpponentBar();
            BuildBottomPlayerBar();
            BuildCardDeck();
            BuildEndModal();
        }

        private void BuildTopOpponentBar()
        {
            // Top Bar Container with Sci-Fi Glass styling
            var topBar = CreateUIObject("TopBar", transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -40f), new Vector2(900f, 65f));
            var topBarBg = topBar.AddComponent<Image>();
            SciFiUITheme.ApplyGlass(topBarBg, new Color(0.06f, 0.02f, 0.03f, 0.88f));

            // Opponent Name with tactical styling
            var nameObj = CreateText(topBar.transform, "COMMANDER MALAKOR // TITAN WARSHIP", 14, FontStyles.Bold, new Color(1f, 0.45f, 0.35f), TextAlignmentOptions.Left);
            nameObj.rectTransform.anchoredPosition = new Vector2(-220f, 16f);

            // Opponent HP Bar Track
            var track = CreateImage(topBar.transform, Color.white, new Vector2(440f, 18f));
            track.sprite = SciFiUITheme.GaugeTrack;
            track.type = Image.Type.Sliced;
            track.rectTransform.anchoredPosition = new Vector2(-220f, -10f);

            // Opponent HP Bar Fill
            var fillObj = CreateUIObject("Fill", track.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            opponentHpFill = fillObj.AddComponent<Image>();
            opponentHpFill.sprite = SciFiUITheme.GaugeFill;
            opponentHpFill.type = Image.Type.Filled;
            opponentHpFill.fillMethod = Image.FillMethod.Horizontal;
            opponentHpFill.fillAmount = 1.0f;
            opponentHpFill.color = new Color(0.95f, 0.25f, 0.2f, 1f);

            var frt = opponentHpFill.rectTransform;
            frt.offsetMin = new Vector2(3f, 2f);
            frt.offsetMax = new Vector2(-3f, -2f);

            // Opponent HP Text
            opponentHpText = CreateText(track.transform, "100 / 100 CORE HP", 11, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);

            // Center Match Clock
            var clockBg = CreateImage(topBar.transform, Color.white, new Vector2(140f, 50f));
            SciFiUITheme.ApplyPanel(clockBg, new Color(0.04f, 0.08f, 0.16f, 0.95f));
            clockBg.rectTransform.anchoredPosition = new Vector2(0f, 0f);

            matchTimerText = CreateText(clockBg.transform, "03:00", 22, FontStyles.Bold, new Color(1f, 0.9f, 0.2f), TextAlignmentOptions.Center);

            // Overtime Banner with high-tech Hazard Tape
            overtimeBanner = CreateUIObject("OvertimeBanner", transform, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -90f), new Vector2(520f, 34f));
            var otBg = overtimeBanner.AddComponent<Image>();
            otBg.sprite = SciFiUITheme.HazardTapeYellow;
            otBg.type = Image.Type.Tiled;
            otBg.color = new Color(1f, 1f, 1f, 0.95f);

            var otInner = CreateImage(overtimeBanner.transform, new Color(0f, 0f, 0f, 0.65f), new Vector2(490f, 26f));
            var otTxt = CreateText(otInner.transform, ">>> DOUBLE ENERGY OVERTIME! <<<", 14, FontStyles.Bold, new Color(1f, 0.92f, 0.2f), TextAlignmentOptions.Center);
            overtimeBanner.SetActive(false);
        }

        private void BuildBottomPlayerBar()
        {
            // Bottom Bar Container with Glass styling
            var bottomBar = CreateUIObject("BottomBar", transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(1000f, 140f));
            var bottomBarBg = bottomBar.AddComponent<Image>();
            SciFiUITheme.ApplyGlass(bottomBarBg, new Color(0.02f, 0.05f, 0.10f, 0.88f));

            // Player HP Bar Track
            var track = CreateImage(bottomBar.transform, Color.white, new Vector2(320f, 18f));
            track.sprite = SciFiUITheme.GaugeTrack;
            track.type = Image.Type.Sliced;
            track.rectTransform.anchoredPosition = new Vector2(-330f, 45f);

            var fillObj = CreateUIObject("Fill", track.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            playerHpFill = fillObj.AddComponent<Image>();
            playerHpFill.sprite = SciFiUITheme.GaugeFill;
            playerHpFill.type = Image.Type.Filled;
            playerHpFill.fillMethod = Image.FillMethod.Horizontal;
            playerHpFill.fillAmount = 1.0f;
            playerHpFill.color = new Color(0.15f, 0.85f, 1f, 1f);

            var frt = playerHpFill.rectTransform;
            frt.offsetMin = new Vector2(3f, 2f);
            frt.offsetMax = new Vector2(-3f, -2f);

            playerHpText = CreateText(track.transform, "100 / 100 CORE HP", 11, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);

            // Command Energy Gauge Track
            var energyTrack = CreateImage(bottomBar.transform, Color.white, new Vector2(320f, 24f));
            energyTrack.sprite = SciFiUITheme.GaugeTrack;
            energyTrack.type = Image.Type.Sliced;
            energyTrack.rectTransform.anchoredPosition = new Vector2(-330f, 15f);

            var eFillObj = CreateUIObject("Fill", energyTrack.transform, new Vector2(0f, 0f), new Vector2(1f, 1f), Vector2.zero, Vector2.zero);
            energyFill = eFillObj.AddComponent<Image>();
            energyFill.sprite = SciFiUITheme.GaugeFill;
            energyFill.type = Image.Type.Filled;
            energyFill.fillMethod = Image.FillMethod.Horizontal;
            energyFill.fillAmount = 0.4f;
            energyFill.color = new Color(0.00f, 0.95f, 0.55f, 1f); // Neon emerald plasma

            var efrt = energyFill.rectTransform;
            efrt.offsetMin = new Vector2(3f, 2f);
            efrt.offsetMax = new Vector2(-3f, -2f);

            energyText = CreateText(energyTrack.transform, "ENERGY: 4.0 / 10", 12, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
        }

        private void BuildCardDeck()
        {
            var deckRoot = CreateUIObject("DeckRoot", transform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(120f, 75f), new Vector2(620f, 110f));

            float cardWidth = 140f;
            float spacing = 12f;
            float startX = -((4 * cardWidth + 3 * spacing) * 0.5f) + cardWidth * 0.5f;

            for (int i = 0; i < 4; i++)
            {
                int cardIndex = i;
                float posX = startX + i * (cardWidth + spacing);

                var cardGo = CreateUIObject($"Card_{i}", deckRoot.transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(posX, 0f), new Vector2(cardWidth, 95f));

                var btn = cardGo.AddComponent<Button>();
                var bg = cardGo.AddComponent<Image>();
                SciFiUITheme.ApplyPanel(bg, new Color(0.04f, 0.08f, 0.16f, 0.95f));

                // Add tactile button animator
                var animator = cardGo.AddComponent<SciFiButtonAnimator>();

                // Glow Border
                var glowObj = CreateImage(cardGo.transform, new Color(0.00f, 0.90f, 1.00f, 0.45f), new Vector2(cardWidth, 95f));
                glowObj.sprite = SciFiUITheme.PanelFrame;
                glowObj.type = Image.Type.Sliced;
                glowObj.rectTransform.anchoredPosition = Vector2.zero;

                // Key Badge [1, 2, 3, 4] with tactical bracket styling
                var badge = CreateText(cardGo.transform, $"[{i + 1}]", 11, FontStyles.Bold, new Color(1f, 0.82f, 0.15f), TextAlignmentOptions.TopLeft);
                badge.rectTransform.anchoredPosition = new Vector2(-48f, 32f);

                // Name Text
                var nameTxt = CreateText(cardGo.transform, "UNIT", 12, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
                nameTxt.rectTransform.anchoredPosition = new Vector2(0f, 12f);

                // Energy Cost Badge with sci-fi gem styling
                var costBadge = CreateImage(cardGo.transform, Color.white, new Vector2(58f, 22f));
                costBadge.sprite = SciFiUITheme.ButtonNormal;
                costBadge.type = Image.Type.Sliced;
                costBadge.rectTransform.anchoredPosition = new Vector2(0f, -24f);

                var costTxt = CreateText(costBadge.transform, "3 EN", 12, FontStyles.Bold, new Color(0.00f, 0.95f, 1.00f), TextAlignmentOptions.Center);

                // Energy Dimmer Overlay
                var dimmerObj = CreateImage(cardGo.transform, new Color(0.02f, 0.04f, 0.08f, 0.85f), new Vector2(cardWidth, 95f));
                dimmerObj.sprite = SciFiUITheme.PanelFrame;
                dimmerObj.type = Image.Type.Sliced;
                dimmerObj.rectTransform.anchoredPosition = Vector2.zero;
                dimmerObj.raycastTarget = false;
                dimmerObj.gameObject.SetActive(false);

                btn.onClick.AddListener(() =>
                {
                    CommanderBattleManager.Instance?.TryDeployPlayerCard(cardIndex);
                });

                cardViews[i] = new CardView
                {
                    button = btn,
                    background = bg,
                    glowBorder = glowObj,
                    nameText = nameTxt,
                    costText = costTxt,
                    keyBadgeText = badge,
                    energyDimmer = dimmerObj
                };
            }
        }

        private void RefreshCardsData()
        {
            if (CommanderBattleManager.Instance == null || CommanderBattleManager.Instance.PlayerDeck == null) return;
            var deck = CommanderBattleManager.Instance.PlayerDeck;

            for (int i = 0; i < 4 && i < deck.Count; i++)
            {
                var card = deck[i];
                cardViews[i].nameText.text = card.displayName;
                cardViews[i].costText.text = $"{card.energyCost} EN";
            }
        }

        private void HandleEnergyChanged(float currentEnergy, float maxEnergy)
        {
            if (energyFill != null)
            {
                energyFill.fillAmount = Mathf.Clamp01(currentEnergy / maxEnergy);
            }
            if (energyText != null)
            {
                energyText.text = $"ENERGY: {currentEnergy:0.0} / {maxEnergy:0}";
            }

            // Update Deck card readiness
            if (CommanderBattleManager.Instance != null && CommanderBattleManager.Instance.PlayerDeck != null)
            {
                var deck = CommanderBattleManager.Instance.PlayerDeck;
                for (int i = 0; i < 4 && i < deck.Count; i++)
                {
                    bool canAfford = currentEnergy >= deck[i].energyCost;
                    cardViews[i].energyDimmer.gameObject.SetActive(!canAfford);
                    cardViews[i].glowBorder.color = canAfford 
                        ? new Color(0.2f, 1f, 0.85f, 0.7f) 
                        : new Color(0.4f, 0.4f, 0.4f, 0.2f);
                }
            }
        }

        private void HandlePlayerHpChanged(int current, int max)
        {
            if (playerHpFill != null) playerHpFill.fillAmount = (float)current / max;
            if (playerHpText != null) playerHpText.text = $"{current} / {max} CORE HP";
        }

        private void HandleOpponentHpChanged(int current, int max)
        {
            if (opponentHpFill != null) opponentHpFill.fillAmount = (float)current / max;
            if (opponentHpText != null) opponentHpText.text = $"{current} / {max} CORE HP";
        }

        private void HandleTimerTick(float timeRemaining)
        {
            if (matchTimerText != null)
            {
                int minutes = Mathf.FloorToInt(timeRemaining / 60f);
                int seconds = Mathf.FloorToInt(timeRemaining % 60f);
                matchTimerText.text = $"{minutes:00}:{seconds:00}";

                if (timeRemaining <= 60f)
                {
                    matchTimerText.color = new Color(1f, 0.3f, 0.2f);
                }
            }
        }

        private void HandleOvertimeStarted()
        {
            if (overtimeBanner != null) overtimeBanner.SetActive(true);
        }

        private void BuildEndModal()
        {
            endModal = CreateUIObject("EndModal", transform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(580f, 330f));
            var modalBg = endModal.AddComponent<Image>();
            SciFiUITheme.ApplyPanel(modalBg, new Color(0.03f, 0.06f, 0.12f, 0.98f));

            endTitleText = CreateText(endModal.transform, "VICTORY ACHIEVED!", 30, FontStyles.Bold, new Color(1f, 0.85f, 0.2f), TextAlignmentOptions.Center);
            endTitleText.rectTransform.anchoredPosition = new Vector2(0f, 85f);

            endSubText = CreateText(endModal.transform, "Opponent Defense Core Destroyed!\n+500 Cosmic Credits Awarded.", 15, FontStyles.Normal, new Color(0.85f, 0.92f, 1f), TextAlignmentOptions.Center);
            endSubText.rectTransform.anchoredPosition = new Vector2(0f, 20f);

            // Rematch Button
            var rematchBtn = CreateButton(endModal.transform, "REMATCH", Color.white, new Vector2(190f, 48f), new Vector2(-115f, -75f));
            var rImg = rematchBtn.GetComponent<Image>();
            SciFiUITheme.ApplyButton(rematchBtn, rImg, isPrimary: true);
            rematchBtn.onClick.AddListener(() =>
            {
                SceneManager.LoadScene(SceneManager.GetActiveScene().name);
            });

            // Return to Hangar Button
            var hangarBtn = CreateButton(endModal.transform, "RETURN TO HUB", Color.white, new Vector2(190f, 48f), new Vector2(115f, -75f));
            var hImg = hangarBtn.GetComponent<Image>();
            SciFiUITheme.ApplyButton(hangarBtn, hImg, isPrimary: false);
            hangarBtn.onClick.AddListener(() =>
            {
                SceneManager.LoadScene("HomeScene");
            });

            endModal.SetActive(false);
        }

        private void HandleMatchEnded(bool playerWon)
        {
            if (endModal == null) return;
            endModal.SetActive(true);

            if (playerWon)
            {
                endTitleText.text = "VICTORY ACHIEVED!";
                endTitleText.color = new Color(1f, 0.85f, 0.2f);
                endSubText.text = "Enemy Defense Core Decimated!\n+500 Cosmic Credits Awarded.";
            }
            else
            {
                endTitleText.text = "DEFEAT";
                endTitleText.color = new Color(1f, 0.3f, 0.25f);
                endSubText.text = "Your Defense Core Was Destroyed.\nStudy the opponent's counter strategy and retry!";
            }
        }

        #region UI Helpers

        private static GameObject CreateUIObject(string name, Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            return go;
        }

        private static Image CreateImage(Transform parent, Color color, Vector2 size)
        {
            var go = CreateUIObject("Image", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, size);
            var img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        private static TextMeshProUGUI CreateText(Transform parent, string content, float fontSize, FontStyles style, Color color, TextAlignmentOptions align)
        {
            var go = CreateUIObject("Text", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(400f, 60f));
            var tmp = go.AddComponent<TextMeshProUGUI>();
            tmp.text = content;
            tmp.fontSize = fontSize;
            tmp.fontStyle = style;
            tmp.color = color;
            tmp.alignment = align;
            return tmp;
        }

        private static Button CreateButton(Transform parent, string label, Color bgColor, Vector2 size, Vector2 pos)
        {
            var go = CreateUIObject($"Btn_{label}", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), pos, size);
            var img = go.AddComponent<Image>();
            img.color = bgColor;
            var btn = go.AddComponent<Button>();

            var txt = CreateText(go.transform, label, 14, FontStyles.Bold, Color.white, TextAlignmentOptions.Center);
            txt.rectTransform.anchoredPosition = Vector2.zero;
            txt.rectTransform.sizeDelta = size;

            return btn;
        }

        #endregion

        private void OnDestroy()
        {
            if (CommanderBattleManager.Instance != null)
            {
                CommanderBattleManager.Instance.OnPlayerEnergyChanged -= HandleEnergyChanged;
                CommanderBattleManager.Instance.OnPlayerCoreHPChanged -= HandlePlayerHpChanged;
                CommanderBattleManager.Instance.OnOpponentCoreHPChanged -= HandleOpponentHpChanged;
                CommanderBattleManager.Instance.OnTimerTick -= HandleTimerTick;
                CommanderBattleManager.Instance.OnOvertimeStarted -= HandleOvertimeStarted;
                CommanderBattleManager.Instance.OnMatchEnded -= HandleMatchEnded;
            }
        }
    }
}
