using System.Collections;
using UnityEngine;
using Starfall.Player;
using Starfall.Enemies;

namespace Starfall.VFX
{
    public enum FireColorPreset
    {
        PlayerIonCyan,       // Player default: High-tech Cyan outer, White core
        EnemyCrimson,        // Standard fighters/scouts: Crimson outer, Yellow core
        EliteVoidPurple,     // Gunships/Carriers: Deep Violet/Purple outer, Lavender core
        BossToxicGreen,      // Hive Queen / Dreadnought: Neon Acid Green outer, Mint core
        BossSupernova,       // Inferno Colossus: Fiery Red-Orange outer, Gold core
        Custom               // Custom user colors
    }

    public enum ThrusterDirection
    {
        Auto,               // Downward for player, Upward for enemies
        Downward,           // Exhaust shoots downward (for ships pointing up)
        Upward              // Exhaust shoots upward (for ships pointing down)
    }

    /// <summary>
    /// Universal ship fire system for Alienwar.
    /// Manages both:
    /// 1. Procedural animated engine thruster flame plumes (customized per ship type/faction).
    /// 2. Hull burning & fire aura effect when ship is damaged or low on HP.
    /// </summary>
    [DisallowMultipleComponent]
    public class ShipFireEffect : MonoBehaviour
    {
        [Header("Color Preset")]
        [SerializeField] private FireColorPreset colorPreset = FireColorPreset.EnemyCrimson;
        [SerializeField] private Color customFlameColor = new Color(1.0f, 0.4f, 0.1f, 1.0f);
        [SerializeField] private Color customCoreColor = new Color(1.0f, 0.95f, 0.7f, 1.0f);
        [SerializeField] [Range(1.0f, 5.0f)] private float flameIntensity = 2.2f;

        [Header("Thruster Setup")]
        [SerializeField] private bool enableThrusters = true;
        [SerializeField] private ThrusterDirection direction = ThrusterDirection.Auto;
        [SerializeField] private Vector2[] engineOffsets = new Vector2[] { new Vector2(0f, 0.45f) };
        [SerializeField] private Vector2 flameSize = new Vector2(0.35f, 0.75f);
        [SerializeField] private int sortingOrderOffset = -1; // Render behind ship hull

        [Header("Hull Burn / Damage Fire")]
        [SerializeField] private bool enableHullBurnOnDamage = true;
        [SerializeField] [Range(0.1f, 0.9f)] private float burnHealthThreshold = 0.35f; // Starts burning below 35% HP
        [SerializeField] private Material hullFireMaterial;

        // Runtime state
        private ParticleSystem[] thrusterParticleSystems;
        private ParticleSystem.EmissionModule[] thrusterEmissionModules;
        private SpriteRenderer shipSpriteRenderer;
        private Material originalShipMaterial;
        private Material shipHullFireInstance;
        private EnemyBase enemyBase;
        private PlayerHealth playerHealth;
        private bool isPlayer = false;
        private float currentBurnAmount = 0f;
        private float targetBurnAmount = 0f;
        private float lastSetBurnAmount = -1f; // dirty-check: skip SetFloat when value unchanged
        private Vector3 previousPosition;
        private float currentSpeed = 0f;
        private float thrusterUpdateTimer; // throttle thruster dynamics updates to 30Hz

        private void Awake()
        {
            isPlayer = GetComponent<PlayerController>() != null || GetComponent<PlayerHealth>() != null;
            enemyBase = GetComponent<EnemyBase>();
            playerHealth = GetComponent<PlayerHealth>();
            shipSpriteRenderer = GetComponent<SpriteRenderer>();

            if (shipSpriteRenderer != null)
            {
                originalShipMaterial = shipSpriteRenderer.sharedMaterial;
            }

            // On the player ship, ShipEngineThrusters manages the twin ion exhaust.
            // ShipFireEffect exclusively handles hull burn damage on player.
            if (isPlayer)
            {
                enableThrusters = false;
            }
        }

        private void Start()
        {
            previousPosition = transform.position;

            if (enableThrusters)
            {
                InitializeThrusters();
            }

            if (enableHullBurnOnDamage && shipSpriteRenderer != null)
            {
                SetupHullFireMaterial();
            }
        }

