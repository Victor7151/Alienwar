using System;
using System.Collections.Generic;
using UnityEngine;
using Starfall.Core;

namespace Starfall.Commander
{
    public enum SquadronUnitId
    {
        ViperSwarm,       // Cost: 2 EN - Trio of fast interceptors
        TitanGunship,     // Cost: 4 EN - Armored frontline brawler with dual plasma
        SpecterSniper,    // Cost: 5 EN - Stealth long-range piercing railgun
        TempestCruiser,   // Cost: 6 EN - Energy shield projector and EMP discharge
        PhoenixColossus   // Cost: 8 EN - Super-heavy dreadnought
    }

    [Serializable]
    public class SquadronCardDef
    {
        public SquadronUnitId id;
        public string displayName;
        public int energyCost;
        public int spawnCount;           // Number of ships in the squad (e.g. 3 for swarm)
        public int maxHealth;            // Health per individual ship
        public float moveSpeed;          // Forward cruising velocity
        public int attackDamage;         // Damage per projectile
        public float fireRate;           // Seconds between attacks
        public float attackRange;        // Detection/attack distance
        public WarshipClass shipClass;   // Sprite styling class
        public Color themeColor;         // Hull/plasma accent
        public string description;       // Tactical summary

        public SquadronCardDef(
            SquadronUnitId id,
            string displayName,
            int energyCost,
            int spawnCount,
            int maxHealth,
            float moveSpeed,
            int attackDamage,
            float fireRate,
            float attackRange,
            WarshipClass shipClass,
            Color themeColor,
            string description)
        {
            this.id = id;
            this.displayName = displayName;
            this.energyCost = energyCost;
            this.spawnCount = spawnCount;
            this.maxHealth = maxHealth;
            this.moveSpeed = moveSpeed;
            this.attackDamage = attackDamage;
            this.fireRate = fireRate;
            this.attackRange = attackRange;
            this.shipClass = shipClass;
            this.themeColor = themeColor;
            this.description = description;
        }
    }

    public static class CommanderCatalog
    {
        private static readonly Dictionary<SquadronUnitId, SquadronCardDef> catalog = new()
        {
            [SquadronUnitId.ViperSwarm] = new SquadronCardDef(
                SquadronUnitId.ViperSwarm,
                "VIPER SWARM",
                2,
                3,
                6,
                5.8f,
                2,
                0.32f,
                4.5f,
                WarshipClass.Viper,
                new Color(0.1f, 0.85f, 1f, 1f),
                "Fast trio of strike interceptors. Overwhelms single-target defenses."
            ),
            [SquadronUnitId.TitanGunship] = new SquadronCardDef(
                SquadronUnitId.TitanGunship,
                "TITAN GUNSHIP",
                4,
                1,
                32,
                3.2f,
                5,
                0.45f,
                5.5f,
                WarshipClass.Titan,
                new Color(1f, 0.45f, 0.1f, 1f),
                "Heavy armored brawler. Sustains heavy fire and punches through lines."
            ),
            [SquadronUnitId.SpecterSniper] = new SquadronCardDef(
                SquadronUnitId.SpecterSniper,
                "SPECTER SNIPER",
                5,
                1,
                14,
                3.8f,
                12,
                1.1f,
                8.5f,
                WarshipClass.Specter,
                new Color(0.75f, 0.25f, 1f, 1f),
                "Long-range railgun sniper. Pierces hulls and snipes the opponent flagship."
            ),
            [SquadronUnitId.TempestCruiser] = new SquadronCardDef(
                SquadronUnitId.TempestCruiser,
                "TEMPEST CRUISER",
                6,
                1,
                48,
                2.5f,
                7,
                0.55f,
                6.0f,
                WarshipClass.Tempest,
                new Color(0.15f, 1f, 0.85f, 1f),
                "Command cruiser projecting an energy barrier that shields nearby allies."
            ),
            [SquadronUnitId.PhoenixColossus] = new SquadronCardDef(
                SquadronUnitId.PhoenixColossus,
                "PHOENIX DREADNOUGHT",
                8,
                1,
                85,
                1.8f,
                15,
                0.65f,
                7.0f,
                WarshipClass.Phoenix,
                new Color(1f, 0.82f, 0.15f, 1f),
                "Apex dreadnought with quadruple plasma cannons. Decimates all resistance."
            )
        };

        public static SquadronCardDef Get(SquadronUnitId id)
        {
            return catalog.TryGetValue(id, out var def) ? def : catalog[SquadronUnitId.ViperSwarm];
        }

        public static List<SquadronCardDef> GetDefaultPlayerDeck()
        {
            return new List<SquadronCardDef>
            {
                Get(SquadronUnitId.ViperSwarm),
                Get(SquadronUnitId.TitanGunship),
                Get(SquadronUnitId.SpecterSniper),
                Get(SquadronUnitId.TempestCruiser)
            };
        }

        public static List<SquadronCardDef> GetDefaultOpponentDeck()
        {
            return new List<SquadronCardDef>
            {
                Get(SquadronUnitId.ViperSwarm),
                Get(SquadronUnitId.TitanGunship),
                Get(SquadronUnitId.SpecterSniper),
                Get(SquadronUnitId.PhoenixColossus)
            };
        }
    }
}
