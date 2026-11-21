using System.Collections;
using UnityEngine;
using Starfall.Core;
using Starfall.Combat;
using Starfall.VFX;

namespace Starfall.Enemies
{
    /// <summary>
    /// Level 4 Campaign Boss: INFERNO COLOSSUS JUGGERNAUT.
    /// An armored, war-torn cybernetic siege titan assembled from derelict orbital warships.
    /// Features twin high-velocity siege railguns, incendiary flak cluster mortars,
    /// superheated kinetic ram charges, and a final meltdown bullet spiral.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class InfernoColossusBoss : MonoBehaviour, IDamageable, IPooledObject
    {
        [Header("Stats")]
        [SerializeField] private int maxHealth = 3000;
        [SerializeField] private int scoreReward = 20000;
        [SerializeField] private float entrySpeed = 2.2f;
        [SerializeField] private float combatY = 2.6f;

        [Header("Weapons")]
        [SerializeField] private string projectilePoolKey = "EnemyProjectile";
        [SerializeField] private int projectileDamage = 1;
        [SerializeField] private float railgunSpeed = 7.5f;
        [SerializeField] private float railgunFireRate = 1.0f;
        [SerializeField] private float mortarCooldown = 4.2f;
        [SerializeField] private float spiralCooldown = 3.6f;

        [Header("Escort Re-inforcements")]
        [SerializeField] private string fighterPoolKey = "FighterEnemy";
        [SerializeField] private float escortCooldown = 6.0f;

        [Header("Kinetic Ram Charge")]
        [SerializeField] private float ramInterval = 7.0f;
        [SerializeField] private float ramSpeed = 9.0f;

        [Header("VFX & Drops")]
        [SerializeField] private string explosionPoolKey = "ExplosionVFX";
        [SerializeField] private string weaponDropKey = "PowerUp_Weapon";
        [SerializeField] private string bombDropKey = "PowerUp_Bomb";
        [SerializeField] private SpriteRenderer[] renderers;

        private enum BossPhase { Entry, Phase1, Phase2, Phase3, Dead }
        private BossPhase currentPhase = BossPhase.Entry;

        private int currentHealth;
        private bool isDead = false;
        private bool isRamming = false;
        private float railgunTimer;
        private float mortarTimer;
        private float spiralTimer;
        private float escortTimer;
        private float ramTimer;
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
            isRamming = false;
            currentHealth = maxHealth;
            currentPhase = BossPhase.Entry;
            railgunTimer = 0.5f;
            mortarTimer = 2.5f;
            spiralTimer = 3.0f;
            escortTimer = 4.0f;
            ramTimer = 5.0f;
            motionTimer = 0f;

            SetTint(new Color(1f, 0.7f, 0.25f)); // Fiery iron
            BossEvents.TriggerBossSpawned("INFERNO COLOSSUS", currentHealth, maxHealth);
            Starfall.Audio.AudioManager.Instance?.PlayBossWarningSiren(4);
            Debug.Log("[InfernoColossus] Level 4 Boss Encounter Begun!");
        }

        public void OnDespawn()
        {
            StopAllCoroutines();
        }

        public void TakeDamage(int damage, Vector2 hitPoint)
        {
            if (isDead) return;

            // Phase 1 heavy armor plating reduces damage by 20%; Phase 3 core meltdown takes 1.5x bonus damage
            float armorMod = 1f;
            if (currentPhase == BossPhase.Phase1) armorMod = 0.8f;
            else if (currentPhase == BossPhase.Phase3) armorMod = 1.5f;

            int actualDamage = Mathf.Max(1, Mathf.RoundToInt(damage * armorMod));
            Starfall.VFX.FloatingDamageText.Spawn(hitPoint, actualDamage, currentPhase == BossPhase.Phase3);
            currentHealth = Mathf.Max(0, currentHealth - actualDamage);

            BossEvents.TriggerBossHealthChanged(currentHealth, maxHealth);

            if (CameraShake.Instance != null)
                CameraShake.Instance.Shake(0.09f, 0.12f);

            CheckPhaseTransition();

            if (currentHealth <= 0)
            {
                StartCoroutine(DieSequence());
            }
        }

