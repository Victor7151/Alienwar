#if UNITY_EDITOR
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Starfall.Core;
using Starfall.VFX;

namespace Starfall.Editor
{
    public static class WarFXIntegrationSetup
    {
        private const string PREFAB_VFX_DIR = "Assets/Prefabs/VFX";
        private const string RESOURCES_VFX_DIR = "Assets/Resources/VFX";
        private const string MAT_DIR = "Assets/Art/Materials";
        private const string RESOURCES_MAT_DIR = "Assets/Resources/VFX/Materials";
        private const string TEX_DIR = "Assets/Art/Textures/VFX";

        [InitializeOnLoadMethod]
        private static void AutoRunOnCompile()
        {
            if (SessionState.GetBool("Starfall_PristineVFX_Ran_V10", false)) return;
            SessionState.SetBool("Starfall_PristineVFX_Ran_V10", true);
            EditorApplication.delayCall += () =>
            {
                IntegrateRealisticVFX();
            };
        }

        [MenuItem("STARFALL/VFX/Build Pristine Realistic Space VFX", priority = 10)]
        public static void IntegrateRealisticVFX()
        {
            Debug.Log("<color=cyan>[WarFXIntegration] Building pristine, realistic space VFX (Multi-layer stretched sparks, kinetic debris, star glint, micro-smoke puffs)...</color>");

            EnsureDirectories();
            ConfigureTextureImporters();
            var mats = CreateMaterials();

            BuildHitSparkPrefabs(mats.sparkMat, mats.flashMat, mats.metalDebrisMat, mats.rockDebrisMat, mats.alienBioMat, mats.smokeMat);
            BuildMuzzleFlashPrefab(mats.flashMat, mats.sparkMat);
            BuildExplosionPrefabs(mats.sparkMat, mats.flashMat, mats.glowMat, mats.additiveSpriteMat, mats.metalDebrisMat, mats.alienBioMat, mats.smokeMat);
            BuildNukePrefab(mats.sparkMat, mats.glowMat, mats.flashMat, mats.smokeMat);

            UpdateScenePools();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("<color=green>[WarFXIntegration] ALL PRISTINE SPACE VFX BUILT & INTEGRATED SUCCESSFULLY!</color>");
        }

        private static void EnsureDirectories()
        {
            if (!Directory.Exists(PREFAB_VFX_DIR)) Directory.CreateDirectory(PREFAB_VFX_DIR);
            if (!Directory.Exists(RESOURCES_VFX_DIR)) Directory.CreateDirectory(RESOURCES_VFX_DIR);
            if (!Directory.Exists(MAT_DIR)) Directory.CreateDirectory(MAT_DIR);
            if (!Directory.Exists(RESOURCES_MAT_DIR)) Directory.CreateDirectory(RESOURCES_MAT_DIR);
            if (!Directory.Exists(TEX_DIR)) Directory.CreateDirectory(TEX_DIR);
        }

        private static void ConfigureTextureImporters()
        {
            string[] texPaths = new[]
            {
                $"{TEX_DIR}/Glow_Circle.png",
                $"{TEX_DIR}/Spark_Needle.png",
                $"{TEX_DIR}/Flash_Star.png"
            };

            foreach (var p in texPaths)
            {
                if (!File.Exists(p)) continue;
                var importer = AssetImporter.GetAtPath(p) as TextureImporter;
                if (importer != null)
                {
                    bool mod = false;
                    if (importer.textureType != TextureImporterType.Default)
                    {
                        importer.textureType = TextureImporterType.Default;
                        mod = true;
                    }
                    if (!importer.alphaIsTransparency)
                    {
                        importer.alphaIsTransparency = true;
                        mod = true;
                    }
                    if (importer.mipmapEnabled)
                    {
                        importer.mipmapEnabled = false;
                        mod = true;
                    }
                    if (mod)
                    {
                        importer.SaveAndReimport();
                    }
                }
            }
        }

        private static (Material sparkMat, Material flashMat, Material glowMat, Material additiveSpriteMat, Material metalDebrisMat, Material rockDebrisMat, Material alienBioMat, Material smokeMat) CreateMaterials()
        {
            Shader urpShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (urpShader == null) urpShader = Shader.Find("Particles/Standard Unlit");
            if (urpShader == null) urpShader = Shader.Find("Mobile/Particles/Additive");

            Texture sparkTex = AssetDatabase.LoadAssetAtPath<Texture>("Assets/Art/VFX/Textures/spark_02.png")
                            ?? AssetDatabase.LoadAssetAtPath<Texture>($"{TEX_DIR}/Spark_Needle.png");
            Texture flashTex = AssetDatabase.LoadAssetAtPath<Texture>("Assets/Art/VFX/Textures/star_06.png")
                            ?? AssetDatabase.LoadAssetAtPath<Texture>($"{TEX_DIR}/Flash_Star.png");
            Texture glowTex = AssetDatabase.LoadAssetAtPath<Texture>("Assets/Art/VFX/Textures/circle_02.png")
                           ?? AssetDatabase.LoadAssetAtPath<Texture>($"{TEX_DIR}/Glow_Circle.png");
            Texture metalDebrisTex = AssetDatabase.LoadAssetAtPath<Texture>("Assets/Art/VFX/Textures/dirt_01.png");
            Texture rockDebrisTex = AssetDatabase.LoadAssetAtPath<Texture>("Assets/Art/VFX/Textures/dirt_02.png");
            Texture alienBioTex = AssetDatabase.LoadAssetAtPath<Texture>("Assets/Art/VFX/Textures/trace_01.png")
                               ?? AssetDatabase.LoadAssetAtPath<Texture>("Assets/Art/VFX/Textures/spark_06.png");
            Texture smokeTex = AssetDatabase.LoadAssetAtPath<Texture>("Assets/Art/VFX/Textures/smoke_04.png")
                            ?? AssetDatabase.LoadAssetAtPath<Texture>("Assets/Art/VFX/Textures/smoke_01.png");

            Material sparkMat = EnsureAdditiveMat($"{MAT_DIR}/VFX_SparkAdditive.mat", urpShader, sparkTex, Color.white);
            Material flashMat = EnsureAdditiveMat($"{MAT_DIR}/VFX_FlashAdditive.mat", urpShader, flashTex, Color.white);
            Material glowMat = EnsureAdditiveMat($"{MAT_DIR}/VFX_GlowAdditive.mat", urpShader, glowTex, Color.white);
            Material metalDebrisMat = EnsureAdditiveMat($"{MAT_DIR}/VFX_MetalDebrisAdditive.mat", urpShader, metalDebrisTex, Color.white);
            Material rockDebrisMat = EnsureAlphaBlendedMat($"{MAT_DIR}/VFX_RockDebrisAlpha.mat", urpShader, rockDebrisTex, Color.white);
            Material alienBioMat = EnsureAdditiveMat($"{MAT_DIR}/VFX_AlienBioAdditive.mat", urpShader, alienBioTex, Color.white);
            Material smokeMat = EnsureAlphaBlendedMat($"{MAT_DIR}/VFX_SmokeAlpha.mat", urpShader, smokeTex, Color.white);

            // Mirror directly into Resources/VFX/Materials for guaranteed runtime loading in Android standalone builds
            EnsureAdditiveMat($"{RESOURCES_MAT_DIR}/VFX_SparkAdditive.mat", urpShader, sparkTex, Color.white);
            EnsureAdditiveMat($"{RESOURCES_MAT_DIR}/VFX_FlashAdditive.mat", urpShader, flashTex, Color.white);
            EnsureAdditiveMat($"{RESOURCES_MAT_DIR}/VFX_GlowAdditive.mat", urpShader, glowTex, Color.white);
            EnsureAdditiveMat($"{RESOURCES_MAT_DIR}/VFX_MetalDebrisAdditive.mat", urpShader, metalDebrisTex, Color.white);
            EnsureAlphaBlendedMat($"{RESOURCES_MAT_DIR}/VFX_RockDebrisAlpha.mat", urpShader, rockDebrisTex, Color.white);
            EnsureAdditiveMat($"{RESOURCES_MAT_DIR}/VFX_AlienBioAdditive.mat", urpShader, alienBioTex, Color.white);
            EnsureAlphaBlendedMat($"{RESOURCES_MAT_DIR}/VFX_SmokeAlpha.mat", urpShader, smokeTex, Color.white);

            Material additiveSpriteMat = AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/AdditiveSpriteMaterial.mat");
            if (additiveSpriteMat == null)
            {
                additiveSpriteMat = EnsureAdditiveMat("Assets/Art/Materials/AdditiveSpriteMaterial.mat", urpShader, null, Color.white);
            }
            EnsureAdditiveMat($"{RESOURCES_MAT_DIR}/AdditiveSpriteMaterial.mat", urpShader, null, Color.white);

            return (sparkMat, flashMat, glowMat, additiveSpriteMat, metalDebrisMat, rockDebrisMat, alienBioMat, smokeMat);
        }

