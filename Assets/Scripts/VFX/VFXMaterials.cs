using UnityEngine;

namespace Starfall.VFX
{
    /// <summary>
    /// Centralized cache and factory for production-grade VFX materials.
    /// Ensures particle systems (missile trails, thruster exhaust, smoke, sparks)
    /// always receive optimized additive or alpha-blended materials with the correct textures.
    /// </summary>
    public static class VFXMaterials
    {
        private static Material fireMaterial;
        private static Material smokeMaterial;
        private static Material sparkMaterial;

        private static Material glowMaterial;

        private static void BindTexture(Material mat, Texture2D tex, Texture2D fallback = null)
        {
            if (mat == null) return;
            var t = tex != null ? tex : (fallback != null ? fallback : ProceduralSprites.SoftCircle.texture);
            if (t != null)
            {
                mat.mainTexture = t;
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", t);
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", t);
            }
        }

        public static Material Fire
        {
            get
            {
                var tex = Resources.Load<Texture2D>("VFX/Textures/fire_01") 
                       ?? Resources.Load<Texture2D>("fire_01");
#if UNITY_EDITOR
                if (tex == null)
                    tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/VFX/Textures/fire_01.png");
#endif
                if (fireMaterial != null)
                {
                    BindTexture(fireMaterial, tex, ProceduralSprites.FlameJet.texture);
                    return fireMaterial;
                }
                fireMaterial = Resources.Load<Material>("VFX/Materials/VFX_FireMat");
#if UNITY_EDITOR
                if (fireMaterial == null)
                    fireMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/VFX/VFX_FireMat.mat");
#endif
                if (fireMaterial == null)
                {
                    var shader = Shader.Find("Starfall/SpriteAdditive") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
                    fireMaterial = new Material(shader) { name = "Runtime_VFX_FireMat" };
                }
                BindTexture(fireMaterial, tex, ProceduralSprites.FlameJet.texture);
                return fireMaterial;
            }
        }

        public static Material Smoke
        {
            get
            {
                var tex = Resources.Load<Texture2D>("VFX/Textures/smoke_04") 
                       ?? Resources.Load<Texture2D>("VFX/Textures/smoke_01")
                       ?? Resources.Load<Texture2D>("smoke_01");
#if UNITY_EDITOR
                if (tex == null)
                    tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/VFX/Textures/smoke_04.png")
                       ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/VFX/Textures/smoke_01.png");
#endif
                if (smokeMaterial != null)
                {
                    BindTexture(smokeMaterial, tex, ProceduralSprites.SoftCircle.texture);
                    return smokeMaterial;
                }
                smokeMaterial = Resources.Load<Material>("VFX/Materials/VFX_SmokeAlpha");
#if UNITY_EDITOR
                if (smokeMaterial == null)
                    smokeMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/VFX_SmokeAlpha.mat")
                                 ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/VFX/VFX_SmokeMat.mat");
#endif
                if (smokeMaterial == null)
                {
                    var shader = Shader.Find("Starfall/SpriteAlphaBlended") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
                    smokeMaterial = new Material(shader) { name = "Runtime_VFX_SmokeMat" };
                }
                BindTexture(smokeMaterial, tex, ProceduralSprites.SoftCircle.texture);
                return smokeMaterial;
            }
        }

        public static Material Spark
        {
            get
            {
                var tex = Resources.Load<Texture2D>("VFX/Textures/spark_02") 
                       ?? Resources.Load<Texture2D>("VFX/Textures/spark_01")
                       ?? Resources.Load<Texture2D>("spark_01");
#if UNITY_EDITOR
                if (tex == null)
                    tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/VFX/Textures/spark_02.png")
                       ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/VFX/Textures/spark_01.png");
#endif
                if (sparkMaterial != null)
                {
                    BindTexture(sparkMaterial, tex, ProceduralSprites.SoftCircle.texture);
                    return sparkMaterial;
                }
                sparkMaterial = Resources.Load<Material>("VFX/Materials/VFX_SparkAdditive");
#if UNITY_EDITOR
                if (sparkMaterial == null)
                    sparkMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/VFX_SparkAdditive.mat")
                                 ?? UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/VFX/VFX_SparkMat.mat");
#endif
                if (sparkMaterial == null)
                {
                    var shader = Shader.Find("Starfall/SpriteAdditive") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
                    sparkMaterial = new Material(shader) { name = "Runtime_VFX_SparkMat" };
                }
                BindTexture(sparkMaterial, tex, ProceduralSprites.SoftCircle.texture);
                return sparkMaterial;
            }
        }

