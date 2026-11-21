using System.Collections.Generic;
using UnityEngine;

namespace Starfall.Core
{
    public enum WarshipClass
    {
        Viper,      // Balanced Interceptor
        Titan,      // Heavy Armored Dreadnought
        Specter,    // Stealth Assassin / Precision Speed
        Tempest,    // Techno-Carrier / 3 Hardpoints
        Phoenix     // Solar Prototype / 4 Hardpoints
    }

    [System.Serializable]
    public class WarshipSpec
    {
        public string id;
        public string name;
        public string role;
        public WarshipClass shipClass;
        public int creditPrice;

        // Base Attributes (Rated 1 to 10 for UI stats)
        public int baseStrength;        // Lives & Shield Capacity (1 to 10)
        public int baseDamagePower;     // Firepower & DPS (1 to 10)
        public int baseFlexibility;     // Speed & Agility (1 to 10)
        public int baseAbilityPower;    // Special Ability Effectiveness (1 to 10)

        // Runtime Gameplay Values
        public int baseLives;
        public int baseShields;
        public float baseMoveSpeed;
        public float damageMultiplier;
        public float fireRateMultiplier;
        public Color hullColor;
        public string spriteResourceName;

        // Muzzle Hardpoints (local position offsets from ship center)
        public Vector2[] hardpointOffsets;

        // Special Ability
        public string abilityName;
        public string abilityDescription;
        public float abilityCooldown;
        public float abilityDuration;
    }

    public static class FleetDatabase
    {
        public const string SHIP_VIPER = "ship_viper";
        public const string SHIP_TITAN = "ship_titan";
        public const string SHIP_SPECTER = "ship_specter";
        public const string SHIP_TEMPEST = "ship_tempest";
        public const string SHIP_PHOENIX = "ship_phoenix";

