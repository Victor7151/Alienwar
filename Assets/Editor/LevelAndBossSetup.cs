using System.IO;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Starfall.Core;
using Starfall.Enemies;
using Starfall.Levels;

namespace Starfall.Editor
{
    public static class LevelAndBossSetup
    {
        [MenuItem("STARFALL/Setup 10-Wave Levels & Bosses")]
        public static void SetupAllLevelsAndBosses()
        {
            Debug.Log("[LevelAndBossSetup] Initializing Boss prefabs, 10-Wave Campaign Levels, and Object Pools...");

            SetupMiniBossPrefab();
            SetupPlasmaDreadnoughtPrefab();
            SetupHiveQueenBossPrefab();
            SetupInfernoColossusPrefab();

            CreateCampaignMissionData();
            SetupLevel4Scene();
            UpdateScenePoolsAndLevelManager();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("<color=cyan>[LevelAndBossSetup] All 3 Bosses, 10-Wave Campaign Levels, and Pools successfully initialized!</color>");
        }

        private static void SafeSetInt(SerializedObject so, string propName, int val)
        {
            var p = so.FindProperty(propName);
            if (p != null) p.intValue = val;
        }

        private static void SafeSetFloat(SerializedObject so, string propName, float val)
        {
            var p = so.FindProperty(propName);
            if (p != null) p.floatValue = val;
        }

        private static void SafeSetString(SerializedObject so, string propName, string val)
        {
            var p = so.FindProperty(propName);
            if (p != null) p.stringValue = val;
        }

        private static void SetupMiniBossPrefab()
        {
            string path = "Assets/Prefabs/Enemies/MiniBossGunship.prefab";
            var go = new GameObject("MiniBossGunship");
            go.layer = 8; // Enemy layer

            var sr = go.AddComponent<SpriteRenderer>();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/Enemies/enemyRed1.png");
            if (sprite != null) sr.sprite = sprite;
            sr.sortingOrder = 14;
            go.transform.localScale = new Vector3(2.2f, 2.2f, 1f);

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(0.9f, 0.85f);

            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;

            var boss = go.AddComponent<MiniBossGunship>();
            var so = new SerializedObject(boss);
            SafeSetInt(so, "maxHealth", 160);
            SafeSetInt(so, "scoreReward", 3500);
            SafeSetFloat(so, "combatY", 2.2f);
            SafeSetString(so, "projectilePoolKey", "EnemyProjectile");
            SafeSetString(so, "weaponDropKey", "PowerUp_RapidFire");
            SafeSetString(so, "explosionPoolKey", "ExplosionVFX");
            so.ApplyModifiedProperties();

            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            Debug.Log("[LevelAndBossSetup] Created MiniBossGunship.prefab");
        }

        private static void SetupPlasmaDreadnoughtPrefab()
        {
            string path = "Assets/Prefabs/Enemies/PlasmaDreadnought.prefab";
            var go = new GameObject("PlasmaDreadnought");
            go.layer = 8; // Enemy layer

            var sr = go.AddComponent<SpriteRenderer>();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/ALienware 2D assets/Enemy Warships/Enemy Boss/PlasmaDreadnought_Boss.png");
            if (sprite != null) sr.sprite = sprite;
            sr.sortingOrder = 15;
            go.transform.localScale = new Vector3(0.88f, 0.88f, 1f);

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(2.6f, 2.6f);

            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;

            var boss = go.AddComponent<PlasmaDreadnought>();
            var so = new SerializedObject(boss);
            SafeSetInt(so, "maxHealth", 350);
            SafeSetInt(so, "scoreReward", 8000);
            SafeSetFloat(so, "combatY", 2.1f);
            SafeSetString(so, "projectilePoolKey", "EnemyProjectile");
            SafeSetString(so, "explosionPoolKey", "ExplosionVFX");
            SafeSetString(so, "weaponDropKey", "PowerUp_Weapon");
            SafeSetString(so, "bombDropKey", "PowerUp_Bomb");
            so.ApplyModifiedProperties();

            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            Debug.Log("[LevelAndBossSetup] Created PlasmaDreadnought.prefab");
        }

        private static void SetupHiveQueenBossPrefab()
        {
            string path = "Assets/Prefabs/Enemies/HiveQueenBoss.prefab";
            var go = new GameObject("HiveQueenBoss");
            go.layer = 8; // Enemy layer

            var sr = go.AddComponent<SpriteRenderer>();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/Enemies/enemyGreen3.png");
            if (sprite != null) sr.sprite = sprite;
            sr.sortingOrder = 16;
            go.transform.localScale = new Vector3(4.0f, 4.0f, 1f);

            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.5f;

            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;

            var boss = go.AddComponent<HiveQueenBoss>();
            var so = new SerializedObject(boss);
            SafeSetInt(so, "maxHealth", 450);
            SafeSetInt(so, "scoreReward", 12000);
            SafeSetFloat(so, "combatY", 2.2f);
            SafeSetString(so, "projectilePoolKey", "EnemyProjectile");
            SafeSetString(so, "scoutPoolKey", "ScoutEnemy");
            SafeSetString(so, "explosionPoolKey", "ExplosionVFX");
            SafeSetString(so, "weaponDropKey", "PowerUp_Weapon");
            SafeSetString(so, "bombDropKey", "PowerUp_Bomb");
            so.ApplyModifiedProperties();

            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            Debug.Log("[LevelAndBossSetup] Created HiveQueenBoss.prefab");
        }

        private static void SetupInfernoColossusPrefab()
        {
            string path = "Assets/Prefabs/Enemies/InfernoColossusBoss.prefab";
            var go = new GameObject("InfernoColossusBoss");
            go.layer = 8; // Enemy layer

            var sr = go.AddComponent<SpriteRenderer>();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/Enemies/InfernoColossus_Boss.png");
            if (sprite != null) sr.sprite = sprite;
            sr.sortingOrder = 16;
            go.transform.localScale = new Vector3(1.35f, 1.35f, 1f);

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(2.6f, 2.6f);

            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;

            var boss = go.AddComponent<InfernoColossusBoss>();
            var so = new SerializedObject(boss);
            SafeSetInt(so, "maxHealth", 700);
            SafeSetInt(so, "scoreReward", 25000);
            SafeSetFloat(so, "combatY", 2.2f);
            SafeSetString(so, "projectilePoolKey", "EnemyProjectile");
            SafeSetString(so, "fighterPoolKey", "FighterEnemy");
            SafeSetString(so, "explosionPoolKey", "ExplosionVFX");
            SafeSetString(so, "weaponDropKey", "PowerUp_Weapon");
            SafeSetString(so, "bombDropKey", "PowerUp_Bomb");
            so.ApplyModifiedProperties();

            PrefabUtility.SaveAsPrefabAsset(go, path);
            Object.DestroyImmediate(go);
            Debug.Log("[LevelAndBossSetup] Created InfernoColossusBoss.prefab");
        }

