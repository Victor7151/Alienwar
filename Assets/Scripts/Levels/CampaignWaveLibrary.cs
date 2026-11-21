using UnityEngine;
using Starfall.Core;
using Starfall.Enemies;

namespace Starfall.Levels
{
    /// <summary>
    /// Per-level tuning that sits outside individual waves (player regen, drop economy,
    /// hazard pacing, boss placement). Harder levels regenerate shields slower and drop less.
    /// </summary>
    public struct LevelDifficultyProfile
    {
        public int level;
        public float shieldRegenDelay;
        public float shieldRegenInterval;
        public float dropChanceMultiplier;
        public int maxDropsPerWave;
        public float ambientHazardMin;
        public float ambientHazardMax;
        public float meteorStormDuration;
        public float miniBossHealthMultiplier;
        public float bossHealthMultiplier;
        public string bossPoolKey;
        public Vector3 bossSpawnPosition;
    }

    /// <summary>
    /// The four-sector campaign as data. Each level follows the directive pacing
    /// (Calm → Build → Pressure → Release → Escalation → Finale), introduces ONE new enemy
    /// role at a time with an on-screen hint, and ramps pressure through patterns, timing and
    /// role combinations — health only rises modestly.
    ///
    /// Curve per level (t = 0 → 1 across waves 1-9) and across levels (L1 → L4):
    ///   fire interval   L1 1.7s → 0.9s   …   L4 1.05s → 0.55s
    ///   bullet speed    L1 0.9x → 1.15x  …   L4 1.1x → 1.4x
    ///   enemy health    L1 1.0x → 1.35x  …   L4 1.5x → 2.0x
    ///   dive interval   L1 ~8s → ~3.7s   …   L4 ~4s → ~2.1s
    /// </summary>
    public static class CampaignWaveLibrary
    {
        public const int WavesPerLevel = 10;

        public static LevelDifficultyProfile GetProfile(int level)
        {
            switch (Mathf.Clamp(level, 1, 4))
            {
                case 2:
                    return new LevelDifficultyProfile { level = 2, shieldRegenDelay = 5.5f, shieldRegenInterval = 2.0f, dropChanceMultiplier = 0.9f, maxDropsPerWave = 3, ambientHazardMin = 3.8f, ambientHazardMax = 6.0f, meteorStormDuration = 15f, miniBossHealthMultiplier = 1.3f, bossHealthMultiplier = 1f, bossPoolKey = "PlasmaDreadnought", bossSpawnPosition = new Vector3(0f, 8.5f, 0f) };
                case 3:
                    return new LevelDifficultyProfile { level = 3, shieldRegenDelay = 6.5f, shieldRegenInterval = 2.4f, dropChanceMultiplier = 0.8f, maxDropsPerWave = 2, ambientHazardMin = 7f, ambientHazardMax = 12f, meteorStormDuration = 16f, miniBossHealthMultiplier = 1.25f, bossHealthMultiplier = 1f, bossPoolKey = "HiveQueenBoss", bossSpawnPosition = new Vector3(0f, 7.5f, 0f) };
                case 4:
                    return new LevelDifficultyProfile { level = 4, shieldRegenDelay = 7.5f, shieldRegenInterval = 2.8f, dropChanceMultiplier = 0.75f, maxDropsPerWave = 2, ambientHazardMin = 3.0f, ambientHazardMax = 5.0f, meteorStormDuration = 18f, miniBossHealthMultiplier = 1.35f, bossHealthMultiplier = 1f, bossPoolKey = "InfernoColossusBoss", bossSpawnPosition = new Vector3(0f, 7.5f, 0f) };
                default:
                    return new LevelDifficultyProfile { level = 1, shieldRegenDelay = 4.5f, shieldRegenInterval = 1.6f, dropChanceMultiplier = 1f, maxDropsPerWave = 3, ambientHazardMin = 0f, ambientHazardMax = 0f, meteorStormDuration = 12f, miniBossHealthMultiplier = 1f, bossHealthMultiplier = 1f, bossPoolKey = "InvaderCarrier", bossSpawnPosition = new Vector3(0f, 8.5f, 0f) };
            }
        }

        public static WaveDefinition[] Build(int level)
        {
            switch (Mathf.Clamp(level, 1, 4))
            {
                case 2: return BuildAsteroidBelt();
                case 3: return BuildDeepNebula();
                case 4: return BuildWarTorn();
                default: return BuildEarthOrbit();
            }
        }

