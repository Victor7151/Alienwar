using System.Collections.Generic;
using UnityEngine;

namespace Starfall.Core
{
    /// <summary>
    /// Provides dedicated 2D spaceship sprites for all 5 Warship fleet classes.
    /// Loads from Resources if available, or procedurally renders crisp, customized
    /// high-tech sci-fi warship sprites with custom wings, cockpits, and laser hardpoints.
    /// </summary>
    public static class WarshipSpriteUtility
    {
        private static readonly Dictionary<string, Sprite> spriteCache = new Dictionary<string, Sprite>();

        public static Sprite GetShipSprite(WarshipSpec spec)
        {
            if (spec == null) return null;
            return GetShipSprite(spec.id, spec.shipClass, spec.hullColor);
        }

        public static Sprite GetShipSprite(string shipId, WarshipClass shipClass, Color themeColor)
        {
            if (spriteCache.TryGetValue(shipId, out var cached) && cached != null)
            {
                return cached;
            }

            // 1. Try loading from Resources/Warships/
            Sprite loaded = Resources.Load<Sprite>($"Warships/{shipId}") ??
                            Resources.Load<Sprite>($"Warships/{shipId.Replace("ship_", "")}") ??
                            Resources.Load<Sprite>(shipId);

            if (loaded != null)
            {
                spriteCache[shipId] = loaded;
                return loaded;
            }

            // 2. Procedurally generate high-quality 2D warship sprite
            Sprite generated = GenerateProceduralWarshipSprite(shipClass, themeColor);
            if (generated != null)
            {
                spriteCache[shipId] = generated;
                return generated;
            }

            return null;
        }

        private static Sprite GenerateProceduralWarshipSprite(WarshipClass shipClass, Color themeColor)
        {
            int size = 128;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            tex.filterMode = FilterMode.Bilinear;
            tex.wrapMode = TextureWrapMode.Clamp;

            Color[] pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.clear;

            int cx = size / 2;
            int cy = size / 2;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int dx = Mathf.Abs(x - cx);
                    Color col = Color.clear;

                    switch (shipClass)
                    {
                        case WarshipClass.Viper:
                            col = SampleViperPixel(dx, y, themeColor);
                            break;
                        case WarshipClass.Titan:
                            col = SampleTitanPixel(dx, y, themeColor);
                            break;
                        case WarshipClass.Specter:
                            col = SampleSpecterPixel(dx, y, themeColor);
                            break;
                        case WarshipClass.Tempest:
                            col = SampleTempestPixel(dx, y, themeColor);
                            break;
                        case WarshipClass.Phoenix:
                            col = SamplePhoenixPixel(dx, y, themeColor);
                            break;
                    }

                    if (col.a > 0f)
                    {
                        pixels[y * size + x] = col;
                    }
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            Sprite sprite = Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
            return sprite;
        }

        // =========================================================================
        // 1. VIPER MK-I (Tactical Aerospace Interceptor - Sleek Delta Wings & Dual Blasters)
        // =========================================================================
        private static Color SampleViperPixel(int dx, int y, Color theme)
        {
            // Forward nose cone
            if (y >= 85 && y <= 116 && dx <= (116 - y) * 0.35f + 1)
                return Color.Lerp(theme, Color.white, 0.3f);

            // Cockpit canopy
            if (y >= 65 && y <= 86 && dx <= 5 - Mathf.Abs(y - 76) * 0.3f)
                return new Color(0.2f, 0.95f, 1f, 1f); // Glowing cyan glass

            // Main fuselage
            if (y >= 25 && y <= 85 && dx <= 8 + (85 - y) * 0.12f)
                return theme;

            // Swept delta wings
            if (y >= 22 && y <= 58 && dx <= 10 + (58 - y) * 1.15f)
            {
                // Wing edge trim
                if (dx >= 9 + (58 - y) * 1.12f)
                    return Color.Lerp(theme, Color.white, 0.4f);
                return Color.Lerp(theme, Color.black, 0.2f);
            }

            // Dual wingtip laser cannons (2 hardpoints)
            if (y >= 35 && y <= 68 && dx >= 43 && dx <= 47)
                return new Color(0.85f, 0.9f, 1f, 1f);

            // Rear engine thruster glow
            if (y >= 10 && y <= 24 && dx <= 7)
                return new Color(1f, 0.6f, 0.1f, 1f - (24 - y) * 0.04f);

            return Color.clear;
        }

        // =========================================================================
        // 2. CRIMSON TITAN (Heavy Siege Dreadnought - Broad Hull, Twin Plasma Cannons)
        // =========================================================================
        private static Color SampleTitanPixel(int dx, int y, Color theme)
        {
            // Heavy nose battlements
            if (y >= 92 && y <= 108 && dx <= 18)
                return Color.Lerp(theme, Color.white, 0.25f);

            // Twin heavy plasma cannons (extending forward to y=120)
            if (y >= 75 && y <= 120 && dx >= 22 && dx <= 28)
            {
                if (y >= 115) return new Color(1f, 0.3f, 0.1f, 1f); // Muzzle tip
                return new Color(0.3f, 0.32f, 0.36f, 1f); // Dark gunmetal
            }

            // Main reinforced armor hull
            if (y >= 28 && y <= 92 && dx <= 38)
            {
                // Armor plating seams
                if (dx == 18 || y == 60) return Color.Lerp(theme, Color.black, 0.5f);
                return theme;
            }

            // Heavy side sponson armor
            if (y >= 35 && y <= 75 && dx <= 52)
                return Color.Lerp(theme, Color.black, 0.25f);

            // Command bridge viewport
            if (y >= 68 && y <= 80 && dx <= 10)
                return new Color(1f, 0.85f, 0.2f, 1f); // Amber bridge glow

            // Massive dual engine exhausts
            if (y >= 12 && y <= 28 && ((dx >= 8 && dx <= 18) || (dx >= 24 && dx <= 34)))
                return new Color(1f, 0.35f, 0.1f, 0.9f);

            return Color.clear;
        }

