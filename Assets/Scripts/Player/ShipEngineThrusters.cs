using UnityEngine;
using Starfall.Core;
using Starfall.VFX;

namespace Starfall.Player
{
    /// <summary>
    /// Spawns and manages realistic warship engine fire at the player's stern.
    /// Uses high-density turbulent flame particle exhaust with incandescent white cores,
    /// natural fluid rotation, and seamless lifetime tapering.
    /// Also supports custom animated spritesheet/flipbook fire plumes if provided.
    /// Tints dynamically per warship fleet class.
    /// </summary>
    public class ShipEngineThrusters : MonoBehaviour
    {
        [Header("Thruster Offsets")]
        [SerializeField] private Vector2 leftThrusterOffset = new Vector2(-0.42f, -0.32f);
        [SerializeField] private Vector2 rightThrusterOffset = new Vector2(0.42f, -0.32f);

        [Header("Materials & Colors")]
        [SerializeField] private Material additiveMaterial;
        [SerializeField] private Color defaultEngineColor = new Color(0.1f, 0.85f, 1f, 1f); // Ion Cyan default

        [Header("Optional Custom Animation")]
        [Tooltip("If you create a custom animated flame flipbook, assign the sliced frames here.")]
        [SerializeField] private Sprite[] customFlameFrames;
        [SerializeField] private float animationFps = 30f;
        public Sprite[] CustomFlameFrames => customFlameFrames;
        public float AnimationFps => animationFps;

        // Particle Systems: Core Ion Jet, World-Space Smoke Contrail, World-Space Sparks
        private ParticleSystem leftCorePS;
        private ParticleSystem rightCorePS;
        private ParticleSystem leftSmokePS;
        private ParticleSystem rightSmokePS;
        private ParticleSystem leftSparkPS;
        private ParticleSystem rightSparkPS;

        private ParticleSystem.MainModule leftCoreMain;
        private ParticleSystem.MainModule rightCoreMain;
        private ParticleSystem.EmissionModule leftCoreEmission;
        private ParticleSystem.EmissionModule rightCoreEmission;

        private ParticleSystem.EmissionModule leftSmokeEmission;
        private ParticleSystem.EmissionModule rightSmokeEmission;
        private ParticleSystem.EmissionModule leftSparkEmission;
        private ParticleSystem.EmissionModule rightSparkEmission;

        // Throttle State
        private float thrustFactor = 0f;

        private void Start()
        {
            SetupThrusters();
            ApplyShipColorIdentity();
        }

        public void SetupThrusters()
        {
            // 1. Left Thruster Root
            var leftRoot = transform.Find("LeftThruster") ?? transform.Find("LeftEngineThruster");
            if (leftRoot == null)
            {
                var go = new GameObject("LeftEngineThruster");
                go.transform.SetParent(transform, false);
                leftRoot = go.transform;
            }
            leftRoot.localPosition = new Vector3(leftThrusterOffset.x, leftThrusterOffset.y, 0f);
            leftRoot.localRotation = Quaternion.identity;

            ConfigureNozzleVFX(
                leftRoot.gameObject,
                ref leftCorePS, ref leftCoreMain, ref leftCoreEmission,
                ref leftSmokePS, ref leftSmokeEmission,
                ref leftSparkPS, ref leftSparkEmission);

            // 2. Right Thruster Root
            var rightRoot = transform.Find("RightThruster") ?? transform.Find("RightEngineThruster");
            if (rightRoot == null)
            {
                var go = new GameObject("RightEngineThruster");
                go.transform.SetParent(transform, false);
                rightRoot = go.transform;
            }
            rightRoot.localPosition = new Vector3(rightThrusterOffset.x, rightThrusterOffset.y, 0f);
            rightRoot.localRotation = Quaternion.identity;

            ConfigureNozzleVFX(
                rightRoot.gameObject,
                ref rightCorePS, ref rightCoreMain, ref rightCoreEmission,
                ref rightSmokePS, ref rightSmokeEmission,
                ref rightSparkPS, ref rightSparkEmission);
        }

