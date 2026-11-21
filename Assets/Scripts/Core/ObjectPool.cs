using System.Collections.Generic;
using UnityEngine;

namespace Starfall.Core
{
    /// <summary>
    /// High-performance, zero-allocation object pooler for projectiles, enemies, and VFX.
    /// Eliminates garbage collection spikes on mobile (Section 35 & 48 of GDD).
    /// </summary>
    public class ObjectPool : MonoBehaviour
    {
        public static ObjectPool Instance { get; private set; }

        [System.Serializable]
        public class PoolItem
        {
            public string poolKey;
            public GameObject prefab;
            public int initialSize = 20;
        }

        [SerializeField] private List<PoolItem> initialPools = new List<PoolItem>();

        private readonly Dictionary<string, Queue<GameObject>> poolQueues = new Dictionary<string, Queue<GameObject>>();
        private readonly Dictionary<string, GameObject> keyToPrefab = new Dictionary<string, GameObject>();
        private readonly Dictionary<string, int> createdCount = new Dictionary<string, int>();
        private readonly Dictionary<string, int> expansionsCount = new Dictionary<string, int>();

        /// <summary>Per-pool numbers for the performance monitor: created, currently active, runtime expansions.</summary>
        public struct PoolStat { public string key; public int created; public int active; public int expansions; }

        public void GetStats(List<PoolStat> into)
        {
            into.Clear();
            foreach (var kv in poolQueues)
            {
                createdCount.TryGetValue(kv.Key, out int created);
                expansionsCount.TryGetValue(kv.Key, out int exp);
                into.Add(new PoolStat { key = kv.Key, created = created, active = created - kv.Value.Count, expansions = exp });
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;

            // Register pool metadata immediately (no instantiation yet) so
            // Spawn() knows which keys are valid right away.
            foreach (var item in initialPools)
            {
                if (string.IsNullOrEmpty(item.poolKey) || item.prefab == null) continue;
                if (!poolQueues.ContainsKey(item.poolKey))
                {
                    poolQueues[item.poolKey] = new Queue<GameObject>();
                    keyToPrefab[item.poolKey] = item.prefab;
                }
            }

            // Spread the actual instantiation across multiple frames to avoid a
            // multi-second freeze at scene start (previously all objects were
            // Instantiated synchronously in one Awake call).
            StartCoroutine(PrewarmCoroutine());
        }

        private System.Collections.IEnumerator PrewarmCoroutine()
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            const double maxMsPerFrame = 1.5; // Strict 1.5ms budget per frame to preserve solid 60 FPS

            foreach (var item in initialPools)
            {
                if (string.IsNullOrEmpty(item.poolKey) || item.prefab == null) continue;

                var queue = poolQueues[item.poolKey];
                int targetCount = GetStartupPrewarmCap(item.poolKey, item.initialSize);

                for (int i = 0; i < targetCount; i++)
                {
                    GameObject obj = Instantiate(item.prefab, transform);
                    var tracker = obj.GetComponent<PooledInstance>();
                    if (tracker == null) tracker = obj.AddComponent<PooledInstance>();
                    tracker.PoolKey = item.poolKey;
                    obj.SetActive(false);
                    queue.Enqueue(obj);
                    createdCount[item.poolKey] = (createdCount.TryGetValue(item.poolKey, out int c) ? c : 0) + 1;

                    if (sw.Elapsed.TotalMilliseconds >= maxMsPerFrame)
                    {
                        yield return null;
                        sw.Restart();
                    }
                }
            }
        }

