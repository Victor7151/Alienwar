using UnityEngine;
using UnityEngine.SceneManagement;

namespace Starfall.VFX
{
    /// <summary>
    /// Creates and manages the 3-layer parallax scrolling background system.
    /// Dynamically swaps sector scenic backgrounds and color grading based on the active sector/level:
    /// - Sector 1 (Levels 1–3): Earth Orbit (Blue Nebula)
    /// - Sector 2 (Levels 4–6): Deep Space Nebula (Purple)
    /// - Sector 3 (Levels 7–9): Hostile War-Torn Sector (Black/Orange)
    /// </summary>
    public class ParallaxController : MonoBehaviour
    {
        [System.Serializable]
        public class LayerConfig
        {
            public string name = "Layer";
            public float scrollSpeed = 2f;
            public Color tint = Color.white;
            [Range(0f, 1f)]
            public float alpha = 1f;
        }

        [Header("Legacy Config")]
        [SerializeField] private LayerConfig[] layerConfigs;
        [SerializeField] private float layerHeight = 20f;

        [Header("Sector Parallax Background Sprites")]
        [SerializeField] private Sprite sector1EarthOrbitSprite;
        [SerializeField] private Sprite sector2AsteroidBeltSprite;
        [SerializeField] private Sprite sector3DeepNebulaSprite;
        [SerializeField] private Sprite sector4WarTornSprite;
        [SerializeField] private Sprite sector5AlienCoreSprite;
        [SerializeField] private Sprite starfieldSprite;

        [Header("Parallax Settings")]
        [SerializeField] private float worldWidth = 20f;
        [SerializeField] private float tileHeight = 11.25f;
        [SerializeField] private ParallaxDirection defaultDirection = ParallaxDirection.TopToBottom;

        public ParallaxDirection DefaultDirection
        {
            get => defaultDirection;
            set => defaultDirection = value;
        }

        private ParallaxLayer[] layers;
        private SpriteRenderer farTileA_SR;
        private SpriteRenderer farTileB_SR;
        private SpriteRenderer midTileA_SR;
        private SpriteRenderer midTileB_SR;
        private SpriteRenderer nearTileA_SR;
        private SpriteRenderer nearTileB_SR;

        private int currentSector = -1;

        private void Awake()
        {
            CacheLayersAndRenderers();
            ApplySectorForCurrentLevel();
        }

        private void Start()
        {
            CacheLayersAndRenderers();
        }

        public void CacheLayersAndRenderers()
        {
            defaultDirection = ParallaxDirection.TopToBottom;
            layers = GetComponentsInChildren<ParallaxLayer>();
            foreach (var l in layers)
            {
                if (l != null)
                {
                    l.Direction = defaultDirection;
                    l.TileWidth = worldWidth;
                    l.TileHeight = tileHeight;
                    if (l.name.Contains("Far")) l.ScrollSpeed = 0.65f;
                    else if (l.name.Contains("Mid")) l.ScrollSpeed = 1.85f;
                    else if (l.name.Contains("Near")) l.ScrollSpeed = 4.2f;
                    l.ResetTilePositions();
                }

                if (l.name.Contains("Far"))
                {
                    if (l.transform.childCount >= 2)
                    {
                        farTileA_SR = l.transform.GetChild(0).GetComponent<SpriteRenderer>();
                        farTileB_SR = l.transform.GetChild(1).GetComponent<SpriteRenderer>();
                    }
                }
                else if (l.name.Contains("Mid"))
                {
                    if (l.transform.childCount >= 2)
                    {
                        midTileA_SR = l.transform.GetChild(0).GetComponent<SpriteRenderer>();
                        midTileB_SR = l.transform.GetChild(1).GetComponent<SpriteRenderer>();
                    }
                }
                else if (l.name.Contains("Near"))
                {
                    if (l.transform.childCount >= 2)
                    {
                        nearTileA_SR = l.transform.GetChild(0).GetComponent<SpriteRenderer>();
                        nearTileB_SR = l.transform.GetChild(1).GetComponent<SpriteRenderer>();
                    }
                }
            }
        }

        public void ApplySectorForCurrentLevel()
        {
            int sector = GetActiveSectorNumber();
            ApplySector(sector);
        }

        public int GetActiveSectorNumber()
        {
            string sceneName = SceneManager.GetActiveScene().name;
            int levelNum = 1;
            if (Starfall.Levels.LevelManager.Instance != null)
            {
                levelNum = Starfall.Levels.LevelManager.Instance.CurrentLevelNumber;
            }
            else
            {
                if (sceneName.Contains("Level4") || sceneName.Contains("WarTorn") || sceneName.Contains("Wreckage")) levelNum = 4;
                else if (sceneName.Contains("Level3") || sceneName.Contains("Nebula")) levelNum = 3;
                else if (sceneName.Contains("Level2") || sceneName.Contains("Asteroid")) levelNum = 2;
                else levelNum = 1;
            }

            if (sceneName.Contains("Hive") || sceneName.Contains("AlienCore") || levelNum >= 5) return 5;
            if (sceneName.Contains("Level4") || sceneName.Contains("WarTorn") || sceneName.Contains("Wreckage") || levelNum == 4) return 4;
            if (sceneName.Contains("Level3") || sceneName.Contains("Nebula") || levelNum == 3) return 3;
            if (sceneName.Contains("Level2") || sceneName.Contains("Asteroid") || levelNum == 2) return 2;
            return 1;
        }