        private void CheckPhaseTransition()
        {
            if (currentPhase == BossPhase.Phase1 && currentHealth <= maxHealth * 0.70f)
            {
                currentPhase = BossPhase.Phase2;
                SetTint(new Color(1f, 0.45f, 0.15f)); // Intense molten orange
                BossEvents.TriggerBossPhaseChanged("PHASE 2 - KINETIC RAM CHARGES & SIEGE MORTAR!", new Color(1f, 0.45f, 0.15f));
                Starfall.Audio.AudioManager.Instance?.PlayBossRoar();
                if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.35f, 0.4f);
            }
            else if (currentPhase == BossPhase.Phase2 && currentHealth <= maxHealth * 0.35f)
            {
                currentPhase = BossPhase.Phase3;
                SetTint(new Color(1f, 0.15f, 0.15f)); // Meltdown crimson
                BossEvents.TriggerBossPhaseChanged("PHASE 3 - APOCALYPSE OVERDRIVE & MELTDOWN SPIRAL!", new Color(1f, 0.15f, 0.15f));
                Starfall.Audio.AudioManager.Instance?.PlayBossRoar();
                if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.55f, 0.6f);
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
                    HandleHorizontalMovement(1.4f, 0.35f);
                    HandleRailguns(railgunFireRate);
                    HandleMortar();
                    break;
                case BossPhase.Phase2:
                    if (!isRamming)
                    {
                        HandleHorizontalMovement(1.8f, 0.45f);
                        HandleRailguns(railgunFireRate * 0.8f);
                        HandleMortar();
                        HandleEscorts();
                        HandleRamTimer();
                    }
                    break;
                case BossPhase.Phase3:
                    if (!isRamming)
                    {
                        HandleHorizontalMovement(2.2f, 0.6f);
                        HandleRailguns(railgunFireRate * 0.6f);
                        HandleMeltdownSpiral();
                        HandleRamTimer();
                    }
                    break;
            }
        }

        private void HandleEntry()
        {
            Vector3 target = new Vector3(0f, combatY, 0f);
            transform.position = Vector3.MoveTowards(transform.position, target, entrySpeed * Time.deltaTime);

            if (Vector3.Distance(transform.position, target) < 0.05f)
            {
                transform.position = target;
                currentPhase = BossPhase.Phase1;
                BossEvents.TriggerBossPhaseChanged("PHASE 1 - DUAL SIEGE RAILGUNS ENGAGED!", new Color(1f, 0.7f, 0.25f));
            }
        }

        private void HandleHorizontalMovement(float amplitude, float frequency)
        {
            motionTimer += Time.deltaTime;
            float targetX = Mathf.Sin(motionTimer * frequency * Mathf.PI * 2f) * amplitude;
            transform.position = new Vector3(targetX, combatY, 0f);
        }

        private void HandleRailguns(float rate)
        {
            railgunTimer += Time.deltaTime;
            if (railgunTimer >= rate)
            {
                railgunTimer = 0f;
                FireDualRailguns();
            }
        }

        private void FireDualRailguns()
        {
            if (ObjectPool.Instance == null) return;

            // Twin hardpoints on left and right railgun arms
            Vector3 leftMuzzle = transform.position + new Vector3(-1.15f, -0.95f, 0f);
            Vector3 rightMuzzle = transform.position + new Vector3(1.15f, -0.95f, 0f);

            FireRailgunBolt(leftMuzzle, Vector2.down);
            FireRailgunBolt(rightMuzzle, Vector2.down);

            // In Phase 3, also fire angled leading bolts
            if (currentPhase == BossPhase.Phase3 && Starfall.Player.PlayerController.Instance != null)
            {
                Vector3 pPos = Starfall.Player.PlayerController.Instance.transform.position;
                Vector2 aimLeft = (pPos - leftMuzzle).normalized;
                Vector2 aimRight = (pPos - rightMuzzle).normalized;
                FireRailgunBolt(leftMuzzle, aimLeft);
                FireRailgunBolt(rightMuzzle, aimRight);
            }

            Starfall.Audio.AudioManager.Instance?.PlayEnemyLaser();
        }

        private void FireRailgunBolt(Vector3 origin, Vector2 direction)
        {
            var bolt = ObjectPool.Instance.Spawn(projectilePoolKey, origin, Quaternion.identity);
            if (bolt != null)
            {
                var proj = bolt.GetComponent<Projectile>();
                if (proj != null)
                {
                    proj.Initialize(direction, projectileDamage, playerLayer, false, railgunSpeed);
                }
            }
        }

        private void HandleMortar()
        {
            mortarTimer += Time.deltaTime;
            if (mortarTimer >= mortarCooldown)
            {
                mortarTimer = 0f;
                StartCoroutine(FireMortarSalvoRoutine());
            }
        }

        private IEnumerator FireMortarSalvoRoutine()
        {
            if (ObjectPool.Instance == null) yield break;

            // Rains 3 explosive shells across left, center, right
            float[] targetXs = { -2.4f, 0f, 2.4f };
            for (int i = 0; i < targetXs.Length; i++)
            {
                if (isDead) yield break;
                Vector3 muzzle = transform.position + new Vector3(0f, -0.6f, 0f);
                Vector2 dir = new Vector2(targetXs[i] - muzzle.x, -4.5f).normalized;
                FireRailgunBolt(muzzle, dir);
                Starfall.Audio.AudioManager.Instance?.PlayEnemyLaser();
                yield return new WaitForSeconds(0.2f);
            }
        }

        private void HandleMeltdownSpiral()
        {
            spiralTimer += Time.deltaTime;
            if (spiralTimer >= spiralCooldown)
            {
                spiralTimer = 0f;
                StartCoroutine(SpiralMeltdownRoutine());
            }
        }

        private IEnumerator SpiralMeltdownRoutine()
        {
            if (ObjectPool.Instance == null) yield break;

            int waves = 8;
            int bulletsPerWave = 10;
            float angleOffset = 0f;

            for (int w = 0; w < waves; w++)
            {
                if (isDead) yield break;

                float step = 360f / bulletsPerWave;
                for (int b = 0; b < bulletsPerWave; b++)
                {
                    float rad = (b * step + angleOffset) * Mathf.Deg2Rad;
                    Vector2 dir = new Vector2(Mathf.Cos(rad), Mathf.Sin(rad));
                    FireRailgunBolt(transform.position, dir);
                }

                angleOffset += 14f;
                Starfall.Audio.AudioManager.Instance?.PlayEnemyLaser();
                yield return new WaitForSeconds(0.18f);
            }
        }

        private void HandleEscorts()
        {
            escortTimer += Time.deltaTime;
            if (escortTimer >= escortCooldown)
            {
                escortTimer = 0f;
                SpawnEscorts();
            }
        }

        private void SpawnEscorts()
        {
            if (ObjectPool.Instance == null) return;

            Vector3 leftSpawn = transform.position + new Vector3(-2.8f, -0.5f, 0f);
            Vector3 rightSpawn = transform.position + new Vector3(2.8f, -0.5f, 0f);

            var e1 = ObjectPool.Instance.Spawn(fighterPoolKey, leftSpawn, Quaternion.identity);
            var e2 = ObjectPool.Instance.Spawn(fighterPoolKey, rightSpawn, Quaternion.identity);

            if (e1 != null && e1.TryGetComponent<EnemyBase>(out var eb1)) eb1.SetWaveTier(4, 1.4f, 1.2f);
            if (e2 != null && e2.TryGetComponent<EnemyBase>(out var eb2)) eb2.SetWaveTier(4, 1.4f, 1.2f);
        }

        private void HandleRamTimer()
        {
            ramTimer += Time.deltaTime;
            if (ramTimer >= ramInterval)
            {
                ramTimer = 0f;
                StartCoroutine(KineticRamRoutine());
            }
        }

        private IEnumerator KineticRamRoutine()
        {
            isRamming = true;
            BossEvents.TriggerBossPhaseChanged("WARNING: SUPERHEATED RAM CHARGE!", Color.red);
            Starfall.Audio.AudioManager.Instance?.PlayDiveBomberSwoop(1.0f);
            if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.35f, 0.45f);

            // Wind up
            Vector3 startP = transform.position;
            yield return new WaitForSeconds(0.5f);

            // High-speed downward ram (stops comfortably in mid screen)
            Vector3 targetP = new Vector3(startP.x, 0.4f, 0f);
            while (Vector3.Distance(transform.position, targetP) > 0.1f)
            {
                if (isDead) yield break;
                transform.position = Vector3.MoveTowards(transform.position, targetP, ramSpeed * Time.deltaTime);
                yield return null;
            }

            // Impact quake at bottom
            if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.5f, 0.5f);
            Starfall.Audio.AudioManager.Instance?.PlayLargeExplosion();

            // Recover back to station
            Vector3 returnTarget = new Vector3(0f, combatY, 0f);
            while (Vector3.Distance(transform.position, returnTarget) > 0.1f)
            {
                if (isDead) yield break;
                transform.position = Vector3.MoveTowards(transform.position, returnTarget, (ramSpeed * 0.7f) * Time.deltaTime);
                yield return null;
            }

            transform.position = returnTarget;
            isRamming = false;
        }

        private IEnumerator DieSequence()
        {
            if (isDead) yield break;
            isDead = true;
            currentPhase = BossPhase.Dead;

            Debug.Log("[InfernoColossus] DEFEATED!");
            BossEvents.TriggerBossDefeated("INFERNO COLOSSUS");

            // Multi-explosion destruction sequence
            for (int i = 0; i < 9; i++)
            {
                Vector3 offset = new Vector3(Random.Range(-2f, 2f), Random.Range(-1.5f, 1.5f), 0f);
                if (ObjectPool.Instance != null)
                {
                    ObjectPool.Instance.Spawn(explosionPoolKey, transform.position + offset, Quaternion.identity);
                }
                Starfall.Audio.AudioManager.Instance?.PlayLargeExplosion();
                if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.35f, 0.25f);
                yield return new WaitForSeconds(0.2f);
            }

            // Final apocalyptic detonation
            if (ObjectPool.Instance != null)
            {
                ObjectPool.Instance.Spawn("NukeDetonationVFX", transform.position, Quaternion.identity);
            }
            if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.6f, 0.5f);

            // High-value reward power-up drops
            if (ObjectPool.Instance != null)
            {
                ObjectPool.Instance.Spawn(weaponDropKey, transform.position + new Vector3(-1.2f, 0f, 0f), Quaternion.identity);
                ObjectPool.Instance.Spawn(bombDropKey, transform.position + new Vector3(1.2f, 0f, 0f), Quaternion.identity);
            }

            if (GameManager.Instance != null)
            {
                GameManager.Instance.AddScore(scoreReward);
                GameManager.Instance.OnBossDefeated();
            }

            ObjectPool.Instance?.Despawn(gameObject);
        }
    }
}
