using System.Collections;
using UnityEngine;
using Starfall.Core;
using Starfall.Combat;
using Starfall.VFX;

namespace Starfall.Enemies
{
    /// <summary>
    /// Level 3 Final Campaign Boss: HIVE QUEEN LEVIATHAN.
    /// Three-phase bio-mechanical flagship featuring bio-plasma spores,
    /// swarm drone hatching, swooping dive charges, and radial bullet spirals.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class HiveQueenBoss : MonoBehaviour, IDamageable, IPooledObject
    {
        [Header("Stats")]
        [SerializeField] private int maxHealth = 2200;
        [SerializeField] private int scoreReward = 15000;
        [SerializeField] private float entrySpeed = 2.4f;
        [SerializeField] private float combatY = 2.2f;

        [Header("Weapons")]
        [SerializeField] private string projectilePoolKey = "EnemyProjectile";
        [SerializeField] private int projectileDamage = 1;
        [SerializeField] private float projectileSpeed = 6.4f;
        [SerializeField] private float sporeFireRate = 1.1f;
        [SerializeField] private float spiralInterval = 4.0f;

        [Header("Swarm Spawns")]
        [SerializeField] private string scoutPoolKey = "ScoutEnemy";
        [SerializeField] private float swarmHatchRate = 5.5f;
        [SerializeField] private int scoutsPerHatch = 3;

        [Header("Signature Bomb Attack")]
        [SerializeField] private string acidSporeBombPoolKey = "BossBomb_AcidSpore";
        [SerializeField] private float sporeBombCooldown = 7.5f;

        [Header("Phase 3 Swoop Dive")]
        [SerializeField] private float diveInterval = 6.0f;
        [SerializeField] private float diveSpeed = 7.5f;

        [Header("VFX & Drops")]
        [SerializeField] private string explosionPoolKey = "ExplosionVFX";
        [SerializeField] private string weaponDropKey = "PowerUp_Weapon";
        [SerializeField] private string bombDropKey = "PowerUp_Bomb";
        [SerializeField] private SpriteRenderer[] renderers;

        private enum BossPhase { Entry, Phase1, Phase2, Phase3, Dead }
        private BossPhase currentPhase = BossPhase.Entry;

        private int currentHealth;
        private bool isDead = false;
        private bool isDiving = false;
        private float sporeTimer;
        private float spiralTimer;
        private float swarmTimer;
        private float sporeBombTimer;
        private float diveTimer;
        private float motionTimer;
        private LayerMask playerLayer;

        public bool IsDead => isDead;

        private void Awake()
        {
            playerLayer = LayerMask.GetMask("Player");
            if (renderers == null || renderers.Length == 0)
                renderers = GetComponentsInChildren<SpriteRenderer>();
        }

        public void OnSpawn()
        {
            isDead = false;
            isDiving = false;
            currentHealth = maxHealth;
            currentPhase = BossPhase.Entry;
            sporeTimer = 0.5f;
            spiralTimer = 3.0f;
            swarmTimer = 3.5f;
            sporeBombTimer = 3.0f;
            diveTimer = 4.0f;
            motionTimer = 0f;

            SetTint(new Color(0.8f, 0.4f, 1f)); // Alien violet
            BossEvents.TriggerBossSpawned("HIVE QUEEN LEVIATHAN", currentHealth, maxHealth);
            Starfall.Audio.AudioManager.Instance?.PlayBossWarningSiren(4);
            Debug.Log("[HiveQueenBoss] Level 3 Boss Encounter Begun!");
        }

        public void OnDespawn()
        {
            StopAllCoroutines();
        }

        public void TakeDamage(int damage, Vector2 hitPoint)
        {
            if (isDead) return;

            int actualDamage = (currentPhase == BossPhase.Phase3) ? damage * 2 : damage;
            Starfall.VFX.FloatingDamageText.Spawn(hitPoint, actualDamage, currentPhase == BossPhase.Phase3);
            currentHealth = Mathf.Max(0, currentHealth - actualDamage);

            BossEvents.TriggerBossHealthChanged(currentHealth, maxHealth);

            if (CameraShake.Instance != null)
                CameraShake.Instance.Shake(0.08f, 0.12f);

            CheckPhaseTransition();

            if (currentHealth <= 0)
            {
                StartCoroutine(DieSequence());
            }
        }

        private void CheckPhaseTransition()
        {
            if (currentPhase == BossPhase.Phase1 && currentHealth <= maxHealth * 0.65f)
            {
                currentPhase = BossPhase.Phase2;
                SetTint(new Color(1f, 0.8f, 0.2f)); // Amber gold
                BossEvents.TriggerBossPhaseChanged("PHASE 2 - SWARM HATCHERY & ACID SPORES!", new Color(1f, 0.8f, 0.2f));
                Starfall.Audio.AudioManager.Instance?.PlayBossRoar();
                if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.35f, 0.4f);
            }
            else if (currentPhase == BossPhase.Phase2 && currentHealth <= maxHealth * 0.30f)
            {
                currentPhase = BossPhase.Phase3;
                SetTint(new Color(1f, 0.15f, 0.3f)); // Blood red
                BossEvents.TriggerBossPhaseChanged("PHASE 3 - APEX SPORE FRENZY!", new Color(1f, 0.15f, 0.3f));
                Starfall.Audio.AudioManager.Instance?.PlayBossRoar();
                if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.5f, 0.55f);
            }
        }

        private void SetTint(Color color)
        {
            if (renderers == null) return;
            foreach (var r in renderers)
            {
                if (r != null) r.color = color;
            }
        }

        private void Update()
        {
            using var __perf = Starfall.Core.PerfMarkers.BossUpdate.Auto();
            if (isDead) return;

            switch (currentPhase)
            {
                case BossPhase.Entry:
                    HandleEntry();
                    break;
                case BossPhase.Phase1:
                    HandleStrafing(amplitude: 2.5f, speed: 1.2f);
                    HandleSporeCannons(rateMult: 1f);
                    break;
                case BossPhase.Phase2:
                    HandleStrafing(amplitude: 3.0f, speed: 1.5f);
                    HandleSporeCannons(rateMult: 1.2f);
                    HandleSwarmHatch();
                    HandleSporeBomb();
                    break;
                case BossPhase.Phase3:
                    if (!isDiving)
                    {
                        HandleStrafing(amplitude: 3.5f, speed: 2.0f);
                        HandleSporeCannons(rateMult: 1.8f);
                        HandleSpiralBurst();
                        HandleSporeBomb();
                        HandleDiveTimer();
                    }
                    break;
            }
        }

        private void HandleEntry()
        {
            if (transform.position.y > combatY)
            {
                transform.position += Vector3.down * entrySpeed * Time.deltaTime;
            }
            else
            {
                transform.position = new Vector3(transform.position.x, combatY, 0f);
                currentPhase = BossPhase.Phase1;
                BossEvents.TriggerBossPhaseChanged("PHASE 1 - BIO-PLASMA SPORES!", new Color(0.8f, 0.4f, 1f));
            }
        }

        private void HandleStrafing(float amplitude, float speed)
        {
            motionTimer += Time.deltaTime * speed;
            float x = Mathf.Sin(motionTimer) * amplitude;
            transform.position = new Vector3(x, combatY, 0f);
        }

        private void HandleSporeCannons(float rateMult)
        {
            sporeTimer += Time.deltaTime * rateMult;
            if (sporeTimer >= sporeFireRate)
            {
                sporeTimer = 0f;
                FireSpores();
            }
        }

        private void FireSpores()
        {
            Starfall.Audio.AudioManager.Instance?.PlayEnemyLaser();

            // Twin spore bursts aimed slightly towards player
            Vector3 leftSpawn = transform.position + new Vector3(-0.9f, -0.6f, 0f);
            Vector3 rightSpawn = transform.position + new Vector3(0.9f, -0.6f, 0f);

            SpawnBullet(leftSpawn, Vector2.down);
            SpawnBullet(rightSpawn, Vector2.down);
        }

        private void HandleSwarmHatch()
        {
            swarmTimer += Time.deltaTime;
            if (swarmTimer >= swarmHatchRate)
            {
                swarmTimer = 0f;
                StartCoroutine(HatchSwarmRoutine());
            }
        }

        private IEnumerator HatchSwarmRoutine()
        {
            if (ObjectPool.Instance == null) yield break;

            for (int i = 0; i < scoutsPerHatch; i++)
            {
                Vector3 spawn = transform.position + new Vector3((i - 1) * 0.9f, -0.8f, 0f);
                ObjectPool.Instance.Spawn(scoutPoolKey, spawn, Quaternion.identity);
                yield return new WaitForSeconds(0.2f);
            }
        }

        private void HandleSporeBomb()
        {
            sporeBombTimer += Time.deltaTime;
            if (sporeBombTimer >= sporeBombCooldown)
            {
                sporeBombTimer = 0f;
                LaunchAcidSporeBombs();
            }
        }

        private void LaunchAcidSporeBombs()
        {
            if (ObjectPool.Instance == null || isDead) return;

            Starfall.Audio.AudioManager.Instance?.PlayEnemyLaser();
            if (CameraShake.Instance != null)
                CameraShake.Instance.Shake(0.18f, 0.22f);

            // Launch two acidic spore pods from port and starboard bio-vents
            Vector3 leftSpawn = transform.position + new Vector3(-1.4f, -0.7f, 0f);
            Vector3 rightSpawn = transform.position + new Vector3(1.4f, -0.7f, 0f);

            var b1 = ObjectPool.Instance.Spawn(acidSporeBombPoolKey, leftSpawn, Quaternion.identity);
            if (b1 != null && b1.TryGetComponent<BossBomb>(out var bomb1))
            {
                bomb1.Initialize(BossBombType.AcidSpore, leftSpawn);
            }

            var b2 = ObjectPool.Instance.Spawn(acidSporeBombPoolKey, rightSpawn, Quaternion.identity);
            if (b2 != null && b2.TryGetComponent<BossBomb>(out var bomb2))
            {
                bomb2.Initialize(BossBombType.AcidSpore, rightSpawn);
            }

            Debug.Log("<color=green>[HiveQueenBoss] Deployed Dual Volatile Acid Spores!</color>");
        }

        private void HandleSpiralBurst()
        {
            spiralTimer += Time.deltaTime;
            if (spiralTimer >= spiralInterval)
            {
                spiralTimer = 0f;
                StartCoroutine(FireSpiralRoutine());
            }
        }

        private IEnumerator FireSpiralRoutine()
        {
            Starfall.Audio.AudioManager.Instance?.PlayEnemyLaser();

            int waves = 10;
            float angleOffset = Random.Range(0f, 360f);

            for (int i = 0; i < waves; i++)
            {
                float angle = (angleOffset + i * 36f) * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                if (dir.y < 0.2f)
                {
                    SpawnBullet(transform.position, dir.normalized);
                }
            }

            yield return null;
        }

        private void HandleDiveTimer()
        {
            diveTimer += Time.deltaTime;
            if (diveTimer >= diveInterval)
            {
                diveTimer = 0f;
                StartCoroutine(SwoopDiveRoutine());
            }
        }

        private IEnumerator SwoopDiveRoutine()
        {
            isDiving = true;
            BossEvents.TriggerBossPhaseChanged("WARNING: INCOMING SWOOP DIVE!", Color.red);
            Starfall.Audio.AudioManager.Instance?.PlayDiveBomberSwoop(1.0f);
            if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.3f, 0.4f);

            Vector3 startP = transform.position;
            Vector3 targetP = new Vector3(startP.x, -1.5f, 0f);

            // Dive down
            while (Vector3.Distance(transform.position, targetP) > 0.1f)
            {
                transform.position = Vector3.MoveTowards(transform.position, targetP, diveSpeed * Time.deltaTime);
                yield return null;
            }

            // Short pause at bottom apex
            yield return new WaitForSeconds(0.35f);

            // Return up to combat position
            Vector3 returnTarget = new Vector3(startP.x, combatY, 0f);
            while (Vector3.Distance(transform.position, returnTarget) > 0.1f)
            {
                transform.position = Vector3.MoveTowards(transform.position, returnTarget, (diveSpeed * 0.8f) * Time.deltaTime);
                yield return null;
            }

            transform.position = returnTarget;
            isDiving = false;
        }

        private void SpawnBullet(Vector3 pos, Vector2 direction)
        {
            if (ObjectPool.Instance == null) return;
            var go = ObjectPool.Instance.Spawn(projectilePoolKey, pos, Quaternion.identity);
            if (go != null && go.TryGetComponent<Projectile>(out var proj))
            {
                proj.Initialize(direction, projectileDamage, playerLayer, false, projectileSpeed);
            }
        }

        private IEnumerator DieSequence()
        {
            if (isDead) yield break;
            isDead = true;
            currentPhase = BossPhase.Dead;

            Debug.Log("[HiveQueenBoss] FINAL CAMPAIGN BOSS DEFEATED!");

            // 8-step explosion sequence
            for (int i = 0; i < 8; i++)
            {
                Vector3 offset = new Vector3(Random.Range(-2.2f, 2.2f), Random.Range(-1.5f, 1.5f), 0f);
                if (ObjectPool.Instance != null)
                    ObjectPool.Instance.Spawn(explosionPoolKey, transform.position + offset, Quaternion.identity);

                Starfall.Audio.AudioManager.Instance?.PlayExplosion(true);
                if (CameraShake.Instance != null)
                    CameraShake.Instance.Shake(0.4f, 0.35f);

                yield return new WaitForSeconds(0.18f);
            }

            // Final catastrophic bio-plasma detonation
            if (ObjectPool.Instance != null)
            {
                ObjectPool.Instance.Spawn("AlienExplosionVFX", transform.position, Quaternion.identity);
            }
            Starfall.Audio.AudioManager.Instance?.PlayLargeExplosion();
            if (CameraShake.Instance != null)
                CameraShake.Instance.Shake(0.65f, 0.55f);

            if (GameManager.Instance != null)
                GameManager.Instance.AddScore(scoreReward);

            // Grand Victory Drops: 4 Weapon Upgrades + 2 Bombs
            if (ObjectPool.Instance != null)
            {
                for (int i = 0; i < 4; i++)
                {
                    Vector3 drop = transform.position + new Vector3((i - 1.5f) * 0.7f, -0.5f, 0f);
                    ObjectPool.Instance.Spawn(weaponDropKey, drop, Quaternion.identity);
                }
                ObjectPool.Instance.Spawn(bombDropKey, transform.position + Vector3.left * 0.5f + Vector3.down, Quaternion.identity);
                ObjectPool.Instance.Spawn(bombDropKey, transform.position + Vector3.right * 0.5f + Vector3.down, Quaternion.identity);
            }

            BossEvents.TriggerBossDefeated("HIVE QUEEN LEVIATHAN");
            if (GameManager.Instance != null)
                GameManager.Instance.OnBossDefeated();

            yield return new WaitForSeconds(0.5f);
            if (ObjectPool.Instance != null)
                ObjectPool.Instance.Despawn(gameObject);
            else
                Destroy(gameObject);
        }
    }
}
