using UnityEngine;
using Starfall.Levels;

namespace Starfall.Enemies
{
    /// <summary>
    /// Recolors grunt enemies dynamically per sector/level group via sprite tint:
    /// - Levels 1–3 (Sector 1 Earth Orbit): Default Red / Steel Grey accents
    /// - Levels 4–6 (Sector 2 Asteroids): Shift toward Deep Purple / Violet accents
    /// - Levels 7–9 (Sector 3 Nebula/War): Shift toward Fiery Black / Amber-Orange accents
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class SectorEnemyTinter : MonoBehaviour
    {
        private SpriteRenderer sr;
        private EnemyBase enemy;

        private void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
            enemy = GetComponent<EnemyBase>();
        }

        private void OnEnable()
        {
            ApplySectorTint();
        }

        public void ApplySectorTint()
        {
            if (sr == null) sr = GetComponent<SpriteRenderer>();

            int level = 1;
            if (LevelManager.Instance != null)
            {
                level = LevelManager.Instance.CurrentLevelNumber;
            }

            Color tint = GetSectorTint(level);
            if (sr != null)
            {
                sr.color = tint;
            }
        }

        public static Color GetSectorTint(int levelNumber)
        {
            return levelNumber switch
            {
                // Sector 1: Earth Orbit (Levels 1–3) - Base Red/Grey
                1 or 2 or 3 => new Color(1f, 0.92f, 0.92f, 1f),

                // Sector 2: Asteroid Belt (Levels 4–6) - Deep Purple / Violet
                4 or 5 or 6 => new Color(0.85f, 0.55f, 1f, 1f),

                // Sector 3: Deep Nebula (Levels 7–9) - Fiery Amber / Orange
                _ => new Color(1f, 0.65f, 0.3f, 1f)
            };
        }
    }
}
