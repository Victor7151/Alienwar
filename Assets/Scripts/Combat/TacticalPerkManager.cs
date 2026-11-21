using System;
using System.Collections.Generic;
using UnityEngine;
using Starfall.Core;
using Starfall.Player;
using Starfall.VFX;

namespace Starfall.Combat
{
    public enum TacticalPerkType
    {
        PlasmaOverdrive,     // Primary weapon fire rate +20%, projectile speed +25%
        WingmanEscort,       // Summons an automated persistent combat wingman drone
        ShieldCapacitor,     // +1 Max Shield, -1s regen delay, 35% faster regen, instant refill
        HomingSwarm,         // Launches twin tracking micro-missiles on every attack volley
        NaniteArmor,         // +1 Max Hull, instantly restores 2 hull points, 15% damage mitigation
        MagneticScavenger,   // Magnets powerups and star credits from 3.5x distance, +20% score bonus
        StarfallOvercharge,  // Starfall Bomb damage +50%, awards +1 immediate bomb
        HyperThrusters       // Player ship speed +25%, dash cooldown reduced
    }

    [System.Serializable]
    public class TacticalPerkDef
    {
        public TacticalPerkType type;
        public string title;
        public string badge;
        public string description;
        public Color badgeColor;
        public int maxStacks = 2;

        public TacticalPerkDef(TacticalPerkType t, string title, string badge, string desc, Color color, int max = 2)
        {
            this.type = t;
            this.title = title;
            this.badge = badge;
            this.description = desc;
            this.badgeColor = color;
            this.maxStacks = max;
        }
    }

    /// <summary>
    /// Manages in-run roguelike tactical upgrade choices awarded at checkpoint waves.
    /// Perks persist for the duration of the sector and empower the player's combat capabilities.
    /// </summary>
    public class TacticalPerkManager : MonoBehaviour
    {
        public static TacticalPerkManager Instance { get; private set; }

        private static readonly Dictionary<TacticalPerkType, TacticalPerkDef> catalog = new()
        {
            {
                TacticalPerkType.PlasmaOverdrive,
                new TacticalPerkDef(TacticalPerkType.PlasmaOverdrive, "Plasma Overdrive", "OFFENSE",
                    "+20% Primary fire rate & +25% projectile velocity.",
                    new Color(1f, 0.35f, 0.2f), 2)
            },
            {
                TacticalPerkType.WingmanEscort,
                new TacticalPerkDef(TacticalPerkType.WingmanEscort, "Wingman Escort Drone", "DRONE",
                    "Deploys an autonomous companion drone with twin autocannons.",
                    new Color(0.2f, 0.9f, 1f), 1)
            },
            {
                TacticalPerkType.ShieldCapacitor,
                new TacticalPerkDef(TacticalPerkType.ShieldCapacitor, "Shield Capacitor", "DEFENSE",
                    "+1 Max Shield, -1.0s recharge delay, and instant shield restore.",
                    new Color(0.3f, 0.65f, 1f), 2)
            },
            {
                TacticalPerkType.HomingSwarm,
                new TacticalPerkDef(TacticalPerkType.HomingSwarm, "Homing Swarm Pod", "MISSILE",
                    "Fires 2 micro-homing missiles on every attack volley.",
                    new Color(0.85f, 0.45f, 1f), 1)
            },
            {
                TacticalPerkType.NaniteArmor,
                new TacticalPerkDef(TacticalPerkType.NaniteArmor, "Nanite Hull Plating", "ARMOR",
                    "+1 Max Hull, heals 2 hull integrity, and reduces incoming damage.",
                    new Color(0.3f, 1f, 0.5f), 2)
            },
            {
                TacticalPerkType.MagneticScavenger,
                new TacticalPerkDef(TacticalPerkType.MagneticScavenger, "Vortex Harvester", "UTILITY",
                    "Magnets items from 3.5x distance and grants +20% score on kills.",
                    new Color(1f, 0.85f, 0.2f), 1)
            },
            {
                TacticalPerkType.StarfallOvercharge,
                new TacticalPerkDef(TacticalPerkType.StarfallOvercharge, "Starfall Overcharge", "BOMB",
                    "+50% Starfall Bomb shockwave damage and grants +1 extra bomb.",
                    new Color(1f, 0.2f, 0.4f), 2)
            },
            {
                TacticalPerkType.HyperThrusters,
                new TacticalPerkDef(TacticalPerkType.HyperThrusters, "Hyper-Thrusters", "MOBILITY",
                    "+25% ship maneuverability speed and extended evasion i-frames.",
                    new Color(0.2f, 1f, 0.85f), 1)
            }
        };

