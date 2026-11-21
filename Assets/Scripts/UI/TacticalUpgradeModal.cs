using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Starfall.Combat;
using Starfall.UI.Theme;

namespace Starfall.UI
{
    /// <summary>
    /// Roguelike Tactical Upgrade selection modal presented after checkpoint waves.
    /// Displays 3 randomized upgrade cards and pauses/slows wave execution until the player chooses.
    /// </summary>
    public class TacticalUpgradeModal : MonoBehaviour
    {
        public static TacticalUpgradeModal Instance { get; private set; }

        private GameObject modalRoot;
        private RectTransform cardsContainer;
        private TextMeshProUGUI headerText;
        private TextMeshProUGUI subheaderText;
        private bool isChoicePending;
        private Action onSelectionFinished;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            BuildModalUI();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static TacticalUpgradeModal EnsureInstance(Transform parent)
        {
            if (Instance != null) return Instance;
            var existing = FindAnyObjectByType<TacticalUpgradeModal>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            var go = new GameObject("TacticalUpgradeModal");
            if (parent != null) go.transform.SetParent(parent, false);

            if (UnityEngine.EventSystems.EventSystem.current == null && FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                var esGo = new GameObject("EventSystem");
                esGo.AddComponent<UnityEngine.EventSystems.EventSystem>();
                var uiModule = esGo.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
                uiModule.AssignDefaultActions();
            }

            return go.AddComponent<TacticalUpgradeModal>();
        }

        private void BuildModalUI()
        {
            if (modalRoot != null) return;

            // Root Canvas / Overlay
            modalRoot = new GameObject("UpgradeModalRoot");
            modalRoot.transform.SetParent(transform, false);
            var rootRect = modalRoot.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.sizeDelta = Vector2.zero;

            var modalCanvas = modalRoot.AddComponent<Canvas>();
            modalCanvas.overrideSorting = true;
            modalCanvas.sortingOrder = 80;
            modalRoot.AddComponent<GraphicRaycaster>();

            var bg = modalRoot.AddComponent<Image>();
            bg.color = new Color(0.02f, 0.04f, 0.08f, 0.92f);
            bg.raycastTarget = true;

            // Modal Box
            var boxGo = new GameObject("ModalBox");
            boxGo.transform.SetParent(modalRoot.transform, false);
            var boxRect = boxGo.AddComponent<RectTransform>();
            boxRect.anchorMin = new Vector2(0.5f, 0.5f);
            boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.pivot = new Vector2(0.5f, 0.5f);
            boxRect.sizeDelta = new Vector2(920f, 520f);

            var boxBg = boxGo.AddComponent<Image>();
            SciFiUITheme.ApplyPanel(boxBg);

            // Title
            var titleGo = new GameObject("Title");
            titleGo.transform.SetParent(boxGo.transform, false);
            var titleRect = titleGo.AddComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0f, 1f);
            titleRect.anchorMax = new Vector2(1f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -18f);
            titleRect.sizeDelta = new Vector2(0f, 36f);

            headerText = titleGo.AddComponent<TextMeshProUGUI>();
            headerText.text = "TACTICAL FIELD UPGRADE";
            headerText.fontSize = 24;
            headerText.fontStyle = FontStyles.Bold;
            headerText.alignment = TextAlignmentOptions.Center;
            headerText.color = new Color(1f, 0.85f, 0.2f);

            // Subtitle
            var subGo = new GameObject("SubTitle");
            subGo.transform.SetParent(boxGo.transform, false);
            var subRect = subGo.AddComponent<RectTransform>();
            subRect.anchorMin = new Vector2(0f, 1f);
            subRect.anchorMax = new Vector2(1f, 1f);
            subRect.pivot = new Vector2(0.5f, 1f);
            subRect.anchoredPosition = new Vector2(0f, -52f);
            subRect.sizeDelta = new Vector2(0f, 22f);

            subheaderText = subGo.AddComponent<TextMeshProUGUI>();
            subheaderText.text = "SECTOR CHECKPOINT REACHED - SELECT 1 SYSTEM REINFORCEMENT";
            subheaderText.fontSize = 12;
            subheaderText.fontStyle = FontStyles.Bold;
            subheaderText.alignment = TextAlignmentOptions.Center;
            subheaderText.color = new Color(0.0f, 0.9f, 1f, 0.9f);

            // Cards container
            var containerGo = new GameObject("CardsContainer");
            containerGo.transform.SetParent(boxGo.transform, false);
            cardsContainer = containerGo.AddComponent<RectTransform>();
            cardsContainer.anchorMin = new Vector2(0f, 0f);
            cardsContainer.anchorMax = new Vector2(1f, 1f);
            cardsContainer.offsetMin = new Vector2(28f, 24f);
            cardsContainer.offsetMax = new Vector2(-28f, -86f);