        private void OnEnable()
        {
            ResetBurn();
            if (thrusterParticleSystems != null)
            {
                for (int i = 0; i < thrusterParticleSystems.Length; i++)
                {
                    if (thrusterParticleSystems[i] != null)
                    {
                        thrusterParticleSystems[i].Clear(true);
                        thrusterParticleSystems[i].Play(true);
                    }
                }
            }
        }

        private void OnDisable()
        {
            ResetBurn();
            if (thrusterParticleSystems != null)
            {
                for (int i = 0; i < thrusterParticleSystems.Length; i++)
                {
                    if (thrusterParticleSystems[i] != null)
                        thrusterParticleSystems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                }
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt > 0.0001f)
            {
                currentSpeed = (transform.position - previousPosition).magnitude / dt;
            }
            previousPosition = transform.position;

            // Throttle thruster dynamics to 30 Hz
            thrusterUpdateTimer -= dt;
            if (thrusterUpdateTimer <= 0f)
            {
                thrusterUpdateTimer = 1f / 30f;
                UpdateThrusterDynamics();
            }

            UpdateHullBurn(dt);
        }

        private void OnDestroy()
        {
            if (shipHullFireInstance != null) Destroy(shipHullFireInstance);
        }

        #region Thruster Flame Setup & Dynamics

        private void InitializeThrusters()
        {
            if (isPlayer) return;

            // Resolve directional orientation
            bool shootUpward = false;
            if (direction == ThrusterDirection.Auto)
            {
                // Player flies up, exhaust shoots down. Enemies fly down, exhaust shoots up.
                shootUpward = !isPlayer;
            }
            else
            {
                shootUpward = (direction == ThrusterDirection.Upward);
            }

            var (flameCol, coreCol) = GetPresetColors();

            // Spawn fluid particle thrusters for each engine nozzle offset
            thrusterParticleSystems = new ParticleSystem[engineOffsets.Length];
            thrusterEmissionModules = new ParticleSystem.EmissionModule[engineOffsets.Length];

            for (int i = 0; i < engineOffsets.Length; i++)
            {
                var thrusterGo = new GameObject($"ThrusterJet_{i}");
                thrusterGo.transform.SetParent(transform, false);
                thrusterGo.transform.localPosition = new Vector3(engineOffsets[i].x, engineOffsets[i].y, 0f);

                // 1. Core Flame Jet (Local Simulation)
                var ps = thrusterGo.AddComponent<ParticleSystem>();
                var psr = thrusterGo.GetComponent<ParticleSystemRenderer>();
                psr.material = VFXMaterials.Fire;
                if (shipSpriteRenderer != null)
                {
                    psr.sortingLayerID = shipSpriteRenderer.sortingLayerID;
                    psr.sortingOrder = shipSpriteRenderer.sortingOrder + sortingOrderOffset;
                }
                else
                {
                    psr.sortingOrder = sortingOrderOffset;
                }

                var main = ps.main;
                main.loop = true;
                main.playOnAwake = true;
                main.simulationSpace = ParticleSystemSimulationSpace.Local;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.20f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 6.0f);
                main.startSize = new ParticleSystem.MinMaxCurve(Mathf.Max(0.18f, flameSize.x * 0.9f), Mathf.Max(0.28f, flameSize.x * 1.3f));
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.gravityModifier = 0f;

                var shape = ps.shape;
                shape.enabled = true;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 6f;
                shape.radius = Mathf.Max(0.02f, flameSize.x * 0.15f);
                shape.rotation = shootUpward ? new Vector3(-90f, 0f, 0f) : new Vector3(90f, 0f, 0f);

                var sol = ps.sizeOverLifetime;
                sol.enabled = true;
                var sizeCurve = new AnimationCurve();
                sizeCurve.AddKey(0f, 0.85f);
                sizeCurve.AddKey(0.2f, 1.0f);
                sizeCurve.AddKey(1f, 0.05f);
                sol.size = new ParticleSystem.MinMaxCurve(1.0f, sizeCurve);

                var rol = ps.rotationOverLifetime;
                rol.enabled = true;
                rol.z = new ParticleSystem.MinMaxCurve(-3.0f, 3.0f);

                var emission = ps.emission;
                emission.enabled = true;
                emission.rateOverTime = 38f;

                UpdateThrusterGradient(ps, flameCol, coreCol);

                // 2. Micro-Sparks / Embers
                var sparkGo = new GameObject("Sparks");
                sparkGo.transform.SetParent(thrusterGo.transform, false);
                sparkGo.transform.localPosition = Vector3.zero;

                var spPS = sparkGo.AddComponent<ParticleSystem>();
                var spPSR = sparkGo.GetComponent<ParticleSystemRenderer>();
                spPSR.material = VFXMaterials.Spark;
                if (shipSpriteRenderer != null)
                {
                    spPSR.sortingLayerID = shipSpriteRenderer.sortingLayerID;
                    spPSR.sortingOrder = shipSpriteRenderer.sortingOrder;
                }

                var spMain = spPS.main;
                spMain.loop = true;
                spMain.playOnAwake = true;
                spMain.simulationSpace = ParticleSystemSimulationSpace.World;
                spMain.startLifetime = new ParticleSystem.MinMaxCurve(0.10f, 0.18f);
                spMain.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 4.5f);
                spMain.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.06f);
                spMain.gravityModifier = 0f;