        private static void SetupLevel4Scene()
        {
            string l4Path = "Assets/Scenes/Level4_WarTornWreckage.unity";
            if (!File.Exists(l4Path))
            {
                string srcScene = "Assets/Scenes/Level3_DeepNebula.unity";
                if (File.Exists(srcScene))
                {
                    File.Copy(srcScene, l4Path);
                    AssetDatabase.Refresh();
                    Debug.Log($"[LevelAndBossSetup] Created {l4Path} from {srcScene}");
                }
            }

            var scenes = EditorBuildSettings.scenes;
            bool exists = false;
            foreach (var s in scenes)
            {
                if (s.path == l4Path)
                {
                    exists = true;
                    break;
                }
            }
            if (!exists)
            {
                var newScenes = new EditorBuildSettingsScene[scenes.Length + 1];
                for (int i = 0; i < scenes.Length; i++) newScenes[i] = scenes[i];
                newScenes[scenes.Length] = new EditorBuildSettingsScene(l4Path, true);
                EditorBuildSettings.scenes = newScenes;
                Debug.Log($"[LevelAndBossSetup] Added {l4Path} to EditorBuildSettings");
            }
        }

        private static void CreateCampaignMissionData()
        {
            string dir = "Assets/ScriptableObjects/Missions/Earth";
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // ==========================================
            // LEVEL 1: Earth Orbit (10 Waves, Wave 5 Mini-Boss, Wave 10 Invader Carrier)
            // ==========================================
            CreateOrUpdate10WaveMission(
                $"{dir}/Earth_Mission_01.asset",
                "MISSION_EARTH_01", "WORLD_EARTH", 1,
                "Earth Orbit: Vanguard",
                "Alien battle formations are entering Earth orbit. Clear 10 escalating waves including enemy gunships and defeat the Flagship Carrier!",
                new WaveDefinition[]
                {
                    // Wave 1 – Scout Armada Alpha (2 sets: Grid -> Diamond)
                    new WaveDefinition
                    {
                        waveName = "Scout Armada Alpha",
                        enemyCount = 32, gridRows = 4, gridColumns = 8, spawnInterval = 0.035f,
                        fighterRatio = 0.0f, formation = FormationType.Grid, secondaryFormation = FormationType.Diamond,
                        totalSets = 2,
                        fireIntervalMin = 1.4f, fireIntervalMax = 2.2f, formationSwaySpeed = 1.0f,
                        enemyHealthMultiplier = 1.0f, projectileSpeedMultiplier = 0.9f, doubleShotChance = 0.10f,
                        diveIntervalMin = 6.0f, diveIntervalMax = 8.5f, aimedShotChance = 0f
                    },

                    // Wave 2 – Dual Flanking Incursion (2 sets: FlankingColumns -> VFormation)
                    new WaveDefinition
                    {
                        waveName = "Dual Flanking Incursion",
                        enemyCount = 32, gridRows = 4, gridColumns = 8, spawnInterval = 0.03f,
                        fighterRatio = 0.15f, formation = FormationType.FlankingColumns, secondaryFormation = FormationType.VFormation,
                        totalSets = 2, waveAnnouncement = "WAVE 2: DUAL FLANKING INCURSION",
                        fireIntervalMin = 1.2f, fireIntervalMax = 1.8f, formationSwaySpeed = 1.2f,
                        enemyHealthMultiplier = 1.1f, projectileSpeedMultiplier = 1.0f, doubleShotChance = 0.20f,
                        diveIntervalMin = 4.5f, diveIntervalMax = 7.0f, aimedShotChance = 0.10f
                    },

                    // Wave 3 – Arrowhead Chevron (2 sets: VFormation -> Crescent) -> Interwave BRACE FOR IMPACT
                    new WaveDefinition
                    {
                        waveName = "Arrowhead Chevron",
                        enemyCount = 28, gridRows = 3, gridColumns = 9, spawnInterval = 0.03f,
                        fighterRatio = 0.30f, formation = FormationType.VFormation, secondaryFormation = FormationType.Crescent,
                        totalSets = 2, waveAnnouncement = "WAVE 3: ARROWHEAD CHEVRON",
                        fireIntervalMin = 1.0f, fireIntervalMax = 1.6f, formationSwaySpeed = 1.4f,
                        enemyHealthMultiplier = 1.2f, projectileSpeedMultiplier = 1.05f, doubleShotChance = 0.25f,
                        diveIntervalMin = 3.5f, diveIntervalMax = 5.5f, aimedShotChance = 0.20f,
                        interwaveEventAfter = InterwaveEventType.BraceForImpact
                    },

                    // Wave 4 – Hazard Belt Incursion (2 sets: Wall -> FlankingColumns with drifting rocks)
                    new WaveDefinition
                    {
                        waveName = "Hazard Belt",
                        enemyCount = 32, gridRows = 4, gridColumns = 8, spawnInterval = 0.03f,
                        fighterRatio = 0.35f, formation = FormationType.Wall, secondaryFormation = FormationType.FlankingColumns,
                        totalSets = 2, spawnAsteroid = true, waveAnnouncement = "WAVE 4: HAZARD BELT INCURSION",
                        fireIntervalMin = 0.9f, fireIntervalMax = 1.5f, formationSwaySpeed = 1.5f,
                        enemyHealthMultiplier = 1.3f, projectileSpeedMultiplier = 1.1f, doubleShotChance = 0.30f,
                        diveIntervalMin = 3.2f, diveIntervalMax = 5.0f, aimedShotChance = 0.25f
                    },

                    // Wave 5 – MINI-BOSS: Elite Armored Gunship Corsair + Diamond Escorts (2 sets)
                    new WaveDefinition
                    {
                        waveName = "ELITE VANGUARD",
                        enemyCount = 16, gridRows = 3, gridColumns = 8, spawnInterval = 0.035f,
                        fighterRatio = 0.50f, formation = FormationType.Diamond, secondaryFormation = FormationType.VFormation,
                        totalSets = 2,
                        isMiniBossWave = true, miniBossPoolKey = "MiniBossGunship", miniBossDisplayName = "Gunship Corsair",
                        miniBossSpawnPosition = new Vector3(0f, 7.5f, 0f),
                        waveAnnouncement = "WARNING: ELITE CORSAIR DETECTED!",
                        fireIntervalMin = 0.85f, fireIntervalMax = 1.4f, formationSwaySpeed = 1.6f,
                        enemyHealthMultiplier = 1.4f, projectileSpeedMultiplier = 1.15f, doubleShotChance = 0.35f,
                        diveIntervalMin = 3.0f, diveIntervalMax = 5.0f, aimedShotChance = 0.25f
                    },

                    // Wave 6 – Twin Crescent Enclosure (2 sets: Crescent -> Pincer) -> Interwave BONUS STAGE
                    new WaveDefinition
                    {
                        waveName = "Twin Crescent Enclosure",
                        enemyCount = 30, gridRows = 3, gridColumns = 10, spawnInterval = 0.03f,
                        fighterRatio = 0.45f, formation = FormationType.Crescent, secondaryFormation = FormationType.Pincer,
                        totalSets = 2, waveAnnouncement = "WAVE 6: CRESCENT ENCLOSURE",
                        fireIntervalMin = 0.8f, fireIntervalMax = 1.3f, formationSwaySpeed = 1.7f,
                        enemyHealthMultiplier = 1.5f, projectileSpeedMultiplier = 1.2f, doubleShotChance = 0.40f,
                        diveIntervalMin = 2.8f, diveIntervalMax = 4.2f, aimedShotChance = 0.30f,
                        interwaveEventAfter = InterwaveEventType.BonusStage
                    },

                    // Wave 7 – Echelon Diagonal Blitz (2 sets: Echelon -> Grid)
                    new WaveDefinition
                    {
                        waveName = "Echelon Diagonal Blitz",
                        enemyCount = 35, gridRows = 4, gridColumns = 10, spawnInterval = 0.025f,
                        fighterRatio = 0.60f, formation = FormationType.Echelon, secondaryFormation = FormationType.Grid,
                        totalSets = 2, waveAnnouncement = "WAVE 7: ECHELON DIAGONAL BLITZ",
                        fireIntervalMin = 0.7f, fireIntervalMax = 1.2f, formationSwaySpeed = 1.8f,
                        enemyHealthMultiplier = 1.6f, projectileSpeedMultiplier = 1.25f, doubleShotChance = 0.45f,
                        diveIntervalMin = 2.6f, diveIntervalMax = 3.8f, aimedShotChance = 0.35f
                    },

                    // Wave 8 – Armored Asteroid Wall (2 sets: Wall -> Diamond)
                    new WaveDefinition
                    {
                        waveName = "Armored Asteroid Wall",
                        enemyCount = 36, gridRows = 3, gridColumns = 12, spawnInterval = 0.025f,
                        fighterRatio = 0.65f, formation = FormationType.Wall, secondaryFormation = FormationType.Diamond,
                        totalSets = 2, spawnAsteroid = true, waveAnnouncement = "WAVE 8: ARMORED METEOR WALL",
                        fireIntervalMin = 0.6f, fireIntervalMax = 1.1f, formationSwaySpeed = 2.0f,
                        enemyHealthMultiplier = 1.75f, projectileSpeedMultiplier = 1.3f, doubleShotChance = 0.50f,
                        diveIntervalMin = 2.4f, diveIntervalMax = 3.6f, aimedShotChance = 0.40f
                    },

                    // Wave 9 – Grand Armada Pre-Boss Swarm (3 sets: Diamond -> Crescent -> Echelon)
                    new WaveDefinition
                    {
                        waveName = "Grand Armada Fleet",
                        enemyCount = 42, gridRows = 4, gridColumns = 11, spawnInterval = 0.02f,
                        fighterRatio = 0.75f, formation = FormationType.Diamond, secondaryFormation = FormationType.Crescent,
                        totalSets = 3, spawnAsteroid = true, waveAnnouncement = "WAVE 9: GRAND ARMADA (3 SETS)",
                        fireIntervalMin = 0.5f, fireIntervalMax = 0.95f, formationSwaySpeed = 2.2f,
                        enemyHealthMultiplier = 2.0f, projectileSpeedMultiplier = 1.35f, doubleShotChance = 0.60f,
                        diveIntervalMin = 1.8f, diveIntervalMax = 2.8f, aimedShotChance = 0.50f
                    },

                    // Wave 10 – Flagship Chapter Boss Wave (triggers Invader Carrier)
                    new WaveDefinition
                    {
                        waveName = "COMMANDER FLAGSHIP",
                        enemyCount = 0, gridRows = 0, gridColumns = 0, spawnInterval = 0.05f,
                        fighterRatio = 0.0f, formation = FormationType.Grid,
                        totalSets = 1,
                        waveAnnouncement = "FINAL WARNING: MOTHERSHIP INBOUND!"
                    }
                },
                true, "InvaderCarrier", 2.0f, 1500, 15000, false
            );

            // ==========================================
            // LEVEL 2: Asteroid Belt (10 Waves, Wave 5 Mini-Boss, Wave 10 Plasma Dreadnought)
            // Harder than Level 1, with 3 to 4 reinforcement sets per wave!
            // ==========================================
            CreateOrUpdate10WaveMission(
                $"{dir}/Level_02_AsteroidBelt.asset",
                "MISSION_EARTH_02", "WORLD_ASTEROID_BELT", 2,
                "Asteroid Belt: Heavy Fire",
                "Navigate the treacherous asteroid fields under intense heavy fire. Survive relentless 3-to-4 set reinforcement waves, destroy the Corsair Overseer, and annihilate the Plasma Dreadnought!",
                new WaveDefinition[]
                {
                    // Wave 1 – Asteroid Belt Incursion (3 Sets: Echelon -> Diamond -> FlankingColumns)
                    new WaveDefinition
                    {
                        waveName = "Belt Incursion",
                        enemyCount = 36, gridRows = 4, gridColumns = 9, spawnInterval = 0.03f,
                        fighterRatio = 0.40f, formation = FormationType.Echelon, secondaryFormation = FormationType.Diamond,
                        totalSets = 3, waveAnnouncement = "WAVE 1: BELT INCURSION (3 SETS)",
                        fireIntervalMin = 0.9f, fireIntervalMax = 1.4f, formationSwaySpeed = 1.6f,
                        enemyHealthMultiplier = 1.5f, projectileSpeedMultiplier = 1.25f, doubleShotChance = 0.35f,
                        diveIntervalMin = 3.5f, diveIntervalMax = 5.0f, aimedShotChance = 0.30f
                    },

                    // Wave 2 – Crescent Cluster Sweep (3 Sets: Crescent -> Pincer -> Diamond) + Bonus Stage
                    new WaveDefinition
                    {
                        waveName = "Crescent Cluster Sweep",
                        enemyCount = 36, gridRows = 4, gridColumns = 10, spawnInterval = 0.03f,
                        fighterRatio = 0.45f, formation = FormationType.Crescent, secondaryFormation = FormationType.Pincer,
                        totalSets = 3, spawnAsteroid = true, waveAnnouncement = "WAVE 2: CLUSTER SWEEP (3 SETS)",
                        fireIntervalMin = 0.85f, fireIntervalMax = 1.35f, formationSwaySpeed = 1.8f,
                        enemyHealthMultiplier = 1.65f, projectileSpeedMultiplier = 1.30f, doubleShotChance = 0.40f,
                        diveIntervalMin = 3.0f, diveIntervalMax = 4.5f, aimedShotChance = 0.35f,
                        interwaveEventAfter = InterwaveEventType.BonusStage
                    },

                    // Wave 3 – Heavy Raider Chevron (4 Sets: VFormation -> Echelon -> Crescent -> FlankingColumns) + BRACE FOR IMPACT
                    new WaveDefinition
                    {
                        waveName = "Heavy Raider Chevron",
                        enemyCount = 36, gridRows = 4, gridColumns = 9, spawnInterval = 0.028f,
                        fighterRatio = 0.55f, formation = FormationType.VFormation, secondaryFormation = FormationType.Echelon,
                        totalSets = 4, waveAnnouncement = "WAVE 3: HEAVY RAIDER CHEVRON (4 SETS)",
                        fireIntervalMin = 0.75f, fireIntervalMax = 1.25f, formationSwaySpeed = 2.0f,
                        enemyHealthMultiplier = 1.8f, projectileSpeedMultiplier = 1.35f, doubleShotChance = 0.45f,
                        diveIntervalMin = 2.6f, diveIntervalMax = 4.0f, aimedShotChance = 0.40f,
                        interwaveEventAfter = InterwaveEventType.BraceForImpact
                    },

                    // Wave 4 – Dense Debris Barrier (3 Sets: Wall -> Crescent -> Diamond)
                    new WaveDefinition
                    {
                        waveName = "Dense Debris Barrier",
                        enemyCount = 38, gridRows = 4, gridColumns = 10, spawnInterval = 0.028f,
                        fighterRatio = 0.60f, formation = FormationType.Wall, secondaryFormation = FormationType.Crescent,
                        totalSets = 3, spawnAsteroid = true, waveAnnouncement = "WAVE 4: DENSE DEBRIS BARRIER (3 SETS)",
                        fireIntervalMin = 0.70f, fireIntervalMax = 1.20f, formationSwaySpeed = 2.1f,
                        enemyHealthMultiplier = 1.95f, projectileSpeedMultiplier = 1.40f, doubleShotChance = 0.50f,
                        diveIntervalMin = 2.4f, diveIntervalMax = 3.8f, aimedShotChance = 0.45f
                    },

                    // Wave 5 – MINI-BOSS: Corsair Overseer (Heavy Gunship) + 3 Sets of Diamond Escorts
                    new WaveDefinition
                    {
                        waveName = "ELITE CORSAIR OVERSEER",
                        enemyCount = 20, gridRows = 3, gridColumns = 8, spawnInterval = 0.035f,
                        fighterRatio = 0.70f, formation = FormationType.Diamond, secondaryFormation = FormationType.FlankingColumns,
                        totalSets = 3,
                        isMiniBossWave = true, miniBossPoolKey = "MiniBossGunship", miniBossDisplayName = "Corsair Overseer",
                        miniBossSpawnPosition = new Vector3(0f, 7.5f, 0f),
                        waveAnnouncement = "WARNING: CORSAIR OVERSEER DETECTED (3 SETS)!",
                        fireIntervalMin = 0.65f, fireIntervalMax = 1.15f, formationSwaySpeed = 2.2f,
                        enemyHealthMultiplier = 2.1f, projectileSpeedMultiplier = 1.45f, doubleShotChance = 0.55f,
                        diveIntervalMin = 2.2f, diveIntervalMax = 3.5f, aimedShotChance = 0.50f
                    },

                    // Wave 6 – Twin Vanguard Surge (4 Sets: Echelon -> Crescent -> Pincer -> Diamond) + Bonus Stage
                    new WaveDefinition
                    {
                        waveName = "Twin Vanguard Surge",
                        enemyCount = 38, gridRows = 4, gridColumns = 10, spawnInterval = 0.025f,
                        fighterRatio = 0.65f, formation = FormationType.Echelon, secondaryFormation = FormationType.Crescent,
                        totalSets = 4, waveAnnouncement = "WAVE 6: TWIN VANGUARD SURGE (4 SETS)",
                        fireIntervalMin = 0.60f, fireIntervalMax = 1.10f, formationSwaySpeed = 2.3f,
                        enemyHealthMultiplier = 2.25f, projectileSpeedMultiplier = 1.45f, doubleShotChance = 0.60f,
                        diveIntervalMin = 2.0f, diveIntervalMax = 3.2f, aimedShotChance = 0.55f,
                        interwaveEventAfter = InterwaveEventType.BonusStage
                    },

                    // Wave 7 – Crossfire Flankers (4 Sets: FlankingColumns -> Crescent -> Echelon -> Wall)
                    new WaveDefinition
                    {
                        waveName = "Crossfire Flankers",
                        enemyCount = 40, gridRows = 4, gridColumns = 10, spawnInterval = 0.024f,
                        fighterRatio = 0.75f, formation = FormationType.FlankingColumns, secondaryFormation = FormationType.Crescent,
                        totalSets = 4, spawnAsteroid = true, waveAnnouncement = "WAVE 7: CROSSFIRE FLANKERS (4 SETS)",
                        fireIntervalMin = 0.55f, fireIntervalMax = 1.05f, formationSwaySpeed = 2.4f,
                        enemyHealthMultiplier = 2.4f, projectileSpeedMultiplier = 1.50f, doubleShotChance = 0.65f,
                        diveIntervalMin = 1.8f, diveIntervalMax = 3.0f, aimedShotChance = 0.60f
                    },

                    // Wave 8 – Armored Asteroid Wall (4 Sets: Wall -> Crescent -> Diamond -> VFormation) + BRACE FOR IMPACT
                    new WaveDefinition
                    {
                        waveName = "Armored Asteroid Wall",
                        enemyCount = 42, gridRows = 4, gridColumns = 11, spawnInterval = 0.022f,
                        fighterRatio = 0.80f, formation = FormationType.Wall, secondaryFormation = FormationType.Diamond,
                        totalSets = 4, spawnAsteroid = true, waveAnnouncement = "WAVE 8: ARMORED ASTEROID WALL (4 SETS)",
                        fireIntervalMin = 0.50f, fireIntervalMax = 0.95f, formationSwaySpeed = 2.5f,
                        enemyHealthMultiplier = 2.6f, projectileSpeedMultiplier = 1.55f, doubleShotChance = 0.70f,
                        diveIntervalMin = 1.6f, diveIntervalMax = 2.8f, aimedShotChance = 0.65f,
                        interwaveEventAfter = InterwaveEventType.BraceForImpact
                    },

                    // Wave 9 – Grand Dreadnought Armada (4 Sets: Diamond -> Crescent -> Echelon -> FlankingColumns)
                    new WaveDefinition
                    {
                        waveName = "Grand Dreadnought Armada",
                        enemyCount = 46, gridRows = 4, gridColumns = 11, spawnInterval = 0.020f,
                        fighterRatio = 0.85f, formation = FormationType.Diamond, secondaryFormation = FormationType.Crescent,
                        totalSets = 4, spawnAsteroid = true, waveAnnouncement = "WAVE 9: GRAND ARMADA (4 SETS - PRE-BOSS BRUTALITY)",
                        fireIntervalMin = 0.45f, fireIntervalMax = 0.85f, formationSwaySpeed = 2.6f,
                        enemyHealthMultiplier = 2.85f, projectileSpeedMultiplier = 1.65f, doubleShotChance = 0.75f,
                        diveIntervalMin = 1.4f, diveIntervalMax = 2.4f, aimedShotChance = 0.70f
                    },

                    // Wave 10 – CHAPTER BOSS: The Plasma Dreadnought
                    new WaveDefinition
                    {
                        waveName = "THE PLASMA DREADNOUGHT",
                        enemyCount = 0, gridRows = 0, gridColumns = 0, spawnInterval = 0.05f,
                        fighterRatio = 0.0f, formation = FormationType.Grid,
                        totalSets = 1,
                        waveAnnouncement = "FINAL WARNING: PLASMA DREADNOUGHT FLAGSHIP APPROACHING!"
                    }
                },
                true, "PlasmaDreadnought", 2.0f, 2500, 25000, false
            );

            // ==========================================
            // LEVEL 3: Deep Nebula (The Hive Swarm) – Apex 10-Wave Campaign (4 to 5 Sets per wave!)
            // ==========================================
            CreateOrUpdate10WaveMission(
                $"{dir}/Level_03_DeepNebula.asset",
                "MISSION_EARTH_03", "WORLD_DEEP_NEBULA", 3,
                "Deep Nebula: Hive Core",
                "Enter the alien nesting nebula. Survive relentless alien hordes and the Hive Dread-Stalker, then confront the Hive Queen herself!",
                new WaveDefinition[]
                {
                    // Wave 1 – Bio-Scout Flankers (4 Sets: FlankingColumns -> Crescent -> Diamond -> VFormation)
                    new WaveDefinition
                    {
                        waveName = "Bio-Scout Flankers",
                        enemyCount = 36, gridRows = 4, gridColumns = 9, spawnInterval = 0.026f,
                        fighterRatio = 0.50f, formation = FormationType.FlankingColumns, secondaryFormation = FormationType.Crescent,
                        totalSets = 4, waveAnnouncement = "WAVE 1: BIO-SCOUT FLANKERS (4 REINFORCEMENT SETS)",
                        fireIntervalMin = 0.80f, fireIntervalMax = 1.30f, formationSwaySpeed = 1.8f,
                        enemyHealthMultiplier = 1.80f, projectileSpeedMultiplier = 1.35f, doubleShotChance = 0.45f,
                        diveIntervalMin = 3.0f, diveIntervalMax = 4.5f, aimedShotChance = 0.40f
                    },

                    // Wave 2 – Venomous Crescent (4 Sets: Crescent -> Diamond -> Echelon -> Grid)
                    new WaveDefinition
                    {
                        waveName = "Venomous Crescent",
                        enemyCount = 38, gridRows = 4, gridColumns = 10, spawnInterval = 0.025f,
                        fighterRatio = 0.55f, formation = FormationType.Crescent, secondaryFormation = FormationType.Diamond,
                        totalSets = 4, waveAnnouncement = "WAVE 2: VENOMOUS CRESCENT (4 SETS)",
                        fireIntervalMin = 0.75f, fireIntervalMax = 1.20f, formationSwaySpeed = 1.9f,
                        enemyHealthMultiplier = 2.0f, projectileSpeedMultiplier = 1.40f, doubleShotChance = 0.50f,
                        diveIntervalMin = 2.8f, diveIntervalMax = 4.2f, aimedShotChance = 0.45f
                    },

                    // Wave 3 – Hive Pincer Claws (4 Sets: Pincer -> FlankingColumns -> Crescent -> Echelon)
                    new WaveDefinition
                    {
                        waveName = "Hive Pincer Claws",
                        enemyCount = 40, gridRows = 4, gridColumns = 10, spawnInterval = 0.024f,
                        fighterRatio = 0.60f, formation = FormationType.Pincer, secondaryFormation = FormationType.FlankingColumns,
                        totalSets = 4, waveAnnouncement = "WAVE 3: HIVE PINCER CLAWS (4 SETS)",
                        fireIntervalMin = 0.70f, fireIntervalMax = 1.15f, formationSwaySpeed = 2.0f,
                        enemyHealthMultiplier = 2.20f, projectileSpeedMultiplier = 1.45f, doubleShotChance = 0.55f,
                        diveIntervalMin = 2.5f, diveIntervalMax = 3.8f, aimedShotChance = 0.50f
                    },

                    // Wave 4 – Nebula Ion Barrier (5 Sets: Wall -> Crescent -> Diamond -> Echelon -> FlankingColumns) + BRACE FOR IMPACT
                    new WaveDefinition
                    {
                        waveName = "Nebula Ion Barrier",
                        enemyCount = 42, gridRows = 4, gridColumns = 11, spawnInterval = 0.022f,
                        fighterRatio = 0.65f, formation = FormationType.Wall, secondaryFormation = FormationType.Crescent,
                        totalSets = 5, spawnAsteroid = true, waveAnnouncement = "WAVE 4: NEBULA ION BARRIER (5 SETS - EXTREME SURGE)",
                        fireIntervalMin = 0.65f, fireIntervalMax = 1.10f, formationSwaySpeed = 2.1f,
                        enemyHealthMultiplier = 2.40f, projectileSpeedMultiplier = 1.50f, doubleShotChance = 0.60f,
                        diveIntervalMin = 2.2f, diveIntervalMax = 3.5f, aimedShotChance = 0.55f,
                        interwaveEventAfter = InterwaveEventType.BraceForImpact
                    },

                    // Wave 5 – MINI-BOSS: Hive Dread-Stalker (Heavy Gunship) + 4 Sets of Escorts
                    new WaveDefinition
                    {
                        waveName = "HIVE DREAD-STALKER",
                        enemyCount = 20, gridRows = 3, gridColumns = 8, spawnInterval = 0.030f,
                        fighterRatio = 0.70f, formation = FormationType.Diamond, secondaryFormation = FormationType.Crescent,
                        totalSets = 4,
                        isMiniBossWave = true, miniBossCount = 1, miniBossPoolKey = "MiniBossGunship", miniBossDisplayName = "Hive Dread-Stalker",
                        miniBossSpawnPosition = new Vector3(0f, 7.5f, 0f),
                        waveAnnouncement = "WARNING: HIVE DREAD-STALKER DETECTED (4 SETS)!",
                        fireIntervalMin = 0.60f, fireIntervalMax = 1.00f, formationSwaySpeed = 2.2f,
                        enemyHealthMultiplier = 2.60f, projectileSpeedMultiplier = 1.55f, doubleShotChance = 0.65f,
                        diveIntervalMin = 2.0f, diveIntervalMax = 3.2f, aimedShotChance = 0.60f
                    },

                    // Wave 6 – Royal Brood Swarm (5 Sets: Crescent -> Diamond -> FlankingColumns -> Echelon -> Pincer)
                    new WaveDefinition
                    {
                        waveName = "Royal Brood Swarm",
                        enemyCount = 42, gridRows = 4, gridColumns = 11, spawnInterval = 0.022f,
                        fighterRatio = 0.75f, formation = FormationType.Crescent, secondaryFormation = FormationType.Diamond,
                        totalSets = 5, waveAnnouncement = "WAVE 6: ROYAL BROOD SWARM (5 SETS)",
                        fireIntervalMin = 0.55f, fireIntervalMax = 0.95f, formationSwaySpeed = 2.3f,
                        enemyHealthMultiplier = 2.80f, projectileSpeedMultiplier = 1.60f, doubleShotChance = 0.70f,
                        diveIntervalMin = 1.8f, diveIntervalMax = 3.0f, aimedShotChance = 0.65f
                    },

                    // Wave 7 – Spore Spreader Echelon (5 Sets: Echelon -> Crescent -> Diamond -> Wall -> FlankingColumns)
                    new WaveDefinition
                    {
                        waveName = "Spore Spreader Echelon",
                        enemyCount = 45, gridRows = 4, gridColumns = 11, spawnInterval = 0.020f,
                        fighterRatio = 0.80f, formation = FormationType.Echelon, secondaryFormation = FormationType.Crescent,
                        totalSets = 5, spawnAsteroid = true, waveAnnouncement = "WAVE 7: SPORE SPREADER ECHELON (5 SETS)",
                        fireIntervalMin = 0.50f, fireIntervalMax = 0.90f, formationSwaySpeed = 2.4f,
                        enemyHealthMultiplier = 3.0f, projectileSpeedMultiplier = 1.65f, doubleShotChance = 0.75f,
                        diveIntervalMin = 1.6f, diveIntervalMax = 2.8f, aimedShotChance = 0.70f
                    },

                    // Wave 8 – DUAL MINI-BOSSES: Twin Dread-Stalker Vanguard + 4 Sets of Escorts + BRACE FOR IMPACT
                    new WaveDefinition
                    {
                        waveName = "TWIN STALKER VANGUARD",
                        enemyCount = 24, gridRows = 3, gridColumns = 8, spawnInterval = 0.025f,
                        fighterRatio = 0.85f, formation = FormationType.Diamond, secondaryFormation = FormationType.FlankingColumns,
                        totalSets = 4, spawnAsteroid = true,
                        isMiniBossWave = true, miniBossCount = 2, miniBossPoolKey = "MiniBossGunship", miniBossDisplayName = "Twin Dread-Stalker",
                        miniBossSpawnPosition = new Vector3(0f, 7.5f, 0f),
                        waveAnnouncement = "WARNING: TWIN DREAD-STALKERS INBOUND (4 SETS)!",
                        fireIntervalMin = 0.48f, fireIntervalMax = 0.85f, formationSwaySpeed = 2.5f,
                        enemyHealthMultiplier = 3.20f, projectileSpeedMultiplier = 1.70f, doubleShotChance = 0.80f,
                        diveIntervalMin = 1.5f, diveIntervalMax = 2.5f, aimedShotChance = 0.75f,
                        interwaveEventAfter = InterwaveEventType.BraceForImpact
                    },

                    // Wave 9 – Apex Hive Armada (5 Sets: Diamond -> Crescent -> Echelon -> FlankingColumns -> Diamond)
                    new WaveDefinition
                    {
                        waveName = "Apex Hive Armada",
                        enemyCount = 48, gridRows = 4, gridColumns = 11, spawnInterval = 0.018f,
                        fighterRatio = 0.90f, formation = FormationType.Diamond, secondaryFormation = FormationType.Crescent,
                        totalSets = 5, spawnAsteroid = true, waveAnnouncement = "WAVE 9: APEX HIVE ARMADA (5 SETS - APEX SWARM!)",
                        fireIntervalMin = 0.42f, fireIntervalMax = 0.78f, formationSwaySpeed = 2.7f,
                        enemyHealthMultiplier = 3.60f, projectileSpeedMultiplier = 1.80f, doubleShotChance = 0.85f,
                        diveIntervalMin = 1.3f, diveIntervalMax = 2.2f, aimedShotChance = 0.80f
                    },

                    // Wave 10 – FINAL CHAPTER BOSS: HIVE QUEEN LEVIATHAN
                    new WaveDefinition
                    {
                        waveName = "HIVE QUEEN LEVIATHAN",
                        enemyCount = 0, gridRows = 0, gridColumns = 0, spawnInterval = 0.05f,
                        fighterRatio = 0.0f, formation = FormationType.Grid,
                        totalSets = 1,
                        waveAnnouncement = "FINAL WARNING: HIVE QUEEN LEVIATHAN AWAKENED!"
                    }
                },
                true, "HiveQueenBoss", 2.0f, 3500, 35000, true
            );

            // ==========================================
            // LEVEL 4: War-Torn Wreckage (10 Waves, Triple Mini-Bosses Wave 8, Apex Boss Inferno Colossus)
            // ==========================================
            CreateOrUpdate10WaveMission(
                $"{dir}/Level_04_WarTornWreckage.asset",
                "MISSION_EARTH_04", "WORLD_EARTH", 4,
                "War-Torn Wreckage: Inferno Colossus",
                "Battle through scorching slag fields, heavy shrapnel barrages, triple corsair vanguards, and extinguish the cybernetic Inferno Colossus Juggernaut!",
                new WaveDefinition[]
                {
                    // Wave 1 – Scorched Vanguard (4 Sets)
                    new WaveDefinition
                    {
                        waveName = "Scorched Vanguard",
                        enemyCount = 36, gridRows = 4, gridColumns = 9, spawnInterval = 0.024f,
                        fighterRatio = 0.60f, formation = FormationType.FlankingColumns, secondaryFormation = FormationType.VFormation,
                        totalSets = 4, waveAnnouncement = "WAVE 1: SCORCHED VANGUARD (4 REINFORCEMENT SETS)",
                        fireIntervalMin = 0.70f, fireIntervalMax = 1.15f, formationSwaySpeed = 2.1f,
                        enemyHealthMultiplier = 2.40f, projectileSpeedMultiplier = 1.50f, doubleShotChance = 0.55f,
                        diveIntervalMin = 2.4f, diveIntervalMax = 3.6f, aimedShotChance = 0.50f
                    },

                    // Wave 2 – Ash-Storm Incursion (5 Sets)
                    new WaveDefinition
                    {
                        waveName = "Ash-Storm Incursion",
                        enemyCount = 38, gridRows = 4, gridColumns = 10, spawnInterval = 0.022f,
                        fighterRatio = 0.65f, formation = FormationType.VFormation, secondaryFormation = FormationType.Wall,
                        totalSets = 5, waveAnnouncement = "WAVE 2: ASH-STORM INCURSION (5 SETS)",
                        fireIntervalMin = 0.65f, fireIntervalMax = 1.10f, formationSwaySpeed = 2.2f,
                        enemyHealthMultiplier = 2.60f, projectileSpeedMultiplier = 1.55f, doubleShotChance = 0.60f,
                        diveIntervalMin = 2.2f, diveIntervalMax = 3.4f, aimedShotChance = 0.55f
                    },

                    // Wave 3 – Derelict Crossfire (5 Sets) + BRACE FOR IMPACT
                    new WaveDefinition
                    {
                        waveName = "Derelict Crossfire",
                        enemyCount = 40, gridRows = 4, gridColumns = 10, spawnInterval = 0.020f,
                        fighterRatio = 0.70f, formation = FormationType.Pincer, secondaryFormation = FormationType.FlankingColumns,
                        totalSets = 5, spawnAsteroid = true, waveAnnouncement = "WAVE 3: DERELICT CROSSFIRE (5 SETS)",
                        fireIntervalMin = 0.60f, fireIntervalMax = 1.05f, formationSwaySpeed = 2.3f,
                        enemyHealthMultiplier = 2.80f, projectileSpeedMultiplier = 1.60f, doubleShotChance = 0.65f,
                        diveIntervalMin = 2.0f, diveIntervalMax = 3.2f, aimedShotChance = 0.60f,
                        interwaveEventAfter = InterwaveEventType.BraceForImpact
                    },

                    // Wave 4 – Magma Flak Perimeter (5 Sets)
                    new WaveDefinition
                    {
                        waveName = "Magma Flak Perimeter",
                        enemyCount = 42, gridRows = 4, gridColumns = 11, spawnInterval = 0.020f,
                        fighterRatio = 0.75f, formation = FormationType.Wall, secondaryFormation = FormationType.VFormation,
                        totalSets = 5, spawnAsteroid = true, waveAnnouncement = "WAVE 4: MAGMA FLAK PERIMETER (5 SETS)",
                        fireIntervalMin = 0.55f, fireIntervalMax = 0.95f, formationSwaySpeed = 2.4f,
                        enemyHealthMultiplier = 3.00f, projectileSpeedMultiplier = 1.65f, doubleShotChance = 0.70f,
                        diveIntervalMin = 1.8f, diveIntervalMax = 3.0f, aimedShotChance = 0.65f
                    },

                    // Wave 5 – MINI-BOSS: Inferno Dread-Corsair + 5 Sets
                    new WaveDefinition
                    {
                        waveName = "ARMORED INFERNO CORSAIR",
                        enemyCount = 22, gridRows = 3, gridColumns = 8, spawnInterval = 0.025f,
                        fighterRatio = 0.80f, formation = FormationType.Diamond, secondaryFormation = FormationType.Crescent,
                        totalSets = 5,
                        isMiniBossWave = true, miniBossCount = 1, miniBossPoolKey = "MiniBossGunship", miniBossDisplayName = "Inferno Dread-Corsair",
                        miniBossSpawnPosition = new Vector3(0f, 7.5f, 0f),
                        waveAnnouncement = "WARNING: INFERNO DREAD-CORSAIR DETECTED (5 SETS)!",
                        fireIntervalMin = 0.50f, fireIntervalMax = 0.90f, formationSwaySpeed = 2.5f,
                        enemyHealthMultiplier = 3.20f, projectileSpeedMultiplier = 1.70f, doubleShotChance = 0.75f,
                        diveIntervalMin = 1.6f, diveIntervalMax = 2.8f, aimedShotChance = 0.70f
                    },

                    // Wave 6 – Volatile Debris Field (5 Sets) + BONUS STAGE
                    new WaveDefinition
                    {
                        waveName = "Volatile Debris Field",
                        enemyCount = 44, gridRows = 4, gridColumns = 11, spawnInterval = 0.018f,
                        fighterRatio = 0.80f, formation = FormationType.Crescent, secondaryFormation = FormationType.Diamond,
                        totalSets = 5, spawnAsteroid = true, waveAnnouncement = "WAVE 6: VOLATILE DEBRIS FIELD (5 SETS)",
                        fireIntervalMin = 0.48f, fireIntervalMax = 0.85f, formationSwaySpeed = 2.5f,
                        enemyHealthMultiplier = 3.40f, projectileSpeedMultiplier = 1.75f, doubleShotChance = 0.80f,
                        diveIntervalMin = 1.5f, diveIntervalMax = 2.6f, aimedShotChance = 0.75f,
                        interwaveEventAfter = InterwaveEventType.BonusStage
                    },

                    // Wave 7 – Ash-Fire Phalanx (6 Sets)
                    new WaveDefinition
                    {
                        waveName = "Ash-Fire Phalanx",
                        enemyCount = 46, gridRows = 4, gridColumns = 12, spawnInterval = 0.018f,
                        fighterRatio = 0.85f, formation = FormationType.Wall, secondaryFormation = FormationType.Crescent,
                        totalSets = 6, spawnAsteroid = true, waveAnnouncement = "WAVE 7: ASH-FIRE PHALANX (6 SETS)",
                        fireIntervalMin = 0.44f, fireIntervalMax = 0.80f, formationSwaySpeed = 2.6f,
                        enemyHealthMultiplier = 3.70f, projectileSpeedMultiplier = 1.80f, doubleShotChance = 0.82f,
                        diveIntervalMin = 1.3f, diveIntervalMax = 2.4f, aimedShotChance = 0.80f
                    },

                    // Wave 8 – TRIPLE MINI-BOSS VANGUARD: 3 Armored Dread-Cruisers + 5 Sets + BRACE FOR IMPACT
                    new WaveDefinition
                    {
                        waveName = "TRIPLE DREAD-CRUISER VANGUARD",
                        enemyCount = 26, gridRows = 3, gridColumns = 9, spawnInterval = 0.020f,
                        fighterRatio = 0.90f, formation = FormationType.Diamond, secondaryFormation = FormationType.FlankingColumns,
                        totalSets = 5, spawnAsteroid = true,
                        isMiniBossWave = true, miniBossCount = 3, miniBossPoolKey = "MiniBossGunship", miniBossDisplayName = "Inferno Dread-Cruiser",
                        miniBossSpawnPosition = new Vector3(0f, 7.5f, 0f),
                        waveAnnouncement = "CRITICAL WARNING: TRIPLE DREAD-CRUISERS INBOUND (5 SETS)!",
                        fireIntervalMin = 0.40f, fireIntervalMax = 0.75f, formationSwaySpeed = 2.7f,
                        enemyHealthMultiplier = 4.00f, projectileSpeedMultiplier = 1.85f, doubleShotChance = 0.85f,
                        diveIntervalMin = 1.2f, diveIntervalMax = 2.2f, aimedShotChance = 0.82f,
                        interwaveEventAfter = InterwaveEventType.BraceForImpact
                    },

                    // Wave 9 – Apocalypse Overlord Fleet (6 Sets)
                    new WaveDefinition
                    {
                        waveName = "Apocalypse Overlord Fleet",
                        enemyCount = 50, gridRows = 4, gridColumns = 12, spawnInterval = 0.015f,
                        fighterRatio = 0.95f, formation = FormationType.Diamond, secondaryFormation = FormationType.Crescent,
                        totalSets = 6, spawnAsteroid = true, waveAnnouncement = "WAVE 9: APOCALYPSE OVERLORD FLEET (6 SETS - MAXIMUM ASSAULT!)",
                        fireIntervalMin = 0.35f, fireIntervalMax = 0.68f, formationSwaySpeed = 2.8f,
                        enemyHealthMultiplier = 4.50f, projectileSpeedMultiplier = 1.95f, doubleShotChance = 0.90f,
                        diveIntervalMin = 1.0f, diveIntervalMax = 1.8f, aimedShotChance = 0.85f
                    },

                    // Wave 10 – FINAL CHAPTER BOSS: INFERNO COLOSSUS JUGGERNAUT
                    new WaveDefinition
                    {
                        waveName = "INFERNO COLOSSUS JUGGERNAUT",
                        enemyCount = 0, gridRows = 0, gridColumns = 0, spawnInterval = 0.05f,
                        fighterRatio = 0.0f, formation = FormationType.Grid,
                        totalSets = 1,
                        waveAnnouncement = "FINAL WARNING: INFERNO COLOSSUS JUGGERNAUT AWAKENED!"
                    }
                },
                true, "InfernoColossusBoss", 2.0f, 5000, 50000, true
            );

            Debug.Log("[LevelAndBossSetup] Successfully created/updated 4 10-Wave Campaign Levels.");
        }

