using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;
using Starfall.Core;
using Starfall.Combat;
using Starfall.VFX;
using Starfall.Audio;
using Starfall.Levels;

namespace Starfall.Editor
{
    public static class AssetIntegrationSetup
    {
        [MenuItem("STARFALL/Apply CC0 Assets & Enhanced VFX")]
        public static void ApplyAllAssetsAndVFX()
        {
            Debug.Log("[AssetIntegration] Beginning full asset & VFX integration...");

            // 1. Build Materials for Particles
            Material particleMat = GetOrCreateParticleMaterial("Assets/Art/VFX/Textures/fire_01.png", "VFX_FireMat");
            Material sparkMat = GetOrCreateParticleMaterial("Assets/Art/VFX/Textures/spark_01.png", "VFX_SparkMat");
            Material shockwaveMat = GetOrCreateParticleMaterial("Assets/Art/VFX/Textures/circle_05.png", "VFX_ShockwaveMat");
            Material smokeMat = GetOrCreateParticleMaterial("Assets/Art/VFX/Textures/smoke_01.png", "VFX_SmokeMat");

            // 2. Setup EnemyProjectile prefab
            SetupEnemyProjectilePrefab();

            // 3. Setup Multi-Stage Explosion VFX Prefab
            SetupExplosionVFXPrefab(sparkMat, shockwaveMat, smokeMat);

            // 4. Setup Player Thrusters on PlayerShip Prefab
            SetupPlayerShipThrusters(particleMat);

            // 5. Setup High-Res Parallax Backgrounds
            SetupEnhancedParallaxBackground();

            // 6. Setup Upgraded PowerUp Visuals
            SetupUpgradedPowerUps();

            // 7. Create Canonical 6 Earth Missions (ScriptableObjects per §20 & §32)
            CreateEarthMissions();

            // 8. Update Scene Pools, LevelManager, and Audio
            UpdateScenePoolsAndAudio();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("<color=green>[AssetIntegration] Successfully applied all CC0 assets, particle systems, missions, and VFX!</color>");
        }

        private static Material GetOrCreateParticleMaterial(string texturePath, string matName)
        {
            string matPath = $"Assets/Art/VFX/{matName}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat == null)
            {
                Shader shader = Shader.Find("Mobile/Particles/Additive");
                if (shader == null) shader = Shader.Find("Particles/Standard Unlit");
                if (shader == null) shader = Shader.Find("Sprites/Default");

                mat = new Material(shader);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
                if (tex != null) mat.mainTexture = tex;

                AssetDatabase.CreateAsset(mat, matPath);
            }
            return mat;
        }

        private static void SetupEnemyProjectilePrefab()
        {
            string prefabPath = "Assets/Prefabs/Projectiles/EnemyProjectile.prefab";
            var go = new GameObject("EnemyProjectile");

            var sr = go.AddComponent<SpriteRenderer>();
            var laserSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/Projectiles/laserRed01.png");
            if (laserSprite != null) sr.sprite = laserSprite;
            sr.sortingOrder = 8;

            var col = go.AddComponent<BoxCollider2D>();
            col.isTrigger = true;
            col.size = new Vector2(0.15f, 0.54f);

            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;

            var proj = go.AddComponent<Projectile>();

            PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
            Object.DestroyImmediate(go);
            Debug.Log("[AssetIntegration] EnemyProjectile.prefab configured.");
        }

        private static void SetupExplosionVFXPrefab(Material sparkMat, Material shockwaveMat, Material smokeMat)
        {
            string prefabPath = "Assets/Prefabs/VFX/ExplosionVFX.prefab";
            var rootGo = new GameObject("ExplosionVFX");

            // Main emitter: Spark & Core Flash
            var ps = rootGo.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = false;
            main.duration = 0.6f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.55f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(4f, 9f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.25f, 0.5f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.7f, 0.2f), new Color(1f, 0.25f, 0.1f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var em = ps.emission;
            em.rateOverTime = 0;
            em.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 25, 35) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.2f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.yellow, 0f), new GradientColorKey(Color.red, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            col.color = new ParticleSystem.MinMaxGradient(grad);

            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            AnimationCurve sizeCurve = new AnimationCurve();
            sizeCurve.AddKey(0f, 1f);
            sizeCurve.AddKey(1f, 0f);
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, sizeCurve);

