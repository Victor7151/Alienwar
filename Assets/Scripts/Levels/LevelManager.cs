using UnityEngine;
using Starfall.Core;

namespace Starfall.Levels
{
    /// <summary>
    /// Coordinates mission lifecycle, currency progression, and campaign advancement.
    /// Acts as the bridge between MissionData, WaveManager, and GameManager.
    /// Recommended flow per Section 17: LevelData -> LevelManager -> WaveManager -> EnemySpawner
    /// </summary>
    public class LevelManager : MonoBehaviour
    {
        public static LevelManager Instance { get; private set; }

        [Header("Active Mission (§32)")]
        [SerializeField] private MissionData activeMission;

        [Header("Campaign Levels")]
        [SerializeField] private MissionData[] campaignLevels;

        [Header("Campaign Missions (Legacy / Earth Fallback)")]
        [SerializeField] private MissionData[] earthMissions;

        [Header("Progression Currency (§14)")]
        [SerializeField] private int starCredits = 0;

        private int currentLevelIndex = 0;

        public MissionData ActiveMission => activeMission;
        public int CurrentLevelNumber => currentLevelIndex + 1;
        public int TotalLevels => (campaignLevels != null && campaignLevels.Length > 0) ? campaignLevels.Length : (earthMissions?.Length ?? 1);
        public int StarCredits => starCredits;

        public event System.Action<MissionData> OnMissionStarted;
        public event System.Action<MissionData, int> OnMissionCompleted;
        public event System.Action<int> OnStarCreditsChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (sceneName.Contains("Level1") || sceneName.Contains("Earth")) currentLevelIndex = 0;
            else if (sceneName.Contains("Level2") || sceneName.Contains("Asteroid")) currentLevelIndex = 1;
            else if (sceneName.Contains("Level3") || sceneName.Contains("Nebula")) currentLevelIndex = 2;
            else if (sceneName.Contains("Level4") || sceneName.Contains("WarTorn") || sceneName.Contains("Wreckage")) currentLevelIndex = 3;

            if (campaignLevels != null && currentLevelIndex < campaignLevels.Length && campaignLevels[currentLevelIndex] != null)
            {
                activeMission = campaignLevels[currentLevelIndex];
            }
            else if (activeMission == null && earthMissions != null && earthMissions.Length > 0)
            {
                activeMission = earthMissions[0];
            }

            if (activeMission != null)
            {
                StartMission(activeMission);
            }
            else
            {
                Debug.LogWarning("[LevelManager] No activeMission found! Calling WaveManager.StartFallbackWaves().");
                if (WaveManager.Instance != null)
                {
                    WaveManager.Instance.StartFallbackWaves();
                }
            }
        }

        public void StartMission(MissionData mission)
        {
            if (mission == null) return;
            activeMission = mission;

            if (WaveManager.Instance != null)
            {
                WaveManager.Instance.LoadMission(mission);
            }

            OnMissionStarted?.Invoke(activeMission);
            Debug.Log($"[LevelManager] Started Mission: {activeMission.MissionId} ({activeMission.DisplayName})");
        }

        private bool missionCompleted = false;

        public void CompleteActiveMission()
        {
            if (missionCompleted) return;
            missionCompleted = true;

            int missionReward = activeMission != null ? activeMission.StarCreditsReward : 500;
            int earnedCredits = missionReward;

            // Evaluate end-of-sector objectives (lives preserved, weapon upgrades, etc.)
            // Objective bounties are banked as each objective completes, so they only count toward the displayed total here.
            var playerHealth = FindAnyObjectByType<Starfall.Player.PlayerHealth>();
            var playerCombat = FindAnyObjectByType<Starfall.Player.PlayerCombat>();
            if (MissionObjectiveManager.Instance != null)
            {
                int objectiveBonus = MissionObjectiveManager.Instance.EvaluateEndOfMissionObjectives(playerHealth, playerCombat);
                earnedCredits += objectiveBonus;
            }

            AddStarCredits(missionReward);

            int currentScore = GameManager.Instance != null ? GameManager.Instance.CurrentScore : 0;
            int levelNum = CurrentLevelNumber;

            // Save wave 10 cleared, persistent high score, and unlock next level
            LevelProgressManager.SaveWaveProgress(levelNum, 10);
            LevelProgressManager.SaveHighScore(levelNum, currentScore);
            LevelProgressManager.UnlockNextLevel(levelNum);

            if (activeMission != null)
            {
                OnMissionCompleted?.Invoke(activeMission, earnedCredits);
            }

            string warpMsg = (levelNum < 4) 
                ? $"SECTOR 0{levelNum} SECURED! WARP DRIVE ENGAGED!" 
                : "ALL SECTORS SECURED! CAMPAIGN VICTORY!";
            Starfall.UI.GameplayHUD.Instance?.ShowAnnouncement(warpMsg, new Color(0.2f, 1f, 0.55f));

            string missionName = activeMission != null ? activeMission.DisplayName : $"Sector {levelNum}";
            Debug.Log($"[LevelManager] Completed {missionName}! Awarded {earnedCredits} Star Credits.");

            StartCoroutine(AdvanceToNextLevelRoutine());
        }

        private System.Collections.IEnumerator AdvanceToNextLevelRoutine()
        {
            yield return new WaitForSeconds(3.0f);

            int levelNum = CurrentLevelNumber;
            if (levelNum < 4)
            {
                int nextLevel = levelNum + 1;
                Debug.Log($"<color=green>[LevelManager] Advancing from Sector {levelNum} to Sector {nextLevel}...</color>");
                LevelProgressManager.LoadLevel(nextLevel);
            }
            else
            {
                Debug.Log("<color=green>[LevelManager] ALL CAMPAIGN SECTORS CLEARED! TRIGGERING VICTORY END SCREEN!</color>");
                if (GameManager.Instance != null)
                {
                    GameManager.Instance.SetState(GameState.Victory);
                }
            }
        }

        public void AddStarCredits(int amount)
        {
            starCredits += amount;
            LevelProgressManager.AddCredits(amount);
            OnStarCreditsChanged?.Invoke(starCredits);
        }
    }
}
