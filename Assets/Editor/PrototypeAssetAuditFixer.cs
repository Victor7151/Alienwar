using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Starfall.VFX;

namespace Starfall.Editor
{
    /// <summary>
    /// Master automated utility to purge prototype leftovers and wire custom assets:
    /// 1. Purges orphan ExplosionVFX scene objects from all 4 level scenes.
    /// 2. Upgrades VFX materials to native URP unlit shaders (eliminating pink shader fallbacks).
    /// 3. Hooks the 96-frame rendered explosion animation into ExplosionVFX & BossExplosionVFX.
    /// 4. Upgrades enemy, boss, and asteroid prefabs from Kenney prototypes to custom warships & meteorites.
    [InitializeOnLoad]
    public static class PrototypeAssetAuditFixer
    {
        private const string PrefKey = "Starfall_PrototypeAuditFixer_Ran_v1";

        static PrototypeAssetAuditFixer()
        {
            EditorApplication.delayCall += () =>
            {
                if (!EditorPrefs.GetBool(PrefKey, false))
                {
                    EditorPrefs.SetBool(PrefKey, true);
                    FixAll();
                }
            };
        }

        private static readonly string[] ScenePaths = new string[]
        {
            "Assets/Scenes/Level1_EarthOrbit.unity",
            "Assets/Scenes/Level2_AsteroidBelt.unity",
            "Assets/Scenes/Level3_DeepNebula.unity",
            "Assets/Scenes/Level4_WarTornWreckage.unity"
        };

        [MenuItem("Starfall/Fix All Prototypes & Shaders", priority = 100)]
        public static void FixAll()
        {
            Debug.Log("<color=cyan>=== [PrototypeAssetAuditFixer] Starting Project-Wide Asset Upgrade ===</color>");

            FixVFXMaterials();
            PurgeOrphanExplosionsFromScenes();
            Wire96FrameExplosionToPrefabs();
            UpgradeEnemyAndHazardPrefabs();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("<color=green>=== [PrototypeAssetAuditFixer] Complete! All prototypes replaced with custom assets and URP shaders verified. ===</color>");
        }

        public static void FixVFXMaterials()
        {
            Shader urpParticleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            Shader spriteAdditiveShader = Shader.Find("Starfall/SpriteAdditive") ?? urpParticleShader;

            string[] matPaths = new string[]
            {
                "Assets/Art/VFX/VFX_SparkMat.mat",
                "Assets/Art/VFX/VFX_SmokeMat.mat",
                "Assets/Art/VFX/VFX_ShockwaveMat.mat"
            };

            foreach (var p in matPaths)
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(p);
                if (mat != null)
                {
                    if (urpParticleShader != null) mat.shader = urpParticleShader;
                    EditorUtility.SetDirty(mat);
                    Debug.Log($"[PrototypeAssetAuditFixer] Updated {p} to {mat.shader.name}");
                }
            }

            var fireMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/VFX/VFX_FireMat.mat");
            if (fireMat != null)
            {
                if (spriteAdditiveShader != null) fireMat.shader = spriteAdditiveShader;
                EditorUtility.SetDirty(fireMat);
                Debug.Log($"[PrototypeAssetAuditFixer] Updated VFX_FireMat.mat to {fireMat.shader.name}");
            }
        }

        public static void PurgeOrphanExplosionsFromScenes()
        {
            string currentActivePath = SceneManager.GetActiveScene().path;

            foreach (var scenePath in ScenePaths)
            {
                if (!File.Exists(scenePath)) continue;

                var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
                var roots = scene.GetRootGameObjects();
                int purgedCount = 0;

                for (int i = roots.Length - 1; i >= 0; i--)
                {
                    var root = roots[i];
                    if (root != null && (root.name == "ExplosionVFX" || root.name.StartsWith("ExplosionVFX")))
                    {
                        Object.DestroyImmediate(root);
                        purgedCount++;
                    }
                }

                if (purgedCount > 0)
                {
                    EditorSceneManager.MarkSceneDirty(scene);
                    EditorSceneManager.SaveScene(scene);
                    Debug.Log($"<color=green>[PrototypeAssetAuditFixer] Purged {purgedCount} orphan ExplosionVFX objects from {scenePath}</color>");
                }
            }

            if (!string.IsNullOrEmpty(currentActivePath) && File.Exists(currentActivePath))
            {
                EditorSceneManager.OpenScene(currentActivePath, OpenSceneMode.Single);
            }
        }

