using UnityEngine;
using Starfall.Core;

namespace Starfall.VFX
{
    public enum HitSparkType
    {
        Metal,
        Rock,
        Alien
    }

    /// <summary>
    /// Production-grade high-octane 2D space impact VFX.
    /// Combines 4 synchronized micro-systems:
    /// 1. Directional velocity-stretched ricochet sparks (spark_02 / spark_05)
    /// 2. Shattered kinetic debris & molten shrapnel (dirt_01 / dirt_02) with 2D tumble
    /// 3. Instantaneous piercing star glint (star_06 / star_02)
    /// 4. Dissipating hot micro-smoke / plasma vapor puff (smoke_04 / smoke_01)
    /// Zero GC allocations, high-performance pooling via IPooledObject.
    /// </summary>
    public class HitSparkVFX : MonoBehaviour, IPooledObject
    {
        [Header("Impact Material")]
        [SerializeField] private HitSparkType sparkType = HitSparkType.Metal;
        [SerializeField] private float duration = 0.28f;

        [Header("Layer Particle Systems")]
        [SerializeField] private ParticleSystem mainSparksPS;
        [SerializeField] private ParticleSystem debrisPS;
        [SerializeField] private ParticleSystem flashPS;
        [SerializeField] private ParticleSystem smokePS;

        private float timer;
        private bool isInitialized = false;

        public HitSparkType SparkType
        {
            get => sparkType;
            set 
            { 
                sparkType = value; 
                isInitialized = false; 
                EnsureHierarchy(); 
            }
        }

        private void Awake()
        {
            if (name.Contains("Rock")) sparkType = HitSparkType.Rock;
            else if (name.Contains("Alien")) sparkType = HitSparkType.Alien;
            else sparkType = HitSparkType.Metal;

            EnsureHierarchy();
        }

        public void EnsureHierarchy()
        {
            if (isInitialized) return;

            // Remove legacy ExplosionVFX if attached to prevent double timer collisions
            var legacyExp = GetComponent<ExplosionVFX>();
            if (legacyExp != null)
            {
                Destroy(legacyExp);
            }

            // 1. Root Stretched Ricochet Sparks
            if (mainSparksPS == null) mainSparksPS = GetComponent<ParticleSystem>();
            if (mainSparksPS == null) mainSparksPS = gameObject.AddComponent<ParticleSystem>();
            ConfigureSparks(mainSparksPS);

            // 2. Kinetic Debris / Shards
            if (debrisPS == null)
            {
                var debrisGo = GetOrCreateChild("Debris");
                debrisPS = debrisGo.GetComponent<ParticleSystem>();
                if (debrisPS == null) debrisPS = debrisGo.AddComponent<ParticleSystem>();
            }
            ConfigureDebris(debrisPS);

            // 3. Piercing Star Glint
            if (flashPS == null)
            {
                var flashGo = GetOrCreateChild("Flash");
                flashPS = flashGo.GetComponent<ParticleSystem>();
                if (flashPS == null) flashPS = flashGo.AddComponent<ParticleSystem>();
            }
            ConfigureFlash(flashPS);

            // 4. Hot Micro-Smoke / Vapor Puff
            if (smokePS == null)
            {
                var smokeGo = GetOrCreateChild("SmokePuff");
                smokePS = smokeGo.GetComponent<ParticleSystem>();
                if (smokePS == null) smokePS = smokeGo.AddComponent<ParticleSystem>();
            }
            ConfigureSmoke(smokePS);

            isInitialized = true;
        }

        private GameObject GetOrCreateChild(string childName)
        {
            var child = transform.Find(childName);
            if (child != null) return child.gameObject;
            var go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go;
        }

