using UnityEngine;
using UnityEngine.SceneManagement;

namespace Starfall.Levels
{
    /// <summary>
    /// Manages persistent level unlocking, high scores, and scene transitions.
    /// Allows players to unlock levels 1 -> 2 -> 3 and replay any unlocked level.
    /// </summary>
    public static class LevelProgressManager
    {
        private const string UNLOCKED_KEY = "Starfall_HighestUnlockedLevel";
        private const string CREDITS_KEY = "Starfall_StarCredits";

        // In-memory target start wave when loading a mission (default: 1)
        public static int TargetStartWave { get; set; } = 1;

        public const int CampaignLevelCount = 4;

        /// <summary>
        /// Returns the highest unlocked level number (1-based). New players start at Sector 01 and
        /// unlock each sector by beating the previous boss. Use STARFALL > Campaign Sectors > Unlock All
        /// (or UnlockAllLevels) for testing.
        /// </summary>
        public static int GetHighestUnlockedLevel()
        {
            int unlocked = Mathf.Clamp(PlayerPrefs.GetInt(UNLOCKED_KEY, 1), 1, CampaignLevelCount);
            for (int i = 1; i <= unlocked; i++)
            {
                if (PlayerPrefs.GetInt($"Starfall_Level_{i}_HighestWave", 0) == 0)
                {
                    PlayerPrefs.SetInt($"Starfall_Level_{i}_HighestWave", 1);
                    PlayerPrefs.SetInt($"Starfall_Level_{i}_CurrentWave", 1);
                }
            }
            return unlocked;
        }

        /// <summary>Checks if a given level number is unlocked.</summary>
        public static bool IsLevelUnlocked(int level)
        {
            if (level <= 1) return true;
            return level <= GetHighestUnlockedLevel();
        }

        /// <summary>
        /// Gets the highest wave reached/cleared in this level (1..10).
        /// Returns 1 by default for unlocked levels, or 0 if level is locked.
        /// </summary>
        public static int GetHighestWave(int level)
        {
            if (!IsLevelUnlocked(level)) return 0;
            return PlayerPrefs.GetInt($"Starfall_Level_{level}_HighestWave", 1);
        }

        /// <summary>
        /// Gets the current wave where the player stopped or should continue from.
        /// </summary>
        public static int GetCurrentWave(int level)
        {
            if (!IsLevelUnlocked(level)) return 1;
            return PlayerPrefs.GetInt($"Starfall_Level_{level}_CurrentWave", 1);
        }

        /// <summary>
        /// Checks if a specific wave in a level is unlocked.
        /// </summary>
        public static bool IsWaveUnlocked(int level, int waveNumber)
        {
            if (!IsLevelUnlocked(level)) return false;
            return waveNumber <= GetHighestWave(level);
        }

        /// <summary>
        /// Saves progress after clearing a wave. Unlocks next wave (up to wave 10).
        /// </summary>
        public static void SaveWaveProgress(int level, int waveCleared)
        {
            int highest = PlayerPrefs.GetInt($"Starfall_Level_{level}_HighestWave", 1);
            int nextWave = Mathf.Clamp(waveCleared + 1, 1, 10);

            if (nextWave > highest)
            {
                PlayerPrefs.SetInt($"Starfall_Level_{level}_HighestWave", nextWave);
            }

            PlayerPrefs.SetInt($"Starfall_Level_{level}_CurrentWave", nextWave);
            PlayerPrefs.Save();
            Debug.Log($"<color=cyan>[LevelProgressManager] Level {level} Wave {waveCleared} cleared! Next wave: {nextWave}</color>");
        }

        /// <summary>Unlocks the next level when a level is completed.</summary>
        public static void UnlockNextLevel(int completedLevelNumber)
        {
            int currentHighest = GetHighestUnlockedLevel();
            int nextLevel = completedLevelNumber + 1;
            if (nextLevel > currentHighest && nextLevel <= 4)
            {
                PlayerPrefs.SetInt(UNLOCKED_KEY, nextLevel);
                // Initialize next level's starting wave to 1
                if (PlayerPrefs.GetInt($"Starfall_Level_{nextLevel}_HighestWave", 0) == 0)
                {
                    PlayerPrefs.SetInt($"Starfall_Level_{nextLevel}_HighestWave", 1);
                    PlayerPrefs.SetInt($"Starfall_Level_{nextLevel}_CurrentWave", 1);
                }
                PlayerPrefs.Save();
                Debug.Log($"<color=green>[LevelProgressManager] Level {nextLevel} UNLOCKED!</color>");
            }
        }

        /// <summary>Saves high score for a specific level.</summary>
        public static void SaveHighScore(int level, int score)
        {
            string key = $"Starfall_Level_{level}_HighScore";
            int high = PlayerPrefs.GetInt(key, 0);
            if (score > high)
            {
                PlayerPrefs.SetInt(key, score);
                PlayerPrefs.Save();
                Debug.Log($"[LevelProgressManager] New High Score for Level {level}: {score:N0}");
            }
        }

        /// <summary>Gets highest recorded score for a specific level.</summary>
        public static int GetHighScore(int level)
        {
            return PlayerPrefs.GetInt($"Starfall_Level_{level}_HighScore", 0);
        }

