using UnityEngine;
using System.Collections;
using Starfall.Core;
using Starfall.Combat;
using Starfall.VFX;

namespace Starfall.Enemies
{
    /// <summary>
    /// INVADER CARRIER – Planet 1 Chapter Boss.
    /// Three-phase state machine inspired by the Chicken Invaders boss structure.
    ///
    ///   Phase 1 (HP 200→134): Turret Phase
    ///     – Slow leftward drift, fires 4-directional rotating turret salvo.
    ///   Phase 2 (HP 133→67): Scout Bay Phase
    ///     – Stops moving, opens scout bay, spawns 3 Scouts every 5s.
    ///     – Turrets continue firing.
    ///   Phase 3 (HP 66→0): Core Exposed Phase
    ///     – Bay opens fully revealing the glowing exposed core.
    ///     – All turrets fire at double speed.
    ///     – Boss starts slow horizontal sine-wave movement.
    ///
    /// On death: massive explosion chain, 5000 score, drops weapon/bomb pickups.
    /// </summary>
    public class InvaderCarrier : MonoBehaviour, IDamageable, IPooledObject
    {
        // ------------------------------------------------------------------
        // Inspector Config
        // ------------------------------------------------------------------

        [Header("Stats")]
        [SerializeField] private int maxHealth = 2500;
        [SerializeField] private int scoreReward = 10000;

        [Header("Movement")]
        [SerializeField] private float entrySpeed = 2.5f;
        [SerializeField] private float entryTargetY = 2.2f;
        [SerializeField] private float phase3SineAmplitude = 2.2f;
        [SerializeField] private float phase3SineFrequency = 0.45f;

        [Header("Phase 1 – Turrets")]
        [SerializeField] private float turretFireRate = 1.7f;
        [SerializeField] private string turretProjectileKey = "EnemyProjectile";
        [SerializeField] private int turretDamage = 1;
        [SerializeField] private float turretProjectileSpeed = 6.2f;
        [SerializeField] private LayerMask playerLayer;

        [Header("Phase 2 – Scout Bay")]
        [SerializeField] private string scoutPoolKey = "ScoutEnemy";
        [SerializeField] private float scoutSpawnRate = 4.5f;
        [SerializeField] private int scoutsPerSpawn = 3;
        [SerializeField] private Vector2 scoutBayOffset = new Vector2(-0.5f, 0f);

        [Header("Phase 3 – Core Exposed & Bullet Hell")]
        [SerializeField] private float corePulseInterval = 2.4f;

        [Header("Signature Bomb Attack")]
        [SerializeField] private string empTorpedoPoolKey = "BossBomb_EmpTorpedo";
        [SerializeField] private float torpedoCooldown = 8.0f;

        [Header("VFX & Drops")]
        [SerializeField] private string explosionPoolKey = "ExplosionVFX";
        [SerializeField] private string weaponDropKey = "PowerUp_Weapon";
        [SerializeField] private string bombDropKey = "PowerUp_Bomb";
        [SerializeField] private SpriteRenderer[] phaseColorRenderers;

        // Phase colors to visualise state visually
        [SerializeField] private Color phase1Color = new Color(0.4f, 0.8f, 1f);   // cool blue
        [SerializeField] private Color phase2Color = new Color(1f, 0.6f, 0.1f);   // amber
        [SerializeField] private Color phase3Color = new Color(1f, 0.2f, 0.2f);   // danger red

        // ------------------------------------------------------------------
        // Runtime State
        // ------------------------------------------------------------------

        private enum BossPhase { Entry, Phase1, Phase2, Phase3, Dead }

        private BossPhase currentPhase = BossPhase.Entry;
        private int currentHealth;
        private float phase1HPThreshold;
        private float phase2HPThreshold;
        private float turretTimer;
        private float scoutTimer;
        private float torpedoTimer;
        private float sineTimer;
        private float corePulseTimer;
        private float entryStartX;
        private bool isDead = false;

        public bool IsDead => isDead;

        // Events for HUD boss health bar
        public static event System.Action<int, int> OnHealthChanged;  // (current, max)
        public static event System.Action<int>       OnPhaseChanged;  // (phaseNumber 1-3)
        public static event System.Action             OnBossDied;

        // ------------------------------------------------------------------
        // Initialization
        // ------------------------------------------------------------------

