using System.Collections;
using UnityEngine;
using Starfall.Core;

namespace Starfall.VFX
{
    /// <summary>
    /// Production-grade dynamic Energy Deflector Shield.
    /// Replaces static flat rings with multi-layered, counter-rotating energy barriers,
    /// a sci-fi hexagonal deflector lattice, reactive hit ripples, stack-based color shifts,
    /// and cinematic break/recharge visual effects.
    /// </summary>
    public class EnergyDeflectorShield : MonoBehaviour
    {
        [Header("Layer References")]
        [SerializeField] private SpriteRenderer outerRimRenderer;
        [SerializeField] private SpriteRenderer hexLatticeRenderer;
        [SerializeField] private SpriteRenderer coreAuraRenderer;
        [SerializeField] private SpriteRenderer impactRippleRenderer;

        [Header("Rotation & Animation Speeds")]
        [SerializeField] private float outerRotationSpeed = 22f;
        [SerializeField] private float hexRotationSpeed = -15f;
        [SerializeField] private float idlePulseSpeed = 3.5f;
        [Tooltip("Target diameter in world space to cleanly frame the player ship")]
        [SerializeField] private float targetDiameter = 2.15f;
        private float baseScale = 0.42f;

        // Dynamic State
        private float hitFlash = 0f;
        private float currentScaleOffset = 0f;
        private float targetScaleOffset = 0f;
        private float rippleTimer = 0f;
        private float currentShieldRatio = 1f;
        private int currentStacks = 1;
        private bool isCritical = false;
        private bool isDepleted = false;

        // Color Palettes
        private static readonly Color ColorCyanCore = new Color(0.0f, 0.95f, 1.0f, 0.75f);
        private static readonly Color ColorCyanLattice = new Color(0.1f, 0.7f, 1.0f, 0.55f);
        private static readonly Color ColorCyanRim = new Color(0.4f, 0.9f, 1.0f, 0.95f);

        private static readonly Color ColorAzureCore = new Color(0.2f, 0.6f, 1.0f, 0.85f);
        private static readonly Color ColorAzureLattice = new Color(0.35f, 0.5f, 1.0f, 0.65f);
        private static readonly Color ColorAzureRim = new Color(0.5f, 0.85f, 1.0f, 1.0f);

        private static readonly Color ColorGoldCore = new Color(1.0f, 0.85f, 0.2f, 0.9f);
        private static readonly Color ColorGoldLattice = new Color(0.85f, 0.45f, 1.0f, 0.7f);
        private static readonly Color ColorGoldRim = new Color(1.0f, 0.95f, 0.6f, 1.0f);

        private static readonly Color ColorCriticalRim = new Color(1.0f, 0.35f, 0.45f, 0.85f);

        private void Awake()
        {
            EnsureShieldHierarchy();
        }

        /// <summary>
        /// Automatically constructs and binds all multi-layer renderers with additive materials.
        /// </summary>
        public void EnsureShieldHierarchy()
        {
            var mat = VFXMaterials.SpriteAdditiveMaterial;

            // 1. Ambient Core Glow (Soft blurred halo, never a solid disc)
            if (coreAuraRenderer == null)
            {
                var coreGo = GetOrCreateChild("Shield_CoreAura");
                var sr = coreGo.GetComponent<SpriteRenderer>();
                if (sr == null) sr = coreGo.AddComponent<SpriteRenderer>();
                coreAuraRenderer = sr;
                coreAuraRenderer.sprite = VFXMaterials.LoadVFXSprite("circle_02", ProceduralSprites.SoftCircle);
                coreAuraRenderer.material = mat;
                coreAuraRenderer.sortingOrder = 14;
            }

            // 2. Hexagonal Deflector Matrix
            if (hexLatticeRenderer == null)
            {
                var hexGo = GetOrCreateChild("Shield_HexLattice");
                var sr = hexGo.GetComponent<SpriteRenderer>();
                if (sr == null) sr = hexGo.AddComponent<SpriteRenderer>();
                hexLatticeRenderer = sr;
                hexLatticeRenderer.sprite = VFXMaterials.LoadVFXSprite("magic_03", 
                    VFXMaterials.LoadVFXSprite("circle_03", ProceduralSprites.Ring));
                hexLatticeRenderer.material = mat;
                hexLatticeRenderer.sortingOrder = 15;
            }

            // 3. Kinetic Deflector Barrier Rim
            if (outerRimRenderer == null)
            {
                var rimGo = GetOrCreateChild("Shield_OuterRim");
                var sr = rimGo.GetComponent<SpriteRenderer>();
                if (sr == null) sr = rimGo.AddComponent<SpriteRenderer>();
                outerRimRenderer = sr;
                outerRimRenderer.sprite = VFXMaterials.LoadVFXSprite("circle_05", 
                    VFXMaterials.LoadVFXSprite("circle_04", ProceduralSprites.Ring));
                outerRimRenderer.material = mat;
                outerRimRenderer.sortingOrder = 16;
            }

            // 4. Directional Impact Ripple
            if (impactRippleRenderer == null)
            {
                var ripGo = GetOrCreateChild("Shield_ImpactRipple");
                var sr = ripGo.GetComponent<SpriteRenderer>();
                if (sr == null) sr = ripGo.AddComponent<SpriteRenderer>();
                impactRippleRenderer = sr;
                impactRippleRenderer.sprite = VFXMaterials.LoadVFXSprite("circle_04", ProceduralSprites.Ring);
                impactRippleRenderer.material = mat;
                impactRippleRenderer.sortingOrder = 17;
                impactRippleRenderer.enabled = false;
            }

            CalibrateScale();
            UpdateVisualLayers(0f);
        }

