using System.IO;
using UnityEditor;
using UnityEngine;
using Starfall.VFX;

namespace Starfall.Editor
{
    public static class ShipFireSetup
    {
        private const string MaterialsFolder = "Assets/Art/Materials/Fire";

        [MenuItem("STARFALL/Setup Ship Fire Shaders and Thrusters")]
        public static void SetupAllShipFire()
        {
            EnsureFolderExists(MaterialsFolder);

            // 1. Create / Update shared Materials for artists & designers to inspect/tweak
            var procShader = Shader.Find("Starfall/ProceduralFire");
            var hullShader = Shader.Find("Starfall/ShipHullFire");

            if (procShader == null)
            {
                Debug.LogError("[ShipFireSetup] Could not find shader 'Starfall/ProceduralFire'!");
                return;
            }

            var matPlayer = CreateOrUpdateMaterial($"{MaterialsFolder}/M_ThrusterFire_Player_Cyan.mat", procShader,
                new Color(0.0f, 0.85f, 1.0f, 1.0f), new Color(0.9f, 0.98f, 1.0f, 1.0f), 2.5f);

            var matEnemy = CreateOrUpdateMaterial($"{MaterialsFolder}/M_ThrusterFire_Enemy_OrangeRed.mat", procShader,
                new Color(1.0f, 0.25f, 0.05f, 1.0f), new Color(1.0f, 0.92f, 0.35f, 1.0f), 2.2f);

            var matElite = CreateOrUpdateMaterial($"{MaterialsFolder}/M_ThrusterFire_Elite_VoidPurple.mat", procShader,
                new Color(0.72f, 0.1f, 1.0f, 1.0f), new Color(0.96f, 0.75f, 1.0f, 1.0f), 2.4f);

            var matToxic = CreateOrUpdateMaterial($"{MaterialsFolder}/M_ThrusterFire_Boss_ToxicGreen.mat", procShader,
                new Color(0.05f, 1.0f, 0.35f, 1.0f), new Color(0.85f, 1.0f, 0.92f, 1.0f), 2.5f);

            var matSupernova = CreateOrUpdateMaterial($"{MaterialsFolder}/M_ThrusterFire_Boss_Supernova.mat", procShader,
                new Color(1.0f, 0.15f, 0.0f, 1.0f), new Color(1.0f, 0.88f, 0.2f, 1.0f), 2.8f);

            if (hullShader != null)
            {
                CreateOrUpdateHullFireMaterial($"{MaterialsFolder}/M_HullFire_BurningShip.mat", hullShader);
            }

            AssetDatabase.SaveAssets();

            // 2. Configure Player Prefab
            ConfigurePrefab("Assets/Prefabs/Player/PlayerShip.prefab", FireColorPreset.PlayerIonCyan,
                ThrusterDirection.Downward,
                new Vector2[] { new Vector2(-0.42f, -0.32f), new Vector2(0.42f, -0.32f) },
                new Vector2(0.28f, 0.75f));

            // 3. Configure Grunt Enemies (Crimson/Orange flames)
            ConfigurePrefab("Assets/Prefabs/Enemies/FighterEnemy.prefab", FireColorPreset.EnemyCrimson,
                ThrusterDirection.Upward,
                new Vector2[] { new Vector2(-0.24f, 0.42f), new Vector2(0.24f, 0.42f) },
                new Vector2(0.22f, 0.65f));

            ConfigurePrefab("Assets/Prefabs/Enemies/ScoutEnemy.prefab", FireColorPreset.EnemyCrimson,
                ThrusterDirection.Upward,
                new Vector2[] { new Vector2(0f, 0.38f) },
                new Vector2(0.26f, 0.7f));

            // 4. Configure Elite & Capital Ships (Void Purple flames)
            ConfigurePrefab("Assets/Prefabs/Enemies/InvaderCarrier.prefab", FireColorPreset.EliteVoidPurple,
                ThrusterDirection.Upward,
                new Vector2[] { new Vector2(-0.55f, 0.65f), new Vector2(0.55f, 0.65f) },
                new Vector2(0.42f, 1.1f));

            ConfigurePrefab("Assets/Prefabs/Enemies/MiniBossGunship.prefab", FireColorPreset.EliteVoidPurple,
                ThrusterDirection.Upward,
                new Vector2[] { new Vector2(-0.42f, 0.52f), new Vector2(0.42f, 0.52f) },
                new Vector2(0.38f, 1.0f));

            // 5. Configure Bosses (Toxic Green and Supernova Red flames)
            ConfigurePrefab("Assets/Prefabs/Enemies/HiveQueenBoss.prefab", FireColorPreset.BossToxicGreen,
                ThrusterDirection.Upward,
                new Vector2[] { new Vector2(-0.68f, 0.85f), new Vector2(0.68f, 0.85f) },
                new Vector2(0.55f, 1.35f));

            ConfigurePrefab("Assets/Prefabs/Enemies/PlasmaDreadnought.prefab", FireColorPreset.BossToxicGreen,
                ThrusterDirection.Upward,
                new Vector2[] { new Vector2(-0.58f, 0.78f), new Vector2(0.58f, 0.78f) },
                new Vector2(0.48f, 1.25f));

            ConfigurePrefab("Assets/Prefabs/Enemies/InfernoColossusBoss.prefab", FireColorPreset.BossSupernova,
                ThrusterDirection.Upward,
                new Vector2[] {
                    new Vector2(-0.75f, 0.90f),
                    new Vector2(-0.28f, 0.98f),
                    new Vector2(0.28f, 0.98f),
                    new Vector2(0.75f, 0.90f)
                },
                new Vector2(0.55f, 1.5f));

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log("<color=#00FFAA><b>[ShipFireSetup] All ship fire shaders, materials, and thruster setups completed successfully!</b></color>");
        }

