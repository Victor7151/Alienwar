using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Starfall.VFX
{
    /// <summary>
    /// Lightweight, allocation-free gameplay juice: impact rings, glow flashes, telegraph lines,
    /// floating score popups and full-screen flashes. One Update drives every live effect
    /// (no per-effect coroutines) and all objects are recycled.
    /// </summary>
    public class VFXKit : MonoBehaviour
    {
        public const int SortRing = 32;
        public const int SortTelegraph = 7;
        public const int SortPopup = 45;

        private enum Kind { Ring, Glow, Telegraph, Popup, Beam }

        private class Fx
        {
            public Kind kind;
            public GameObject go;
            public SpriteRenderer sr;
            public TextMeshPro tmp;
            public float t;
            public float duration;
            public float startScale;
            public float endScale;
            public Color color;
            public Vector3 velocity;
            public float width;
        }

        private static VFXKit instance;
        private readonly List<Fx> active = new List<Fx>(64);
        private readonly Stack<Fx> spriteFree = new Stack<Fx>(64);
        private readonly Stack<Fx> popupFree = new Stack<Fx>(16);

        private Canvas flashCanvas;
        private Image flashImage;
        private float flashTime;
        private float flashDuration;
        private Color flashColor;

        private static VFXKit Instance
        {
            get
            {
                if (instance == null)
                {
                    var go = new GameObject("VFXKit");
                    instance = go.AddComponent<VFXKit>();
                }
                return instance;
            }
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        // ------------------------------------------------------------------
        // Public API
        // ------------------------------------------------------------------

        /// <summary>Expanding, fading ring (shockwaves, pickups, shield hits).</summary>
        public static void Ring(Vector3 pos, Color color, float startScale, float endScale, float duration)
        {
            var fx = Instance.TakeSprite(Kind.Ring, ProceduralSprites.Ring, SortRing);
            fx.go.transform.position = pos;
            fx.go.transform.rotation = Quaternion.identity;
            fx.startScale = startScale;
            fx.endScale = endScale;
            fx.duration = Mathf.Max(0.01f, duration);
            fx.color = color;
            fx.go.transform.localScale = Vector3.one * startScale;
            fx.sr.color = color;
        }

        /// <summary>Bright soft flash (death cores, muzzle blooms).</summary>
        public static void Glow(Vector3 pos, Color color, float scale, float duration)
        {
            var fx = Instance.TakeSprite(Kind.Glow, ProceduralSprites.SoftCircle, SortRing - 1);
            fx.go.transform.position = pos;
            fx.go.transform.rotation = Quaternion.identity;
            fx.startScale = scale;
            fx.endScale = scale * 1.35f;
            fx.duration = Mathf.Max(0.01f, duration);
            fx.color = color;
            fx.go.transform.localScale = Vector3.one * scale;
            fx.sr.color = color;
        }

        /// <summary>
        /// Blinking danger line from 'from' toward 'to'. Used to warn the player before a
        /// charge or aimed burst so attacks are readable and dodgeable.
        /// </summary>
        public static void Telegraph(Vector3 from, Vector3 to, Color color, float duration, float width = 0.18f)
        {
            var fx = Instance.TakeSprite(Kind.Telegraph, ProceduralSprites.Bar, SortTelegraph);
            Vector3 d = to - from;
            float len = d.magnitude;
            float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            fx.go.transform.position = from;
            fx.go.transform.rotation = Quaternion.Euler(0f, 0f, ang);
            fx.go.transform.localScale = new Vector3(len, width, 1f);
            fx.width = width;
            fx.duration = Mathf.Max(0.01f, duration);
            fx.color = color;
            fx.sr.color = color;
        }

        /// <summary>Smooth energy beam or tether connecting two points.</summary>
        public static void Beam(Vector3 from, Vector3 to, Color color, float duration, float width = 0.08f)
        {
            var fx = Instance.TakeSprite(Kind.Beam, ProceduralSprites.Bar, SortTelegraph);
            Vector3 d = to - from;
            float len = d.magnitude;
            float ang = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            fx.go.transform.position = from;
            fx.go.transform.rotation = Quaternion.Euler(0f, 0f, ang);
            fx.go.transform.localScale = new Vector3(len, width, 1f);
            fx.width = width;
            fx.duration = Mathf.Max(0.01f, duration);
            fx.color = color;
            fx.sr.color = color;
        }

        /// <summary>Floating text that rises and fades (score, combo, "+SHIELD").</summary>
        public static void Popup(Vector3 pos, string text, Color color, float size = 3.2f)
        {
            var fx = Instance.TakePopup();
            fx.go.transform.position = pos + new Vector3(0f, 0.25f, 0f);
            fx.tmp.text = text;
            fx.tmp.fontSize = size;
            fx.color = color;
            fx.tmp.color = color;
            fx.duration = 0.75f;
            fx.velocity = new Vector3(0f, 1.6f, 0f);
            fx.go.transform.localScale = Vector3.one * 1.15f;
        }

        /// <summary>Full-screen colour flash (player hit, bomb, warp).</summary>
        public static void FlashScreen(Color color, float duration)
        {
            var kit = Instance;
            kit.EnsureFlashCanvas();
            kit.flashColor = color;
            kit.flashDuration = Mathf.Max(0.01f, duration);
            kit.flashTime = 0f;
            kit.flashImage.enabled = true;
            kit.flashImage.color = color;
        }

        /// <summary>Attaches a soft glow halo under a sprite (bullets, auras). Returns the renderer.</summary>
        public static SpriteRenderer AttachGlow(Transform parent, Color color, float scale, int sortingOrder, string name = "Glow")
        {
            var existing = parent.Find(name);
            SpriteRenderer sr;
            if (existing != null)
            {
                sr = existing.GetComponent<SpriteRenderer>();
            }
            else
            {
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = ProceduralSprites.SoftCircle;
            }
            sr.transform.localPosition = Vector3.zero;
            sr.transform.localRotation = Quaternion.identity;
            // Compensate for parent scale so 'scale' is in world units.
            Vector3 ls = parent.lossyScale;
            float inv = Mathf.Abs(ls.x) > 0.0001f ? 1f / Mathf.Abs(ls.x) : 1f;
            sr.transform.localScale = Vector3.one * scale * inv;
            sr.color = color;
            sr.sortingOrder = sortingOrder;
            return sr;
        }

        /// <summary>Same as AttachGlow but using the ring sprite (shield bubble, shielder aura).</summary>
        public static SpriteRenderer AttachRing(Transform parent, Color color, float scale, int sortingOrder, string name = "Ring")
        {
            var sr = AttachGlow(parent, color, scale, sortingOrder, name);
            sr.sprite = ProceduralSprites.Ring;
            return sr;
        }

        // ------------------------------------------------------------------
        // Internals
        // ------------------------------------------------------------------

        private Fx TakeSprite(Kind kind, Sprite sprite, int order)
        {
            Fx fx = spriteFree.Count > 0 ? spriteFree.Pop() : null;
            if (fx == null)
            {
                var go = new GameObject("FX");
                go.transform.SetParent(transform, false);
                fx = new Fx { go = go, sr = go.AddComponent<SpriteRenderer>() };
            }
            fx.kind = kind;
            fx.t = 0f;
            fx.sr.sprite = sprite;
            fx.sr.sortingOrder = order;
            fx.go.SetActive(true);
            active.Add(fx);
            return fx;
        }

        private Fx TakePopup()
        {
            Fx fx = popupFree.Count > 0 ? popupFree.Pop() : null;
            if (fx == null)
            {
                var go = new GameObject("Popup");
                go.transform.SetParent(transform, false);
                var tmp = go.AddComponent<TextMeshPro>();
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.fontStyle = FontStyles.Bold;
                tmp.sortingOrder = SortPopup;
                tmp.textWrappingMode = TextWrappingModes.NoWrap;
                tmp.rectTransform.sizeDelta = new Vector2(4f, 1f);
                fx = new Fx { go = go, tmp = tmp };
            }
            fx.kind = Kind.Popup;
            fx.t = 0f;
            fx.go.SetActive(true);
            active.Add(fx);
            return fx;
        }

        private void EnsureFlashCanvas()
        {
            if (flashCanvas != null) return;
            var go = new GameObject("VFXKit_ScreenFlash", typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(transform, false);
            flashCanvas = go.GetComponent<Canvas>();
            flashCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            flashCanvas.sortingOrder = 900;

            var imgGo = new GameObject("Flash", typeof(RectTransform), typeof(Image));
            imgGo.transform.SetParent(go.transform, false);
            var rt = imgGo.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            flashImage = imgGo.GetComponent<Image>();
            flashImage.raycastTarget = false;
            flashImage.enabled = false;
        }

        private void Update()
        {
            using var __perf = Starfall.Core.PerfMarkers.VFXUpdate.Auto();
            // Effects use unscaled time so they finish even during pause/hit-stop.
            float dt = Time.unscaledDeltaTime;

            for (int i = active.Count - 1; i >= 0; i--)
            {
                var fx = active[i];
                fx.t += dt;
                float p = Mathf.Clamp01(fx.t / fx.duration);

                switch (fx.kind)
                {
                    case Kind.Ring:
                    case Kind.Glow:
                    {
                        float ease = 1f - (1f - p) * (1f - p);
                        float s = Mathf.Lerp(fx.startScale, fx.endScale, ease);
                        fx.go.transform.localScale = new Vector3(s, s, 1f);
                        var c = fx.color;
                        c.a = fx.color.a * (1f - p);
                        fx.sr.color = c;
                        break;
                    }
                    case Kind.Telegraph:
                    {
                        // Blink faster as the attack approaches.
                        float blink = 0.55f + 0.45f * Mathf.Sin(fx.t * Mathf.Lerp(14f, 40f, p));
                        var c = fx.color;
                        c.a = fx.color.a * blink;
                        fx.sr.color = c;
                        var ls = fx.go.transform.localScale;
                        fx.go.transform.localScale = new Vector3(ls.x, fx.width * Mathf.Lerp(1f, 0.45f, p), 1f);
                        break;
                    }
                    case Kind.Popup:
                    {
                        fx.go.transform.position += fx.velocity * dt;
                        fx.velocity *= 0.92f;
                        var c = fx.color;
                        c.a = p < 0.6f ? 1f : 1f - (p - 0.6f) / 0.4f;
                        fx.tmp.color = c;
                        float s = p < 0.12f ? Mathf.Lerp(1.15f, 1f, p / 0.12f) : 1f;
                        fx.go.transform.localScale = Vector3.one * s;
                        break;
                    }
                    case Kind.Beam:
                    {
                        var c = fx.color;
                        c.a = fx.color.a * (1f - p);
                        fx.sr.color = c;
                        var ls = fx.go.transform.localScale;
                        fx.go.transform.localScale = new Vector3(ls.x, fx.width * (1f - p * 0.35f), 1f);
                        break;
                    }
                }

                if (p >= 1f)
                {
                    fx.go.SetActive(false);
                    active.RemoveAt(i);
                    if (fx.kind == Kind.Popup) popupFree.Push(fx);
                    else spriteFree.Push(fx);
                }
            }

            if (flashImage != null && flashImage.enabled)
            {
                flashTime += dt;
                float p = Mathf.Clamp01(flashTime / flashDuration);
                var c = flashColor;
                c.a = flashColor.a * (1f - p);
                flashImage.color = c;
                if (p >= 1f) flashImage.enabled = false;
            }
        }
    }

    public class FloatingDamageText : MonoBehaviour
    {
        private TextMeshPro tmp;
        private float timer = 0f;
        private const float duration = 0.75f;
        private Vector3 startPos;
        private Vector3 randomOffset;
        private bool isCrit;

        public static void Spawn(Vector3 position, int damage, bool isCrit = false)
        {
            GameObject go = new GameObject("FloatingDamage");
            go.transform.position = position;
            FloatingDamageText txt = go.AddComponent<FloatingDamageText>();
            txt.Init(damage, isCrit);
        }

        private void Init(int damage, bool isCrit)
        {
            this.isCrit = isCrit;
            tmp = gameObject.AddComponent<TextMeshPro>();
            tmp.text = damage.ToString();
            tmp.fontSize = isCrit ? 5.5f : 4f;
            tmp.color = isCrit ? new Color(1f, 0.8f, 0.2f) : Color.white;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.sortingOrder = 500;
            tmp.fontStyle = FontStyles.Bold;

            startPos = transform.position;
            randomOffset = new Vector3(Random.Range(-0.8f, 0.8f), Random.Range(1.2f, 1.8f), 0f);
        }

        private void Update()
        {
            timer += Time.deltaTime;
            float p = timer / duration;
            if (p >= 1f)
            {
                Destroy(gameObject);
                return;
            }

            float t = 1f - Mathf.Pow(1f - p, 3f);
            transform.position = startPos + randomOffset * t;
            
            Color c = tmp.color;
            c.a = 1f - Mathf.Pow(p, 2f);
            tmp.color = c;
            
            if (isCrit)
            {
                float s = Mathf.Lerp(1.5f, 0.8f, p);
                transform.localScale = new Vector3(s, s, 1f);
            }
        }
    }
}