        private readonly Dictionary<TacticalPerkType, int> activePerks = new();

        public event Action<TacticalPerkDef, int> OnPerkAcquired;

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

        public static TacticalPerkDef GetDef(TacticalPerkType type)
        {
            return catalog.TryGetValue(type, out var def) ? def : null;
        }

        public int GetStackCount(TacticalPerkType type)
        {
            return activePerks.TryGetValue(type, out int count) ? count : 0;
        }

        public bool HasPerk(TacticalPerkType type) => GetStackCount(type) > 0;

        /// <summary>
        /// Selects 3 random perks that have not yet reached their maximum stack limit.
        /// </summary>
        public List<TacticalPerkDef> RollUpgradeChoices(int count = 3)
        {
            var eligible = new List<TacticalPerkDef>();
            foreach (var kvp in catalog)
            {
                int currentStacks = GetStackCount(kvp.Key);
                if (currentStacks < kvp.Value.maxStacks)
                {
                    eligible.Add(kvp.Value);
                }
            }

            var choices = new List<TacticalPerkDef>();
            while (choices.Count < count && eligible.Count > 0)
            {
                int idx = UnityEngine.Random.Range(0, eligible.Count);
                choices.Add(eligible[idx]);
                eligible.RemoveAt(idx);
            }
            return choices;
        }

        /// <summary>
        /// Applies the selected tactical perk directly to the player's active systems.
        /// </summary>
        public void ApplyPerk(TacticalPerkType type)
        {
            int current = GetStackCount(type);
            activePerks[type] = current + 1;
            var def = GetDef(type);

            var playerCombat = FindAnyObjectByType<PlayerCombat>();
            var playerHealth = FindAnyObjectByType<PlayerHealth>();
            var playerController = FindAnyObjectByType<PlayerController>();

            switch (type)
            {
                case TacticalPerkType.PlasmaOverdrive:
                    // PlayerCombat reads fire rate multiplier
                    break;

                case TacticalPerkType.WingmanEscort:
                    playerCombat?.DeployWingmanDrones(9999f); // Persistent for entire run
                    break;

                case TacticalPerkType.ShieldCapacitor:
                    if (playerHealth != null)
                    {
                        playerHealth.RepairShield(99);
                        playerHealth.SetShieldRegen(
                            Mathf.Max(1.0f, 3.5f - (current + 1) * 0.8f),
                            Mathf.Max(0.4f, 1.0f - (current + 1) * 0.25f)
                        );
                    }
                    break;

                case TacticalPerkType.HomingSwarm:
                    playerCombat?.ApplyHomingMissiles(9999f); // Persistent micro-missiles
                    break;

                case TacticalPerkType.NaniteArmor:
                    playerHealth?.RepairHull(2);
                    break;

                case TacticalPerkType.MagneticScavenger:
                    playerCombat?.ApplyMagnet(9999f);
                    break;

                case TacticalPerkType.StarfallOvercharge:
                    playerCombat?.AddBombs(1);
                    break;

                case TacticalPerkType.HyperThrusters:
                    // PlayerController reads speed multiplier
                    break;
            }

            if (playerHealth != null)
            {
                VFXKit.Ring(playerHealth.transform.position, def?.badgeColor ?? Color.cyan, 0.4f, 2.2f, 0.35f);
                VFXKit.Popup(playerHealth.transform.position + Vector3.up * 0.8f, $"UPGRADE: {def?.title.ToUpperInvariant()}!", def?.badgeColor ?? Color.yellow, 3.8f);
            }

            Starfall.Audio.AudioManager.Instance?.PlayPowerUp();
            OnPerkAcquired?.Invoke(def, activePerks[type]);
            Debug.Log($"<color=lime>[TacticalPerkManager] Acquired {type} (Stack {activePerks[type]})</color>");
        }

        public void ResetPerks()
        {
            activePerks.Clear();
        }

        // Multiplier helpers for Player systems
        public float FireRateMultiplier => 1f + GetStackCount(TacticalPerkType.PlasmaOverdrive) * 0.20f;
        public float MoveSpeedMultiplier => 1f + GetStackCount(TacticalPerkType.HyperThrusters) * 0.25f;
        public float ScoreMultiplier => 1f + GetStackCount(TacticalPerkType.MagneticScavenger) * 0.20f;
        public float BombDamageMultiplier => 1f + GetStackCount(TacticalPerkType.StarfallOvercharge) * 0.50f;
        public float DamageMitigation => GetStackCount(TacticalPerkType.NaniteArmor) * 0.15f;
    }
}
