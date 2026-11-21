using System.Collections;
using UnityEngine;
using Starfall.Core;
using Starfall.VFX;

namespace Starfall.Combat
{
    public enum BossBombType
    {
        PlasmaSingularity,
        AcidSpore,
        EmpTorpedo
    }

    /// <summary>
    /// Boss Signature Special Bomb.
    /// Telegraphed heavy ordnance deployed by bosses.
    /// Can be intercepted and shot down by the player for bonus score,
    /// or detonates on timer/proximity into lethal cluster bursts and shockwaves.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class BossBomb : MonoBehaviour, IDamageable, IPooledObject
    {
        [Header("Bomb Configuration")]
        [SerializeField] private BossBombType bombType = BossBombType.PlasmaSingularity;
        [SerializeField] private int maxHealth = 15;
        [SerializeField] private float fuseDuration = 2.4f;
        [SerializeField] private float descentSpeed = 1.8f;
        [SerializeField] private float targetY = 0.5f;
        [SerializeField] private int contactDamage = 1;
        [SerializeField] private int interceptScoreReward = 500;

        [Header("Cluster Detonation")]
        [SerializeField] private int clusterCount = 12;
        [SerializeField] private float clusterSpeed = 6.2f;
        [SerializeField] private string projectilePoolKey = "EnemyProjectile";
        [SerializeField] private string explosionPoolKey = "ExplosionVFX";

        [Header("Visuals & Audio")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [SerializeField] private Color normalColor = Color.white;
        [SerializeField] private Color warningPulseColor = new Color(1f, 0.3f, 0.3f);

        private int currentHealth;
        private bool isDead = false;
        private float fuseTimer = 0f;
        private float initialX;
        private float currentSpeed;
        private Coroutine flashRoutine;
        private LayerMask playerLayer;

        public bool IsDead => isDead;
        public BossBombType Type => bombType;

        private void Awake()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
            playerLayer = LayerMask.GetMask("Player");
        }

        public void Initialize(BossBombType type, Vector3 spawnPosition)
        {
            bombType = type;
            transform.position = spawnPosition;
            initialX = spawnPosition.x;
            currentSpeed = descentSpeed;
            fuseTimer = 0f;
            isDead = false;

            switch (bombType)
            {
                case BossBombType.PlasmaSingularity:
                    maxHealth = 14;
                    fuseDuration = 2.5f;
                    clusterCount = 12;
                    descentSpeed = 1.6f;
                    normalColor = new Color(0.3f, 0.9f, 1f);
                    break;
                case BossBombType.AcidSpore:
                    maxHealth = 8;
                    fuseDuration = 3.0f;
                    clusterCount = 6;
                    descentSpeed = 2.0f;
                    normalColor = new Color(0.7f, 1f, 0.2f);
                    break;
                case BossBombType.EmpTorpedo:
                    maxHealth = 12;
                    fuseDuration = 3.2f;
                    clusterCount = 8;
                    descentSpeed = 2.4f;
                    normalColor = new Color(1f, 0.6f, 0.2f);
                    break;
            }

            currentHealth = maxHealth;
            if (spriteRenderer != null) spriteRenderer.color = normalColor;
        }

        public void OnSpawn()
        {
            isDead = false;
            currentHealth = maxHealth;
            fuseTimer = 0f;
            initialX = transform.position.x;
            currentSpeed = descentSpeed;

            if (spriteRenderer != null)
            {
                spriteRenderer.color = normalColor;
            }
        }

        public void OnDespawn()
        {
            if (flashRoutine != null)
            {
                StopCoroutine(flashRoutine);
                flashRoutine = null;
            }
        }

        private void Update()
        {
            using var __perf = Starfall.Core.PerfMarkers.BossUpdate.Auto();
            if (isDead) return;

            fuseTimer += Time.deltaTime;

            switch (bombType)
            {
                case BossBombType.PlasmaSingularity:
                    UpdatePlasmaSingularity();
                    break;
                case BossBombType.AcidSpore:
                    UpdateAcidSpore();
                    break;
                case BossBombType.EmpTorpedo:
                    UpdateEmpTorpedo();
                    break;
            }

            // Pulsing warning color as fuse nears completion
            float progress = fuseTimer / fuseDuration;
            if (progress > 0.6f && spriteRenderer != null)
            {
                float pulse = Mathf.PingPong(Time.time * 12f, 1f);
                spriteRenderer.color = Color.Lerp(normalColor, warningPulseColor, pulse);
            }

            if (fuseTimer >= fuseDuration)
            {
                Detonate(isIntercepted: false);
            }
        }

        private void UpdatePlasmaSingularity()
        {
            // Descend towards mid-screen targetY
            if (transform.position.y > targetY)
            {
                transform.position += Vector3.down * descentSpeed * Time.deltaTime;
            }

            // Revolving rotation
            transform.Rotate(0f, 0f, 140f * Time.deltaTime);

            // Subtle electric pulsing
            float scalePulse = 1f + Mathf.PingPong(Time.time * 5f, 0.15f);
            transform.localScale = new Vector3(scalePulse, scalePulse, 1f);
        }

        private void UpdateAcidSpore()
        {
            // Organic serpentine drift
            float x = initialX + Mathf.Sin(fuseTimer * 2.8f) * 0.9f;
            float y = transform.position.y - (descentSpeed * Time.deltaTime);
            transform.position = new Vector3(x, y, 0f);

            // Squish and stretch pulsation
            float squish = 1f + Mathf.Sin(fuseTimer * 7f) * 0.12f;
            transform.localScale = new Vector3(1.1f / squish, squish, 1f);

            // Detonate early if reached lower play area
            if (transform.position.y <= -2.8f)
            {
                Detonate(isIntercepted: false);
            }
        }

        private void UpdateEmpTorpedo()
        {
            // Slowly track towards player horizontal position
            float playerX = 0f;
            if (Starfall.Player.PlayerController.Instance != null)
            {
                playerX = Starfall.Player.PlayerController.Instance.transform.position.x;
            }

            float diff = playerX - transform.position.x;
            float stepX = Mathf.Sign(diff) * Mathf.Min(Mathf.Abs(diff), 1.6f * Time.deltaTime);

            currentSpeed += 1.2f * Time.deltaTime; // Accelerate downward
            transform.position += new Vector3(stepX, -currentSpeed * Time.deltaTime, 0f);

            // Tilt toward travel direction
            float angle = Mathf.Clamp(-diff * 8f, -25f, 25f);
            transform.rotation = Quaternion.Euler(0f, 0f, angle);

            // Detonate on proximity
            if (transform.position.y <= -3.6f)
            {
                Detonate(isIntercepted: false);
            }
        }

        public void TakeDamage(int damage, Vector2 hitPoint)
        {
            if (isDead) return;

            currentHealth -= damage;

            if (flashRoutine != null) StopCoroutine(flashRoutine);
            flashRoutine = StartCoroutine(FlashDamageRoutine());

            if (currentHealth <= 0)
            {
                // Player successfully intercepted and destroyed the bomb!
                Detonate(isIntercepted: true);
            }
        }

        private IEnumerator FlashDamageRoutine()
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.white;
                yield return new WaitForSeconds(0.06f);
                if (spriteRenderer != null && !isDead)
                {
                    spriteRenderer.color = normalColor;
                }
            }
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (isDead) return;

