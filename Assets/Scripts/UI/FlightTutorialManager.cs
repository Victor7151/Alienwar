using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Starfall.Core;
using Starfall.Player;
using Starfall.Enemies;
using Starfall.Audio;
using Starfall.VFX;

namespace Starfall.UI
{
    /// <summary>
    /// Interactive In-Flight Tutorial Manager.
    /// Guides the pilot step-by-step through live flight gameplay before launching Wave 1:
    ///   Step 1: Thrusters Calibration (Analog Trackpad)
    ///   Step 2: Weapon Systems Targeting Drill (Target Drone)
    ///   Step 3: Hazard Evasion Protocol (Asteroid corridor)
    ///   Step 4: Special Ordnance (Screen Bomb / Ability detonator)
    ///   Step 5: Fleet Command Authorization -> Launches Wave 1
    /// </summary>
    public class FlightTutorialManager : MonoBehaviour
    {
        public static FlightTutorialManager Instance { get; private set; }

        private GameObject tutorialCanvasGo;
        private TextMeshProUGUI instructionText;
        private TextMeshProUGUI subInstructionText;
        private TextMeshProUGUI stepBadgeText;
        private GameObject pointerArrowGo;
        private bool isTutorialRunning = false;
        private bool skipRequested = false;

        public bool IsRunning => isTutorialRunning;

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

        public static FlightTutorialManager EnsureInstance(Transform parent)
        {
            if (Instance != null) return Instance;
            var existing = FindAnyObjectByType<FlightTutorialManager>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            var go = new GameObject("FlightTutorialManager");
            if (parent != null) go.transform.SetParent(parent, false);
            return go.AddComponent<FlightTutorialManager>();
        }

        public IEnumerator RunTutorialRoutine(Action onComplete)
        {
            isTutorialRunning = true;
            skipRequested = false;
            BuildTutorialHUD();

            tutorialCanvasGo.SetActive(true);
            AudioManager.Instance?.PlayReinforcementAlert(0.9f);

            // Step 1: Thruster Calibration
            yield return StartCoroutine(Step1_ThrusterCalibration());
            if (skipRequested) { FinishTutorial(onComplete); yield break; }

            // Step 2: Weapons Test
            yield return StartCoroutine(Step2_WeaponsDrill());
            if (skipRequested) { FinishTutorial(onComplete); yield break; }

            // Step 3: Hazard Evasion
            yield return StartCoroutine(Step3_HazardEvasion());
            if (skipRequested) { FinishTutorial(onComplete); yield break; }

            // Step 4: Special Ordnance Trigger
            yield return StartCoroutine(Step4_SpecialOrdnance());
            if (skipRequested) { FinishTutorial(onComplete); yield break; }

            // Step 5: Mission Clearance
            yield return StartCoroutine(Step5_MissionClearance());

            FinishTutorial(onComplete);
        }

        private void FinishTutorial(Action onComplete)
        {
            isTutorialRunning = false;
            PlayerPrefs.SetInt("Starfall_TutorialCompleted", 1);
            PlayerPrefs.Save();

            if (tutorialCanvasGo != null)
                tutorialCanvasGo.SetActive(false);

            onComplete?.Invoke();
        }

