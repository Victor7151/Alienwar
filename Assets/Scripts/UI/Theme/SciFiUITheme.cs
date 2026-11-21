using UnityEngine;
using UnityEngine.UI;

namespace Starfall.UI.Theme
{
    /// <summary>
    /// SciFiUITheme – Production-grade procedural UI design system for STARFALL.
    /// Generates high-fidelity 9-sliced cybernetic frames, glass panels, beveled buttons,
    /// segmented gauge tracks, LED upgrade pips, and tactical hazard banners at runtime.
    /// Cached statically so zero runtime garbage is allocated after initialization.
    /// </summary>
    public static class SciFiUITheme
    {
        // Color Palette Tokens
        public static readonly Color CyanPrimary      = new Color(0.00f, 0.90f, 1.00f, 1.00f); // #00E5FF
        public static readonly Color CyanGlow         = new Color(0.00f, 0.90f, 1.00f, 0.35f);
        public static readonly Color GoldAccent       = new Color(1.00f, 0.82f, 0.15f, 1.00f); // #FFD126
        public static readonly Color GoldGlow         = new Color(1.00f, 0.82f, 0.15f, 0.40f);
        public static readonly Color AmberWarning     = new Color(1.00f, 0.45f, 0.10f, 1.00f); // #FF731A
        public static readonly Color CrimsonDanger    = new Color(1.00f, 0.22f, 0.25f, 1.00f); // #FF3840
        public static readonly Color EmeraldSuccess   = new Color(0.00f, 0.95f, 0.55f, 1.00f); // #00F28C
        public static readonly Color DarkGlassBg      = new Color(0.035f, 0.055f, 0.095f, 0.88f);
        public static readonly Color PanelBorderCyan  = new Color(0.00f, 0.80f, 0.95f, 0.75f);
        public static readonly Color PanelBorderGold  = new Color(1.00f, 0.78f, 0.12f, 0.80f);
        public static readonly Color MutedSlotBg      = new Color(0.06f, 0.09f, 0.15f, 0.70f);

        // Cached Sprites
        private static Sprite panelFrame;
        private static Sprite panelGlass;
        private static Sprite buttonNormal;
        private static Sprite buttonHover;
        private static Sprite buttonPressed;
        private static Sprite buttonPrimary;
        private static Sprite gaugeTrack;
        private static Sprite gaugeFill;
        private static Sprite hazardTapeYellow;
        private static Sprite hazardTapeRed;
        private static Sprite ledPipOn;
        private static Sprite ledPipOff;
        private static Sprite starFilled;
        private static Sprite starEmpty;
        private static Sprite cornerBracket;

        // ------------------------------------------------------------------
        // Sprite Accessors
        // ------------------------------------------------------------------
        public static Sprite PanelFrame => panelFrame != null ? panelFrame : (panelFrame = BuildPanelFrame());
        public static Sprite PanelGlass => panelGlass != null ? panelGlass : (panelGlass = BuildPanelGlass());
        public static Sprite ButtonNormal => buttonNormal != null ? buttonNormal : (buttonNormal = BuildButton(ButtonState.Normal));
        public static Sprite ButtonHover => buttonHover != null ? buttonHover : (buttonHover = BuildButton(ButtonState.Hover));
        public static Sprite ButtonPressed => buttonPressed != null ? buttonPressed : (buttonPressed = BuildButton(ButtonState.Pressed));
        public static Sprite ButtonPrimary => buttonPrimary != null ? buttonPrimary : (buttonPrimary = BuildButton(ButtonState.Primary));
        public static Sprite GaugeTrack => gaugeTrack != null ? gaugeTrack : (gaugeTrack = BuildGaugeTrack());
        public static Sprite GaugeFill => gaugeFill != null ? gaugeFill : (gaugeFill = BuildGaugeFill());
        public static Sprite HazardTapeYellow => hazardTapeYellow != null ? hazardTapeYellow : (hazardTapeYellow = BuildHazardTape(false));
        public static Sprite HazardTapeRed => hazardTapeRed != null ? hazardTapeRed : (hazardTapeRed = BuildHazardTape(true));
        public static Sprite LedPipOn => ledPipOn != null ? ledPipOn : (ledPipOn = BuildLedPip(true));
        public static Sprite LedPipOff => ledPipOff != null ? ledPipOff : (ledPipOff = BuildLedPip(false));
        public static Sprite StarFilled => starFilled != null ? starFilled : (starFilled = BuildStar(true));
        public static Sprite StarEmpty => starEmpty != null ? starEmpty : (starEmpty = BuildStar(false));
        public static Sprite CornerBracket => cornerBracket != null ? cornerBracket : (cornerBracket = BuildCornerBracket());

