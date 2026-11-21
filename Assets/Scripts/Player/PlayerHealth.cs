using System.Collections;
using UnityEngine;
using Starfall.Combat;
using Starfall.Core;

namespace Starfall.Player
{
    /// <summary>
    /// Implements Section 17 of GDD: Dual defensive layers (Shield + Hull).
    /// Shield absorbs incoming damage and regenerates after delay without damage.
    /// Hull represents permanent ship health.
    /// </summary>
    public class PlayerHealth : MonoBehaviour, IDamageable
    {
        [Header("Defense Stats")]
        [SerializeField] private int maxLives = 3;
        [SerializeField] private int maxShield = 3;
        [SerializeField] private int maxHull = 3;
        [SerializeField] private int maxShieldStacks = 5;
        [SerializeField] private float shieldRegenDelay = 3.5f;
        [SerializeField] private float shieldRegenRate = 1.0f; // Seconds per 1 shield point
        [SerializeField] private float invulnerabilityDuration = 2.0f;

        [Header("Visuals")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Color hitColor = new Color(1f, 0.3f, 0.3f, 0.8f);
        [SerializeField] private string explosionPoolKey = "ExplosionVFX";

        private int currentLives;
        private int currentShield;
        private int currentHull;
        private bool isInvulnerable = false;
        private Color originalColor = Color.white;
        private Collider2D playerCollider;
        private PlayerController playerController;

        public int CurrentLives => currentLives;
        public int MaxLives => maxLives;
        public int CurrentShield => currentShield;
        public int CurrentHull => currentHull;
        public int MaxShield => maxShield;
        public int MaxHull => maxHull;
        public int MaxShieldStacks => maxShieldStacks;
        public int ShieldLayers => maxShield > 0 ? Mathf.CeilToInt((float)currentShield / maxShield) : 0;
        public int MaxAccumulatedShield => maxShield * maxShieldStacks;
        public bool IsDead => currentLives <= 0;

        public event System.Action<int, int> OnLivesChanged;
        public event System.Action<int, int> OnShieldChanged;
        public event System.Action<int, int> OnHullChanged;
        public event System.Action OnPlayerDeath;
        /// <summary>Raised whenever an unblocked hit lands (shield or hull). Resets combo, voids flawless bonus.</summary>
        public event System.Action OnDamageTaken;

        private Starfall.VFX.EnergyDeflectorShield deflectorShield;

        /// <summary>Per-sector shield regeneration. Harder sectors make you earn your shield back.</summary>
        public void SetShieldRegen(float delay, float secondsPerPoint)
        {
            shieldRegenDelay = Mathf.Max(0.5f, delay);
            shieldRegenRate = Mathf.Max(0.2f, secondsPerPoint);
        }

        private void Awake()
        {
            playerCollider = GetComponent<Collider2D>();
            playerController = GetComponent<PlayerController>();

            if (spriteRenderer == null)
            {
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            }
            var activeShip = Starfall.Core.ShipUpgradesManager.GetActiveShipSpec();
            maxLives = Mathf.Clamp(Starfall.Core.ShipUpgradesManager.GetEffectiveLives(), 1, 5);
            maxShield = Starfall.Core.ShipUpgradesManager.GetEffectiveShields();

            if (spriteRenderer != null && activeShip != null)
            {
                var shipSprite = Starfall.Core.WarshipSpriteUtility.GetShipSprite(activeShip);
                if (shipSprite != null)
                {
                    spriteRenderer.sprite = shipSprite;
                }
                spriteRenderer.color = Color.white;
                originalColor = Color.white;
            }
        }

        public void TriggerInvulnerability(float duration)
        {
            StartCoroutine(TemporaryInvulnerabilityRoutine(duration));
        }

        private IEnumerator TemporaryInvulnerabilityRoutine(float duration)
        {
            isInvulnerable = true;
            if (spriteRenderer != null)
            {
                spriteRenderer.color = new Color(originalColor.r, originalColor.g, originalColor.b, 0.45f);
            }
            yield return new WaitForSeconds(duration);
            isInvulnerable = false;
            if (spriteRenderer != null)
            {
                spriteRenderer.color = originalColor;
            }
        }

        private void Start()
        {
            ResetHealth();
        }

        public void ResetHealth()
        {
            currentLives = maxLives;
            currentShield = maxShield;
            currentHull = maxHull;
            isInvulnerable = false;
            OnLivesChanged?.Invoke(currentLives, maxLives);
            OnShieldChanged?.Invoke(currentShield, maxShield);
            OnHullChanged?.Invoke(currentHull, maxHull);
        }

        /// <summary>Restores shield by the given amount, or accumulates another full shield layer if amount <= 0.</summary>
        public void RestoreShield(int amount = -1)
        {
            int add = (amount <= 0) ? maxShield : amount;
            currentShield = Mathf.Min(currentShield + add, MaxAccumulatedShield);
            OnShieldChanged?.Invoke(currentShield, maxShield);

            int layers = ShieldLayers;
            if (layers > 1)
            {
                Starfall.UI.GameplayHUD.Instance?.ShowAnnouncement($"<color=#00e5ff><b>SHIELD STACKED (x{layers})!</b></color>", new Color(0f, 0.9f, 1f));
                Starfall.VFX.VFXKit.Ring(transform.position, new Color(0.2f, 0.9f, 1f, 0.95f), 1.2f, 2.6f, 0.35f);
            }
            deflectorShield?.OnShieldRecharged();
        }

        public void RepairShield(int amount) => RestoreShield(amount);

        public void RepairHull(int amount)
        {
            currentHull = Mathf.Min(currentHull + amount, maxHull);
            OnHullChanged?.Invoke(currentHull, maxHull);
        }

        /// <summary>Revives the player after Game Over (e.g., from Emergency Reinforcements / Distress Beacon).</summary>
        public void Revive(int lives = 1)
        {
            if (playerCollider == null) playerCollider = GetComponent<Collider2D>();
            if (playerController == null) playerController = GetComponent<PlayerController>();
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();

            gameObject.SetActive(true);
            transform.position = new Vector3(0f, PlayfieldBounds.Bottom + 1.2f, 0f);

            currentLives = Mathf.Clamp(lives, 1, maxLives);
            currentShield = maxShield;
            currentHull = maxHull;

            if (spriteRenderer != null)
            {
                spriteRenderer.enabled = true;
                spriteRenderer.color = originalColor;
            }
            if (playerCollider != null) playerCollider.enabled = true;
            if (playerController != null) playerController.enabled = true;

            OnLivesChanged?.Invoke(currentLives, maxLives);
            OnShieldChanged?.Invoke(currentShield, maxShield);
            OnHullChanged?.Invoke(currentHull, maxHull);

            Starfall.VFX.VFXKit.Ring(transform.position, new Color(0.2f, 0.95f, 0.65f, 0.95f), 1.2f, 3f, 0.5f);
            TriggerInvulnerability(3.5f);
        }

        private void Update()
        {
            using var __perf = Starfall.Core.PerfMarkers.PlayerUpdate.Auto();
            if (IsDead) return;
            UpdateShieldBubble();

            // Shield does NOT auto-regenerate. A Shield power-up must be picked up to restore it.
        }

        public void TakeDamage(int damage, Vector2 hitPoint)
        {
            if (isInvulnerable || IsDead) return;

            OnDamageTaken?.Invoke();
            GameManager.Instance?.BreakCombo();

            bool hadShield = currentShield > 0;
            if (hadShield)
            {
                deflectorShield?.OnShieldHit(hitPoint);
                Starfall.Audio.AudioManager.Instance?.PlayHit(true);
                Starfall.Audio.HapticFeedback.MediumImpact();
            }
            else
            {
                Starfall.Audio.AudioManager.Instance?.PlayImpactHull();
                Starfall.Audio.HapticFeedback.HeavyImpact();
            }

            if (Starfall.Combat.TacticalPerkManager.Instance != null && Starfall.Combat.TacticalPerkManager.Instance.DamageMitigation > 0f)
            {
                damage = Mathf.Max(1, Mathf.RoundToInt(damage * (1f - Starfall.Combat.TacticalPerkManager.Instance.DamageMitigation)));
            }

            int remainingDamage = damage;

            // Shield takes damage first
            if (currentShield > 0)
            {
                if (currentShield >= remainingDamage)
                {
                    currentShield -= remainingDamage;
                    remainingDamage = 0;
                }
                else
                {
                    remainingDamage -= currentShield;
                    currentShield = 0;
                }
                OnShieldChanged?.Invoke(currentShield, maxShield);
                if (currentShield <= 0)
                {
                    deflectorShield?.OnShieldBroken();
                }
            }

            // Hull takes remaining damage
            if (remainingDamage > 0)
            {
                currentHull = Mathf.Max(0, currentHull - remainingDamage);
                OnHullChanged?.Invoke(currentHull, maxHull);
            }

            // If hull depleted or lethal damage hit, trigger life loss / respawn
            if (currentHull <= 0)
            {
                HandleLifeLost();
            }
            else
            {
                StartCoroutine(InvulnerabilityRoutine(0.8f));
            }
        }

        private void HandleLifeLost()
        {
            if (currentLives > 1)
            {
                currentLives--;
                OnLivesChanged?.Invoke(currentLives, maxLives);

                // Losing a ship costs a weapon level: staying alive is what keeps you strong.
                GetComponent<PlayerCombat>()?.DowngradeWeapon(1);
                Starfall.VFX.VFXKit.FlashScreen(new Color(1f, 0.95f, 0.85f, 0.6f), 0.5f);

                // Spawn explosion VFX & sound
                if (ObjectPool.Instance != null && !string.IsNullOrEmpty(explosionPoolKey))
                {
                    ObjectPool.Instance.Spawn(explosionPoolKey, transform.position, Quaternion.identity);
                }
                Starfall.Audio.AudioManager.Instance?.PlayExplosion();

                // Respawn at center-bottom with temporary invulnerability (Wave continues!)
                StartCoroutine(RespawnRoutine());
            }
            else
            {
                currentLives = 0;
                OnLivesChanged?.Invoke(0, maxLives);
                Die();
            }
        }

        private IEnumerator RespawnRoutine()
        {
            isInvulnerable = true;

            if (playerCollider != null) playerCollider.enabled = false;
            if (playerController != null) playerController.enabled = false;
            if (spriteRenderer != null) spriteRenderer.enabled = false;

            yield return new WaitForSeconds(0.7f);

            // Move to center bottom
            transform.position = new Vector3(0f, PlayfieldBounds.Bottom + 1.2f, 0f);
            Starfall.VFX.VFXKit.Ring(transform.position, new Color(0.4f, 0.9f, 1f, 0.9f), 3f, 0.4f, 0.45f);

            // Refill shield and hull
            currentShield = maxShield;
            currentHull = maxHull;
            OnShieldChanged?.Invoke(currentShield, maxShield);
            OnHullChanged?.Invoke(currentHull, maxHull);

            if (spriteRenderer != null) spriteRenderer.enabled = true;
            if (playerCollider != null) playerCollider.enabled = true;
            if (playerController != null) playerController.enabled = true;

            // Invulnerability flashing for 2.5 seconds
            yield return StartCoroutine(InvulnerabilityRoutine(2.5f));
        }

        private IEnumerator InvulnerabilityRoutine(float duration = -1f)
        {
            float targetDuration = duration > 0f ? duration : invulnerabilityDuration;
            isInvulnerable = true;
            float elapsed = 0f;
            float flashInterval = 0.1f;

            while (elapsed < targetDuration)
            {
                if (spriteRenderer != null)
                {
                    spriteRenderer.color = hitColor;
                    yield return new WaitForSeconds(flashInterval * 0.5f);
                    spriteRenderer.color = originalColor;
                    yield return new WaitForSeconds(flashInterval * 0.5f);
                }
                else
                {
                    yield return new WaitForSeconds(flashInterval);
                }
                elapsed += flashInterval;
            }

            if (spriteRenderer != null)
            {
                spriteRenderer.color = originalColor;
            }
            isInvulnerable = false;
        }

        private void Die()
        {
            OnPlayerDeath?.Invoke();
            if (GameManager.Instance != null)
            {
                GameManager.Instance.OnPlayerDied();
            }

            if (ObjectPool.Instance != null && !string.IsNullOrEmpty(explosionPoolKey))
            {
                ObjectPool.Instance.Spawn(explosionPoolKey, transform.position, Quaternion.identity);
            }

            gameObject.SetActive(false);
        }

        private void EnsureDeflectorShield()
        {
            if (deflectorShield != null) return;
            deflectorShield = GetComponentInChildren<Starfall.VFX.EnergyDeflectorShield>();
            if (deflectorShield == null)
            {
                var shieldGo = new GameObject("EnergyDeflectorShield");
                shieldGo.transform.SetParent(transform, false);
                shieldGo.transform.localPosition = Vector3.zero;
                shieldGo.transform.localRotation = Quaternion.identity;
                deflectorShield = shieldGo.AddComponent<Starfall.VFX.EnergyDeflectorShield>();
            }
        }

        private void UpdateShieldBubble()
        {
            EnsureDeflectorShield();
            if (deflectorShield != null)
            {
                deflectorShield.SetShieldStats(currentShield, maxShield, ShieldLayers);
                deflectorShield.SetVisible((spriteRenderer == null || spriteRenderer.enabled) && currentShield > 0);
            }
        }

    }
}