        /// <summary>
        /// Caps the startup prewarm count to prevent hundreds of objects from freezing the scene start.
        /// Any additional instances needed at runtime expand automatically on demand with zero lag.
        /// </summary>
        private int GetStartupPrewarmCap(string key, int authoredCount)
        {
            if (string.IsNullOrEmpty(key) || authoredCount <= 0) return 0;

            // Bosses and late-game boss weapons/explosions: 0 at start (they appear only at the climax).
            if (key.Contains("Boss") || key == "PlasmaDreadnought" || key == "InvaderCarrier" || key.StartsWith("BossBomb_"))
                return 0;

            // Asteroids: only prewarm if active scene is an asteroid level.
            if (key.StartsWith("Asteroid"))
            {
                bool isAsteroidScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name.Contains("Asteroid");
                return isAsteroidScene ? Mathf.Min(authoredCount, 4) : 0;
            }

            return key switch
            {
                "PlayerProjectile" => Mathf.Min(authoredCount, 32),
                "EnemyProjectile" => Mathf.Min(authoredCount, 32),
                "ScoutEnemy" => Mathf.Min(authoredCount, 16),
                "FighterEnemy" => Mathf.Min(authoredCount, 12),
                "ExplosionVFX" => Mathf.Min(authoredCount, 30),
                "BossExplosionVFX" => Mathf.Min(authoredCount, 4),
                "AlienExplosionVFX" => Mathf.Min(authoredCount, 8),
                "NukeDetonationVFX" => Mathf.Min(authoredCount, 4),
                "MuzzleFlashVFX" => Mathf.Min(authoredCount, 8),
                "HitSparkMetalVFX" => Mathf.Min(authoredCount, 24),
                "HitSparkRockVFX" => Mathf.Min(authoredCount, 16),
                "HitSparkAlienVFX" => Mathf.Min(authoredCount, 12),
                "ShockwaveVFX" => Mathf.Min(authoredCount, 6),
                _ when key.StartsWith("PowerUp_") => Mathf.Min(authoredCount, 4),
                _ => Mathf.Min(authoredCount, 8)
            };
        }

        /// <summary>
        /// Lazily prewarms additional instances of a specific pool in the background (e.g. boss before a boss fight).
        /// </summary>
        public void PrewarmPool(string key, int count)
        {
            if (string.IsNullOrEmpty(key) || count <= 0) return;
            StartCoroutine(PrewarmSpecificPoolAsync(key, count));
        }

        private System.Collections.IEnumerator PrewarmSpecificPoolAsync(string key, int count)
        {
            if (!poolQueues.TryGetValue(key, out var queue) || !keyToPrefab.TryGetValue(key, out var prefab) || prefab == null)
                yield break;

            var sw = System.Diagnostics.Stopwatch.StartNew();
            for (int i = 0; i < count; i++)
            {
                GameObject obj = Instantiate(prefab, transform);
                var tracker = obj.GetComponent<PooledInstance>();
                if (tracker == null) tracker = obj.AddComponent<PooledInstance>();
                tracker.PoolKey = key;
                obj.SetActive(false);
                queue.Enqueue(obj);
                createdCount[key] = (createdCount.TryGetValue(key, out int c) ? c : 0) + 1;

                if (sw.Elapsed.TotalMilliseconds >= 1.5)
                {
                    yield return null;
                    sw.Restart();
                }
            }
        }

        public void RegisterPool(string key, GameObject prefab, int initialCount)
        {
            if (string.IsNullOrEmpty(key) || prefab == null) return;

            if (!poolQueues.ContainsKey(key))
            {
                poolQueues[key] = new Queue<GameObject>();
                keyToPrefab[key] = prefab;
            }

            // Instantiate synchronously when called at runtime (not at startup).
            var queue = poolQueues[key];
            for (int i = 0; i < initialCount; i++)
            {
                GameObject obj = Instantiate(prefab, transform);
                var tracker = obj.GetComponent<PooledInstance>();
                if (tracker == null) tracker = obj.AddComponent<PooledInstance>();
                tracker.PoolKey = key;
                obj.SetActive(false);
                queue.Enqueue(obj);
                createdCount[key] = (createdCount.TryGetValue(key, out int c) ? c : 0) + 1;
            }
        }

        private readonly System.Collections.Generic.HashSet<string> failedKeys = new System.Collections.Generic.HashSet<string>();

        public bool HasPool(string key) => poolQueues.ContainsKey(key);

