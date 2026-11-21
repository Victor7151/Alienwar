using System.Collections;
using UnityEngine;
using Starfall.Core;
using Starfall.Combat;
using Starfall.VFX;

namespace Starfall.Enemies
{
    /// <summary>
    /// Level 2 Chapter Boss: PLASMA DREADNOUGHT.
    /// Three-phase battlecruiser featuring quad battery cannons, 8-way radial plasma bursts,
    /// fighter escort launches, and an overclocked figure-8 core sequence.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class PlasmaDreadnought : MonoBehaviour, IDamageable, IPooledObject
    {
        [Header("Stats")]
        [SerializeField] private int maxHealth = 1800;
        [SerializeField] private int scoreReward = 8000;
        [SerializeField] private float entrySpeed = 2.2f;
        [SerializeField] private float combatY = 2.1f;

        [Header("Weapons")]
        [SerializeField] private string projectilePoolKey = "EnemyProjectile";
        [SerializeField] private int projectileDamage = 1;
        [SerializeField] private float projectileSpeed = 6.2f;
        [SerializeField] private float batteryFireRate = 1.3f;
        [SerializeField] private float radialBurstInterval = 4.5f;

        [Header("Signature Bomb Attack")]
        [SerializeField] private string plasmaBombPoolKey = "BossBomb_PlasmaCore";
        [SerializeField] private float bombCooldown = 8.5f;

        [Header("Escorts & Spawns")]
        [SerializeField] private string fighterPoolKey = "FighterEnemy";
        [SerializeField] private float escortSpawnRate = 7f;

        [Header("Visuals & VFX")]
        [SerializeField] private string explosionPoolKey = "ExplosionVFX";
        [SerializeField] private string weaponDropKey = "PowerUp_Weapon";
        [SerializeField] private string bombDropKey = "PowerUp_Bomb";
        [SerializeField] private SpriteRenderer[] renderers;

        private enum BossPhase { Entry, Phase1, Phase2, Phase3, Dead }
        private BossPhase currentPhase = BossPhase.Entry;

        private int currentHealth;
        private bool isDead = false;
        private float batteryTimer;
        private float radialTimer;
        private float escortTimer;
        private float bombTimer;
        private float motionTimer;
        private int currentCannonIndex = 0;
        private LayerMask playerLayer;

        private readonly Vector3[] cannonOffsets = new Vector3[]
        {
            new Vector3(-0.95f, -0.5f, 0f),
            new Vector3(-0.35f, -0.6f, 0f),
            new Vector3( 0.35f, -0.6f, 0f),
            new Vector3( 0.95f, -0.5f, 0f)
        };

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
            currentHealth = maxHealth;
            currentPhase = BossPhase.Entry;
            batteryTimer = 0.5f;
            radialTimer = 3.0f;
            escortTimer = 4.0f;
            bombTimer = 3.5f;
            motionTimer = 0f;

            SetTint(new Color(0.3f, 0.8f, 1f));
            BossEvents.TriggerBossSpawned("PLASMA DREADNOUGHT", currentHealth, maxHealth);
            Starfall.Audio.AudioManager.Instance?.PlayBossWarningSiren(3);
            Debug.Log("[PlasmaDreadnought] Level 2 Boss Encounter Begun!");
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
                CameraShake.Instance.Shake(0.08f, 0.1f);

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
                SetTint(new Color(1f, 0.65f, 0.15f)); // Amber alert
                BossEvents.TriggerBossPhaseChanged("PHASE 2 - ESCORT CARRIER & PLASMA BOMB!", new Color(1f, 0.65f, 0.15f));
                Starfall.Audio.AudioManager.Instance?.PlayBossRoar();
                if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.3f, 0.35f);
            }
            else if (currentPhase == BossPhase.Phase2 && currentHealth <= maxHealth * 0.30f)
            {
                currentPhase = BossPhase.Phase3;
                SetTint(new Color(1f, 0.2f, 0.2f)); // Crimson overdrive
                BossEvents.TriggerBossPhaseChanged("PHASE 3 - OVERCLOCKED CORE SINGULARITY!", new Color(1f, 0.2f, 0.2f));
                Starfall.Audio.AudioManager.Instance?.PlayBossRoar();
                if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.45f, 0.5f);
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
                    HandleMovement(horizontalRange: 2.2f, speed: 1.1f);
                    HandleBatteries(speedMult: 1f);
                    break;
                case BossPhase.Phase2:
                    HandleMovement(horizontalRange: 2.8f, speed: 1.4f);
                    HandleBatteries(speedMult: 1f);
                    HandleRadialBurst();
                    HandleEscorts();
                    HandlePlasmaBomb();
                    break;
                case BossPhase.Phase3:
                    HandleFigure8Movement();
                    HandleBatteries(speedMult: 1.8f);
                    HandleRadialBurst();
                    HandlePlasmaBomb();
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
                BossEvents.TriggerBossPhaseChanged("PHASE 1 - QUAD BATTERIES!", new Color(0.3f, 0.8f, 1f));
            }
        }

        private void HandleMovement(float horizontalRange, float speed)
        {
            motionTimer += Time.deltaTime * speed;
            float x = Mathf.Sin(motionTimer) * horizontalRange;
            transform.position = new Vector3(x, combatY, 0f);
        }

        private void HandleFigure8Movement()
        {
            motionTimer += Time.deltaTime * 1.8f;
            float x = Mathf.Sin(motionTimer) * 2.8f;
            float y = combatY + Mathf.Sin(motionTimer * 2f) * 0.35f;
            transform.position = new Vector3(x, y, 0f);
        }

        private void HandleBatteries(float speedMult)
        {
            batteryTimer += Time.deltaTime * speedMult;
            if (batteryTimer >= batteryFireRate)
            {
                batteryTimer = 0f;
                FireNextBattery();
            }
        }

        private void FireNextBattery()
        {
            Starfall.Audio.AudioManager.Instance?.PlayEnemyLaser();

            Vector3 spawn = transform.position + cannonOffsets[currentCannonIndex];
            currentCannonIndex = (currentCannonIndex + 1) % cannonOffsets.Length;

            SpawnBullet(spawn, Vector2.down);
        }

        private void HandleRadialBurst()
        {
            radialTimer += Time.deltaTime;
            if (radialTimer >= radialBurstInterval)
            {
                radialTimer = 0f;
                StartCoroutine(FireRadialBlossom());
            }
        }

        private IEnumerator FireRadialBlossom()
        {
            Starfall.Audio.AudioManager.Instance?.PlayEnemyLaser();

            int petalCount = 8;
            float angleStep = 360f / petalCount;

            for (int i = 0; i < petalCount; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                // Filter to primarily downward angles so shots are dodgeable
                if (dir.y < 0.2f)
                {
                    SpawnBullet(transform.position, dir.normalized);
                }
            }

            yield return null;
        }

        private void HandleEscorts()
        {
            escortTimer += Time.deltaTime;
            if (escortTimer >= escortSpawnRate)
            {
                escortTimer = 0f;
                SpawnEscortDrones();
            }
        }

        private void SpawnEscortDrones()
        {
            if (ObjectPool.Instance == null) return;
            Vector3 leftSpawn = transform.position + new Vector3(-1.8f, -0.8f, 0f);
            Vector3 rightSpawn = transform.position + new Vector3(1.8f, -0.8f, 0f);

            ObjectPool.Instance.Spawn(fighterPoolKey, leftSpawn, Quaternion.identity);
            ObjectPool.Instance.Spawn(fighterPoolKey, rightSpawn, Quaternion.identity);
        }

        private void HandlePlasmaBomb()
        {
            bombTimer += Time.deltaTime;
            if (bombTimer >= bombCooldown)
            {
                bombTimer = 0f;
                LaunchPlasmaBomb();
            }
        }

        private void LaunchPlasmaBomb()
        {
            if (ObjectPool.Instance == null || isDead) return;

            Vector3 spawnPos = transform.position + new Vector3(0f, -0.9f, 0f);
            GameObject bomb = ObjectPool.Instance.Spawn(plasmaBombPoolKey, spawnPos, Quaternion.identity);
            if (bomb != null && bomb.TryGetComponent<BossBomb>(out var bb))
            {
                bb.Initialize(BossBombType.PlasmaSingularity, spawnPos);
            }

            if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.18f, 0.2f);
            Debug.Log("<color=cyan>[PlasmaDreadnought] Deployed Overcharged Plasma Singularity Bomb!</color>");
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

            Debug.Log("[PlasmaDreadnought] DEFEATED!");

            // 6-step massive explosion sequence
            for (int i = 0; i < 6; i++)
            {
                Vector3 offset = new Vector3(Random.Range(-2f, 2f), Random.Range(-1.2f, 1.2f), 0f);
                if (ObjectPool.Instance != null)
                    ObjectPool.Instance.Spawn(explosionPoolKey, transform.position + offset, Quaternion.identity);

                Starfall.Audio.AudioManager.Instance?.PlayExplosion(true);
                if (CameraShake.Instance != null)
                    CameraShake.Instance.Shake(0.35f, 0.3f);

                yield return new WaitForSeconds(0.2f);
            }

            // Final catastrophic detonation
            if (ObjectPool.Instance != null)
            {
                ObjectPool.Instance.Spawn("NukeDetonationVFX", transform.position, Quaternion.identity);
            }
            if (CameraShake.Instance != null)
                CameraShake.Instance.Shake(0.6f, 0.5f);

            if (GameManager.Instance != null)
                GameManager.Instance.AddScore(scoreReward);

            // Generous drops: 3 Weapon Upgrades + 1 Bomb
            if (ObjectPool.Instance != null)
            {
                for (int i = 0; i < 3; i++)
                {
                    Vector3 drop = transform.position + new Vector3((i - 1) * 0.8f, -0.5f, 0f);
                    ObjectPool.Instance.Spawn(weaponDropKey, drop, Quaternion.identity);
                }
                ObjectPool.Instance.Spawn(bombDropKey, transform.position + Vector3.down, Quaternion.identity);
            }

            BossEvents.TriggerBossDefeated("PLASMA DREADNOUGHT");
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