        // ------------------------------------------------------------------
        // Curve helpers
        // ------------------------------------------------------------------

        private struct Curve
        {
            public float fireMinA, fireMinB, fireMaxA, fireMaxB;
            public float speedA, speedB;
            public float hpA, hpB;
            public float diveMinA, diveMinB, diveMaxA, diveMaxB;
            public float specialA, specialB;
            public float swayA, swayB;
            public int bulletsA, bulletsB;
            public float fighterA, fighterB;
        }

        private static Curve LevelCurve(int level)
        {
            switch (level)
            {
                case 2: return new Curve { fireMinA = 1.3f, fireMinB = 0.72f, fireMaxA = 1.9f, fireMaxB = 1.1f, speedA = 1.0f, speedB = 1.25f, hpA = 1.15f, hpB = 1.55f, diveMinA = 5f, diveMinB = 2.4f, diveMaxA = 7f, diveMaxB = 3.6f, specialA = 3.4f, specialB = 2.1f, swayA = 1.3f, swayB = 2.0f, bulletsA = 16, bulletsB = 24, fighterA = 0.3f, fighterB = 0.55f };
                case 3: return new Curve { fireMinA = 1.15f, fireMinB = 0.64f, fireMaxA = 1.7f, fireMaxB = 1.0f, speedA = 1.05f, speedB = 1.32f, hpA = 1.3f, hpB = 1.75f, diveMinA = 4.4f, diveMinB = 2.0f, diveMaxA = 6.2f, diveMaxB = 3.1f, specialA = 3.0f, specialB = 1.7f, swayA = 1.5f, swayB = 2.3f, bulletsA = 20, bulletsB = 28, fighterA = 0.3f, fighterB = 0.6f };
                case 4: return new Curve { fireMinA = 1.05f, fireMinB = 0.55f, fireMaxA = 1.5f, fireMaxB = 0.9f, speedA = 1.1f, speedB = 1.4f, hpA = 1.5f, hpB = 2.0f, diveMinA = 3.4f, diveMinB = 1.7f, diveMaxA = 5f, diveMaxB = 2.6f, specialA = 2.6f, specialB = 1.4f, swayA = 1.7f, swayB = 2.6f, bulletsA = 24, bulletsB = 32, fighterA = 0.45f, fighterB = 0.7f };
                default: return new Curve { fireMinA = 1.7f, fireMinB = 0.9f, fireMaxA = 2.5f, fireMaxB = 1.35f, speedA = 0.9f, speedB = 1.15f, hpA = 1.0f, hpB = 1.35f, diveMinA = 7f, diveMinB = 3f, diveMaxA = 10f, diveMaxB = 4.5f, specialA = 4.2f, specialB = 2.5f, swayA = 1.0f, swayB = 1.8f, bulletsA = 12, bulletsB = 20, fighterA = 0.1f, fighterB = 0.45f };
            }
        }

        /// <summary>Creates a wave whose pressure sits at position t (0..1) on the level curve.</summary>
        private static WaveDefinition W(int level, float t, string name, int count, params FormationType[] sets)
        {
            var c = LevelCurve(level);
            float L(float a, float b) => Mathf.Lerp(a, b, Mathf.Clamp01(t));
            var def = new WaveDefinition
            {
                waveName = name,
                enemyCount = count,
                formationSequence = sets,
                formation = sets.Length > 0 ? sets[0] : FormationType.Grid,
                secondaryFormation = sets.Length > 1 ? sets[1] : FormationType.Grid,
                totalSets = Mathf.Max(1, sets.Length),
                fighterRatio = L(c.fighterA, c.fighterB),
                fireIntervalMin = L(c.fireMinA, c.fireMinB),
                fireIntervalMax = L(c.fireMaxA, c.fireMaxB),
                projectileSpeedMultiplier = L(c.speedA, c.speedB),
                enemyHealthMultiplier = L(c.hpA, c.hpB),
                diveIntervalMin = L(c.diveMinA, c.diveMinB),
                diveIntervalMax = L(c.diveMaxA, c.diveMaxB),
                specialIntervalMin = L(c.specialA, c.specialB),
                specialIntervalMax = L(c.specialA, c.specialB) * 1.45f,
                formationSwaySpeed = L(c.swayA, c.swayB),
                maxEnemyBullets = Mathf.RoundToInt(L(c.bulletsA, c.bulletsB)),
                doubleShotChance = L(0.1f, 0.55f) + (level - 1) * 0.08f,
                aimedShotChance = L(0.05f, 0.35f) + (level - 1) * 0.07f,
                waveAnnouncement = name.ToUpperInvariant()
            };
            return def;
        }