        private void BuildTutorialHUD()
        {
            if (tutorialCanvasGo != null) return;

            tutorialCanvasGo = new GameObject("TutorialHUDOverlay", typeof(RectTransform));
            tutorialCanvasGo.transform.SetParent(transform, false);
            var rootRect = tutorialCanvasGo.GetComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.sizeDelta = Vector2.zero;

            // Top Instruction Banner Card
            var cardGo = new GameObject("InstructionCard", typeof(RectTransform), typeof(Image));
            cardGo.transform.SetParent(tutorialCanvasGo.transform, false);
            var cardRect = cardGo.GetComponent<RectTransform>();
            cardRect.anchorMin = new Vector2(0.5f, 1f);
            cardRect.anchorMax = new Vector2(0.5f, 1f);
            cardRect.pivot = new Vector2(0.5f, 1f);
            cardRect.anchoredPosition = new Vector2(0f, -60f);
            cardRect.sizeDelta = new Vector2(680f, 95f);

            var cardImg = cardGo.GetComponent<Image>();
            cardImg.color = new Color(0.04f, 0.08f, 0.16f, 0.92f);

            // Step Badge
            var badgeGo = new GameObject("StepBadge", typeof(RectTransform), typeof(TextMeshProUGUI));
            badgeGo.transform.SetParent(cardGo.transform, false);
            var bRect = badgeGo.GetComponent<RectTransform>();
            bRect.anchorMin = new Vector2(0f, 1f);
            bRect.anchorMax = new Vector2(1f, 1f);
            bRect.pivot = new Vector2(0.5f, 1f);
            bRect.anchoredPosition = new Vector2(0f, -10f);
            bRect.sizeDelta = new Vector2(0f, 22f);

            stepBadgeText = badgeGo.GetComponent<TextMeshProUGUI>();
            stepBadgeText.fontSize = 13;
            stepBadgeText.fontStyle = FontStyles.Bold;
            stepBadgeText.alignment = TextAlignmentOptions.Center;
            stepBadgeText.color = new Color(1f, 0.8f, 0.15f);

            // Main Instruction Text
            var instGo = new GameObject("MainInst", typeof(RectTransform), typeof(TextMeshProUGUI));
            instGo.transform.SetParent(cardGo.transform, false);
            var iRect = instGo.GetComponent<RectTransform>();
            iRect.anchorMin = new Vector2(0f, 0.35f);
            iRect.anchorMax = new Vector2(1f, 0.78f);
            iRect.sizeDelta = Vector2.zero;

            instructionText = instGo.GetComponent<TextMeshProUGUI>();
            instructionText.fontSize = 17;
            instructionText.fontStyle = FontStyles.Bold;
            instructionText.alignment = TextAlignmentOptions.Center;
            instructionText.color = Color.white;

            // Sub Instruction Text
            var subGo = new GameObject("SubInst", typeof(RectTransform), typeof(TextMeshProUGUI));
            subGo.transform.SetParent(cardGo.transform, false);
            var sRect = subGo.GetComponent<RectTransform>();
            sRect.anchorMin = new Vector2(0f, 0.05f);
            sRect.anchorMax = new Vector2(1f, 0.35f);
            sRect.sizeDelta = Vector2.zero;

            subInstructionText = subGo.GetComponent<TextMeshProUGUI>();
            subInstructionText.fontSize = 12;
            subInstructionText.alignment = TextAlignmentOptions.Center;
            subInstructionText.color = new Color(0f, 0.85f, 1f, 0.9f);

            // Skip Tutorial Button (Top-Right)
            var skipGo = new GameObject("SkipBtn", typeof(RectTransform), typeof(Image), typeof(Button));
            skipGo.transform.SetParent(tutorialCanvasGo.transform, false);
            var skipRect = skipGo.GetComponent<RectTransform>();
            skipRect.anchorMin = new Vector2(1f, 1f);
            skipRect.anchorMax = new Vector2(1f, 1f);
            skipRect.pivot = new Vector2(1f, 1f);
            skipRect.anchoredPosition = new Vector2(-20f, -20f);
            skipRect.sizeDelta = new Vector2(130f, 36f);

            var skipImg = skipGo.GetComponent<Image>();
            skipImg.color = new Color(0.1f, 0.15f, 0.25f, 0.85f);

            var skipBtn = skipGo.GetComponent<Button>();
            skipBtn.onClick.AddListener(() => { skipRequested = true; });

            var skipTxtGo = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
            skipTxtGo.transform.SetParent(skipGo.transform, false);
            var stRect = skipTxtGo.GetComponent<RectTransform>();
            stRect.anchorMin = Vector2.zero;
            stRect.anchorMax = Vector2.one;
            stRect.sizeDelta = Vector2.zero;

            var skipTxt = skipTxtGo.GetComponent<TextMeshProUGUI>();
            skipTxt.text = "SKIP TUTORIAL >>";
            skipTxt.fontSize = 12;
            skipTxt.fontStyle = FontStyles.Bold;
            skipTxt.alignment = TextAlignmentOptions.Center;
            skipTxt.color = new Color(0.8f, 0.85f, 0.9f);
        }

        private IEnumerator Step1_ThrusterCalibration()
        {
            stepBadgeText.text = "STEP 1 // FLIGHT CONTROLS CALIBRATION";
            instructionText.text = "MANEUVER USING THE ANALOG TRACKPAD";
            subInstructionText.text = "Tilt the Flight Trackpad in the bottom-left corner in all directions";

            bool movedLeft = false, movedRight = false, movedUp = false, movedDown = false;
            float timer = 0f;

            while (timer < 10f)
            {
                if (skipRequested) yield break;

                Vector2 input = TacticalFlightTrackpad.Instance != null && TacticalFlightTrackpad.Instance.IsActive
                    ? TacticalFlightTrackpad.Instance.InputDirection
                    : (VirtualJoystick.Instance != null ? VirtualJoystick.Instance.InputDirection : Vector2.zero);

                if (input.sqrMagnitude < 0.05f && Starfall.Player.PlayerController.Instance != null)
                {
                    input = Starfall.Player.PlayerController.Instance.CurrentInput;
                }

                if (input.x < -0.3f) movedLeft = true;
                if (input.x > 0.3f) movedRight = true;
                if (input.y > 0.3f) movedUp = true;
                if (input.y < -0.3f) movedDown = true;

                if (movedLeft && movedRight && (movedUp || movedDown))
                {
                    break;
                }

                timer += Time.deltaTime;
                yield return null;
            }

            AudioManager.Instance?.PlayPowerUp();
            instructionText.text = "<color=#00FFAA>THRUSTER CALIBRATION COMPLETE [OK]</color>";
            yield return new WaitForSeconds(1.0f);
        }