        private void ConfigureNozzleVFX(
            GameObject root, 
            ref ParticleSystem corePS, 
            ref ParticleSystem.MainModule coreMain, 
            ref ParticleSystem.EmissionModule coreEmission,
            ref ParticleSystem smokePS,
            ref ParticleSystem.EmissionModule smokeEmission,
            ref ParticleSystem sparkPS,
            ref ParticleSystem.EmissionModule sparkEmission)
        {
            // Clean up any legacy flat quad sprites or flipbooks
            var oldPlume = root.transform.Find("JetPlume");
            if (oldPlume != null) Destroy(oldPlume.gameObject);
            var oldGlow = root.transform.Find("NozzleGlow");
            if (oldGlow != null) Destroy(oldGlow.gameObject);
            var oldFlipbook = root.transform.Find("FlameFlipbook");
            if (oldFlipbook != null) Destroy(oldFlipbook.gameObject);

            var shipSR = GetComponent<SpriteRenderer>();
            int baseSort = shipSR != null ? shipSR.sortingOrder : 10;
            int sortLayerId = shipSR != null ? shipSR.sortingLayerID : 0;

            // =================================================================
            // 1. Fluid Ion Exhaust Core (Local Simulation - stays on nozzle)
            // =================================================================
            corePS = root.GetComponent<ParticleSystem>();
            if (corePS == null) corePS = root.AddComponent<ParticleSystem>();

            var corePSR = root.GetComponent<ParticleSystemRenderer>();
            if (corePSR == null) corePSR = root.AddComponent<ParticleSystemRenderer>();
            corePSR.material = additiveMaterial != null ? additiveMaterial : VFXMaterials.Fire;
            corePSR.sortingLayerID = sortLayerId;
            corePSR.sortingOrder = baseSort - 1;
            corePSR.renderMode = ParticleSystemRenderMode.Billboard;

            coreMain = corePS.main;
            coreMain.loop = true;
            coreMain.playOnAwake = true;
            coreMain.startLifetime = new ParticleSystem.MinMaxCurve(0.14f, 0.22f);
            coreMain.startSpeed = new ParticleSystem.MinMaxCurve(5.0f, 7.5f);
            coreMain.startSize = new ParticleSystem.MinMaxCurve(0.22f, 0.36f);
            coreMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            coreMain.gravityModifier = 0f;
            coreMain.simulationSpace = ParticleSystemSimulationSpace.Local;
            coreMain.startColor = Color.white;

            var coreShape = corePS.shape;
            coreShape.enabled = true;
            coreShape.shapeType = ParticleSystemShapeType.Cone;
            coreShape.angle = 5f;
            coreShape.radius = 0.035f;
            coreShape.rotation = new Vector3(90f, 0f, 0f);

            var coreSol = corePS.sizeOverLifetime;
            coreSol.enabled = true;
            var coreSizeCurve = new AnimationCurve();
            coreSizeCurve.AddKey(0f, 0.85f);
            coreSizeCurve.AddKey(0.2f, 1.0f);
            coreSizeCurve.AddKey(1f, 0.05f);
            coreSol.size = new ParticleSystem.MinMaxCurve(1.0f, coreSizeCurve);

            var coreRol = corePS.rotationOverLifetime;
            coreRol.enabled = true;
            coreRol.z = new ParticleSystem.MinMaxCurve(-3.0f, 3.0f);

            coreEmission = corePS.emission;
            coreEmission.enabled = true;
            coreEmission.rateOverTime = 65f;

            // =================================================================
            // 2. Twin Smoke & Vapor Contrail (World Simulation - trails in space)
            // =================================================================
            var smokeTrans = root.transform.Find("SmokeContrail");
            if (smokeTrans == null)
            {
                var sGo = new GameObject("SmokeContrail");
                sGo.transform.SetParent(root.transform, false);
                smokeTrans = sGo.transform;
            }
            smokeTrans.localPosition = Vector3.zero;
            smokeTrans.localRotation = Quaternion.identity;

            smokePS = smokeTrans.GetComponent<ParticleSystem>();
            if (smokePS == null) smokePS = smokeTrans.gameObject.AddComponent<ParticleSystem>();

            var smokePSR = smokeTrans.GetComponent<ParticleSystemRenderer>();
            if (smokePSR == null) smokePSR = smokeTrans.gameObject.AddComponent<ParticleSystemRenderer>();
            smokePSR.material = VFXMaterials.Smoke;
            smokePSR.sortingLayerID = sortLayerId;
            smokePSR.sortingOrder = baseSort - 2;

            var smokeMain = smokePS.main;
            smokeMain.loop = true;
            smokeMain.playOnAwake = true;
            smokeMain.simulationSpace = ParticleSystemSimulationSpace.World;
            smokeMain.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.55f);
            smokeMain.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.4f);
            smokeMain.startSize = new ParticleSystem.MinMaxCurve(0.14f, 0.20f);
            smokeMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            smokeMain.gravityModifier = 0f;