            var layout = containerGo.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 18f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            modalRoot.SetActive(false);
        }

        public IEnumerator ShowUpgradeSelectionRoutine(Action onComplete)
        {
            if (TacticalPerkManager.Instance == null)
            {
                var mgrGo = new GameObject("TacticalPerkManager");
                mgrGo.AddComponent<TacticalPerkManager>();
            }

            var choices = TacticalPerkManager.Instance.RollUpgradeChoices(3);
            if (choices == null || choices.Count == 0)
            {
                onComplete?.Invoke();
                yield break;
            }

            BuildModalUI();
            PopulateCards(choices);

            isChoicePending = true;
            onSelectionFinished = onComplete;
            modalRoot.SetActive(true);

            Starfall.Audio.AudioManager.Instance?.PlayPowerUp();
            Time.timeScale = 0f; // Pause combat while selecting

            while (isChoicePending)
            {
                yield return null;
            }

            Time.timeScale = 1f;
            modalRoot.SetActive(false);
            onSelectionFinished?.Invoke();
            onSelectionFinished = null;
        }

        private void PopulateCards(List<TacticalPerkDef> choices)
        {
            // Clear previous cards
            for (int i = cardsContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(cardsContainer.GetChild(i).gameObject);
            }

            for (int i = 0; i < choices.Count; i++)
            {
                var perk = choices[i];
                CreatePerkCard(perk);
            }
        }