        private static WaveDefinition Roles(this WaveDefinition d, float gunner = 0f, float charger = 0f, float bomber = 0f, float shielder = 0f, float elite = 0f, float blinker = 0f)
        {
            d.gunnerRatio = gunner;
            d.chargerRatio = charger;
            d.bomberRatio = bomber;
            d.shielderRatio = shielder;
            d.eliteRatio = elite;
            d.blinkerRatio = blinker;
            return d;
        }

        private static WaveDefinition Patterns(this WaveDefinition d, float curtain = 0f, float fan = 0f)
        {
            d.curtainChance = curtain;
            d.fanChance = fan;
            return d;
        }

        private static WaveDefinition Teach(this WaveDefinition d, EnemyRole role)
        {
            d.introducesRole = true;
            d.newRole = role;
            return d;
        }

        private static WaveDefinition Hazard(this WaveDefinition d) { d.spawnAsteroid = true; return d; }

        private static WaveDefinition After(this WaveDefinition d, InterwaveEventType e) { d.interwaveEventAfter = e; return d; }

        private static WaveDefinition Objective(this WaveDefinition d, WaveObjective obj, float duration = 35f, int breaches = 5)
        {
            d.objective = obj;
            d.survivalDuration = duration;
            d.maxBreachesAllowed = breaches;
            return d;
        }

        private static WaveDefinition Modifier(this WaveDefinition d, WaveModifier mod)
        {
            d.modifier = mod;
            return d;
        }

        private static WaveDefinition Commander(this WaveDefinition d)
        {
            d.hasCommander = true;
            return d;
        }

        private static WaveDefinition Stealth(this WaveDefinition d, int method, string text) 
        { 
            d.isStealthWave = true; 
            d.stealthMethod = method; 
            d.waveAnnouncement = text;
            return d; 
        }

        private static WaveDefinition MiniBoss(this WaveDefinition d, string displayName, int count)
        {
            d.isMiniBossWave = true;
            d.miniBossCount = count;
            d.miniBossPoolKey = "MiniBossGunship";
            d.miniBossDisplayName = displayName;
            d.miniBossSpawnPosition = new Vector3(0f, 7.5f, 0f);
            d.waveAnnouncement = count > 1 ? $"WARNING: {count} {displayName.ToUpperInvariant()}S INBOUND!" : $"WARNING: {displayName.ToUpperInvariant()} DETECTED!";
            return d;
        }

        private static WaveDefinition BossWave(string name, string announcement)
        {
            return new WaveDefinition
            {
                waveName = name,
                enemyCount = 0,
                totalSets = 1,
                formation = FormationType.Grid,
                waveAnnouncement = announcement
            };
        }