            var smokeShape = smokePS.shape;
            smokeShape.enabled = true;
            smokeShape.shapeType = ParticleSystemShapeType.Cone;
            smokeShape.angle = 12f;
            smokeShape.radius = 0.04f;
            smokeShape.rotation = new Vector3(90f, 0f, 0f);

            var smokeSol = smokePS.sizeOverLifetime;
            smokeSol.enabled = true;
            var smokeSizeCurve = new AnimationCurve();
            smokeSizeCurve.AddKey(0f, 0.75f);
            smokeSizeCurve.AddKey(0.45f, 1.8f);
            smokeSizeCurve.AddKey(1f, 1.5f);
            smokeSol.size = new ParticleSystem.MinMaxCurve(1.0f, smokeSizeCurve);

            var smokeRol = smokePS.rotationOverLifetime;
            smokeRol.enabled = true;
            smokeRol.z = new ParticleSystem.MinMaxCurve(-2.2f, 2.2f);

            smokeEmission = smokePS.emission;
            smokeEmission.enabled = true;
            smokeEmission.rateOverTime = 25f;

            // =================================================================
            // 3. Twin Ion Sparks & Embers (World Simulation)
            // =================================================================
            var sparkTrans = root.transform.Find("SparkContrail");
            if (sparkTrans == null)
            {
                var spGo = new GameObject("SparkContrail");
                spGo.transform.SetParent(root.transform, false);
                sparkTrans = spGo.transform;
            }
            sparkTrans.localPosition = Vector3.zero;
            sparkTrans.localRotation = Quaternion.identity;

            sparkPS = sparkTrans.GetComponent<ParticleSystem>();
            if (sparkPS == null) sparkPS = sparkTrans.gameObject.AddComponent<ParticleSystem>();

            var sparkPSR = sparkTrans.GetComponent<ParticleSystemRenderer>();
            if (sparkPSR == null) sparkPSR = sparkTrans.gameObject.AddComponent<ParticleSystemRenderer>();
            sparkPSR.material = VFXMaterials.Spark;
            sparkPSR.sortingLayerID = sortLayerId;
            sparkPSR.sortingOrder = baseSort;

            var sparkMain = sparkPS.main;
            sparkMain.loop = true;
            sparkMain.playOnAwake = true;
            sparkMain.simulationSpace = ParticleSystemSimulationSpace.World;
            sparkMain.startLifetime = new ParticleSystem.MinMaxCurve(0.14f, 0.24f);
            sparkMain.startSpeed = new ParticleSystem.MinMaxCurve(3.5f, 6.5f);
            sparkMain.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
            sparkMain.gravityModifier = 0f;

            var sparkShape = sparkPS.shape;
            sparkShape.enabled = true;
            sparkShape.shapeType = ParticleSystemShapeType.Cone;
            sparkShape.angle = 18f;
            sparkShape.radius = 0.03f;
            sparkShape.rotation = new Vector3(90f, 0f, 0f);