        private void CreatePerkCard(TacticalPerkDef perk)
        {
            var cardGo = new GameObject("Card_" + perk.type, typeof(RectTransform), typeof(Image), typeof(Button));
            cardGo.transform.SetParent(cardsContainer, false);

            var cardImg = cardGo.GetComponent<Image>();
            SciFiUITheme.ApplyGlass(cardImg);
            cardImg.color = new Color(0.06f, 0.09f, 0.16f, 0.96f);

            var button = cardGo.GetComponent<Button>();
            button.onClick.AddListener(() => OnCardSelected(perk));
            if (cardGo.GetComponent<SciFiButtonAnimator>() == null)
            {
                cardGo.AddComponent<SciFiButtonAnimator>();
            }

            // Top Badge Pill
            var badgeGo = new GameObject("Badge", typeof(RectTransform), typeof(Image));
            badgeGo.transform.SetParent(cardGo.transform, false);
            var badgeRect = badgeGo.GetComponent<RectTransform>();
            badgeRect.anchorMin = new Vector2(0.12f, 1f);
            badgeRect.anchorMax = new Vector2(0.88f, 1f);
            badgeRect.pivot = new Vector2(0.5f, 1f);
            badgeRect.anchoredPosition = new Vector2(0f, -14f);
            badgeRect.sizeDelta = new Vector2(0f, 24f);

            var badgeImg = badgeGo.GetComponent<Image>();
            badgeImg.sprite = SciFiUITheme.ButtonNormal;
            badgeImg.type = Image.Type.Sliced;
            badgeImg.color = new Color(perk.badgeColor.r * 0.35f, perk.badgeColor.g * 0.35f, perk.badgeColor.b * 0.35f, 0.85f);
            badgeImg.raycastTarget = false;

            var badgeTxtGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            badgeTxtGo.transform.SetParent(badgeGo.transform, false);
            var btRect = badgeTxtGo.GetComponent<RectTransform>();
            btRect.anchorMin = Vector2.zero;
            btRect.anchorMax = Vector2.one;
            btRect.sizeDelta = Vector2.zero;
            var badgeTxt = badgeTxtGo.GetComponent<TextMeshProUGUI>();
            badgeTxt.text = $"[ {perk.badge.ToUpper()} ]";
            badgeTxt.fontSize = 11;
            badgeTxt.fontStyle = FontStyles.Bold;
            badgeTxt.alignment = TextAlignmentOptions.Center;
            badgeTxt.color = perk.badgeColor;
            badgeTxt.raycastTarget = false;

            // Title Text
            var titleGo = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleGo.transform.SetParent(cardGo.transform, false);
            var titleRect = titleGo.GetComponent<RectTransform>();
            titleRect.anchorMin = new Vector2(0.06f, 1f);
            titleRect.anchorMax = new Vector2(0.94f, 1f);
            titleRect.pivot = new Vector2(0.5f, 1f);
            titleRect.anchoredPosition = new Vector2(0f, -44f);
            titleRect.sizeDelta = new Vector2(0f, 44f);

            var titleTxt = titleGo.GetComponent<TextMeshProUGUI>();
            titleTxt.text = perk.title;
            titleTxt.fontSize = 16;
            titleTxt.fontStyle = FontStyles.Bold;
            titleTxt.alignment = TextAlignmentOptions.Center;
            titleTxt.color = Color.white;
            titleTxt.textWrappingMode = TextWrappingModes.Normal;
            titleTxt.overflowMode = TextOverflowModes.Ellipsis;
            titleTxt.raycastTarget = false;

            // Thin 2px Neon Accent Divider
            var divGo = new GameObject("Divider", typeof(RectTransform), typeof(Image));
            divGo.transform.SetParent(cardGo.transform, false);
            var divRect = divGo.GetComponent<RectTransform>();
            divRect.anchorMin = new Vector2(0.12f, 1f);
            divRect.anchorMax = new Vector2(0.88f, 1f);
            divRect.pivot = new Vector2(0.5f, 1f);
            divRect.anchoredPosition = new Vector2(0f, -94f);
            divRect.sizeDelta = new Vector2(0f, 2f);

            var divImg = divGo.GetComponent<Image>();
            divImg.color = new Color(perk.badgeColor.r, perk.badgeColor.g, perk.badgeColor.b, 0.75f);
            divImg.raycastTarget = false;

            // Tactical Hologram Socket (Center visual element with real rendered icon)
            var socketGo = new GameObject("Socket", typeof(RectTransform), typeof(Image));
            socketGo.transform.SetParent(cardGo.transform, false);
            var sockRect = socketGo.GetComponent<RectTransform>();
            sockRect.anchorMin = new Vector2(0.5f, 1f);
            sockRect.anchorMax = new Vector2(0.5f, 1f);
            sockRect.pivot = new Vector2(0.5f, 1f);
            sockRect.anchoredPosition = new Vector2(0f, -104f);
            sockRect.sizeDelta = new Vector2(80f, 80f);

            var sockImg = socketGo.GetComponent<Image>();
            SciFiUITheme.ApplyGlass(sockImg);
            sockImg.color = new Color(perk.badgeColor.r * 0.20f, perk.badgeColor.g * 0.20f, perk.badgeColor.b * 0.20f, 0.90f);
            sockImg.raycastTarget = false;

            var perkSprite = GetPerkSprite(perk.type);
            if (perkSprite != null)
            {
                var iconGo = new GameObject("PerkIcon", typeof(RectTransform), typeof(Image));
                iconGo.transform.SetParent(socketGo.transform, false);
                var iRect = iconGo.GetComponent<RectTransform>();
                iRect.anchorMin = new Vector2(0.12f, 0.12f);
                iRect.anchorMax = new Vector2(0.88f, 0.88f);
                iRect.offsetMin = Vector2.zero;
                iRect.offsetMax = Vector2.zero;

                var iconImg = iconGo.GetComponent<Image>();
                iconImg.sprite = perkSprite;
                iconImg.preserveAspect = true;
                iconImg.color = Color.white;
                iconImg.raycastTarget = false;
            }
            else
            {
                var sockTxtGo = new GameObject("IconText", typeof(RectTransform), typeof(TextMeshProUGUI));
                sockTxtGo.transform.SetParent(socketGo.transform, false);
                var stRect = sockTxtGo.GetComponent<RectTransform>();
                stRect.anchorMin = Vector2.zero;
                stRect.anchorMax = Vector2.one;
                stRect.sizeDelta = Vector2.zero;
                var sockTxt = sockTxtGo.GetComponent<TextMeshProUGUI>();
                string hexCol = ColorUtility.ToHtmlStringRGB(perk.badgeColor);
                sockTxt.text = $"<size=18><color=#{hexCol}>SYS</color></size>\n<size=9><color=#A0C0E0>MODULE</color></size>";
                sockTxt.alignment = TextAlignmentOptions.Center;
                sockTxt.raycastTarget = false;
            }

            // Description Text Box (constrained with word wrapping!)
            var descGo = new GameObject("Desc", typeof(RectTransform), typeof(TextMeshProUGUI));
            descGo.transform.SetParent(cardGo.transform, false);
            var descRect = descGo.GetComponent<RectTransform>();
            descRect.anchorMin = new Vector2(0.08f, 0.17f);
            descRect.anchorMax = new Vector2(0.92f, 0.49f);
            descRect.offsetMin = Vector2.zero;
            descRect.offsetMax = Vector2.zero;

            var descTxt = descGo.GetComponent<TextMeshProUGUI>();
            descTxt.text = perk.description;
            descTxt.fontSize = 12;
            descTxt.textWrappingMode = TextWrappingModes.Normal;
            descTxt.overflowMode = TextOverflowModes.Truncate;
            descTxt.alignment = TextAlignmentOptions.Center;
            descTxt.lineSpacing = 2;
            descTxt.color = new Color(0.85f, 0.92f, 1f, 0.92f);
            descTxt.raycastTarget = false;

            // Select Prompt Button Footer ("INSTALL UPGRADE")
            var selectGo = new GameObject("SelectButton", typeof(RectTransform), typeof(Image), typeof(Button));
            selectGo.transform.SetParent(cardGo.transform, false);
            var selectRect = selectGo.GetComponent<RectTransform>();
            selectRect.anchorMin = new Vector2(0.08f, 0.04f);
            selectRect.anchorMax = new Vector2(0.92f, 0.15f);
            selectRect.offsetMin = Vector2.zero;
            selectRect.offsetMax = Vector2.zero;

            var selectImg = selectGo.GetComponent<Image>();
            var selectBtn = selectGo.GetComponent<Button>();
            SciFiUITheme.ApplyButton(selectBtn, selectImg, isPrimary: true);
            selectBtn.onClick.AddListener(() => OnCardSelected(perk));

            var selectTxtGo = new GameObject("SelectText", typeof(RectTransform), typeof(TextMeshProUGUI));
            selectTxtGo.transform.SetParent(selectGo.transform, false);
            var selectTxtRect = selectTxtGo.GetComponent<RectTransform>();
            selectTxtRect.anchorMin = Vector2.zero;
            selectTxtRect.anchorMax = Vector2.one;
            selectTxtRect.sizeDelta = Vector2.zero;

            var selectTxt = selectTxtGo.GetComponent<TextMeshProUGUI>();
            selectTxt.text = "<b>INSTALL UPGRADE</b>";
            selectTxt.fontSize = 12;
            selectTxt.fontStyle = FontStyles.Bold;
            selectTxt.alignment = TextAlignmentOptions.Center;
            selectTxt.color = Color.white;
            selectTxt.raycastTarget = false;
        }

