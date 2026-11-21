using UnityEngine;

namespace Starfall.Core
{
    /// <summary>
    /// Single source of truth for the playable area, derived from the main camera so the
    /// game works the same in portrait (mobile) and landscape (web / Windows).
    /// Enemy formations, firing checks and player clamping all read from here.
    /// </summary>
    public static class PlayfieldBounds
    {
        /// <summary>Widest the combat lane is allowed to get on wide landscape screens.</summary>
        public const float MaxPlayHalfWidth = 14.0f;
        /// <summary>Space reserved at the top of the screen for the HUD.</summary>
        public const float HudTopMargin = 0.95f;

        private static int cachedFrame = -1;
        private static float left, right, top, bottom;

        private static void Refresh()
        {
            if (cachedFrame == Time.frameCount) return;
            cachedFrame = Time.frameCount;

            var cam = Camera.main;
            if (cam == null || !cam.orthographic)
            {
                left = -10f; right = 10f; top = 5f; bottom = -5f;
                return;
            }

            float halfH = cam.orthographicSize;
            float halfW = halfH * cam.aspect;
            Vector3 c = cam.transform.position;
            left = c.x - halfW;
            right = c.x + halfW;
            top = c.y + halfH;
            bottom = c.y - halfH;
        }

        public static float CameraLeft { get { Refresh(); return left; } }
        public static float CameraRight { get { Refresh(); return right; } }
        public static float Top { get { Refresh(); return top; } }
        public static float Bottom { get { Refresh(); return bottom; } }

        /// <summary>Half width of the combat lane (centered on x = 0).</summary>
        public static float PlayHalfWidth
        {
            get
            {
                Refresh();
                float camHalf = (right - left) * 0.5f;
                // Keep comfortable border margin of 0.75f, clamped between 2.5f and MaxPlayHalfWidth
                return Mathf.Clamp(camHalf - 0.75f, 2.5f, MaxPlayHalfWidth);
            }
        }

        public static float PlayLeft => -PlayHalfWidth;
        public static float PlayRight => PlayHalfWidth;

        /// <summary>Highest y an enemy slot may occupy (just under the HUD).</summary>
        public static float FormationTop => Top - HudTopMargin;

        /// <summary>Lowest y an enemy slot may occupy; everything below belongs to the player.</summary>
        public static float FormationBottom => Mathf.Max(Top - 4.1f, Bottom + 5.6f);

        /// <summary>Y just above the visible screen where enemies are spawned.</summary>
        public static float SpawnY => Top + 1.2f;

        /// <summary>True when a point is on screen, below the HUD band and inside the combat lane.</summary>
        public static bool IsInCombatZone(Vector3 p, float margin = 0.1f)
        {
            float hw = PlayHalfWidth + 0.3f;
            return p.y <= FormationTop + 0.35f && p.y >= Bottom + margin && p.x >= -hw && p.x <= hw;
        }

        public static bool IsOnScreen(Vector3 p, float margin = 0f)
        {
            Refresh();
            return p.x >= left - margin && p.x <= right + margin && p.y >= bottom - margin && p.y <= top + margin;
        }
    }
}
