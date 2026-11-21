using UnityEngine;
using Starfall.Core;

namespace Starfall.VFX
{
    /// <summary>
    /// Pooled visual effect that plays animated sprite explosion flipbooks (e.g. 96-frame movie-quality fireball),
    /// ParticleSystems, and child effects, then returns cleanly to the pool.
    /// </summary>
    public class ExplosionVFX : MonoBehaviour, IPooledObject
    {
        [Header("Cinematic Flipbook Animation")]
        [Tooltip("If true, renders the 2D flipbook fireball (auto-scaled so it never covers the screen). If false, plays pure realistic space particle VFX.")]
        [SerializeField] private bool useFlipbookAnimation = false;
        [SerializeField] private SpriteRenderer flipbookRenderer;
        [SerializeField] private Sprite[] animationFrames;
        [SerializeField] private float animationFps = 48f;
        [SerializeField] private Vector3 flipbookScale = new Vector3(0.07f, 0.07f, 1f);
        [SerializeField] private float targetWorldRadius = 1.35f;

        [Header("Particle & Fallback Settings")]
        [SerializeField] private float durationOverride = 0.45f;
        private ParticleSystem[] particleSystems;
        private Light[] lights;
        private float[] initialLightIntensities;
        private float timer;

        private int currentFrame = 0;
        private float frameTimer = 0f;
        private bool isFlipbookPlaying = false;

        private static Sprite[] sharedExplosionFrames;

        public bool UseFlipbookAnimation
        {
            get => useFlipbookAnimation;
            set => useFlipbookAnimation = value;
        }

        private void Awake()
        {
            EnsureFlipbookSetup();
            CacheComponents();
        }

        private void EnsureFlipbookSetup()
        {
            if (!useFlipbookAnimation)
            {
                if (flipbookRenderer != null) flipbookRenderer.enabled = false;
                return;
            }

            if (flipbookRenderer == null)
            {
                var child = transform.Find("ExplosionFlipbook");
                if (child != null)
                {
                    flipbookRenderer = child.GetComponent<SpriteRenderer>();
                }
                else
                {
                    var go = new GameObject("ExplosionFlipbook");
                    go.transform.SetParent(transform, false);
                    go.transform.localPosition = Vector3.zero;
                    go.transform.localScale = flipbookScale;
                    flipbookRenderer = go.AddComponent<SpriteRenderer>();
                    flipbookRenderer.sortingOrder = 20;
                }
            }

            if (animationFrames == null || animationFrames.Length == 0)
            {
                if (sharedExplosionFrames == null || sharedExplosionFrames.Length == 0)
                {
                    LoadSharedExplosionFrames();
                }
                animationFrames = sharedExplosionFrames;
            }

            // Always dynamically ensure flipbook never exceeds targetWorldRadius
            if (flipbookRenderer != null && animationFrames != null && animationFrames.Length > 0 && animationFrames[0] != null)
            {
                float nativeWidth = animationFrames[0].rect.width / animationFrames[0].pixelsPerUnit;
                if (nativeWidth > 1.5f)
                {
                    float safeScale = targetWorldRadius / nativeWidth;
                    flipbookRenderer.transform.localScale = new Vector3(safeScale, safeScale, 1f);
                }
                else
                {
                    flipbookRenderer.transform.localScale = flipbookScale;
                }
                flipbookRenderer.enabled = useFlipbookAnimation;
            }
        }

        private void LoadSharedExplosionFrames()
        {
#if UNITY_EDITOR
            string dir = "Assets/ALienware 2D assets/Explosions/Explosion_animation_expanding_wi…_20260924124436";
            if (!System.IO.Directory.Exists(dir)) return;
            var list = new System.Collections.Generic.List<Sprite>();
            for (int i = 1; i <= 96; i++)
            {
                string path = $"{dir}/{i:0000}.png";
                var spr = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
                if (spr == null)
                {
                    var all = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
                    for (int a = 0; a < all.Length; a++)
                    {
                        if (all[a] is Sprite s) { spr = s; break; }
                    }
                }
                if (spr != null) list.Add(spr);
            }
            if (list.Count > 0)
            {
                sharedExplosionFrames = list.ToArray();
            }
#endif
        }