        // ------------------------------------------------------------------
        // SECTOR 01 — EARTH ORBIT: learn to move, prioritise and dodge.
        // New roles: Gunner (W2), Commander (W4), Elite (W5 escorts).
        // Objectives: Intercept (W3), Decapitate (W4), Survive (W9).
        // ------------------------------------------------------------------
        private static WaveDefinition[] BuildEarthOrbit()
        {
            const int L = 1;
            return new[]
            {
                W(L, 0.00f, "First Contact", 36, FormationType.Grid, FormationType.Wall),
                W(L, 0.10f, "Incoming", 40, FormationType.VFormation, FormationType.Checker)
                    .Roles(gunner: 0.12f).Teach(EnemyRole.Gunner),
                W(L, 0.25f, "Orbital Defense", 44, FormationType.Diamond, FormationType.Swarm)
                    .Roles(gunner: 0.14f).Objective(WaveObjective.Intercept, breaches: 5).Hazard().After(InterwaveEventType.BraceForImpact),
                W(L, 0.40f, "Breaking Point", 44, FormationType.Crescent, FormationType.Grid, FormationType.VFormation)
                    .Roles(gunner: 0.12f, charger: 0.12f).Patterns(curtain: 0.08f).Objective(WaveObjective.Decapitate).Commander().Teach(EnemyRole.Commander),
                W(L, 0.35f, "Elite Vanguard", 28, FormationType.Wall, FormationType.Checker)
                    .Roles(gunner: 0.14f, elite: 0.08f).MiniBoss("Gunship Corsair", 1).Teach(EnemyRole.Elite),
                W(L, 0.45f, "Crescent Enclosure", 48, FormationType.Crescent, FormationType.Pincer)
                    .Roles(gunner: 0.12f, charger: 0.12f).Patterns(fan: 0.08f).Modifier(WaveModifier.VolatileHazards).After(InterwaveEventType.BonusStage),
                W(L, 0.65f, "Echelon Blitz", 48, FormationType.Echelon, FormationType.Swarm, FormationType.Diamond)
                    .Roles(gunner: 0.15f, charger: 0.12f, elite: 0.04f).Patterns(curtain: 0.14f, fan: 0.1f).Modifier(WaveModifier.Overclocked),
                W(L, 0.80f, "Slumbering River", 60, FormationType.Grid, FormationType.Checker, FormationType.Checker)
                    .Roles(gunner: 0f).Stealth(0, "WARNING: ENEMY FLEET SLUMBERING.\nDO NOT WAKE THEM! MANEUVER TO SURVIVE."),
                W(L, 1.00f, "Grand Armada", 52, FormationType.Diamond, FormationType.Echelon, FormationType.Swarm, FormationType.Wall)
                    .Roles(gunner: 0.18f, charger: 0.15f, elite: 0.07f).Patterns(curtain: 0.2f, fan: 0.15f).Objective(WaveObjective.Survive, duration: 40f).Modifier(WaveModifier.ArmoredFront),
                BossWave("Invader Carrier", "FINAL WARNING: MOTHERSHIP INBOUND!")
            };
        }

        // ------------------------------------------------------------------
        // SECTOR 02 — ASTEROID BELT: constant rock traffic; control space.
        // New roles: Bomber (W2), Shielder (W4).
        // Objectives: Intercept (W6), Decapitate (W7), Survive (W9).
        // ------------------------------------------------------------------
        private static WaveDefinition[] BuildAsteroidBelt()
        {
            const int L = 2;
            return new[]
            {
                W(L, 0.00f, "Belt Incursion", 44, FormationType.Swarm, FormationType.Grid)
                    .Roles(gunner: 0.12f, charger: 0.08f),
                W(L, 0.12f, "Minefield", 44, FormationType.Checker, FormationType.Wall)
                    .Roles(gunner: 0.1f, bomber: 0.16f).Teach(EnemyRole.Bomber),
                W(L, 0.28f, "Rock Crossfire", 48, FormationType.Pincer, FormationType.Diamond, FormationType.Crescent)
                    .Roles(gunner: 0.12f, charger: 0.1f, bomber: 0.12f).Patterns(curtain: 0.12f).Modifier(WaveModifier.VolatileHazards).Hazard().After(InterwaveEventType.BraceForImpact),
                W(L, 0.40f, "Bulwark", 48, FormationType.Grid, FormationType.VFormation, FormationType.Wall)
                    .Roles(gunner: 0.14f, bomber: 0.08f, shielder: 0.1f).Modifier(WaveModifier.ArmoredFront).Teach(EnemyRole.Shielder),
                W(L, 0.38f, "Rock Hauler Corsair", 32, FormationType.Diamond, FormationType.Checker, FormationType.Swarm)
                    .Roles(gunner: 0.15f, shielder: 0.1f, elite: 0.06f).MiniBoss("Rock Hauler Corsair", 1),
                W(L, 0.50f, "Debris Storm", 52, FormationType.Crescent, FormationType.Echelon, FormationType.Pincer)
                    .Roles(charger: 0.14f, bomber: 0.12f, gunner: 0.1f).Patterns(curtain: 0.2f).Objective(WaveObjective.Intercept, breaches: 4).Hazard().After(InterwaveEventType.BonusStage),
                W(L, 0.68f, "Siege Line", 52, FormationType.Wall, FormationType.Grid, FormationType.Diamond, FormationType.Swarm)
                    .Roles(gunner: 0.18f, shielder: 0.12f, elite: 0.06f, bomber: 0.08f).Patterns(fan: 0.15f, curtain: 0.12f).Objective(WaveObjective.Decapitate).Commander(),
                W(L, 0.84f, "Drifting Minefield", 52, FormationType.Pincer, FormationType.FlankingColumns, FormationType.Crescent, FormationType.Wall)
                    .Roles(charger: 0f, bomber: 0f, shielder: 0f, gunner: 0f).Stealth(1, "WARNING: MINEFIELD IN SLEEP MODE.\nEVASIVE MANEUVERS REQUIRED.").Hazard(),
                W(L, 1.00f, "Dreadnought Escort", 56, FormationType.Diamond, FormationType.Echelon, FormationType.Grid, FormationType.Swarm)
                    .Roles(gunner: 0.16f, charger: 0.14f, bomber: 0.12f, shielder: 0.1f, elite: 0.08f).Patterns(curtain: 0.22f, fan: 0.18f).Objective(WaveObjective.Survive, duration: 45f).Modifier(WaveModifier.Overclocked).Hazard(),
                BossWave("The Plasma Dreadnought", "FINAL WARNING: PLASMA DREADNOUGHT APPROACHING!")
            };
        }

