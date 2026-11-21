#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Starfall.Core;
using Starfall.Enemies;
using Starfall.Player;
using Starfall.Combat;
using Starfall.VFX;

namespace Starfall.Editor
{
    public static class StarfallAssetPipelineSetup
    {
        private const string ASSET_DIR = "Assets/ALienware 2D assets";
        private const string ADDITIVE_MAT_PATH = "Assets/Art/Materials/AdditiveSpriteMaterial.mat";

        [InitializeOnLoadMethod]
        private static void AutoRunOnCompile()
        {
            if (SessionState.GetBool("Starfall_AssetPipeline_Ran_V6", false)) return;
            SessionState.SetBool("Starfall_AssetPipeline_Ran_V6", true);
            EditorApplication.delayCall += () =>
            {
                ProcessAndIntegrateAllAssets();
            };
        }

        [MenuItem("STARFALL/2D Assets/Process & Integrate All Assets", priority = 1)]
        public static void ProcessAndIntegrateAllAssets()
        {
            Debug.Log("<color=cyan>[StarfallAssetPipeline] Beginning full automated 2D asset integration...</color>");

            // Step 1: Configure all Texture Importers as Sprites
            ConfigureAllTextureImporters();

            // Step 2: Ensure Additive Sprite Material
            Material additiveMat = EnsureAdditiveMaterial();

            // Step 3: Setup Warship Sprites & Player Prefab
            SetupPlayerWarshipsAndThrusters(additiveMat);

            // Step 4: Setup Enemy Grunts & Bosses
            SetupEnemiesAndBosses();

            // Step 5: Setup Meteorites & Advanced Hazards
            SetupMeteoritesAndHazards();

            // Step 6: Setup Projectiles & Additive Materials
            SetupProjectiles(additiveMat);

            // Step 7: Setup Power-Ups
            SetupPowerUps();

            // Step 8: Setup Frame-by-Frame Sprite Explosions
            SetupSpriteExplosions(additiveMat);

            // Step 9: Update ObjectPools across all Campaign Scenes
            UpdateScenePools();

            // Step 10: Setup Dynamic Sector Parallax Backgrounds across Scenes
            SetupDynamicSectorParallaxBackgrounds();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("<color=green>[StarfallAssetPipeline] ALL 2D ASSETS, PREFABS, SHADERS, AND SCENES SUCCESSFULLY INTEGRATED!</color>");
        }

        private static void ConfigureAllTextureImporters()
        {
            string[] searchDirs = new[] { ASSET_DIR, "Assets/Resources/Warships", "Assets/Art/Sprites" };
            var guids = AssetDatabase.FindAssets("t:Texture2D", searchDirs);
            int count = 0;

            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!path.EndsWith(".png") && !path.EndsWith(".webp") && !path.EndsWith(".jpg")) continue;

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null) continue;

                bool modified = false;

                if (importer.textureType != TextureImporterType.Sprite)
                {
                    importer.textureType = TextureImporterType.Sprite;
                    modified = true;
                }

                if (importer.spriteImportMode != SpriteImportMode.Single)
                {
                    importer.spriteImportMode = SpriteImportMode.Single;
                    modified = true;
                }

                if (importer.alphaIsTransparency != true)
                {
                    importer.alphaIsTransparency = true;
                    modified = true;
                }

                if (importer.mipmapEnabled != false)
                {
                    importer.mipmapEnabled = false;
                    modified = true;
                }

                float targetPPU = GetTargetPPU(Path.GetFileNameWithoutExtension(path));
                if (Mathf.Abs(importer.spritePixelsPerUnit - targetPPU) > 1f)
                {
                    importer.spritePixelsPerUnit = targetPPU;
                    modified = true;
                }

                if (importer.maxTextureSize < 2048)
                {
                    importer.maxTextureSize = 2048;
                    modified = true;
                }

