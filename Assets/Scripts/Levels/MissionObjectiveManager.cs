using System;
using System.Collections.Generic;
using UnityEngine;
using Starfall.Core;
using Starfall.Combat;
using Starfall.Enemies;
using Starfall.Player;
using Starfall.Audio;

namespace Starfall.Levels
{
    public enum ObjectiveType
    {
        ClearSectorBoss,
        EliminateCommanders,
        PreserveLives,
        DestroyAsteroids,
        CollectPowerUps,
        EliminateBlinkers,
        DeployOrdnanceOrAbility,
        MaintainWeaponLevel,
        ReachScoreTarget
    }

    [Serializable]
    public class MissionObjective
    {
        public string id;
        public string title;
        public string description;
        public ObjectiveType type;
        public int targetCount;
        public int currentCount;
        public bool isCompleted;
        public int starCreditsReward = 100;

        public string GetStatusString()
        {
            if (isCompleted)
            {
                return $"<color=#00FF88><b>[OK]</b> {title}</color> <size=12><color=#FFD700>+{starCreditsReward} CR</color></size>";
            }

            if (targetCount > 1)
            {
                return $"<color=#E0F0FF>[ ] {title} <color=#00E5FF>({currentCount}/{targetCount})</color></color>";
            }

            return $"<color=#E0F0FF>[ ] {title}</color>";
        }
    }

    /// <summary>
    /// Tracks level mission objectives across all 4 sectors (and future sectors).
    /// Enforces smart tactical gameplay: target prioritization, asteroid demolition,
    /// power-up acquisition, ability/bomb ordnance usage, and warship shop upgrades.
    /// </summary>
    public class MissionObjectiveManager : MonoBehaviour
    {
        public static MissionObjectiveManager Instance { get; private set; }

        [Header("Active Level Objectives")]
        [SerializeField] private List<MissionObjective> activeObjectives = new List<MissionObjective>();

        public IReadOnlyList<MissionObjective> ActiveObjectives => activeObjectives;
        public int CompletedObjectivesCount
        {
            get
            {
                int c = 0;
                for (int i = 0; i < activeObjectives.Count; i++)
                {
                    if (activeObjectives[i].isCompleted) c++;
                }
                return c;
            }
        }

        public event Action<MissionObjective> OnObjectiveUpdated;
        public event Action<MissionObjective> OnObjectiveCompleted;
        public event Action<int, int> OnAllObjectivesEvaluated; // completedCount, totalCreditsAwarded

        private int currentLevelNumber = 1;
        private bool isSubscribed = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            SubscribeEvents();
        }

        private void OnDisable()
        {
            UnsubscribeEvents();
        }

        private void Start()
        {
            currentLevelNumber = LevelManager.Instance != null ? LevelManager.Instance.CurrentLevelNumber : 1;
            InitializeObjectivesForLevel(currentLevelNumber);
        }

        public static MissionObjectiveManager EnsureInstance(Transform parent = null)
        {
            if (Instance != null) return Instance;
            var existing = FindAnyObjectByType<MissionObjectiveManager>();
            if (existing != null)
            {
                Instance = existing;
                return existing;
            }

            var go = new GameObject("MissionObjectiveManager");
            if (parent != null) go.transform.SetParent(parent, false);
            return go.AddComponent<MissionObjectiveManager>();
        }

        public void InitializeObjectivesForLevel(int level)
        {
            currentLevelNumber = level;
            activeObjectives = GenerateObjectives(level);

            // Check if any objectives start pre-fulfilled (e.g. score or weapon level)
            EvaluateRealtimeProgress();
        }