        private IEnumerator Step2_WeaponsDrill()
        {
            stepBadgeText.text = "STEP 2 // PRIMARY WEAPONS TARGETING";
            instructionText.text = "AUTO-TARGETING ACTIVE: DESTROY PRACTICE DRONE";
            subInstructionText.text = "Align your warship under the drone; plasma cannons fire automatically";

            // Spawn training drone in center
            GameObject drone = null;
            if (ObjectPool.Instance != null)
            {
                drone = ObjectPool.Instance.Spawn("ScoutEnemy", new Vector3(0f, 6.0f, 0f), Quaternion.identity);
                if (drone != null && drone.TryGetComponent<EnemyBase>(out var e))
                {
                    e.SetWaveTier(1, 0.5f, 0.5f);
                }
            }

            while (drone != null && drone.activeInHierarchy)
            {
                if (skipRequested) yield break;
                yield return null;
            }

            AudioManager.Instance?.PlayExplosion(false);
            instructionText.text = "<color=#00FFAA>TARGET ELIMINATED: WEAPONS CERTIFIED [OK]</color>";
            yield return new WaitForSeconds(1.0f);
        }

        private IEnumerator Step3_HazardEvasion()
        {
            stepBadgeText.text = "STEP 3 // HAZARD AVOIDANCE PROTOCOL";
            instructionText.text = "WARNING: INCOMING ASTEROID HAZARD!";
            subInstructionText.text = "Maneuver away from the descending trajectory";

            // Spawn an asteroid that drifts down along the right side
            if (ObjectPool.Instance != null)
            {
                var rock = ObjectPool.Instance.Spawn("AsteroidHazard", new Vector3(1.8f, 7.5f, 0f), Quaternion.identity);
                if (rock != null && rock.TryGetComponent<AsteroidHazard>(out var h))
                {
                    h.Initialize(3.5f, Vector2.down, 3, 45f);
                }
            }

            AudioManager.Instance?.PlayHazardAlert();
            yield return new WaitForSeconds(2.8f);

            instructionText.text = "<color=#00FFAA>HAZARD CLEARED: EVASION VERIFIED [OK]</color>";
            AudioManager.Instance?.PlayPowerUp();
            yield return new WaitForSeconds(1.0f);
        }

        private IEnumerator Step4_SpecialOrdnance()
        {
            stepBadgeText.text = "STEP 4 // SPECIAL ORDNANCE DETONATION";
            instructionText.text = "TACTICAL CORE PRIMED: TAP SPECIAL ABILITY TRIGGER";
            subInstructionText.text = "Tap the circular Rive Special Button on the bottom-right";

            var pc = FindAnyObjectByType<PlayerCombat>();
            if (pc != null)
            {
                pc.ChargeAbilityFull();
            }

            // Spawn 3 weak training drones to detonate
            if (ObjectPool.Instance != null)
            {
                ObjectPool.Instance.Spawn("ScoutEnemy", new Vector3(-1.8f, 3.5f, 0f), Quaternion.identity);
                ObjectPool.Instance.Spawn("ScoutEnemy", new Vector3( 0.0f, 3.8f, 0f), Quaternion.identity);
                ObjectPool.Instance.Spawn("ScoutEnemy", new Vector3( 1.8f, 3.5f, 0f), Quaternion.identity);
            }

            bool abilityUsed = false;
            float timer = 0f;
            while (!abilityUsed && timer < 12f)
            {
                if (skipRequested) yield break;
                // If player used ability (or bombs decreased or enemies wiped)
                if (pc != null && !pc.IsAbilityReady)
                {
                    abilityUsed = true;
                    break;
                }
                timer += Time.deltaTime;
                yield return null;
            }

            VFXKit.Ring(Vector3.zero, new Color(0f, 1f, 0.9f, 0.9f), 0.5f, 18f, 0.6f);
            EnemyBase.DespawnAllHostiles(true);
            AudioManager.Instance?.PlaySpecialBomb();

            instructionText.text = "<color=#00FFAA>TACTICAL DETONATION SUCCESSFUL [OK]</color>";
            yield return new WaitForSeconds(1.2f);
        }

        private IEnumerator Step5_MissionClearance()
        {
            stepBadgeText.text = "// FLIGHT CERTIFICATION COMPLETE //";
            instructionText.text = "<color=#00E5FF>WELCOME ONBOARD, PILOT!</color>";
            subInstructionText.text = "Fleet Command grants mission clearance. Prepare for Wave 1...";

            AudioManager.Instance?.PlayPowerUp();
            yield return new WaitForSeconds(2.0f);
        }
    }
}