        private void EnsureSpritesLoaded()
        {
            // Sector 1: Earth Orbit
            if (sector1EarthOrbitSprite == null)
            {
                sector1EarthOrbitSprite = Resources.Load<Sprite>("Backgrounds/Orbital Eclipse & Dark Atmospheric Horizon (Orbit Sector)");
#if UNITY_EDITOR
                if (sector1EarthOrbitSprite == null)
                    sector1EarthOrbitSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Backgrounds/Orbital Eclipse & Dark Atmospheric Horizon (Orbit Sector).png");
#endif
            }

            // Sector 2: Asteroid Belt
            if (sector2AsteroidBeltSprite == null)
            {
                sector2AsteroidBeltSprite = Resources.Load<Sprite>("Backgrounds/Abyssal Asteroid Field in Deep Shadow (Asteroid Belt _ Sector 1)");
#if UNITY_EDITOR
                if (sector2AsteroidBeltSprite == null)
                    sector2AsteroidBeltSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Backgrounds/Abyssal Asteroid Field in Deep Shadow (Asteroid Belt _ Sector 1).png");
#endif
            }

            // Sector 3: Deep Nebula
            if (sector3DeepNebulaSprite == null)
            {
                sector3DeepNebulaSprite = Resources.Load<Sprite>("Backgrounds/Dark Obsidian Deep Nebula (Mystic _ Sector 2)");
#if UNITY_EDITOR
                if (sector3DeepNebulaSprite == null)
                    sector3DeepNebulaSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Backgrounds/Dark Obsidian Deep Nebula (Mystic _ Sector 2).png");
#endif
            }

            // Sector 4: War-Torn Wreckage
            if (sector4WarTornSprite == null)
            {
                sector4WarTornSprite = Resources.Load<Sprite>("Backgrounds/War-Torn Wreckage & Dark Ash Sector (Hostile _ Sector 3)");
#if UNITY_EDITOR
                if (sector4WarTornSprite == null)
                    sector4WarTornSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Backgrounds/War-Torn Wreckage & Dark Ash Sector (Hostile _ Sector 3).png");
#endif
            }

            // Sector 5: Alien Core Hive
            if (sector5AlienCoreSprite == null)
            {
                sector5AlienCoreSprite = Resources.Load<Sprite>("Backgrounds/Alien Bio-Mechanical Abyssal Hive (Alien Core Sector)");
#if UNITY_EDITOR
                if (sector5AlienCoreSprite == null)
                    sector5AlienCoreSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Resources/Backgrounds/Alien Bio-Mechanical Abyssal Hive (Alien Core Sector).png");
#endif
            }

            if (starfieldSprite == null)
            {
                starfieldSprite = Resources.Load<Sprite>("Backgrounds/Starfield");
#if UNITY_EDITOR
                if (starfieldSprite == null)
                    starfieldSprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/Art/Sprites/Backgrounds/Stars-Big_1.png");
#endif
            }
        }