        private void OnCardSelected(TacticalPerkDef perk)
        {
            if (!isChoicePending) return;
            isChoicePending = false;
            TacticalPerkManager.Instance?.ApplyPerk(perk.type);
        }

        private static Sprite GetPerkSprite(TacticalPerkType type)
        {
            string prefabKey = type switch
            {
                TacticalPerkType.PlasmaOverdrive => "PowerUps/PowerUp_Weapon",
                TacticalPerkType.WingmanEscort => "PowerUps/PowerUp_Wingman",
                TacticalPerkType.ShieldCapacitor => "PowerUps/PowerUp_Shield",
                TacticalPerkType.HomingSwarm => "PowerUps/PowerUp_HomingMissiles",
                TacticalPerkType.MagneticScavenger => "PowerUps/PowerUp_Magnet",
                TacticalPerkType.StarfallOvercharge => "PowerUps/PowerUp_Bomb",
                TacticalPerkType.HyperThrusters => "PowerUps/PowerUp_TimeWarp",
                _ => null
            };

            if (!string.IsNullOrEmpty(prefabKey))
            {
                var go = Resources.Load<GameObject>(prefabKey);
                if (go != null)
                {
                    var sr = go.GetComponent<SpriteRenderer>();
                    if (sr != null && sr.sprite != null) return sr.sprite;
                }
            }

            if (type == TacticalPerkType.NaniteArmor)
            {
                var viperSprite = Resources.Load<Sprite>("Warships/ship_viper");
                if (viperSprite != null) return viperSprite;
            }

#if UNITY_EDITOR
            string directPath = type switch
            {
                TacticalPerkType.PlasmaOverdrive => "Assets/ALienware 2D assets/Power Ups/Weapon upgrade.png",
                TacticalPerkType.WingmanEscort => "Assets/ALienware 2D assets/Power Ups/Wingman Drone.png",
                TacticalPerkType.ShieldCapacitor => "Assets/ALienware 2D assets/Power Ups/Shield.png",
                TacticalPerkType.HomingSwarm => "Assets/ALienware 2D assets/Power Ups/Homing Missiles.png",
                TacticalPerkType.MagneticScavenger => "Assets/ALienware 2D assets/Power Ups/Magnet.png",
                TacticalPerkType.StarfallOvercharge => "Assets/ALienware 2D assets/Power Ups/Bomb.png",
                TacticalPerkType.HyperThrusters => "Assets/ALienware 2D assets/Power Ups/Time Warp.png",
                TacticalPerkType.NaniteArmor => "Assets/ALienware 2D assets/Player warships/Viper Mk-I.png",
                _ => null
            };
            if (!string.IsNullOrEmpty(directPath))
            {
                var sp = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(directPath);
                if (sp != null) return sp;
            }
#endif
            return null;
        }
    }
}