        /// <summary>Loads the scene corresponding to the given level at wave 1.</summary>
        public static void LoadLevel(int level)
        {
            LoadLevelAtWave(level, 1);
        }

        /// <summary>Loads the scene corresponding to the given level starting at a designated wave.</summary>
        public static void LoadLevelAtWave(int level, int targetWave)
        {
            TargetStartWave = Mathf.Clamp(targetWave, 1, 10);

            string sceneName = level switch
            {
                1 => "Level1_EarthOrbit",
                2 => "Level2_AsteroidBelt",
                3 => "Level3_DeepNebula",
                4 => "Level4_WarTornWreckage",
                _ => "SampleScene"
            };

            Debug.Log($"[LevelProgressManager] Loading {sceneName} starting at Wave {TargetStartWave}...");
            string title = level switch
            {
                1 => "SECTOR 01 • EARTH ORBIT",
                2 => "SECTOR 02 • ASTEROID BELT",
                3 => "SECTOR 03 • DEEP NEBULA",
                4 => "SECTOR 04 • WAR-TORN WRECKAGE",
                _ => "WARPING…"
            };
            Starfall.UI.LoadingScreen.LoadScene(sceneName, title);
        }

        /// <summary>Gets current Star Credits balance. New players start with 1,000 credits.</summary>
        public static int GetCredits()
        {
            return PlayerPrefs.GetInt(CREDITS_KEY, 1000);
        }

        /// <summary>Adds earned Star Credits to persistent bank.</summary>
        public static void AddCredits(int amount)
        {
            if (amount <= 0) return;
            int total = GetCredits() + amount;
            PlayerPrefs.SetInt(CREDITS_KEY, total);
            PlayerPrefs.Save();
            Debug.Log($"[LevelProgressManager] +{amount} Credits! Total: {total:N0}");
        }

        /// <summary>Spends Star Credits if balance allows.</summary>
        public static bool SpendCredits(int cost)
        {
            int current = GetCredits();
            if (current >= cost)
            {
                PlayerPrefs.SetInt(CREDITS_KEY, current - cost);
                PlayerPrefs.Save();
                Debug.Log($"[LevelProgressManager] Spent {cost} Credits! Remaining: {current - cost:N0}");
                return true;
            }
            return false;
        }

        /// <summary>Loads the Home Scene (Central Command Hub).</summary>
        public static void LoadHomeScene()
        {
            Time.timeScale = 1f;
            Debug.Log("[LevelProgressManager] Returning to Home Scene...");
            Starfall.UI.LoadingScreen.LoadScene("HomeScene", "RETURNING TO COMMAND");
        }

        /// <summary>Loads the Level Select / Star Map menu scene.</summary>
        public static void LoadLevelSelect()
        {
            Time.timeScale = 1f;
            Debug.Log("[LevelProgressManager] Returning to Command Center...");
            Starfall.UI.LoadingScreen.LoadScene("HomeScene", "RETURNING TO COMMAND");
        }

        /// <summary>Unlocks all campaign levels (Sectors 1, 2, 3, 4) and initializes their waves.</summary>
        public static void UnlockAllLevels()
        {
            PlayerPrefs.SetInt(UNLOCKED_KEY, 4);
            for (int i = 1; i <= 4; i++)
            {
                if (PlayerPrefs.GetInt($"Starfall_Level_{i}_HighestWave", 0) == 0)
                {
                    PlayerPrefs.SetInt($"Starfall_Level_{i}_HighestWave", 1);
                    PlayerPrefs.SetInt($"Starfall_Level_{i}_CurrentWave", 1);
                }
            }
            PlayerPrefs.Save();
            Debug.Log("<color=green>[LevelProgressManager] All campaign levels (Sectors 1, 2, 3, 4) UNLOCKED!</color>");
        }

        /// <summary>Resets progression (useful for testing or profile reset).</summary>
        public static void ResetProgress(bool resetToLevelOne = false)
        {
            int startingLevel = 1;
            PlayerPrefs.SetInt(UNLOCKED_KEY, startingLevel);
            PlayerPrefs.SetInt(CREDITS_KEY, 1000);
            for (int i = 1; i <= 5; i++)
            {
                PlayerPrefs.DeleteKey($"Starfall_Level_{i}_HighScore");
                if (i <= startingLevel)
                {
                    PlayerPrefs.SetInt($"Starfall_Level_{i}_HighestWave", 1);
                    PlayerPrefs.SetInt($"Starfall_Level_{i}_CurrentWave", 1);
                }
                else
                {
                    PlayerPrefs.DeleteKey($"Starfall_Level_{i}_HighestWave");
                    PlayerPrefs.DeleteKey($"Starfall_Level_{i}_CurrentWave");
                }
            }
            Starfall.Core.ShipUpgradesManager.ResetAllUpgrades();
            PlayerPrefs.Save();
            TargetStartWave = 1;
            Debug.Log($"[LevelProgressManager] Progression reset. Unlocked Sectors: {startingLevel}, Credits: 1,000.");
        }
    }
}