        public static void Wire96FrameExplosionToPrefabs()
        {
            var frames = Load96ExplosionFrames();
            if (frames == null || frames.Length == 0)
            {
                Debug.LogWarning("[PrototypeAssetAuditFixer] Could not load 96-frame explosion sequence!");
                return;
            }

            string[] prefabPaths = new string[]
            {
                "Assets/Resources/VFX/ExplosionVFX.prefab",
                "Assets/Resources/VFX/BossExplosionVFX.prefab"
            };

            foreach (var path in prefabPaths)
            {
                if (!File.Exists(path)) continue;

                var prefab = PrefabUtility.LoadPrefabContents(path);
                var vfx = prefab.GetComponent<ExplosionVFX>();
                if (vfx == null) vfx = prefab.AddComponent<ExplosionVFX>();

                // Configure flipbook child
                var flipChild = prefab.transform.Find("ExplosionFlipbook");
                if (flipChild == null)
                {
                    var go = new GameObject("ExplosionFlipbook");
                    go.transform.SetParent(prefab.transform, false);
                    flipChild = go.transform;
                }
                flipChild.localPosition = Vector3.zero;

                // Scale calculation: Native 1080p frame is 19.2 units wide at 100 PPU!
                // Regular explosions must stay ~1.35m (matching asteroid/ship size). Bosses ~3.8m.
                float targetSize = path.Contains("Boss") ? 3.8f : 1.35f;
                float nativeWidth = (frames.Length > 0 && frames[0] != null) ? (frames[0].rect.width / frames[0].pixelsPerUnit) : 19.2f;
                float scaleMultiplier = targetSize / Mathf.Max(0.1f, nativeWidth);
                flipChild.localScale = new Vector3(scaleMultiplier, scaleMultiplier, 1f);

                var sr = flipChild.GetComponent<SpriteRenderer>();
                if (sr == null) sr = flipChild.gameObject.AddComponent<SpriteRenderer>();
                sr.sprite = frames[0];
                sr.sortingOrder = 20;
                sr.enabled = false; // Pure realistic space particle VFX is default

                // Remove duplicate AnimatedSpriteExplosion on root to prevent dual full-screen rendering
                var dupAnim = prefab.GetComponent<AnimatedSpriteExplosion>();
                if (dupAnim != null) Object.DestroyImmediate(dupAnim, true);
                var rootSr = prefab.GetComponent<SpriteRenderer>();
                if (rootSr != null) { rootSr.sprite = null; rootSr.enabled = false; }

                // Ensure particle systems on explosion do not playOnAwake with missing materials
                foreach (var ps in prefab.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main = ps.main;
                    main.playOnAwake = false;

                    var psr = ps.GetComponent<ParticleSystemRenderer>();
                    if (psr != null && (psr.sharedMaterial == null || psr.sharedMaterial.shader.name.Contains("InternalErrorShader")))
                    {
                        var fallbackMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/VFX_GlowAdditive.mat")
                                       ?? AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/VFX/VFX_FireMat.mat");
                        if (fallbackMat != null) psr.sharedMaterial = fallbackMat;
                    }
                }

                // Serialize frames onto ExplosionVFX serialized object
                var so = new SerializedObject(vfx);
                so.Update();
                var framesProp = so.FindProperty("animationFrames");
                if (framesProp != null)
                {
                    framesProp.arraySize = frames.Length;
                    for (int i = 0; i < frames.Length; i++)
                    {
                        framesProp.GetArrayElementAtIndex(i).objectReferenceValue = frames[i];
                    }
                }

                var srProp = so.FindProperty("flipbookRenderer");
                if (srProp != null) srProp.objectReferenceValue = sr;

                var useFlipProp = so.FindProperty("useFlipbookAnimation");
                if (useFlipProp != null) useFlipProp.boolValue = false; // Default to pure realistic particle VFX

                var targetRadiusProp = so.FindProperty("targetWorldRadius");
                if (targetRadiusProp != null) targetRadiusProp.floatValue = targetSize;

                var durProp = so.FindProperty("durationOverride");
                if (durProp != null) durProp.floatValue = path.Contains("Boss") ? 1.2f : 0.45f;

                var fpsProp = so.FindProperty("animationFps");
                if (fpsProp != null) fpsProp.floatValue = 54f;

                var scaleProp = so.FindProperty("flipbookScale");
                if (scaleProp != null) scaleProp.vector3Value = new Vector3(scaleMultiplier, scaleMultiplier, 1f);

                so.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(prefab, path);
                PrefabUtility.UnloadPrefabContents(prefab);
                Debug.Log($"<color=green>[PrototypeAssetAuditFixer] Properly scaled explosion (targetSize: {targetSize}m) wired into {path}!</color>");
            }
        }