        public static List<MissionObjective> GenerateObjectives(int level)
        {
            var list = new List<MissionObjective>();

            switch (level)
            {
                case 2: // SECTOR 02: ASTEROID BELT
                    list.Add(new MissionObjective
                    {
                        id = "L2_PRIMARY",
                        title = "Neutralize Plasma Dreadnought",
                        description = "Clear 10 combat waves and destroy the alien flagship.",
                        type = ObjectiveType.ClearSectorBoss,
                        targetCount = 1,
                        starCreditsReward = 200
                    });
                    list.Add(new MissionObjective
                    {
                        id = "L2_ASTEROIDS",
                        title = "Clear 12 Asteroid Hazards",
                        description = "Blast asteroid hazards to open a safe flight channel.",
                        type = ObjectiveType.DestroyAsteroids,
                        targetCount = 12,
                        starCreditsReward = 150
                    });
                    list.Add(new MissionObjective
                    {
                        id = "L2_POWERUPS",
                        title = "Collect 4 Power-Up Pods",
                        description = "Reclaim shields, bombs, or weapon upgrades drifting in the belt.",
                        type = ObjectiveType.CollectPowerUps,
                        targetCount = 4,
                        starCreditsReward = 150
                    });
                    break;

                case 3: // SECTOR 03: DEEP NEBULA
                    list.Add(new MissionObjective
                    {
                        id = "L3_PRIMARY",
                        title = "Eliminate Hive Queen Leviathan",
                        description = "Clear 10 combat waves and slay the bio-mechanical leviathan.",
                        type = ObjectiveType.ClearSectorBoss,
                        targetCount = 1,
                        starCreditsReward = 250
                    });
                    list.Add(new MissionObjective
                    {
                        id = "L3_BLINKERS",
                        title = "Intercept 8 Blinker Scouts",
                        description = "Eliminate teleporting phase scouts before they snap aimed fire.",
                        type = ObjectiveType.EliminateBlinkers,
                        targetCount = 8,
                        starCreditsReward = 150
                    });
                    list.Add(new MissionObjective
                    {
                        id = "L3_ORDNANCE",
                        title = "Deploy Bomb or Ability 3 Times",
                        description = "Use tactical Bombs or Warship Special Abilities to breach bullet curtains.",
                        type = ObjectiveType.DeployOrdnanceOrAbility,
                        targetCount = 3,
                        starCreditsReward = 150
                    });
                    break;

                case 4: // SECTOR 04: WAR-TORN WRECKAGE
                    list.Add(new MissionObjective
                    {
                        id = "L4_PRIMARY",
                        title = "Destroy Inferno Colossus",
                        description = "Clear 10 combat waves and annihilate the final juggernaut.",
                        type = ObjectiveType.ClearSectorBoss,
                        targetCount = 1,
                        starCreditsReward = 300
                    });
                    list.Add(new MissionObjective
                    {
                        id = "L4_WEAPON_LV3",
                        title = "Attain Weapon Level 3+",
                        description = "Upgrade main warship cannons to Level 3 or higher using weapon pods.",
                        type = ObjectiveType.MaintainWeaponLevel,
                        targetCount = 3,
                        starCreditsReward = 175
                    });
                    list.Add(new MissionObjective
                    {
                        id = "L4_SCORE_25K",
                        title = "Score 25,000 Tactical Points",
                        description = "Chain kills and maintain combos to surpass 25,000 mission score.",
                        type = ObjectiveType.ReachScoreTarget,
                        targetCount = 25000,
                        starCreditsReward = 200
                    });
                    break;

                default: // SECTOR 01: EARTH ORBIT
                    list.Add(new MissionObjective
                    {
                        id = "L1_PRIMARY",
                        title = "Destroy Invader Carrier",
                        description = "Intercept 10 hostile waves and defeat the mothership.",
                        type = ObjectiveType.ClearSectorBoss,
                        targetCount = 1,
                        starCreditsReward = 150
                    });
                    list.Add(new MissionObjective
                    {
                        id = "L1_COMMANDERS",
                        title = "Eliminate 2 Squad Commanders",
                        description = "Target squad commanders leading chevron assault squads.",
                        type = ObjectiveType.EliminateCommanders,
                        targetCount = 2,
                        starCreditsReward = 120
                    });
                    list.Add(new MissionObjective
                    {
                        id = "L1_LIVES",
                        title = "Survive with 3+ Lives",
                        description = "Evade incoming bullet fire and finish the sector with at least 3 lives.",
                        type = ObjectiveType.PreserveLives,
                        targetCount = 3,
                        starCreditsReward = 130
                    });
                    break;
            }

            return list;
        }