        private static Material CreateOrUpdateMaterial(string path, Shader shader, Color flame, Color core, float intensity)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = shader;
            }

            mat.SetColor("_FlameColor", flame);
            mat.SetColor("_CoreColor", core);
            mat.SetFloat("_Intensity", intensity);
            mat.SetFloat("_ScrollSpeed", 4.5f);
            mat.SetFloat("_Distortion", 0.16f);
            mat.SetFloat("_FlickerSpeed", 14f);
            mat.SetFloat("_FlickerAmount", 0.12f);
            mat.SetFloat("_Taper", 1.8f);
            mat.SetFloat("_CoreSize", 0.45f);

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static Material CreateOrUpdateHullFireMaterial(string path, Shader shader)
        {
            var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
            }
            else
            {
                mat.shader = shader;
            }

            mat.SetFloat("_BurnAmount", 0f);
            mat.SetColor("_BurnColor", new Color(1.0f, 0.35f, 0.05f, 1.0f));
            mat.SetColor("_BurnCoreColor", new Color(1.0f, 0.95f, 0.6f, 1.0f));
            mat.SetFloat("_FlameIntensity", 2.2f);
            mat.SetFloat("_BurnSpeed", 3.5f);

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void ConfigurePrefab(string prefabPath, FireColorPreset preset, ThrusterDirection dir, Vector2[] offsets, Vector2 flameSize)
        {
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            if (root == null)
            {
                Debug.LogWarning($"[ShipFireSetup] Could not load prefab at {prefabPath}");
                return;
            }

            try
            {
                var fireEffect = root.GetComponent<ShipFireEffect>();
                if (fireEffect == null)
                {
                    fireEffect = root.AddComponent<ShipFireEffect>();
                }

                // Set serialized properties via SerializedObject to ensure clean prefab serialization
                var so = new SerializedObject(fireEffect);
                so.Update();

                so.FindProperty("colorPreset").enumValueIndex = (int)preset;
                so.FindProperty("direction").enumValueIndex = (int)dir;
                so.FindProperty("flameSize").vector2Value = flameSize;
                so.FindProperty("enableThrusters").boolValue = true;
                so.FindProperty("enableHullBurnOnDamage").boolValue = true;

                var offsetsProp = so.FindProperty("engineOffsets");
                offsetsProp.arraySize = offsets.Length;
                for (int i = 0; i < offsets.Length; i++)
                {
                    offsetsProp.GetArrayElementAtIndex(i).vector2Value = offsets[i];
                }

                so.ApplyModifiedProperties();

                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
                Debug.Log($"[ShipFireSetup] Configured fire effect for: {root.name} ({preset})");
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void EnsureFolderExists(string relativePath)
        {
            if (!AssetDatabase.IsValidFolder(relativePath))
            {
                string parent = Path.GetDirectoryName(relativePath).Replace('\\', '/');
                string folder = Path.GetFileName(relativePath);
                if (!AssetDatabase.IsValidFolder(parent))
                {
                    EnsureFolderExists(parent);
                }
                AssetDatabase.CreateFolder(parent, folder);
            }
        }
    }
}
