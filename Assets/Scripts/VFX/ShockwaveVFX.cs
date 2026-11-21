using System.Collections;
using UnityEngine;
using Starfall.Core;

namespace Starfall.VFX
{
    /// <summary>
    /// Production-grade Refractive Shockwave Burst.
    /// Replaces flat 2D pink rings with a multi-wave, high-velocity atmospheric compression blast:
    /// - Primary energetic leading wavefront (circle_05)
    /// - Secondary trailing vacuum distortion wake (circle_04)
    /// - Epicenter anamorphic solar flash (flare_01 + light_02)
    /// - High-speed radial EMP spark corona
    /// Fully pooled with zero runtime GC allocation.
    /// </summary>
    public class ShockwaveVFX : MonoBehaviour, IPooledObject
    {
        [Header("Blast Wave Dynamics")]
        [SerializeField] private float duration = 0.42f;
        [SerializeField] private float targetDiameter = 8.5f;
        [SerializeField] private float maxScale = 1.66f;
        [SerializeField] private Color primaryColor = new Color(0.0f, 0.95f, 1.0f, 1.0f);
        [SerializeField] private Color secondaryColor = new Color(0.7f, 0.25f, 1.0f, 0.85f);
        [SerializeField] private Color coreFlashColor = new Color(1.0f, 1.0f, 1.0f, 1.0f);
        [SerializeField] private bool triggerCameraShake = true;

        [Header("Renderers")]
        [SerializeField] private SpriteRenderer primaryWaveRenderer;
        [SerializeField] private SpriteRenderer secondaryWaveRenderer;
        [SerializeField] private SpriteRenderer coreFlashRenderer;
        [SerializeField] private ParticleSystem sparkCorona;

        private Coroutine burstCoroutine;
        private bool isInitialized = false;

        private void Awake()
        {
            EnsureHierarchy();
            SetVisualsActive(false);
        }

        private void Start()
        {
            // If placed directly in a scene hierarchy (not an active pooled clone), keep dormant
            if (!gameObject.name.Contains("(Clone)"))
            {
                gameObject.SetActive(false);
            }
        }

        public void EnsureHierarchy()
        {
            if (isInitialized) return;
            var mat = VFXMaterials.SpriteAdditiveMaterial;

            // 1. Primary Leading Wavefront
            if (primaryWaveRenderer == null)
            {
                var go = GetOrCreateChild("PrimaryWave");
                var sr = go.GetComponent<SpriteRenderer>();
                if (sr == null) sr = go.AddComponent<SpriteRenderer>();
                primaryWaveRenderer = sr;
                primaryWaveRenderer.sprite = VFXMaterials.LoadVFXSprite("circle_05", 
                    VFXMaterials.LoadVFXSprite("circle_04", ProceduralSprites.Ring));
                primaryWaveRenderer.material = mat;
                primaryWaveRenderer.sortingOrder = 30;
                primaryWaveRenderer.enabled = false;
            }

            // 2. Secondary Refractive Distortion Wake
            if (secondaryWaveRenderer == null)
            {
                var go = GetOrCreateChild("SecondaryWake");
                var sr = go.GetComponent<SpriteRenderer>();
                if (sr == null) sr = go.AddComponent<SpriteRenderer>();
                secondaryWaveRenderer = sr;
                secondaryWaveRenderer.sprite = VFXMaterials.LoadVFXSprite("circle_04", 
                    VFXMaterials.LoadVFXSprite("twirl_03", ProceduralSprites.Ring));
                secondaryWaveRenderer.material = mat;
                secondaryWaveRenderer.sortingOrder = 29;
                secondaryWaveRenderer.enabled = false;
            }

            // 3. Epicenter Core Solar Flash
            if (coreFlashRenderer == null)
            {
                var go = GetOrCreateChild("CoreFlash");
                var sr = go.GetComponent<SpriteRenderer>();
                if (sr == null) sr = go.AddComponent<SpriteRenderer>();
                coreFlashRenderer = sr;
                coreFlashRenderer.sprite = VFXMaterials.LoadVFXSprite("flare_01", 
                    VFXMaterials.LoadVFXSprite("light_02", ProceduralSprites.SoftCircle));
                coreFlashRenderer.material = mat;
                coreFlashRenderer.sortingOrder = 31;
                coreFlashRenderer.enabled = false;
            }

            // 4. Radial EMP Spark Corona
            if (sparkCorona == null)
            {
                var psGo = GetOrCreateChild("SparkCorona");
                var ps = psGo.GetComponent<ParticleSystem>();
                if (ps == null) ps = psGo.AddComponent<ParticleSystem>();
                sparkCorona = ps;
                ConfigureSparkCorona(sparkCorona);
            }

            // Calibrate max scale dynamically based on native sprite size
            float nativeWidth = 5.12f;
            if (primaryWaveRenderer != null && primaryWaveRenderer.sprite != null && primaryWaveRenderer.sprite.rect.width > 0f)
            {
                nativeWidth = primaryWaveRenderer.sprite.rect.width / primaryWaveRenderer.sprite.pixelsPerUnit;
            }
            maxScale = nativeWidth > 0f ? (targetDiameter / nativeWidth) : 1.66f;

            // Disable base sprite renderer if present from old setups
            var legacySr = GetComponent<SpriteRenderer>();
            if (legacySr != null && legacySr != primaryWaveRenderer)
            {
                legacySr.enabled = false;
            }

            SetVisualsActive(false);
            isInitialized = true;
        }

