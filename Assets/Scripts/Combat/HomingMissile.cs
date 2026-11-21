using UnityEngine;
using Starfall.Core;
using Starfall.Enemies;
using Starfall.VFX;

namespace Starfall.Combat
{
    /// <summary>
    /// Seeker micro-missile launched by the Homing Missiles power-up.
    /// Steers toward the nearest enemy and detonates on impact.
    /// Performance: pooled, retargets 5x per second from the enemy registry (no physics queries,
    /// no allocations), and uses a static glow instead of spawning thruster particles every frame.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class HomingMissile : MonoBehaviour, IPooledObject
    {
        [Header("Flight Dynamics")]
        [SerializeField] private float speed = 14f;
        [SerializeField] private float turnRate = 420f; // degrees per second
        [SerializeField] private float lifetime = 3.5f;
        [SerializeField] private int damage = 15;
        [SerializeField] private float searchRadius = 20f;
        [SerializeField] private LayerMask enemyLayer;

        [Header("Visuals")]
        [SerializeField] private string explosionPoolKey = "ExplosionVFX";

        private const float RetargetInterval = 0.15f;

        private EnemyBase target;
        private float lifeTimer;
        private float retargetTimer;
        private bool exploded;

        // Realistic Propulsion VFX
        private ParticleSystem corePS;
        private ParticleSystem smokePS;
        private ParticleSystem sparkPS;

        private void Awake()
        {
            var rb = GetComponent<Rigidbody2D>();
            if (rb != null)
            {
                rb.gravityScale = 0f;
                rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
            }

            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;

            if (enemyLayer.value == 0) enemyLayer = LayerMask.GetMask("Enemy");

            SetupPropulsionVFX();
        }

        private void SetupPropulsionVFX()
        {
            // Remove legacy static sprite blob if present
            var oldGlow = transform.Find("ThrusterGlow");
            if (oldGlow != null) Destroy(oldGlow.gameObject);

            var root = transform.Find("MissilePropulsion");
            if (root == null)
            {
                var go = new GameObject("MissilePropulsion");
                go.transform.SetParent(transform, false);
                root = go.transform;
            }
            root.localPosition = new Vector3(0f, -0.28f, 0f);
            root.localRotation = Quaternion.identity;

            // 1. Incandescent Core Jet Flame (Local Space)
            corePS = root.GetComponent<ParticleSystem>();
            if (corePS == null) corePS = root.gameObject.AddComponent<ParticleSystem>();

            var corePSR = root.GetComponent<ParticleSystemRenderer>();
            if (corePSR == null) corePSR = root.gameObject.AddComponent<ParticleSystemRenderer>();
            corePSR.material = VFXMaterials.Fire;
            corePSR.sortingOrder = 22;

            var cMain = corePS.main;
            cMain.loop = true;
            cMain.playOnAwake = true;
            cMain.scalingMode = ParticleSystemScalingMode.Shape;
            cMain.simulationSpace = ParticleSystemSimulationSpace.Local;
            cMain.startLifetime = new ParticleSystem.MinMaxCurve(0.08f, 0.12f);
            cMain.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 5.5f);
            cMain.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.28f);
            cMain.gravityModifier = 0f;

            var cShape = corePS.shape;
            cShape.enabled = true;
            cShape.shapeType = ParticleSystemShapeType.Cone;
            cShape.angle = 6f;
            cShape.radius = 0.02f;
            cShape.rotation = new Vector3(90f, 0f, 0f);

            var cSol = corePS.sizeOverLifetime;
            cSol.enabled = true;
            var cSizeCurve = new AnimationCurve();
            cSizeCurve.AddKey(0f, 1.0f);
            cSizeCurve.AddKey(0.2f, 0.85f);
            cSizeCurve.AddKey(1f, 0.15f);
            cSol.size = new ParticleSystem.MinMaxCurve(1f, cSizeCurve);

