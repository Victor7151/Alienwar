using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using Starfall.Core;
using Starfall.VFX;
using Starfall.Enemies;
using Starfall.Combat;
using Starfall.Player;
using Starfall.UI;

/// <summary>
/// Setup script for STARFALL:
/// - Weapon Upgrade (Lv 1-5) & Starfall Bomb collectibles
/// - Shockwave VFX & Camera Shake
/// - WaveManager with 5 escalating waves
/// - 3-layer Parallax Starfield
/// - Complete HUD (Score, Shield, Hull, Waves, Weapon Level, Bomb Counter, Bomb Button)
/// Run via menu: STARFALL > Setup Phase 1 (Weapons & Bombs)
/// </summary>
public static class Prototype3Setup
{
    [MenuItem("STARFALL/Setup Phase 1 (Weapons & Bombs)")]
    [MenuItem("STARFALL/Setup Prototype 3")]
    public static void SetupPhase1()
    {
        SetupCameraShake();
        SetupShockwavePool();
        SetupPowerUpPools();
        SetupEnemyDropChance();
        SetupWaveManager();
        SetupAsteroidPool();
        SetupBossPool();
        SetupParallaxBackground();
        SetupWaveHUD();
        SetupWeaponAndBombHUD();
        SetupBossHealthBar();
        SetupMissionCompleteUI();

        var scene = UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene();
        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);