        // ------------------------------------------------------------------
        // SECTOR 03 — DEEP NEBULA: unpredictability. Swarms, teleports, bullet curtains.
        // New role: Blinker (W2). Twin mini-bosses at W5.
        // Objectives: Intercept (W4), Decapitate (W7), Survive (W9).
        // ------------------------------------------------------------------
        private static WaveDefinition[] BuildDeepNebula()
        {
            const int L = 3;
            return new[]
            {
                W(L, 0.00f, "Spore Drift", 52, FormationType.Swarm, FormationType.Swarm)
                    .Roles(gunner: 0.12f, charger: 0.12f),
                W(L, 0.12f, "Phase Shift", 48, FormationType.Checker, FormationType.Diamond)
                    .Roles(gunner: 0.1f, blinker: 0.16f).Teach(EnemyRole.Blinker),
                W(L, 0.28f, "Hive Claws", 52, FormationType.Pincer, FormationType.FlankingColumns, FormationType.Crescent)
                    .Roles(charger: 0.16f, blinker: 0.1f, gunner: 0.1f).Patterns(curtain: 0.2f).Modifier(WaveModifier.Overclocked),
                W(L, 0.45f, "Ion Curtain", 52, FormationType.Wall, FormationType.Grid, FormationType.Wall, FormationType.Swarm)
                    .Roles(gunner: 0.15f, bomber: 0.1f, shielder: 0.08f).Patterns(curtain: 0.35f).Objective(WaveObjective.Intercept, breaches: 4).After(InterwaveEventType.BraceForImpact),
                W(L, 0.40f, "Nebula Phantoms", 32, FormationType.Diamond, FormationType.Swarm, FormationType.Checker)
                    .Roles(blinker: 0.12f, elite: 0.08f, gunner: 0.12f).MiniBoss("Nebula Phantom", 2),
                W(L, 0.55f, "Brood Swarm", 56, FormationType.Swarm, FormationType.Swarm, FormationType.Echelon, FormationType.Crescent)
                    .Roles(charger: 0.18f, blinker: 0.12f).Patterns(curtain: 0.15f).Modifier(WaveModifier.VolatileHazards).After(InterwaveEventType.BonusStage),
                W(L, 0.70f, "Venom Lattice", 56, FormationType.Grid, FormationType.Diamond, FormationType.Wall, FormationType.Pincer)
                    .Roles(shielder: 0.12f, bomber: 0.14f, gunner: 0.15f, blinker: 0.06f).Patterns(fan: 0.2f, curtain: 0.18f).Objective(WaveObjective.Decapitate).Commander(),
                W(L, 0.85f, "Searchlight Patrol", 56, FormationType.Crescent, FormationType.Checker, FormationType.Echelon, FormationType.Wall, FormationType.Diamond)
                    .Roles(elite: 0f, shielder: 0f, gunner: 0f, charger: 0f, blinker: 0f).Stealth(2, "WARNING: PATROL FLEET DORMANT.\nDO NOT TRIGGER ALARM."),
                W(L, 1.00f, "Apex Hive", 60, FormationType.Swarm, FormationType.Diamond, FormationType.Echelon, FormationType.Grid, FormationType.Crescent)
                    .Roles(elite: 0.1f, shielder: 0.1f, gunner: 0.15f, charger: 0.14f, bomber: 0.1f, blinker: 0.1f).Patterns(curtain: 0.28f, fan: 0.2f).Objective(WaveObjective.Survive, duration: 45f).Modifier(WaveModifier.ArmoredFront),
                BossWave("Hive Queen Leviathan", "FINAL WARNING: HIVE QUEEN LEVIATHAN AWAKENED!")
            };
        }