            var psr = rootGo.GetComponent<ParticleSystemRenderer>();
            if (sparkMat != null) psr.material = sparkMat;

            // Child 1: Shockwave Ring
            var ringGo = new GameObject("ShockwaveRing");
            ringGo.transform.SetParent(rootGo.transform, false);
            var ringPs = ringGo.AddComponent<ParticleSystem>();
            var ringMain = ringPs.main;
            ringMain.loop = false;
            ringMain.duration = 0.35f;
            ringMain.startLifetime = 0.3f;
            ringMain.startSpeed = 0f;
            ringMain.startSize = 0.3f;
            ringMain.startColor = new Color(1f, 0.6f, 0.2f, 0.9f);
            ringMain.simulationSpace = ParticleSystemSimulationSpace.World;

            var ringEm = ringPs.emission;
            ringEm.rateOverTime = 0;
            ringEm.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 1) });

            var ringSize = ringPs.sizeOverLifetime;
            ringSize.enabled = true;
            AnimationCurve ringCurve = new AnimationCurve();
            ringCurve.AddKey(0f, 0.5f);
            ringCurve.AddKey(1f, 3.2f);
            ringSize.size = new ParticleSystem.MinMaxCurve(1f, ringCurve);

            var ringCol = ringPs.colorOverLifetime;
            ringCol.enabled = true;
            var ringGrad = new Gradient();
            ringGrad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(1f, 0.4f, 0.1f), 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.9f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            ringCol.color = new ParticleSystem.MinMaxGradient(ringGrad);

            var ringPsr = ringGo.GetComponent<ParticleSystemRenderer>();
            if (shockwaveMat != null) ringPsr.material = shockwaveMat;

            // Child 2: Smoke billow
            var smokeGo = new GameObject("SmokeCloud");
            smokeGo.transform.SetParent(rootGo.transform, false);
            var smokePs = smokeGo.AddComponent<ParticleSystem>();
            var smokeMain = smokePs.main;
            smokeMain.loop = false;
            smokeMain.duration = 0.7f;
            smokeMain.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.7f);
            smokeMain.startSpeed = new ParticleSystem.MinMaxCurve(1f, 2.5f);
            smokeMain.startSize = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            smokeMain.startColor = new Color(0.3f, 0.3f, 0.35f, 0.5f);
            smokeMain.simulationSpace = ParticleSystemSimulationSpace.World;

            var smokeEm = smokePs.emission;
            smokeEm.rateOverTime = 0;
            smokeEm.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0.05f, 8, 12) });

            var smokeShape = smokePs.shape;
            smokeShape.shapeType = ParticleSystemShapeType.Sphere;
            smokeShape.radius = 0.25f;

            var smokeCol = smokePs.colorOverLifetime;
            smokeCol.enabled = true;
            var smokeGrad = new Gradient();
            smokeGrad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(new Color(0.4f, 0.4f, 0.4f), 0f), new GradientColorKey(new Color(0.15f, 0.15f, 0.15f), 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.6f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            smokeCol.color = new ParticleSystem.MinMaxGradient(smokeGrad);

            var smokePsr = smokeGo.GetComponent<ParticleSystemRenderer>();
            if (smokeMat != null) smokePsr.material = smokeMat;

            rootGo.AddComponent<ExplosionVFX>();

            PrefabUtility.SaveAsPrefabAsset(rootGo, prefabPath);
            Object.DestroyImmediate(rootGo);
            Debug.Log("[AssetIntegration] Multi-Stage ExplosionVFX.prefab configured.");
        }

        private static void SetupPlayerShipThrusters(Material fireMat)
        {
            string shipPrefabPath = "Assets/Prefabs/Player/PlayerShip.prefab";
            var shipRoot = PrefabUtility.LoadPrefabContents(shipPrefabPath);

            // Remove existing thrusters if any
            Transform oldL = shipRoot.transform.Find("LeftThruster");
            if (oldL != null) Object.DestroyImmediate(oldL.gameObject);
            Transform oldR = shipRoot.transform.Find("RightThruster");
            if (oldR != null) Object.DestroyImmediate(oldR.gameObject);

            CreateThruster("LeftThruster", shipRoot.transform, new Vector3(-0.25f, -0.38f, 0f), fireMat);
            CreateThruster("RightThruster", shipRoot.transform, new Vector3(0.25f, -0.38f, 0f), fireMat);

            PrefabUtility.SaveAsPrefabAsset(shipRoot, shipPrefabPath);
            PrefabUtility.UnloadPrefabContents(shipRoot);
            Debug.Log("[AssetIntegration] PlayerShip twin thrusters attached.");
        }

        private static void CreateThruster(string name, Transform parent, Vector3 localPos, Material mat)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // Point exhaust downwards

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.duration = 1f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.15f, 0.25f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 4f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.32f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.2f, 0.8f, 1f, 0.9f), new Color(0.1f, 0.4f, 1f, 0.7f));
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var em = ps.emission;
            em.rateOverTime = 30f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 8f;
            shape.radius = 0.05f;

            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(new Color(0.4f, 0.9f, 1f), 0f), new GradientColorKey(new Color(0f, 0.2f, 0.8f), 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            col.color = new ParticleSystem.MinMaxGradient(grad);

            var psr = go.GetComponent<ParticleSystemRenderer>();
            if (mat != null) psr.material = mat;
        }

        private static void SetupEnhancedParallaxBackground()
        {
            var existing = GameObject.Find("ParallaxBackground");
            if (existing != null) Object.DestroyImmediate(existing);

            var nebulaSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/Backgrounds/Nebula Blue.png");
            var starsSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/Backgrounds/Stars-Big_1.png");
            var tileSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/Starfield_Tile.png");

            if (nebulaSprite == null && tileSprite == null) return;

            var root = new GameObject("ParallaxBackground");
            root.transform.position = Vector3.zero;
            root.AddComponent<ParallaxController>();

            // Layer 1: Distant glowing nebula
            Sprite farSprite = nebulaSprite != null ? nebulaSprite : tileSprite;
            BuildParallaxLayer(root.transform, "Layer_FarNebula", farSprite, new Color(0.8f, 0.85f, 1f, 0.5f), 0.8f, -25);

            // Layer 2: Mid-distance stars
            Sprite midSprite = starsSprite != null ? starsSprite : tileSprite;
            BuildParallaxLayer(root.transform, "Layer_MidStars", midSprite, new Color(0.9f, 0.95f, 1f, 0.8f), 2.2f, -20);

            // Layer 3: Near passing space dust
            BuildParallaxLayer(root.transform, "Layer_NearStars", midSprite, new Color(1f, 1f, 1f, 1f), 4.5f, -15);

            Debug.Log("[AssetIntegration] Enhanced 3-layer parallax background setup with Nebula & Stars.");
        }

        private static void BuildParallaxLayer(Transform parent, string layerName, Sprite sprite, Color tint, float speed, int sortingOrder)
        {
            float tileHeight = 20f;
            var layerGo = new GameObject(layerName);
            layerGo.transform.SetParent(parent, false);
            layerGo.transform.localPosition = Vector3.zero;

            var tileA = CreateBackgroundTile($"{layerName}_A", layerGo.transform, sprite, tint, 0f, sortingOrder, tileHeight);
            var tileB = CreateBackgroundTile($"{layerName}_B", layerGo.transform, sprite, tint, tileHeight, sortingOrder, tileHeight);

            var pl = layerGo.AddComponent<ParallaxLayer>();
            pl.Initialize(tileA, tileB, tileHeight, speed);

            var plSO = new SerializedObject(pl);
            plSO.FindProperty("tileA").objectReferenceValue = tileA;
            plSO.FindProperty("tileB").objectReferenceValue = tileB;
            plSO.FindProperty("tileHeight").floatValue = tileHeight;
            plSO.FindProperty("scrollSpeed").floatValue = speed;
            plSO.ApplyModifiedProperties();
        }

        private static Transform CreateBackgroundTile(string tileName, Transform parent, Sprite sprite,
            Color tint, float offsetY, int sortingOrder, float tileHeight)
        {
            var go = new GameObject(tileName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, offsetY, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = tint;
            sr.sortingOrder = sortingOrder;

            float worldWidth = 12f;
            float spriteW = sprite != null && sprite.rect.width > 0 ? sprite.rect.width / sprite.pixelsPerUnit : 1f;
            float spriteH = sprite != null && sprite.rect.height > 0 ? sprite.rect.height / sprite.pixelsPerUnit : 1f;
            go.transform.localScale = new Vector3(worldWidth / spriteW, tileHeight / spriteH, 1f);

            return go.transform;
        }

        private static void SetupUpgradedPowerUps()
        {
            var poolManagerGo = GameObject.Find("PoolManager");
            if (poolManagerGo == null) return;

            var poolManager = poolManagerGo.GetComponent<ObjectPool>();
            var so = new SerializedObject(poolManager);
            var initialPools = so.FindProperty("initialPools");

            var rapidSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/PowerUps/pill_blue.png");
            var shieldSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/PowerUps/shield_gold.png");
            var scoreSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/PowerUps/star_gold.png");
            var weaponSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/PowerUps/bolt_gold.png");
            var bombSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/PowerUps/pill_red.png");

            UpdatePowerUpSprite(initialPools, "PowerUp_RapidFire", rapidSprite);
            UpdatePowerUpSprite(initialPools, "PowerUp_Shield", shieldSprite);
            UpdatePowerUpSprite(initialPools, "PowerUp_Score", scoreSprite);
            UpdatePowerUpSprite(initialPools, "PowerUp_Weapon", weaponSprite);
            UpdatePowerUpSprite(initialPools, "PowerUp_Bomb", bombSprite);

            so.ApplyModifiedProperties();
            Debug.Log("[AssetIntegration] Power-up pools updated with custom CC0 icons.");
        }

        private static void UpdatePowerUpSprite(SerializedProperty pools, string key, Sprite sprite)
        {
            if (sprite == null) return;
            for (int i = 0; i < pools.arraySize; i++)
            {
                var p = pools.GetArrayElementAtIndex(i);
                if (p.FindPropertyRelative("poolKey").stringValue == key)
                {
                    var go = p.FindPropertyRelative("prefab").objectReferenceValue as GameObject;
                    if (go != null)
                    {
                        var sr = go.GetComponent<SpriteRenderer>();
                        if (sr != null)
                        {
                            sr.sprite = sprite;
                            sr.color = Color.white; // Clear prototype tint to display true sprite colors
                        }
                    }
                    break;
                }
            }
        }

        private static void CreateEarthMissions()
        {
            string dir = "Assets/ScriptableObjects/Missions/Earth";
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            // Mission 1 — First Contact (§20)
            CreateOrUpdateMission(
                $"{dir}/Earth_Mission_01.asset",
                "MISSION_EARTH_01", "WORLD_EARTH", 1,
                "First Contact",
                "Anomalous alien objects detected approaching Earth Orbit. Pilot, launch your interceptor and establish contact.",
                new WaveDefinition[]
                {
                    new WaveDefinition { waveName = "Scout Armada Alpha", enemyCount = 40, gridRows = 4, gridColumns = 10, spawnInterval = 0.03f, fighterRatio = 0.0f, formation = FormationType.Grid, spawnAsteroid = false },
                    new WaveDefinition { waveName = "Fighter Wing Grid", enemyCount = 44, gridRows = 4, gridColumns = 11, spawnInterval = 0.03f, fighterRatio = 0.30f, formation = FormationType.Grid, spawnAsteroid = false },
                    new WaveDefinition { waveName = "Grand Armada Grid", enemyCount = 55, gridRows = 5, gridColumns = 11, spawnInterval = 0.025f, fighterRatio = 0.40f, formation = FormationType.Grid, spawnAsteroid = true }
                },
                false, "", 0f, 100, 800, false
            );

            // Mission 2 — Incoming (§20)
            CreateOrUpdateMission(
                $"{dir}/Earth_Mission_02.asset",
                "MISSION_EARTH_02", "WORLD_EARTH", 2,
                "Incoming",
                "Alien battle formations are deploying into orbit. Watch out for enemy strafers armed with plasma blasters.",
                new WaveDefinition[]
                {
                    new WaveDefinition { waveName = "Orbital Grid A", enemyCount = 24, gridRows = 3, gridColumns = 8, spawnInterval = 0.04f, fighterRatio = 0.2f, formation = FormationType.Grid, spawnAsteroid = false },
                    new WaveDefinition { waveName = "Chevron Assault", enemyCount = 24, gridRows = 3, gridColumns = 8, spawnInterval = 0.04f, fighterRatio = 0.3f, formation = FormationType.VFormation, spawnAsteroid = false },
                    new WaveDefinition { waveName = "Heavy Fighter Grid", enemyCount = 30, gridRows = 3, gridColumns = 10, spawnInterval = 0.035f, fighterRatio = 0.5f, formation = FormationType.Grid, spawnAsteroid = false },
                    new WaveDefinition { waveName = "Combined Vanguard", enemyCount = 30, gridRows = 3, gridColumns = 10, spawnInterval = 0.035f, fighterRatio = 0.4f, formation = FormationType.Grid, spawnAsteroid = false }
                },
                false, "", 0f, 150, 1500, false
            );

            // Mission 3 — Orbital Defense (§20)
            CreateOrUpdateMission(
                $"{dir}/Earth_Mission_03.asset",
                "MISSION_EARTH_03", "WORLD_EARTH", 3,
                "Orbital Defense",
                "The invaders are shattering space debris towards our satellites. Clear the asteroid belt under heavy fire.",
                new WaveDefinition[]
                {
                    new WaveDefinition { waveName = "Debris Field Grid", enemyCount = 24, gridRows = 3, gridColumns = 8, spawnInterval = 0.04f, fighterRatio = 0.2f, formation = FormationType.Grid, spawnAsteroid = true },
                    new WaveDefinition { waveName = "Asteroid Chevron", enemyCount = 24, gridRows = 3, gridColumns = 8, spawnInterval = 0.04f, fighterRatio = 0.3f, formation = FormationType.VFormation, spawnAsteroid = true },
                    new WaveDefinition { waveName = "Heavy Meteors Grid", enemyCount = 30, gridRows = 3, gridColumns = 10, spawnInterval = 0.035f, fighterRatio = 0.4f, formation = FormationType.Grid, spawnAsteroid = true },
                    new WaveDefinition { waveName = "Belt Clearance Grid", enemyCount = 30, gridRows = 3, gridColumns = 10, spawnInterval = 0.035f, fighterRatio = 0.45f, formation = FormationType.Grid, spawnAsteroid = true }
                },
                false, "", 0f, 200, 2200, false
            );

            // Mission 4 — Breaking Point (§20)
            CreateOrUpdateMission(
                $"{dir}/Earth_Mission_04.asset",
                "MISSION_EARTH_04", "WORLD_EARTH", 4,
                "Breaking Point",
                "Human defensive lines are falling. Hold the orbital perimeter against relentless mixed alien waves.",
                new WaveDefinition[]
                {
                    new WaveDefinition { waveName = "Assault Grid", enemyCount = 24, gridRows = 3, gridColumns = 8, spawnInterval = 0.04f, fighterRatio = 0.4f, formation = FormationType.Grid, spawnAsteroid = false },
                    new WaveDefinition { waveName = "Crossfire Chevron", enemyCount = 24, gridRows = 3, gridColumns = 8, spawnInterval = 0.04f, fighterRatio = 0.5f, formation = FormationType.VFormation, spawnAsteroid = false },
                    new WaveDefinition { waveName = "Fighter Phalanx Grid", enemyCount = 30, gridRows = 3, gridColumns = 10, spawnInterval = 0.035f, fighterRatio = 0.6f, formation = FormationType.Grid, spawnAsteroid = true },
                    new WaveDefinition { waveName = "Heavy V-Swarm", enemyCount = 24, gridRows = 3, gridColumns = 8, spawnInterval = 0.04f, fighterRatio = 0.5f, formation = FormationType.VFormation, spawnAsteroid = false },
                    new WaveDefinition { waveName = "Overwhelming Grid", enemyCount = 30, gridRows = 3, gridColumns = 10, spawnInterval = 0.035f, fighterRatio = 0.55f, formation = FormationType.Grid, spawnAsteroid = false }
                },
                false, "", 0f, 250, 3200, false
            );

            // Mission 5 — Starfall (§20)
            CreateOrUpdateMission(
                $"{dir}/Earth_Mission_05.asset",
                "MISSION_EARTH_05", "WORLD_EARTH", 5,
                "Starfall",
                "The invaders have converged on the Starfall facility. Protect the ancient energy station at all costs.",
                new WaveDefinition[]
                {
                    new WaveDefinition { waveName = "Facility Siege Grid", enemyCount = 24, gridRows = 3, gridColumns = 8, spawnInterval = 0.04f, fighterRatio = 0.4f, formation = FormationType.Grid, spawnAsteroid = false },
                    new WaveDefinition { waveName = "Elite Interceptors", enemyCount = 24, gridRows = 3, gridColumns = 8, spawnInterval = 0.04f, fighterRatio = 0.6f, formation = FormationType.VFormation, spawnAsteroid = false },
                    new WaveDefinition { waveName = "Orbital Breach Grid", enemyCount = 30, gridRows = 3, gridColumns = 10, spawnInterval = 0.035f, fighterRatio = 0.5f, formation = FormationType.Grid, spawnAsteroid = true },
                    new WaveDefinition { waveName = "Heavy Barrage Grid", enemyCount = 30, gridRows = 3, gridColumns = 10, spawnInterval = 0.035f, fighterRatio = 0.7f, formation = FormationType.Grid, spawnAsteroid = false },
                    new WaveDefinition { waveName = "Final Outpost Grid", enemyCount = 30, gridRows = 3, gridColumns = 10, spawnInterval = 0.035f, fighterRatio = 0.65f, formation = FormationType.Grid, spawnAsteroid = false }
                },
                false, "", 0f, 350, 4500, false
            );

            // Mission 6 — The Warp (§20, §21, §22)
            CreateOrUpdateMission(
                $"{dir}/Earth_Mission_06.asset",
                "MISSION_EARTH_06", "WORLD_EARTH", 6,
                "The Warp",
                "The alien flagship has entered high orbit! Destroy the Invader Carrier and engage the Starfall gateway.",
                new WaveDefinition[]
                {
                    new WaveDefinition { waveName = "Fleet Spearhead", enemyCount = 24, gridRows = 3, gridColumns = 8, spawnInterval = 0.04f, fighterRatio = 0.5f, formation = FormationType.VFormation, spawnAsteroid = false },
                    new WaveDefinition { waveName = "Pincer Escorts Grid", enemyCount = 24, gridRows = 3, gridColumns = 8, spawnInterval = 0.04f, fighterRatio = 0.5f, formation = FormationType.Grid, spawnAsteroid = false },
                    new WaveDefinition { waveName = "Mothership Guard Grid", enemyCount = 30, gridRows = 3, gridColumns = 10, spawnInterval = 0.035f, fighterRatio = 0.6f, formation = FormationType.Grid, spawnAsteroid = true },
                    new WaveDefinition { waveName = "Carrier Approach Grid", enemyCount = 30, gridRows = 3, gridColumns = 10, spawnInterval = 0.035f, fighterRatio = 0.7f, formation = FormationType.Grid, spawnAsteroid = false }
                },
                true, "InvaderCarrier", 2.5f, 600, 7000, true
            );

            Debug.Log("[AssetIntegration] Created/updated all 6 canonical Earth Orbit MissionData assets (§20).");
        }

        private static void CreateOrUpdateMission(string assetPath, string id, string worldId, int number,
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
                w.FindPropertyRelative("spawnInterval").floatValue = waves[i].spawnInterval;
                w.FindPropertyRelative("fighterRatio").floatValue = waves[i].fighterRatio;
                w.FindPropertyRelative("formation").enumValueIndex = (int)waves[i].formation;
                w.FindPropertyRelative("spawnAsteroid").boolValue = waves[i].spawnAsteroid;
            }

            so.FindProperty("hasBoss").boolValue = hasBoss;
            so.FindProperty("bossPoolKey").stringValue = bossKey;
            so.FindProperty("bossSpawnDelay").floatValue = bossDelay;
            so.FindProperty("starCreditsReward").intValue = rewardCredits;
            so.FindProperty("scoreTarget").intValue = scoreTarget;
            so.FindProperty("isWarpMission").boolValue = isWarp;

            so.ApplyModifiedProperties();
        }

        private static void UpdateScenePoolsAndAudio()
        {
            string scenePath = "Assets/Scenes/SampleScene.unity";
            var activeScene = EditorSceneManager.GetActiveScene();
            if (activeScene.path != scenePath)
            {
                activeScene = EditorSceneManager.OpenScene(scenePath);
            }

            // 1. Setup Audio Manager
            var audioGo = GameObject.Find("AudioManager");
            if (audioGo == null)
            {
                audioGo = new GameObject("AudioManager");
            }
            var audioMgr = audioGo.GetComponent<AudioManager>();
            if (audioMgr == null) audioMgr = audioGo.AddComponent<AudioManager>();

            var audioSO = new SerializedObject(audioMgr);
            audioSO.FindProperty("playerLaserClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/laser1.ogg");
            audioSO.FindProperty("enemyLaserClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/laser2.ogg");
            audioSO.FindProperty("smallExplosionClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/Death Effect.ogg");
            audioSO.FindProperty("largeExplosionClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/lowDown.ogg");
            audioSO.FindProperty("hitClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/spaceTrash1.ogg");
            audioSO.FindProperty("powerUpClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/powerUp1.ogg");
            audioSO.FindProperty("diveBomberSwoopClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/phaserDown1.ogg");
            audioSO.FindProperty("formationEntryClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/phaserUp1.ogg");
            audioSO.FindProperty("waveIncomingClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/threeTone1.ogg");
            audioSO.FindProperty("reinforcementAlertClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/lowThreeTone.ogg");
            audioSO.FindProperty("bossWarningSirenClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/twoTone1.ogg");
            audioSO.FindProperty("bossRoarClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/zapThreeToneDown.ogg");
            audioSO.FindProperty("hazardAlertClip").objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/highDown.ogg");
            audioSO.ApplyModifiedProperties();

            // 2. Setup LevelManager in active scene (§17 & §32)
            var levelMgrGo = GameObject.Find("LevelManager");
            if (levelMgrGo == null)
            {
                levelMgrGo = new GameObject("LevelManager");
            }
            var levelMgr = levelMgrGo.GetComponent<LevelManager>();
            if (levelMgr == null) levelMgr = levelMgrGo.AddComponent<LevelManager>();

            var levelSO = new SerializedObject(levelMgr);
            var m1 = AssetDatabase.LoadAssetAtPath<MissionData>("Assets/ScriptableObjects/Missions/Earth/Earth_Mission_01.asset");
            levelSO.FindProperty("activeMission").objectReferenceValue = m1;

            var earthMissionsProp = levelSO.FindProperty("earthMissions");
            earthMissionsProp.arraySize = 6;
            for (int i = 1; i <= 6; i++)
            {
                var m = AssetDatabase.LoadAssetAtPath<MissionData>($"Assets/ScriptableObjects/Missions/Earth/Earth_Mission_0{i}.asset");
                earthMissionsProp.GetArrayElementAtIndex(i - 1).objectReferenceValue = m;
            }
            levelSO.ApplyModifiedProperties();

            // 3. Setup Object Pools in active scene
            var poolManagerGo = GameObject.Find("PoolManager");
            if (poolManagerGo != null)
            {
                var poolManager = poolManagerGo.GetComponent<ObjectPool>();
                if (poolManager != null)
                {
                    var so = new SerializedObject(poolManager);
                    var initialPools = so.FindProperty("initialPools");

                    SetOrAddPool(initialPools, "PlayerProjectile", "Assets/Prefabs/Projectiles/PlayerProjectile.prefab", 40);
                    SetOrAddPool(initialPools, "EnemyProjectile", "Assets/Prefabs/Projectiles/EnemyProjectile.prefab", 40);
                    SetOrAddPool(initialPools, "ScoutEnemy", "Assets/Prefabs/Enemies/ScoutEnemy.prefab", 20);
                    SetOrAddPool(initialPools, "FighterEnemy", "Assets/Prefabs/Enemies/FighterEnemy.prefab", 15);
                    SetOrAddPool(initialPools, "AsteroidHazard", "Assets/Prefabs/Enemies/AsteroidHazard.prefab", 10);
                    SetOrAddPool(initialPools, "InvaderCarrier", "Assets/Prefabs/Enemies/InvaderCarrier.prefab", 2);
                    SetOrAddPool(initialPools, "ExplosionVFX", "Assets/Prefabs/VFX/ExplosionVFX.prefab", 25);

                    so.ApplyModifiedProperties();
                }
            }

            EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
            EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
            Debug.Log("[AssetIntegration] Scene ObjectPool, LevelManager, and AudioManager updated and saved!");
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