                if (modified)
                {
                    importer.SaveAndReimport();
                    count++;
                }
            }

            Debug.Log($"[StarfallAssetPipeline] Configured {count} TextureImporters as 2D Sprites with battle-tuned PPU.");
        }

        private static float GetTargetPPU(string name)
        {
            // Grunts: target world width ~1.31 to 1.42 units (bold, readable formation warships)
            if (name.Contains("swarm")) return 1250f; // 1698px -> 1.36 units wide, 0.74 units tall
            if (name.Contains("tank")) return 720f;   // 1024px -> 1.42 units wide, 0.78 units tall

            // Player Warships: target world width ~1.73 to 1.82 units (heroic, clear silhouette, agile)
            if (name.Contains("Viper") || name.Contains("viper")) return 950f;      // 1640px -> 1.73 units wide, 0.70 units tall
            if (name.Contains("Titan") || name.Contains("titan")) return 1450f;     // 1536px -> 1.06 units wide, 1.94 units tall
            if (name.Contains("Specter") || name.Contains("specter")) return 1550f; // 2816px -> 1.82 units wide, 0.99 units tall
            if (name.Contains("Vortex") || name.Contains("tempest")) return 1550f;  // 2816px -> 1.82 units wide, 0.99 units tall
            if (name.Contains("Phoenix") || name.Contains("phoenix")) return 1550f; // 2816px -> 1.82 units wide, 0.99 units tall

            // Mini-Bosses: target world width ~2.36 units (elite heavy gunship centerpiece)
            if (name.Contains("Mini-Boss") || name.Contains("Family")) return 720f; // 1697px -> 2.36 units wide, 1.29 units tall

            // Level Bosses: target world width ~2.8 to 3.1 units, height ~4.0 units (epic capital flagship)
            if (name.Contains("Level Boss") || name.Contains("Boss") || name.Contains("Colossus") || name.Contains("Dreadnought") || name.Contains("HiveQueen")) return 360f;

            // Meteorites:
            if (name.Contains("Large meteorite")) return 1100f;  // 1230px -> 1.12 units
            if (name.Contains("Small meteorite")) return 2200f;  // 1254px -> 0.57 units
            if (name.Contains("Explosivevolatile")) return 1100f; // 1200px -> 1.09 units

            // Bullets & Projectiles:
            if (name.Contains("Bullets")) return 2500f; // 1254px -> 0.50 units
            if (name.Contains("Missiles")) return 2200f; // 1024px -> 0.46 units

            // Power-ups:
            if (name.Contains("upgrade") || name.Contains("Shield") || name.Contains("Bomb") ||
                name.Contains("Wingman") || name.Contains("Homing") || name.Contains("Time") || name.Contains("Magnet")) return 1800f; // High-res 2D vector icons

            return 100f;
        }

        private static Material EnsureAdditiveMaterial()
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(ADDITIVE_MAT_PATH);
            if (mat == null)
            {
                var shader = Shader.Find("Starfall/SpriteAdditive") ??
                             Shader.Find("Mobile/Particles/Additive") ??
                             Shader.Find("Sprites/Default");

                mat = new Material(shader);
                mat.name = "AdditiveSpriteMaterial";

                string dir = Path.GetDirectoryName(ADDITIVE_MAT_PATH);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                AssetDatabase.CreateAsset(mat, ADDITIVE_MAT_PATH);
            }
            return mat;
        }

        private static Sprite LoadSprite(string path)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (sprite != null) return sprite;
            return AssetDatabase.LoadAllAssetsAtPath(path).OfType<Sprite>().FirstOrDefault();
        }

        private static void SetupPlayerWarshipsAndThrusters(Material additiveMat)
        {
            // Ensure Resources/Warships directory exists
            string resDir = "Assets/Resources/Warships";
            if (!Directory.Exists(resDir)) Directory.CreateDirectory(resDir);

            // Copy/Verify 5 warship sprites in Resources
            CopySpriteIfMissing($"{ASSET_DIR}/Player warships/Viper Mk-I.png", $"{resDir}/ship_viper.png");
            CopySpriteIfMissing($"{ASSET_DIR}/Player warships/Crimson Titan.png", $"{resDir}/ship_titan.png");
            CopySpriteIfMissing($"{ASSET_DIR}/Player warships/Phantom Specter.png", $"{resDir}/ship_specter.png");
            CopySpriteIfMissing($"{ASSET_DIR}/Player warships/Tempest Vortex.png", $"{resDir}/ship_tempest.png");
            CopySpriteIfMissing($"{ASSET_DIR}/Player warships/Solaris Phoenix-X.png", $"{resDir}/ship_phoenix.png");

            AssetDatabase.Refresh();

            // Update PlayerShip prefab (Heroic arcade fighter)
            string prefabPath = "Assets/Prefabs/Player/PlayerShip.prefab";
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab != null)
            {
                var root = PrefabUtility.LoadPrefabContents(prefabPath);
                var sr = root.GetComponent<SpriteRenderer>();
                var viperSprite = LoadSprite($"{resDir}/ship_viper.png");
                if (viperSprite != null && sr != null)
                {
                    sr.sprite = viperSprite;
                    sr.sortingOrder = 10;
                }

                // Well-fitted scale for war
                root.transform.localScale = Vector3.one;

                // Add or configure ShipEngineThrusters
                var thrusters = root.GetComponent<ShipEngineThrusters>();
                if (thrusters == null) thrusters = root.AddComponent<ShipEngineThrusters>();

                // Configure BoxCollider for heroic dodging hitbox
                var col = root.GetComponent<BoxCollider2D>();
                if (col != null)
                {
                    col.size = new Vector2(1.45f, 0.55f);
                }

                // Configure Muzzle position
                var muzzleTransform = root.transform.Find("Muzzle");
                if (muzzleTransform != null)
                {
                    muzzleTransform.localPosition = new Vector3(0f, 0.50f, 0f);
                }

                // Configure Thruster positions
                var leftThruster = root.transform.Find("LeftThruster");
                if (leftThruster != null)
                {
                    leftThruster.localPosition = new Vector3(-0.42f, -0.32f, 0f);
                }
                var rightThruster = root.transform.Find("RightThruster");
                if (rightThruster != null)
                {
                    rightThruster.localPosition = new Vector3(0.42f, -0.32f, 0f);
                }

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                PrefabUtility.UnloadPrefabContents(root);
                Debug.Log("[StarfallAssetPipeline] PlayerShip.prefab updated with tuned proportions and dynamic thrusters.");
            }
        }

        private static void CopySpriteIfMissing(string src, string dst)
        {
            if (File.Exists(src) && !File.Exists(dst))
            {
                File.Copy(src, dst, true);
            }
        }

        private static void SetupEnemiesAndBosses()
        {
            // 1. Grunts (Scaled slightly smaller to fit screen and formations cleanly)
            string scoutPath = "Assets/Prefabs/Enemies/ScoutEnemy.prefab";
            UpdateEnemyPrefab(scoutPath, $"{ASSET_DIR}/Enemy Warships/Grunt enemy the swarm type.png", new Vector3(0.82f, 0.82f, 1f), new Vector2(1.10f, 0.55f));

            string fighterPath = "Assets/Prefabs/Enemies/FighterEnemy.prefab";
            UpdateEnemyPrefab(fighterPath, $"{ASSET_DIR}/Enemy Warships/Grunt enemy — the _tank_ type (1).png", new Vector3(0.82f, 0.82f, 1f), new Vector2(1.15f, 0.60f));

            // 2. Bosses (Scaled cleanly to fit well below top HUD within visible bounds)
            // Sector 1 Mini-Boss
            UpdateEnemyPrefab("Assets/Prefabs/Enemies/MiniBossGunship.prefab", $"{ASSET_DIR}/Enemy Warships/Enemy Boss/Mini-Boss Family 1.png", new Vector3(0.88f, 0.88f, 1f), new Vector2(2.10f, 1.10f));

            // Sector 1 Level-Boss (Invader Carrier)
            UpdateEnemyPrefab("Assets/Prefabs/Enemies/InvaderCarrier.prefab", $"{ASSET_DIR}/Enemy Warships/Enemy Boss/Level Boss 1.png", new Vector3(0.88f, 0.88f, 1f), new Vector2(2.70f, 3.90f));

            // Sector 2 Level-Boss (Plasma Dreadnought - New pristine transparent battlecruiser sprite)
            UpdateEnemyPrefab("Assets/Prefabs/Enemies/PlasmaDreadnought.prefab", $"{ASSET_DIR}/Enemy Warships/Enemy Boss/PlasmaDreadnought_Boss.png", new Vector3(0.88f, 0.88f, 1f), new Vector2(2.60f, 2.60f));

            // Sector 3 Level-Boss (Hive Queen)
            UpdateEnemyPrefab("Assets/Prefabs/Enemies/HiveQueenBoss.prefab", $"{ASSET_DIR}/Enemy Warships/Enemy Boss/Level Boss 3 (Levels 7–9).png", new Vector3(0.88f, 0.88f, 1f), new Vector2(2.70f, 3.90f));
        }

        private static void UpdateEnemyPrefab(string prefabPath, string spritePath, Vector3 scale, Vector2 colliderSize)
        {
            if (!File.Exists(prefabPath)) return;

            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            var sr = root.GetComponent<SpriteRenderer>();
            var sprite = LoadSprite(spritePath);
            if (sprite != null && sr != null)
            {
                sr.sprite = sprite;
                sr.color = Color.white;
            }

            root.transform.localScale = scale;

            var boxCol = root.GetComponent<BoxCollider2D>();
            if (boxCol != null)
            {
                boxCol.size = colliderSize;
                boxCol.isTrigger = true;
            }

            var circleCol = root.GetComponent<CircleCollider2D>();
            if (circleCol != null)
            {
                circleCol.radius = Mathf.Min(colliderSize.x, colliderSize.y) * 0.5f;
                circleCol.isTrigger = true;
            }

            // Ensure SectorEnemyTinter on grunts
            if (prefabPath.Contains("Scout") || prefabPath.Contains("Fighter"))
            {
                if (root.GetComponent<SectorEnemyTinter>() == null)
                    root.AddComponent<SectorEnemyTinter>();
            }

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            Debug.Log($"[StarfallAssetPipeline] Updated {Path.GetFileName(prefabPath)} with {Path.GetFileName(spritePath)}");
        }

        private static void SetupMeteoritesAndHazards()
        {
            string largeRockSprite = $"{ASSET_DIR}/Rocks and Meteorites/Large meteorite.png";
            string smallRockSprite = $"{ASSET_DIR}/Rocks and Meteorites/Small meteorite fragment.png";
            string explosiveRockSprite = $"{ASSET_DIR}/Rocks and Meteorites/Explosivevolatile variant.png";

            // 1. Large Asteroid (splits into fragments)
            string largePrefabPath = "Assets/Prefabs/Enemies/AsteroidHazard.prefab";
            if (File.Exists(largePrefabPath))
            {
                var root = PrefabUtility.LoadPrefabContents(largePrefabPath);
                var sr = root.GetComponent<SpriteRenderer>();
                var s = AssetDatabase.LoadAssetAtPath<Sprite>(largeRockSprite);
                if (s != null && sr != null) sr.sprite = s;

                root.transform.localScale = Vector3.one;

                var hazard = root.GetComponent<AsteroidHazard>();
                if (hazard != null)
                {
                    var so = new SerializedObject(hazard);
                    var splitsProp = so.FindProperty("splitsOnDestroy");
                    if (splitsProp != null) splitsProp.boolValue = true;
                    var fragKeyProp = so.FindProperty("fragmentPoolKey");
                    if (fragKeyProp != null) fragKeyProp.stringValue = "AsteroidSmallFragment";
                    so.ApplyModifiedProperties();
                }

                var col = root.GetComponent<CircleCollider2D>();
                if (col == null) col = root.AddComponent<CircleCollider2D>();
                col.isTrigger = true;
                col.radius = 0.55f;

                PrefabUtility.SaveAsPrefabAsset(root, largePrefabPath);
                PrefabUtility.UnloadPrefabContents(root);
            }

            // 2. Small Fragment Prefab (Tuned ~0.50 units wide)
            string smallPrefabPath = "Assets/Prefabs/Enemies/AsteroidSmallFragment.prefab";
            CreateOrUpdateAsteroidVariant(smallPrefabPath, smallRockSprite, 1.0f, 1, 100, false, false);

            // 3. Volatile Explosive Prefab (Tuned ~1.20 units wide)
            string volatilePrefabPath = "Assets/Prefabs/Enemies/AsteroidVolatile.prefab";
            CreateOrUpdateAsteroidVariant(volatilePrefabPath, explosiveRockSprite, 1.0f, 2, 400, false, true);

            Debug.Log("[StarfallAssetPipeline] Configured AsteroidHazard (Large), AsteroidSmallFragment, and AsteroidVolatile prefabs.");
        }

        private static void CreateOrUpdateAsteroidVariant(string prefabPath, string spritePath, float scale, int hp, int score, bool splits, bool explosive)
        {
            GameObject root;
            if (File.Exists(prefabPath))
            {
                root = PrefabUtility.LoadPrefabContents(prefabPath);
            }
            else
            {
                root = new GameObject(Path.GetFileNameWithoutExtension(prefabPath));
                root.layer = 8; // Enemy layer
                var rb = root.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Kinematic;
            }

            var sr = root.GetComponent<SpriteRenderer>();
            if (sr == null) sr = root.AddComponent<SpriteRenderer>();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (sprite != null) sr.sprite = sprite;
            sr.sortingOrder = 7;

            root.transform.localScale = Vector3.one * scale;

            var col = root.GetComponent<CircleCollider2D>();
            if (col == null) col = root.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.5f;

            var hazard = root.GetComponent<AsteroidHazard>();
            if (hazard == null) hazard = root.AddComponent<AsteroidHazard>();

            var so = new SerializedObject(hazard);
            so.FindProperty("maxHealth").intValue = hp;
            so.FindProperty("scoreValue").intValue = score;
            var splitsProp = so.FindProperty("splitsOnDestroy");
            if (splitsProp != null) splitsProp.boolValue = splits;
            var explProp = so.FindProperty("isVolatileExplosive");
            if (explProp != null) explProp.boolValue = explosive;
            so.ApplyModifiedProperties();

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        private static void SetupProjectiles(Material additiveMat)
        {
            // 1. Player Projectile (Additive Glowing Bolt - sleek ~0.6 units long)
            string playerProjPath = "Assets/Prefabs/Projectiles/PlayerProjectile.prefab";
            if (File.Exists(playerProjPath))
            {
                var root = PrefabUtility.LoadPrefabContents(playerProjPath);
                var sr = root.GetComponent<SpriteRenderer>();
                var boltSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ASSET_DIR}/Bullets/Player Bullets.png");
                if (boltSprite != null && sr != null)
                {
                    sr.sprite = boltSprite;
                    sr.material = additiveMat;
                    sr.sortingOrder = 9;
                }
                root.transform.localScale = Vector3.one;

                var col = root.GetComponent<BoxCollider2D>();
                if (col != null) col.size = new Vector2(0.2f, 0.55f);

                PrefabUtility.SaveAsPrefabAsset(root, playerProjPath);
                PrefabUtility.UnloadPrefabContents(root);
            }

            // 2. Enemy Projectile (Boss Missiles & Lasers)
            string enemyProjPath = "Assets/Prefabs/Projectiles/EnemyProjectile.prefab";
            if (File.Exists(enemyProjPath))
            {
                var root = PrefabUtility.LoadPrefabContents(enemyProjPath);
                var sr = root.GetComponent<SpriteRenderer>();
                var missileSprite = AssetDatabase.LoadAssetAtPath<Sprite>($"{ASSET_DIR}/Bullets/Boss Missiles.png");
                if (missileSprite != null && sr != null)
                {
                    sr.sprite = missileSprite;
                    sr.material = additiveMat;
                    sr.sortingOrder = 8;
                }
                root.transform.localScale = Vector3.one;

                var col = root.GetComponent<BoxCollider2D>();
                if (col != null) col.size = new Vector2(0.25f, 0.65f);

                PrefabUtility.SaveAsPrefabAsset(root, enemyProjPath);
                PrefabUtility.UnloadPrefabContents(root);
            }

            Debug.Log("[StarfallAssetPipeline] Configured PlayerProjectile and EnemyProjectile with Additive materials.");
        }

        private static void SetupPowerUps()
        {
            string powerUpDir = "Assets/Prefabs/PowerUps";
            if (!Directory.Exists(powerUpDir)) Directory.CreateDirectory(powerUpDir);

            string weaponIcon = $"{ASSET_DIR}/Power Ups/Weapon upgrade.png";
            string shieldIcon = $"{ASSET_DIR}/Power Ups/Shield.png";
            string bombIcon = $"{ASSET_DIR}/Power Ups/Bomb.png";
            string diamondIcon = "Assets/Art/Sprites/PowerUp_Diamond.png";
            string wingmanIcon = $"{ASSET_DIR}/Power Ups/Wingman Drone.png";
            string missilesIcon = $"{ASSET_DIR}/Power Ups/Homing Missiles.png";
            string timeWarpIcon = $"{ASSET_DIR}/Power Ups/Time Warp.png";
            string magnetIcon = $"{ASSET_DIR}/Power Ups/Magnet.png";

            CreateOrUpdatePowerUpPrefab($"{powerUpDir}/PowerUp_Weapon.prefab", weaponIcon, PowerUpType.WeaponUpgrade);
            CreateOrUpdatePowerUpPrefab($"{powerUpDir}/PowerUp_Shield.prefab", shieldIcon, PowerUpType.ShieldRestore);
            CreateOrUpdatePowerUpPrefab($"{powerUpDir}/PowerUp_Bomb.prefab", bombIcon, PowerUpType.Bomb);
            CreateOrUpdatePowerUpPrefab($"{powerUpDir}/PowerUp_RapidFire.prefab", weaponIcon, PowerUpType.RapidFire);
            CreateOrUpdatePowerUpPrefab($"{powerUpDir}/PowerUp_Score.prefab", diamondIcon, PowerUpType.ScoreBonus);
            CreateOrUpdatePowerUpPrefab($"{powerUpDir}/PowerUp_Wingman.prefab", wingmanIcon, PowerUpType.WingmanDrone);
            CreateOrUpdatePowerUpPrefab($"{powerUpDir}/PowerUp_HomingMissiles.prefab", missilesIcon, PowerUpType.HomingMissiles);
            CreateOrUpdatePowerUpPrefab($"{powerUpDir}/PowerUp_TimeWarp.prefab", timeWarpIcon, PowerUpType.TimeWarp);
            CreateOrUpdatePowerUpPrefab($"{powerUpDir}/PowerUp_Magnet.prefab", magnetIcon, PowerUpType.AttractorMagnet);

            Debug.Log("[StarfallAssetPipeline] Configured all 9 PowerUp prefabs (Weapon, Shield, Bomb, RapidFire, Score, Wingman, Missiles, TimeWarp, Magnet) with dedicated 2D vector sprites.");
        }

        private static void CreateOrUpdatePowerUpPrefab(string prefabPath, string spritePath, PowerUpType type)
        {
            GameObject root;
            if (File.Exists(prefabPath))
            {
                root = PrefabUtility.LoadPrefabContents(prefabPath);
            }
            else
            {
                root = new GameObject(Path.GetFileNameWithoutExtension(prefabPath));
                var rb = root.AddComponent<Rigidbody2D>();
                rb.bodyType = RigidbodyType2D.Kinematic;
            }

            var sr = root.GetComponent<SpriteRenderer>();
            if (sr == null) sr = root.AddComponent<SpriteRenderer>();
            var s = AssetDatabase.LoadAssetAtPath<Sprite>(spritePath);
            if (s != null) sr.sprite = s;
            sr.color = Color.white;
            sr.sortingOrder = 22;

            var col = root.GetComponent<CircleCollider2D>();
            if (col == null) col = root.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = 0.5f;

            var pup = root.GetComponent<PowerUp>();
            if (pup == null) pup = root.AddComponent<PowerUp>();

            var so = new SerializedObject(pup);
            var typeProp = so.FindProperty("type");
            if (typeProp != null) typeProp.enumValueIndex = (int)type;
            var scaleProp = so.FindProperty("baseScale");
            if (scaleProp != null) scaleProp.floatValue = 0.75f;
            so.ApplyModifiedProperties();

            root.transform.localScale = Vector3.one * 0.75f;

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            PrefabUtility.UnloadPrefabContents(root);
        }

        private static void SetupSpriteExplosions(Material additiveMat)
        {
            // Find all sequential explosion frames
            string animFolder = $"{ASSET_DIR}/Explosions";
            var dirs = Directory.GetDirectories(animFolder, "Explosion_animation_expanding_wi*");
            if (dirs.Length == 0) return;

            string targetDir = dirs[0];
            var frameFiles = Directory.GetFiles(targetDir, "*.png")
                .OrderBy(f => f)
                .ToArray();

            if (frameFiles.Length == 0) return;

            var sprites = new List<Sprite>();
            foreach (var f in frameFiles)
            {
                string relPath = f.Replace("\\", "/");
                if (relPath.StartsWith(Application.dataPath))
                {
                    relPath = "Assets" + relPath.Substring(Application.dataPath.Length);
                }
                var s = AssetDatabase.LoadAssetAtPath<Sprite>(relPath);
                if (s != null) sprites.Add(s);
            }

            if (sprites.Count == 0) return;

            Debug.Log($"[StarfallAssetPipeline] Loaded {sprites.Count} explosion animation frames.");

            // 1. Small Explosion Prefab (for grunt destruction) - strictly 1.35m
            string smallExplosionPath = "Assets/Prefabs/VFX/ExplosionVFX.prefab";
            CreateOrUpdateExplosionPrefab(smallExplosionPath, sprites.Take(Mathf.Min(sprites.Count, 30)).ToArray(), 32f, 1.35f, additiveMat);

            // 2. Big Explosion Prefab (for boss destruction) - 3.8m
            string bigExplosionPath = "Assets/Prefabs/VFX/BossExplosionVFX.prefab";
            CreateOrUpdateExplosionPrefab(bigExplosionPath, sprites.ToArray(), 28f, 3.8f, additiveMat);
        }

        private static void CreateOrUpdateExplosionPrefab(string prefabPath, Sprite[] frames, float fps, float targetWorldSize, Material additiveMat)
        {
            GameObject root;
            if (File.Exists(prefabPath))
            {
                root = PrefabUtility.LoadPrefabContents(prefabPath);
            }
            else
            {
                root = new GameObject(Path.GetFileNameWithoutExtension(prefabPath));
            }

            float nativeWidth = (frames != null && frames.Length > 0 && frames[0] != null) ? (frames[0].rect.width / frames[0].pixelsPerUnit) : 19.2f;
            float safeScale = (nativeWidth > 2.0f) ? (targetWorldSize / nativeWidth) : targetWorldSize;

            var sr = root.GetComponent<SpriteRenderer>();
            if (sr == null) sr = root.AddComponent<SpriteRenderer>();
            sr.material = additiveMat;
            sr.sortingOrder = 15;
            sr.enabled = false;

            var animExp = root.GetComponent<AnimatedSpriteExplosion>();
            if (animExp == null) animExp = root.AddComponent<AnimatedSpriteExplosion>();

            animExp.SetFrames(frames, fps, safeScale);

            PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            PrefabUtility.UnloadPrefabContents(root);
            Debug.Log($"[StarfallAssetPipeline] Configured explosion prefab: {Path.GetFileName(prefabPath)} with {frames.Length} Additive frames.");
        }

        private static void UpdateScenePools()
        {
            string[] scenePaths = new[]
            {
                "Assets/Scenes/SampleScene.unity",
                "Assets/Scenes/Level1_EarthOrbit.unity",
                "Assets/Scenes/Level2_AsteroidBelt.unity",
                "Assets/Scenes/Level3_DeepNebula.unity"
            };

            foreach (var scenePath in scenePaths)
            {
                if (!File.Exists(scenePath)) continue;

                var scene = EditorSceneManager.OpenScene(scenePath);
                var poolManagerGo = GameObject.Find("PoolManager");
                if (poolManagerGo != null)
                {
                    var poolManager = poolManagerGo.GetComponent<ObjectPool>();
                    if (poolManager != null)
                    {
                        var so = new SerializedObject(poolManager);
                        var initialPools = so.FindProperty("initialPools");

                        SetOrAddPool(initialPools, "ScoutEnemy", "Assets/Prefabs/Enemies/ScoutEnemy.prefab", 25);
                        SetOrAddPool(initialPools, "FighterEnemy", "Assets/Prefabs/Enemies/FighterEnemy.prefab", 20);
                        SetOrAddPool(initialPools, "MiniBossGunship", "Assets/Prefabs/Enemies/MiniBossGunship.prefab", 2);
                        SetOrAddPool(initialPools, "PlasmaDreadnought", "Assets/Prefabs/Enemies/PlasmaDreadnought.prefab", 2);
                        SetOrAddPool(initialPools, "HiveQueenBoss", "Assets/Prefabs/Enemies/HiveQueenBoss.prefab", 2);
                        SetOrAddPool(initialPools, "InvaderCarrier", "Assets/Prefabs/Enemies/InvaderCarrier.prefab", 2);

                        SetOrAddPool(initialPools, "AsteroidHazard", "Assets/Prefabs/Enemies/AsteroidHazard.prefab", 12);
                        SetOrAddPool(initialPools, "AsteroidSmallFragment", "Assets/Prefabs/Enemies/AsteroidSmallFragment.prefab", 20);
                        SetOrAddPool(initialPools, "AsteroidVolatile", "Assets/Prefabs/Enemies/AsteroidVolatile.prefab", 6);

                        SetOrAddPool(initialPools, "PlayerProjectile", "Assets/Prefabs/Projectiles/PlayerProjectile.prefab", 40);
                        SetOrAddPool(initialPools, "EnemyProjectile", "Assets/Prefabs/Projectiles/EnemyProjectile.prefab", 40);
                        SetOrAddPool(initialPools, "BossBomb_PlasmaCore", "Assets/Prefabs/Projectiles/BossBomb_PlasmaCore.prefab", 4);
                        SetOrAddPool(initialPools, "BossBomb_AcidSpore", "Assets/Prefabs/Projectiles/BossBomb_AcidSpore.prefab", 6);
                        SetOrAddPool(initialPools, "BossBomb_EmpTorpedo", "Assets/Prefabs/Projectiles/BossBomb_EmpTorpedo.prefab", 4);
                        SetOrAddPool(initialPools, "ExplosionVFX", "Assets/Prefabs/VFX/ExplosionVFX.prefab", 20);
                        SetOrAddPool(initialPools, "BossExplosionVFX", "Assets/Prefabs/VFX/BossExplosionVFX.prefab", 4);

                        SetOrAddPool(initialPools, "PowerUp_Weapon", "Assets/Prefabs/PowerUps/PowerUp_Weapon.prefab", 6);
                        SetOrAddPool(initialPools, "PowerUp_Shield", "Assets/Prefabs/PowerUps/PowerUp_Shield.prefab", 6);
                        SetOrAddPool(initialPools, "PowerUp_Bomb", "Assets/Prefabs/PowerUps/PowerUp_Bomb.prefab", 6);
                        SetOrAddPool(initialPools, "PowerUp_RapidFire", "Assets/Prefabs/PowerUps/PowerUp_RapidFire.prefab", 6);
                        SetOrAddPool(initialPools, "PowerUp_Score", "Assets/Prefabs/PowerUps/PowerUp_Score.prefab", 10);
                        SetOrAddPool(initialPools, "PowerUp_Wingman", "Assets/Prefabs/PowerUps/PowerUp_Wingman.prefab", 6);
                        SetOrAddPool(initialPools, "PowerUp_HomingMissiles", "Assets/Prefabs/PowerUps/PowerUp_HomingMissiles.prefab", 6);
                        SetOrAddPool(initialPools, "PowerUp_TimeWarp", "Assets/Prefabs/PowerUps/PowerUp_TimeWarp.prefab", 6);
                        SetOrAddPool(initialPools, "PowerUp_Magnet", "Assets/Prefabs/PowerUps/PowerUp_Magnet.prefab", 6);

                        so.ApplyModifiedProperties();
                    }
                }

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            Debug.Log("[StarfallAssetPipeline] Saved updated ObjectPools across all campaign scenes.");
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
        private static void SetupDynamicSectorParallaxBackgrounds()
        {
            string s1Path = "Assets/Resources/Backgrounds/Orbital Eclipse & Dark Atmospheric Horizon (Orbit Sector).png";
            string s2Path = "Assets/Resources/Backgrounds/Abyssal Asteroid Field in Deep Shadow (Asteroid Belt _ Sector 1).png";
            string s3Path = "Assets/Resources/Backgrounds/Dark Obsidian Deep Nebula (Mystic _ Sector 2).png";
            string starsPath = "Assets/Resources/Backgrounds/Starfield.png";
            if (!File.Exists(starsPath)) starsPath = "Assets/Art/Sprites/Backgrounds/Stars-Big_1.png";

            var s1 = AssetDatabase.LoadAssetAtPath<Sprite>(s1Path);
            var s2 = AssetDatabase.LoadAssetAtPath<Sprite>(s2Path);
            var s3 = AssetDatabase.LoadAssetAtPath<Sprite>(s3Path);
            var stars = AssetDatabase.LoadAssetAtPath<Sprite>(starsPath);

            string[] scenePaths = new[]
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
                var bgGo = GameObject.Find("ParallaxBackground");
                if (bgGo == null)
                {
                    bgGo = new GameObject("ParallaxBackground");
                }

                var controller = bgGo.GetComponent<ParallaxController>();
                if (controller == null) controller = bgGo.AddComponent<ParallaxController>();
                controller.DefaultDirection = ParallaxDirection.TopToBottom;

                controller.ConfigureSectorSprites(s1, s2, s3, stars);

                // Build / ensure 3 vertical parallax layers with full coverage (worldWidth = 20, tileHeight = 11.25)
                EnsureParallaxLayer(bgGo.transform, "Layer_FarNebula", s1, 0.65f, -25, 20f, 11.25f);
                EnsureParallaxLayer(bgGo.transform, "Layer_MidStars", stars, 1.85f, -20, 20f, 11.25f);
                EnsureParallaxLayer(bgGo.transform, "Layer_NearStars", stars, 4.2f, -15, 20f, 11.25f);

                controller.CacheLayersAndRenderers();

                int sector = 1;
                if (scenePath.Contains("Level4") || scenePath.Contains("WarTorn")) sector = 4;
                else if (scenePath.Contains("Level3") || scenePath.Contains("Nebula")) sector = 3;
                else if (scenePath.Contains("Level2") || scenePath.Contains("Asteroid")) sector = 2;
                controller.ApplySector(sector);

                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            Debug.Log("[StarfallAssetPipeline] Configured 3-Layer Dynamic Sector Parallax Backgrounds (Top to Down) across all campaign scenes.");
        }

        private static void EnsureParallaxLayer(Transform parent, string layerName, Sprite sprite, float speed, int sortingOrder, float width, float height)
        {
            var layerT = parent.Find(layerName);
            GameObject layerGo;
            if (layerT == null)
            {
                layerGo = new GameObject(layerName);
                layerGo.transform.SetParent(parent, false);
            }
            else
            {
                layerGo = layerT.gameObject;
            }

            var tileA = GetOrCreateTile($"{layerName}_A", layerGo.transform, sprite, 0f, 0f, sortingOrder, width, height);
            var tileB = GetOrCreateTile($"{layerName}_B", layerGo.transform, sprite, 0f, height, sortingOrder, width, height);

            var pl = layerGo.GetComponent<ParallaxLayer>();
            if (pl == null) pl = layerGo.AddComponent<ParallaxLayer>();
            pl.Initialize(tileA, tileB, width, height, speed, ParallaxDirection.TopToBottom);
        }

        private static Transform GetOrCreateTile(string name, Transform parent, Sprite sprite, float offsetX, float offsetY, int sortingOrder, float width, float height)
        {
            var t = parent.Find(name);
            GameObject go;
            if (t == null)
            {
                go = new GameObject(name);
                go.transform.SetParent(parent, false);
            }
            else
            {
                go = t.gameObject;
            }

            go.transform.localPosition = new Vector3(offsetX, offsetY, 0f);

            var sr = go.GetComponent<SpriteRenderer>();
            if (sr == null) sr = go.AddComponent<SpriteRenderer>();
            if (sprite != null) sr.sprite = sprite;
            sr.sortingOrder = sortingOrder;

            if (sprite != null)
            {
                float spriteW = sprite.rect.width > 0 ? sprite.rect.width / sprite.pixelsPerUnit : 1f;
                float spriteH = sprite.rect.height > 0 ? sprite.rect.height / sprite.pixelsPerUnit : 1f;
                go.transform.localScale = new Vector3(width / spriteW, height / spriteH, 1f);
            }

            return go.transform;
        }
    }
}
#endif