        // ------------------------------------------------------------------
        // SECTOR 04 — WAR-TORN WRECKAGE: everything at once, scarce drops, heavy hazards.
        // Twin mini-bosses at W5, triple at W8.
        // Objectives: Intercept (W6), Decapitate (W7), Survive (W9).
        // ------------------------------------------------------------------
        private static WaveDefinition[] BuildWarTorn()
        {
            const int L = 4;
            return new[]
            {
                W(L, 0.00f, "Scorched Vanguard", 52, FormationType.Grid, FormationType.Swarm, FormationType.Crescent)
                    .Roles(charger: 0.14f, gunner: 0.15f, bomber: 0.08f),
                W(L, 0.14f, "Ash Storm", 52, FormationType.Wall, FormationType.Checker, FormationType.Echelon)
                    .Roles(bomber: 0.15f, shielder: 0.1f, blinker: 0.08f, gunner: 0.1f).Patterns(curtain: 0.25f).Modifier(WaveModifier.VolatileHazards).Hazard(),
                W(L, 0.30f, "Derelict Crossfire", 56, FormationType.Pincer, FormationType.FlankingColumns, FormationType.Diamond, FormationType.Swarm)
                    .Roles(charger: 0.18f, gunner: 0.15f, blinker: 0.08f).Patterns(fan: 0.18f, curtain: 0.18f).Modifier(WaveModifier.Overclocked).After(InterwaveEventType.BraceForImpact),
                W(L, 0.45f, "Magma Flak", 56, FormationType.Wall, FormationType.Grid, FormationType.Wall, FormationType.Crescent)
                    .Roles(gunner: 0.18f, shielder: 0.1f, elite: 0.06f).Patterns(curtain: 0.35f, fan: 0.2f).Modifier(WaveModifier.ArmoredFront),
                W(L, 0.42f, "Scrap Titans", 36, FormationType.Diamond, FormationType.Checker, FormationType.Swarm)
                    .Roles(elite: 0.08f, shielder: 0.1f, gunner: 0.14f, charger: 0.1f).MiniBoss("Scrap Titan", 2),
                W(L, 0.58f, "Volatile Field", 56, FormationType.Crescent, FormationType.Swarm, FormationType.Pincer, FormationType.Echelon)
                    .Roles(bomber: 0.16f, charger: 0.16f, blinker: 0.1f).Patterns(curtain: 0.22f).Objective(WaveObjective.Intercept, breaches: 4).Hazard().After(InterwaveEventType.BonusStage),
                W(L, 0.74f, "Ash-Fire Phalanx", 56, FormationType.Wall, FormationType.Crescent, FormationType.Echelon, FormationType.Diamond, FormationType.Grid)
                    .Roles(shielder: 0.12f, elite: 0.1f, gunner: 0.16f, bomber: 0.1f).Patterns(fan: 0.24f, curtain: 0.25f).Objective(WaveObjective.Decapitate).Commander(),
                W(L, 0.80f, "Stealth Abyss", 40, FormationType.Diamond, FormationType.FlankingColumns, FormationType.Swarm, FormationType.Checker)
                    .Roles(elite: 0f, charger: 0f, blinker: 0f, gunner: 0f).Stealth(3, "WARNING: DREAD-CRUISER FLEET ASLEEP.\nPASS QUIETLY OR PERISH."),
                W(L, 1.00f, "Apocalypse Fleet", 60, FormationType.Swarm, FormationType.Diamond, FormationType.Crescent, FormationType.Echelon, FormationType.Wall, FormationType.Grid)
                    .Roles(elite: 0.12f, shielder: 0.1f, gunner: 0.15f, charger: 0.15f, bomber: 0.1f, blinker: 0.1f).Patterns(curtain: 0.3f, fan: 0.24f).Objective(WaveObjective.Survive, duration: 50f).Modifier(WaveModifier.Overclocked).Hazard(),
                BossWave("Inferno Colossus", "FINAL WARNING: INFERNO COLOSSUS JUGGERNAUT AWAKENED!")
            };
        }
    }
}

