using UnityEngine;
using UnityEditor;
using Starfall.Core;

public static class Prototype2Setup
{
    [MenuItem("STARFALL/Setup Prototype 2")]
    public static void SetupPrototype2()
    {
        var poolManagerGo = GameObject.Find("PoolManager");
        if (poolManagerGo == null)
        {
            Debug.LogError("PoolManager not found in scene!");
            return;
        }

        var poolManager = poolManagerGo.GetComponent<ObjectPool>();
        var so = new SerializedObject(poolManager);
        var initialPools = so.FindProperty("initialPools");

        // Use saved FighterEnemy prefab
        var fighterPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/FighterEnemy.prefab");
        if (fighterPrefab != null)
        {
            AddPoolEntry(initialPools, "FighterEnemy", fighterPrefab, 10);
        }

        // Create EnemyProjectile prefab
        var projGo = new GameObject("EnemyProjectile");
        var projSprite = projGo.AddComponent<SpriteRenderer>();
        projSprite.color = new Color(1f, 1f, 0f);
        var projCol = projGo.AddComponent<BoxCollider2D>();
        projCol.isTrigger = true;
        var projRb = projGo.AddComponent<Rigidbody2D>();
        projRb.bodyType = RigidbodyType2D.Kinematic;
        projGo.AddComponent<Starfall.Combat.Projectile>();

        // Create ExplosionVFX prefab
        var explGo = new GameObject("ExplosionVFX");
        var ps = explGo.AddComponent<ParticleSystem>();

        // Main module
        var main = ps.main;
        main.loop = false;
        main.duration = 0.6f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(3f, 8f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.1f, 0.4f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.6f, 0f), new Color(1f, 0.2f, 0f));
        main.gravityModifier = 0f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        // Emission — burst only
        var em = ps.emission;
        em.rateOverTime = 0;
        em.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 20, 25) });

        // Shape
        var shape = ps.shape;
        shape.shapeType = ParticleSystemShapeType.Sphere;
        shape.radius = 0.2f;

        // Disable velocity-over-lifetime to avoid curve mode warnings
        var vel = ps.velocityOverLifetime;
        vel.enabled = false;

        // Color over lifetime — fade out
        var col = ps.colorOverLifetime;
        col.enabled = true;
        var grad = new Gradient();
        grad.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.yellow, 0f), new GradientColorKey(Color.red, 1f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) }
        );
        col.color = new ParticleSystem.MinMaxGradient(grad);

        explGo.AddComponent<Starfall.VFX.ExplosionVFX>();

        // Add to pools
        AddPoolEntry(initialPools, "EnemyProjectile", projGo, 30);
        AddPoolEntry(initialPools, "ExplosionVFX", explGo, 15);

        so.ApplyModifiedProperties();

        // Setup EnemySpawner
        var spawnerGo = GameObject.Find("EnemySpawner");
        if (spawnerGo != null)
        {
            var spawner = spawnerGo.GetComponent<Starfall.Enemies.EnemySpawner>();
            var spawnerSO = new SerializedObject(spawner);
            var enemyTypes = spawnerSO.FindProperty("enemyTypes");
            enemyTypes.arraySize = 2;
            var scoutType = enemyTypes.GetArrayElementAtIndex(0);
            scoutType.FindPropertyRelative("poolKey").stringValue = "ScoutEnemy";
            scoutType.FindPropertyRelative("weight").floatValue = 3f;
            var fighterType = enemyTypes.GetArrayElementAtIndex(1);
            fighterType.FindPropertyRelative("poolKey").stringValue = "FighterEnemy";
            fighterType.FindPropertyRelative("weight").floatValue = 1f;
            spawnerSO.ApplyModifiedProperties();
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
            UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());

        Debug.Log("[Setup] Prototype 2 pools and spawner updated!");
    }

    private static void AddPoolEntry(SerializedProperty initialPools, string key, GameObject prefab, int count)
    {
        for (int i = 0; i < initialPools.arraySize; i++)
        {
            var existing = initialPools.GetArrayElementAtIndex(i);
            if (existing.FindPropertyRelative("poolKey").stringValue == key)
            {
                return; // Already exists
            }
        }

        initialPools.arraySize++;
        var prop = initialPools.GetArrayElementAtIndex(initialPools.arraySize - 1);
        prop.FindPropertyRelative("poolKey").stringValue = key;
        prop.FindPropertyRelative("prefab").objectReferenceValue = prefab;
        prop.FindPropertyRelative("initialSize").intValue = count;
    }
}