        private static void CreateOrUpdate10WaveMission(string assetPath, string id, string worldId, int number,
            string name, string desc, WaveDefinition[] waves, bool hasBoss, string bossKey, float bossDelay,
            int rewardCredits, int scoreTarget, bool isWarp)
        {
            var mission = AssetDatabase.LoadAssetAtPath<MissionData>(assetPath);
            if (mission == null)
            {
                mission = ScriptableObject.CreateInstance<MissionData>();
                AssetDatabase.CreateAsset(mission, assetPath);
            }

            var so = new SerializedObject(mission);
            so.FindProperty("missionId").stringValue = id;
            so.FindProperty("worldId").stringValue = worldId;
            so.FindProperty("missionNumber").intValue = number;
            so.FindProperty("displayName").stringValue = name;
            so.FindProperty("description").stringValue = desc;

            var wavesProp = so.FindProperty("waves");
            wavesProp.arraySize = waves.Length;
            for (int i = 0; i < waves.Length; i++)
            {
                var w = wavesProp.GetArrayElementAtIndex(i);
                w.FindPropertyRelative("waveName").stringValue = waves[i].waveName;
                w.FindPropertyRelative("enemyCount").intValue = waves[i].enemyCount;
                w.FindPropertyRelative("gridRows").intValue = waves[i].gridRows > 0 ? waves[i].gridRows : 3;
                w.FindPropertyRelative("gridColumns").intValue = waves[i].gridColumns > 0 ? waves[i].gridColumns : 8;
                w.FindPropertyRelative("spawnInterval").floatValue = waves[i].spawnInterval > 0 ? waves[i].spawnInterval : 0.035f;
                w.FindPropertyRelative("fighterRatio").floatValue = waves[i].fighterRatio;
                w.FindPropertyRelative("formation").enumValueIndex = (int)waves[i].formation;
                w.FindPropertyRelative("secondaryFormation").enumValueIndex = (int)waves[i].secondaryFormation;
                w.FindPropertyRelative("totalSets").intValue = waves[i].totalSets > 0 ? waves[i].totalSets : 2;
                w.FindPropertyRelative("spawnAsteroid").boolValue = waves[i].spawnAsteroid;
                w.FindPropertyRelative("isMiniBossWave").boolValue = waves[i].isMiniBossWave;
                var mbcProp = w.FindPropertyRelative("miniBossCount");
                if (mbcProp != null) mbcProp.intValue = waves[i].miniBossCount > 0 ? waves[i].miniBossCount : 1;
                w.FindPropertyRelative("miniBossPoolKey").stringValue = waves[i].miniBossPoolKey ?? "";
                w.FindPropertyRelative("miniBossDisplayName").stringValue = waves[i].miniBossDisplayName ?? "";
                w.FindPropertyRelative("miniBossSpawnPosition").vector3Value = waves[i].miniBossSpawnPosition;
                w.FindPropertyRelative("waveAnnouncement").stringValue = waves[i].waveAnnouncement ?? "";
                w.FindPropertyRelative("interwaveEventAfter").enumValueIndex = (int)waves[i].interwaveEventAfter;
                w.FindPropertyRelative("fireIntervalMin").floatValue = waves[i].fireIntervalMin;
                w.FindPropertyRelative("fireIntervalMax").floatValue = waves[i].fireIntervalMax;
                w.FindPropertyRelative("formationSwaySpeed").floatValue = waves[i].formationSwaySpeed;
                w.FindPropertyRelative("enemyHealthMultiplier").floatValue = waves[i].enemyHealthMultiplier > 0 ? waves[i].enemyHealthMultiplier : 1f;
                w.FindPropertyRelative("projectileSpeedMultiplier").floatValue = waves[i].projectileSpeedMultiplier > 0 ? waves[i].projectileSpeedMultiplier : 1f;
                w.FindPropertyRelative("doubleShotChance").floatValue = waves[i].doubleShotChance;
                w.FindPropertyRelative("diveIntervalMin").floatValue = waves[i].diveIntervalMin > 0 ? waves[i].diveIntervalMin : 4f;
                w.FindPropertyRelative("diveIntervalMax").floatValue = waves[i].diveIntervalMax > 0 ? waves[i].diveIntervalMax : 6f;
                w.FindPropertyRelative("aimedShotChance").floatValue = waves[i].aimedShotChance;
            }

            so.FindProperty("hasBoss").boolValue = hasBoss;
            so.FindProperty("bossPoolKey").stringValue = bossKey;
            so.FindProperty("bossSpawnDelay").floatValue = bossDelay;
            so.FindProperty("starCreditsReward").intValue = rewardCredits;
            so.FindProperty("scoreTarget").intValue = scoreTarget;
            so.FindProperty("isWarpMission").boolValue = isWarp;

            so.ApplyModifiedProperties();
        }