        Debug.Log("[Setup] Planet 1 (Earth Orbit) – Full setup complete and scene saved!");
    }

    // ─── Camera Shake ──────────────────────────────────────────────────────────

    private static void SetupCameraShake()
    {
        var cam = Camera.main;
        if (cam != null)
        {
            if (cam.GetComponent<CameraShake>() == null)
            {
                cam.gameObject.AddComponent<CameraShake>();
                Debug.Log("[Setup] CameraShake attached to Main Camera.");
            }
        }
    }

    // ─── Shockwave Pool ────────────────────────────────────────────────────────

    private static void SetupShockwavePool()
    {
        var poolManagerGo = GameObject.Find("PoolManager");
        if (poolManagerGo == null) return;

        var ringSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/Shockwave_Ring.png");
        if (ringSprite == null)
        {
            Debug.LogError("Shockwave_Ring.png not found!");
            return;
        }

        var poolManager = poolManagerGo.GetComponent<ObjectPool>();
        var so = new SerializedObject(poolManager);
        var initialPools = so.FindProperty("initialPools");

        string key = "ShockwaveVFX";
        GameObject go = null;
        SerializedProperty targetPool = null;

        for (int i = 0; i < initialPools.arraySize; i++)
        {
            var p = initialPools.GetArrayElementAtIndex(i);
            if (p.FindPropertyRelative("poolKey").stringValue == key)
            {
                targetPool = p;
                go = p.FindPropertyRelative("prefab").objectReferenceValue as GameObject;
                break;
            }
        }

        if (go == null) go = GameObject.Find(key);
        if (go == null) go = new GameObject(key);

        var sr = go.GetComponent<SpriteRenderer>();
        if (sr == null) sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = ringSprite;
        sr.color = new Color(0.9f, 0.3f, 1f, 0.9f);
        sr.sortingOrder = 30;

        if (go.GetComponent<ShockwaveVFX>() == null) go.AddComponent<ShockwaveVFX>();

        if (targetPool == null)
        {
            initialPools.arraySize++;
            targetPool = initialPools.GetArrayElementAtIndex(initialPools.arraySize - 1);
        }

        targetPool.FindPropertyRelative("poolKey").stringValue = key;
        targetPool.FindPropertyRelative("prefab").objectReferenceValue = go;
        targetPool.FindPropertyRelative("initialSize").intValue = 5;

        so.ApplyModifiedProperties();
        Debug.Log("[Setup] ShockwaveVFX pool configured.");
    }

    // ─── Power-up Pools ────────────────────────────────────────────────────────

    private static void SetupPowerUpPools()
    {
        var poolManagerGo = GameObject.Find("PoolManager");
        if (poolManagerGo == null)
        {
            Debug.LogError("PoolManager not found!");
            return;
        }

        var puSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/PowerUp_Diamond.png");
        if (puSprite == null)
        {
            Debug.LogError("PowerUp_Diamond.png sprite not found at Assets/Art/Sprites/PowerUp_Diamond.png!");
            return;
        }

        var poolManager = poolManagerGo.GetComponent<ObjectPool>();
        var so = new SerializedObject(poolManager);
        var initialPools = so.FindProperty("initialPools");

        CreateOrUpdatePowerUpPool(initialPools, "PowerUp_RapidFire",  PowerUpType.RapidFire,     new Color(1f, 0.85f, 0.1f), 10, puSprite);
        CreateOrUpdatePowerUpPool(initialPools, "PowerUp_Shield",     PowerUpType.ShieldRestore,  new Color(0.1f, 0.9f, 1f),  10, puSprite);
        CreateOrUpdatePowerUpPool(initialPools, "PowerUp_Score",      PowerUpType.ScoreBonus,     new Color(0.3f, 1f, 0.4f),  10, puSprite);
        CreateOrUpdatePowerUpPool(initialPools, "PowerUp_Weapon",     PowerUpType.WeaponUpgrade,  new Color(1f, 0.45f, 0.1f), 10, puSprite);
        CreateOrUpdatePowerUpPool(initialPools, "PowerUp_Bomb",       PowerUpType.Bomb,           new Color(0.85f, 0.2f, 1f), 10, puSprite);

        so.ApplyModifiedProperties();
        Debug.Log("[Setup] All 5 power-up pools configured (RapidFire, Shield, Score, WeaponUpgrade, Bomb).");
    }

    private static void CreateOrUpdatePowerUpPool(SerializedProperty pools, string key,
        PowerUpType type, Color tint, int count, Sprite sprite)
    {
        GameObject go = null;
        SerializedProperty targetPool = null;

        for (int i = 0; i < pools.arraySize; i++)
        {
            var p = pools.GetArrayElementAtIndex(i);
            if (p.FindPropertyRelative("poolKey").stringValue == key)
            {
                targetPool = p;
                go = p.FindPropertyRelative("prefab").objectReferenceValue as GameObject;
                break;
            }
        }

        if (go == null) go = GameObject.Find(key);
        if (go == null) go = new GameObject(key);

        var sr = go.GetComponent<SpriteRenderer>();
        if (sr == null) sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = tint;
        sr.sortingOrder = 10;

        var col = go.GetComponent<CircleCollider2D>();
        if (col == null) col = go.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.45f;

        var rb = go.GetComponent<Rigidbody2D>();
        if (rb == null) rb = go.AddComponent<Rigidbody2D>();
        rb.bodyType = RigidbodyType2D.Dynamic;
        rb.gravityScale = 0f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.constraints = RigidbodyConstraints2D.FreezeAll;

        var pu = go.GetComponent<PowerUp>();
        if (pu == null) pu = go.AddComponent<PowerUp>();
        var puSO = new SerializedObject(pu);
        puSO.FindProperty("type").enumValueIndex = (int)type;
        puSO.ApplyModifiedProperties();

        if (targetPool == null)
        {
            pools.arraySize++;
            targetPool = pools.GetArrayElementAtIndex(pools.arraySize - 1);
        }

        targetPool.FindPropertyRelative("poolKey").stringValue = key;
        targetPool.FindPropertyRelative("prefab").objectReferenceValue = go;
        targetPool.FindPropertyRelative("initialSize").intValue = count;
    }

    private static void SetupEnemyDropChance()
    {
        var poolManagerGo = GameObject.Find("PoolManager");
        if (poolManagerGo == null) return;

        var poolManager = poolManagerGo.GetComponent<ObjectPool>();
        var so = new SerializedObject(poolManager);
        var pools = so.FindProperty("initialPools");

        string[] puKeys = new string[] {
            "PowerUp_RapidFire",
            "PowerUp_Shield",
            "PowerUp_Score",
            "PowerUp_Weapon",
            "PowerUp_Bomb"
        };

        for (int i = 0; i < pools.arraySize; i++)
        {
            var p = pools.GetArrayElementAtIndex(i);
            var key = p.FindPropertyRelative("poolKey").stringValue;
            if (key == "ScoutEnemy" || key == "FighterEnemy")
            {
                var prefab = p.FindPropertyRelative("prefab").objectReferenceValue as GameObject;
                if (prefab != null)
                {
                    var eb = prefab.GetComponent<EnemyBase>();
                    if (eb != null)
                    {
                        var ebSO = new SerializedObject(eb);
                        ebSO.FindProperty("dropChance").floatValue = 0.45f;
                        var keysProp = ebSO.FindProperty("powerUpPoolKeys");
                        keysProp.arraySize = puKeys.Length;
                        for (int k = 0; k < puKeys.Length; k++)
                        {
                            keysProp.GetArrayElementAtIndex(k).stringValue = puKeys[k];
                        }
                        ebSO.ApplyModifiedProperties();
                    }
                }
            }
        }
        Debug.Log("[Setup] Enemy drop chance tuned with all 5 power-up types.");
    }

    // ─── Wave Manager ──────────────────────────────────────────────────────────
    // WaveManager now builds its own Planet 1 wave table in code.
    // The setup script just ensures the WaveManager GameObject exists.

    private static void SetupWaveManager()
    {
        var go = GameObject.Find("WaveManager");
        if (go == null) go = new GameObject("WaveManager");

        var wm = go.GetComponent<WaveManager>();
        if (wm == null) wm = go.AddComponent<WaveManager>();

        var so = new SerializedObject(wm);

        var spawner = Object.FindAnyObjectByType<EnemySpawner>();
        so.FindProperty("enemySpawner").objectReferenceValue = spawner;
        so.FindProperty("restBetweenWaves").floatValue = 3f;
        // Boss spawn position – top center
        so.FindProperty("bossSpawnPosition").vector3Value = new Vector3(0f, 8.5f, 0f);
        so.FindProperty("bossSpawnDelay").floatValue = 2f;
        so.FindProperty("bossPoolKey").stringValue = "InvaderCarrier";
        // Clear waves array so WaveManager uses BuildPlanet1Waves() at runtime
        so.FindProperty("waves").arraySize = 0;

        so.ApplyModifiedProperties();
        Debug.Log("[Setup] WaveManager configured for Planet 1 (waves built at runtime).");
    }

    // ─── Asteroid Hazard Pool ──────────────────────────────────────────────────

    private static void SetupAsteroidPool()
    {
        var poolManagerGo = GameObject.Find("PoolManager");
        if (poolManagerGo == null) return;

        var poolManager = poolManagerGo.GetComponent<ObjectPool>();
        var so = new SerializedObject(poolManager);
        var pools = so.FindProperty("initialPools");

        // Check if already registered
        for (int i = 0; i < pools.arraySize; i++)
        {
            if (pools.GetArrayElementAtIndex(i).FindPropertyRelative("poolKey").stringValue == "AsteroidHazard")
            {
                Debug.Log("[Setup] AsteroidHazard pool already registered.");
                return;
            }
        }

        var asteroidGo = new GameObject("AsteroidHazard");
        var sr = asteroidGo.AddComponent<SpriteRenderer>();
        // Use a placeholder grey circle sprite – swap for proper art later
        sr.color = new Color(0.55f, 0.55f, 0.6f);
        sr.sortingOrder = 5;

        var col = asteroidGo.AddComponent<CircleCollider2D>();
        col.isTrigger = true;
        col.radius = 0.6f;

        asteroidGo.AddComponent<AsteroidHazard>();

        pools.arraySize++;
        var entry = pools.GetArrayElementAtIndex(pools.arraySize - 1);
        entry.FindPropertyRelative("poolKey").stringValue = "AsteroidHazard";
        entry.FindPropertyRelative("prefab").objectReferenceValue = asteroidGo;
        entry.FindPropertyRelative("initialSize").intValue = 6;

        so.ApplyModifiedProperties();
        Debug.Log("[Setup] AsteroidHazard pool configured.");
    }

    // ─── Invader Carrier Boss Pool ─────────────────────────────────────────────

    private static void SetupBossPool()
    {
        var poolManagerGo = GameObject.Find("PoolManager");
        if (poolManagerGo == null) return;

        var poolManager = poolManagerGo.GetComponent<ObjectPool>();
        var so = new SerializedObject(poolManager);
        var pools = so.FindProperty("initialPools");

        // Check if already registered
        for (int i = 0; i < pools.arraySize; i++)
        {
            if (pools.GetArrayElementAtIndex(i).FindPropertyRelative("poolKey").stringValue == "InvaderCarrier")
            {
                Debug.Log("[Setup] InvaderCarrier pool already registered.");
                return;
            }
        }

        // Build carrier GO – large sprite, multi-part body approximated by quad overlays
        var carrierGo = new GameObject("InvaderCarrier");
        carrierGo.transform.localScale = new Vector3(3f, 2f, 1f); // Big ship!

        var sr = carrierGo.AddComponent<SpriteRenderer>();
        sr.color = new Color(0.3f, 0.7f, 1f); // Phase 1 blue
        sr.sortingOrder = 10;

        // Collider covering the body
        var col = carrierGo.AddComponent<BoxCollider2D>();
        col.isTrigger = true;
        col.size = new Vector2(2.5f, 1.2f);

        var carrier = carrierGo.AddComponent<InvaderCarrier>();

        // Wire the carrier's phaseColorRenderers to include its own SR
        var carrierSO = new SerializedObject(carrier);
        var colorRenderers = carrierSO.FindProperty("phaseColorRenderers");
        colorRenderers.arraySize = 1;
        colorRenderers.GetArrayElementAtIndex(0).objectReferenceValue = sr;
        carrierSO.ApplyModifiedProperties();

        pools.arraySize++;
        var entry = pools.GetArrayElementAtIndex(pools.arraySize - 1);
        entry.FindPropertyRelative("poolKey").stringValue = "InvaderCarrier";
        entry.FindPropertyRelative("prefab").objectReferenceValue = carrierGo;
        entry.FindPropertyRelative("initialSize").intValue = 1;

        so.ApplyModifiedProperties();
        Debug.Log("[Setup] InvaderCarrier boss pool configured.");
    }

    // ─── Parallax Background ───────────────────────────────────────────────────

    private static void SetupParallaxBackground()
    {
        var existing = GameObject.Find("ParallaxBackground");
        if (existing != null) Object.DestroyImmediate(existing);

        var starSprite = AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/Starfield_Tile.png");
        if (starSprite == null)
        {
            Debug.LogError("Starfield_Tile.png not found!");
            return;
        }

        var root = new GameObject("ParallaxBackground");
        root.transform.position = Vector3.zero;
        root.AddComponent<ParallaxController>();

        BuildParallaxLayer(root.transform, "Layer_FarNebula",  starSprite, new Color(0.4f, 0.3f, 0.7f, 0.45f), 1.0f, -25);
        BuildParallaxLayer(root.transform, "Layer_MidStars",   starSprite, new Color(0.7f, 0.85f, 1f, 0.75f),  2.5f, -20);
        BuildParallaxLayer(root.transform, "Layer_NearStars",  starSprite, new Color(1f, 1f, 1f, 1f),           5.0f, -15);

        Debug.Log("[Setup] Parallax background created with 3 scrolling star layers.");
    }

    private static void BuildParallaxLayer(Transform parent, string layerName,
        Sprite sprite, Color tint, float speed, int sortingOrder)
    {
        float tileHeight = 20f;

        var layerGo = new GameObject(layerName);
        layerGo.transform.SetParent(parent, false);
        layerGo.transform.localPosition = Vector3.zero;

        var tileA = CreateBackgroundTile($"{layerName}_A", layerGo.transform, sprite, tint, 0f, sortingOrder, tileHeight);
        var tileB = CreateBackgroundTile($"{layerName}_B", layerGo.transform, sprite, tint, tileHeight, sortingOrder, tileHeight);

        var pl = layerGo.AddComponent<ParallaxLayer>();
        pl.Initialize(tileA, tileB, tileHeight, speed);

        var plSO = new SerializedObject(pl);
        plSO.FindProperty("tileA").objectReferenceValue = tileA;
        plSO.FindProperty("tileB").objectReferenceValue = tileB;
        plSO.FindProperty("tileHeight").floatValue = tileHeight;
        plSO.FindProperty("scrollSpeed").floatValue = speed;
        plSO.ApplyModifiedProperties();
    }

    private static Transform CreateBackgroundTile(string name, Transform parent,
        Sprite sprite, Color tint, float offsetY, int sortingOrder, float height)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = new Vector3(0f, offsetY, 0f);

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = tint;
        sr.sortingOrder = sortingOrder;
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.size = new Vector2(24f, height);

        return go.transform;
    }

    // ─── HUD Wave Text ─────────────────────────────────────────────────────────

    private static void SetupWaveHUD()
    {
        var hud = Object.FindAnyObjectByType<GameplayHUD>();
        if (hud == null)
        {
            var hudCanvas = GameObject.Find("HUDCanvas");
            if (hudCanvas != null) hud = hudCanvas.GetComponent<GameplayHUD>();
        }

        if (hud == null) return;
        var canvas = hud.GetComponentInParent<Canvas>();
        if (canvas == null) canvas = hud.GetComponentInChildren<Canvas>();
        if (canvas == null) return;

        var oldWave = canvas.transform.Find("WaveText");
        if (oldWave != null) Object.DestroyImmediate(oldWave.gameObject);

        var oldAnno = canvas.transform.Find("WaveAnnouncement");
        if (oldAnno != null) Object.DestroyImmediate(oldAnno.gameObject);

        var hudSO = new SerializedObject(hud);

        var waveTextGo = CreateHUDText("WaveText", canvas.transform, "WAVE 1",
            new Vector2(-30f, -80f), new Vector2(1f, 1f), new Vector2(1f, 1f), new Vector2(1f, 1f),
            TMPro.TextAlignmentOptions.TopRight, 36, new Color(0f, 0.9f, 1f));
        hudSO.FindProperty("waveText").objectReferenceValue =
            waveTextGo.GetComponent<TMPro.TextMeshProUGUI>();

        var announcementGo = CreateHUDText("WaveAnnouncement", canvas.transform, "WAVE 1",
            new Vector2(0f, 40f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            TMPro.TextAlignmentOptions.Center, 56, new Color(0f, 0.9f, 1f));
        var annoTMP = announcementGo.GetComponent<TMPro.TextMeshProUGUI>();
        annoTMP.fontStyle = TMPro.FontStyles.Bold;
        announcementGo.SetActive(false);
        hudSO.FindProperty("waveAnnouncementText").objectReferenceValue = annoTMP;

        hudSO.ApplyModifiedProperties();
    }

    // ─── Weapon & Bomb HUD ─────────────────────────────────────────────────────

    private static void SetupWeaponAndBombHUD()
    {
        var hud = Object.FindAnyObjectByType<GameplayHUD>();
        if (hud == null)
        {
            var hudCanvas = GameObject.Find("HUDCanvas");
            if (hudCanvas != null) hud = hudCanvas.GetComponent<GameplayHUD>();
        }

        if (hud == null) return;
        var canvas = hud.GetComponentInParent<Canvas>();
        if (canvas == null) canvas = hud.GetComponentInChildren<Canvas>();
        if (canvas == null) return;

        // Clean existing
        var oldWep = canvas.transform.Find("WeaponLevelText");
        if (oldWep != null) Object.DestroyImmediate(oldWep.gameObject);

        var oldBomb = canvas.transform.Find("BombText");
        if (oldBomb != null) Object.DestroyImmediate(oldBomb.gameObject);

        var oldBtn = canvas.transform.Find("BombButton");
        if (oldBtn != null) Object.DestroyImmediate(oldBtn.gameObject);

        var hudSO = new SerializedObject(hud);

        // 1. Weapon Level Text (below HullText on the left)
        var wepGo = CreateHUDText("WeaponLevelText", canvas.transform, "WEAPON: <color=#ffaa00>LV.1</color> |",
            new Vector2(30f, -220f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            TMPro.TextAlignmentOptions.Left, 28, Color.white);
        hudSO.FindProperty("weaponLevelText").objectReferenceValue = wepGo.GetComponent<TextMeshProUGUI>();

        // 2. Bomb Counter Text (below WeaponLevelText)
        var bombGo = CreateHUDText("BombText", canvas.transform, "BOMBS: <color=#d444ff>2</color>",
            new Vector2(30f, -270f), new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(0f, 1f),
            TMPro.TextAlignmentOptions.Left, 28, Color.white);
        hudSO.FindProperty("bombText").objectReferenceValue = bombGo.GetComponent<TextMeshProUGUI>();

        // 3. Bomb Button (bottom-right of screen for touch/click)
        var btnGo = new GameObject("BombButton");
        btnGo.transform.SetParent(canvas.transform, false);

        var btnImage = btnGo.AddComponent<Image>();
        btnImage.color = new Color(0.4f, 0.1f, 0.5f, 0.85f); // Deep purple

        var btn = btnGo.AddComponent<Button>();
        var colors = btn.colors;
        colors.highlightedColor = new Color(0.6f, 0.2f, 0.75f, 1f);
        colors.pressedColor = new Color(0.8f, 0.3f, 1f, 1f);
        btn.colors = colors;

        var btnRect = btnGo.GetComponent<RectTransform>();
        btnRect.anchorMin = new Vector2(1f, 0f);
        btnRect.anchorMax = new Vector2(1f, 0f);
        btnRect.pivot = new Vector2(1f, 0f);
        btnRect.anchoredPosition = new Vector2(-40f, 40f);
        btnRect.sizeDelta = new Vector2(150f, 65f);

        // Button Label
        var labelGo = new GameObject("Label");
        labelGo.transform.SetParent(btnGo.transform, false);
        var labelTMP = labelGo.AddComponent<TextMeshProUGUI>();
        labelTMP.text = "BOMB";
        labelTMP.fontSize = 28;
        labelTMP.fontStyle = FontStyles.Bold;
        labelTMP.color = Color.white;
        labelTMP.alignment = TextAlignmentOptions.Center;

        var font = AssetDatabase.LoadAssetAtPath<TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        if (font != null) labelTMP.font = font;

        var labelRect = labelGo.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.sizeDelta = Vector2.zero;

        hudSO.FindProperty("bombButton").objectReferenceValue = btn;

        hudSO.ApplyModifiedProperties();
        Debug.Log("[Setup] Weapon & Bomb HUD elements hooked up.");
    }

    // ─── Boss Health Bar ───────────────────────────────────────────────────────

    private static void SetupBossHealthBar()
    {
        var hud = Object.FindAnyObjectByType<GameplayHUD>();
        if (hud == null) return;
        var canvas = hud.GetComponentInParent<Canvas>();
        if (canvas == null) canvas = hud.GetComponentInChildren<Canvas>();
        if (canvas == null) return;

        // Remove old if re-running setup
        var oldBar = canvas.transform.Find("BossHealthBarPanel");
        if (oldBar != null) Object.DestroyImmediate(oldBar.gameObject);

        // Outer panel
        var panelGo = new GameObject("BossHealthBarPanel");
        panelGo.transform.SetParent(canvas.transform, false);

        var panelRect = panelGo.AddComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.15f, 1f);
        panelRect.anchorMax = new Vector2(0.85f, 1f);
        panelRect.pivot = new Vector2(0.5f, 1f);
        panelRect.anchoredPosition = new Vector2(0f, 120f); // Hidden above screen
        panelRect.sizeDelta = new Vector2(0f, 70f);

        var panelBG = panelGo.AddComponent<Image>();
        panelBG.color = new Color(0.05f, 0.05f, 0.15f, 0.85f);

        // Boss name text
        var nameGo = new GameObject("BossNameText");
        nameGo.transform.SetParent(panelGo.transform, false);
        var nameTMP = nameGo.AddComponent<TextMeshProUGUI>();
        nameTMP.text = "INVADER CARRIER";
        nameTMP.fontSize = 20;
        nameTMP.fontStyle = FontStyles.Bold;
        nameTMP.color = new Color(0.2f, 0.8f, 1f);
        nameTMP.alignment = TextAlignmentOptions.Left;
        var nameRect = nameGo.GetComponent<RectTransform>();
        nameRect.anchorMin = new Vector2(0f, 0.5f);
        nameRect.anchorMax = new Vector2(0.4f, 1f);
        nameRect.offsetMin = new Vector2(10f, 0f);
        nameRect.offsetMax = new Vector2(0f, 0f);

        // Phase text
        var phaseGo = new GameObject("PhaseText");
        phaseGo.transform.SetParent(panelGo.transform, false);
        var phaseTMP = phaseGo.AddComponent<TextMeshProUGUI>();
        phaseTMP.text = "PHASE 1";
        phaseTMP.fontSize = 16;
        phaseTMP.color = new Color(0.2f, 0.8f, 1f);
        phaseTMP.alignment = TextAlignmentOptions.Right;
        var phaseRect = phaseGo.GetComponent<RectTransform>();
        phaseRect.anchorMin = new Vector2(0.6f, 0.5f);
        phaseRect.anchorMax = new Vector2(1f, 1f);
        phaseRect.offsetMin = new Vector2(0f, 0f);
        phaseRect.offsetMax = new Vector2(-10f, 0f);

        // Health bar background
        var barBGGo = new GameObject("HealthBarBG");
        barBGGo.transform.SetParent(panelGo.transform, false);
        var barBGImg = barBGGo.AddComponent<Image>();
        barBGImg.color = new Color(0.1f, 0.1f, 0.2f);
        var barBGRect = barBGGo.GetComponent<RectTransform>();
        barBGRect.anchorMin = new Vector2(0f, 0f);
        barBGRect.anchorMax = new Vector2(1f, 0.5f);
        barBGRect.offsetMin = new Vector2(8f, 8f);
        barBGRect.offsetMax = new Vector2(-8f, -2f);

        // Health fill
        var barFillGo = new GameObject("HealthBarFill");
        barFillGo.transform.SetParent(barBGGo.transform, false);
        var fillImg = barFillGo.AddComponent<Image>();
        fillImg.color = new Color(0.2f, 0.8f, 1f);
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillAmount = 1f;
        var fillRect = barFillGo.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;

        // Wire BossHealthBar component
        var bhb = panelGo.AddComponent<BossHealthBar>();
        var bhbSO = new SerializedObject(bhb);
        bhbSO.FindProperty("panelRect").objectReferenceValue = panelRect;
        bhbSO.FindProperty("healthFillImage").objectReferenceValue = fillImg;
        bhbSO.FindProperty("bossNameText").objectReferenceValue = nameTMP;
        bhbSO.FindProperty("phaseText").objectReferenceValue = phaseTMP;
        bhbSO.FindProperty("hiddenAnchoredPos").vector2Value = new Vector2(0f, 120f);
        bhbSO.FindProperty("visibleAnchoredPos").vector2Value = new Vector2(0f, -20f);
        bhbSO.ApplyModifiedProperties();

        Debug.Log("[Setup] BossHealthBar panel created and wired.");
    }

    // ─── Mission Complete UI ───────────────────────────────────────────────────

    private static void SetupMissionCompleteUI()
    {
        var hud = Object.FindAnyObjectByType<GameplayHUD>();
        if (hud == null) return;
        var canvas = hud.GetComponentInParent<Canvas>();
        if (canvas == null) canvas = hud.GetComponentInChildren<Canvas>();
        if (canvas == null) return;

        var old = canvas.transform.Find("MissionCompletePanel");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        // Full-screen dimmed overlay panel
        var panelGo = new GameObject("MissionCompletePanel");
        panelGo.transform.SetParent(canvas.transform, false);

        var cg = panelGo.AddComponent<CanvasGroup>();
        cg.alpha = 0f;
        cg.interactable = false;
        cg.blocksRaycasts = false;

        var panelRect = panelGo.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        var bg = panelGo.AddComponent<Image>();
        bg.color = new Color(0f, 0.02f, 0.08f, 0.92f);

        // Title
        var titleGo = CreateHUDText("TitleText", panelGo.transform, "PLANET 1 CLEARED!",
            new Vector2(0f, 100f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            TextAlignmentOptions.Center, 60, new Color(0f, 0.9f, 1f));
        var titleTMP = titleGo.GetComponent<TextMeshProUGUI>();
        titleTMP.fontStyle = FontStyles.Bold;

        // Score
        var scoreGo = CreateHUDText("ScoreText", panelGo.transform, "SCORE: 0",
            new Vector2(0f, 20f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            TextAlignmentOptions.Center, 36, Color.white);

        // Stars
        var starsGo = CreateHUDText("StarsText", panelGo.transform, "★★★",
            new Vector2(0f, -50f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
            TextAlignmentOptions.Center, 72, new Color(1f, 0.85f, 0.1f));

        // Warp button
        var warpBtnGo = new GameObject("WarpButton");
        warpBtnGo.transform.SetParent(panelGo.transform, false);
        var warpImg = warpBtnGo.AddComponent<Image>();
        warpImg.color = new Color(0f, 0.45f, 0.7f, 0.9f);
        var warpBtn = warpBtnGo.AddComponent<Button>();
        var warpRect = warpBtnGo.GetComponent<RectTransform>();
        warpRect.anchorMin = new Vector2(0.5f, 0.5f);
        warpRect.anchorMax = new Vector2(0.5f, 0.5f);
        warpRect.pivot = new Vector2(0.5f, 0.5f);
        warpRect.anchoredPosition = new Vector2(-90f, -150f);
        warpRect.sizeDelta = new Vector2(200f, 65f);
        var warpLabelGo = new GameObject("Label");
        warpLabelGo.transform.SetParent(warpBtnGo.transform, false);
        var warpTMP = warpLabelGo.AddComponent<TextMeshProUGUI>();
        warpTMP.text = "WARP ►";
        warpTMP.fontSize = 28; warpTMP.fontStyle = FontStyles.Bold; warpTMP.color = Color.white;
        warpTMP.alignment = TextAlignmentOptions.Center;
        var warpLabelRect = warpLabelGo.GetComponent<RectTransform>();
        warpLabelRect.anchorMin = Vector2.zero; warpLabelRect.anchorMax = Vector2.one; warpLabelRect.sizeDelta = Vector2.zero;

        // Retry button
        var retryBtnGo = new GameObject("RetryButton");
        retryBtnGo.transform.SetParent(panelGo.transform, false);
        var retryImg = retryBtnGo.AddComponent<Image>();
        retryImg.color = new Color(0.3f, 0.08f, 0.05f, 0.9f);
        var retryBtn = retryBtnGo.AddComponent<Button>();
        var retryRect = retryBtnGo.GetComponent<RectTransform>();
        retryRect.anchorMin = new Vector2(0.5f, 0.5f);
        retryRect.anchorMax = new Vector2(0.5f, 0.5f);
        retryRect.pivot = new Vector2(0.5f, 0.5f);
        retryRect.anchoredPosition = new Vector2(90f, -150f);
        retryRect.sizeDelta = new Vector2(200f, 65f);
        var retryLabelGo = new GameObject("Label");
        retryLabelGo.transform.SetParent(retryBtnGo.transform, false);
        var retryTMP = retryLabelGo.AddComponent<TextMeshProUGUI>();
        retryTMP.text = "RETRY";
        retryTMP.fontSize = 28; retryTMP.fontStyle = FontStyles.Bold; retryTMP.color = Color.white;
        retryTMP.alignment = TextAlignmentOptions.Center;
        var retryLabelRect = retryLabelGo.GetComponent<RectTransform>();
        retryLabelRect.anchorMin = Vector2.zero; retryLabelRect.anchorMax = Vector2.one; retryLabelRect.sizeDelta = Vector2.zero;

        // Wire MissionCompleteUI component
        var mcu = panelGo.AddComponent<MissionCompleteUI>();
        var mcuSO = new SerializedObject(mcu);
        mcuSO.FindProperty("canvasGroup").objectReferenceValue = cg;
        mcuSO.FindProperty("panelRect").objectReferenceValue = panelRect;
        mcuSO.FindProperty("titleText").objectReferenceValue = titleTMP;
        mcuSO.FindProperty("scoreText").objectReferenceValue = scoreGo.GetComponent<TextMeshProUGUI>();
        mcuSO.FindProperty("starsText").objectReferenceValue = starsGo.GetComponent<TextMeshProUGUI>();
        mcuSO.FindProperty("warpButton").objectReferenceValue = warpBtn;
        mcuSO.FindProperty("retryButton").objectReferenceValue = retryBtn;
        mcuSO.ApplyModifiedProperties();

        Debug.Log("[Setup] MissionCompleteUI panel created and wired.");
    }

    private static GameObject CreateHUDText(string name, Transform parent, string defaultText,
        Vector2 anchoredPos, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot,
        TMPro.TextAlignmentOptions alignment, int fontSize, Color color)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);

        var tmp = go.AddComponent<TMPro.TextMeshProUGUI>();
        tmp.text = defaultText;
        tmp.fontSize = fontSize;
        tmp.color = color;
        tmp.alignment = alignment;

        var font = AssetDatabase.LoadAssetAtPath<TMPro.TMP_FontAsset>("Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF.asset");
        if (font != null) tmp.font = font;

        var rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPos;
        rect.sizeDelta = new Vector2(500f, 60f);

        return go;
    }
}