        private static readonly Dictionary<string, WarshipSpec> ships = new Dictionary<string, WarshipSpec>()
        {
            {
                SHIP_VIPER,
                new WarshipSpec
                {
                    id = SHIP_VIPER,
                    name = "VIPER MK-I",
                    role = "Tactical Aerospace Interceptor",
                    shipClass = WarshipClass.Viper,
                    creditPrice = 0, // Starter ship
                    baseStrength = 5,
                    baseDamagePower = 5,
                    baseFlexibility = 7,
                    baseAbilityPower = 6,
                    baseLives = 5,
                    baseShields = 3,
                    baseMoveSpeed = 12f,
                    damageMultiplier = 1.0f,
                    fireRateMultiplier = 1.0f,
                    hullColor = new Color(0.2f, 0.65f, 1.0f),
                    spriteResourceName = "PlayerShip",
                    hardpointOffsets = new Vector2[]
                    {
                        new Vector2(-0.25f, 0.45f),
                        new Vector2(0.25f, 0.45f)
                    },
                    abilityName = "OVERDRIVE BURST",
                    abilityDescription = "Discharges a super-rapid laser volley and grants a 50% movement speed boost for 4 seconds.",
                    abilityCooldown = 15f,
                    abilityDuration = 4.0f
                }
            },
            {
                SHIP_TITAN,
                new WarshipSpec
                {
                    id = SHIP_TITAN,
                    name = "CRIMSON TITAN",
                    role = "Heavy Siege Dreadnought",
                    shipClass = WarshipClass.Titan,
                    creditPrice = 2500,
                    baseStrength = 9,
                    baseDamagePower = 8,
                    baseFlexibility = 4,
                    baseAbilityPower = 7,
                    baseLives = 5,
                    baseShields = 5,
                    baseMoveSpeed = 9f,
                    damageMultiplier = 1.8f,
                    fireRateMultiplier = 0.85f,
                    hullColor = new Color(1.0f, 0.25f, 0.2f),
                    spriteResourceName = "playerShip2_blue",
                    hardpointOffsets = new Vector2[]
                    {
                        new Vector2(-0.35f, 0.4f),
                        new Vector2(0.35f, 0.4f)
                    },
                    abilityName = "AEGIS SHOCKWAVE",
                    abilityDescription = "Deploys an invulnerable kinetic shield that detonates a shockwave, annihilating all enemy bullets on screen.",
                    abilityCooldown = 18f,
                    abilityDuration = 3.5f
                }
            },
            {
                SHIP_SPECTER,
                new WarshipSpec
                {
                    id = SHIP_SPECTER,
                    name = "PHANTOM SPECTER",
                    role = "Stealth Skirmisher Assassin",
                    shipClass = WarshipClass.Specter,
                    creditPrice = 6000,
                    baseStrength = 4,
                    baseDamagePower = 7,
                    baseFlexibility = 10,
                    baseAbilityPower = 8,
                    baseLives = 3,
                    baseShields = 2,
                    baseMoveSpeed = 16f,
                    damageMultiplier = 1.35f,
                    fireRateMultiplier = 1.25f,
                    hullColor = new Color(0.1f, 0.9f, 0.85f),
                    spriteResourceName = "playerShip1_blue",
                    hardpointOffsets = new Vector2[]
                    {
                        new Vector2(-0.18f, 0.55f),
                        new Vector2(0.18f, 0.55f)
                    },
                    abilityName = "PHASE WARP CLOAK",
                    abilityDescription = "Phases through space-time becoming completely untargetable for 3s while firing homing needle lasers.",
                    abilityCooldown = 14f,
                    abilityDuration = 3.0f
                }
            },
            {
                SHIP_TEMPEST,
                new WarshipSpec
                {
                    id = SHIP_TEMPEST,
                    name = "TEMPEST VORTEX",
                    role = "Electromagnetic Tech Carrier",
                    shipClass = WarshipClass.Tempest,
                    creditPrice = 12000,
                    baseStrength = 7,
                    baseDamagePower = 9,
                    baseFlexibility = 6,
                    baseAbilityPower = 9,
                    baseLives = 4,
                    baseShields = 4,
                    baseMoveSpeed = 11f,
                    damageMultiplier = 1.5f,
                    fireRateMultiplier = 1.1f,
                    hullColor = new Color(0.7f, 0.3f, 1.0f),
                    spriteResourceName = "PlayerShip",
                    hardpointOffsets = new Vector2[]
                    {
                        new Vector2(0f, 0.6f),        // Center cannon
                        new Vector2(-0.38f, 0.35f),   // Left vortex pod
                        new Vector2(0.38f, 0.35f)     // Right vortex pod
                    },
                    abilityName = "VORTEX EMP DISRUPTION",
                    abilityDescription = "Unleashes an electromagnetic pulse pulling all star credits/power-ups directly to the ship and EMP-stunning enemies.",
                    abilityCooldown = 16f,
                    abilityDuration = 4.0f
                }
            },
            {
                SHIP_PHOENIX,
                new WarshipSpec
                {
                    id = SHIP_PHOENIX,
                    name = "SOLARIS PHOENIX",
                    role = "Solar Hybrid Supreme Prototype",
                    shipClass = WarshipClass.Phoenix,
                    creditPrice = 25000,
                    baseStrength = 8,
                    baseDamagePower = 10,
                    baseFlexibility = 8,
                    baseAbilityPower = 10,
                    baseLives = 5,
                    baseShields = 6,
                    baseMoveSpeed = 13.5f,
                    damageMultiplier = 2.2f,
                    fireRateMultiplier = 1.3f,
                    hullColor = new Color(1.0f, 0.8f, 0.1f),
                    spriteResourceName = "playerShip2_blue",
                    hardpointOffsets = new Vector2[]
                    {
                        new Vector2(-0.16f, 0.6f),    // Inner left solar beam
                        new Vector2(0.16f, 0.6f),     // Inner right solar beam
                        new Vector2(-0.42f, 0.42f),   // Outer left photon blaster
                        new Vector2(0.42f, 0.42f)     // Outer right photon blaster
                    },
                    abilityName = "SUPERNOVA FLARE",
                    abilityDescription = "Channels solar fusion energy into a massive full-screen solar flare beam, melting everything in its path.",
                    abilityCooldown = 20f,
                    abilityDuration = 5.0f
                }
            }
        };

        public static readonly string[] AllShipIds = new string[]
        {
            SHIP_VIPER,
            SHIP_TITAN,
            SHIP_SPECTER,
            SHIP_TEMPEST,
            SHIP_PHOENIX
        };

        public static WarshipSpec GetShip(string shipId)
        {
            if (ships.TryGetValue(shipId, out var spec))
            {
                return spec;
            }
            return ships[SHIP_VIPER];
        }
    }
}