        // ------------------------------------------------------------------
        // Procedural Sprite Builders
        // ------------------------------------------------------------------
        private static Sprite BuildPanelFrame()
        {
            const int size = 64;
            const int b = 14;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "SciFi_PanelFrame",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int dx = Mathf.Min(x, size - 1 - x);
                    int dy = Mathf.Min(y, size - 1 - y);

                    // 45-degree chamfer / cut-corner distance
                    int cornerDist = dx + dy;
                    if (cornerDist < 7)
                    {
                        // Outside chamfer corner
                        pixels[y * size + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    // Outer border line
                    bool isBorder = (cornerDist == 7 || cornerDist == 8 || dx == 0 || dy == 0 || dx == 1 || dy == 1);
                    // Inner corner bracket accent notch
                    bool isNotch = (dx == 12 && dy >= 6 && dy <= 12) || (dy == 12 && dx >= 6 && dx <= 12);

                    if (isBorder || isNotch)
                    {
                        pixels[y * size + x] = new Color32(0, 225, 255, 240);
                    }
                    else if (dx <= 4 || dy <= 4)
                    {
                        // Subtle outer bevel glow
                        pixels[y * size + x] = new Color32(0, 160, 220, 140);
                    }
                    else
                    {
                        // Dark translucent space-glass core
                        pixels[y * size + x] = new Color32(8, 14, 25, 230);
                    }
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);

            // 9-slice: border size 14px on all sides
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        private static Sprite BuildPanelGlass()
        {
            const int size = 32;
            const int b = 8;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "SciFi_PanelGlass",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int dx = Mathf.Min(x, size - 1 - x);
                    int dy = Mathf.Min(y, size - 1 - y);

                    if (dx == 0 || dy == 0)
                    {
                        // Fine 1px cyan hairline border
                        pixels[y * size + x] = new Color32(0, 200, 255, 180);
                    }
                    else
                    {
                        // Subtle horizontal scanline effect
                        byte scanline = (byte)(y % 2 == 0 ? 210 : 185);
                        pixels[y * size + x] = new Color32(10, 16, 28, scanline);
                    }
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(b, b, b, b));
        }

        private enum ButtonState { Normal, Hover, Pressed, Primary }

        private static Sprite BuildButton(ButtonState state)
        {
            const int w = 64;
            const int h = 32;
            const int bX = 12;
            const int bY = 10;

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = $"SciFi_Button_{state}",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            Color32 borderCol = state switch
            {
                ButtonState.Hover => new Color32(0, 255, 255, 255),
                ButtonState.Pressed => new Color32(255, 255, 255, 255),
                ButtonState.Primary => new Color32(255, 180, 20, 255),
                _ => new Color32(0, 180, 240, 200)
            };

            Color32 fillTop = state switch
            {
                ButtonState.Hover => new Color32(15, 45, 75, 245),
                ButtonState.Pressed => new Color32(30, 80, 130, 255),
                ButtonState.Primary => new Color32(110, 45, 10, 245),
                _ => new Color32(12, 22, 38, 235)
            };

            Color32 fillBottom = state switch
            {
                ButtonState.Hover => new Color32(5, 20, 40, 245),
                ButtonState.Pressed => new Color32(10, 35, 65, 255),
                ButtonState.Primary => new Color32(65, 20, 5, 245),
                _ => new Color32(6, 12, 22, 235)
            };

            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = (float)y / (h - 1);
                for (int x = 0; x < w; x++)
                {
                    int dx = Mathf.Min(x, w - 1 - x);
                    int dy = Mathf.Min(y, h - 1 - y);

                    // 45° chamfer corners (5px cut)
                    int cornerDist = dx + dy;
                    if (cornerDist < 5)
                    {
                        pixels[y * w + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    bool isBorder = (cornerDist == 5 || cornerDist == 6 || dx <= 1 || dy <= 1);
                    if (isBorder)
                    {
                        pixels[y * w + x] = borderCol;
                    }
                    else
                    {
                        // Vertical gradient fill with top bevel highlight
                        Color32 c = Color32.Lerp(fillBottom, fillTop, v);
                        if (y == h - 3)
                        {
                            c = Color32.Lerp(c, borderCol, 0.45f); // Subtle top specular sheen
                        }
                        pixels[y * w + x] = c;
                    }
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(bX, bY, bX, bY));
        }

        private static Sprite BuildGaugeTrack()
        {
            const int w = 64;
            const int h = 18;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "SciFi_GaugeTrack",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int dx = Mathf.Min(x, w - 1 - x);
                    int dy = Mathf.Min(y, h - 1 - y);

                    // 45° chamfer corners (3px cut)
                    int cornerDist = dx + dy;
                    if (cornerDist < 3)
                    {
                        pixels[y * w + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    if (cornerDist == 3 || dx == 0 || dy == 0)
                    {
                        // 1px metallic rim
                        pixels[y * w + x] = new Color32(0, 140, 200, 160);
                    }
                    else
                    {
                        // Deep grooved channel with top inner drop shadow
                        byte shadow = (byte)(y >= h - 3 ? 12 : 24);
                        pixels[y * w + x] = new Color32(4, 8, shadow, 240);
                    }
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(6, 4, 6, 4));
        }

        private static Sprite BuildGaugeFill()
        {
            const int w = 32;
            const int h = 16;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
            {
                name = "SciFi_GaugeFill",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[w * h];
            for (int y = 0; y < h; y++)
            {
                float v = (float)y / (h - 1);
                for (int x = 0; x < w; x++)
                {
                    // Clean vertical gradient with bright central core
                    float core = 1.0f - Mathf.Abs(v - 0.5f) * 1.3f;
                    byte brightness = (byte)Mathf.Clamp(core * 255f, 160f, 255f);
                    pixels[y * w + x] = new Color32(brightness, brightness, brightness, 255);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(4, 2, 4, 2));
        }

        private static Sprite BuildHazardTape(bool isRed)
        {
            const int size = 32;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = isRed ? "SciFi_HazardRed" : "SciFi_HazardYellow",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat
            };

            Color32 stripeColor = isRed ? new Color32(240, 35, 40, 240) : new Color32(255, 190, 15, 240);
            Color32 darkColor = new Color32(10, 12, 16, 245);

            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // 45-degree repeating diagonal stripe
                    int diagonal = (x + y) % 16;
                    pixels[y * size + x] = (diagonal < 8) ? stripeColor : darkColor;
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite BuildLedPip(bool isOn)
        {
            const int s = 16;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
            {
                name = isOn ? "SciFi_PipOn" : "SciFi_PipOff",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[s * s];
            float center = (s - 1) * 0.5f;

            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    // Diamond shape: Manhattan distance from center
                    float dist = Mathf.Abs(x - center) + Mathf.Abs(y - center);
                    if (dist > 6.5f)
                    {
                        pixels[y * s + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    if (dist > 5.5f)
                    {
                        // Outer rim
                        pixels[y * s + x] = isOn ? new Color32(0, 225, 255, 255) : new Color32(40, 60, 80, 180);
                    }
                    else if (isOn)
                    {
                        // Glowing LED center
                        float glow = 1.0f - (dist / 5.5f);
                        byte g = (byte)(160 + glow * 95);
                        pixels[y * s + x] = new Color32(g, 255, 255, 255);
                    }
                    else
                    {
                        // Inactive recessed socket
                        pixels[y * s + x] = new Color32(12, 18, 28, 200);
                    }
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite BuildStar(bool isFilled)
        {
            const int s = 32;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
            {
                name = isFilled ? "SciFi_StarFilled" : "SciFi_StarEmpty",
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[s * s];
            float center = (s - 1) * 0.5f;

            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    float dx = x - center;
                    float dy = y - center;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float angle = Mathf.Atan2(dy, dx) + Mathf.PI * 0.5f;
                    if (angle < 0) angle += Mathf.PI * 2f;

                    // 5-point star mathematical modulation
                    float arm = Mathf.Cos(5f * angle);
                    float maxR = 11f + arm * 4.5f;

                    if (r > maxR)
                    {
                        pixels[y * s + x] = new Color32(0, 0, 0, 0);
                        continue;
                    }

                    if (isFilled)
                    {
                        float inner = Mathf.Clamp01(1f - (r / maxR));
                        byte val = (byte)(180 + inner * 75);
                        pixels[y * s + x] = new Color32(255, val, 20, 255);
                    }
                    else
                    {
                        if (r > maxR - 2.5f)
                            pixels[y * s + x] = new Color32(100, 120, 140, 180);
                        else
                            pixels[y * s + x] = new Color32(15, 20, 30, 200);
                    }
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        }

        private static Sprite BuildCornerBracket()
        {
            const int s = 24;
            var tex = new Texture2D(s, s, TextureFormat.RGBA32, false)
            {
                name = "SciFi_CornerBracket",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            var pixels = new Color32[s * s];
            for (int y = 0; y < s; y++)
            {
                for (int x = 0; x < s; x++)
                {
                    bool isBracket = (x <= 3 && y <= 16) || (y <= 3 && x <= 16);
                    pixels[y * s + x] = isBracket ? new Color32(0, 225, 255, 240) : new Color32(0, 0, 0, 0);
                }
            }

            tex.SetPixels32(pixels);
            tex.Apply(false, true);
            return Sprite.Create(tex, new Rect(0, 0, s, s), new Vector2(0.5f, 0.5f), 100f);
        }

        // ------------------------------------------------------------------
        // Quick Convenience Styling Helpers for UGUI
        // ------------------------------------------------------------------
        public static void ApplyPanel(Image img, Color? tint = null)
        {
            if (img == null) return;
            img.sprite = PanelFrame;
            img.type = Image.Type.Sliced;
            img.color = tint ?? Color.white;
        }

        public static void ApplyGlass(Image img, Color? tint = null)
        {
            if (img == null) return;
            img.sprite = PanelGlass;
            img.type = Image.Type.Sliced;
            img.color = tint ?? Color.white;
        }

        public static void ApplyButton(Button btn, Image img, bool isPrimary = false)
        {
            if (img != null)
            {
                img.sprite = isPrimary ? ButtonPrimary : ButtonNormal;
                img.type = Image.Type.Sliced;
                img.color = Color.white;
            }

            if (btn != null)
            {
                btn.transition = Selectable.Transition.SpriteSwap;
                var ss = btn.spriteState;
                ss.highlightedSprite = ButtonHover;
                ss.pressedSprite = ButtonPressed;
                ss.selectedSprite = ButtonHover;
                ss.disabledSprite = ButtonNormal;
                btn.spriteState = ss;

                // Add tactile sound and scale animator if missing
                if (btn.GetComponent<SciFiButtonAnimator>() == null)
                {
                    btn.gameObject.AddComponent<SciFiButtonAnimator>();
                }
            }
        }
    }
}