        private static void UpdateScenePoolsAndLevelManager()
        {
            string[] scenePaths = new string[]
            {
                "Assets/Scenes/SampleScene.unity",
                "Assets/Scenes/Level1_EarthOrbit.unity",
                "Assets/Scenes/Level2_AsteroidBelt.unity",
                "Assets/Scenes/Level3_DeepNebula.unity",
                "Assets/Scenes/Level4_WarTornWreckage.unity"
            };

            foreach (var scenePath in scenePaths)
            {
                if (!File.Exists(scenePath)) continue;

                var scene = EditorSceneManager.OpenScene(scenePath);

                // 1. Update ObjectPool with Boss and hazard prefabs
                var poolManagerGo = GameObject.Find("PoolManager");
                if (poolManagerGo != null)
                {
                    var poolManager = poolManagerGo.GetComponent<ObjectPool>();
                    if (poolManager != null)
                    {
                        var so = new SerializedObject(poolManager);
                        var initialPools = so.FindProperty("initialPools");

                        SetOrAddPool(initialPools, "MiniBossGunship", "Assets/Prefabs/Enemies/MiniBossGunship.prefab", 3);
                        SetOrAddPool(initialPools, "PlasmaDreadnought", "Assets/Prefabs/Enemies/PlasmaDreadnought.prefab", 2);
                        SetOrAddPool(initialPools, "HiveQueenBoss", "Assets/Prefabs/Enemies/HiveQueenBoss.prefab", 2);
                        SetOrAddPool(initialPools, "InfernoColossusBoss", "Assets/Prefabs/Enemies/InfernoColossusBoss.prefab", 1);
                        SetOrAddPool(initialPools, "AsteroidHazard", "Assets/Prefabs/Enemies/AsteroidHazard.prefab", 14);

                        so.ApplyModifiedProperties();
                    }
                }

                // 2. Configure LevelManager with 4 campaign levels
                var levelMgrGo = GameObject.Find("LevelManager");
                if (levelMgrGo != null)
                {
                    var levelMgr = levelMgrGo.GetComponent<LevelManager>();
                    if (levelMgr != null)
                    {
                        var so = new SerializedObject(levelMgr);
                        var m1 = AssetDatabase.LoadAssetAtPath<MissionData>("Assets/ScriptableObjects/Missions/Earth/Earth_Mission_01.asset");
                        var m2 = AssetDatabase.LoadAssetAtPath<MissionData>("Assets/ScriptableObjects/Missions/Earth/Level_02_AsteroidBelt.asset");
                        var m3 = AssetDatabase.LoadAssetAtPath<MissionData>("Assets/ScriptableObjects/Missions/Earth/Level_03_DeepNebula.asset");
                        var m4 = AssetDatabase.LoadAssetAtPath<MissionData>("Assets/ScriptableObjects/Missions/Earth/Level_04_WarTornWreckage.asset");

                        int activeIndex = scenePath.Contains("Level4") ? 3 : (scenePath.Contains("Level3") ? 2 : (scenePath.Contains("Level2") ? 1 : 0));
                        var activeMission = activeIndex == 3 ? m4 : (activeIndex == 2 ? m3 : (activeIndex == 1 ? m2 : m1));
                        so.FindProperty("activeMission").objectReferenceValue = activeMission;

                        var campaignProp = so.FindProperty("campaignLevels");
                        if (campaignProp != null)
                        {
                            campaignProp.arraySize = 4;
                            campaignProp.GetArrayElementAtIndex(0).objectReferenceValue = m1;
                            campaignProp.GetArrayElementAtIndex(1).objectReferenceValue = m2;
                            campaignProp.GetArrayElementAtIndex(2).objectReferenceValue = m3;
                            campaignProp.GetArrayElementAtIndex(3).objectReferenceValue = m4;
                        }

                        so.ApplyModifiedProperties();
                    }
                }

                // 3. If this is Level 4, ensure ParallaxController applies Sector 4
                if (scenePath.Contains("Level4"))
                {
                    var parallax = Object.FindAnyObjectByType<Starfall.VFX.ParallaxController>();
                    if (parallax != null)
                    {
                        parallax.ApplySector(4);
                    }
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
                Debug.Log($"[LevelAndBossSetup] Saved ObjectPool and LevelManager changes to {scenePath}");
            }
        }

        private static void SetOrAddPool(SerializedProperty initialPools, string key, string prefabPath, int count)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) return;

            for (int i = 0; i < initialPools.arraySize; i++)
            {
                var entry = initialPools.GetArrayElementAtIndex(i);
                if (entry.FindPropertyRelative("poolKey").stringValue == key)
                {
                    entry.FindPropertyRelative("prefab").objectReferenceValue = prefab;
                    entry.FindPropertyRelative("initialSize").intValue = count;
                    return;
                }
            }

            initialPools.arraySize++;
            var newEntry = initialPools.GetArrayElementAtIndex(initialPools.arraySize - 1);
            newEntry.FindPropertyRelative("poolKey").stringValue = key;
            newEntry.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            newEntry.FindPropertyRelative("initialSize").intValue = count;
        }
    }
}