        private void CacheComponents()
        {
            if (particleSystems == null || particleSystems.Length == 0)
            {
                particleSystems = GetComponentsInChildren<ParticleSystem>(true);
            }
            if (lights == null || lights.Length == 0)
            {
                lights = GetComponentsInChildren<Light>(true);
                if (lights != null && lights.Length > 0)
                {
                    initialLightIntensities = new float[lights.Length];
                    for (int i = 0; i < lights.Length; i++)
                    {
                        if (lights[i] != null) initialLightIntensities[i] = lights[i].intensity;
                    }
                }
            }
        }

        public void OnSpawn()
        {
            EnsureFlipbookSetup();
            CacheComponents();
            timer = durationOverride;
            currentFrame = 0;
            frameTimer = 0f;

            if (useFlipbookAnimation && animationFrames != null && animationFrames.Length > 0 && flipbookRenderer != null)
            {
                isFlipbookPlaying = true;
                flipbookRenderer.enabled = true;
                flipbookRenderer.sprite = animationFrames[0];
            }
            else
            {
                isFlipbookPlaying = false;
                if (flipbookRenderer != null) flipbookRenderer.enabled = false;
            }

            if (particleSystems != null)
            {
                for (int i = 0; i < particleSystems.Length; i++)
                {
                    if (particleSystems[i] != null)
                    {
                        particleSystems[i].Clear(true);
                        particleSystems[i].Play(true);
                    }
                }
            }

            if (lights != null)
            {
                for (int i = 0; i < lights.Length; i++)
                {
                    if (lights[i] != null)
                    {
                        lights[i].enabled = true;
                        lights[i].intensity = (initialLightIntensities != null && i < initialLightIntensities.Length) ? initialLightIntensities[i] : 1f;
                    }
                }
            }
        }

        public void OnDespawn()
        {
            isFlipbookPlaying = false;
            if (flipbookRenderer != null)
            {
                flipbookRenderer.enabled = false;
            }

            if (particleSystems != null)
            {
                for (int i = 0; i < particleSystems.Length; i++)
                {
                    if (particleSystems[i] != null)
                    {
                        particleSystems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                    }
                }
            }

            if (lights != null)
            {
                for (int i = 0; i < lights.Length; i++)
                {
                    if (lights[i] != null) lights[i].enabled = false;
                }
            }
        }

        private void Update()
        {
            using var __perf = Starfall.Core.PerfMarkers.VFXUpdate.Auto();

            if (isFlipbookPlaying && animationFrames != null && animationFrames.Length > 0)
            {
                float frameDuration = 1f / Mathf.Max(1f, animationFps);
                frameTimer += Time.unscaledDeltaTime;
                if (frameTimer >= frameDuration)
                {
                    frameTimer -= frameDuration;
                    currentFrame++;
                    if (currentFrame < animationFrames.Length)
                    {
                        if (flipbookRenderer != null)
                            flipbookRenderer.sprite = animationFrames[currentFrame];
                    }
                    else
                    {
                        isFlipbookPlaying = false;
                        if (flipbookRenderer != null) flipbookRenderer.enabled = false;
                        Despawn();
                        return;
                    }
                }
                return;
            }

            if (durationOverride > 0f)
            {
                timer -= Time.unscaledDeltaTime;
                if (timer <= 0f)
                {
                    Despawn();
                }
                return;
            }

            bool anyAlive = false;
            if (particleSystems != null && particleSystems.Length > 0)
            {
                for (int i = 0; i < particleSystems.Length; i++)
                {
                    if (particleSystems[i] != null && particleSystems[i].IsAlive(true))
                    {
                        anyAlive = true;
                        break;
                    }
                }
            }

            if (!anyAlive)
            {
                Despawn();
            }
        }

        private void Despawn()
        {
            if (ObjectPool.Instance != null)
            {
                ObjectPool.Instance.Despawn(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