        public static Material Glow
        {
            get
            {
                var tex = Resources.Load<Texture2D>("VFX/Textures/circle_02") 
                       ?? Resources.Load<Texture2D>("circle_02");
#if UNITY_EDITOR
                if (tex == null)
                    tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/VFX/Textures/circle_02.png");
#endif
                if (glowMaterial != null)
                {
                    BindTexture(glowMaterial, tex, ProceduralSprites.SoftCircle.texture);
                    return glowMaterial;
                }
                glowMaterial = Resources.Load<Material>("VFX/Materials/VFX_GlowAdditive");
#if UNITY_EDITOR
                if (glowMaterial == null)
                    glowMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/VFX_GlowAdditive.mat");
#endif
                if (glowMaterial == null)
                {
                    var shader = Shader.Find("Starfall/SpriteAdditive") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
                    glowMaterial = new Material(shader) { name = "Runtime_VFX_GlowMat" };
                }
                BindTexture(glowMaterial, tex, ProceduralSprites.SoftCircle.texture);
                return glowMaterial;
            }
        }

        private static Material flashStarMaterial;
        public static Material FlashStar
        {
            get
            {
                var tex = Resources.Load<Texture2D>("VFX/Textures/star_06") 
                       ?? Resources.Load<Texture2D>("star_06");
#if UNITY_EDITOR
                if (tex == null)
                    tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/VFX/Textures/star_06.png");
#endif
                if (flashStarMaterial != null)
                {
                    BindTexture(flashStarMaterial, tex, ProceduralSprites.SoftCircle.texture);
                    return flashStarMaterial;
                }
                flashStarMaterial = Resources.Load<Material>("VFX/Materials/VFX_FlashAdditive");
#if UNITY_EDITOR
                if (flashStarMaterial == null)
                    flashStarMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/VFX_FlashAdditive.mat");
#endif
                if (flashStarMaterial == null)
                {
                    var shader = Shader.Find("Starfall/SpriteAdditive") ?? Shader.Find("Universal Render Pipeline/Particles/Unlit") ?? Shader.Find("Sprites/Default");
                    flashStarMaterial = new Material(shader) { name = "Runtime_VFX_FlashStarMat" };
                }
                BindTexture(flashStarMaterial, tex, ProceduralSprites.SoftCircle.texture);
                return flashStarMaterial;
            }
        }

        private static Material debrisMaterial;
        public static Material Debris
        {
            get
            {
                var tex = Resources.Load<Texture2D>("VFX/Textures/dirt_01") 
                       ?? Resources.Load<Texture2D>("dirt_01");
#if UNITY_EDITOR
                if (tex == null)
                    tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/VFX/Textures/dirt_01.png");
#endif
                if (debrisMaterial != null)
                {
                    BindTexture(debrisMaterial, tex, ProceduralSprites.SoftCircle.texture);
                    return debrisMaterial;
                }
                debrisMaterial = Resources.Load<Material>("VFX/Materials/VFX_MetalDebrisAdditive");
#if UNITY_EDITOR
                if (debrisMaterial == null)
                    debrisMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/VFX_MetalDebrisAdditive.mat");
#endif
                if (debrisMaterial == null)
                {
                    var shader = Shader.Find("Starfall/SpriteAdditive") ?? Shader.Find("Sprites/Default");
                    debrisMaterial = new Material(shader) { name = "Runtime_VFX_DebrisMat" };
                }
                BindTexture(debrisMaterial, tex, ProceduralSprites.SoftCircle.texture);
                return debrisMaterial;
            }
        }

