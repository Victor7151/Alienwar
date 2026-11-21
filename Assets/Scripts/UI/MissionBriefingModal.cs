using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Starfall.Levels;
using Starfall.Audio;

namespace Starfall.UI
{
    /// <summary>
    /// Pre-mission Tactical Intel Briefing modal presented before Level Wave 1 begins.
    /// Displays sector intel, primary objectives, threat recon, and launch controls.
    /// </summary>
    public class MissionBriefingModal : MonoBehaviour
    {
        public static MissionBriefingModal Instance { get; private set; }

        private GameObject modalRoot;
        private Action onCommenceAction;
        private Action onTutorialAction;
        private bool isWaitingForPlayer = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public static MissionBriefingModal EnsureInstance(Transform parent)
        {
            if (Instance != null) return Instance;
            var existing = FindAnyObjectByType<MissionBriefingModal>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            var go = new GameObject("MissionBriefingModal");
            if (parent != null) go.transform.SetParent(parent, false);
            return go.AddComponent<MissionBriefingModal>();
        }

        public IEnumerator ShowBriefingRoutine(int level, Action onCommence, Action onTutorial)
        {
            onCommenceAction = onCommence;
            onTutorialAction = onTutorial;
            isWaitingForPlayer = true;

            BuildModalUI(level);
            modalRoot.SetActive(true);
            AudioManager.Instance?.PlayWaveIncoming(0.9f);

            // Wait until player presses Launch or Tutorial
            while (isWaitingForPlayer)
            {
                yield return null;
            }

            if (modalRoot != null)
                modalRoot.SetActive(false);
        }

