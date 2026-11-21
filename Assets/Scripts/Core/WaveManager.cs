using UnityEngine;
using System.Collections;
using Starfall.Enemies;
using Starfall.Levels;
using Starfall.VFX;

namespace Starfall.Core
{
    /// <summary>Formation shapes. All are laid out on a spaced lattice by FormationGridController.</summary>
    public enum FormationType
    {
        Grid,
        Random,
        VFormation,
        Pincer,
        Wall,
        Diamond,
        Crescent,
        FlankingColumns,
        Echelon,
        Swarm,
        Checker
    }

    public enum InterwaveEventType
    {
        None,
        BraceForImpact,
        BonusStage,
        TacticalUpgrade
    }

    public enum WaveObjective
    {
        Annihilate,
        Decapitate,
        Intercept,
        Survive
    }

    public enum WaveModifier
    {
        None,
        Overclocked,
        ArmoredFront,
        VolatileHazards
    }

    [System.Serializable]
    public class WaveDefinition
    {
        public string waveName = "Wave";
        [Tooltip("Enemies requested per set. Capped automatically to what fits on the player's screen.")]
        public int enemyCount = 24;
        public int gridRows = 3;
        public int gridColumns = 8;
        public float spawnInterval = 0.05f;
        [Tooltip("Share of heavy (Fighter) bodies among Grunts/Gunners. 0 = all scouts, 1 = all fighters.")]
        [Range(0f, 1f)]
        public float fighterRatio = 0.2f;
        public FormationType formation = FormationType.Grid;
        public FormationType secondaryFormation = FormationType.VFormation;
        [Tooltip("Explicit shape per reinforcement set. Overrides formation/secondaryFormation when set.")]
        public FormationType[] formationSequence;
        [Tooltip("Total reinforcement sets the player must clear to beat this wave.")]
        public int totalSets = 2;
        [Tooltip("If true, WaveManager also sends asteroids through this wave.")]
        public bool spawnAsteroid = false;
        [Tooltip("If true, this wave features a mid-level mini-boss encounter.")]
        public bool isMiniBossWave = false;
        public int miniBossCount = 1;
        public string miniBossPoolKey = "MiniBossGunship";
        public string miniBossDisplayName = "Mini-Boss";
        public Vector3 miniBossSpawnPosition = new Vector3(0f, 7.5f, 0f);
        public string waveAnnouncement = "";
        
        [Tooltip("If true, this is a stealth dodging wave where the player cannot shoot.")]
        public bool isStealthWave = false;
        [Tooltip("Method of stealth wave (0 = Slumbering River, 1 = Drifting Minefield, 2 = Searchlight Patrol)")]
        public int stealthMethod = 0;
        [Tooltip("Special hazard or bonus stage event to trigger in-between this wave and the next.")]
        public InterwaveEventType interwaveEventAfter = InterwaveEventType.None;

        [Header("Tactical Objective & Modifiers")]
        public WaveObjective objective = WaveObjective.Annihilate;
        public WaveModifier modifier = WaveModifier.None;
        [Tooltip("If true, a squad Commander leads the formation. Killing it panics the squad.")]
        public bool hasCommander = false;
        [Tooltip("Target survival time for Survive waves.")]
        public float survivalDuration = 35f;
        [Tooltip("Max breaches allowed in Intercept waves.")]
        public int maxBreachesAllowed = 5;

        [Header("Difficulty Scaling")]
        public float fireIntervalMin = 0.8f;
        public float fireIntervalMax = 1.5f;
        public float formationSwaySpeed = 1.3f;
        public float enemyHealthMultiplier = 1.0f;
        public float projectileSpeedMultiplier = 1.0f;
        public float doubleShotChance = 0.25f;
        public float diveIntervalMin = 3.5f;
        public float diveIntervalMax = 5.5f;
        public float aimedShotChance = 0.2f;

        [Header("Enemy Roles (share of each set)")]
        [Range(0f, 1f)] public float gunnerRatio;
        [Range(0f, 1f)] public float chargerRatio;
        [Range(0f, 1f)] public float bomberRatio;
        [Range(0f, 1f)] public float shielderRatio;
        [Range(0f, 1f)] public float eliteRatio;
        [Range(0f, 1f)] public float blinkerRatio;
        [Tooltip("Shows a one-time hint teaching the role introduced by this wave.")]
        public bool introducesRole;
        public EnemyRole newRole = EnemyRole.Grunt;

        [Header("Bullet Patterns")]
        [Range(0f, 1f)] public float curtainChance;
        [Range(0f, 1f)] public float fanChance;
        [Tooltip("Hard cap on enemy bullets alive at once (fairness). 0 = default.")]
        public int maxEnemyBullets;
        public float specialIntervalMin = 3f;
        public float specialIntervalMax = 4.5f;

        public FormationType GetFormationForSet(int setIndex)
        {
            if (formationSequence != null && formationSequence.Length > 0)
                return formationSequence[setIndex % formationSequence.Length];

            if (setIndex == 0) return formation;
            if (setIndex == 1) return secondaryFormation;
            FormationType[] rotation = { FormationType.Swarm, FormationType.Crescent, FormationType.Wall, FormationType.Diamond };
            return rotation[(setIndex - 2) % rotation.Length];
        }
    }

    /// <summary>
    /// Runs a mission's waves: formation sets, mini-bosses, hazards, interwave events and the boss.
    /// Flow per directive §17: LevelManager → WaveManager → EnemySpawner.
    /// Wave content for the four campaign sectors comes from <see cref="CampaignWaveLibrary"/>.
    /// </summary>
    public class WaveManager : MonoBehaviour
    {
        public static WaveManager Instance { get; private set; }
        public static bool IsStealthWaveActive { get; private set; }
        public static int StealthMethod { get; private set; }
        public static bool StealthAlarmTriggered { get; private set; }

        public static void TriggerStealthAlarm()
        {
            if (StealthAlarmTriggered) return;
            StealthAlarmTriggered = true;
            Starfall.UI.GameplayHUD.Instance?.ShowAnnouncement("ALARM TRIGGERED! EVASION FAILED!", new Color(1f, 0f, 0f));
            
            // Wake up all enemies and execute
            var enemies = FindObjectsByType<EnemyBase>(FindObjectsInactive.Exclude);
            foreach (var e in enemies)
            {
                e.WakeUpAndExecute();
            }
        }

        [Header("Mission Waves Configuration")]
        [SerializeField] private WaveDefinition[] waves;
        [SerializeField] private float restBetweenWaves = 3f;