        /// <summary>
        /// Dynamically measures the player plane's bounds to ensure the shield covers the entire plane snugly without being too big.
        /// </summary>
        public void CalibrateScale()
        {
            var shipRenderer = GetComponentInParent<SpriteRenderer>();
            if (shipRenderer != null && shipRenderer.sprite != null)
            {
                float shipWidth = shipRenderer.bounds.size.x;
                float shipHeight = shipRenderer.bounds.size.y;
                float shipMaxDim = Mathf.Max(shipWidth, shipHeight);
                if (shipMaxDim > 0.5f)
                {
                    // Cleanly covers the entire plane with a snug ~18% clearance
                    targetDiameter = Mathf.Max(2.15f, shipMaxDim * 1.18f);
                }
            }
            else
            {
                targetDiameter = 2.15f;
            }

            float nativeWidth = 5.12f;
            if (outerRimRenderer != null && outerRimRenderer.sprite != null && outerRimRenderer.sprite.rect.width > 0f)
            {
                nativeWidth = outerRimRenderer.sprite.rect.width / outerRimRenderer.sprite.pixelsPerUnit;
            }

            float parentLossyScale = (transform.parent != null && transform.parent.lossyScale.x > 0.01f) 
                ? transform.parent.lossyScale.x 
                : 1f;

            baseScale = nativeWidth > 0f ? (targetDiameter / (nativeWidth * parentLossyScale)) : 0.42f;
        }

        private GameObject GetOrCreateChild(string childName)
        {
            var child = transform.Find(childName);
            if (child != null) return child.gameObject;
            var go = new GameObject(childName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
            return go;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            UpdateVisualLayers(dt);
        }

        /// <summary>
        /// Drives real-time shield state from PlayerHealth.
        /// </summary>
        public void SetShieldStats(int currentShield, int maxShield, int shieldStacks)
        {
            currentStacks = Mathf.Max(1, shieldStacks);
            currentShieldRatio = maxShield > 0 ? Mathf.Clamp01((float)currentShield / maxShield) : 0f;
            isCritical = currentShield == 1 && maxShield > 1;
            isDepleted = currentShield <= 0;

            CalibrateScale();

            if (isDepleted)
            {
                SetVisible(false);
            }
            else
            {
                SetVisible(true);
            }
        }

        public void SetVisible(bool visible)
        {
            if (coreAuraRenderer != null) coreAuraRenderer.enabled = visible;
            if (hexLatticeRenderer != null) hexLatticeRenderer.enabled = visible;
            if (outerRimRenderer != null) outerRimRenderer.enabled = visible;
            if (!visible && impactRippleRenderer != null) impactRippleRenderer.enabled = false;
        }

        /// <summary>
        /// Triggers visual kinetic impact at the localized hit point on the shield surface.
        /// </summary>
        public void OnShieldHit(Vector2 hitPoint)
        {
            hitFlash = 1.0f;
            targetScaleOffset = baseScale * 0.08f; // Crisp 8% shock outward pulse
            rippleTimer = 0.35f;

            if (impactRippleRenderer != null)
            {
                impactRippleRenderer.enabled = true;
                Vector3 localHit = transform.InverseTransformPoint(hitPoint);
                if (localHit.sqrMagnitude > 0.01f)
                {
                    float angle = Mathf.Atan2(localHit.y, localHit.x) * Mathf.Rad2Deg - 90f;
                    impactRippleRenderer.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
                }
            }
        }

        /// <summary>
        /// Triggers explosive plasma collapse when shield is destroyed.
        /// </summary>
        public void OnShieldBroken()
        {
            StartCoroutine(ShieldBreakRoutine());
        }

        private IEnumerator ShieldBreakRoutine()
        {
            hitFlash = 1.5f;
            float elapsed = 0f;
            float duration = 0.35f;

            Vector3 startScale = Vector3.one * (baseScale + currentScaleOffset);
            Vector3 endScale = Vector3.one * (baseScale * 2.2f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float ease = Mathf.Sin(t * Mathf.PI * 0.5f);

                transform.localScale = Vector3.Lerp(startScale, endScale, ease);
                float a = Mathf.Lerp(1f, 0f, t);

                if (outerRimRenderer != null)
                    outerRimRenderer.color = new Color(0.2f, 0.8f, 1f, a * 0.9f);
                if (hexLatticeRenderer != null)
                    hexLatticeRenderer.color = new Color(1f, 0.3f, 0.5f, a * 0.7f);
                if (coreAuraRenderer != null)
                    coreAuraRenderer.color = new Color(0.4f, 0.9f, 1f, a * 0.5f);

                yield return null;
            }

            SetVisible(false);
            transform.localScale = Vector3.one * baseScale;
        }

        /// <summary>
        /// Triggers inward implosion flash when shields are restored via pickup.
        /// </summary>
        public void OnShieldRecharged()
        {
            StopAllCoroutines();
            StartCoroutine(ShieldRechargeRoutine());
        }

        private IEnumerator ShieldRechargeRoutine()
        {
            SetVisible(true);
            float elapsed = 0f;
            float duration = 0.4f;

            Vector3 startScale = Vector3.one * (baseScale * 2.5f);
            Vector3 endScale = Vector3.one * baseScale;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                float ease = 1f - Mathf.Pow(1f - t, 3f);

                transform.localScale = Vector3.Lerp(startScale, endScale, ease);
                float flash = Mathf.Sin(t * Mathf.PI);

                if (outerRimRenderer != null)
                    outerRimRenderer.color = Color.Lerp(Color.white, ColorCyanRim, t) * (0.8f + flash * 0.6f);
                if (hexLatticeRenderer != null)
                    hexLatticeRenderer.color = Color.Lerp(Color.white, ColorCyanLattice, t) * (0.6f + flash * 0.5f);

                yield return null;
            }

            transform.localScale = Vector3.one * baseScale;
            hitFlash = 0.5f;
        }