        private void Awake()
        {
            if (string.IsNullOrEmpty(scoutPoolKey) || scoutPoolKey == "Scout")
                scoutPoolKey = "ScoutEnemy";
            if (playerLayer.value == 0)
                playerLayer = LayerMask.GetMask("Player");
            if (phaseColorRenderers == null || phaseColorRenderers.Length == 0)
            {
                var sr = GetComponentInChildren<SpriteRenderer>();
                if (sr != null) phaseColorRenderers = new[] { sr };
            }
        }

        // ------------------------------------------------------------------
        // IPooledObject
        // ------------------------------------------------------------------

        public void OnSpawn()
        {
            isDead = false;
            currentHealth = maxHealth;
            currentPhase = BossPhase.Entry;
            phase1HPThreshold = maxHealth * 0.67f;
            phase2HPThreshold = maxHealth * 0.33f;
            turretTimer = 0f;
            scoutTimer = 0f;
            torpedoTimer = 3.0f;
            sineTimer = 0f;
            corePulseTimer = 0f;
            entryStartX = 0f;

            SetPhaseColor(phase1Color);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            OnPhaseChanged?.Invoke(1);
            BossEvents.TriggerBossSpawned("INVADER CARRIER", currentHealth, maxHealth);
            Starfall.Audio.AudioManager.Instance?.PlayBossWarningSiren(3);

            Debug.Log($"[InvaderCarrier] SPAWNED – Phase 1 (Turrets, {maxHealth} HP)");
        }

        public void OnDespawn()
        {
            StopAllCoroutines();
        }

        // ------------------------------------------------------------------
        // IDamageable
        // ------------------------------------------------------------------

        public void TakeDamage(int damage, Vector2 hitPoint)
        {
            if (isDead) return;

            // Maximum damage clamp per hit to prevent bomb or rapid cheese
            int incoming = Mathf.Min(damage, 25);

            // Core exposed in Phase 3 → takes bonus 1.5x weapon damage
            int actualDamage = (currentPhase == BossPhase.Phase3)
                ? Mathf.Min(Mathf.RoundToInt(incoming * 1.5f), 35)
                : incoming;

            Starfall.VFX.FloatingDamageText.Spawn(hitPoint, actualDamage, currentPhase == BossPhase.Phase3);
            currentHealth -= actualDamage;
            currentHealth = Mathf.Max(0, currentHealth);
            OnHealthChanged?.Invoke(currentHealth, maxHealth);
            BossEvents.TriggerBossHealthChanged(currentHealth, maxHealth);

            if (CameraShake.Instance != null)
                CameraShake.Instance.Shake(0.08f, 0.12f);

            CheckPhaseTransition();

            if (currentHealth <= 0)
                StartCoroutine(DieSequence());
        }