        private void ConfigureSparks(ParticleSystem ps)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.2f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.08f, 0.18f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(8f, 15f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.09f, 0.15f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            Color sparkColor;
            switch (sparkType)
            {
                case HitSparkType.Rock:
                    sparkColor = new Color(1.0f, 0.68f, 0.20f, 1.0f);
                    break;
                case HitSparkType.Alien:
                    sparkColor = new Color(0.28f, 1.0f, 0.40f, 1.0f);
                    break;
                default:
                    sparkColor = new Color(0.25f, 0.95f, 1.0f, 1.0f);
                    break;
            }
            main.startColor = sparkColor;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0;
            int sparkCount = sparkType == HitSparkType.Metal ? 14 : 12;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)sparkCount, (short)(sparkCount + 4)) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 45f;
            shape.radius = 0.04f;
            shape.rotation = new Vector3(0f, 90f, 0f); // Emits in 2D plane along impact reflection!

            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0f, 1f);
            curve.AddKey(1f, 0f);
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, curve);

            var ren = ps.GetComponent<ParticleSystemRenderer>();
            if (ren != null)
            {
                ren.sharedMaterial = VFXMaterials.Spark;
                ren.renderMode = ParticleSystemRenderMode.Stretch;
                ren.velocityScale = 0.08f;
                ren.lengthScale = 1.4f;
                ren.cameraVelocityScale = 0f;
                ren.sortingOrder = 24;
            }
        }

        private void ConfigureDebris(ParticleSystem ps)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.25f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.28f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 8.5f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.13f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            Color debrisColor;
            Material debrisMat;
            switch (sparkType)
            {
                case HitSparkType.Rock:
                    debrisColor = new Color(0.85f, 0.75f, 0.6f, 1.0f);
                    debrisMat = VFXMaterials.RockDebris;
                    break;
                case HitSparkType.Alien:
                    debrisColor = new Color(0.85f, 0.2f, 1.0f, 1.0f);
                    debrisMat = VFXMaterials.BioSplatter;
                    break;
                default:
                    debrisColor = new Color(1.0f, 0.85f, 0.45f, 1.0f);
                    debrisMat = VFXMaterials.Debris;
                    break;
            }
            main.startColor = debrisColor;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0;
            int debrisCount = sparkType == HitSparkType.Rock ? 8 : 6;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)debrisCount, (short)(debrisCount + 3)) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 60f;
            shape.radius = 0.05f;
            shape.rotation = new Vector3(0f, 90f, 0f);

            var rotOverLife = ps.rotationOverLifetime;
            rotOverLife.enabled = true;
            rotOverLife.z = new ParticleSystem.MinMaxCurve(-360f * Mathf.Deg2Rad, 360f * Mathf.Deg2Rad);

            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0f, 1f);
            curve.AddKey(0.7f, 0.9f);
            curve.AddKey(1f, 0f);
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, curve);

            var ren = ps.GetComponent<ParticleSystemRenderer>();
            if (ren != null)
            {
                ren.sharedMaterial = debrisMat;
                ren.renderMode = ParticleSystemRenderMode.Billboard;
                ren.sortingOrder = 23;
            }
        }

        private void ConfigureFlash(ParticleSystem ps)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.06f;
            main.startLifetime = 0.05f;
            main.startSpeed = 0f;
            main.startSize = 0.45f;
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;

            Color flashColor;
            switch (sparkType)
            {
                case HitSparkType.Rock:
                    flashColor = new Color(1.0f, 0.85f, 0.55f, 1.0f);
                    break;
                case HitSparkType.Alien:
                    flashColor = new Color(0.6f, 1.0f, 0.65f, 1.0f);
                    break;
                default:
                    flashColor = Color.white;
                    break;
            }
            main.startColor = flashColor;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 1) });

            var ren = ps.GetComponent<ParticleSystemRenderer>();
            if (ren != null)
            {
                ren.sharedMaterial = VFXMaterials.FlashStar;
                ren.renderMode = ParticleSystemRenderMode.Billboard;
                ren.sortingOrder = 25;
            }
        }

        private void ConfigureSmoke(ParticleSystem ps)
        {
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.playOnAwake = false;
            main.loop = false;
            main.duration = 0.22f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.25f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.8f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            Color smokeColor;
            switch (sparkType)
            {
                case HitSparkType.Rock:
                    smokeColor = new Color(0.65f, 0.55f, 0.45f, 0.35f);
                    break;
                case HitSparkType.Alien:
                    smokeColor = new Color(0.2f, 0.85f, 0.35f, 0.32f);
                    break;
                default:
                    smokeColor = new Color(0.2f, 0.7f, 1.0f, 0.30f);
                    break;
            }
            main.startColor = smokeColor;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 2) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f;

            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0f, 0.8f);
            curve.AddKey(1f, 2.2f);
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, curve);

            var colorOverLife = ps.colorOverLifetime;
            colorOverLife.enabled = true;
            Gradient grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(smokeColor, 0f), new GradientColorKey(smokeColor, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(smokeColor.a, 0f), new GradientAlphaKey(0f, 1f) }
            );
            colorOverLife.color = grad;

            var ren = ps.GetComponent<ParticleSystemRenderer>();
            if (ren != null)
            {
                ren.sharedMaterial = VFXMaterials.Smoke;
                ren.renderMode = ParticleSystemRenderMode.Billboard;
                ren.sortingOrder = 22;
            }
        }

        public void OnSpawn()
        {
            EnsureHierarchy();
            timer = duration;

            if (mainSparksPS != null) { mainSparksPS.Clear(true); mainSparksPS.Play(true); }
            if (debrisPS != null) { debrisPS.Clear(true); debrisPS.Play(true); }
            if (flashPS != null) { flashPS.Clear(true); flashPS.Play(true); }
            if (smokePS != null) { smokePS.Clear(true); smokePS.Play(true); }
        }

        public void OnDespawn()
        {
            if (mainSparksPS != null) mainSparksPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (debrisPS != null) debrisPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (flashPS != null) flashPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (smokePS != null) smokePS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void Update()
        {
            timer -= Time.unscaledDeltaTime;
            if (timer <= 0f)
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
                gameObject.SetActive(false);
            }
        }
    }
}