        private void UpdateVisualLayers(float dt)
        {
            if (isDepleted && hitFlash <= 0.01f) return;

            // 1. Kinetic Dampening on Scale Pulse
            currentScaleOffset = Mathf.MoveTowards(currentScaleOffset, targetScaleOffset, dt * 4f);
            targetScaleOffset = Mathf.MoveTowards(targetScaleOffset, 0f, dt * 2.5f);

            // 2. Idle Harmonic Oscillation
            float pulse = Mathf.Sin(Time.time * idlePulseSpeed) * (baseScale * 0.02f);
            float netScale = baseScale + currentScaleOffset + pulse;
            transform.localScale = Vector3.one * netScale;

            // 3. Counter-Rotations
            if (outerRimRenderer != null)
            {
                outerRimRenderer.transform.Rotate(0f, 0f, outerRotationSpeed * dt);
            }
            if (hexLatticeRenderer != null)
            {
                hexLatticeRenderer.transform.Rotate(0f, 0f, hexRotationSpeed * dt);
            }

            // 4. Hit Flash Decay
            hitFlash = Mathf.MoveTowards(hitFlash, 0f, dt * 3.8f);

            // 5. Critical Flicker (instability near 0 shield)
            float flicker = 1f;
            if (isCritical)
            {
                flicker = 0.45f + 0.55f * Mathf.PerlinNoise(Time.time * 28f, 0f);
            }

            // 6. Color & Alpha Interpolation based on Stacks
            Color targetCore, targetLattice, targetRim;
            if (currentStacks >= 3)
            {
                targetCore = ColorGoldCore;
                targetLattice = ColorGoldLattice;
                targetRim = ColorGoldRim;
            }
            else if (currentStacks == 2)
            {
                targetCore = ColorAzureCore;
                targetLattice = ColorAzureLattice;
                targetRim = ColorAzureRim;
            }
            else
            {
                targetCore = isCritical ? Color.Lerp(ColorCyanCore, ColorCriticalRim, 0.4f) : ColorCyanCore;
                targetLattice = isCritical ? Color.Lerp(ColorCyanLattice, ColorCriticalRim, 0.5f) : ColorCyanLattice;
                targetRim = isCritical ? ColorCriticalRim : ColorCyanRim;
            }

            float baseAlpha = (0.25f + 0.55f * currentShieldRatio) * flicker;
            float flashBoost = hitFlash * 0.85f;

            if (coreAuraRenderer != null)
            {
                Color c = targetCore;
                c.a = Mathf.Clamp01((c.a * baseAlpha * 0.08f) + flashBoost * 0.18f);
                coreAuraRenderer.color = c;
            }

            if (hexLatticeRenderer != null)
            {
                Color c = targetLattice;
                c.a = Mathf.Clamp01((c.a * baseAlpha * 0.35f) + flashBoost * 0.40f);
                hexLatticeRenderer.color = c;
            }

            if (outerRimRenderer != null)
            {
                Color c = targetRim;
                c.a = Mathf.Clamp01((c.a * baseAlpha * 0.70f) + flashBoost * 0.55f);
                outerRimRenderer.color = c;
            }

            // 7. Impact Ripple Fade
            if (rippleTimer > 0f)
            {
                rippleTimer -= dt;
                float ripT = 1f - (rippleTimer / 0.35f);
                if (impactRippleRenderer != null)
                {
                    impactRippleRenderer.transform.localScale = Vector3.one * Mathf.Lerp(1.1f, 1.85f, ripT);
                    Color rc = Color.white;
                    rc.a = Mathf.Lerp(0.85f, 0f, ripT * ripT);
                    impactRippleRenderer.color = rc;
                }
            }
            else if (impactRippleRenderer != null && impactRippleRenderer.enabled)
            {
                impactRippleRenderer.enabled = false;
            }
        }
    }
}