            var sparkSol = sparkPS.sizeOverLifetime;
            sparkSol.enabled = true;
            var sparkSizeCurve = new AnimationCurve();
            sparkSizeCurve.AddKey(0f, 1.0f);
            sparkSizeCurve.AddKey(1f, 0.0f);
            sparkSol.size = new ParticleSystem.MinMaxCurve(1.0f, sparkSizeCurve);

            sparkEmission = sparkPS.emission;
            sparkEmission.enabled = true;
            sparkEmission.rateOverTime = 16f;

            UpdateColorGradient(corePS, smokePS, sparkPS, defaultEngineColor);

            if (!corePS.isPlaying) corePS.Play();
            if (!smokePS.isPlaying) smokePS.Play();
            if (!sparkPS.isPlaying) sparkPS.Play();
        }

        public void ApplyShipColorIdentity()
        {
            var spec = ShipUpgradesManager.GetActiveShipSpec();
            Color theme = defaultEngineColor;

            if (spec != null)
            {
                theme = spec.shipClass switch
                {
                    WarshipClass.Viper => new Color(0.1f, 0.85f, 1f, 1f),      // Ion Cyan
                    WarshipClass.Titan => new Color(1f, 0.35f, 0.1f, 1f),      // Plasma Orange-Red
                    WarshipClass.Specter => new Color(0.75f, 0.25f, 1f, 1f),   // Dark Void Violet
                    WarshipClass.Tempest => new Color(0.15f, 1f, 0.85f, 1f),   // High-Voltage Teal
                    WarshipClass.Phoenix => new Color(1f, 0.8f, 0.15f, 1f),    // Solar Gold
                    _ => spec.hullColor
                };

                float width = spec.shipClass switch
                {
                    WarshipClass.Titan => 0.38f,
                    WarshipClass.Tempest => 0.48f,
                    WarshipClass.Phoenix => 0.48f,
                    WarshipClass.Specter => 0.46f,
                    _ => 0.42f
                };

                float thrusterY = spec.shipClass switch
                {
                    WarshipClass.Titan => -0.90f,
                    WarshipClass.Specter => -0.45f,
                    WarshipClass.Tempest => -0.45f,
                    WarshipClass.Phoenix => -0.45f,
                    _ => -0.32f
                };

                leftThrusterOffset = new Vector2(-width, thrusterY);
                rightThrusterOffset = new Vector2(width, thrusterY);

                var leftRoot = transform.Find("LeftThruster") ?? transform.Find("LeftEngineThruster");
                if (leftRoot != null) leftRoot.localPosition = leftThrusterOffset;

                var rightRoot = transform.Find("RightThruster") ?? transform.Find("RightEngineThruster");
                if (rightRoot != null) rightRoot.localPosition = rightThrusterOffset;
            }

            SetThrusterColor(theme);
        }

        public void SetThrusterColor(Color color)
        {
            defaultEngineColor = color;

            if (leftCorePS != null) UpdateColorGradient(leftCorePS, leftSmokePS, leftSparkPS, color);
            if (rightCorePS != null) UpdateColorGradient(rightCorePS, rightSmokePS, rightSparkPS, color);
        }

        private void UpdateColorGradient(ParticleSystem core, ParticleSystem smoke, ParticleSystem spark, Color color)
        {
            if (core != null)
            {
                var col = core.colorOverLifetime;
                col.enabled = true;
                var grad = new Gradient();
                grad.SetKeys(
                    new GradientColorKey[] 
                    { 
                        new GradientColorKey(Color.white, 0f),                                          // White-hot core ignition
                        new GradientColorKey(Color.Lerp(Color.white, color, 0.45f), 0.18f),             // Radiant flame core
                        new GradientColorKey(color, 0.52f),                                             // Rich flame body
                        new GradientColorKey(Color.Lerp(color, new Color(0.02f, 0.1f, 0.5f), 0.6f), 0.8f), // Outer dissipation
                        new GradientColorKey(color * 0.2f, 1f)                                          // Trailing exhaust tip
                    },
                    new GradientAlphaKey[] 
                    { 
                        new GradientAlphaKey(1.0f, 0f), 
                        new GradientAlphaKey(0.95f, 0.35f),
                        new GradientAlphaKey(0.65f, 0.7f),
                        new GradientAlphaKey(0f, 1f) 
                    }
                );
                col.color = new ParticleSystem.MinMaxGradient(grad);
            }

            if (smoke != null)
            {
                var sCol = smoke.colorOverLifetime;
                sCol.enabled = true;
                var sGrad = new Gradient();
                sGrad.SetKeys(
                    new GradientColorKey[]
                    {
                        new GradientColorKey(Color.Lerp(new Color(0.85f, 0.85f, 0.9f), color, 0.35f), 0f), // Soft tinted vapor
                        new GradientColorKey(new Color(0.6f, 0.65f, 0.7f), 0.45f),
                        new GradientColorKey(new Color(0.35f, 0.38f, 0.42f), 1f)
                    },
                    new GradientAlphaKey[]
                    {
                        new GradientAlphaKey(0.38f, 0f),
                        new GradientAlphaKey(0.24f, 0.4f),
                        new GradientAlphaKey(0f, 1f)
                    }
                );
                sCol.color = new ParticleSystem.MinMaxGradient(sGrad);
            }

            if (spark != null)
            {
                var spCol = spark.colorOverLifetime;
                spCol.enabled = true;
                var spGrad = new Gradient();
                spGrad.SetKeys(
                    new GradientColorKey[]
                    {
                        new GradientColorKey(Color.white, 0f),
                        new GradientColorKey(Color.Lerp(Color.white, color, 0.6f), 0.4f),
                        new GradientColorKey(color, 1f)
                    },
                    new GradientAlphaKey[]
                    {
                        new GradientAlphaKey(1f, 0f),
                        new GradientAlphaKey(0.8f, 0.65f),
                        new GradientAlphaKey(0f, 1f)
                    }
                );
                spCol.color = new ParticleSystem.MinMaxGradient(spGrad);
            }
        }

        private void Update()
        {
            using var __perf = Starfall.Core.PerfMarkers.PlayerUpdate.Auto();

            float dt = Time.deltaTime;

            // Dynamic acceleration detection: input, trackpad or ship movement
            bool isMoving = false;
            if (PlayerController.Instance != null && PlayerController.Instance.IsMoving)
            {
                isMoving = true;
            }
            else if (UnityEngine.InputSystem.Keyboard.current != null)
            {
                var kb = UnityEngine.InputSystem.Keyboard.current;
                isMoving = kb.wKey.isPressed || kb.upArrowKey.isPressed || 
                           kb.aKey.isPressed || kb.dKey.isPressed || 
                           kb.sKey.isPressed || kb.downArrowKey.isPressed;
            }

            // Smooth throttle transition
            float targetThrust = isMoving ? 1.0f : 0.0f;
            thrustFactor = Mathf.MoveTowards(thrustFactor, targetThrust, dt * 6.0f);

            // Throttle dynamics: core flame jet
            float targetCoreRate = Mathf.Lerp(50f, 115f, thrustFactor);
            float targetCoreSpeed = Mathf.Lerp(4.5f, 8.5f, thrustFactor);

            // Throttle dynamics: twin smoke & spark contrails
            float targetSmokeRate = Mathf.Lerp(18f, 48f, thrustFactor);
            float targetSparkRate = Mathf.Lerp(10f, 32f, thrustFactor);

            if (leftCorePS != null)
            {
                leftCoreEmission.rateOverTime = targetCoreRate;
                leftCoreMain.startSpeed = targetCoreSpeed;
            }
            if (rightCorePS != null)
            {
                rightCoreEmission.rateOverTime = targetCoreRate;
                rightCoreMain.startSpeed = targetCoreSpeed;
            }

            if (leftSmokePS != null) leftSmokeEmission.rateOverTime = targetSmokeRate;
            if (rightSmokePS != null) rightSmokeEmission.rateOverTime = targetSmokeRate;

            if (leftSparkPS != null) leftSparkEmission.rateOverTime = targetSparkRate;
            if (rightSparkPS != null) rightSparkEmission.rateOverTime = targetSparkRate;
        }
    }
}