                var spShape = spPS.shape;
                spShape.enabled = true;
                spShape.shapeType = ParticleSystemShapeType.Cone;
                spShape.angle = 18f;
                spShape.radius = 0.02f;
                spShape.rotation = shape.rotation;

                var spSol = spPS.sizeOverLifetime;
                spSol.enabled = true;
                var spCurve = new AnimationCurve();
                spCurve.AddKey(0f, 1.0f);
                spCurve.AddKey(1f, 0.0f);
                spSol.size = new ParticleSystem.MinMaxCurve(1.0f, spCurve);

                var spCol = spPS.colorOverLifetime;
                spCol.enabled = true;
                var spGrad = new Gradient();
                spGrad.SetKeys(
                    new GradientColorKey[]
                    {
                        new GradientColorKey(Color.white, 0f),
                        new GradientColorKey(coreCol, 0.5f),
                        new GradientColorKey(flameCol, 1f)
                    },
                    new GradientAlphaKey[]
                    {
                        new GradientAlphaKey(1f, 0f),
                        new GradientAlphaKey(0.8f, 0.6f),
                        new GradientAlphaKey(0f, 1f)
                    }
                );
                spCol.color = new ParticleSystem.MinMaxGradient(spGrad);

                var spEmission = spPS.emission;
                spEmission.enabled = true;
                spEmission.rateOverTime = 12f;