        private void SubscribeEvents()
        {
            if (isSubscribed) return;
            isSubscribed = true;

            EnemyBase.OnEnemyKilled += HandleEnemyKilled;
            AsteroidHazard.OnAsteroidDestroyed += HandleAsteroidDestroyed;
            PowerUp.OnPowerUpCollected += HandlePowerUpCollected;
            PlayerCombat.OnBombUsed += HandleOrdnanceUsed;
            PlayerCombat.OnSpecialAbilityUsed += HandleOrdnanceUsed;
            BossEvents.OnBossDefeated += HandleBossDefeated;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnScoreChanged += HandleScoreChanged;
            }
        }

        private void UnsubscribeEvents()
        {
            if (!isSubscribed) return;
            isSubscribed = false;

            EnemyBase.OnEnemyKilled -= HandleEnemyKilled;
            AsteroidHazard.OnAsteroidDestroyed -= HandleAsteroidDestroyed;
            PowerUp.OnPowerUpCollected -= HandlePowerUpCollected;
            PlayerCombat.OnBombUsed -= HandleOrdnanceUsed;
            PlayerCombat.OnSpecialAbilityUsed -= HandleOrdnanceUsed;
            BossEvents.OnBossDefeated -= HandleBossDefeated;

            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnScoreChanged -= HandleScoreChanged;
            }
        }

        private void HandleEnemyKilled(EnemyBase enemy)
        {
            if (enemy == null) return;

            if (enemy.Role == EnemyRole.Commander)
            {
                IncrementObjective(ObjectiveType.EliminateCommanders, 1);
            }
            else if (enemy.Role == EnemyRole.Blinker)
            {
                IncrementObjective(ObjectiveType.EliminateBlinkers, 1);
            }
        }

        private void HandleAsteroidDestroyed(AsteroidHazard hazard)
        {
            IncrementObjective(ObjectiveType.DestroyAsteroids, 1);
        }

        private void HandlePowerUpCollected(PowerUpType type)
        {
            IncrementObjective(ObjectiveType.CollectPowerUps, 1);

            // Also check weapon level right away if weapon upgrade picked up
            if (type == PowerUpType.WeaponUpgrade)
            {
                EvaluateWeaponLevel();
            }
        }

        private void HandleOrdnanceUsed()
        {
            IncrementObjective(ObjectiveType.DeployOrdnanceOrAbility, 1);
        }

        private void HandleBossDefeated(string bossName)
        {
            CompleteObjectiveByType(ObjectiveType.ClearSectorBoss);
        }

        private void HandleScoreChanged(int score)
        {
            for (int i = 0; i < activeObjectives.Count; i++)
            {
                var obj = activeObjectives[i];
                if (obj.type == ObjectiveType.ReachScoreTarget && !obj.isCompleted)
                {
                    obj.currentCount = score;
                    if (obj.currentCount >= obj.targetCount)
                    {
                        MarkObjectiveComplete(obj);
                    }
                    else
                    {
                        OnObjectiveUpdated?.Invoke(obj);
                    }
                }
            }
        }

        private void EvaluateWeaponLevel()
        {
            var combat = FindAnyObjectByType<PlayerCombat>();
            if (combat == null) return;

            for (int i = 0; i < activeObjectives.Count; i++)
            {
                var obj = activeObjectives[i];
                if (obj.type == ObjectiveType.MaintainWeaponLevel && !obj.isCompleted)
                {
                    obj.currentCount = combat.WeaponLevel;
                    if (obj.currentCount >= obj.targetCount)
                    {
                        MarkObjectiveComplete(obj);
                    }
                    else
                    {
                        OnObjectiveUpdated?.Invoke(obj);
                    }
                }
            }
        }

        private void EvaluateRealtimeProgress()
        {
            if (GameManager.Instance != null)
            {
                HandleScoreChanged(GameManager.Instance.CurrentScore);
            }
            EvaluateWeaponLevel();
        }

        private void IncrementObjective(ObjectiveType type, int amount)
        {
            for (int i = 0; i < activeObjectives.Count; i++)
            {
                var obj = activeObjectives[i];
                if (obj.type == type && !obj.isCompleted)
                {
                    obj.currentCount += amount;
                    if (obj.currentCount >= obj.targetCount)
                    {
                        MarkObjectiveComplete(obj);
                    }
                    else
                    {
                        OnObjectiveUpdated?.Invoke(obj);
                    }
                }
            }
        }

        private void CompleteObjectiveByType(ObjectiveType type)
        {
            for (int i = 0; i < activeObjectives.Count; i++)
            {
                var obj = activeObjectives[i];
                if (obj.type == type && !obj.isCompleted)
                {
                    obj.currentCount = obj.targetCount;
                    MarkObjectiveComplete(obj);
                }
            }
        }

        private void MarkObjectiveComplete(MissionObjective obj)
        {
            if (obj.isCompleted) return;
            obj.isCompleted = true;
            obj.currentCount = Mathf.Max(obj.currentCount, obj.targetCount);

            // Award immediate bounty credits
            if (LevelManager.Instance != null && obj.starCreditsReward > 0)
            {
                LevelManager.Instance.AddStarCredits(obj.starCreditsReward);
            }

            // Audio + Announcement
            AudioManager.Instance?.PlayPowerUp();
            Starfall.UI.GameplayHUD.Instance?.ShowAnnouncement(
                $"MISSION OBJECTIVE COMPLETE!\n<color=#FFD700>+{obj.starCreditsReward} STAR CREDITS</color>",
                new Color(0.2f, 1f, 0.5f)
            );

            // Save persistent completion
            SaveObjectiveCompletion(currentLevelNumber, obj.id);

            OnObjectiveUpdated?.Invoke(obj);
            OnObjectiveCompleted?.Invoke(obj);

            Debug.Log($"<color=green>[MissionObjectiveManager] Completed Objective: {obj.title} (+{obj.starCreditsReward} Credits)</color>");
        }

        /// <summary>
        /// Evaluates end-of-sector objectives (such as preserving lives or weapon level)
        /// when the sector is cleared and awards bonus credits.
        /// </summary>
        public int EvaluateEndOfMissionObjectives(PlayerHealth health, PlayerCombat combat)
        {
            int bonusAwarded = 0;

            for (int i = 0; i < activeObjectives.Count; i++)
            {
                var obj = activeObjectives[i];
                if (obj.isCompleted) continue;

                if (obj.type == ObjectiveType.PreserveLives)
                {
                    int lives = health != null ? health.CurrentLives : 1;
                    obj.currentCount = lives;
                    if (lives >= obj.targetCount)
                    {
                        MarkObjectiveComplete(obj);
                        bonusAwarded += obj.starCreditsReward;
                    }
                }
                else if (obj.type == ObjectiveType.MaintainWeaponLevel)
                {
                    int wpn = combat != null ? combat.WeaponLevel : 1;
                    obj.currentCount = wpn;
                    if (wpn >= obj.targetCount)
                    {
                        MarkObjectiveComplete(obj);
                        bonusAwarded += obj.starCreditsReward;
                    }
                }
            }

            OnAllObjectivesEvaluated?.Invoke(CompletedObjectivesCount, bonusAwarded);
            return bonusAwarded;
        }

        private static void SaveObjectiveCompletion(int level, string objectiveId)
        {
            PlayerPrefs.SetInt($"Starfall_Obj_{level}_{objectiveId}", 1);
            PlayerPrefs.Save();
        }

        public static bool IsObjectiveCompleted(int level, string objectiveId)
        {
            return PlayerPrefs.GetInt($"Starfall_Obj_{level}_{objectiveId}", 0) == 1;
        }
    }
}