        private void BuildModalUI(int level)
        {
            if (modalRoot != null) Destroy(modalRoot);

            // Fullscreen backdrop
            modalRoot = new GameObject("BriefingModalRoot", typeof(RectTransform), typeof(CanvasGroup));
            modalRoot.transform.SetParent(transform, false);
            var rootRect = modalRoot.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.sizeDelta = Vector2.zero;

            var bg = modalRoot.AddComponent<Image>();
            bg.color = new Color(0.02f, 0.05f, 0.1f, 0.94f);
            bg.raycastTarget = true;

            // Tactical Box Frame
            var boxGo = new GameObject("BriefingBox", typeof(RectTransform), typeof(Image));
            boxGo.transform.SetParent(modalRoot.transform, false);
            var boxRect = boxGo.GetComponent<RectTransform>();
            boxRect.anchorMin = new Vector2(0.5f, 0.5f);
            boxRect.anchorMax = new Vector2(0.5f, 0.5f);
            boxRect.pivot = new Vector2(0.5f, 0.5f);
            boxRect.sizeDelta = new Vector2(780f, 500f);

            var boxBg = boxGo.GetComponent<Image>();
            Starfall.UI.Theme.SciFiUITheme.ApplyPanel(boxBg, new Color(0.04f, 0.08f, 0.16f, 0.98f));

            // Top Header Banner
            var headerGo = new GameObject("Header", typeof(RectTransform), typeof(TextMeshProUGUI));
            headerGo.transform.SetParent(boxGo.transform, false);
            var hRect = headerGo.GetComponent<RectTransform>();
            hRect.anchorMin = new Vector2(0f, 1f);
            hRect.anchorMax = new Vector2(1f, 1f);
            hRect.pivot = new Vector2(0.5f, 1f);
            hRect.anchoredPosition = new Vector2(0f, -20f);
            hRect.sizeDelta = new Vector2(0f, 40f);

            var hTmp = headerGo.GetComponent<TextMeshProUGUI>();
            hTmp.text = "// FLEET COMMAND // TACTICAL MISSION BRIEFING";
            hTmp.fontSize = 22;
            hTmp.fontStyle = FontStyles.Bold;
            hTmp.alignment = TextAlignmentOptions.Center;
            hTmp.color = new Color(1f, 0.85f, 0.2f); // Gold / Amber

            // Sector Title & Threat
            string sectorTitle = level switch
            {
                2 => "SECTOR 02: ASTEROID BELT // DEBRIS CORRIDOR",
                3 => "SECTOR 03: DEEP NEBULA // ION STORM FRONT",
                4 => "SECTOR 04: WAR-TORN WRECKAGE // DERELICT GRAVEYARD",
                _ => "SECTOR 01: EARTH ORBIT // LOW ORBIT PATROL ZONE"
            };

            string threatText = level switch
            {
                2 => "THREAT LEVEL: ELEVATED // DREADNOUGHT FLAGSHIP DETECTED",
                3 => "THREAT LEVEL: SEVERE // HIVE QUEEN BIO-LEVIATHAN",
                4 => "THREAT LEVEL: EXTREME // INFERNO JUGGERNAUT",
                _ => "THREAT LEVEL: MODERATE // CARRIER MOTHERSHIP INBOUND"
            };

            var subGo = new GameObject("SectorTitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            subGo.transform.SetParent(boxGo.transform, false);
            var subRect = subGo.GetComponent<RectTransform>();
            subRect.anchorMin = new Vector2(0f, 1f);
            subRect.anchorMax = new Vector2(1f, 1f);
            subRect.pivot = new Vector2(0.5f, 1f);
            subRect.anchoredPosition = new Vector2(0f, -62f);
            subRect.sizeDelta = new Vector2(0f, 26f);

            var subTmp = subGo.GetComponent<TextMeshProUGUI>();
            subTmp.text = $"<color=#00E5FF><b>{sectorTitle}</b></color>\n<size=13><color=#FF6644>{threatText}</color></size>";
            subTmp.fontSize = 15;
            subTmp.alignment = TextAlignmentOptions.Center;

            // Directives & Intel Body Text
            var bodyGo = new GameObject("IntelBody", typeof(RectTransform), typeof(TextMeshProUGUI));
            bodyGo.transform.SetParent(boxGo.transform, false);
            var bodyRect = bodyGo.GetComponent<RectTransform>();
            bodyRect.anchorMin = new Vector2(0.06f, 0.26f);
            bodyRect.anchorMax = new Vector2(0.94f, 0.82f);
            bodyRect.sizeDelta = Vector2.zero;

            var objList = MissionObjectiveManager.GenerateObjectives(level);
            var sb = new System.Text.StringBuilder();
            sb.AppendLine("<size=15><color=#FFD700><b>TACTICAL MISSION OBJECTIVES & BOUNTIES:</b></color></size>");
            for (int i = 0; i < objList.Count; i++)
            {
                var obj = objList[i];
                string tag = (i == 0) ? "<color=#FF4444>[PRIMARY]</color>" : (i == 1) ? "<color=#00E5FF>[TACTICAL]</color>" : "<color=#CC77FF>[CHALLENGE]</color>";
                sb.AppendLine($"{tag} <b>{obj.title}</b> — <color=#00FF88>+{obj.starCreditsReward} CR</color>");
                sb.AppendLine($"   <size=12><color=#A8C5E2>{obj.description}</color></size>");
            }

            string advice = level switch
            {
                2 => "<color=#FFAA00><b>[ARMORY INTEL]</b></color> <size=12><color=#C8D8E8>Shields do NOT auto-recharge! Blast drifting Asteroids to collect Shield pods. Upgrade Shield Generator & Drone Attractor in the Armory Shop.</color></size>",
                3 => "<color=#FFAA00><b>[ARMORY INTEL]</b></color> <size=12><color=#C8D8E8>Ion storms and phasing Blinkers will test your reflexes. Upgrade Special Ability Cooldown and Bomb payload in Hangar to clear bullet curtains.</color></size>",
                4 => "<color=#FFAA00><b>[ARMORY INTEL]</b></color> <size=12><color=#C8D8E8>Extreme hostile density. Visit the Fleet Hangar to deploy advanced warships (Heavy Destroyer or Apex Sovereign) with Level 3+ cannons.</color></size>",
                _ => "<color=#FFAA00><b>[ARMORY INTEL]</b></color> <size=12><color=#C8D8E8>Maneuver using the Flight Trackpad. Eliminate squad commanders first to demoralize enemy formations. Shields require power-up pickups to restore.</color></size>"
            };
            sb.AppendLine();
            sb.AppendLine(advice);

            var bodyTmp = bodyGo.GetComponent<TextMeshProUGUI>();
            bodyTmp.text = sb.ToString();
            bodyTmp.fontSize = 13;
            bodyTmp.lineSpacing = 10;
            bodyTmp.color = new Color(0.9f, 0.95f, 1f, 0.95f);

            // Button Container at bottom
            var btnContainer = new GameObject("ButtonRow", typeof(RectTransform), typeof(HorizontalLayoutGroup));
            btnContainer.transform.SetParent(boxGo.transform, false);
            var rowRect = btnContainer.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0.06f, 0.06f);
            rowRect.anchorMax = new Vector2(0.94f, 0.22f);
            rowRect.sizeDelta = Vector2.zero;

            var layout = btnContainer.GetComponent<HorizontalLayoutGroup>();
            layout.spacing = 20f;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = true;

            // Tutorial Button (Prominent on Level 1)
            bool isFirstLevel = (level == 1);
            if (isFirstLevel)
            {
                CreateButton(btnContainer.transform, "PILOT TUTORIAL DRILL", new Color(0.1f, 0.45f, 0.85f, 0.95f), () =>
                {
                    isWaitingForPlayer = false;
                    AudioManager.Instance?.PlayPowerUp();
                    onTutorialAction?.Invoke();
                });
            }

            // Launch / Commence Button
            CreateButton(btnContainer.transform, "COMMENCE MISSION [LAUNCH]", new Color(0.05f, 0.75f, 0.45f, 0.95f), () =>
            {
                isWaitingForPlayer = false;
                AudioManager.Instance?.PlayReinforcementAlert(1.0f);
                onCommenceAction?.Invoke();
            });
        }

        private void CreateButton(Transform parent, string label, Color bgColor, Action onClick)
        {
            var btnGo = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image), typeof(Button));
            btnGo.transform.SetParent(parent, false);

            var img = btnGo.GetComponent<Image>();
            var btn = btnGo.GetComponent<Button>();
            Starfall.UI.Theme.SciFiUITheme.ApplyButton(btn, img, isPrimary: true);
            var anim = btnGo.AddComponent<Starfall.UI.Theme.SciFiButtonAnimator>();
            anim.PulseIdle = true;
            btn.onClick.AddListener(() => onClick?.Invoke());

            var txtGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            txtGo.transform.SetParent(btnGo.transform, false);
            var tRect = txtGo.GetComponent<RectTransform>();
            tRect.anchorMin = Vector2.zero;
            tRect.anchorMax = Vector2.one;
            tRect.sizeDelta = Vector2.zero;

            var tmp = txtGo.GetComponent<TextMeshProUGUI>();
            tmp.text = label;
            tmp.fontSize = 15;
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.color = Color.white;
            tmp.raycastTarget = false;
        }
    }
}