        // =========================================================================
        // 3. PHANTOM SPECTER (Stealth Assassin - Needle Dart & Forward-Swept Wings)
        // =========================================================================
        private static Color SampleSpecterPixel(int dx, int y, Color theme)
        {
            // Razor-sharp needle nose
            if (y >= 80 && y <= 122 && dx <= (122 - y) * 0.18f)
                return Color.Lerp(theme, Color.white, 0.5f);

            // Sleek narrow body
            if (y >= 25 && y <= 80 && dx <= 6 + (80 - y) * 0.1f)
                return new Color(0.12f, 0.14f, 0.18f, 1f); // Obsidian stealth coat

            // Forward-swept assassin wings
            if (y >= 30 && y <= 78 && dx <= 8 + (y - 30) * 0.95f)
            {
                // Glowing cyan stealth circuit lines
                if (Mathf.Abs(dx - (y - 30) * 0.85f) < 2)
                    return theme;
                return new Color(0.15f, 0.18f, 0.24f, 1f);
            }

            // Twin needle muzzles at dx=18, y=95
            if (y >= 75 && y <= 102 && dx >= 17 && dx <= 20)
                return theme;

            // Glowing cockpit visor slit
            if (y >= 72 && y <= 85 && dx <= 3)
                return new Color(0f, 1f, 0.85f, 1f);

            // Ion engine glow
            if (y >= 14 && y <= 26 && dx <= 5)
                return new Color(0f, 0.8f, 1f, 0.85f);

            return Color.clear;
        }

        // =========================================================================
        // 4. TEMPEST VORTEX (Electromagnetic Tech Carrier - 3 Muzzles & Power Core)
        // =========================================================================
        private static Color SampleTempestPixel(int dx, int y, Color theme)
        {
            // Center heavy beam cannon (1st hardpoint)
            if (y >= 85 && y <= 120 && dx <= 5)
            {
                if (y >= 114) return new Color(0.8f, 0.4f, 1f, 1f); // Emitter tip
                return new Color(0.28f, 0.25f, 0.35f, 1f);
            }

            // Central vortex power core (spherical glow)
            float distFromCore = Vector2.Distance(new Vector2(dx, y), new Vector2(0, 58));
            if (distFromCore <= 14)
                return Color.Lerp(Color.white, new Color(0.8f, 0.2f, 1f), distFromCore / 14f);

            // Wide carrier hull
            if (y >= 26 && y <= 85 && dx <= 25 + (85 - y) * 0.15f)
                return theme;

            // Outboard vortex weapon pods (2nd and 3rd hardpoints at dx=38, y=92)
            if (y >= 45 && y <= 95 && dx >= 32 && dx <= 46)
            {
                if (y >= 90) return new Color(0.9f, 0.5f, 1f, 1f); // Pod muzzles
                return Color.Lerp(theme, Color.black, 0.25f);
            }

            // Outboard connecting pylons
            if (y >= 42 && y <= 56 && dx <= 38)
                return new Color(0.2f, 0.18f, 0.25f, 1f);

            // Rear plasma vortex exhaust
            if (y >= 12 && y <= 26 && dx <= 18)
                return new Color(0.7f, 0.2f, 1f, 0.8f);

            return Color.clear;
        }

        // =========================================================================
        // 5. SOLARIS PHOENIX (Solar Hybrid Supreme Prototype - 4 Muzzles & Solar Wings)
        // =========================================================================
        private static Color SamplePhoenixPixel(int dx, int y, Color theme)
        {
            // Dual central solar lance emitters (Hardpoints 1 & 2 at dx=16, y=118)
            if (y >= 80 && y <= 118 && dx >= 14 && dx <= 18)
            {
                if (y >= 112) return Color.white; // Superheated tips
                return new Color(1f, 0.85f, 0.2f, 1f);
            }

            // Dual outer photon blasters (Hardpoints 3 & 4 at dx=44, y=100)
            if (y >= 68 && y <= 102 && dx >= 42 && dx <= 46)
            {
                if (y >= 97) return Color.white;
                return new Color(1f, 0.7f, 0.1f, 1f);
            }

            // Sharp crest nose
            if (y >= 88 && y <= 112 && dx <= 7 - (y - 88) * 0.25f)
                return Color.white;

            // Radiant solar body
            if (y >= 26 && y <= 90 && dx <= 14 + (90 - y) * 0.2f)
                return theme;

            // Avian solar sweep wings
            if (y >= 24 && y <= 85 && dx <= 16 + Mathf.Sin((y - 24) * 0.05f) * 38)
            {
                // Solar wing feather edge
                if (dx >= 14 + Mathf.Sin((y - 24) * 0.05f) * 36)
                    return new Color(1f, 0.35f, 0.1f, 1f); // Fiery orange wingtips
                return Color.Lerp(theme, new Color(1f, 0.9f, 0.3f), 0.35f);
            }

            // Solar fusion core
            float distCore = Vector2.Distance(new Vector2(dx, y), new Vector2(0, 56));
            if (distCore <= 12)
                return Color.Lerp(Color.white, new Color(1f, 0.8f, 0.1f), distCore / 12f);

            // Twin solar flare exhausts
            if (y >= 8 && y <= 25 && dx <= 16)
                return new Color(1f, 0.6f, 0.1f, 0.9f);

            return Color.clear;
        }
    }
}