        private GameObject GetOrCreateChild(string name)
        {
            var child = transform.Find(name);
            if (child != null) return child.gameObject;
            var go = new GameObject(name);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go;
        }

        private void ConfigureSparkCorona(ParticleSystem ps)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.3f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.35f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(6f, 14f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
            main.startColor = new Color(0.4f, 0.95f, 1.0f, 0.9f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.useUnscaledTime = true;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 16) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.35f;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(new Color(0f, 0.7f, 1f), 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLifetime.color = grad;

            var psr = ps.GetComponent<ParticleSystemRenderer>();
            if (psr != null)
            {
                psr.material = VFXMaterials.Spark;
                psr.sortingOrder = 32;
            }
        }

        public void SetVisualsActive(bool active)
        {
            if (primaryWaveRenderer != null) primaryWaveRenderer.enabled = active;
            if (secondaryWaveRenderer != null) secondaryWaveRenderer.enabled = active;
            if (coreFlashRenderer != null) coreFlashRenderer.enabled = active;
            if (sparkCorona != null && !active)
            {
                sparkCorona.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
        }

        /// <summary>
        /// Allows custom tint overrides for explosive hazards (e.g. orange for asteroids, purple for alien bombs).
        /// </summary>
        public void SetCustomTint(Color primary, Color secondary)
        {
            primaryColor = primary;
            secondaryColor = secondary;
        }

        public void OnSpawn()
        {
            EnsureHierarchy();
            if (!gameObject.activeSelf)
            {
                gameObject.SetActive(true);
            }
            SetVisualsActive(true);
            transform.localScale = Vector3.one * 0.05f;

            if (primaryWaveRenderer != null) primaryWaveRenderer.color = primaryColor;
            if (secondaryWaveRenderer != null) secondaryWaveRenderer.color = secondaryColor;
            if (coreFlashRenderer != null) coreFlashRenderer.color = coreFlashColor;

            if (triggerCameraShake && CameraShake.Instance != null)
            {
                CameraShake.Instance.Shake(0.35f, 0.30f);
            }

            if (sparkCorona != null)
            {
                sparkCorona.Clear(true);
                sparkCorona.Play(true);
            }

            if (burstCoroutine != null) StopCoroutine(burstCoroutine);
            burstCoroutine = StartCoroutine(BurstRoutine());
        }

        public void OnDespawn()
        {
            if (burstCoroutine != null)
            {
                StopCoroutine(burstCoroutine);
                burstCoroutine = null;
            }
            if (sparkCorona != null)
            {
                sparkCorona.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            }
            SetVisualsActive(false);
        }

        private IEnumerator BurstRoutine()
        {
            float elapsed = 0f;
            SetVisualsActive(true);

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(elapsed / duration);

                // High-velocity supersonic cubic expansion curve
                float waveEase = 1f - Mathf.Pow(1f - t, 3f);
                float lagEase = 1f - Mathf.Pow(1f - Mathf.Clamp01(t * 0.92f), 2.5f);

                // 1. Primary Wave Expansion
                if (primaryWaveRenderer != null)
                {
                    primaryWaveRenderer.transform.localScale = Vector3.one * Mathf.Lerp(0.05f, maxScale, waveEase);
                    float a = Mathf.Lerp(0.85f, 0.0f, Mathf.Pow(t, 1.6f));
                    Color c = primaryColor;
                    c.a = a;
                    primaryWaveRenderer.color = c;
                }

                // 2. Secondary Refractive Distortion Wake (slightly trailing with gentle rotation)
                if (secondaryWaveRenderer != null)
                {
                    secondaryWaveRenderer.transform.localScale = Vector3.one * Mathf.Lerp(0.05f, maxScale * 0.82f, lagEase);
                    secondaryWaveRenderer.transform.Rotate(0f, 0f, 45f * Time.unscaledDeltaTime);
                    float a = Mathf.Lerp(0.7f, 0.0f, Mathf.Pow(t, 1.3f));
                    Color c = secondaryColor;
                    c.a = a;
                    secondaryWaveRenderer.color = c;
                }

                // 3. Epicenter Core Flash (peaks in first 15% of duration, then burns out)
                if (coreFlashRenderer != null)
                {
                    float flashT = Mathf.Clamp01(t / 0.18f);
                    float flashScale = Mathf.Lerp(0.05f, maxScale * 0.22f, Mathf.Sin(flashT * Mathf.PI * 0.5f));
                    coreFlashRenderer.transform.localScale = Vector3.one * flashScale;
                    float flashAlpha = (1f - flashT) * 0.85f;
                    Color fc = coreFlashColor;
                    fc.a = flashAlpha;
                    coreFlashRenderer.color = fc;
                }

                yield return null;
            }

            SetVisualsActive(false);

            if (ObjectPool.Instance != null)
            {
                ObjectPool.Instance.Despawn(gameObject);
            }
            else
            {
                gameObject.SetActive(false);
            }
        }
    }
}