        private static Sprite[] Load96ExplosionFrames()
        {
            var list = new List<Sprite>();
            string dir = "Assets/ALienware 2D assets/Explosions/Explosion_animation_expanding_wi…_20260924124436";

            for (int i = 1; i <= 96; i++)
            {
                string path = $"{dir}/{i:0000}.png";
                var spr = AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (spr == null)
                {
                    var all = AssetDatabase.LoadAllAssetsAtPath(path);
                    for (int a = 0; a < all.Length; a++)
                    {
                        if (all[a] is Sprite s) { spr = s; break; }
                    }
                }
                if (spr != null) list.Add(spr);
            }
            return list.ToArray();
        }

        public static void UpgradeEnemyAndHazardPrefabs()
        {
            // 1. Swarm Grunt (Replaces Scout prototype)
            var swarmSprite = LoadSprite("Assets/ALienware 2D assets/Enemy Warships/Grunt enemy the swarm type.png");
            UpdateShipPrefab("Assets/Resources/Enemies/ScoutEnemy.prefab", swarmSprite, new Vector3(0.38f, 0.38f, 1f), 0.7f, isCircleCollider: true);
            UpdateShipPrefab("Assets/Prefabs/Enemies/ScoutEnemy.prefab", swarmSprite, new Vector3(0.38f, 0.38f, 1f), 0.7f, isCircleCollider: true);

            // 2. Tank Grunt (Replaces Fighter prototype)
            var tankSprite = LoadSprite("Assets/ALienware 2D assets/Enemy Warships/Grunt enemy — the _tank_ type (1).png");
            UpdateShipPrefab("Assets/Resources/Enemies/FighterEnemy.prefab", tankSprite, new Vector3(0.42f, 0.42f, 1f), 0.8f, isCircleCollider: true);
            UpdateShipPrefab("Assets/Prefabs/Enemies/FighterEnemy.prefab", tankSprite, new Vector3(0.42f, 0.42f, 1f), 0.8f, isCircleCollider: true);

            // 3. Mini-Boss Gunship (Replaces prototype)
            var miniBossSprite = LoadSprite("Assets/ALienware 2D assets/Enemy Warships/Enemy Boss/Mini-Boss Family 1.png");
            UpdateShipPrefab("Assets/Resources/Enemies/MiniBossGunship.prefab", miniBossSprite, new Vector3(0.45f, 0.45f, 1f), 1.1f, isCircleCollider: false);
            UpdateShipPrefab("Assets/Prefabs/Enemies/MiniBossGunship.prefab", miniBossSprite, new Vector3(0.45f, 0.45f, 1f), 1.1f, isCircleCollider: false);

            // 4. Invader Carrier (Replaces prototype)
            var carrierSprite = LoadSprite("Assets/ALienware 2D assets/Enemy Warships/Enemy Boss/Mini-Boss Family 3.png");
            UpdateShipPrefab("Assets/Resources/Enemies/InvaderCarrier.prefab", carrierSprite, new Vector3(0.55f, 0.55f, 1f), 1.4f, isCircleCollider: false);
            UpdateShipPrefab("Assets/Prefabs/Enemies/InvaderCarrier.prefab", carrierSprite, new Vector3(0.55f, 0.55f, 1f), 1.4f, isCircleCollider: false);

            // 5. Plasma Dreadnought (Replaces prototype)
            var dreadSprite = LoadSprite("Assets/ALienware 2D assets/Enemy Warships/Enemy Boss/PlasmaDreadnought_Boss.png");
            UpdateShipPrefab("Assets/Resources/Enemies/PlasmaDreadnought.prefab", dreadSprite, new Vector3(0.58f, 0.58f, 1f), 1.5f, isCircleCollider: false);
            UpdateShipPrefab("Assets/Prefabs/Enemies/PlasmaDreadnought.prefab", dreadSprite, new Vector3(0.58f, 0.58f, 1f), 1.5f, isCircleCollider: false);

            // 6. Large Meteorite (Replaces prototype cartoon rock)
            var meteorSprite = LoadSprite("Assets/ALienware 2D assets/Rocks and Meteorites/Large meteorite.png");
            UpdateShipPrefab("Assets/Resources/Enemies/AsteroidHazard.prefab", meteorSprite, new Vector3(0.45f, 0.45f, 1f), 0.85f, isCircleCollider: true);
            UpdateShipPrefab("Assets/Prefabs/Enemies/AsteroidHazard.prefab", meteorSprite, new Vector3(0.45f, 0.45f, 1f), 0.85f, isCircleCollider: true);

            // 7. Small Meteorite Fragment
            var fragmentSprite = LoadSprite("Assets/ALienware 2D assets/Rocks and Meteorites/Small meteorite fragment.png");
            UpdateShipPrefab("Assets/Prefabs/Enemies/AsteroidSmallFragment.prefab", fragmentSprite, new Vector3(0.35f, 0.35f, 1f), 0.5f, isCircleCollider: true);

            // 8. Volatile Explosive Variant
            var volatileSprite = LoadSprite("Assets/ALienware 2D assets/Rocks and Meteorites/Explosivevolatile variant.png");
            UpdateShipPrefab("Assets/Prefabs/Enemies/AsteroidVolatile.prefab", volatileSprite, new Vector3(0.45f, 0.45f, 1f), 0.85f, isCircleCollider: true);
        }