        public void ApplySector(int sector)
        {
            currentSector = sector;
            CacheLayersAndRenderers();
            EnsureSpritesLoaded();

            Sprite targetFarSprite = sector switch
            {
                1 => sector1EarthOrbitSprite,
                2 => sector2AsteroidBeltSprite != null ? sector2AsteroidBeltSprite : sector1EarthOrbitSprite,
                3 => sector3DeepNebulaSprite != null ? sector3DeepNebulaSprite : sector1EarthOrbitSprite,
                4 => sector4WarTornSprite != null ? sector4WarTornSprite : sector1EarthOrbitSprite,
                5 => sector5AlienCoreSprite != null ? sector5AlienCoreSprite : (sector4WarTornSprite != null ? sector4WarTornSprite : sector1EarthOrbitSprite),
                _ => sector1EarthOrbitSprite
            };

            Color farTint = Color.white;
            Color midTint = sector switch
            {
                2 => new Color(0.85f, 0.9f, 1f, 0.8f),    // Cold asteroid twilight
                3 => new Color(0.9f, 0.72f, 1f, 0.85f),  // Mystical lavender-purple
                4 => new Color(1f, 0.65f, 0.35f, 0.85f), // Fiery amber-orange
                5 => new Color(0.7f, 1f, 0.8f, 0.85f),   // Bio-luminescent alien hue
                _ => new Color(0.85f, 0.95f, 1f, 0.8f)   // Cool cyan-blue
            };

            Color nearTint = sector switch
            {
                2 => new Color(0.92f, 0.95f, 1f, 1f),
                3 => new Color(0.98f, 0.92f, 1f, 1f),
                4 => new Color(1f, 0.88f, 0.75f, 1f),
                5 => new Color(0.85f, 1f, 0.9f, 1f),
                _ => Color.white
            };

            if (farTileA_SR != null && targetFarSprite != null)
            {
                farTileA_SR.sprite = targetFarSprite;
                farTileA_SR.color = farTint;
                UpdateTileScale(farTileA_SR.transform, targetFarSprite);
            }
            if (farTileB_SR != null && targetFarSprite != null)
            {
                farTileB_SR.sprite = targetFarSprite;
                farTileB_SR.color = farTint;
                UpdateTileScale(farTileB_SR.transform, targetFarSprite);
            }

            if (midTileA_SR != null)
            {
                if (starfieldSprite != null) midTileA_SR.sprite = starfieldSprite;
                midTileA_SR.color = midTint;
                if (midTileA_SR.sprite != null) UpdateTileScale(midTileA_SR.transform, midTileA_SR.sprite);
            }
            if (midTileB_SR != null)
            {
                if (starfieldSprite != null) midTileB_SR.sprite = starfieldSprite;
                midTileB_SR.color = midTint;
                if (midTileB_SR.sprite != null) UpdateTileScale(midTileB_SR.transform, midTileB_SR.sprite);
            }
            if (nearTileA_SR != null)
            {
                if (starfieldSprite != null) nearTileA_SR.sprite = starfieldSprite;
                nearTileA_SR.color = nearTint;
                if (nearTileA_SR.sprite != null) UpdateTileScale(nearTileA_SR.transform, nearTileA_SR.sprite);
            }
            if (nearTileB_SR != null)
            {
                if (starfieldSprite != null) nearTileB_SR.sprite = starfieldSprite;
                nearTileB_SR.color = nearTint;
                if (nearTileB_SR.sprite != null) UpdateTileScale(nearTileB_SR.transform, nearTileB_SR.sprite);
            }

            Debug.Log($"<color=cyan>[ParallaxController] Active Sector {sector} Parallax Theme: {targetFarSprite?.name ?? "Default"}</color>");
        }

        private void UpdateTileScale(Transform tileT, Sprite sprite)
        {
            if (sprite == null) return;
            float spriteW = sprite.rect.width > 0 ? sprite.rect.width / sprite.pixelsPerUnit : 1f;
            float spriteH = sprite.rect.height > 0 ? sprite.rect.height / sprite.pixelsPerUnit : 1f;
            tileT.localScale = new Vector3(worldWidth / spriteW, tileHeight / spriteH, 1f);
        }

        public void ConfigureSectorSprites(Sprite s1, Sprite s2, Sprite s3, Sprite stars)
        {
            sector1EarthOrbitSprite = s1;
            sector2AsteroidBeltSprite = s2;
            sector3DeepNebulaSprite = s3;
            starfieldSprite = stars;
        }

        public void ConfigureSectorSprites(Sprite s1, Sprite s2, Sprite s3, Sprite s4, Sprite s5, Sprite stars)
        {
            sector1EarthOrbitSprite = s1;
            sector2AsteroidBeltSprite = s2;
            sector3DeepNebulaSprite = s3;
            sector4WarTornSprite = s4;
            sector5AlienCoreSprite = s5;
            starfieldSprite = stars;
        }

        /// <summary>
        /// Legacy builder from Prototype3Setup.
        /// </summary>
        public void BuildLayers(Sprite starSprite)
        {
            if (layerConfigs == null) return;

            foreach (var cfg in layerConfigs)
            {
                var layerGo = new GameObject(cfg.name);
                layerGo.transform.SetParent(transform, false);

                var tileA = CreateTile($"{cfg.name}_A", layerGo.transform, starSprite, cfg.tint, cfg.alpha, 0f);
                var tileB = CreateTile($"{cfg.name}_B", layerGo.transform, starSprite, cfg.tint, cfg.alpha, tileHeight);

                var pl = layerGo.AddComponent<ParallaxLayer>();
                pl.Initialize(tileA, tileB, worldWidth, tileHeight, cfg.scrollSpeed, defaultDirection);
            }
        }

        private Transform CreateTile(string tileName, Transform parent, Sprite sprite,
                                     Color tint, float alpha, float offsetY)
        {
            var go = new GameObject(tileName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = new Vector3(0f, offsetY, 0f);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            var c = tint;
            c.a = alpha;
            sr.color = c;
            sr.sortingOrder = -10;
            sr.drawMode = SpriteDrawMode.Tiled;
            sr.size = new Vector2(24f, layerHeight);

            return go.transform;
        }
    }
}