        [Header("Set Pacing")]
        [Tooltip("Share of a set that triggers overlap / last stand when that few remain (e.g. 0.15 = last 15%).")]
        [Range(0.05f, 0.4f)]
        [SerializeField] private float stragglerShare = 0.15f;
        [Tooltip("Straggler threshold is clamped to this range of enemies.")]
        [SerializeField] private int stragglerMin = 2;
        [SerializeField] private int stragglerMax = 6;
        [Tooltip("If a set still has reinforcements queued, stragglers break ranks and the next set flies in immediately.")]
        [SerializeField] private bool overlapReinforcements = true;
        [Tooltip("On the final set, the last few enemies fire, dive and sway faster.")]
        [SerializeField] private bool lastStandEnabled = true;

        [Header("Boss Encounter")]
        [SerializeField] private bool hasBoss = true;
        [SerializeField] private string bossPoolKey = "InvaderCarrier";
        [SerializeField] private float bossSpawnDelay = 2f;
        [SerializeField] private Vector3 bossSpawnPosition = new Vector3(0f, 8.5f, 0f);

        [Header("References")]
        [SerializeField] private EnemySpawner enemySpawner;

        private int currentWaveIndex = 0;
        private bool planetCleared = false;
        private bool bossTriggered = false;
        private bool briefingShown = false;
        private bool currentWaveCleared = false;
        private GameState lastKnownState = GameState.Playing;
        private int level = 1;
        private LevelDifficultyProfile profile;
        private Coroutine ambientHazardsCor;

        private WaveDefinition activeWaveDef;
        private float waveStartTime;
        private int waveHitsTaken;
        private int maxComboInWave;
        private int enemiesBreachedInWave;
        private bool commanderKilledThisWave;

        public int CurrentWaveNumber => currentWaveIndex + 1;
        public int TotalWaves => waves?.Length ?? 0;
        public int Level => level;
        public LevelDifficultyProfile ActiveProfile => profile;

        public event System.Action<int, int> OnWaveStarted;   // (waveNumber, totalWaves)
        public event System.Action<int>      OnWaveCleared;   // (waveNumber)
        public event System.Action           OnBossFightStarted;
        public event System.Action           OnPlanetCleared;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            CameraFitter.Ensure();