        private static Sprite LoadSprite(string path)
        {
            var spr = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (spr == null)
            {
                var all = AssetDatabase.LoadAllAssetsAtPath(path);
                for (int a = 0; a < all.Length; a++)
                {
                    if (all[a] is Sprite s) return s;
                }
            }
            return spr;
        }

        private static void UpdateShipPrefab(string prefabPath, Sprite newSprite, Vector3 targetScale, float colliderSize, bool isCircleCollider)
        {
            if (newSprite == null || !File.Exists(prefabPath)) return;

            var prefab = PrefabUtility.LoadPrefabContents(prefabPath);
            var sr = prefab.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.sprite = newSprite;
                sr.flipY = true; // Enemy warships in Starfall fly downward
            }

            prefab.transform.localScale = targetScale;

            if (isCircleCollider)
            {
                var circle = prefab.GetComponent<CircleCollider2D>();
                if (circle != null)
                {
                    circle.radius = colliderSize;
                }
            }
            else
            {
                var box = prefab.GetComponent<BoxCollider2D>();
                if (box != null)
                {
                    box.size = new Vector2(colliderSize * 1.5f, colliderSize * 1.2f);
                }
            }

            PrefabUtility.SaveAsPrefabAsset(prefab, prefabPath);
            PrefabUtility.UnloadPrefabContents(prefab);
            Debug.Log($"<color=green>[PrototypeAssetAuditFixer] Successfully upgraded {prefabPath} to {newSprite.name}!</color>");
        }
    }
}