        private static Material rockDebrisMaterial;
        public static Material RockDebris
        {
            get
            {
                var tex = Resources.Load<Texture2D>("VFX/Textures/dirt_02") 
                       ?? Resources.Load<Texture2D>("dirt_02");
#if UNITY_EDITOR
                if (tex == null)
                    tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/VFX/Textures/dirt_02.png");
#endif
                if (rockDebrisMaterial != null)
                {
                    BindTexture(rockDebrisMaterial, tex, ProceduralSprites.SoftCircle.texture);
                    return rockDebrisMaterial;
                }
                rockDebrisMaterial = Resources.Load<Material>("VFX/Materials/VFX_RockDebrisAlpha");
#if UNITY_EDITOR
                if (rockDebrisMaterial == null)
                    rockDebrisMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/VFX_RockDebrisAlpha.mat");
#endif
                if (rockDebrisMaterial == null)
                {
                    var shader = Shader.Find("Starfall/SpriteAlphaBlended") ?? Shader.Find("Sprites/Default");
                    rockDebrisMaterial = new Material(shader) { name = "Runtime_VFX_RockDebrisMat" };
                }
                BindTexture(rockDebrisMaterial, tex, ProceduralSprites.SoftCircle.texture);
                return rockDebrisMaterial;
            }
        }

        private static Material bioSplatterMaterial;
        public static Material BioSplatter
        {
            get
            {
                var tex = Resources.Load<Texture2D>("VFX/Textures/trace_01") 
                       ?? Resources.Load<Texture2D>("trace_01");
#if UNITY_EDITOR
                if (tex == null)
                    tex = UnityEditor.AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/VFX/Textures/trace_01.png");
#endif
                if (bioSplatterMaterial != null)
                {
                    BindTexture(bioSplatterMaterial, tex, ProceduralSprites.SoftCircle.texture);
                    return bioSplatterMaterial;
                }
                bioSplatterMaterial = Resources.Load<Material>("VFX/Materials/VFX_AlienBioAdditive");
#if UNITY_EDITOR
                if (bioSplatterMaterial == null)
                    bioSplatterMaterial = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/VFX_AlienBioAdditive.mat");
#endif
                if (bioSplatterMaterial == null)
                {
                    var shader = Shader.Find("Starfall/SpriteAdditive") ?? Shader.Find("Sprites/Default");
                    bioSplatterMaterial = new Material(shader) { name = "Runtime_VFX_BioSplatterMat" };
                }
                BindTexture(bioSplatterMaterial, tex, ProceduralSprites.SoftCircle.texture);
                return bioSplatterMaterial;
            }
        }

        private static Material additiveSpriteMat;
        public static Material SpriteAdditiveMaterial
        {
            get
            {
                if (additiveSpriteMat != null) return additiveSpriteMat;
                var shader = Shader.Find("Starfall/SpriteAdditive") ?? Shader.Find("Sprites/Default");
                additiveSpriteMat = new Material(shader) { name = "Runtime_VFX_SpriteAdditiveMat" };
                return additiveSpriteMat;
            }
        }

        private static Material alphaBlendedSpriteMat;
        public static Material SpriteAlphaBlendedMaterial
        {
            get
            {
                if (alphaBlendedSpriteMat != null) return alphaBlendedSpriteMat;
                var shader = Shader.Find("Starfall/SpriteAlphaBlended") ?? Shader.Find("Sprites/Default");
                alphaBlendedSpriteMat = new Material(shader) { name = "Runtime_VFX_SpriteAlphaBlendedMat" };
                return alphaBlendedSpriteMat;
            }
        }

        private static readonly System.Collections.Generic.Dictionary<string, Sprite> cachedSprites = new();

        /// <summary>
        /// Loads high-fidelity Kenney VFX sprites with zero lag and procedural fallback safety.
        /// </summary>
        public static Sprite LoadVFXSprite(string name, Sprite fallback = null)
        {
            if (string.IsNullOrEmpty(name)) return fallback;
            if (cachedSprites.TryGetValue(name, out var cached) && cached != null)
                return cached;

            Sprite spr = null;
#if UNITY_EDITOR
            string path = $"Assets/Art/VFX/Textures/{name}.png";
            spr = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>(path);
            if (spr == null)
            {
                var all = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
                for (int i = 0; i < all.Length; i++)
                {
                    if (all[i] is Sprite s) { spr = s; break; }
                }
            }
#endif
            if (spr == null)
            {
                spr = Resources.Load<Sprite>($"VFX/Textures/{name}") ??
                      Resources.Load<Sprite>(name);
            }

            if (spr == null) spr = fallback;
            if (spr != null) cachedSprites[name] = spr;
            return spr;
        }
    }
}