        private static Material EnsureAdditiveMat(string path, Shader shader, Texture tex, Color tint)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = shader;
            }

            if (tex != null)
            {
                mat.SetTexture("_BaseMap", tex);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            }
            mat.SetColor("_BaseColor", tint);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", tint);

            mat.SetFloat("_Surface", 1.0f); // Transparent
            mat.SetFloat("_Blend", 2.0f);   // Additive
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            mat.SetFloat("_Cull", 0.0f);
            mat.SetFloat("_ZWrite", 0.0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.EnableKeyword("_BLENDMODE_ADD");
            mat.renderQueue = 3000;

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material EnsureAlphaBlendedMat(string path, Shader shader, Texture tex, Color tint)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = shader;
            }

            if (tex != null)
            {
                mat.SetTexture("_BaseMap", tex);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            }
            mat.SetColor("_BaseColor", tint);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", tint);

            mat.SetFloat("_Surface", 1.0f); // Transparent
            mat.SetFloat("_Blend", 0.0f);   // Alpha
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_Cull", 0.0f);
            mat.SetFloat("_ZWrite", 0.0f);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.EnableKeyword("_BLENDMODE_ALPHA");
            mat.renderQueue = 3000;

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void BuildHitSparkPrefabs(
            Material sparkMat, 
            Material flashMat, 
            Material metalDebrisMat, 
            Material rockDebrisMat, 
            Material alienBioMat, 
            Material smokeMat)
        {
            // 1. Metal Hit Spark (Electric cyan/white sharp ricochet sparks, molten gold debris, piercing star glint, hot plasma vapor puff)
            SavePrefabPair(CreateMultiLayerSparkObject(
                "HitSparkMetalVFX", 
                sparkMat, flashMat, metalDebrisMat, smokeMat,
                new Color(0.3f, 0.95f, 1.0f, 1.0f),  // Sparks: Electric cyan
                Color.white,                         // Flash: Pure white
                new Color(1.0f, 0.85f, 0.45f, 1.0f), // Debris: Incandescent gold
                new Color(0.2f, 0.7f, 1.0f, 0.32f),  // Smoke: Hot plasma vapor
                14, 6,                               // Spark count, debris count
                11f, 6.2f,                           // Spark speed, debris speed
                0.12f, 0.09f,                        // Spark size, debris size
                0.18f), "HitSparkMetalVFX");

            // 2. Rock Hit Spark (Molten amber sparks, shattered mineral asteroid rock chunks, warm glint, pulverized rock dust cloud)
            SavePrefabPair(CreateMultiLayerSparkObject(
                "HitSparkRockVFX", 
                sparkMat, flashMat, rockDebrisMat, smokeMat,
                new Color(1.0f, 0.65f, 0.18f, 1.0f), // Sparks: Molten amber
                new Color(1.0f, 0.85f, 0.55f, 1.0f), // Flash: Amber glint
                new Color(0.85f, 0.75f, 0.6f, 1.0f), // Debris: Jagged mineral rock chips
                new Color(0.65f, 0.55f, 0.45f, 0.35f), // Smoke: Pulverized rock dust
                12, 8,                               // Spark count, debris count
                9.5f, 5.5f,                          // Spark speed, debris speed
                0.13f, 0.11f,                        // Spark size, debris size
                0.20f), "HitSparkRockVFX");

            // 3. Alien Hit Spark (Toxic neon lime/magenta plasma sparks, corrosive bio-plasma splatter, toxic glint, acid vapor cloud)
            SavePrefabPair(CreateMultiLayerSparkObject(
                "HitSparkAlienVFX", 
                sparkMat, flashMat, alienBioMat, smokeMat,
                new Color(0.25f, 1.0f, 0.4f, 1.0f),  // Sparks: Toxic neon lime
                new Color(0.6f, 1.0f, 0.65f, 1.0f),  // Flash: Toxic glint
                new Color(0.85f, 0.2f, 1.0f, 1.0f),  // Debris: Corrosive bio-plasma
                new Color(0.2f, 0.85f, 0.35f, 0.32f), // Smoke: Emerald acid smoke
                12, 7,                               // Spark count, debris count
                10f, 5.8f,                           // Spark speed, debris speed
                0.13f, 0.10f,                        // Spark size, debris size
                0.19f), "HitSparkAlienVFX");
        }

