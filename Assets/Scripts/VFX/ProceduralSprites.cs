using UnityEngine;

namespace Starfall.VFX
{
    /// <summary>
    /// Runtime-generated soft shapes (glow, ring, bar) used by gameplay VFX.
    /// Generated once and cached, so effects need no extra art assets or custom shaders
    /// (important for WebGL / mobile builds where shader stripping can bite).
    /// </summary>
    public static class ProceduralSprites
    {
        private static Sprite softCircle;
        private static Sprite ring;
        private static Sprite bar;
        private static Sprite flameJet;

        /// <summary>Radial gradient: white centre fading to transparent edge. 1 world unit wide.</summary>
        public static Sprite SoftCircle => softCircle != null ? softCircle : (softCircle = BuildCircle(64, 0f, 1f, 2.2f));

        /// <summary>Thin soft ring. 1 world unit wide.</summary>
        public static Sprite Ring => ring != null ? ring : (ring = BuildCircle(128, 0.78f, 0.11f, 1f));

        /// <summary>Soft-edged horizontal bar, 1x1 world unit, for telegraph lines and beams.</summary>
        public static Sprite Bar => bar != null ? bar : (bar = BuildBar());

        /// <summary>Tapered supersonic plasma flame plume. 1 world unit wide, 2 units long, pivot at top (nozzle).</summary>
        public static Sprite FlameJet => flameJet != null ? flameJet : (flameJet = BuildFlameJet());

        private static Sprite whitePixel;
        /// <summary>Single 1x1 solid white pixel sprite for UI bars and trackers.</summary>
        public static Sprite WhitePixel => whitePixel != null ? whitePixel : (whitePixel = BuildWhitePixel());

        private static Sprite BuildCircle(int size, float ringCenter, float ringWidth, float falloff)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = ringCenter > 0f ? "Starfall_ProcRing" : "Starfall_ProcGlow"
            };

            var pixels = new Color32[size * size];
            float half = (size - 1) * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x - half) / half;
                    float dy = (y - half) / half;
                    float d = Mathf.Sqrt(dx * dx + dy * dy);
                    float a;
                    if (ringCenter <= 0f)
                    {
                        a = Mathf.Pow(Mathf.Clamp01(1f - d), falloff);
                    }
                    else
                    {
                        float band = Mathf.Abs(d - ringCenter) / ringWidth;
                        a = Mathf.Clamp01(1f - band * band);
                    }
                    byte alphaByte = (byte)(Mathf.Clamp01(a) * 255f);
                    pixels[y * size + x] = new Color32(alphaByte, alphaByte, alphaByte, alphaByte);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        private static Sprite BuildBar()
        {
            const int w = 32, h = 32;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "Starfall_ProcBar"
            };
            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = Mathf.Abs((y - (h - 1) * 0.5f) / ((h - 1) * 0.5f));
                byte a = (byte)(Mathf.Clamp01(1f - v * v) * 255f);
                for (int x = 0; x < w; x++) pixels[y * w + x] = new Color32(a, a, a, a);
            }
            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            // Pixels-per-unit chosen so the sprite is 1 x 1 world units.
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0f, 0.5f), h, 0, SpriteMeshType.FullRect);
        }

        private static Sprite BuildFlameJet()
        {
            const int w = 64;
            const int h = 128;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
                name = "Starfall_ProcFlameJet"
            };

            var pixels = new Color32[w * h];
            float halfW = (w - 1) * 0.5f;

            for (int y = 0; y < h; y++)
            {
                // v from 0.0 (flame tip at bottom) to 1.0 (nozzle root at top)
                float v = (float)y / (h - 1);

                // Supersonic plasma jet contour: broad at nozzle, tapering gracefully to sharp needle tip
                float profile = Mathf.Pow(Mathf.Clamp01(v), 0.62f);
                float radius = Mathf.Max(0.04f, profile * halfW);

                for (int x = 0; x < w; x++)
                {
                    float distFromCenter = Mathf.Abs(x - halfW);
                    if (distFromCenter > radius || v <= 0.005f)
                    {
                        pixels[y * w + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    float normDist = distFromCenter / radius; // 0 center, 1 edge

                    // Outer plasma envelope with smooth quadratic falloff
                    float outerAlpha = Mathf.Clamp01(1f - normDist * normDist);
                    // Tip fade so bottom doesn't cut off abruptly
                    float tipFade = Mathf.SmoothStep(0f, 0.16f, v);
                    float finalAlpha = outerAlpha * tipFade;

                    // Incandescent core (white-hot column along the center axis)
                    float coreFactor = Mathf.Clamp01(1f - normDist * 2.2f) * Mathf.SmoothStep(0.06f, 0.35f, v);
                    byte coreB = (byte)(255 - Mathf.Clamp01(coreFactor) * 35);
                    byte a = (byte)(Mathf.Clamp01(finalAlpha) * 255f);
                    float aNorm = a / 255f;

                    pixels[y * w + x] = new Color32((byte)(255 * aNorm), (byte)(255 * aNorm), (byte)(coreB * aNorm), a);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);

            // Pivot at (0.5, 1.0) so local origin connects directly to the engine nozzle!
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 1.0f), 64f, 0, SpriteMeshType.FullRect);
        }

        private static Sprite BuildWhitePixel()
        {
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false)
            {
                name = "Starfall_WhitePixel"
            };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);
        }
    }
}