        public GameObject Spawn(string key, Vector3 position, Quaternion rotation)
        {
            using var _ = PerfMarkers.PoolSpawn.Auto();
            if (failedKeys.Contains(key)) return null;

            if (!poolQueues.TryGetValue(key, out var queue))
            {
                // Dynamic fallback: attempt to load from Resources if pool wasn't registered
                GameObject fallbackPrefab = Resources.Load<GameObject>($"Enemies/{key}") ??
                                            Resources.Load<GameObject>($"Prefabs/Enemies/{key}") ??
                                            Resources.Load<GameObject>($"Prefabs/PowerUps/{key}") ??
                                            Resources.Load<GameObject>($"PowerUps/{key}") ??
                                            Resources.Load<GameObject>($"Prefabs/VFX/{key}") ??
                                            Resources.Load<GameObject>($"VFX/{key}") ??
                                            Resources.Load<GameObject>(key);

#if UNITY_EDITOR
                if (fallbackPrefab == null)
                {
                    fallbackPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/PowerUps/{key}.prefab") ??
                                     UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Enemies/{key}.prefab") ??
                                     UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/Projectiles/{key}.prefab") ??
                                     UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>($"Assets/Prefabs/VFX/{key}.prefab");
                }
#endif

                if (fallbackPrefab == null)
                {
                    if (key.Contains("Explosion") || key.Contains("Nuke"))
                    {
                        fallbackPrefab = Resources.Load<GameObject>("VFX/ExplosionVFX") ??
                                         Resources.Load<GameObject>("Prefabs/VFX/ExplosionVFX");
                    }
                    else if (key.Contains("HitSpark"))
                    {
                        fallbackPrefab = Resources.Load<GameObject>("VFX/HitSparkMetalVFX") ??
                                         Resources.Load<GameObject>("Prefabs/VFX/HitSparkMetalVFX");
                    }
                }

                if (fallbackPrefab != null)
                {
                    RegisterPool(key, fallbackPrefab, 3);
                    queue = poolQueues[key];
                }
                else
                {
                    failedKeys.Add(key);
                    Debug.LogWarning($"[ObjectPool] Pool with key '{key}' not found! Disabling further lookups for this key to prevent lag.");
                    return null;
                }
            }

            GameObject obj;
            if (queue.Count > 0)
            {
                obj = queue.Dequeue();
            }
            else
            {
                // Expand pool automatically if exhausted (counted: steady growth means objects are not returned)
                obj = Instantiate(keyToPrefab[key], transform);
                createdCount[key] = (createdCount.TryGetValue(key, out int c) ? c : 0) + 1;
                expansionsCount[key] = (expansionsCount.TryGetValue(key, out int e) ? e : 0) + 1;
                var tracker = obj.GetComponent<PooledInstance>();
                if (tracker == null) tracker = obj.AddComponent<PooledInstance>();
                tracker.PoolKey = key;
            }

            obj.transform.position = position;
            obj.transform.rotation = rotation;
            obj.SetActive(true);

            var pooledObj = obj.GetComponent<IPooledObject>();
            if (pooledObj != null)
            {
                pooledObj.OnSpawn();
            }

            return obj;
        }

        public void Despawn(GameObject obj)
        {
            if (obj == null) return;

            if (obj.TryGetComponent<PooledInstance>(out var tracker) && !string.IsNullOrEmpty(tracker.PoolKey))
            {
                if (poolQueues.TryGetValue(tracker.PoolKey, out var queue))
                {
                    var pooledObj = obj.GetComponent<IPooledObject>();
                    if (pooledObj != null)
                    {
                        pooledObj.OnDespawn();
                    }

                    obj.SetActive(false);
                    obj.transform.SetParent(transform);
                    queue.Enqueue(obj);
                    return;
                }
            }

            // Fallback for non-pooled items
            Destroy(obj);
        }
    }

    public class PooledInstance : MonoBehaviour
    {
        public string PoolKey { get; set; }
    }

    public interface IPooledObject
    {
        void OnSpawn();
        void OnDespawn();
    }
}
