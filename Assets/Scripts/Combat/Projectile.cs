using UnityEngine;
using Starfall.Core;
using Starfall.VFX;

namespace Starfall.Combat
{
    /// <summary>
    /// Pooled projectile for both player and enemy weaponry.
    /// Enemy bullets get a soft glow halo and draw above ships so they stay readable,
    /// slow down during Time Warp, and can be configured as bursting mines (Bomber role).
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class Projectile : MonoBehaviour, IPooledObject
    {
        /// <summary>Hostile bullets currently alive. Used to cap on-screen bullet density.</summary>
        public static int ActiveEnemyCount { get; private set; }

        public static readonly System.Collections.Generic.List<Projectile> ActiveEnemyProjectiles = 
            new System.Collections.Generic.List<Projectile>(128);

        private const int EnemyBulletSortingOrder = 30;

        [Header("Settings")]
        [SerializeField] private float speed = 16f;
        [SerializeField] private int damage = 1;
        [SerializeField] private float lifetime = 3.5f;
        [SerializeField] private LayerMask targetLayer;
        [SerializeField] private bool isPlayerProjectile = true;

        [Header("Enemy Bullet Visuals")]
        [Tooltip("Tick if the sprite art is drawn nose-DOWN (e.g. 'Boss Missiles.png'). Untick for nose-UP art.")]
        [SerializeField] private bool spriteArtPointsDown = false;
        [Tooltip("On-screen length of hostile bullets in world units, whatever the sprite's native size.")]
        [SerializeField] private float enemyVisualLength = 0.7f;
        [Tooltip("Hitbox of hostile bullets in world units (kept independent of the sprite size).")]
        [SerializeField] private Vector2 enemyHitbox = new Vector2(0.16f, 0.42f);

        private Vector2 moveDirection = Vector2.up;
        private float lifeTimer;
        private bool countedAsEnemy;
        private SpriteRenderer spriteRenderer;
        private SpriteRenderer glow;
        private int authoredSortingOrder;
        private Vector3 authoredScale;
        private Vector3 enemyScale = Vector3.one;
        private BoxCollider2D boxCollider;
        private CircleCollider2D circleCollider;
        private Vector2 authoredBoxSize;
        private float authoredCircleRadius;

        // Mine mode
        private bool isMine;
        private float mineFuse;
        private int mineBurstCount;
        private float mineBurstSpeed;

        public bool IsPlayerProjectile => isPlayerProjectile;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer != null) authoredSortingOrder = spriteRenderer.sortingOrder;
            authoredScale = transform.localScale;
            boxCollider = GetComponent<BoxCollider2D>();
            circleCollider = GetComponent<CircleCollider2D>();
            if (boxCollider != null) authoredBoxSize = boxCollider.size;
            if (circleCollider != null) authoredCircleRadius = circleCollider.radius;
        }

        /// <summary>
        /// Hostile bullets are normalised to a fixed on-screen length so large source art
        /// (1024x1536 missile at 360 PPU = 4.3 units tall) reads as a bullet, not a rocket,
        /// and the hitbox always matches what the player sees.
        /// </summary>
        private void ApplyEnemyVisualSize()
        {
            float nativeLength = 0f;
            if (spriteRenderer != null && spriteRenderer.sprite != null)
                nativeLength = spriteRenderer.sprite.bounds.size.y;

            float scale = nativeLength > 0.0001f ? enemyVisualLength / nativeLength : authoredScale.x;
            enemyScale = new Vector3(scale, scale, 1f);
            if (transform.localScale != enemyScale) transform.localScale = enemyScale;

            // Resizing a collider rebuilds its physics shape, so only do it once per pooled bullet.
            if (sizedAsEnemy) return;
            sizedAsEnemy = true;
            float inv = 1f / Mathf.Max(0.0001f, scale);
            if (boxCollider != null) boxCollider.size = enemyHitbox * inv;
            if (circleCollider != null) circleCollider.radius = enemyHitbox.x * 0.5f * inv;
        }

        private bool sizedAsEnemy;

        private void RestoreAuthoredSize()
        {
            if (transform.localScale != authoredScale) transform.localScale = authoredScale;
            if (!sizedAsEnemy) return;
            sizedAsEnemy = false;
            if (boxCollider != null) boxCollider.size = authoredBoxSize;
            if (circleCollider != null) circleCollider.radius = authoredCircleRadius;
        }

        public void Initialize(Vector2 direction, int damageAmount, LayerMask targets, bool playerOwned = true, float customSpeed = -1f)
        {
            moveDirection = direction.normalized;
            damage = damageAmount;
            targetLayer = targets;
            isPlayerProjectile = playerOwned;
            if (customSpeed > 0f) speed = customSpeed;

            // Rotate the art so its nose leads: nose-up art is offset by -90, nose-down art by +90.
            float offset = spriteArtPointsDown ? 90f : -90f;
            float angle = Mathf.Atan2(moveDirection.y, moveDirection.x) * Mathf.Rad2Deg + offset;
            transform.rotation = Quaternion.Euler(0f, 0f, angle);

            if (playerOwned) RestoreAuthoredSize();
            else ApplyEnemyVisualSize();

            if (!playerOwned && !countedAsEnemy)
            {
                countedAsEnemy = true;
                ActiveEnemyCount++;
                ActiveEnemyProjectiles.Add(this);
            }

            if (spriteRenderer != null)
                spriteRenderer.sortingOrder = playerOwned ? authoredSortingOrder : EnemyBulletSortingOrder;

            if (!playerOwned)
            {
                if (glow == null) glow = VFXKit.AttachGlow(transform, new Color(1f, 0.35f, 0.3f, 0.55f), 0.55f, EnemyBulletSortingOrder - 1, "BulletGlow");
                glow.enabled = true;
                glow.color = new Color(1f, 0.35f, 0.3f, 0.55f);
                glow.transform.localScale = Vector3.one * (0.5f / Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x)));
            }
            else if (glow != null)
            {
                glow.enabled = false;
            }
        }

        /// <summary>Turns this bullet into a slow mine that bursts into a ring after 'fuse' seconds.</summary>
        public void ConfigureMine(float fuse, int burstCount, float burstSpeed)
        {
            isMine = true;
            mineFuse = fuse;
            mineBurstCount = Mathf.Max(3, burstCount);
            mineBurstSpeed = burstSpeed;
            lifeTimer = fuse + 1f;
            transform.localScale = enemyScale * 1.5f;
            if (glow != null)
            {
                glow.color = new Color(0.8f, 0.4f, 1f, 0.7f);
                glow.transform.localScale = Vector3.one * (1.1f / Mathf.Max(0.0001f, Mathf.Abs(transform.lossyScale.x)));
            }
        }

        public void OnSpawn()
        {
            lifeTimer = lifetime;
            isMine = false;
            transform.localScale = authoredScale;
        }

        public void OnDespawn()
        {
            if (countedAsEnemy)
            {
                countedAsEnemy = false;
                ActiveEnemyCount = Mathf.Max(0, ActiveEnemyCount - 1);
                ActiveEnemyProjectiles.Remove(this);
            }
            isMine = false;
        }

        private void OnDisable()
        {
            // Covers scene unloads where OnDespawn is not called.
            if (countedAsEnemy)
            {
                countedAsEnemy = false;
                ActiveEnemyCount = Mathf.Max(0, ActiveEnemyCount - 1);
                ActiveEnemyProjectiles.Remove(this);
            }
        }

        private void Update()
        {
            using var __perf = Starfall.Core.PerfMarkers.ProjectileUpdate.Auto();
            float dt = isPlayerProjectile ? Time.deltaTime : TimeWarpManager.EnemyDeltaTime;
            transform.Translate(moveDirection * speed * dt, Space.World);

            if (isMine)
            {
                mineFuse -= dt;
                if (glow != null)
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.Lerp(8f, 30f, 1f - Mathf.Clamp01(mineFuse)));
                    glow.color = new Color(0.85f, 0.4f, 1f, 0.35f + 0.5f * pulse);
                }
                if (mineFuse <= 0f)
                {
                    BurstMine();
                    return;
                }
            }

            lifeTimer -= dt;
            if (lifeTimer <= 0f || !PlayfieldBounds.IsOnScreen(transform.position, 1.5f))
                Despawn();

        }

        private void BurstMine()
        {
            Vector3 pos = transform.position;
            isMine = false;
            VFXKit.Ring(pos, new Color(0.85f, 0.45f, 1f, 0.85f), 0.3f, 2.2f, 0.3f);
            Starfall.Audio.AudioManager.Instance?.PlayEnemyLaser();

            if (ObjectPool.Instance != null)
            {
                float offset = Random.Range(0f, 360f / mineBurstCount);
                for (int i = 0; i < mineBurstCount; i++)
                {
                    float a = (offset + i * 360f / mineBurstCount) * Mathf.Deg2Rad;
                    var go = ObjectPool.Instance.Spawn("EnemyProjectile", pos, Quaternion.identity);
                    if (go != null && go.TryGetComponent<Projectile>(out var p))
                        p.Initialize(new Vector2(Mathf.Cos(a), Mathf.Sin(a)), 1, targetLayer, false, mineBurstSpeed);
                }
            }
            Despawn();
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            bool hitTarget = (targetLayer.value & (1 << collision.gameObject.layer)) != 0;
            if (!isPlayerProjectile && (collision.gameObject.layer == 6 || collision.CompareTag("Player")))
                hitTarget = true;

            if (!hitTarget) return;

            Vector3 hitPoint = collision.ClosestPoint(transform.position);
            SpawnHitImpact(collision, hitPoint);

            var damageable = collision.GetComponentInParent<IDamageable>();
            if (damageable != null) damageable.TakeDamage(damage, hitPoint);

            if (isMine) { BurstMine(); return; }
            Despawn();
        }

        private void SpawnHitImpact(Collider2D collision, Vector3 hitPoint)
        {
            if (ObjectPool.Instance == null) return;

            string vfxKey = "HitSparkMetalVFX";
            string colName = collision.gameObject.name;

            bool isRock = collision.GetComponentInParent<Starfall.Enemies.AsteroidHazard>() != null || colName.Contains("Asteroid");
            bool isAlien = collision.GetComponentInParent<Starfall.Enemies.HiveQueenBoss>() != null || colName.Contains("Spore") || colName.Contains("Queen");

            if (isRock)
                vfxKey = "HitSparkRockVFX";
            else if (isAlien)
                vfxKey = "HitSparkAlienVFX";

            // Sparks bounce outwards from the impact surface.
            float angle = Mathf.Atan2(-moveDirection.y, -moveDirection.x) * Mathf.Rad2Deg;
            ObjectPool.Instance.Spawn(vfxKey, hitPoint, Quaternion.Euler(0f, 0f, angle));

            // Dynamic micro-ring & hit feedback for player projectiles
            if (isPlayerProjectile)
            {
                Color ringColor;
                if (isRock)
                    ringColor = new Color(1f, 0.65f, 0.25f, 0.85f);
                else if (isAlien)
                    ringColor = new Color(0.95f, 0.35f, 1f, 0.9f);
                else
                    ringColor = new Color(0.35f, 0.85f, 1f, 0.9f);

                VFXKit.Ring(hitPoint, ringColor, 0.08f, 0.5f, 0.12f);

                if (damage > 1)
                {
                    VFXKit.Glow(hitPoint, Color.white, 0.42f, 0.08f);
                    CameraShake.Instance?.Shake(0.04f, 0.06f);
                    Starfall.Audio.AudioManager.Instance?.PlayHit(true);
                }
                else if (isRock)
                {
                    Starfall.Audio.AudioManager.Instance?.PlayHit(false);
                }
            }
        }

        public void Despawn()
        {
            if (!gameObject.activeSelf) return;
            if (ObjectPool.Instance != null) ObjectPool.Instance.Despawn(gameObject);
            else Destroy(gameObject);
        }
    }
}