            var cCol = corePS.colorOverLifetime;
            cCol.enabled = true;
            var cGrad = new Gradient();
            cGrad.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(Color.white, 0f),                               // White-hot core
                    new GradientColorKey(new Color(1f, 0.75f, 0.2f), 0.22f),            // Yellow rocket fire
                    new GradientColorKey(new Color(1f, 0.35f, 0.05f), 0.65f),           // Orange-red plume
                    new GradientColorKey(new Color(0.35f, 0.1f, 0.02f), 1f)             // Smoke tip
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1.0f, 0f),
                    new GradientAlphaKey(0.95f, 0.5f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            cCol.color = new ParticleSystem.MinMaxGradient(cGrad);

            var cEmission = corePS.emission;
            cEmission.enabled = true;
            cEmission.rateOverTime = 48f;

            // 2. Billowing Smoke Contrail (World Space)
            var smokeTrans = root.Find("SmokeTrail");
            if (smokeTrans == null)
            {
                var sGo = new GameObject("SmokeTrail");
                sGo.transform.SetParent(root, false);
                smokeTrans = sGo.transform;
            }
            smokeTrans.localPosition = Vector3.zero;
            smokeTrans.localRotation = Quaternion.identity;

            smokePS = smokeTrans.GetComponent<ParticleSystem>();
            if (smokePS == null) smokePS = smokeTrans.gameObject.AddComponent<ParticleSystem>();

            var sPSR = smokeTrans.GetComponent<ParticleSystemRenderer>();
            if (sPSR == null) sPSR = smokeTrans.gameObject.AddComponent<ParticleSystemRenderer>();
            sPSR.material = VFXMaterials.Smoke;
            sPSR.sortingOrder = 21;

            var sMain = smokePS.main;
            sMain.loop = true;
            sMain.playOnAwake = true;
            sMain.scalingMode = ParticleSystemScalingMode.Shape;
            sMain.simulationSpace = ParticleSystemSimulationSpace.World; // Traces curve through space!
            sMain.startLifetime = new ParticleSystem.MinMaxCurve(0.38f, 0.58f);
            sMain.startSpeed = new ParticleSystem.MinMaxCurve(0.4f, 1.0f);
            sMain.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.18f);
            sMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            sMain.gravityModifier = 0f;

            var sShape = smokePS.shape;
            sShape.enabled = true;
            sShape.shapeType = ParticleSystemShapeType.Sphere;
            sShape.radius = 0.035f;

            var sSol = smokePS.sizeOverLifetime;
            sSol.enabled = true;
            var sSizeCurve = new AnimationCurve();
            sSizeCurve.AddKey(0f, 0.75f);
            sSizeCurve.AddKey(0.4f, 1.6f);
            sSizeCurve.AddKey(1f, 1.3f);
            sSol.size = new ParticleSystem.MinMaxCurve(1f, sSizeCurve);

            var sRol = smokePS.rotationOverLifetime;
            sRol.enabled = true;
            sRol.z = new ParticleSystem.MinMaxCurve(-2.5f, 2.5f);

            var sCol = smokePS.colorOverLifetime;
            sCol.enabled = true;
            var sGrad = new Gradient();
            sGrad.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(new Color(0.92f, 0.85f, 0.72f), 0f),           // Warm missile vapor
                    new GradientColorKey(new Color(0.65f, 0.65f, 0.65f), 0.35f),         // Dense gray smoke
                    new GradientColorKey(new Color(0.4f, 0.4f, 0.4f), 1f)               // Vacuum dissipation
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(0.65f, 0f),
                    new GradientAlphaKey(0.48f, 0.35f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            sCol.color = new ParticleSystem.MinMaxGradient(sGrad);

            var sEmission = smokePS.emission;
            sEmission.enabled = true;
            sEmission.rateOverTime = 42f;

            // 3. Rocket Motor Sparks / Embers (World Space)
            var sparkTrans = root.Find("SparkTrail");
            if (sparkTrans == null)
            {
                var spGo = new GameObject("SparkTrail");
                spGo.transform.SetParent(root, false);
                sparkTrans = spGo.transform;
            }
            sparkTrans.localPosition = Vector3.zero;
            sparkTrans.localRotation = Quaternion.identity;

            sparkPS = sparkTrans.GetComponent<ParticleSystem>();
            if (sparkPS == null) sparkPS = sparkTrans.gameObject.AddComponent<ParticleSystem>();

            var spPSR = sparkTrans.GetComponent<ParticleSystemRenderer>();
            if (spPSR == null) spPSR = sparkTrans.gameObject.AddComponent<ParticleSystemRenderer>();
            spPSR.material = VFXMaterials.Spark;
            spPSR.sortingOrder = 24;

            var spMain = sparkPS.main;
            spMain.loop = true;
            spMain.playOnAwake = true;
            spMain.scalingMode = ParticleSystemScalingMode.Shape;
            spMain.simulationSpace = ParticleSystemSimulationSpace.World;
            spMain.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
            spMain.startSpeed = new ParticleSystem.MinMaxCurve(1.8f, 3.8f);
            spMain.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
            spMain.gravityModifier = 0f;

            var spShape = sparkPS.shape;
            spShape.enabled = true;
            spShape.shapeType = ParticleSystemShapeType.Cone;
            spShape.angle = 24f;
            spShape.radius = 0.02f;
            spShape.rotation = new Vector3(90f, 0f, 0f);

            var spSol = sparkPS.sizeOverLifetime;
            spSol.enabled = true;
            var spSizeCurve = new AnimationCurve();
            spSizeCurve.AddKey(0f, 1f);
            spSizeCurve.AddKey(1f, 0f);
            spSol.size = new ParticleSystem.MinMaxCurve(1f, spSizeCurve);

            var spCol = sparkPS.colorOverLifetime;
            spCol.enabled = true;
            var spGrad = new Gradient();
            spGrad.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(new Color(1f, 0.88f, 0.35f), 0.3f),            // Brilliant spark
                    new GradientColorKey(new Color(1f, 0.35f, 0.05f), 1f)               // Red ember
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.85f, 0.6f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            spCol.color = new ParticleSystem.MinMaxGradient(spGrad);

            var spEmission = sparkPS.emission;
            spEmission.enabled = true;
            spEmission.rateOverTime = 22f;
        }

        public void Initialize(Vector3 spawnPos, Vector2 initialDirection, int missileDamage = 15)
        {
            transform.position = spawnPos;
            damage = missileDamage;
            lifeTimer = lifetime;
            exploded = false;

            float angle = Mathf.Atan2(initialDirection.y, initialDirection.x) * Mathf.Rad2Deg - 90f;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);

            if (corePS == null) SetupPropulsionVFX();

            if (corePS != null) { corePS.Clear(true); corePS.Play(true); }
            if (smokePS != null) { smokePS.Clear(true); smokePS.Play(true); }
            if (sparkPS != null) { sparkPS.Clear(true); sparkPS.Play(true); }

            AcquireTarget();
        }

        public void OnSpawn()
        {
            lifeTimer = lifetime;
            target = null;
            exploded = false;
            retargetTimer = 0f;

            if (corePS == null) SetupPropulsionVFX();

            if (corePS != null) { corePS.Clear(true); corePS.Play(true); }
            if (smokePS != null) { smokePS.Clear(true); smokePS.Play(true); }
            if (sparkPS != null) { sparkPS.Clear(true); sparkPS.Play(true); }
        }

        public void OnDespawn()
        {
            target = null;
            if (corePS != null) corePS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (smokePS != null) smokePS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            if (sparkPS != null) sparkPS.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private void Update()
        {
            using var __perf = Starfall.Core.PerfMarkers.ProjectileUpdate.Auto();
            float dt = Time.deltaTime;
            lifeTimer -= dt;
            if (lifeTimer <= 0f || !PlayfieldBounds.IsOnScreen(transform.position, 2f))
            {
                Explode(false);
                return;
            }

            retargetTimer -= dt;
            if (target == null || target.IsDead || !target.gameObject.activeInHierarchy)
            {
                target = null;
                if (retargetTimer <= 0f) AcquireTarget();
            }

            if (target != null)
            {
                Vector2 toTarget = ((Vector2)target.transform.position - (Vector2)transform.position).normalized;
                float targetAngle = Mathf.Atan2(toTarget.y, toTarget.x) * Mathf.Rad2Deg - 90f;
                float newAngle = Mathf.MoveTowardsAngle(transform.eulerAngles.z, targetAngle, turnRate * dt);
                transform.rotation = Quaternion.Euler(0f, 0f, newAngle);
            }

            transform.position += transform.up * (speed * dt);
        }

        private void AcquireTarget()
        {
            retargetTimer = RetargetInterval;
            target = null;
            float best = searchRadius * searchRadius;
            Vector3 p = transform.position;
            var list = EnemyBase.Active;
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (e == null || e.IsDead || e.IsBonusPod || !e.gameObject.activeInHierarchy) continue;
                if (!PlayfieldBounds.IsOnScreen(e.transform.position, 1.5f)) continue;
                float d = (e.transform.position - p).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    target = e;
                }
            }
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (exploded) return;
            if ((enemyLayer.value & (1 << collision.gameObject.layer)) == 0 &&
                collision.GetComponentInParent<AsteroidHazard>() == null) return;

            var damageable = collision.GetComponentInParent<IDamageable>();
            if (damageable == null || damageable.IsDead) return;

            damageable.TakeDamage(damage, transform.position);
            Explode(true);
        }

        private void Explode(bool hit)
        {
            if (exploded) return;
            exploded = true;

            if (corePS != null) corePS.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (smokePS != null) smokePS.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (sparkPS != null) sparkPS.Stop(true, ParticleSystemStopBehavior.StopEmitting);

            if (hit)
            {
                if (ObjectPool.Instance != null && !string.IsNullOrEmpty(explosionPoolKey))
                    ObjectPool.Instance.Spawn(explosionPoolKey, transform.position, Quaternion.identity);

                CameraShake.Instance?.Shake(0.08f, 0.12f);
                Starfall.Audio.AudioManager.Instance?.PlayExplosion(false);
            }

            if (ObjectPool.Instance != null) ObjectPool.Instance.Despawn(gameObject);
            else Destroy(gameObject);
        }
    }
}
