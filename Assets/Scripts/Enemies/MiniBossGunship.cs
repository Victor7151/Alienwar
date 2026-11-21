using System.Collections;
using UnityEngine;
using Starfall.Core;
using Starfall.Combat;
using Starfall.VFX;

namespace Starfall.Enemies
{
    /// <summary>
    /// Mid-level elite mini-boss (Wave 5).
    /// Heavy armored gunship that strafes laterally, firing alternating heavy blasters
    /// and periodic 3-way spreads. Must be defeated to proceed to the second half of the level.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class MiniBossGunship : MonoBehaviour, IDamageable, IPooledObject
    {
        [Header("Stats")]
        [SerializeField] private string bossDisplayName = "Gunship Corsair";
        [SerializeField] private int maxHealth = 600;
        [SerializeField] private int scoreReward = 3500;
        [SerializeField] private float entrySpeed = 3f;
        [SerializeField] private float combatY = 2.2f;
        [SerializeField] private float strafeSpeed = 2.4f;
        [SerializeField] private float strafeWidth = 3.2f;

        [Header("Weapons")]
        [SerializeField] private string projectilePoolKey = "EnemyProjectile";
        [SerializeField] private int projectileDamage = 1;
        [SerializeField] private float blasterInterval = 0.85f;
        [SerializeField] private float spreadInterval = 2.8f;
        [SerializeField] private float projectileSpeed = 6.8f;

        [Header("Drops & VFX")]
        [SerializeField] private string weaponDropKey = "PowerUp_Weapon";
        [SerializeField] private string bombDropKey = "PowerUp_Bomb";
        [SerializeField] private string explosionPoolKey = "ExplosionVFX";
        [SerializeField] private SpriteRenderer spriteRenderer;

        private int currentHealth;
        private bool isDead = false;
        private bool inPosition = false;
        private bool isPhase2 = false;
        private float blasterTimer;
        private float spreadTimer;
        private float strafeTimer;
        private float sectorSpecialTimer;
        private bool alternateLeft = false;
        private Vector3 startPos;
        private LayerMask targetLayer;
        private int baseMaxHealth = -1;
        private float baseBlasterInterval;
        private float baseSpreadInterval;
        private float centerX;
        private float activeStrafeWidth;
        private int level = 1;

        public bool IsDead => isDead;

        public void SetDisplayName(string name)
        {
            if (!string.IsNullOrEmpty(name))
                bossDisplayName = name;
        }

        private void Awake()
        {
            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            targetLayer = LayerMask.GetMask("Player");
        }

        /// <summary>
        /// Scales the gunship for the current sector and gives each gunship its own strafe lane
        /// so twin / triple encounters never overlap each other.
        /// </summary>
        public void ApplyDifficulty(float healthMultiplier, int sectorLevel, float laneCenterX, int gunshipCount)
        {
            if (baseMaxHealth < 0) CacheBase();
            level = Mathf.Max(1, sectorLevel);
            combatY = PlayfieldBounds.FormationTop - 0.55f;
            maxHealth = Mathf.Max(20, Mathf.RoundToInt(baseMaxHealth * healthMultiplier));
            currentHealth = maxHealth;
            blasterInterval = baseBlasterInterval / (1f + (level - 1) * 0.12f);
            spreadInterval = baseSpreadInterval / (1f + (level - 1) * 0.1f);

            float half = PlayfieldBounds.PlayHalfWidth;
            centerX = laneCenterX;
            float laneHalf = gunshipCount <= 1 ? half - 1.2f : (half * 2f / gunshipCount) * 0.5f - 0.9f;
            activeStrafeWidth = Mathf.Clamp(laneHalf, 0.4f, strafeWidth);

            if (spriteRenderer != null)
            {
                spriteRenderer.color = level switch
                {
                    2 => new Color(0.92f, 0.78f, 1f), // Asteroid Belt: violet/bronze cast
                    3 => new Color(0.65f, 1f, 0.9f),  // Deep Nebula: bio-teal cast
                    4 => new Color(1f, 0.65f, 0.5f),  // War-Torn: scorched ember cast
                    _ => Color.white
                };
            }

            BossEvents.TriggerBossSpawned(bossDisplayName, currentHealth, maxHealth);
        }

        private void CacheBase()
        {
            baseMaxHealth = maxHealth;
            baseBlasterInterval = blasterInterval;
            baseSpreadInterval = spreadInterval;
        }

        public void OnSpawn()
        {
            if (baseMaxHealth < 0) CacheBase();
            maxHealth = baseMaxHealth;
            blasterInterval = baseBlasterInterval;
            spreadInterval = baseSpreadInterval;
            centerX = transform.position.x;
            combatY = PlayfieldBounds.FormationTop - 0.55f;
            activeStrafeWidth = Mathf.Min(strafeWidth, Mathf.Max(0.4f, PlayfieldBounds.PlayHalfWidth - 1.2f));
            level = 1;
            isDead = false;
            inPosition = false;
            isPhase2 = false;
            currentHealth = maxHealth;
            blasterTimer = blasterInterval * 0.5f;
            spreadTimer = spreadInterval * 0.8f;
            strafeTimer = 0f;
            sectorSpecialTimer = 2.0f;
            startPos = transform.position;

            BossEvents.TriggerBossSpawned(bossDisplayName, currentHealth, maxHealth);
            Debug.Log($"[MiniBossGunship] Spawned Wave 5 Mini-Boss ({bossDisplayName}) with {maxHealth} HP!");
        }

        public void OnDespawn()
        {
            StopAllCoroutines();
        }

        public void TakeDamage(int damage, Vector2 hitPoint)
        {
            if (isDead) return;

            // Maximum damage clamp per hit to prevent bomb or burst cheese
            int clampedDamage = Mathf.Min(damage, 20);

            Starfall.VFX.FloatingDamageText.Spawn(hitPoint, clampedDamage);
            currentHealth = Mathf.Max(0, currentHealth - clampedDamage);
            BossEvents.TriggerBossHealthChanged(currentHealth, maxHealth);

            if (CameraShake.Instance != null)
                CameraShake.Instance.Shake(0.06f, 0.08f);

            StartCoroutine(FlashDamageRoutine());

            // Phase 2 transition trigger at 55% HP
            if (!isPhase2 && currentHealth <= Mathf.RoundToInt(maxHealth * 0.55f))
            {
                isPhase2 = true;
                if (CameraShake.Instance != null)
                    CameraShake.Instance.Shake(0.28f, 0.35f);
                if (level == 4)
                {
                    Starfall.VFX.VFXKit.Ring(transform.position, new Color(1f, 0.3f, 0.1f, 0.9f), 0.5f, 3.5f, 0.4f);
                    Starfall.Audio.AudioManager.Instance?.PlayExplosion(true);
                }
                Debug.Log($"[MiniBossGunship] {bossDisplayName} ENRAGED – Phase 2 Activated!");
            }

            if (currentHealth <= 0)
            {
                StartCoroutine(DieRoutine());
            }
        }

        private IEnumerator FlashDamageRoutine()
        {
            if (spriteRenderer != null)
            {
                spriteRenderer.color = isPhase2 ? new Color(1f, 0.4f, 0.2f) : Color.red;
                yield return new WaitForSeconds(0.06f);
                spriteRenderer.color = isPhase2 ? new Color(1f, 0.8f, 0.7f) : Color.white;
            }
        }

        private void Update()
        {
            using var __perf = Starfall.Core.PerfMarkers.BossUpdate.Auto();
            if (isDead) return;

            if (!inPosition)
            {
                // Descend into upper combat position
                float newY = Mathf.MoveTowards(transform.position.y, combatY, entrySpeed * Time.deltaTime);
                transform.position = new Vector3(centerX, newY, 0f);
                if (Mathf.Abs(transform.position.y - combatY) < 0.05f)
                {
                    inPosition = true;
                    startPos = transform.position;
                }
                return;
            }

            // Lateral combat strafe around this gunship's own lane (faster in Phase 2)
            float dt = Starfall.Combat.TimeWarpManager.EnemyDeltaTime;
            float activeStrafeSpeed = isPhase2 ? strafeSpeed * 1.35f : strafeSpeed;
            strafeTimer += dt * activeStrafeSpeed;
            float newX = centerX + Mathf.Sin(strafeTimer) * activeStrafeWidth;
            transform.position = new Vector3(newX, combatY, 0f);

            // Blaster firing (cadence tightens in Phase 2)
            float activeBlasterRate = isPhase2 ? blasterInterval * 0.65f : blasterInterval;
            blasterTimer += dt;
            if (blasterTimer >= activeBlasterRate)
            {
                blasterTimer = 0f;
                FireBlaster();
            }

            // Spread attack (5-way in Phase 2, 3-way in Phase 1)
            float activeSpreadRate = isPhase2 ? spreadInterval * 0.8f : spreadInterval;
            spreadTimer += dt;
            if (spreadTimer >= activeSpreadRate)
            {
                spreadTimer = 0f;
                FireSpread();
            }

            // Sector-specific signature special action
            sectorSpecialTimer += dt;
            float specialThreshold = level switch
            {
                2 => isPhase2 ? 3.4f : 4.8f,
                3 => isPhase2 ? 3.6f : 5.0f,
                4 => isPhase2 ? 2.8f : 4.0f,
                _ => 9999f
            };
            if (sectorSpecialTimer >= specialThreshold)
            {
                sectorSpecialTimer = 0f;
                TriggerSectorSpecial();
            }
        }

        private void TriggerSectorSpecial()
        {
            switch (level)
            {
                case 2:
                    LaunchCargoRock();
                    break;
                case 3:
                    StartCoroutine(NebulaPhaseShiftRoutine());
                    break;
                case 4:
                    FireScrapFlak();
                    break;
            }
        }

        private void LaunchCargoRock()
        {
            if (ObjectPool.Instance == null) return;
            string key = (isPhase2 || Random.value < 0.45f) ? "AsteroidVolatile" : "AsteroidHazard";
            var rock = ObjectPool.Instance.Spawn(key, transform.position + new Vector3(0f, -0.6f, 0f), Quaternion.identity);
            if (rock != null)
            {
                float scale = Random.Range(0.7f, 1.15f);
                rock.transform.localScale = Vector3.one * scale;
                if (rock.TryGetComponent<AsteroidHazard>(out var hazard))
                {
                    Vector2 drift = new Vector2(Random.Range(-0.35f, 0.35f), -1f).normalized;
                    hazard.Initialize(4.2f, drift, 3, Random.Range(-90f, 90f));
                }
                Starfall.VFX.VFXKit.Popup(transform.position + Vector3.down * 0.5f, "CARGO DUMP!", new Color(1f, 0.6f, 0.2f), 3.0f);
                Starfall.Audio.AudioManager.Instance?.PlayHazardAlert();
            }
        }

        private IEnumerator NebulaPhaseShiftRoutine()
        {
            if (spriteRenderer != null) spriteRenderer.color = new Color(0.2f, 1f, 0.8f, 0.25f);
            Starfall.VFX.VFXKit.Ring(transform.position, new Color(0.2f, 1f, 0.85f, 0.8f), 0.4f, 2.0f, 0.3f);
            yield return new WaitForSeconds(0.45f);
            if (isDead) yield break;

            strafeTimer += Mathf.PI;
            transform.position = new Vector3(centerX + Mathf.Sin(strafeTimer) * activeStrafeWidth, combatY, 0f);
            if (spriteRenderer != null) spriteRenderer.color = isPhase2 ? new Color(1f, 0.8f, 0.7f) : new Color(0.65f, 1f, 0.9f);
            Starfall.VFX.VFXKit.Ring(transform.position, new Color(0.2f, 1f, 0.85f, 0.9f), 0.5f, 2.5f, 0.35f);
            Starfall.Audio.AudioManager.Instance?.PlayDiveBomberSwoop();

            Vector3 center = transform.position + Vector3.down * 0.4f;
            for (int i = 0; i < 8; i++)
            {
                float ang = i * 45f * Mathf.Deg2Rad;
                SpawnBullet(center, new Vector2(Mathf.Cos(ang), Mathf.Sin(ang)));
            }
        }

        private void FireScrapFlak()
        {
            Vector3 center = transform.position + Vector3.down * 0.5f;
            Starfall.VFX.VFXKit.Ring(center, new Color(1f, 0.35f, 0.15f, 0.9f), 0.3f, 1.8f, 0.25f);
            Starfall.Audio.AudioManager.Instance?.PlayExplosion();
            for (int i = -2; i <= 2; i++)
            {
                Vector2 dir = new Vector2(i * 0.28f, -1f).normalized;
                SpawnBullet(center, dir);
            }
        }

        private void FireBlaster()
        {
            Starfall.Audio.AudioManager.Instance?.PlayEnemyLaser();

            float offsetX = alternateLeft ? -0.45f : 0.45f;
            alternateLeft = !alternateLeft;
            Vector3 spawnPos = transform.position + new Vector3(offsetX, -0.4f, 0f);

            SpawnBullet(spawnPos, Vector2.down);
        }

        private void FireSpread()
        {
            Starfall.Audio.AudioManager.Instance?.PlayEnemyLaser();

            Vector3 spawnCenter = transform.position + new Vector3(0f, -0.45f, 0f);
            Starfall.VFX.VFXKit.Glow(spawnCenter, new Color(1f, 0.5f, 0.2f, 0.8f), 1.2f, 0.2f);
            Vector2[] dirs;

            if (isPhase2)
            {
                // 5-way spread in Phase 2
                dirs = new Vector2[]
                {
                    Vector2.down,
                    new Vector2(-0.25f, -1f).normalized,
                    new Vector2( 0.25f, -1f).normalized,
                    new Vector2(-0.52f, -0.85f).normalized,
                    new Vector2( 0.52f, -0.85f).normalized
                };
            }
            else
            {
                // 3-way spread in Phase 1
                dirs = new Vector2[]
                {
                    Vector2.down,
                    new Vector2(-0.35f, -1f).normalized,
                    new Vector2( 0.35f, -1f).normalized
                };
            }

            foreach (var dir in dirs)
            {
                SpawnBullet(spawnCenter, dir);
            }
        }

        private void SpawnBullet(Vector3 pos, Vector2 direction)
        {
            if (ObjectPool.Instance == null) return;
            var go = ObjectPool.Instance.Spawn(projectilePoolKey, pos, Quaternion.identity);
            if (go != null && go.TryGetComponent<Projectile>(out var proj))
            {
                proj.Initialize(direction, projectileDamage, targetLayer, false, projectileSpeed);
            }
        }

        private IEnumerator DieRoutine()
        {
            isDead = true;

            // Explosion burst
            for (int i = 0; i < 3; i++)
            {
                Vector3 offset = new Vector3(Random.Range(-0.8f, 0.8f), Random.Range(-0.4f, 0.4f), 0f);
                if (ObjectPool.Instance != null)
                    ObjectPool.Instance.Spawn(explosionPoolKey, transform.position + offset, Quaternion.identity);

                Starfall.Audio.AudioManager.Instance?.PlayExplosion(false);
                if (CameraShake.Instance != null)
                    CameraShake.Instance.Shake(0.2f, 0.15f);

                yield return new WaitForSeconds(0.18f);
            }

            // Final gunship detonation
            if (ObjectPool.Instance != null)
            {
                ObjectPool.Instance.Spawn("BossExplosionVFX", transform.position, Quaternion.identity);
            }
            Starfall.Audio.AudioManager.Instance?.PlayLargeExplosion();
            if (CameraShake.Instance != null)
                CameraShake.Instance.Shake(0.45f, 0.35f);

            if (GameManager.Instance != null)
                GameManager.Instance.AddScore(scoreReward);

            // Guaranteed drops
            if (ObjectPool.Instance != null)
            {
                ObjectPool.Instance.Spawn(weaponDropKey, transform.position + Vector3.left * 0.4f, Quaternion.identity);
                if (Random.value < 0.5f)
                    ObjectPool.Instance.Spawn(bombDropKey, transform.position + Vector3.right * 0.4f, Quaternion.identity);
            }

            BossEvents.TriggerBossDefeated(bossDisplayName);

            yield return new WaitForSeconds(0.25f);
            if (ObjectPool.Instance != null)
                ObjectPool.Instance.Despawn(gameObject);
            else
                Destroy(gameObject);
        }
    }
}