                thrusterParticleSystems[i] = ps;
                thrusterEmissionModules[i] = emission;
            }
        }

        private void UpdateThrusterGradient(ParticleSystem ps, Color flame, Color core)
        {
            if (ps == null) return;
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(core, 0.2f),
                    new GradientColorKey(flame, 0.6f),
                    new GradientColorKey(flame * 0.25f, 1f)
                },
                new GradientAlphaKey[]
                {
                    new GradientAlphaKey(1.0f, 0f),
                    new GradientAlphaKey(0.9f, 0.4f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            col.color = new ParticleSystem.MinMaxGradient(grad);
        }

        private void UpdateThrusterDynamics()
        {
            if (thrusterParticleSystems == null || thrusterParticleSystems.Length == 0) return;

            float speedScale = Mathf.Clamp(1.0f + currentSpeed * 0.08f, 1.0f, 1.7f);
            for (int i = 0; i < thrusterParticleSystems.Length; i++)
            {
                if (thrusterParticleSystems[i] != null)
                {
                    thrusterEmissionModules[i].rateOverTime = 38f * speedScale;
                }
            }
        }

        #endregion

        #region Hull Burning & Damage Fire

        private void SetupHullFireMaterial()
        {
            var shader = Shader.Find("Starfall/ShipHullFire");
            if (shader == null) return;

            shipHullFireInstance = new Material(shader)
            {
                name = $"{gameObject.name}_HullFireMat"
            };

            var (flameCol, coreCol) = GetPresetColors();
            shipHullFireInstance.SetColor("_BurnColor", flameCol);
            shipHullFireInstance.SetColor("_BurnCoreColor", coreCol);
            shipHullFireInstance.SetFloat("_FlameIntensity", flameIntensity);
            shipHullFireInstance.SetFloat("_BurnAmount", 0f);

            shipSpriteRenderer.material = shipHullFireInstance;
        }

        private void UpdateHullBurn(float dt)
        {
            if (!enableHullBurnOnDamage || shipHullFireInstance == null) return;

            float healthPercent = 1.0f;

            if (isPlayer && playerHealth != null)
            {
                if (playerHealth.MaxHull > 0)
                    healthPercent = (float)playerHealth.CurrentHull / playerHealth.MaxHull;
            }
            else if (enemyBase != null)
            {
                if (enemyBase.MaxHealth > 0)
                    healthPercent = (float)enemyBase.CurrentHealth / enemyBase.MaxHealth;
            }

            targetBurnAmount = healthPercent <= burnHealthThreshold
                ? Mathf.InverseLerp(burnHealthThreshold, 0.0f, healthPercent) * 0.85f
                : 0.0f;

            currentBurnAmount = Mathf.MoveTowards(currentBurnAmount, targetBurnAmount, dt * 1.5f);

            // Only push the value to the GPU driver when it has actually changed.
            if (Mathf.Abs(currentBurnAmount - lastSetBurnAmount) > 0.0005f)
            {
                lastSetBurnAmount = currentBurnAmount;
                shipHullFireInstance.SetFloat("_BurnAmount", currentBurnAmount);
            }
        }

        public void TriggerDamageBurn(float duration = 1.0f, float intensity = 0.5f)
        {
            StartCoroutine(TemporaryBurnRoutine(duration, intensity));
        }

        private IEnumerator TemporaryBurnRoutine(float duration, float intensity)
        {
            if (shipHullFireInstance == null) yield break;

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float burn = Mathf.Sin(t * Mathf.PI) * intensity;
                shipHullFireInstance.SetFloat("_BurnAmount", Mathf.Max(currentBurnAmount, burn));
                yield return null;
            }
        }

        public void ResetBurn()
        {
            currentBurnAmount = 0f;
            targetBurnAmount = 0f;
            lastSetBurnAmount = -1f; // force re-upload on next frame
            if (shipHullFireInstance != null)
                shipHullFireInstance.SetFloat("_BurnAmount", 0f);
        }

        #endregion

        #region Color Configurations

        public void SetPreset(FireColorPreset preset)
        {
            colorPreset = preset;
            ApplyColors();
        }

        public void SetCustomColors(Color flame, Color core)
        {
            colorPreset = FireColorPreset.Custom;
            customFlameColor = flame;
            customCoreColor = core;
            ApplyColors();
        }

        private void ApplyColors()
        {
            var (flameCol, coreCol) = GetPresetColors();

            if (thrusterParticleSystems != null)
            {
                for (int i = 0; i < thrusterParticleSystems.Length; i++)
                {
                    if (thrusterParticleSystems[i] != null)
                        UpdateThrusterGradient(thrusterParticleSystems[i], flameCol, coreCol);
                }
            }

            if (shipHullFireInstance != null)
            {
                shipHullFireInstance.SetColor("_BurnColor", flameCol);
                shipHullFireInstance.SetColor("_BurnCoreColor", coreCol);
            }
        }

        public (Color flame, Color core) GetPresetColors()
        {
            return colorPreset switch
            {
                FireColorPreset.PlayerIonCyan => (
                    new Color(0.0f, 0.85f, 1.0f, 1.0f),    // Neon Cyan
                    new Color(0.9f, 0.98f, 1.0f, 1.0f)     // White Core
                ),
                FireColorPreset.EnemyCrimson => (
                    new Color(1.0f, 0.25f, 0.05f, 1.0f),   // Crimson-Orange
                    new Color(1.0f, 0.92f, 0.35f, 1.0f)    // Golden-Yellow Core
                ),
                FireColorPreset.EliteVoidPurple => (
                    new Color(0.72f, 0.1f, 1.0f, 1.0f),    // Void Purple
                    new Color(0.96f, 0.75f, 1.0f, 1.0f)    // Lavender Core
                ),
                FireColorPreset.BossToxicGreen => (
                    new Color(0.05f, 1.0f, 0.35f, 1.0f),   // Toxic Acid Green
                    new Color(0.85f, 1.0f, 0.92f, 1.0f)    // Mint Core
                ),
                FireColorPreset.BossSupernova => (
                    new Color(1.0f, 0.15f, 0.0f, 1.0f),    // Deep Supernova Red
                    new Color(1.0f, 0.88f, 0.2f, 1.0f)     // Solar Yellow Core
                ),
                FireColorPreset.Custom => (customFlameColor, customCoreColor),
                _ => (customFlameColor, customCoreColor)
            };
        }

        #endregion
    }
}