        // ------------------------------------------------------------------
        // Update Loop
        // ------------------------------------------------------------------

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
                    HandleTurrets();
                    HandleSineMovement(1.2f, 0.22f);
                    break;
                case BossPhase.Phase2:
                    HandleTurrets();
                    HandleScoutBay();
                    HandleEmpTorpedo();
                    HandleSineMovement(1.6f, 0.32f);
                    break;
                case BossPhase.Phase3:
                    HandleTurrets(doubled: true);
                    HandleScoutBay();
                    HandleEmpTorpedo();
                    HandleSineMovement();
                    HandleCorePulse();
                    break;
            }
        }

        // ------------------------------------------------------------------
        // Phase Behaviour
        // ------------------------------------------------------------------

        private void HandleEntry()
        {
            // Smoothly glide boss to top-center of viewport (X = 0, Y = entryTargetY)
            Vector3 targetPos = new Vector3(0f, entryTargetY, transform.position.z);
            transform.position = Vector3.MoveTowards(transform.position, targetPos, entrySpeed * Time.deltaTime);

            if (Vector3.Distance(transform.position, targetPos) < 0.05f)
            {
                transform.position = targetPos;
                entryStartX = 0f;
                TransitionToPhase(BossPhase.Phase1);
            }
        }

        private void HandleTurrets(bool doubled = false)
        {
            float rate = doubled ? turretFireRate * 0.55f : turretFireRate;
            turretTimer += Time.deltaTime;
            if (turretTimer >= rate)
            {
                turretTimer = 0f;
                FireTurretSalvo();
            }
        }

        private void HandleScoutBay()
        {
            scoutTimer += Time.deltaTime;
            if (scoutTimer >= scoutSpawnRate)
            {
                scoutTimer = 0f;
                SpawnScouts();
            }
        }

        private void HandleSineMovement(float amplitude = -1f, float frequency = -1f)
        {
            // Smooth sine-wave horizontal drift symmetrically centered across the screen
            float amp = amplitude > 0f ? amplitude : phase3SineAmplitude;
            float freq = frequency > 0f ? frequency : phase3SineFrequency;
            sineTimer += Time.deltaTime;
            float sineX = Mathf.Sin(sineTimer * freq * Mathf.PI * 2f) * amp;
            Vector3 pos = transform.position;
            pos.x = entryStartX + sineX;
            pos.y = entryTargetY;
            transform.position = pos;
        }

        private void HandleCorePulse()
        {
            corePulseTimer += Time.deltaTime;
            if (corePulseTimer >= corePulseInterval)
            {
                corePulseTimer = 0f;
                FireCorePulseSalvo();
            }
        }

        private void HandleEmpTorpedo()
        {
            torpedoTimer += Time.deltaTime;
            if (torpedoTimer >= torpedoCooldown)
            {
                torpedoTimer = 0f;
                LaunchEmpTorpedo();
            }
        }

        private void LaunchEmpTorpedo()
        {
            if (ObjectPool.Instance == null || isDead) return;

            Starfall.Audio.AudioManager.Instance?.PlayEnemyLaser();
            if (CameraShake.Instance != null)
                CameraShake.Instance.Shake(0.18f, 0.22f);

            Vector3 spawnPos = transform.position + new Vector3(0f, -0.8f, 0f);
            var go = ObjectPool.Instance.Spawn(empTorpedoPoolKey, spawnPos, Quaternion.identity);
            if (go != null && go.TryGetComponent<BossBomb>(out var bomb))
            {
                bomb.Initialize(BossBombType.EmpTorpedo, spawnPos);
            }

            Debug.Log("<color=orange>[InvaderCarrier] Launched Heavy EMP Seismic Guided Torpedo!</color>");
        }

        // ------------------------------------------------------------------
        // Combat
        // ------------------------------------------------------------------

        private void FireTurretSalvo()
        {
            if (ObjectPool.Instance == null) return;

            // 5 directional downward fan towards the player
            Vector2[] directions =
            {
                Vector2.down,
                new Vector2(-0.35f, -1f).normalized,
                new Vector2( 0.35f, -1f).normalized,
                new Vector2(-0.7f,  -0.7f).normalized,
                new Vector2( 0.7f,  -0.7f).normalized,
            };

            foreach (var dir in directions)
            {
                var go = ObjectPool.Instance.Spawn(turretProjectileKey,
                    transform.position, Quaternion.identity);
                if (go != null && go.TryGetComponent<Projectile>(out var proj))
                    proj.Initialize(dir, turretDamage, playerLayer, false, turretProjectileSpeed);
            }
        }

        private void FireCorePulseSalvo()
        {
            if (ObjectPool.Instance == null) return;

            Starfall.Audio.AudioManager.Instance?.PlayEnemyLaser();
            if (CameraShake.Instance != null)
                CameraShake.Instance.Shake(0.12f, 0.15f);

            Vector3 corePos = transform.position + new Vector3(0f, -0.6f, 0f);

            // 8-directional radial burst from the exposed core
            float angleStep = 360f / 8f;
            for (int i = 0; i < 8; i++)
            {
                float angle = i * angleStep * Mathf.Deg2Rad;
                Vector2 dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));

                var go = ObjectPool.Instance.Spawn(turretProjectileKey, corePos, Quaternion.identity);
                if (go != null && go.TryGetComponent<Projectile>(out var proj))
                {
                    proj.Initialize(dir, turretDamage, playerLayer, false, turretProjectileSpeed * 0.9f);
                }
            }
        }

        private void SpawnScouts()
        {
            if (ObjectPool.Instance == null) return;

            for (int i = 0; i < scoutsPerSpawn; i++)
            {
                Vector3 offset = (Vector3)scoutBayOffset + new Vector3((i - 1) * 1.0f, -0.5f, 0f);
                ObjectPool.Instance.Spawn(scoutPoolKey, transform.position + offset, Quaternion.identity);
            }

            Debug.Log($"[InvaderCarrier] Scout bay released {scoutsPerSpawn} scouts.");
        }

        // ------------------------------------------------------------------
        // Phase Transitions
        // ------------------------------------------------------------------

        private void CheckPhaseTransition()
        {
            if (currentPhase == BossPhase.Phase1 && currentHealth <= phase1HPThreshold)
                TransitionToPhase(BossPhase.Phase2);
            else if (currentPhase == BossPhase.Phase2 && currentHealth <= phase2HPThreshold)
                TransitionToPhase(BossPhase.Phase3);
        }

        private void TransitionToPhase(BossPhase newPhase)
        {
            currentPhase = newPhase;

            if (CameraShake.Instance != null)
                CameraShake.Instance.Shake(0.35f, 0.4f);

            switch (newPhase)
            {
                case BossPhase.Phase1:
                    SetPhaseColor(phase1Color);
                    OnPhaseChanged?.Invoke(1);
                    BossEvents.TriggerBossPhaseChanged("PHASE 1 - TURRETS ONLINE!", phase1Color);
                    Debug.Log("[InvaderCarrier] -> Phase 1: Turrets active.");
                    break;
                case BossPhase.Phase2:
                    SetPhaseColor(phase2Color);
                    OnPhaseChanged?.Invoke(2);
                    BossEvents.TriggerBossPhaseChanged("PHASE 2 - SCOUT BAY OPEN!", phase2Color);
                    Starfall.Audio.AudioManager.Instance?.PlayBossRoar();
                    Debug.Log("[InvaderCarrier] -> Phase 2: Scout Bay opened!");
                    break;
                case BossPhase.Phase3:
                    SetPhaseColor(phase3Color);
                    OnPhaseChanged?.Invoke(3);
                    BossEvents.TriggerBossPhaseChanged("PHASE 3 - CORE EXPOSED!", phase3Color);
                    Starfall.Audio.AudioManager.Instance?.PlayBossRoar();
                    Debug.Log("[InvaderCarrier] -> Phase 3: CORE EXPOSED!");
                    break;
            }
        }

        private void SetPhaseColor(Color c)
        {
            if (phaseColorRenderers == null) return;
            foreach (var sr in phaseColorRenderers)
                if (sr != null) sr.color = c;
        }

        // ------------------------------------------------------------------
        // Death
        // ------------------------------------------------------------------

        private IEnumerator DieSequence()
        {
            if (isDead) yield break;
            isDead = true;
            currentPhase = BossPhase.Dead;

            Debug.Log("[InvaderCarrier] DEFEATED!");

            // Explosion chain: 5 explosions spread over the boss body
            for (int i = 0; i < 5; i++)
            {
                Vector3 randomOffset = new Vector3(
                    Random.Range(-1.5f, 1.5f),
                    Random.Range(-1f, 1f), 0f);

                if (ObjectPool.Instance != null)
                    ObjectPool.Instance.Spawn(explosionPoolKey,
                        transform.position + randomOffset, Quaternion.identity);

                Starfall.Audio.AudioManager.Instance?.PlayExplosion(true);

                if (CameraShake.Instance != null)
                    CameraShake.Instance.Shake(0.3f, 0.25f);

                yield return new WaitForSeconds(0.2f);
            }

            // Final catastrophic capital ship detonation
            if (ObjectPool.Instance != null)
            {
                ObjectPool.Instance.Spawn("BossExplosionVFX", transform.position, Quaternion.identity);
            }
            Starfall.Audio.AudioManager.Instance?.PlayLargeExplosion();
            if (CameraShake.Instance != null)
                CameraShake.Instance.Shake(0.55f, 0.45f);

            // Award score
            if (GameManager.Instance != null)
                GameManager.Instance.AddScore(scoreReward);

            // Drop rewards: 3 weapon upgrades + 1 bomb
            if (ObjectPool.Instance != null)
            {
                for (int i = 0; i < 3; i++)
                {
                    Vector3 drop = transform.position + new Vector3(Random.Range(-1f, 1f), Random.Range(-0.5f, 0.5f), 0f);
                    ObjectPool.Instance.Spawn(weaponDropKey, drop, Quaternion.identity);
                }
                ObjectPool.Instance.Spawn(bombDropKey, transform.position + Vector3.down, Quaternion.identity);
            }

            // Fire death events → triggers BossHealthBar to hide, GameManager to warp
            OnBossDied?.Invoke();
            BossEvents.TriggerBossDefeated("INVADER CARRIER");
            if (GameManager.Instance != null)
                GameManager.Instance.OnBossDefeated();

            // Final despawn
            yield return new WaitForSeconds(0.5f);
            if (ObjectPool.Instance != null)
                ObjectPool.Instance.Despawn(gameObject);
            else
                Destroy(gameObject);
        }
    }
}
