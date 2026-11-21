using UnityEngine;
using System.Collections;
using Starfall.Core;
using Starfall.Combat;

namespace Starfall.Enemies
{
    /// <summary>
    /// AsteroidHazard – a destructible space rock that drifts across the screen.
    /// Inspired by the asteroid wave hazards in Chicken Invaders.
    /// - 3 hits to destroy (shows visual damage stages).
    /// - Damages player on contact.
    /// - Drops a Score bonus collectible when destroyed.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class AsteroidHazard : MonoBehaviour, IDamageable, IPooledObject
    {
        [Header("Stats")]
        [SerializeField] private int maxHealth = 3;
        [SerializeField] private float speed = 3.5f;
        [SerializeField] private float rotationSpeed = 45f;
        [SerializeField] private int contactDamage = 1;
        [SerializeField] private int scoreValue = 250;

        [Header("Visuals")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Color damagedColor1 = new Color(0.9f, 0.6f, 0.2f); // Orange crack
        [SerializeField] private Color damagedColor2 = new Color(0.8f, 0.3f, 0.1f); // Red-hot crack

        [Header("VFX")]
        [SerializeField] private string explosionPoolKey = "ExplosionVFX";

        [Header("Drop")]
        [SerializeField] private string scoreDropKey = "PowerUp_Score";

        [Header("Advanced Mechanics (2D Asset Guide)")]
        [SerializeField] private bool splitsOnDestroy = false;
        [SerializeField] private string fragmentPoolKey = "AsteroidSmallFragment";
        [SerializeField] private int fragmentCount = 3;
        [SerializeField] private bool isVolatileExplosive = false;
        [SerializeField] private float explosionRadius = 2.5f;
        [SerializeField] private int explosiveDamage = 30;

        private int currentHealth;
        private Color originalColor;
        private Vector2 driftDirection = Vector2.down;

        public bool IsDead => currentHealth <= 0;
        public static event System.Action<AsteroidHazard> OnAsteroidDestroyed;

        private void Awake()
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
                originalColor = spriteRenderer.color;
        }

        public void Initialize(float customSpeed, Vector2 direction, int health = 3, float rotSpeed = 45f)
        {
            speed = customSpeed;
            driftDirection = direction.normalized;
            maxHealth = health;
            currentHealth = health;
            rotationSpeed = rotSpeed;
            if (spriteRenderer != null)
                spriteRenderer.color = originalColor;
        }

        public void OnSpawn()
        {
            currentHealth = maxHealth;
            driftDirection = Vector2.down;
            if (spriteRenderer != null)
                spriteRenderer.color = originalColor;
        }

        public void OnDespawn() { }

        private void Update()
        {
            using var __perf = Starfall.Core.PerfMarkers.HazardUpdate.Auto();
            // Drift along direction across the screen
            transform.position += (Vector3)driftDirection * speed * Time.deltaTime;
            transform.Rotate(0f, 0f, rotationSpeed * Time.deltaTime);

            // Despawn when off-screen bounds
            if (transform.position.y < -9.5f || transform.position.y > 10.5f || Mathf.Abs(transform.position.x) > 12f)
                DespawnSelf();
        }

        private void OnTriggerEnter2D(Collider2D other)
        {
            // Damage player on contact
            if (other.CompareTag("Player") || other.GetComponentInParent<Starfall.Player.PlayerHealth>() != null)
            {
                var health = other.GetComponentInParent<IDamageable>();
                health?.TakeDamage(contactDamage, transform.position);
                TakeDamage(999, transform.position); // Asteroid also destroyed on player impact
            }
        }

        public void TakeDamage(int damage) => TakeDamage(damage, (Vector2)transform.position);

        public void TakeDamage(int damage, Vector2 hitPoint)
        {
            if (IsDead) return;
            currentHealth -= damage;

            // Show damage cracks via color stages
            if (spriteRenderer != null)
            {
                if (currentHealth == 2)
                    spriteRenderer.color = damagedColor1;
                else if (currentHealth == 1)
                    spriteRenderer.color = damagedColor2;
            }

            if (currentHealth <= 0)
                DestroyAsteroid();
        }

        private void DestroyAsteroid()
        {
            OnAsteroidDestroyed?.Invoke(this);
            if (GameManager.Instance != null)
                GameManager.Instance.AddScore(scoreValue);

            // Explosion VFX
            if (ObjectPool.Instance != null && !string.IsNullOrEmpty(explosionPoolKey))
                ObjectPool.Instance.Spawn(explosionPoolKey, transform.position, Quaternion.identity);

            // 1. Splitting Mechanic (Chicken Invaders style Large -> Small fragments)
            if (splitsOnDestroy && ObjectPool.Instance != null && !string.IsNullOrEmpty(fragmentPoolKey))
            {
                float angleStep = 360f / Mathf.Max(1, fragmentCount);
                for (int i = 0; i < fragmentCount; i++)
                {
                    float angle = (i * angleStep + Random.Range(-15f, 15f)) * Mathf.Deg2Rad;
                    Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)).normalized;
                    var fragGo = ObjectPool.Instance.Spawn(fragmentPoolKey, (Vector2)transform.position + dir * 0.4f, Quaternion.identity);
                    if (fragGo != null)
                    {
                        var fragHazard = fragGo.GetComponent<AsteroidHazard>();
                        if (fragHazard != null)
                        {
                            fragHazard.Initialize(speed * Random.Range(1.2f, 1.8f), dir, health: 1, rotSpeed: Random.Range(60f, 120f));
                        }
                    }
                }
            }

            // 2. Volatile AOE Detonation (damages nearby enemies, hazards, and player)
            if (isVolatileExplosive)
            {
                Starfall.VFX.CameraShake.Instance?.Shake(0.35f, 0.25f);
                if (ObjectPool.Instance != null)
                {
                    ObjectPool.Instance.Spawn("ShockwaveVFX", transform.position, Quaternion.identity);
                }
                Starfall.Audio.AudioManager.Instance?.PlayExplosion();

                Collider2D[] hits = Physics2D.OverlapCircleAll(transform.position, explosionRadius);
                foreach (var hit in hits)
                {
                    if (hit.gameObject == gameObject) continue;
                    var damageable = hit.GetComponentInParent<IDamageable>();
                    if (damageable != null && !damageable.IsDead)
                    {
                        damageable.TakeDamage(explosiveDamage, transform.position);
                    }
                }
            }

            // Occasional rare drop (10% chance) so screen is not spammed with bonus items
            if (Random.value < 0.10f && ObjectPool.Instance != null && !string.IsNullOrEmpty(scoreDropKey))
                ObjectPool.Instance.Spawn(scoreDropKey, transform.position, Quaternion.identity);

            DespawnSelf();
        }

        private void DespawnSelf()
        {
            if (ObjectPool.Instance != null)
                ObjectPool.Instance.Despawn(gameObject);
            else
                Destroy(gameObject);
        }
    }
}