        private static GameObject CreateMultiLayerSparkObject(
            string name, 
            Material sparkMat, 
            Material flashMat, 
            Material debrisMat, 
            Material smokeMat,
            Color sparkColor, 
            Color flashColor, 
            Color debrisColor, 
            Color smokeColor,
            int sparkCount, 
            int debrisCount,
            float sparkSpeed, 
            float debrisSpeed,
            float sparkSize, 
            float debrisSize,
            float lifetime)
        {
            GameObject root = new GameObject(name);

            // 1. Primary Layer: Directional Velocity-Stretched Ricochet Sparks
            var ps = root.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.2f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.08f, lifetime);
            main.startSpeed = new ParticleSystem.MinMaxCurve(sparkSpeed * 0.7f, sparkSpeed * 1.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(sparkSize * 0.8f, sparkSize * 1.3f);
            main.startColor = sparkColor;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)sparkCount, (short)(sparkCount + 4)) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 45f;
            shape.radius = 0.04f;
            shape.rotation = new Vector3(0f, 90f, 0f); // Emits outward in 2D plane along impact reflection!

            var sizeOverLife = ps.sizeOverLifetime;
            sizeOverLife.enabled = true;
            AnimationCurve sparkSizeCurve = new AnimationCurve();
            sparkSizeCurve.AddKey(0f, 1f);
            sparkSizeCurve.AddKey(1f, 0f);
            sizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, sparkSizeCurve);

            var ren = ps.GetComponent<ParticleSystemRenderer>();
            ren.sharedMaterial = sparkMat;
            ren.renderMode = ParticleSystemRenderMode.Stretch;
            ren.velocityScale = 0.08f;
            ren.lengthScale = 1.4f;
            ren.cameraVelocityScale = 0f;
            ren.sortingOrder = 24;

            // 2. Second Layer: Kinetic Shattered Debris / Shrapnel (Child "Debris")
            GameObject debrisObj = new GameObject("Debris");
            debrisObj.transform.SetParent(root.transform, false);
            var dPs = debrisObj.AddComponent<ParticleSystem>();
            var dMain = dPs.main;
            dMain.duration = 0.25f;
            dMain.loop = false;
            dMain.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, lifetime * 1.5f);
            dMain.startSpeed = new ParticleSystem.MinMaxCurve(debrisSpeed * 0.6f, debrisSpeed * 1.2f);
            dMain.startSize = new ParticleSystem.MinMaxCurve(debrisSize * 0.7f, debrisSize * 1.3f);
            dMain.startColor = debrisColor;
            dMain.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
            dMain.simulationSpace = ParticleSystemSimulationSpace.World;
            dMain.playOnAwake = false;

            var dEmission = dPs.emission;
            dEmission.rateOverTime = 0;
            dEmission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)debrisCount, (short)(debrisCount + 3)) });

            var dShape = dPs.shape;
            dShape.shapeType = ParticleSystemShapeType.Cone;
            dShape.angle = 60f;
            dShape.radius = 0.05f;
            dShape.rotation = new Vector3(0f, 90f, 0f); // Emits in 2D plane

            var dRotOverLife = dPs.rotationOverLifetime;
            dRotOverLife.enabled = true;
            dRotOverLife.z = new ParticleSystem.MinMaxCurve(-360f * Mathf.Deg2Rad, 360f * Mathf.Deg2Rad);

            var dSizeOverLife = dPs.sizeOverLifetime;
            dSizeOverLife.enabled = true;
            AnimationCurve dSizeCurve = new AnimationCurve();
            dSizeCurve.AddKey(0f, 1f);
            dSizeCurve.AddKey(0.7f, 0.9f);
            dSizeCurve.AddKey(1f, 0f);
            dSizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, dSizeCurve);

            var dRen = dPs.GetComponent<ParticleSystemRenderer>();
            dRen.sharedMaterial = debrisMat;
            dRen.renderMode = ParticleSystemRenderMode.Billboard;
            dRen.sortingOrder = 23;

            // 3. Third Layer: Instantaneous Piercing Star Glint (Child "Flash")
            GameObject flashObj = new GameObject("Flash");
            flashObj.transform.SetParent(root.transform, false);
            var fPs = flashObj.AddComponent<ParticleSystem>();
            var fMain = fPs.main;
            fMain.duration = 0.06f;
            fMain.loop = false;
            fMain.startLifetime = 0.05f;
            fMain.startSpeed = 0f;
            fMain.startSize = 0.42f;
            fMain.startColor = flashColor;
            fMain.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
            fMain.simulationSpace = ParticleSystemSimulationSpace.Local;
            fMain.playOnAwake = false;

            var fEmission = fPs.emission;
            fEmission.rateOverTime = 0;
            fEmission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)1) });

            var fRen = fPs.GetComponent<ParticleSystemRenderer>();
            fRen.sharedMaterial = flashMat;
            fRen.renderMode = ParticleSystemRenderMode.Billboard;
            fRen.sortingOrder = 25;

            // 4. Fourth Layer: Dissipating Hot Micro-Smoke / Vapor Puff (Child "SmokePuff")
            GameObject smokeObj = new GameObject("SmokePuff");
            smokeObj.transform.SetParent(root.transform, false);
            var sPs = smokeObj.AddComponent<ParticleSystem>();
            var sMain = sPs.main;
            sMain.duration = 0.22f;
            sMain.loop = false;
            sMain.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.26f);
            sMain.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.8f);
            sMain.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
            sMain.startColor = smokeColor;
            sMain.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
            sMain.simulationSpace = ParticleSystemSimulationSpace.World;
            sMain.playOnAwake = false;

            var sEmission = sPs.emission;
            sEmission.rateOverTime = 0;
            sEmission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)2) });

            var sShape = sPs.shape;
            sShape.shapeType = ParticleSystemShapeType.Sphere;
            sShape.radius = 0.05f;

            var sSizeOverLife = sPs.sizeOverLifetime;
            sSizeOverLife.enabled = true;
            AnimationCurve sSizeCurve = new AnimationCurve();
            sSizeCurve.AddKey(0f, 0.8f);
            sSizeCurve.AddKey(1f, 2.2f);
            sSizeOverLife.size = new ParticleSystem.MinMaxCurve(1f, sSizeCurve);

            var sColorOverLife = sPs.colorOverLifetime;
            sColorOverLife.enabled = true;
            Gradient sGrad = new Gradient();
            sGrad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(smokeColor, 0f), new GradientColorKey(smokeColor, 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(smokeColor.a, 0f), new GradientAlphaKey(0f, 1f) }
            );
            sColorOverLife.color = sGrad;

            var sRen = sPs.GetComponent<ParticleSystemRenderer>();
            sRen.sharedMaterial = smokeMat;
            sRen.renderMode = ParticleSystemRenderMode.Billboard;
            sRen.sortingOrder = 22;

            // HitSparkVFX Controller for clean zero-GC pooling and multi-layer management
            var sparkComp = root.AddComponent<HitSparkVFX>();
            if (name.Contains("Rock")) sparkComp.SparkType = HitSparkType.Rock;
            else if (name.Contains("Alien")) sparkComp.SparkType = HitSparkType.Alien;
            else sparkComp.SparkType = HitSparkType.Metal;
            sparkComp.EnsureHierarchy();

            return root;
        }

        private static void BuildMuzzleFlashPrefab(Material flashMat, Material sparkMat)
        {
            GameObject root = new GameObject("MuzzleFlashVFX");

            // Instant Star Flash
            var ps = root.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.08f;
            main.loop = false;
            main.startLifetime = 0.05f;
            main.startSpeed = 0f;
            main.startSize = 0.42f;
            main.startColor = new Color(0.4f, 0.85f, 1.0f, 1.0f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.playOnAwake = false;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)1) });

            var ren = ps.GetComponent<ParticleSystemRenderer>();
            ren.sharedMaterial = flashMat;
            ren.renderMode = ParticleSystemRenderMode.Billboard;
            ren.sortingOrder = 24;

            // Ejection sparks (directional stretched sparks in 2D)
            GameObject sparkObj = new GameObject("Sparks");
            sparkObj.transform.SetParent(root.transform, false);
            var sPs = sparkObj.AddComponent<ParticleSystem>();
            var sMain = sPs.main;
            sMain.duration = 0.08f;
            sMain.loop = false;
            sMain.startLifetime = 0.08f;
            sMain.startSpeed = new ParticleSystem.MinMaxCurve(3f, 7f);
            sMain.startSize = 0.09f;
            sMain.startColor = new Color(0.85f, 0.95f, 1f, 1f);
            sMain.simulationSpace = ParticleSystemSimulationSpace.Local;
            sMain.playOnAwake = false;

            var sEmission = sPs.emission;
            sEmission.rateOverTime = 0;
            sEmission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)4) });

            var sShape = sPs.shape;
            sShape.shapeType = ParticleSystemShapeType.Cone;
            sShape.angle = 35f;
            sShape.rotation = new Vector3(0f, 90f, 0f); // Forward in 2D plane

            var sRen = sPs.GetComponent<ParticleSystemRenderer>();
            sRen.sharedMaterial = sparkMat;
            sRen.renderMode = ParticleSystemRenderMode.Stretch;
            sRen.velocityScale = 0.05f;
            sRen.lengthScale = 1.3f;
            sRen.cameraVelocityScale = 0f;
            sRen.sortingOrder = 25;

            var exp = root.AddComponent<ExplosionVFX>();
            var so = new SerializedObject(exp);
            so.FindProperty("durationOverride").floatValue = 0.12f;
            so.ApplyModifiedProperties();

            SavePrefabPair(root, "MuzzleFlashVFX");
        }

        private static void BuildExplosionPrefabs(
            Material sparkMat, 
            Material flashMat, 
            Material glowMat, 
            Material additiveSpriteMat, 
            Material debrisMat, 
            Material alienBioMat, 
            Material smokeMat)
        {
            // Find the 2D explosion animation sprites
            string animFolder = "Assets/ALienware 2D assets/Explosions";
            var dirs = Directory.Exists(animFolder) ? Directory.GetDirectories(animFolder, "Explosion_animation_expanding_wi*") : new string[0];
            Sprite[] frames = null;
            if (dirs.Length > 0)
            {
                frames = Directory.GetFiles(dirs[0], "*.png")
                    .OrderBy(f => f)
                    .Select(f => AssetDatabase.LoadAssetAtPath<Sprite>(f.Replace("\\", "/")))
                    .Where(s => s != null)
                    .ToArray();
            }

            // 1. Standard Enemy & Asteroid Explosion (6 realistic layers: Flash + Plasma Fireball + Stretched Sparks + Kinetic Debris + Dark Smoke + Shockwave)
            // Target size is 1.4m to match fighter/asteroid scale - never covers the screen!
            GameObject smallExp = CreateExplosionObject("ExplosionVFX", frames != null ? frames.Take(30).ToArray() : null,
                targetSize: 1.4f, sparkCount: 20, debrisCount: 8, smokeCount: 6, sparkMat, flashMat, glowMat, debrisMat, smokeMat, additiveSpriteMat, duration: 0.45f);
            SavePrefabPair(smallExp, "ExplosionVFX");
            SavePrefabPair(smallExp, "ExplosionWarFX");

            // 2. Boss Heavy Explosion (Catastrophic multi-layer capital ship blast, 4.2m radius)
            GameObject bossExp = CreateExplosionObject("BossExplosionVFX", frames,
                targetSize: 4.2f, sparkCount: 40, debrisCount: 16, smokeCount: 12, sparkMat, flashMat, glowMat, debrisMat, smokeMat, additiveSpriteMat, duration: 1.25f);
            SavePrefabPair(bossExp, "BossExplosionVFX");
            SavePrefabPair(bossExp, "BossExplosionWarFX");

            // 3. Alien Bio-Plasma Explosion (Toxic lime & violet organic rupture, 2.2m radius)
            GameObject alienExp = CreateAlienExplosionObject("AlienExplosionVFX",
                targetSize: 2.2f, sparkCount: 22, debrisCount: 12, smokeCount: 8, sparkMat, flashMat, glowMat, alienBioMat, smokeMat, duration: 0.65f);
            SavePrefabPair(alienExp, "AlienExplosionVFX");
        }

        private static GameObject CreateExplosionObject(
            string name, 
            Sprite[] frames, 
            float targetSize,
            int sparkCount, 
            int debrisCount,
            int smokeCount,
            Material sparkMat, 
            Material flashMat, 
            Material glowMat, 
            Material debrisMat,
            Material smokeMat,
            Material additiveMat, 
            float duration)
        {
            GameObject root = new GameObject(name);
            root.transform.localScale = Vector3.one;

            // 1. Instant Incandescent Core Flash (star_06 optical glint)
            GameObject flashObj = new GameObject("CoreFlash");
            flashObj.transform.SetParent(root.transform, false);
            var fPs = flashObj.AddComponent<ParticleSystem>();
            var fMain = fPs.main;
            fMain.duration = 0.08f;
            fMain.loop = false;
            fMain.startLifetime = 0.06f;
            fMain.startSpeed = 0f;
            fMain.startSize = targetSize * 0.95f;
            fMain.startColor = new Color(1f, 0.98f, 0.9f, 1f);
            fMain.simulationSpace = ParticleSystemSimulationSpace.Local;
            fMain.playOnAwake = false;

            var fEmission = fPs.emission;
            fEmission.rateOverTime = 0;
            fEmission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 1) });

            var fRen = fPs.GetComponent<ParticleSystemRenderer>();
            fRen.sharedMaterial = flashMat;
            fRen.renderMode = ParticleSystemRenderMode.Billboard;
            fRen.sortingOrder = 24;

            // 2. Volumetric Fireball Plasma Burst (Expanding spherical hot gas)
            GameObject fireObj = new GameObject("FireballPlasma");
            fireObj.transform.SetParent(root.transform, false);
            var firePs = fireObj.AddComponent<ParticleSystem>();
            var fireMain = firePs.main;
            fireMain.duration = 0.35f;
            fireMain.loop = false;
            fireMain.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.35f);
            fireMain.startSpeed = new ParticleSystem.MinMaxCurve(1.6f * (targetSize / 1.35f), 3.5f * (targetSize / 1.35f));
            fireMain.startSize = new ParticleSystem.MinMaxCurve(0.35f * targetSize, 0.65f * targetSize);
            fireMain.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.95f, 0.7f, 1f),
                new Color(1f, 0.42f, 0.1f, 1f)
            );
            fireMain.simulationSpace = ParticleSystemSimulationSpace.World;
            fireMain.playOnAwake = false;

            var fireEmission = firePs.emission;
            fireEmission.rateOverTime = 0;
            int burstCount = Mathf.RoundToInt(10 * (targetSize / 1.35f));
            fireEmission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)burstCount) });

            var fireShape = firePs.shape;
            fireShape.shapeType = ParticleSystemShapeType.Sphere;
            fireShape.radius = 0.12f * targetSize;

            var fireSizeLife = firePs.sizeOverLifetime;
            fireSizeLife.enabled = true;
            AnimationCurve fireCurve = new AnimationCurve();
            fireCurve.AddKey(0f, 0.5f);
            fireCurve.AddKey(0.2f, 1.1f);
            fireCurve.AddKey(1f, 0f);
            fireSizeLife.size = new ParticleSystem.MinMaxCurve(1f, fireCurve);

            var fireRen = firePs.GetComponent<ParticleSystemRenderer>();
            fireRen.sharedMaterial = glowMat;
            fireRen.renderMode = ParticleSystemRenderMode.Billboard;
            fireRen.sortingOrder = 18;

            // 3. Radial Velocity-Stretched Fiery Sparks
            GameObject sparkObj = new GameObject("FierySparks");
            sparkObj.transform.SetParent(root.transform, false);
            var ps = sparkObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.35f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.38f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(5.5f * (targetSize / 1.35f), 10.5f * (targetSize / 1.35f));
            main.startSize = new ParticleSystem.MinMaxCurve(0.11f, 0.20f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(1f, 0.95f, 0.4f, 1f),
                new Color(1f, 0.35f, 0.08f, 1f)
            );
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)sparkCount) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.10f * targetSize;

            var sizeLife = ps.sizeOverLifetime;
            sizeLife.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0f, 1f);
            curve.AddKey(1f, 0f);
            sizeLife.size = new ParticleSystem.MinMaxCurve(1f, curve);

            var ren = ps.GetComponent<ParticleSystemRenderer>();
            ren.sharedMaterial = sparkMat;
            ren.renderMode = ParticleSystemRenderMode.Stretch;
            ren.velocityScale = 0.08f;
            ren.lengthScale = 1.35f;
            ren.cameraVelocityScale = 0f;
            ren.sortingOrder = 23;

            // 4. Kinetic Molten Hull Debris (Tumbling shards)
            GameObject debrisObj = new GameObject("KineticDebris");
            debrisObj.transform.SetParent(root.transform, false);
            var dPs = debrisObj.AddComponent<ParticleSystem>();
            var dMain = dPs.main;
            dMain.duration = 0.4f;
            dMain.loop = false;
            dMain.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
            dMain.startSpeed = new ParticleSystem.MinMaxCurve(3.2f * (targetSize / 1.35f), 7.5f * (targetSize / 1.35f));
            dMain.startSize = new ParticleSystem.MinMaxCurve(0.10f, 0.20f);
            dMain.startColor = new Color(1f, 0.82f, 0.45f, 1f);
            dMain.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
            dMain.simulationSpace = ParticleSystemSimulationSpace.World;
            dMain.playOnAwake = false;

            var dEmission = dPs.emission;
            dEmission.rateOverTime = 0;
            dEmission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)debrisCount) });

            var dShape = dPs.shape;
            dShape.shapeType = ParticleSystemShapeType.Sphere;
            dShape.radius = 0.12f * targetSize;

            var dRot = dPs.rotationOverLifetime;
            dRot.enabled = true;
            dRot.z = new ParticleSystem.MinMaxCurve(-360f * Mathf.Deg2Rad, 360f * Mathf.Deg2Rad);

            var dSizeLife = dPs.sizeOverLifetime;
            dSizeLife.enabled = true;
            AnimationCurve dCurve = new AnimationCurve();
            dCurve.AddKey(0f, 1f);
            dCurve.AddKey(0.7f, 0.85f);
            dCurve.AddKey(1f, 0f);
            dSizeLife.size = new ParticleSystem.MinMaxCurve(1f, dCurve);

            var dRen = dPs.GetComponent<ParticleSystemRenderer>();
            dRen.sharedMaterial = debrisMat;
            dRen.renderMode = ParticleSystemRenderMode.Billboard;
            dRen.sortingOrder = 21;

            // 5. Billowing Dark Space Smoke (Soft expanding vacuum puff)
            GameObject smokeObj = new GameObject("DarkSmoke");
            smokeObj.transform.SetParent(root.transform, false);
            var sPs = smokeObj.AddComponent<ParticleSystem>();
            var sMain = sPs.main;
            sMain.duration = 0.45f;
            sMain.loop = false;
            sMain.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.55f);
            sMain.startSpeed = new ParticleSystem.MinMaxCurve(0.8f * (targetSize / 1.35f), 2.2f * (targetSize / 1.35f));
            sMain.startSize = new ParticleSystem.MinMaxCurve(0.28f * targetSize, 0.58f * targetSize);
            sMain.startColor = new Color(0.24f, 0.22f, 0.20f, 0.42f);
            sMain.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
            sMain.simulationSpace = ParticleSystemSimulationSpace.World;
            sMain.playOnAwake = false;

            var sEmission = sPs.emission;
            sEmission.rateOverTime = 0;
            sEmission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)smokeCount) });

            var sShape = sPs.shape;
            sShape.shapeType = ParticleSystemShapeType.Sphere;
            sShape.radius = 0.14f * targetSize;

            var sRot = sPs.rotationOverLifetime;
            sRot.enabled = true;
            sRot.z = new ParticleSystem.MinMaxCurve(-120f * Mathf.Deg2Rad, 120f * Mathf.Deg2Rad);

            var sSizeLife = sPs.sizeOverLifetime;
            sSizeLife.enabled = true;
            AnimationCurve sCurve = new AnimationCurve();
            sCurve.AddKey(0f, 0.8f);
            sCurve.AddKey(1f, 2.2f);
            sSizeLife.size = new ParticleSystem.MinMaxCurve(1f, sCurve);

            var sColorLife = sPs.colorOverLifetime;
            sColorLife.enabled = true;
            Gradient sGrad = new Gradient();
            sGrad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(new Color(0.24f, 0.22f, 0.20f), 0f), new GradientColorKey(new Color(0.12f, 0.11f, 0.10f), 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.42f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            sColorLife.color = sGrad;

            var sRen = sPs.GetComponent<ParticleSystemRenderer>();
            sRen.sharedMaterial = smokeMat;
            sRen.renderMode = ParticleSystemRenderMode.Billboard;
            sRen.sortingOrder = 16;

            // 6. Expanding Shockwave Glow Ring (circle_05 planar corona)
            GameObject glowObj = new GameObject("ShockwaveGlow");
            glowObj.transform.SetParent(root.transform, false);
            var gPs = glowObj.AddComponent<ParticleSystem>();
            var gMain = gPs.main;
            gMain.duration = 0.22f;
            gMain.loop = false;
            gMain.startLifetime = 0.20f;
            gMain.startSpeed = 0f;
            gMain.startSize = new ParticleSystem.MinMaxCurve(0.2f * targetSize, 1.4f * targetSize);
            gMain.startColor = new Color(1f, 0.6f, 0.2f, 0.85f);
            gMain.simulationSpace = ParticleSystemSimulationSpace.Local;
            gMain.playOnAwake = false;

            var gEmission = gPs.emission;
            gEmission.rateOverTime = 0;
            gEmission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 1) });

            var gRen = gPs.GetComponent<ParticleSystemRenderer>();
            gRen.sharedMaterial = glowMat;
            gRen.renderMode = ParticleSystemRenderMode.Billboard;
            gRen.sortingOrder = 19;

            // Pooled Explosion Handler
            var exp = root.AddComponent<ExplosionVFX>();
            var so = new SerializedObject(exp);
            so.FindProperty("durationOverride").floatValue = duration;
            so.FindProperty("targetWorldRadius").floatValue = targetSize;
            so.FindProperty("useFlipbookAnimation").boolValue = false;
            so.ApplyModifiedProperties();

            return root;
        }

        private static GameObject CreateAlienExplosionObject(
            string name,
            float targetSize,
            int sparkCount,
            int debrisCount,
            int smokeCount,
            Material sparkMat,
            Material flashMat,
            Material glowMat,
            Material bioMat,
            Material smokeMat,
            float duration)
        {
            GameObject root = new GameObject(name);
            root.transform.localScale = Vector3.one;

            // 1. Toxic Emerald Core Flash
            GameObject flashObj = new GameObject("CoreFlash");
            flashObj.transform.SetParent(root.transform, false);
            var fPs = flashObj.AddComponent<ParticleSystem>();
            var fMain = fPs.main;
            fMain.duration = 0.08f;
            fMain.loop = false;
            fMain.startLifetime = 0.06f;
            fMain.startSpeed = 0f;
            fMain.startSize = targetSize * 0.95f;
            fMain.startColor = new Color(0.4f, 1f, 0.5f, 1f);
            fMain.simulationSpace = ParticleSystemSimulationSpace.Local;
            fMain.playOnAwake = false;

            var fEmission = fPs.emission;
            fEmission.rateOverTime = 0;
            fEmission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 1) });

            var fRen = fPs.GetComponent<ParticleSystemRenderer>();
            fRen.sharedMaterial = flashMat;
            fRen.renderMode = ParticleSystemRenderMode.Billboard;
            fRen.sortingOrder = 24;

            // 2. Volumetric Bio-Plasma Burst (Expanding violet & neon green plasma)
            GameObject fireObj = new GameObject("BioPlasmaBurst");
            fireObj.transform.SetParent(root.transform, false);
            var firePs = fireObj.AddComponent<ParticleSystem>();
            var fireMain = firePs.main;
            fireMain.duration = 0.35f;
            fireMain.loop = false;
            fireMain.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.38f);
            fireMain.startSpeed = new ParticleSystem.MinMaxCurve(1.8f * (targetSize / 1.35f), 3.8f * (targetSize / 1.35f));
            fireMain.startSize = new ParticleSystem.MinMaxCurve(0.35f * targetSize, 0.70f * targetSize);
            fireMain.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.35f, 1.0f, 0.45f, 1f),
                new Color(0.85f, 0.25f, 1.0f, 1f)
            );
            fireMain.simulationSpace = ParticleSystemSimulationSpace.World;
            fireMain.playOnAwake = false;

            var fireEmission = firePs.emission;
            fireEmission.rateOverTime = 0;
            int burstCount = Mathf.RoundToInt(12 * (targetSize / 1.35f));
            fireEmission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)burstCount) });

            var fireShape = firePs.shape;
            fireShape.shapeType = ParticleSystemShapeType.Sphere;
            fireShape.radius = 0.12f * targetSize;

            var fireSizeLife = firePs.sizeOverLifetime;
            fireSizeLife.enabled = true;
            AnimationCurve fireCurve = new AnimationCurve();
            fireCurve.AddKey(0f, 0.5f);
            fireCurve.AddKey(0.2f, 1.1f);
            fireCurve.AddKey(1f, 0f);
            fireSizeLife.size = new ParticleSystem.MinMaxCurve(1f, fireCurve);

            var fireRen = firePs.GetComponent<ParticleSystemRenderer>();
            fireRen.sharedMaterial = glowMat;
            fireRen.renderMode = ParticleSystemRenderMode.Billboard;
            fireRen.sortingOrder = 18;

            // 3. Stretched Bio Sparks (Toxic neon lime/cyan)
            GameObject sparkObj = new GameObject("BioSparks");
            sparkObj.transform.SetParent(root.transform, false);
            var ps = sparkObj.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.35f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.20f, 0.40f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(5.5f * (targetSize / 1.35f), 11f * (targetSize / 1.35f));
            main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.3f, 1f, 0.6f, 1f),
                new Color(0.8f, 0.2f, 1f, 1f)
            );
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.playOnAwake = false;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)sparkCount) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.10f * targetSize;

            var sizeLife = ps.sizeOverLifetime;
            sizeLife.enabled = true;
            AnimationCurve curve = new AnimationCurve();
            curve.AddKey(0f, 1f);
            curve.AddKey(1f, 0f);
            sizeLife.size = new ParticleSystem.MinMaxCurve(1f, curve);

            var ren = ps.GetComponent<ParticleSystemRenderer>();
            ren.sharedMaterial = sparkMat;
            ren.renderMode = ParticleSystemRenderMode.Stretch;
            ren.velocityScale = 0.08f;
            ren.lengthScale = 1.35f;
            ren.cameraVelocityScale = 0f;
            ren.sortingOrder = 23;

            // 4. Corrosive Splatter & Bio Shards (trace_01)
            GameObject debrisObj = new GameObject("CorrosiveDebris");
            debrisObj.transform.SetParent(root.transform, false);
            var dPs = debrisObj.AddComponent<ParticleSystem>();
            var dMain = dPs.main;
            dMain.duration = 0.4f;
            dMain.loop = false;
            dMain.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.45f);
            dMain.startSpeed = new ParticleSystem.MinMaxCurve(3.2f * (targetSize / 1.35f), 7.5f * (targetSize / 1.35f));
            dMain.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.22f);
            dMain.startColor = new Color(0.85f, 0.2f, 1.0f, 1f);
            dMain.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
            dMain.simulationSpace = ParticleSystemSimulationSpace.World;
            dMain.playOnAwake = false;

            var dEmission = dPs.emission;
            dEmission.rateOverTime = 0;
            dEmission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)debrisCount) });

            var dShape = dPs.shape;
            dShape.shapeType = ParticleSystemShapeType.Sphere;
            dShape.radius = 0.12f * targetSize;

            var dRot = dPs.rotationOverLifetime;
            dRot.enabled = true;
            dRot.z = new ParticleSystem.MinMaxCurve(-360f * Mathf.Deg2Rad, 360f * Mathf.Deg2Rad);

            var dSizeLife = dPs.sizeOverLifetime;
            dSizeLife.enabled = true;
            AnimationCurve dCurve = new AnimationCurve();
            dCurve.AddKey(0f, 1f);
            dCurve.AddKey(0.7f, 0.85f);
            dCurve.AddKey(1f, 0f);
            dSizeLife.size = new ParticleSystem.MinMaxCurve(1f, dCurve);

            var dRen = dPs.GetComponent<ParticleSystemRenderer>();
            dRen.sharedMaterial = bioMat;
            dRen.renderMode = ParticleSystemRenderMode.Billboard;
            dRen.sortingOrder = 21;

            // 5. Emerald Toxic Acid Smoke
            GameObject smokeObj = new GameObject("ToxicSmoke");
            smokeObj.transform.SetParent(root.transform, false);
            var sPs = smokeObj.AddComponent<ParticleSystem>();
            var sMain = sPs.main;
            sMain.duration = 0.45f;
            sMain.loop = false;
            sMain.startLifetime = new ParticleSystem.MinMaxCurve(0.35f, 0.55f);
            sMain.startSpeed = new ParticleSystem.MinMaxCurve(0.8f * (targetSize / 1.35f), 2.2f * (targetSize / 1.35f));
            sMain.startSize = new ParticleSystem.MinMaxCurve(0.28f * targetSize, 0.58f * targetSize);
            sMain.startColor = new Color(0.2f, 0.85f, 0.35f, 0.38f);
            sMain.startRotation = new ParticleSystem.MinMaxCurve(0f, 360f * Mathf.Deg2Rad);
            sMain.simulationSpace = ParticleSystemSimulationSpace.World;
            sMain.playOnAwake = false;

            var sEmission = sPs.emission;
            sEmission.rateOverTime = 0;
            sEmission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)smokeCount) });

            var sShape = sPs.shape;
            sShape.shapeType = ParticleSystemShapeType.Sphere;
            sShape.radius = 0.14f * targetSize;

            var sRot = sPs.rotationOverLifetime;
            sRot.enabled = true;
            sRot.z = new ParticleSystem.MinMaxCurve(-120f * Mathf.Deg2Rad, 120f * Mathf.Deg2Rad);

            var sSizeLife = sPs.sizeOverLifetime;
            sSizeLife.enabled = true;
            AnimationCurve sCurve = new AnimationCurve();
            sCurve.AddKey(0f, 0.8f);
            sCurve.AddKey(1f, 2.2f);
            sSizeLife.size = new ParticleSystem.MinMaxCurve(1f, sCurve);

            var sColorLife = sPs.colorOverLifetime;
            sColorLife.enabled = true;
            Gradient sGrad = new Gradient();
            sGrad.SetKeys(
                new GradientColorKey[] { new GradientColorKey(new Color(0.2f, 0.85f, 0.35f), 0f), new GradientColorKey(new Color(0.1f, 0.45f, 0.2f), 1f) },
                new GradientAlphaKey[] { new GradientAlphaKey(0.38f, 0f), new GradientAlphaKey(0f, 1f) }
            );
            sColorLife.color = sGrad;

            var sRen = sPs.GetComponent<ParticleSystemRenderer>();
            sRen.sharedMaterial = smokeMat;
            sRen.renderMode = ParticleSystemRenderMode.Billboard;
            sRen.sortingOrder = 16;

            // 6. Bio Shockwave Ring
            GameObject glowObj = new GameObject("ShockwaveGlow");
            glowObj.transform.SetParent(root.transform, false);
            var gPs = glowObj.AddComponent<ParticleSystem>();
            var gMain = gPs.main;
            gMain.duration = 0.22f;
            gMain.loop = false;
            gMain.startLifetime = 0.20f;
            gMain.startSpeed = 0f;
            gMain.startSize = new ParticleSystem.MinMaxCurve(0.2f * targetSize, 1.4f * targetSize);
            gMain.startColor = new Color(0.35f, 1f, 0.45f, 0.85f);
            gMain.simulationSpace = ParticleSystemSimulationSpace.Local;
            gMain.playOnAwake = false;

            var gEmission = gPs.emission;
            gEmission.rateOverTime = 0;
            gEmission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 1) });

            var gRen = gPs.GetComponent<ParticleSystemRenderer>();
            gRen.sharedMaterial = glowMat;
            gRen.renderMode = ParticleSystemRenderMode.Billboard;
            gRen.sortingOrder = 19;

            var exp = root.AddComponent<ExplosionVFX>();
            var so = new SerializedObject(exp);
            so.FindProperty("durationOverride").floatValue = duration;
            so.FindProperty("targetWorldRadius").floatValue = targetSize;
            so.FindProperty("useFlipbookAnimation").boolValue = false;
            so.ApplyModifiedProperties();

            return root;
        }

        private static void BuildNukePrefab(Material sparkMat, Material glowMat, Material flashMat, Material smokeMat)
        {
            GameObject root = new GameObject("NukeDetonationVFX");
            root.transform.localScale = Vector3.one;

            // 1. Blinding Core Flash
            GameObject flashObj = new GameObject("CoreFlash");
            flashObj.transform.SetParent(root.transform, false);
            var fPs = flashObj.AddComponent<ParticleSystem>();
            var fMain = fPs.main;
            fMain.duration = 0.12f;
            fMain.loop = false;
            fMain.startLifetime = 0.09f;
            fMain.startSpeed = 0f;
            fMain.startSize = 9.0f;
            fMain.startColor = new Color(1f, 0.98f, 0.9f, 1f);
            fMain.simulationSpace = ParticleSystemSimulationSpace.Local;
            fMain.playOnAwake = false;

            var fEmission = fPs.emission;
            fEmission.rateOverTime = 0;
            fEmission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 1) });

            var fRen = fPs.GetComponent<ParticleSystemRenderer>();
            fRen.sharedMaterial = flashMat;
            fRen.renderMode = ParticleSystemRenderMode.Billboard;
            fRen.sortingOrder = 27;

            // 2. Huge Expanding Chromatic Shockwave Corona
            var ps = root.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.duration = 0.6f;
            main.loop = false;
            main.startLifetime = 0.55f;
            main.startSpeed = 0f;
            main.startSize = new ParticleSystem.MinMaxCurve(1f, 14f);
            main.startColor = new Color(0.9f, 0.4f, 1f, 0.95f);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.playOnAwake = false;

            var emission = ps.emission;
            emission.rateOverTime = 0;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 1) });

            var ren = ps.GetComponent<ParticleSystemRenderer>();
            ren.sharedMaterial = glowMat;
            ren.renderMode = ParticleSystemRenderMode.Billboard;
            ren.sortingOrder = 25;

            // 3. Radial Shower of Velocity-Stretched Plasma Sparks
            GameObject sparkObj = new GameObject("PlasmaSparks");
            sparkObj.transform.SetParent(root.transform, false);
            var sPs = sparkObj.AddComponent<ParticleSystem>();
            var sMain = sPs.main;
            sMain.duration = 0.45f;
            sMain.loop = false;
            sMain.startLifetime = 0.45f;
            sMain.startSpeed = new ParticleSystem.MinMaxCurve(8f, 18f);
            sMain.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.35f);
            sMain.startColor = new Color(0.45f, 0.9f, 1f, 1f);
            sMain.simulationSpace = ParticleSystemSimulationSpace.World;
            sMain.playOnAwake = false;

            var sEmission = sPs.emission;
            sEmission.rateOverTime = 0;
            sEmission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)60) });

            var sShape = sPs.shape;
            sShape.shapeType = ParticleSystemShapeType.Sphere;

            var sSizeLife = sPs.sizeOverLifetime;
            sSizeLife.enabled = true;
            AnimationCurve sCurve = new AnimationCurve();
            sCurve.AddKey(0f, 1f);
            sCurve.AddKey(1f, 0f);
            sSizeLife.size = new ParticleSystem.MinMaxCurve(1f, sCurve);

            var sRen = sPs.GetComponent<ParticleSystemRenderer>();
            sRen.sharedMaterial = sparkMat;
            sRen.renderMode = ParticleSystemRenderMode.Stretch;
            sRen.velocityScale = 0.08f;
            sRen.lengthScale = 1.4f;
            sRen.cameraVelocityScale = 0f;
            sRen.sortingOrder = 26;

            // 4. Expanding Vacuum Dust Cloud
            GameObject smokeObj = new GameObject("ShockwaveDust");
            smokeObj.transform.SetParent(root.transform, false);
            var smPs = smokeObj.AddComponent<ParticleSystem>();
            var smMain = smPs.main;
            smMain.duration = 0.65f;
            smMain.loop = false;
            smMain.startLifetime = new ParticleSystem.MinMaxCurve(0.45f, 0.65f);
            smMain.startSpeed = new ParticleSystem.MinMaxCurve(2.5f, 6.5f);
            smMain.startSize = new ParticleSystem.MinMaxCurve(1.5f, 3.5f);
            smMain.startColor = new Color(0.35f, 0.30f, 0.40f, 0.35f);
            smMain.simulationSpace = ParticleSystemSimulationSpace.World;
            smMain.playOnAwake = false;

            var smEmission = smPs.emission;
            smEmission.rateOverTime = 0;
            smEmission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, (short)14) });

            var smShape = smPs.shape;
            smShape.shapeType = ParticleSystemShapeType.Sphere;

            var smRen = smPs.GetComponent<ParticleSystemRenderer>();
            smRen.sharedMaterial = smokeMat;
            smRen.renderMode = ParticleSystemRenderMode.Billboard;
            smRen.sortingOrder = 24;

            var exp = root.AddComponent<ExplosionVFX>();
            var so = new SerializedObject(exp);
            so.FindProperty("durationOverride").floatValue = 0.85f;
            so.FindProperty("targetWorldRadius").floatValue = 14f;
            so.FindProperty("useFlipbookAnimation").boolValue = false;
            so.ApplyModifiedProperties();

            SavePrefabPair(root, "NukeDetonationVFX");
        }

        private static void SavePrefabPair(GameObject obj, string prefabName)
        {
            if (obj == null)
            {
                Debug.LogWarning($"[WarFXIntegration] Skipped {prefabName}: source effect was not found (WarFX prefab missing or renamed).");
                return;
            }
            string p1 = $"{PREFAB_VFX_DIR}/{prefabName}.prefab";
            string p2 = $"{RESOURCES_VFX_DIR}/{prefabName}.prefab";

            PrefabUtility.SaveAsPrefabAsset(obj, p1);
            PrefabUtility.SaveAsPrefabAsset(obj, p2);

            Object.DestroyImmediate(obj);
            Debug.Log($"[WarFXIntegration] Saved pristine VFX: {prefabName}.prefab");
        }

        public static void UpdateScenePools()
        {
            string[] scenePaths = new[]
            {
                "Assets/Scenes/Level1_EarthOrbit.unity",
                "Assets/Scenes/Level2_AsteroidBelt.unity",
                "Assets/Scenes/Level3_DeepNebula.unity",
                "Assets/Scenes/Level4_WarTornWreckage.unity",
                "Assets/Scenes/SampleScene.unity"
            };

            foreach (var scenePath in scenePaths)
            {
                if (!File.Exists(scenePath)) continue;

                var scene = EditorSceneManager.OpenScene(scenePath);
                var poolManagerGo = GameObject.Find("PoolManager");
                if (poolManagerGo != null)
                {
                    var pool = poolManagerGo.GetComponent<ObjectPool>();
                    if (pool != null)
                    {
                        var so = new SerializedObject(pool);
                        var initialPools = so.FindProperty("initialPools");

                        SetOrAddPool(initialPools, "HitSparkMetalVFX", $"{PREFAB_VFX_DIR}/HitSparkMetalVFX.prefab", 40);
                        SetOrAddPool(initialPools, "HitSparkRockVFX", $"{PREFAB_VFX_DIR}/HitSparkRockVFX.prefab", 25);
                        SetOrAddPool(initialPools, "HitSparkAlienVFX", $"{PREFAB_VFX_DIR}/HitSparkAlienVFX.prefab", 25);
                        SetOrAddPool(initialPools, "MuzzleFlashVFX", $"{PREFAB_VFX_DIR}/MuzzleFlashVFX.prefab", 30);
                        SetOrAddPool(initialPools, "ExplosionVFX", $"{PREFAB_VFX_DIR}/ExplosionVFX.prefab", 35);
                        SetOrAddPool(initialPools, "BossExplosionVFX", $"{PREFAB_VFX_DIR}/BossExplosionVFX.prefab", 15);
                        SetOrAddPool(initialPools, "NukeDetonationVFX", $"{PREFAB_VFX_DIR}/NukeDetonationVFX.prefab", 4);

                        so.ApplyModifiedProperties();
                        EditorUtility.SetDirty(pool);
                        EditorSceneManager.MarkSceneDirty(scene);
                        EditorSceneManager.SaveScene(scene);
                    }
                }
            }

            Debug.Log("[WarFXIntegration] Updated ObjectPools with pristine VFX across all scenes.");
        }

        private static void SetOrAddPool(SerializedProperty arrayProp, string poolKey, string prefabPath, int count)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null) return;

            for (int i = 0; i < arrayProp.arraySize; i++)
            {
                var elem = arrayProp.GetArrayElementAtIndex(i);
                var keyProp = elem.FindPropertyRelative("poolKey");
                if (keyProp != null && keyProp.stringValue == poolKey)
                {
                    var pProp = elem.FindPropertyRelative("prefab");
                    if (pProp != null) pProp.objectReferenceValue = prefab;
                    var cProp = elem.FindPropertyRelative("initialSize");
                    if (cProp != null && cProp.intValue < count) cProp.intValue = count;
                    return;
                }
            }

            int idx = arrayProp.arraySize;
            arrayProp.InsertArrayElementAtIndex(idx);
            var newElem = arrayProp.GetArrayElementAtIndex(idx);
            newElem.FindPropertyRelative("poolKey").stringValue = poolKey;
            newElem.FindPropertyRelative("prefab").objectReferenceValue = prefab;
            newElem.FindPropertyRelative("initialSize").intValue = count;
        }
    }
}
#endif
