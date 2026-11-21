using UnityEngine;
using UnityEngine.SceneManagement;

namespace Starfall.Core
{
    public enum GameState
    {
        Playing,
        BossFight,
        MissionComplete,
        GameOver,
        Victory
    }

    /// <summary>
    /// Central manager for game loop states and score tracking.
    /// Tracks planet progress and drives state transitions for the campaign.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        [Header("State")]
        [SerializeField] private GameState currentState = GameState.Playing;
        [SerializeField] private int currentScore = 0;
        [SerializeField] private int currentPlanet = 1;

        public GameState CurrentState => currentState;
        public int CurrentScore => currentScore;
        public int CurrentPlanet => currentPlanet;

        public event System.Action<int> OnScoreChanged;
        public event System.Action<GameState> OnGameStateChanged;
        /// <summary>Fired when the planet boss is defeated and the warp screen should appear.</summary>
        public event System.Action<int> OnPlanetCleared; // (planetNumber)

        [Header("Combo (GDD 20)")]
        [Tooltip("Seconds without a kill before the combo drops.")]
        [SerializeField] private float comboWindow = 2.4f;
        [Tooltip("Kills needed per multiplier step (x2, x3 ...).")]
        [SerializeField] private int killsPerMultiplier = 8;
        [SerializeField] private int maxMultiplier = 5;

        private int comboKills;
        private float comboTimer;
        private int bestCombo;
        private int totalKills;

        public int ComboKills => comboKills;
        public int ComboMultiplier => Mathf.Clamp(1 + comboKills / Mathf.Max(1, killsPerMultiplier), 1, maxMultiplier);
        public float ComboTimeLeft01 => comboKills > 0 ? Mathf.Clamp01(comboTimer / comboWindow) : 0f;
        public int BestCombo => bestCombo;
        public int TotalKills => totalKills;

        /// <summary>(kills in chain, multiplier). Multiplier 1 with 0 kills means the combo ended.</summary>
        public event System.Action<int, int> OnComboChanged;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            Application.runInBackground = true;
            CameraFitter.Ensure();
            PersistentGlobalLight.EnsureLight();
            // Android/iOS default to 30 FPS; a shooter needs 60 for readable bullets and responsive drag.
#if !UNITY_EDITOR
            if (Application.isMobilePlatform || Application.platform == RuntimePlatform.WebGLPlayer)
            {
                QualitySettings.vSyncCount = 0;
                Application.targetFrameRate = 60;
            }
#endif
            Starfall.UI.PerformanceOverlay.Ensure();
            Starfall.UI.PauseMenu.Ensure();

            // Permanently purge legacy/broken particle systems (pink squares)
            var oldStarfield = GameObject.Find("Starfield");
            if (oldStarfield != null)
            {
                Destroy(oldStarfield);
            }
            foreach (var ps in FindObjectsByType<ParticleSystem>())
            {
                if (ps.gameObject.name.Contains("Starfield"))
                {
                    Destroy(ps.gameObject);
                }
            }
            // Permanently purge orphan pre-placed ExplosionVFX scene objects that detonate on awake
            foreach (var exp in FindObjectsByType<Starfall.VFX.ExplosionVFX>())
            {
                if (exp.transform.parent == null)
                {
                    Destroy(exp.gameObject);
                }
            }
        }

        /// <summary>Awards a kill through the combo system and returns the score actually granted.</summary>
        public int RegisterKill(int baseScore, Vector3 worldPos)
        {
            int before = ComboMultiplier;
            comboKills++;
            totalKills++;
            comboTimer = comboWindow;
            bestCombo = Mathf.Max(bestCombo, comboKills);

            int mult = ComboMultiplier;
            int awarded = baseScore * mult;
            AddScore(awarded);
            OnComboChanged?.Invoke(comboKills, mult);

            if (mult > before)
            {
                Starfall.VFX.VFXKit.Popup(worldPos + Vector3.up * 0.5f, "COMBO x" + mult + "!", new Color(1f, 0.55f, 1f), 4.2f);
                Starfall.Audio.AudioManager.Instance?.PlayPowerUp();
            }
            return awarded;
        }

        /// <summary>Taking damage ends the chain (risk/reward: aggression pays only if you stay clean).</summary>
        public void BreakCombo()
        {
            if (comboKills == 0) return;
            comboKills = 0;
            comboTimer = 0f;
            OnComboChanged?.Invoke(0, 1);
        }

        private void Update()
        {
            if (comboKills <= 0) return;
            comboTimer -= Time.deltaTime;
            if (comboTimer <= 0f) BreakCombo();
        }

        public void AddScore(int amount)
        {
            currentScore += amount;
            OnScoreChanged?.Invoke(currentScore);
        }

        public void SetState(GameState newState)
        {
            currentState = newState;
            OnGameStateChanged?.Invoke(currentState);
        }

        /// <summary>Called by WaveManager when all waves are complete and the boss is triggered.</summary>
        public void EnterBossFight()
        {
            if (currentState != GameState.Playing) return;
            currentState = GameState.BossFight;
            OnGameStateChanged?.Invoke(currentState);
            Debug.Log("[GameManager] BOSS FIGHT STARTED!");
        }

        /// <summary>Called by boss on death – triggers the Mission Complete, unlocks next level, and warps to next sector.</summary>
        public void OnBossDefeated()
        {
            currentState = GameState.MissionComplete;
            OnGameStateChanged?.Invoke(currentState);
            OnPlanetCleared?.Invoke(currentPlanet);
            Debug.Log($"[GameManager] Planet {currentPlanet} cleared!");

            int currentLvl = Starfall.Levels.LevelManager.Instance != null 
                ? Starfall.Levels.LevelManager.Instance.CurrentLevelNumber 
                : currentPlanet;

            // Save completed wave progress and unlock next level immediately
            Starfall.Levels.LevelProgressManager.SaveWaveProgress(currentLvl, 10);
            Starfall.Levels.LevelProgressManager.SaveHighScore(currentLvl, currentScore);
            Starfall.Levels.LevelProgressManager.UnlockNextLevel(currentLvl);

            if (Starfall.Levels.LevelManager.Instance != null)
            {
                Starfall.Levels.LevelManager.Instance.CompleteActiveMission();
            }
            else
            {
                StartCoroutine(FallbackWarpRoutine(currentLvl));
            }
        }

        private System.Collections.IEnumerator FallbackWarpRoutine(int currentLvl)
        {
            yield return new WaitForSeconds(3.5f);
            int nextLvl = currentLvl + 1;
            if (nextLvl <= 4)
            {
                Starfall.Levels.LevelProgressManager.LoadLevel(nextLvl);
            }
            else
            {
                Starfall.Levels.LevelProgressManager.LoadLevelSelect();
            }
        }

        public void OnPlayerDied()
        {
            currentState = GameState.GameOver;
            OnGameStateChanged?.Invoke(currentState);
            Debug.Log("[GameManager] Player destroyed. Game Over!");
        }

        public void RestartGame()
        {
            currentScore = 0;
            currentState = GameState.Playing;
            OnGameStateChanged?.Invoke(currentState);
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }
    }
}