            // Direct impact with player ship
            if (collision.CompareTag("Player") || (playerLayer.value & (1 << collision.gameObject.layer)) != 0)
            {
                var damageable = collision.GetComponentInParent<IDamageable>();
                if (damageable != null)
                {
                    damageable.TakeDamage(contactDamage, transform.position);
                }
                Detonate(isIntercepted: false);
            }
        }

        private void Detonate(bool isIntercepted)
        {
            if (isDead) return;
            isDead = true;

            Vector3 pos = transform.position;

            if (isIntercepted)
            {
                // Intercept reward!
                GameManager.Instance?.AddScore(interceptScoreReward);
                Debug.Log($"<color=lime>[BossBomb] INTERCEPTED! +{interceptScoreReward} points rewarded.</color>");

                // Harmless spark explosion
                SpawnExplosion(pos, scaleMult: 1.2f);
                if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.12f, 0.15f);
            }
            else
            {
                // Catastrophic Detonation!
                SpawnExplosion(pos, scaleMult: 2.2f);
                if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.35f, 0.40f);

                SpawnClusterProjectiles(pos);
            }

            Despawn();
        }

        private void SpawnClusterProjectiles(Vector3 center)
        {
            if (ObjectPool.Instance == null) return;

            Starfall.Audio.AudioManager.Instance?.PlayEnemyLaser();

            switch (bombType)
            {
                case BossBombType.PlasmaSingularity:
                    // Full 360-degree radial ring burst
                    float angleStep = 360f / clusterCount;
                    for (int i = 0; i < clusterCount; i++)
                    {
                        float rad = (i * angleStep) * Mathf.Deg2Rad;
                        Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                        SpawnBullet(center, dir, clusterSpeed);
                    }
                    break;

                case BossBombType.AcidSpore:
                    // Downward fan spray (-140 to -40 degrees)
                    float fanStart = -140f;
                    float fanStep = 100f / Mathf.Max(1, clusterCount - 1);
                    for (int i = 0; i < clusterCount; i++)
                    {
                        float rad = (fanStart + (i * fanStep)) * Mathf.Deg2Rad;
                        Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                        SpawnBullet(center, dir, clusterSpeed * 0.9f);
                    }
                    break;

                case BossBombType.EmpTorpedo:
                    // Downward targeted spread + horizontal shock shrapnel
                    Vector2 playerDir = Vector2.down;
                    if (Starfall.Player.PlayerController.Instance != null)
                    {
                        playerDir = ((Vector2)Starfall.Player.PlayerController.Instance.transform.position - (Vector2)center).normalized;
                    }

                    for (int i = 0; i < clusterCount; i++)
                    {
                        float spreadAngle = ((i - (clusterCount - 1) * 0.5f) * 16f) * Mathf.Deg2Rad;
                        Vector2 dir = new Vector2(
                            playerDir.x * Mathf.Cos(spreadAngle) - playerDir.y * Mathf.Sin(spreadAngle),
                            playerDir.x * Mathf.Sin(spreadAngle) + playerDir.y * Mathf.Cos(spreadAngle)
                        ).normalized;

                        SpawnBullet(center, dir, clusterSpeed * 1.1f);
                    }
                    break;
            }
        }

        private void SpawnBullet(Vector3 spawnPos, Vector2 dir, float spd)
        {
            GameObject bullet = ObjectPool.Instance.Spawn(projectilePoolKey, spawnPos, Quaternion.identity);
            if (bullet != null && bullet.TryGetComponent<Projectile>(out var proj))
            {
                proj.Initialize(dir, 1, playerLayer, false, spd);
            }
        }

        private void SpawnExplosion(Vector3 pos, float scaleMult = 1f)
        {
            if (ObjectPool.Instance == null) return;
            GameObject vfx = ObjectPool.Instance.Spawn(explosionPoolKey, pos, Quaternion.identity);
            if (vfx != null)
            {
                vfx.transform.localScale = Vector3.one * scaleMult;
            }
        }

        private void Despawn()
        {
            if (ObjectPool.Instance != null)
            {
                ObjectPool.Instance.Despawn(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