            if (enemySpawner == null)
                enemySpawner = FindAnyObjectByType<EnemySpawner>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnComboChanged -= HandleComboChanged;
                GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
            }
        }

        private void Start()
        {
            if (enemySpawner == null)
                enemySpawner = FindAnyObjectByType<EnemySpawner>();
            if (enemySpawner != null)
                enemySpawner.AutoSpawn = false;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnComboChanged -= HandleComboChanged;
                GameManager.Instance.OnComboChanged += HandleComboChanged;
                GameManager.Instance.OnGameStateChanged -= HandleGameStateChanged;
                GameManager.Instance.OnGameStateChanged += HandleGameStateChanged;
            }

            Starfall.Combat.TacticalPerkManager.Instance?.ResetPerks();
            StartCoroutine(EnsureWavesRunning());
        }

        private void HandleComboChanged(int kills, int multiplier)
        {
            if (kills > maxComboInWave) maxComboInWave = kills;
        }

        private void HandleGameStateChanged(GameState state)
        {
            bool revived = lastKnownState == GameState.GameOver && state == GameState.Playing;
            lastKnownState = state;
            if (revived && !planetCleared && !bossTriggered && waves != null)
                ResumeAfterRevive();
        }

        /// <summary>
        /// Every wave coroutine exits on Game Over, so a revive must restart the loop.
        /// Leftover enemies are cleared and the interrupted wave is replayed (or skipped if it was already cleared).
        /// </summary>
        private void ResumeAfterRevive()
        {
            StopAllCoroutines();
            ambientHazardsCor = null;

            EnemyBase.DespawnAllHostiles(false);
            foreach (var mb in FindObjectsByType<MiniBossGunship>(FindObjectsInactive.Exclude))
            {
                if (mb == null || mb.IsDead) continue;
                if (ObjectPool.Instance != null) ObjectPool.Instance.Despawn(mb.gameObject);
                else Destroy(mb.gameObject);
            }

            if (IsStealthWaveActive)
            {
                IsStealthWaveActive = false;
                var pc = FindAnyObjectByType<Starfall.Player.PlayerCombat>();
                if (pc != null) pc.SetGunsJammed(false);
            }

            if (currentWaveCleared)
            {
                currentWaveIndex++;
                currentWaveCleared = false;
            }

            Debug.Log($"[WaveManager] Player revived - resuming at wave {CurrentWaveNumber}.");
            StartCoroutine(RunWaves());
        }

        public void NotifyCommanderKilled(EnemyBase commander)
        {
            commanderKilledThisWave = true;
            int bonus = 1500 * level;
            GameManager.Instance?.AddScore(bonus);
            Starfall.Audio.AudioManager.Instance?.PlayPowerUp();
            Starfall.UI.GameplayHUD.Instance?.ShowAnnouncement($"<size=28><color=#D47BFF>COMMANDER ELIMINATED!</color></size>\n<size=18><color=#FFD700>FORMATION PANICKED! +{bonus:N0} PTS</color></size>", new Color(0.9f, 0.7f, 1f));
            FormationGridController.Instance?.DemoralizeSquad(3.5f);
        }

        public void NotifyEnemyEscaped(EnemyBase enemy)
        {
            if (activeWaveDef != null && activeWaveDef.objective == WaveObjective.Intercept)
            {
                enemiesBreachedInWave++;
                Starfall.UI.GameplayHUD.Instance?.ShowAnnouncement($"ORBITAL BREACH! ({enemiesBreachedInWave}/{activeWaveDef.maxBreachesAllowed})", new Color(1f, 0.35f, 0.2f));
                Starfall.Audio.AudioManager.Instance?.PlayHazardAlert();
                Starfall.VFX.CameraShake.Instance?.Shake(0.2f, 0.2f);
            }
        }

        private IEnumerator EnsureWavesRunning()
        {
            // Give LevelManager one frame to call LoadMission()
            yield return null;

            if (waves != null && waves.Length > 0 && currentWaveIndex > 0)
                yield break;

            var levelMgr = FindAnyObjectByType<LevelManager>();
            if (levelMgr != null && levelMgr.ActiveMission != null)
                yield break;

            Debug.Log("[WaveManager] No active LevelManager mission detected. Starting campaign waves for this scene.");
            StartFallbackWaves();
        }

        private static int DetectLevel()
        {
            if (LevelManager.Instance != null) return LevelManager.Instance.CurrentLevelNumber;
            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (sceneName.Contains("Level4") || sceneName.Contains("WarTorn") || sceneName.Contains("Wreckage")) return 4;
            if (sceneName.Contains("Level3") || sceneName.Contains("Nebula")) return 3;
            if (sceneName.Contains("Level2") || sceneName.Contains("Asteroid")) return 2;
            return 1;
        }

        public void StartFallbackWaves()
        {
            ConfigureCampaignLevel(DetectLevel());
            BeginRun();
        }

        /// <summary>Loads a MissionData asset. Campaign sectors 1-4 use the tuned wave library.</summary>
        public void LoadMission(MissionData mission)
        {
            if (mission == null) return;

            int lvl = DetectLevel();
            if (lvl >= 1 && lvl <= 4)
            {
                ConfigureCampaignLevel(lvl);
            }
            else
            {
                level = lvl;
                profile = CampaignWaveLibrary.GetProfile(lvl);
                waves = (mission.Waves != null && mission.Waves.Length > 0) ? mission.Waves : CampaignWaveLibrary.Build(1);
                hasBoss = mission.HasBoss;
                bossPoolKey = mission.BossPoolKey;
                bossSpawnDelay = mission.BossSpawnDelay;
                bossSpawnPosition = mission.BossSpawnPosition;
            }

            restBetweenWaves = mission.RestBetweenWaves > 0 ? mission.RestBetweenWaves : 2.5f;
            Debug.Log($"[WaveManager] Loaded Mission '{mission.DisplayName}' ({waves.Length} waves, level {level}).");
            BeginRun();
        }

        private void ConfigureCampaignLevel(int lvl)
        {
            level = Mathf.Clamp(lvl, 1, 4);
            profile = CampaignWaveLibrary.GetProfile(level);
            waves = CampaignWaveLibrary.Build(level);
            hasBoss = true;
            bossPoolKey = profile.bossPoolKey;
            bossSpawnDelay = 2.0f;
            bossSpawnPosition = profile.bossSpawnPosition;
        }

        private void BeginRun()
        {
            StopAllCoroutines();
            ambientHazardsCor = null;

            if (enemySpawner == null)
                enemySpawner = FindAnyObjectByType<EnemySpawner>();
            if (enemySpawner != null)
                enemySpawner.AutoSpawn = false;

            ApplyProfile();
            planetCleared = false;
            bossTriggered = false;
            currentWaveCleared = false;

            int targetWave = LevelProgressManager.TargetStartWave;
            currentWaveIndex = (targetWave >= 1 && targetWave <= waves.Length) ? targetWave - 1 : 0;
            LevelProgressManager.TargetStartWave = 1;

            StartCoroutine(RunWaves());
        }

        private void ApplyProfile()
        {
            EnemyBase.DropChanceMultiplier = profile.dropChanceMultiplier;
            EnemyBase.MaxDropsPerWave = Mathf.Max(1, profile.maxDropsPerWave);

            var ph = FindAnyObjectByType<Starfall.Player.PlayerHealth>();
            if (ph != null)
            {
                ph.SetShieldRegen(profile.shieldRegenDelay, profile.shieldRegenInterval);
                ph.OnDamageTaken -= HandlePlayerDamaged;
                ph.OnDamageTaken += HandlePlayerDamaged;
            }
        }

        private void HandlePlayerDamaged()
        {
            waveHitsTaken++;
        }

        private static bool IsGameOver => GameManager.Instance != null && GameManager.Instance.CurrentState == GameState.GameOver;

        // ------------------------------------------------------------------
        // Main loop
        // ------------------------------------------------------------------

        private IEnumerator RunWaves()
        {
            yield return new WaitForSeconds(0.5f);

            // Pre-mission tactical briefing and interactive onboarding tutorial
            if (currentWaveIndex == 0 && !briefingShown)
            {
                briefingShown = true;
                yield return StartCoroutine(RunBriefingAndTutorialSequence());
            }

            if (profile.ambientHazardMax > 0f)
                ambientHazardsCor = StartCoroutine(RunAmbientHazards());

            while (waves != null && currentWaveIndex < waves.Length)
            {
                if (IsGameOver) yield break;

                var waveDef = waves[currentWaveIndex];
                yield return StartCoroutine(RunSingleWave(waveDef));

                if (IsGameOver) yield break;
                if (CountAliveEnemies() > 0) yield break;

                currentWaveCleared = true;
                LevelProgressManager.SaveWaveProgress(level, CurrentWaveNumber);
                OnWaveCleared?.Invoke(CurrentWaveNumber);
                AwardWaveGrade(waveDef);

                if (CurrentWaveNumber == 5)
                {
                    Starfall.UI.Rive.RiveAnimationManager.Instance?.PlayCheckpointAnimation("CHECKPOINT - WAVE 6 UNLOCKED");
                    Starfall.UI.Rive.RiveAnimationManager.Instance?.PreloadBossAssets();
                    if (!string.IsNullOrEmpty(bossPoolKey)) ObjectPool.Instance?.PrewarmPool(bossPoolKey, 1);
                }

                if (waveDef.interwaveEventAfter == InterwaveEventType.BraceForImpact)
                {
                    yield return new WaitForSeconds(1.2f);
                    string stormMsg = level switch
                    {
                        4 => "BRACE FOR IMPACT: VOLATILE SHRAPNEL BARRAGE!",
                        3 => "BRACE FOR IMPACT: BIO-SPORE ION TEMPEST!",
                        _ => "BRACE FOR IMPACT: ASTEROID STORM!"
                    };
                    yield return StartCoroutine(RunMeteorStorm(stormMsg));
                }
                else if (waveDef.interwaveEventAfter == InterwaveEventType.BonusStage)
                {
                    yield return new WaitForSeconds(1.2f);
                    string bonusMsg = level switch
                    {
                        4 => "BONUS STAGE: SCORCHED FLOTSAM SALVAGE RUN!",
                        3 => "BONUS STAGE: NEBULA CRYSTAL SUPPLY RUN!",
                        2 => "BONUS STAGE: BELT SUPPLY RUN!",
                        _ => "BONUS STAGE: ORBITAL SUPPLY DROP!"
                    };
                    yield return StartCoroutine(RunBonusStage(bonusMsg, 18 + level * 5));
                }
                else if (waveDef.interwaveEventAfter == InterwaveEventType.TacticalUpgrade ||
                         (currentWaveIndex == 2 || (waveDef.isMiniBossWave && currentWaveIndex < waves.Length - 1)))
                {
                    yield return new WaitForSeconds(1.0f);
                    var modal = Starfall.UI.TacticalUpgradeModal.EnsureInstance(Starfall.UI.GameplayHUD.Instance?.transform);
                    if (modal != null)
                    {
                        yield return StartCoroutine(modal.ShowUpgradeSelectionRoutine(null));
                    }
                }

                currentWaveIndex++;
                currentWaveCleared = false;

                if (currentWaveIndex < waves.Length)
                {
                    if (IsGameOver) yield break;
                    yield return new WaitForSeconds(restBetweenWaves);
                }
            }

            if (ambientHazardsCor != null) StopCoroutine(ambientHazardsCor);

            if (hasBoss && !string.IsNullOrEmpty(bossPoolKey))
            {
                Starfall.UI.Rive.RiveAnimationManager.Instance?.PreloadBossAssets();
                ObjectPool.Instance?.PrewarmPool(bossPoolKey, 1);
                yield return new WaitForSeconds(bossSpawnDelay);
                yield return StartCoroutine(TriggerBoss());
            }
            else
            {
                NotifyPlanetCleared();
                if (LevelManager.Instance != null) LevelManager.Instance.CompleteActiveMission();
                if (GameManager.Instance != null) GameManager.Instance.OnBossDefeated();
            }
        }

        private IEnumerator RunBriefingAndTutorialSequence()
        {
            var hudTransform = Starfall.UI.GameplayHUD.Instance != null ? Starfall.UI.GameplayHUD.Instance.transform : null;
            var briefing = Starfall.UI.MissionBriefingModal.EnsureInstance(hudTransform);
            var tutorial = Starfall.UI.FlightTutorialManager.EnsureInstance(hudTransform);

            bool runTutorial = false;

            if (briefing != null)
            {
                yield return StartCoroutine(briefing.ShowBriefingRoutine(level,
                    onCommence: () => { runTutorial = false; },
                    onTutorial: () => { runTutorial = true; }));
            }

            if (runTutorial && tutorial != null)
            {
                yield return StartCoroutine(tutorial.RunTutorialRoutine(null));
            }

            yield return new WaitForSeconds(0.6f);
        }

        private IEnumerator RunSingleWave(WaveDefinition def)
        {
            activeWaveDef = def;
            waveStartTime = Time.time;
            waveHitsTaken = 0;
            maxComboInWave = 0;
            enemiesBreachedInWave = 0;
            commanderKilledThisWave = false;

            IsStealthWaveActive = def.isStealthWave;
            StealthMethod = def.stealthMethod;
            StealthAlarmTriggered = false;

            if (IsStealthWaveActive)
            {
                var pc = FindAnyObjectByType<Starfall.Player.PlayerCombat>();
                if (pc != null) pc.SetGunsJammed(true);
            }

            OnWaveStarted?.Invoke(CurrentWaveNumber, TotalWaves);
            EnemyBase.ResetWavePowerUpCount();

            if (def.isMiniBossWave) Starfall.Audio.AudioManager.Instance?.PlayBossWarningSiren(3);
            else if (def.spawnAsteroid) Starfall.Audio.AudioManager.Instance?.PlayHazardAlert();
            else Starfall.Audio.AudioManager.Instance?.PlayWaveIncoming();

            string objectiveSub = def.objective switch
            {
                WaveObjective.Decapitate => "\n<size=18><color=#D47BFF>[TARGET] OBJECTIVE: ELIMINATE SQUAD COMMANDER</color></size>",
                WaveObjective.Intercept => $"\n<size=18><color=#FF6633>[DEFENSE] OBJECTIVE: INTERCEPT DIVERS (MAX {def.maxBreachesAllowed} BREACHES)</color></size>",
                WaveObjective.Survive => $"\n<size=18><color=#00E5FF>[SURVIVE] OBJECTIVE: SURVIVE {def.survivalDuration:F0} SECONDS</color></size>",
                _ => ""
            };

            string modSub = def.modifier switch
            {
                WaveModifier.Overclocked => "\n<size=16><color=#FF4444>[OVERCLOCK] BULLETS +25% SPEED (SCORE x1.3)</color></size>",
                WaveModifier.ArmoredFront => "\n<size=16><color=#44AAFF>[ARMORED] FRONT ROW HP BOOSTED (SCORE x1.2)</color></size>",
                WaveModifier.VolatileHazards => "\n<size=16><color=#FFAA00>[VOLATILE] HAZARD CHAIN REACTIONS</color></size>",
                _ => ""
            };

            if (def.enemyCount > 0)
            {
                yield return new WaitForSeconds(0.9f);
                string title = !string.IsNullOrEmpty(def.waveAnnouncement) ? def.waveAnnouncement : $"WAVE {CurrentWaveNumber}";
                Color headerColor = def.isMiniBossWave ? new Color(1f, 0.3f, 0.25f) : (def.objective == WaveObjective.Decapitate ? new Color(0.9f, 0.6f, 1f) : new Color(0f, 0.9f, 1f));
                Starfall.UI.GameplayHUD.Instance?.ShowAnnouncement(title + objectiveSub + modSub, headerColor);
            }

            if (def.introducesRole && def.newRole != EnemyRole.Grunt)
                StartCoroutine(ShowRoleHint(def.newRole));

            var grid = FormationGridController.Instance;
            if (grid == null)
            {
                var go = new GameObject("FormationGridController");
                grid = go.AddComponent<FormationGridController>();
            }

            float speedMult = def.projectileSpeedMultiplier > 0f ? def.projectileSpeedMultiplier : 1.0f;
            float hpMult = def.enemyHealthMultiplier > 0f ? def.enemyHealthMultiplier : 1.0f;
            if (def.modifier == WaveModifier.Overclocked) speedMult *= 1.25f;
            else if (def.modifier == WaveModifier.ArmoredFront) hpMult *= 1.35f;

            grid.ApplyWaveParameters(
                def.formationSwaySpeed > 0f ? def.formationSwaySpeed : 1.3f,
                def.fireIntervalMin > 0f ? def.fireIntervalMin : 0.8f,
                def.fireIntervalMax > 0f ? def.fireIntervalMax : 1.5f,
                def.doubleShotChance,
                0.22f,
                speedMult,
                def.diveIntervalMin > 0f ? def.diveIntervalMin : 3.5f,
                def.diveIntervalMax > 0f ? def.diveIntervalMax : 5.5f,
                def.aimedShotChance);
            grid.ReservedTop = def.isMiniBossWave ? 1.35f : 0f;
            grid.ApplyPatternParameters(def.curtainChance, def.fanChance,
                def.maxEnemyBullets > 0 ? def.maxEnemyBullets : 18,
                def.specialIntervalMin > 0f ? def.specialIntervalMin : 3f,
                def.specialIntervalMax > 0f ? def.specialIntervalMax : 4.5f);

            if (enemySpawner != null)
            {
                enemySpawner.SetWaveTier(CurrentWaveNumber, hpMult * 2.5f, speedMult * 1.35f);
                enemySpawner.SetWeights(1f - def.fighterRatio, def.fighterRatio);
            }

            if (def.spawnAsteroid)
                StartCoroutine(SpawnWaveAsteroids());

            if (def.isMiniBossWave && !string.IsNullOrEmpty(def.miniBossPoolKey))
                SpawnMiniBosses(def);

            if (def.objective == WaveObjective.Survive)
            {
                yield return StartCoroutine(RunSurviveWaveSequence(def, grid));
            }
            else if (IsStealthWaveActive && enemySpawner != null)
            {
                yield return StartCoroutine(RunStealthWaveSequence(def));
            }
            else
            {
                int setsToRun = Mathf.Max(1, def.totalSets);
                for (int setIndex = 0; setIndex < setsToRun; setIndex++)
                {
                    if (IsGameOver) yield break;
                    if (def.enemyCount <= 0) break;

                    FormationType shape = def.GetFormationForSet(setIndex);
                    if (setIndex > 0)
                    {
                        Starfall.UI.GameplayHUD.Instance?.ShowAnnouncement($"REINFORCEMENTS {setIndex + 1}/{setsToRun} - {FormationLabel(shape)}", new Color(1f, 0.7f, 0.15f));
                        Starfall.Audio.AudioManager.Instance?.PlayReinforcementAlert();
                        yield return new WaitForSeconds(1.1f);
                    }

                    if (enemySpawner == null)
                    {
                        Debug.LogError("[WaveManager] EnemySpawner is missing from scene! Cannot spawn formation.");
                        yield break;
                    }

                    int spawned = 0;
                    yield return StartCoroutine(enemySpawner.SpawnFormation(shape, def.enemyCount, def, n => spawned = n));
                    float setStart = Time.time;
                    bool hasNextSet = setIndex < setsToRun - 1;

                    yield return new WaitForSeconds(0.4f);
                    yield return StartCoroutine(WaitForSetToFall(grid, spawned, hasNextSet));
                    if (IsGameOver) yield break;

                    AwardSwiftClear(spawned, Time.time - setStart);

                    if (def.objective == WaveObjective.Decapitate && commanderKilledThisWave)
                    {
                        break; // Decapitate objective fulfilled!
                    }
                }
            }

            // Mini-boss waves with no escorts left still need the mini-boss dead.
            float finalOffscreenTimer = 0f;
            while (CountAliveEnemies() > 0)
            {
                if (IsGameOver) break;

                bool anyVisible = false;
                for (int i = 0; i < EnemyBase.Active.Count; i++)
                {
                    var e = EnemyBase.Active[i];
                    if (e != null && !e.IsDead && PlayfieldBounds.IsOnScreen(e.transform.position, 0.4f))
                    {
                        anyVisible = true;
                        break;
                    }
                }

                if (!anyVisible)
                {
                    finalOffscreenTimer += 0.3f;
                    if (finalOffscreenTimer >= 2.5f)
                    {
                        EnemyBase.ForceAllHostilesOnScreenOrDespawn();
                        finalOffscreenTimer = 0f;
                    }
                }
                else
                {
                    finalOffscreenTimer = 0f;
                }

                yield return new WaitForSeconds(0.3f);
            }

            if (IsStealthWaveActive)
            {
                IsStealthWaveActive = false;
                var pc = FindAnyObjectByType<Starfall.Player.PlayerCombat>();
                if (pc != null) pc.SetGunsJammed(false);
            }
        }

        private IEnumerator RunSurviveWaveSequence(WaveDefinition def, FormationGridController grid)
        {
            float duration = def.survivalDuration > 0f ? def.survivalDuration : 35f;
            float elapsed = 0f;
            int setIndex = 0;

            if (enemySpawner != null)
            {
                FormationType firstShape = def.GetFormationForSet(setIndex++);
                yield return StartCoroutine(enemySpawner.SpawnFormation(firstShape, def.enemyCount, def));
            }

            bool warned10 = false;
            bool warned5 = false;

            while (elapsed < duration)
            {
                if (IsGameOver) yield break;
                float dt = Time.deltaTime;
                elapsed += dt;
                float remaining = duration - elapsed;

                if (!warned10 && remaining <= 10f)
                {
                    warned10 = true;
                    Starfall.UI.GameplayHUD.Instance?.ShowAnnouncement("<size=28><color=#00E5FF>10 SECONDS REMAINING!</color></size>", Color.cyan);
                    Starfall.Audio.AudioManager.Instance?.PlayReinforcementAlert(0.8f);
                }
                else if (!warned5 && remaining <= 5f)
                {
                    warned5 = true;
                    Starfall.UI.GameplayHUD.Instance?.ShowAnnouncement("<size=32><color=#FFD700>FINAL 5 SECONDS! HOLD OUT!</color></size>", Color.yellow);
                    Starfall.Audio.AudioManager.Instance?.PlayHazardAlert();
                }

                if (grid != null && grid.AliveCount < 6 && remaining > 6f && enemySpawner != null)
                {
                    grid.ReleaseRemainingMembers();
                    FormationType nextShape = def.GetFormationForSet(setIndex++);
                    yield return StartCoroutine(enemySpawner.SpawnFormation(nextShape, Mathf.RoundToInt(def.enemyCount * 0.75f), def));
                }

                yield return null;
            }

            Starfall.UI.GameplayHUD.Instance?.ShowAnnouncement("<size=30><color=#00FFAA>SURVIVAL COMPLETE! ORBITAL EMP!</color></size>", Color.green);
            Starfall.Audio.AudioManager.Instance?.PlayExplosion();
            Starfall.VFX.CameraShake.Instance?.Shake(0.6f, 0.5f);
            VFXKit.Ring(Vector3.zero, new Color(0f, 1f, 0.9f, 0.9f), 0.5f, 18f, 0.6f);
            yield return new WaitForSeconds(0.4f);
            EnemyBase.DespawnAllHostiles(true);
            yield return new WaitForSeconds(0.8f);
        }

        private IEnumerator RunStealthWaveSequence(WaveDefinition def)
        {
            float durationSeconds = 22f;
            float elapsed = 0f;

            float playLeft = PlayfieldBounds.PlayLeft;
            float playRight = PlayfieldBounds.PlayRight;
            float top = PlayfieldBounds.Top + 1.2f;

            int phase = 0;
            float nextPhaseTime = 0f;

            while (elapsed < durationSeconds)
            {
                if (IsGameOver) yield break;

                // Alternate evasion patterns every ~5 seconds
                if (elapsed >= nextPhaseTime)
                {
                    phase = (phase + 1) % 4;
                    nextPhaseTime = elapsed + 5.2f;
                }

                switch (phase)
                {
                    case 0:
                        // Pattern 1: Angled Cross-Breeze Streams (Top-Left drifting Down-Right)
                        float startX1 = Random.Range(playLeft - 0.2f, (playLeft + playRight) * 0.25f);
                        Vector3 dir1 = new Vector3(Random.Range(0.25f, 0.42f), -1f, 0f).normalized;
                        enemySpawner.SpawnStealthEnemy(new Vector3(startX1, top, 0f), dir1, Random.Range(3.4f, 4.3f));

                        if (Random.value < 0.45f)
                        {
                            float startX1b = Random.Range(0f, playRight);
                            Vector3 dir1b = new Vector3(Random.Range(-0.35f, -0.18f), -1f, 0f).normalized;
                            enemySpawner.SpawnStealthEnemy(new Vector3(startX1b, top, 0f), dir1b, Random.Range(3.0f, 3.8f));
                        }
                        yield return new WaitForSeconds(0.42f);
                        elapsed += 0.42f;
                        break;

                    case 1:
                        // Pattern 2: Criss-Cross Scissors (Intersecting in the center)
                        float leftX = Random.Range(playLeft, -0.6f);
                        float rightX = Random.Range(0.6f, playRight);
                        Vector3 crossRight = new Vector3(0.40f, -1f, 0f).normalized;
                        Vector3 crossLeft = new Vector3(-0.40f, -1f, 0f).normalized;

                        enemySpawner.SpawnStealthEnemy(new Vector3(leftX, top, 0f), crossRight, 3.7f);
                        enemySpawner.SpawnStealthEnemy(new Vector3(rightX, top, 0f), crossLeft, 3.7f);
                        yield return new WaitForSeconds(0.62f);
                        elapsed += 0.62f;
                        break;

                    case 2:
                        // Pattern 3: Slalom Sine Serpent (Curving river requiring rhythmic swerving)
                        float midX = Mathf.Lerp(playLeft + 1.2f, playRight - 1.2f, Mathf.PingPong(elapsed * 0.65f, 1f));
                        enemySpawner.SpawnStealthEnemy(new Vector3(midX, top, 0f), Vector3.down, 3.3f, oscAmp: 1.8f, oscFreq: 2.3f);

                        if (Random.value < 0.45f)
                        {
                            float flankX = Random.value < 0.5f ? playLeft : playRight;
                            Vector3 flankDir = flankX < 0 ? new Vector3(0.22f, -1f, 0f) : new Vector3(-0.22f, -1f, 0f);
                            enemySpawner.SpawnStealthEnemy(new Vector3(flankX, top, 0f), flankDir.normalized, 3.4f);
                        }
                        yield return new WaitForSeconds(0.48f);
                        elapsed += 0.48f;
                        break;

                    case 3:
                        // Pattern 4: Alternating Gates (Clear passage shifts Left -> Right -> Center)
                        int gateType = Mathf.FloorToInt((elapsed * 0.65f) % 3);
                        int count = 6;
                        for (int i = 0; i < count; i++)
                        {
                            float t = (float)i / (count - 1);
                            float gx = Mathf.Lerp(playLeft, playRight, t);

                            bool isGate = (gateType == 0 && t < 0.35f) ||
                                          (gateType == 1 && t > 0.65f) ||
                                          (gateType == 2 && t >= 0.35f && t <= 0.65f);

                            if (!isGate)
                            {
                                enemySpawner.SpawnStealthEnemy(new Vector3(gx, top, 0f), Vector3.down, 3.6f);
                            }
                        }
                        yield return new WaitForSeconds(1.35f);
                        elapsed += 1.35f;
                        break;
                }
            }

            // Drain any remaining off-screen enemies
            float drainTimer = 0f;
            while (CountAliveEnemies() > 0 && drainTimer < 4.0f)
            {
                if (IsGameOver) yield break;
                drainTimer += 0.3f;
                yield return new WaitForSeconds(0.3f);
            }
        }

        /// <summary>
        /// Waits for a formation set to fall. When only a few members remain:
        ///  • another set is queued → the stragglers break ranks on a final raid and we return early,
        ///    so the next formation flies in without the dead time of hunting the last enemy;
        ///  • this is the final set → the survivors enter a red last stand (faster fire, dives, sway).
        /// On return with a queued set, raiders may still be alive; the wave-end check catches them.
        /// </summary>
        private IEnumerator WaitForSetToFall(FormationGridController grid, int spawned, bool hasNextSet)
        {
            int threshold = Mathf.Clamp(Mathf.RoundToInt(spawned * stragglerShare), stragglerMin, stragglerMax);
            // Tiny sets (narrow screens) play out normally.
            bool eligible = grid != null && spawned >= threshold * 2;

            float offscreenStuckTimer = 0f;

            while (CountAliveEnemies() > 0)
            {
                if (IsGameOver) yield break;

                bool anyVisible = false;
                for (int i = 0; i < EnemyBase.Active.Count; i++)
                {
                    var e = EnemyBase.Active[i];
                    if (e != null && !e.IsDead && PlayfieldBounds.IsOnScreen(e.transform.position, 0.4f))
                    {
                        anyVisible = true;
                        break;
                    }
                }

                if (!anyVisible)
                {
                    offscreenStuckTimer += 0.3f;
                    if (offscreenStuckTimer >= 2.5f)
                    {
                        EnemyBase.ForceAllHostilesOnScreenOrDespawn();
                        offscreenStuckTimer = 0f;
                    }
                }
                else
                {
                    offscreenStuckTimer = 0f;
                }

                if (eligible && grid.IsSetup && !grid.IsLastStand)
                {
                    int remaining = grid.AliveCount;
                    if (remaining > 0 && remaining <= threshold)
                    {
                        if (hasNextSet && overlapReinforcements)
                        {
                            grid.ReleaseRemainingMembers();
                            Starfall.VFX.CameraShake.Instance?.Shake(0.15f, 0.12f);
                            yield break;
                        }
                        if (!hasNextSet && lastStandEnabled)
                        {
                            grid.EnterLastStand();
                            Starfall.UI.GameplayHUD.Instance?.ShowAnnouncement("LAST STAND! FINISH THEM!", new Color(1f, 0.3f, 0.2f));
                            Starfall.Audio.AudioManager.Instance?.PlayReinforcementAlert(0.6f);
                        }
                    }
                }

                yield return new WaitForSeconds(0.3f);
            }
        }

        private IEnumerator ShowRoleHint(EnemyRole role)
        {
            yield return new WaitForSeconds(2.6f);
            if (IsGameOver) yield break;
            string hint = EnemyRoleProfile.Hint(role);
            Starfall.UI.GameplayHUD.Instance?.ShowAnnouncement($"NEW THREAT: {hint}", EnemyRoleProfile.Get(role).tint);
            Starfall.UI.Rive.RiveAnimationManager.Instance?.PlayCheckpointAnimation($"NEW THREAT - {EnemyRoleProfile.DisplayName(role)}");
        }

        private static string FormationLabel(FormationType f) => f switch
        {
            FormationType.VFormation => "V-FORMATION CHEVRON",
            FormationType.Pincer => "PINCER CLAWS",
            FormationType.Wall => "ARMORED WALL",
            FormationType.Diamond => "DIAMOND SPEARHEAD",
            FormationType.Crescent => "CRESCENT ENCLOSURE",
            FormationType.FlankingColumns => "FLANKING COLUMNS",
            FormationType.Echelon => "ECHELON BLITZ",
            FormationType.Swarm => "SWARM CLOUD",
            FormationType.Checker => "CHECKERBOARD LINE",
            _ => "BATTLE GRID"
        };

        private void SpawnMiniBosses(WaveDefinition def)
        {
            if (ObjectPool.Instance == null) return;
            int count = Mathf.Max(1, def.miniBossCount);
            float half = PlayfieldBounds.PlayHalfWidth;
            for (int m = 0; m < count; m++)
            {
                float x = count == 1 ? 0f : Mathf.Lerp(-half * 0.6f, half * 0.6f, m / (float)(count - 1));
                Vector3 spawnPos = new Vector3(x, def.miniBossSpawnPosition.y, 0f);
                var spawned = ObjectPool.Instance.Spawn(def.miniBossPoolKey, spawnPos, Quaternion.identity);
                if (spawned == null) continue;

                var gunship = spawned.GetComponent<MiniBossGunship>();
                if (gunship != null)
                {
                    string designation = m == 0 ? "ALPHA" : (m == 1 ? "BETA" : "GAMMA");
                    gunship.SetDisplayName(count > 1 ? $"{def.miniBossDisplayName} {designation}" : def.miniBossDisplayName);
                    // Several gunships share the pressure, so each is a little lighter.
                    float hp = profile.miniBossHealthMultiplier * (count == 1 ? 1f : count == 2 ? 0.7f : 0.55f);
                    gunship.ApplyDifficulty(hp, level, x, count);
                }
            }
        }

        // ------------------------------------------------------------------
        // Rewards for skilful play
        // ------------------------------------------------------------------

        private void AwardSwiftClear(int enemies, float seconds)
        {
            if (enemies <= 0 || GameManager.Instance == null) return;
            float par = 5f + enemies * 0.75f;
            if (seconds >= par) return;
            int bonus = Mathf.RoundToInt((par - seconds) * 40f * level) / 10 * 10;
            if (bonus <= 0) return;
            GameManager.Instance.AddScore(bonus);
            Starfall.UI.GameplayHUD.Instance?.ShowAnnouncement($"SWIFT CLEAR  +{bonus:N0}", new Color(0.5f, 1f, 0.9f));
        }

        private void AwardWaveGrade(WaveDefinition def)
        {
            if (GameManager.Instance == null || def.enemyCount <= 0) return;

            float waveDuration = Time.time - waveStartTime;
            float parTime = 7f + def.enemyCount * 0.7f * Mathf.Max(1, def.totalSets);
            bool isFlawless = waveHitsTaken == 0;
            bool isSwift = waveDuration < parTime;
            bool noBreaches = def.objective != WaveObjective.Intercept || enemiesBreachedInWave == 0;

            string rank;
            Color rankColor;
            int baseBonus;
            int creditReward;
            string comment;

            if (isFlawless && (isSwift || maxComboInWave >= 8) && noBreaches)
            {
                rank = "S";
                rankColor = new Color(1f, 0.85f, 0.15f); // Rich Gold
                baseBonus = 2500 * level;
                creditReward = 20 * level;
                comment = "PERFECT CLEAR - FLAWLESS & SWIFT";
            }
            else if ((isFlawless || isSwift) && noBreaches)
            {
                rank = "A";
                rankColor = new Color(0.2f, 0.9f, 1f); // Neon Cyan
                baseBonus = 1500 * level;
                creditReward = 12 * level;
                comment = isFlawless ? "FLAWLESS CLEAR" : "SWIFT EXECUTION";
            }
            else if (waveHitsTaken <= 2 && (def.objective != WaveObjective.Intercept || enemiesBreachedInWave <= def.maxBreachesAllowed))
            {
                rank = "B";
                rankColor = new Color(0.4f, 1f, 0.5f); // Bright Green
                baseBonus = 800 * level;
                creditReward = 6 * level;
                comment = "SECTOR SECURED";
            }
            else
            {
                rank = "C";
                rankColor = new Color(0.85f, 0.85f, 0.85f); // Silver / White
                baseBonus = 400 * level;
                creditReward = 2 * level;
                comment = "SURVIVED";
            }

            if (def.modifier == WaveModifier.Overclocked) baseBonus = Mathf.RoundToInt(baseBonus * 1.3f);
            else if (def.modifier == WaveModifier.ArmoredFront) baseBonus = Mathf.RoundToInt(baseBonus * 1.2f);

            GameManager.Instance.AddScore(baseBonus);
            if (creditReward > 0)
            {
                LevelProgressManager.AddCredits(creditReward);
            }

            Starfall.Audio.AudioManager.Instance?.PlayPowerUp();
            string hex = ColorUtility.ToHtmlStringRGB(rankColor);
            string gradeCard = $"<size=38><color=#{hex}>RANK {rank}</color></size>\n" +
                               $"<size=22><color=#FFFFFF>{comment}</color></size>\n" +
                               $"<size=17><color=#FFD700>+{baseBonus:N0} PTS</color>   <color=#00E5FF>+{creditReward} CREDITS</color></size>";
            Starfall.UI.GameplayHUD.Instance?.ShowAnnouncement(gradeCard, Color.white);
        }

        private void AwardWaveBonuses(WaveDefinition def)
        {
            AwardWaveGrade(def);
        }

        // ------------------------------------------------------------------
        // Hazards & interwave events
        // ------------------------------------------------------------------

        private GameObject SpawnRock(float xRange, float scaleMin, float scaleMax, float speedMin, float speedMax, float volatileChance)
        {
            if (ObjectPool.Instance == null) return null;
            float spawnX = Random.Range(-xRange, xRange);
            string rockKey = Random.value < volatileChance ? "AsteroidVolatile" : "AsteroidHazard";
            var rock = ObjectPool.Instance.Spawn(rockKey, new Vector3(spawnX, PlayfieldBounds.SpawnY, 0f), Quaternion.identity);
            if (rock == null) return null;

            float scale = Random.Range(scaleMin, scaleMax);
            rock.transform.localScale = Vector3.one * scale;
            Vector2 driftDir = new Vector2(Random.Range(-0.3f, 0.3f), -1f).normalized;
            int hp = scale > 1.15f ? 4 : 2;
            if (rock.TryGetComponent<AsteroidHazard>(out var hazard))
                hazard.Initialize(Random.Range(speedMin, speedMax), driftDir, hp, Random.Range(-100f, 100f));
            return rock;
        }

        private IEnumerator RunAmbientHazards()
        {
            float volatileChance = level >= 4 ? 0.4f : level == 3 ? 0.28f : 0.3f;
            float scaleMin = level == 3 ? 0.5f : 0.65f;
            float scaleMax = level == 3 ? 0.8f : 1.4f;
            while (!IsGameOver && !planetCleared)
            {
                // Ease off while an interwave event or boss is running.
                yield return new WaitForSeconds(Random.Range(profile.ambientHazardMin, profile.ambientHazardMax));
                if (IsGameOver || planetCleared) yield break;
                SpawnRock(PlayfieldBounds.PlayHalfWidth + 0.5f, scaleMin, scaleMax, 3.5f + level * 0.3f, 5.5f + level * 0.4f, volatileChance);
            }
        }

        private IEnumerator SpawnWaveAsteroids()
        {
            int rocks = 2 + level;
            for (int i = 0; i < rocks; i++)
            {
                yield return new WaitForSeconds(Random.Range(3f, 6f));
                if (IsGameOver) yield break;
                SpawnRock(PlayfieldBounds.PlayHalfWidth, 0.7f, 1.3f, 3.2f, 5f, 0.33f);
            }
        }

        private IEnumerator RunMeteorStorm(string alert)
        {
            Starfall.UI.GameplayHUD.Instance?.ShowAnnouncement(alert, new Color(1f, 0.25f, 0.2f));
            Starfall.VFX.CameraShake.Instance?.Shake(0.6f, 0.4f);
            Starfall.Audio.AudioManager.Instance?.PlayHazardAlert();

            float duration = profile.meteorStormDuration > 0f ? profile.meteorStormDuration : 14f;
            float elapsed = 0f;
            float nextSpawnTime = 0f;
            // Storm intensifies over its duration; safe lanes still exist because rocks are sparse early.
            while (elapsed < duration)
            {
                if (IsGameOver) yield break;
                elapsed += Time.deltaTime;
                if (Time.time >= nextSpawnTime)
                {
                    float p = elapsed / duration;
                    nextSpawnTime = Time.time + Mathf.Lerp(0.6f, 0.3f, p) / (0.85f + level * 0.15f);
                    SpawnRock(PlayfieldBounds.PlayHalfWidth + 0.8f, 0.75f, 1.6f, 4.6f + level * 0.3f, 6.8f + level * 0.4f, 0.3f);
                    if (Random.value < 0.2f) Starfall.VFX.CameraShake.Instance?.Shake(0.2f, 0.12f);
                }
                yield return null;
            }

            yield return new WaitForSeconds(1.6f);
            Starfall.UI.GameplayHUD.Instance?.ShowAnnouncement("STORM PASSED!", new Color(0.3f, 1f, 0.5f));
            yield return new WaitForSeconds(1.2f);
        }

        private IEnumerator RunBonusStage(string alert, int totalPods)
        {
            Starfall.UI.GameplayHUD.Instance?.ShowAnnouncement(alert, new Color(1f, 0.85f, 0.1f));
            Starfall.Audio.AudioManager.Instance?.PlayPowerUp();

            int spawnedCount = 0;
            float half = PlayfieldBounds.PlayHalfWidth - 0.4f;
            while (spawnedCount < totalPods)
            {
                if (IsGameOver) yield break;
                int batch = (totalPods - spawnedCount >= 2 && Random.value < 0.45f) ? 2 : 1;
                for (int b = 0; b < batch && spawnedCount < totalPods; b++)
                {
                    if (ObjectPool.Instance != null)
                    {
                        // Pods in the same batch start on opposite halves so they never overlap.
                        float x = b == 0 ? Random.Range(-half, 0f) : Random.Range(0.6f, half);
                        var pod = ObjectPool.Instance.Spawn("ScoutEnemy", new Vector3(x, PlayfieldBounds.SpawnY, 0f), Quaternion.identity);
                        if (pod != null && pod.TryGetComponent<EnemyBase>(out var enemy))
                            enemy.SetAsBonusPod(true);
                    }
                    spawnedCount++;
                }
                yield return new WaitForSeconds(0.55f);
            }

            yield return new WaitForSeconds(1.5f);
            while (CountAliveBonusPods() > 0)
            {
                if (IsGameOver) yield break;
                yield return new WaitForSeconds(0.4f);
            }

            Starfall.UI.GameplayHUD.Instance?.ShowAnnouncement("BONUS COMPLETE!", new Color(1f, 0.9f, 0.2f));
            yield return new WaitForSeconds(1.2f);
        }

        private static int CountAliveBonusPods()
        {
            int count = 0;
            var list = EnemyBase.Active;
            for (int i = 0; i < list.Count; i++)
                if (list[i] != null && !list[i].IsDead && list[i].IsBonusPod) count++;
            return count;
        }

        private IEnumerator TriggerBoss()
        {
            bossTriggered = true;
            OnBossFightStarted?.Invoke();
            Starfall.Audio.AudioManager.Instance?.PlayBossWarningSiren(4);
            Starfall.UI.Rive.RiveAnimationManager.Instance?.PlayBossWarningFrame();

            if (GameManager.Instance != null)
                GameManager.Instance.EnterBossFight();

            if (ObjectPool.Instance != null)
                ObjectPool.Instance.Spawn(bossPoolKey, bossSpawnPosition, Quaternion.identity);
            else
                Debug.LogWarning("[WaveManager] ObjectPool not found – boss could not be spawned.");

            yield return null;
        }

        private int CountAliveEnemies()
        {
            using var __perf = Starfall.Core.PerfMarkers.WaveBookkeeping.Auto();
            int count = EnemyBase.ActiveHostileCount;
            // Scene-wide searches are only needed to catch a lone (mini-)boss.
            if (count > 0) return count;

            foreach (var mb in FindObjectsByType<MiniBossGunship>(FindObjectsInactive.Exclude))
                if (mb != null && !mb.IsDead) count++;
            foreach (var b in FindObjectsByType<InvaderCarrier>(FindObjectsInactive.Exclude))
                if (b != null && !b.IsDead) count++;
            foreach (var b in FindObjectsByType<PlasmaDreadnought>(FindObjectsInactive.Exclude))
                if (b != null && !b.IsDead) count++;
            foreach (var b in FindObjectsByType<HiveQueenBoss>(FindObjectsInactive.Exclude))
                if (b != null && !b.IsDead) count++;
            foreach (var b in FindObjectsByType<InfernoColossusBoss>(FindObjectsInactive.Exclude))
                if (b != null && !b.IsDead) count++;

            return count;
        }

        /// <summary>Called by GameManager.OnPlanetCleared (boss defeated).</summary>
        public void NotifyPlanetCleared()
        {
            if (planetCleared) return;
            planetCleared = true;
            OnPlanetCleared?.Invoke();
        }
    }
}
