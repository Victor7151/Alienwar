using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Starfall.Combat;
using Starfall.Core;
using Starfall.VFX;

namespace Starfall.Enemies
{
    public enum EntryFlightPath
    {
        DirectDrop,
        SineWaveWeave,
        FlankArcSwoop,
        SpiralLoop
    }

    public enum DivePatternType
    {
        StandardSwoop,
        LoopingDive,
        FlankCrossfireDive,
        HighSpeedStrafe,
        KamikazeHoming
    }

    /// <summary>
    /// Base class for all alien invader enemies.
    ///
    /// Movement states: formation entry → stationed in slot → dive / charge → return lane → slot.
    /// Free-flying enemies (divers, raiders, bonus pods) steer apart so bodies never overlap;
    /// formation members are kept apart by the slot lattice itself.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public abstract class EnemyBase : MonoBehaviour, IDamageable, IPooledObject
    {
        /// <summary>Same speed for every formation member so entry queues keep their spacing.</summary>
        public const float FormationEntrySpeed = 7.5f;
        public const float ShieldFieldRadius = 1.7f;
        private const float ReturnSpeed = 6.5f;

        /// <summary>All enabled enemies. Used for separation, shield fields and cheap alive counts.</summary>
        public static readonly List<EnemyBase> Active = new List<EnemyBase>(64);
        public static event System.Action<EnemyBase> OnEnemyKilled;

        [Header("Stats")]
        [SerializeField] protected int maxHealth = 4;
        [SerializeField] protected int scoreValue = 100;
        [SerializeField] protected float speed = 4f;
        [SerializeField] protected int contactDamage = 3;

        [Header("Visuals")]
        [SerializeField] protected SpriteRenderer spriteRenderer;
        [SerializeField] protected Color hitFlashColor = new Color(1f, 0.25f, 0.25f, 1f);

        [Header("VFX")]
        [SerializeField] protected string explosionPoolKey = "ExplosionVFX";

        [Header("Power-up Drop")]
        [SerializeField] protected float dropChance = 0.12f;
        [SerializeField] protected string[] powerUpPoolKeys = { "PowerUp_Weapon", "PowerUp_Shield", "PowerUp_Bomb", "PowerUp_RapidFire", "PowerUp_Score", "PowerUp_Wingman", "PowerUp_HomingMissiles", "PowerUp_TimeWarp", "PowerUp_Magnet" };

        private static int powerUpsDroppedThisWave = 0;
        public static int MaxDropsPerWave = 3;
        public static float DropChanceMultiplier = 1f;
        public static void ResetWavePowerUpCount() => powerUpsDroppedThisWave = 0;

        [Header("Formation")]
        [SerializeField] protected float entrySpeed = 8f;
        [SerializeField] protected string projectilePoolKey = "EnemyProjectile";
        [SerializeField] protected float projectileSpeed = 6.2f;
        [SerializeField] protected LayerMask targetLayer;

        protected bool isInFormation = false;
        protected FormationGridController formationGrid;
        protected int formationRow;
        protected int formationCol;
        protected bool isStationed = false;

        [Header("Flight Paths & Dive Bombers")]
        [SerializeField] protected EntryFlightPath entryFlightPath = EntryFlightPath.DirectDrop;
        [SerializeField] protected float diveSpeed = 7.5f;
        protected DivePatternType currentDivePattern = DivePatternType.StandardSwoop;
        protected Vector3 entryStartPos;
        protected Vector3 entryLogicalPos;
        protected float entryTimer = 0f;
        protected float totalEntryDist = 1f;
        protected bool isDiving = false;
        protected bool isReturning = false;
        protected bool isBusy = false;
        /// <summary>Set while a routine (blink, charge telegraph) positions the enemy itself.</summary>
        protected bool transformOwned = false;
        protected bool isRaiding = false;
        protected Coroutine diveCoroutine;
        protected Coroutine actionCoroutine;
        protected Coroutine warningCoroutine;
        protected bool isBonusPod = false;
        protected float freeTimer;
        private float podPhase;
        protected float offscreenTimer;
        private bool lastStandActive;
        private float panicTimer;
        public bool IsPanicked => panicTimer > 0f;

        private static float lastDiveSfxTime = -1f;
        private static float lastEntrySfxTime = -1f;

        protected int currentHealth;
        protected Color originalColor = Color.white;
        protected Coroutine hitFlashCoroutine;
        private float shieldCarry;
        private SpriteRenderer roleAura;

        private static int activeBomberChainDepth = 0;
        private static readonly List<Collider2D> blastColliderList = new List<Collider2D>(16);
        private float shielderPulseTimer = 0f;
        private float lastProtectedPopupTime = -1f;

        // Cached authored values so pooled reuse never inherits a previous life's modifiers.
        private int baseMaxHealth;
        private int baseScoreValue;
        private float baseDropChance;
        private float baseProjectileSpeed;
        private bool baseCached;

        public EnemyRole Role { get; private set; } = EnemyRole.Grunt;
        public bool IsDead => currentHealth <= 0;
        public int MaxHealth => maxHealth;
        public int CurrentHealth => currentHealth;
        public bool IsInFormation => isInFormation;
        public bool IsDiving => isDiving;
        public bool IsStationed => isInFormation && isStationed && !isDiving;
        public bool IsBusy => isBusy || isDiving || isReturning;
        public bool IsBonusPod => isBonusPod;
        public bool IsFreeFlying => isDiving || isReturning || isRaiding || isBonusPod;

        protected Vector3 baseScale = Vector3.one;

        private Vector3 stealthDirection = Vector3.down;
        private float stealthSpeed = 3.5f;
        private float stealthOscillationAmp = 0f;
        private float stealthOscillationFreq = 0f;
        private float stealthSpawnTime = 0f;

        public void SetStealthTrajectory(Vector3 dir, float moveSpeed, float oscAmp = 0f, float oscFreq = 0f)
        {
            stealthDirection = dir.sqrMagnitude > 0.001f ? dir.normalized : Vector3.down;
            stealthSpeed = moveSpeed > 0.1f ? moveSpeed : 3.5f;
            stealthOscillationAmp = oscAmp;
            stealthOscillationFreq = oscFreq;
            stealthSpawnTime = Time.time;
        }

        /// <summary>Hostile enemies still alive (bonus pods excluded).</summary>
        public static int ActiveHostileCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < Active.Count; i++)
                {
                    var e = Active[i];
                    if (e != null && !e.IsDead && !e.isBonusPod) n++;
                }
                return n;
            }
        }

        /// <summary>Forces any hostiles lingering off-screen back into view or despawns them to avoid soft-locks.</summary>
        public static void ForceAllHostilesOnScreenOrDespawn()
        {
            for (int i = Active.Count - 1; i >= 0; i--)
            {
                if (i >= Active.Count) continue;
                var e = Active[i];
                if (e == null || e.IsDead || e.isBonusPod) continue;

                if (!PlayfieldBounds.IsOnScreen(e.transform.position, 0.4f))
                {
                    if (e.isInFormation && e.formationGrid != null)
                    {
                        if (e.diveCoroutine != null) { e.StopCoroutine(e.diveCoroutine); e.diveCoroutine = null; }
                        e.transform.position = e.formationGrid.GetSlotWorldPosition(e.formationRow, e.formationCol);
                        e.transform.rotation = Quaternion.identity;
                        e.isStationed = true;
                        e.isDiving = false;
                        e.isReturning = false;
                    }
                    else
                    {
                        e.DespawnSelf();
                    }
                }
            }
        }

        // ------------------------------------------------------------------
        // Lifecycle
        // ------------------------------------------------------------------

        protected virtual void Awake()
        {
            baseScale = transform.localScale;
            // Ensure enemy planes maintain clear readability and prominence on mobile screens
            if (Application.isMobilePlatform && baseScale.x < 0.85f)
            {
                baseScale *= 1.8f;
                transform.localScale = baseScale;
            }

            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (spriteRenderer != null) originalColor = spriteRenderer.color;
            CacheBaseStats();
        }

        private void CacheBaseStats()
        {
            if (baseCached) return;
            baseCached = true;
            baseMaxHealth = maxHealth;
            baseScoreValue = scoreValue;
            baseDropChance = dropChance;
            baseProjectileSpeed = projectileSpeed;
        }

        protected virtual void OnEnable()
        {
            if (!Active.Contains(this)) Active.Add(this);
        }

        protected virtual void OnDisable()
        {
            Active.Remove(this);
        }

        public virtual void OnSpawn()
        {
            CacheBaseStats();
            maxHealth = baseMaxHealth;
            scoreValue = baseScoreValue;
            dropChance = baseDropChance;
            projectileSpeed = baseProjectileSpeed;
            currentHealth = maxHealth;

            isInFormation = false;
            formationGrid = null;
            isStationed = false;
            isDiving = false;
            isReturning = false;
            isBusy = false;
            transformOwned = false;
            isRaiding = false;
            isBonusPod = false;
            freeTimer = 0f;
            offscreenTimer = 0f;
            podPhase = Random.Range(0f, 6.28f);
            shieldCarry = 0f;
            lastStandActive = false;
            panicTimer = 0f;
            shielderPulseTimer = Random.Range(0.1f, 0.4f);
            lastProtectedPopupTime = -1f;
            Role = EnemyRole.Grunt;
            transform.rotation = Quaternion.identity;
            transform.localScale = baseScale;

            if (targetLayer.value == 0) targetLayer = LayerMask.GetMask("Player");

            StopAllCoroutines();
            diveCoroutine = null;
            actionCoroutine = null;
            warningCoroutine = null;
            hitFlashCoroutine = null;

            originalColor = GetSectorTint();
            if (spriteRenderer != null) spriteRenderer.color = originalColor;
            if (roleAura != null) roleAura.enabled = false;
            var existingAura = transform.Find("RoleAura");
            if (existingAura != null) existingAura.gameObject.SetActive(false);
        }

        public virtual void OnDespawn()
        {
            StopAllCoroutines();
            diveCoroutine = null;
            actionCoroutine = null;
            warningCoroutine = null;
            hitFlashCoroutine = null;
            isDiving = false;
            isReturning = false;
            isBusy = false;
            transformOwned = false;
            isRaiding = false;
            isBonusPod = false;
            transform.rotation = Quaternion.identity;
            transform.localScale = baseScale;

            if (isInFormation && formationGrid != null)
            {
                formationGrid.NotifyMemberDefeated(formationRow, formationCol);
            }
            isInFormation = false;
        }

        protected static int CurrentLevel()
        {
            return Starfall.Levels.LevelManager.Instance != null ? Starfall.Levels.LevelManager.Instance.CurrentLevelNumber : 1;
        }

        protected Color GetSectorTint()
        {
            return CurrentLevel() switch
            {
                2 => new Color(0.93f, 0.84f, 1f),   // Asteroid Belt: violet cast
                3 => new Color(0.82f, 1f, 0.92f),   // Deep Nebula: bio-teal cast
                4 => new Color(1f, 0.82f, 0.68f),   // War-Torn Wreckage: ember cast
                _ => Color.white
            };
        }

        // ------------------------------------------------------------------
        // Configuration from spawner / wave
        // ------------------------------------------------------------------

        /// <summary>Applies a tactical role: tint, aura, health and score. Call after SetWaveTier.</summary>
        public void SetRole(EnemyRole role)
        {
            Role = role;
            var profile = EnemyRoleProfile.Get(role);
            if (role != EnemyRole.Grunt)
            {
                maxHealth = Mathf.Max(1, Mathf.RoundToInt(maxHealth * profile.healthMultiplier));
                currentHealth = Mathf.Max(1, Mathf.RoundToInt(currentHealth * profile.healthMultiplier));
                scoreValue = Mathf.RoundToInt(scoreValue * profile.scoreMultiplier);
                originalColor = profile.tint;
                if (spriteRenderer != null) spriteRenderer.color = originalColor;

                // NOTE: Role ring/circle auras removed per user request (distracts from gameplay/UI).
                // Ship sprite body tint is retained for clear role identification.
            }

            if (roleAura != null)
            {
                roleAura.enabled = false;
            }
            var existingAura = transform.Find("RoleAura");
            if (existingAura != null)
            {
                existingAura.gameObject.SetActive(false);
            }
        }

        public void SetAsBonusPod(bool isBonus)
        {
            isBonusPod = isBonus;
            if (isBonus)
            {
                maxHealth = 1;
                currentHealth = 1;
                scoreValue = 500;
                dropChance = 1f;
                originalColor = new Color(1f, 0.85f, 0.15f);
                if (spriteRenderer != null) spriteRenderer.color = originalColor;
            }
        }

        /// <summary>Scales health and projectile speed for the active wave (non-compounding).</summary>
        public virtual void SetWaveTier(int waveNumber, float healthMultiplier = 1f, float speedMultiplier = 1f)
        {
            maxHealth = Mathf.Max(1, Mathf.RoundToInt(baseMaxHealth * healthMultiplier));
            currentHealth = maxHealth;
            projectileSpeed = Mathf.Max(3f, baseProjectileSpeed * speedMultiplier);
        }

        /// <summary>Assigns this enemy to a formation slot. Position it at its entry start first.</summary>
        public void AssignToFormation(FormationGridController grid, int row, int col, EntryFlightPath flightPath = EntryFlightPath.DirectDrop)
        {
            isInFormation = true;
            formationGrid = grid;
            formationRow = row;
            formationCol = col;
            isStationed = false;
            entryFlightPath = flightPath;
            entryStartPos = transform.position;
            entryLogicalPos = transform.position;
            entryTimer = 0f;
            totalEntryDist = grid != null ? Mathf.Max(0.5f, Vector3.Distance(entryStartPos, grid.GetSlotWorldPosition(row, col))) : 1f;
            grid?.RegisterMember(row, col, this);

            if (flightPath != EntryFlightPath.DirectDrop && Time.time - lastEntrySfxTime >= 0.35f)
            {
                lastEntrySfxTime = Time.time;
                Starfall.Audio.AudioManager.Instance?.PlayFormationEntry();
            }
        }

        public void SetEntryFlightPath(EntryFlightPath flightPath)
        {
            entryFlightPath = flightPath;
            entryStartPos = transform.position;
            entryLogicalPos = transform.position;
            entryTimer = 0f;
            if (formationGrid != null)
                totalEntryDist = Mathf.Max(0.5f, Vector3.Distance(entryStartPos, formationGrid.GetSlotWorldPosition(formationRow, formationCol)));
        }

        // ------------------------------------------------------------------
        // Visibility
        // ------------------------------------------------------------------

        /// <summary>Enemies only fire when clearly on screen and inside the combat lane.</summary>
        public virtual bool IsVisibleToPlayer() => PlayfieldBounds.IsInCombatZone(transform.position);

        // ------------------------------------------------------------------
        // Main update
        // ------------------------------------------------------------------

        protected virtual void Update()
        {
            using var __perf = Starfall.Core.PerfMarkers.EnemyUpdate.Auto();
            if (IsDead) return;

            if (Role == EnemyRole.Shielder)
            {
                UpdateShielderProjection();
            }

            if (panicTimer > 0f)
            {
                panicTimer -= TimeWarpManager.EnemyDeltaTime;
                if (panicTimer <= 0f && spriteRenderer != null)
                    spriteRenderer.color = originalColor;
            }

            if (isBonusPod) { UpdateBonusPodMovement(); return; }

            if (Starfall.Core.WaveManager.IsStealthWaveActive && !Starfall.Core.WaveManager.StealthAlarmTriggered)
            {
                if (spriteRenderer != null) spriteRenderer.color = new Color(0.2f, 0.45f, 1f, 0.9f); // Dark blue sleeping tint
                float dt = TimeWarpManager.EnemyDeltaTime;
                Vector3 pos = transform.position;
                pos += stealthDirection * (stealthSpeed * dt);
                if (stealthOscillationAmp > 0.01f)
                {
                    pos.x += Mathf.Sin((Time.time - stealthSpawnTime) * stealthOscillationFreq) * (stealthOscillationAmp * dt * 2.5f);
                }
                transform.position = pos;

                if (stealthDirection != Vector3.zero)
                {
                    float angle = Mathf.Atan2(stealthDirection.y, stealthDirection.x) * Mathf.Rad2Deg + 90f;
                    transform.rotation = Quaternion.Euler(0f, 0f, angle);
                }

                if (transform.position.y < PlayfieldBounds.Bottom - 3f ||
                    transform.position.x < PlayfieldBounds.CameraLeft - 3f ||
                    transform.position.x > PlayfieldBounds.CameraRight + 3f)
                {
                    DespawnSelf();
                }
                return;
            }
            if (Starfall.Core.WaveManager.IsStealthWaveActive && Starfall.Core.WaveManager.StealthAlarmTriggered)
            {
                if (spriteRenderer != null) spriteRenderer.color = new Color(1f, 0.1f, 0.1f, 1f); // Angry red tint
            }

            // Off-screen watchdog: ensure all active enemies stay on screen and never soft-lock the wave
            if (!PlayfieldBounds.IsOnScreen(transform.position, 0.4f))
            {
                offscreenTimer += Time.deltaTime;
                if (offscreenTimer > 3.0f)
                {
                    if (isInFormation && formationGrid != null)
                    {
                        if (diveCoroutine != null) { StopCoroutine(diveCoroutine); diveCoroutine = null; }
                        transform.position = formationGrid.GetSlotWorldPosition(formationRow, formationCol);
                        transform.rotation = Quaternion.identity;
                        isStationed = true;
                        isDiving = false;
                        isReturning = false;
                        offscreenTimer = 0f;
                    }
                    else if (isRaiding)
                    {
                        transform.position = new Vector3(Mathf.Clamp(transform.position.x, PlayfieldBounds.CameraLeft + 0.8f, PlayfieldBounds.CameraRight - 0.8f), PlayfieldBounds.Top - 0.5f, 0f);
                        offscreenTimer = 0f;
                    }
                    else if (offscreenTimer > 5.0f)
                    {
                        DespawnSelf();
                    }
                }
            }
            else
            {
                offscreenTimer = 0f;
            }

            if (isDiving) return; // driven by coroutine

            if (isInFormation && formationGrid != null)
            {
                UpdateFormationMovement();
                return;
            }

            if (!isRaiding)
            {
                // Anything spawned outside a formation (boss scout bays, legacy spawns) becomes a raider.
                BeginRaid();
            }
        }

        /// <summary>Optional per-type behaviour when not in formation (not used by default).</summary>
        protected virtual void UpdateFreeMovement() { }

        protected virtual void UpdateFormationMovement()
        {
            if (transformOwned) return; // blink / charge routines position the enemy themselves

            Vector3 targetPos = formationGrid.GetSlotWorldPosition(formationRow, formationCol);
            float dt = TimeWarpManager.EnemyDeltaTime;

            if (!isStationed)
            {
                entryTimer += dt;
                entryLogicalPos = Vector3.MoveTowards(entryLogicalPos, targetPos, FormationEntrySpeed * dt);
                float dist = Vector3.Distance(entryLogicalPos, targetPos);
                float t = Mathf.Clamp01(1f - (dist / Mathf.Max(0.5f, totalEntryDist)));

                Vector3 visual = entryLogicalPos;
                switch (entryFlightPath)
                {
                    case EntryFlightPath.SineWaveWeave:
                    {
                        float fade = Mathf.Clamp01(dist / 2.0f);
                        float w = Mathf.Sin(entryTimer * 6.0f + formationCol * 0.45f) * 0.45f * fade;
                        if (formationGrid.EntryStyle == FormationEntryStyle.Drop) visual.x += w;
                        else visual.y += w * 0.35f;
                        break;
                    }
                    case EntryFlightPath.FlankArcSwoop:
                    {
                        // Dramatic Galaga-style swoop: dives down toward the playfield center then arcs up into position
                        float arcDip = Mathf.Sin(t * Mathf.PI) * 2.2f;
                        float sideSign = entryStartPos.x >= 0f ? 1f : -1f;
                        float arcSway = Mathf.Sin(t * Mathf.PI * 1.5f) * 1.3f * sideSign;
                        visual.y -= arcDip;
                        visual.x += arcSway;
                        break;
                    }
                    case EntryFlightPath.SpiralLoop:
                    {
                        // 360-degree loop/corkscrew mid-flight
                        if (t > 0.15f && t < 0.85f)
                        {
                            float loopPhase = (t - 0.15f) / 0.7f;
                            float angle = loopPhase * Mathf.PI * 2f;
                            float loopRadius = Mathf.Sin(loopPhase * Mathf.PI) * 1.2f;
                            float dir = (formationCol % 2 == 0) ? 1f : -1f;
                            visual.x += Mathf.Cos(angle) * loopRadius * dir;
                            visual.y += (Mathf.Sin(angle) - 0.3f) * loopRadius;
                        }
                        break;
                    }
                    default:
                        break;
                }

                // Keep visual clamped within screen bounds so enemies stay visible
                visual.x = Mathf.Clamp(visual.x, PlayfieldBounds.CameraLeft + 0.4f, PlayfieldBounds.CameraRight - 0.4f);
                visual.y = Mathf.Min(visual.y, PlayfieldBounds.Top + 0.5f);

                Vector3 delta = visual - transform.position;
                transform.position = visual;
                if (delta.sqrMagnitude > 0.00001f && dt > 0f)
                {
                    float bank = Mathf.Clamp(-delta.x / dt * 3.2f, -38f, 38f);
                    float targetRotZ = bank;
                    if (entryFlightPath == EntryFlightPath.SpiralLoop || entryFlightPath == EntryFlightPath.FlankArcSwoop)
                    {
                        float headingAngle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg + 90f;
                        targetRotZ = Mathf.LerpAngle(headingAngle, bank, 0.45f);
                    }
                    targetRotZ = Mathf.Lerp(0f, targetRotZ, Mathf.Clamp01(dist / 0.4f));
                    transform.rotation = Quaternion.Euler(0f, 0f, targetRotZ);
                }

                // Safety net: never leave a member stuck off-screen (prevents wave soft-locks).
                if (dist < 0.08f || entryTimer > 3.0f)
                {
                    isStationed = true;
                    transform.position = targetPos;
                    transform.rotation = Quaternion.identity;
                }
            }
            else
            {
                transform.position = targetPos;
                transform.rotation = Quaternion.identity;
            }
        }

        // ------------------------------------------------------------------
        // Separation (free-flyers only)
        // ------------------------------------------------------------------

        /// <summary>Pushes a free-flying enemy away from any other enemy body closer than MinSeparation.</summary>
        protected Vector3 Separate(Vector3 pos, float dt)
        {
            const float minSep = FormationGridController.MinSeparation;
            Vector3 push = Vector3.zero;
            for (int i = 0; i < Active.Count; i++)
            {
                var o = Active[i];
                if (o == this || o == null || o.IsDead) continue;
                Vector3 d = pos - o.transform.position;
                float dist = d.magnitude;
                if (dist >= minSep) continue;
                if (dist < 0.001f) d = new Vector3(Random.Range(-1f, 1f), 0.2f, 0f);
                // Stationed members are immovable; free-flyers split the correction.
                float weight = o.IsFreeFlying ? 0.5f : 1f;
                push += d.normalized * (minSep - dist) * weight;
            }
            if (push == Vector3.zero) return pos;
            float maxStep = 9f * dt + 0.02f;
            return pos + Vector3.ClampMagnitude(push, maxStep);
        }

        // ------------------------------------------------------------------
        // Firing
        // ------------------------------------------------------------------

        protected Projectile SpawnEnemyBullet(Vector3 pos, Vector2 dir, int damage, float speedValue)
        {
            if (ObjectPool.Instance == null) return null;
            var bullet = ObjectPool.Instance.Spawn(projectilePoolKey, pos, Quaternion.identity);
            if (bullet != null && bullet.TryGetComponent<Projectile>(out var proj))
            {
                if (targetLayer.value == 0) targetLayer = LayerMask.GetMask("Player");
                proj.Initialize(dir, damage, targetLayer, false, speedValue);
                return proj;
            }
            return null;
        }

        public void WakeUpAndExecute()
        {
            if (IsDead) return;
            isDiving = false;
            isInFormation = false;
            isBusy = false;
            if (actionCoroutine != null) StopCoroutine(actionCoroutine);
            
            // Turn bright red
            if (spriteRenderer != null) spriteRenderer.color = new Color(1f, 0f, 0f, 1f);

            // Execute the player
            FireAimed(PlayerPosition(), 100, 2f);
            FireFan(PlayerPosition(), 5, 100, 2f);
        }

        public virtual void FireDownwards(int damage = 1, float speedMultiplier = 1f)
        {
            if (panicTimer > 0f) return;
            if (Starfall.Core.WaveManager.IsStealthWaveActive && !Starfall.Core.WaveManager.StealthAlarmTriggered) return;
            if (ObjectPool.Instance == null || IsDead || !gameObject.activeInHierarchy || isBonusPod) return;
            if (!IsVisibleToPlayer()) return;
            Starfall.Audio.AudioManager.Instance?.PlayEnemyLaser();
            SpawnEnemyBullet(transform.position + new Vector3(0f, -0.45f, 0f), Vector2.down, damage, projectileSpeed * speedMultiplier);
        }

        /// <summary>Curtain variant: skips the per-shot sound so a full curtain isn't a wall of noise.</summary>
        public void FireDownwards(int damage, float speedMultiplier, bool silent)
        {
            if (Starfall.Core.WaveManager.IsStealthWaveActive && !Starfall.Core.WaveManager.StealthAlarmTriggered) return;
            if (!silent) { FireDownwards(damage, speedMultiplier); return; }
            if (ObjectPool.Instance == null || IsDead || !gameObject.activeInHierarchy || isBonusPod) return;
            if (!IsVisibleToPlayer()) return;
            SpawnEnemyBullet(transform.position + new Vector3(0f, -0.45f, 0f), Vector2.down, damage, projectileSpeed * speedMultiplier);
        }

        public virtual void FireAimed(Vector2 targetPosition, int damage = 1, float speedMultiplier = 1f)
        {
            if (panicTimer > 0f) return;
            if (Starfall.Core.WaveManager.IsStealthWaveActive && !Starfall.Core.WaveManager.StealthAlarmTriggered) return;
            if (ObjectPool.Instance == null || IsDead || !gameObject.activeInHierarchy || isBonusPod) return;
            if (!IsVisibleToPlayer()) return;

            Starfall.Audio.AudioManager.Instance?.PlayEnemyLaser();
            Vector3 spawnPos = transform.position + new Vector3(0f, -0.45f, 0f);
            Vector2 dir = (targetPosition - (Vector2)spawnPos).normalized;
            if (dir.sqrMagnitude < 0.001f) dir = Vector2.down;
            SpawnEnemyBullet(spawnPos, dir, damage, projectileSpeed * speedMultiplier);
        }

        /// <summary>Fan of bullets centred on the target (Elites, mini-bosses).</summary>
        public void FireFan(Vector3 target, int count, int damage, float speedMultiplier)
        {
            if (Starfall.Core.WaveManager.IsStealthWaveActive && !Starfall.Core.WaveManager.StealthAlarmTriggered) return;
            if (ObjectPool.Instance == null || IsDead || !gameObject.activeInHierarchy || isBonusPod) return;
            if (!IsVisibleToPlayer()) return;

            Starfall.Audio.AudioManager.Instance?.PlayEnemyLaser();
            Vector3 origin = transform.position + new Vector3(0f, -0.45f, 0f);
            Vector2 baseDir = ((Vector2)(target - origin)).normalized;
            if (baseDir.y > -0.25f) baseDir = new Vector2(baseDir.x, -0.25f).normalized;
            float spread = count >= 5 ? 16f : 20f;
            float start = -spread * (count - 1) * 0.5f;
            for (int i = 0; i < count; i++)
            {
                Vector2 d = Quaternion.Euler(0f, 0f, start + spread * i) * baseDir;
                SpawnEnemyBullet(origin, d, damage, projectileSpeed * speedMultiplier);
            }
            VFXKit.Glow(origin, new Color(1f, 0.85f, 0.3f, 0.8f), 0.9f, 0.18f);
        }

        /// <summary>Brief colour pulse used to telegraph an incoming attack.</summary>
        public void FlashWarning(Color color, float duration)
        {
            if (!gameObject.activeInHierarchy || spriteRenderer == null) return;
            if (warningCoroutine != null) StopCoroutine(warningCoroutine);
            warningCoroutine = StartCoroutine(WarningRoutine(color, duration));
        }

        private IEnumerator WarningRoutine(Color color, float duration)
        {
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float blink = 0.5f + 0.5f * Mathf.Sin(t * 30f);
                if (spriteRenderer != null) spriteRenderer.color = Color.Lerp(originalColor, color, blink);
                yield return null;
            }
            if (spriteRenderer != null) spriteRenderer.color = originalColor;
            warningCoroutine = null;
        }

        // ------------------------------------------------------------------
        // Role specials
        // ------------------------------------------------------------------

        /// <summary>Gunner: laser sight locks on, then a 3-round burst at the locked point.</summary>
        public void StartGunnerBurst(Vector3 target, int damage, float speedMultiplier)
        {
            if (IsBusy || IsDead || !gameObject.activeInHierarchy) return;
            actionCoroutine = StartCoroutine(GunnerBurstRoutine(target, damage, speedMultiplier));
        }

        private IEnumerator GunnerBurstRoutine(Vector3 target, int damage, float speedMultiplier)
        {
            isBusy = true;
            Vector3 origin = transform.position;
            Vector3 dir = (target - origin).normalized;
            if (dir.y > -0.2f) dir = new Vector3(dir.x, -0.2f, 0f).normalized;
            Vector3 lineEnd = origin + dir * 14f;
            VFXKit.Telegraph(origin, lineEnd, new Color(0.3f, 1f, 0.5f, 0.55f), 0.6f, 0.12f);
            FlashWarning(new Color(0.4f, 1f, 0.6f), 0.6f);
            yield return new WaitForSeconds(0.6f);

            for (int i = 0; i < 3 && !IsDead; i++)
            {
                if (IsVisibleToPlayer())
                {
                    Starfall.Audio.AudioManager.Instance?.PlayEnemyLaser();
                    SpawnEnemyBullet(transform.position + dir * 0.45f, dir, damage, projectileSpeed * speedMultiplier * 1.25f);
                }
                yield return new WaitForSeconds(0.1f);
            }
            isBusy = false;
            actionCoroutine = null;
        }

        /// <summary>Bomber: drops a slow mine that bursts into a ring of bullets.</summary>
        public void DropMine()
        {
            if (IsBusy || IsDead || !IsVisibleToPlayer()) return;
            FlashWarning(new Color(0.9f, 0.5f, 1f), 0.3f);
            var proj = SpawnEnemyBullet(transform.position + new Vector3(0f, -0.5f, 0f), Vector2.down, 1, 1.8f);
            if (proj != null)
            {
                int ring = CurrentLevel() >= 3 ? 10 : 8;
                proj.ConfigureMine(Random.Range(1.1f, 1.5f), ring, 3.6f + CurrentLevel() * 0.25f);
            }
        }

        /// <summary>Blinker: phases out, reappears in another free slot, snaps an aimed shot.</summary>
        public void StartBlink()
        {
            if (IsBusy || IsDead || !isInFormation || formationGrid == null) return;
            actionCoroutine = StartCoroutine(BlinkRoutine());
        }

        private IEnumerator BlinkRoutine()
        {
            isBusy = true;
            transformOwned = true;
            Color c = originalColor;
            VFXKit.Ring(transform.position, new Color(1f, 0.4f, 0.9f, 0.8f), 0.3f, 1.4f, 0.3f);

            for (float t = 0f; t < 0.3f; t += Time.deltaTime)
            {
                if (spriteRenderer != null) spriteRenderer.color = new Color(c.r, c.g, c.b, 1f - t / 0.3f);
                transform.position = formationGrid.GetSlotWorldPosition(formationRow, formationCol);
                yield return null;
            }

            int to = formationGrid.RelocateMember(this, formationCol);
            if (to >= 0) formationCol = to;
            transform.position = formationGrid.GetSlotWorldPosition(formationRow, formationCol);
            VFXKit.Ring(transform.position, new Color(1f, 0.4f, 0.9f, 0.8f), 1.4f, 0.3f, 0.25f);

            for (float t = 0f; t < 0.25f; t += Time.deltaTime)
            {
                if (spriteRenderer != null) spriteRenderer.color = new Color(c.r, c.g, c.b, t / 0.25f);
                transform.position = formationGrid.GetSlotWorldPosition(formationRow, formationCol);
                yield return null;
            }
            if (spriteRenderer != null) spriteRenderer.color = c;

            transformOwned = false;
            var player = Starfall.Player.PlayerController.Instance;
            if (player != null) FireAimed(player.transform.position, 1, 1.15f);

            isBusy = false;
            actionCoroutine = null;
        }

        /// <summary>Charger: telegraphed ram along a locked line, then returns to formation.</summary>
        public void StartChargeRun(Vector3 playerTargetPos)
        {
            if (IsBusy || IsDead || !gameObject.activeInHierarchy || isBonusPod) return;
            if (diveCoroutine != null) StopCoroutine(diveCoroutine);
            diveCoroutine = StartCoroutine(ChargeRoutine());
        }

        private IEnumerator ChargeRoutine()
        {
            isBusy = true;
            transformOwned = true;
            float telegraph = CurrentLevel() >= 3 ? 0.6f : 0.75f;
            FlashWarning(new Color(1f, 0.15f, 0.1f), telegraph);
            Starfall.Audio.AudioManager.Instance?.PlayDiveBomberSwoop(0.9f);

            // Hold in slot and shiver while the target is being chosen.
            float t = 0f;
            Vector3 lockTarget = Vector3.zero;
            bool locked = false;
            while (t < telegraph)
            {
                t += TimeWarpManager.EnemyDeltaTime;
                Vector3 slot = formationGrid != null ? formationGrid.GetSlotWorldPosition(formationRow, formationCol) : transform.position;
                transform.position = slot + (Vector3)(Random.insideUnitCircle * 0.05f);

                // Lock the target partway through so a late sidestep dodges it.
                if (!locked && t >= telegraph * 0.45f)
                {
                    locked = true;
                    var p = Starfall.Player.PlayerController.Instance;
                    lockTarget = p != null ? p.transform.position : new Vector3(0f, -4f, 0f);
                    Vector3 dirPreview = (lockTarget - transform.position).normalized;
                    VFXKit.Telegraph(transform.position, transform.position + dirPreview * 16f, new Color(1f, 0.2f, 0.15f, 0.6f), telegraph * 0.55f, 0.32f);
                }
                if (IsDead) yield break;
                yield return null;
            }

            isDiving = true;
            transformOwned = false;
            Vector3 dir = (lockTarget - transform.position).normalized;
            if (dir.y > -0.3f) dir = new Vector3(dir.x, -0.3f, 0f).normalized;
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 90f);
            float chargeSpeed = 11.5f + CurrentLevel() * 0.8f;

            while (PlayfieldBounds.IsOnScreen(transform.position, 1f))
            {
                if (IsDead) yield break;
                float dt = TimeWarpManager.EnemyDeltaTime;
                transform.position = Separate(transform.position + dir * chargeSpeed * dt, dt);
                yield return null;
            }

            isBusy = false;
            yield return ReturnToSlotRoutine();
        }

        // ------------------------------------------------------------------
        // Dives
        // ------------------------------------------------------------------

        public virtual void StartDiveRun(Vector3 playerTargetPos) => StartDiveRun(playerTargetPos, DivePatternType.StandardSwoop);

        public virtual void StartDiveRun(Vector3 playerTargetPos, DivePatternType pattern)
        {
            if (IsBusy || IsDead || !gameObject.activeInHierarchy || isBonusPod) return;
            if (diveCoroutine != null) StopCoroutine(diveCoroutine);
            currentDivePattern = pattern;
            diveCoroutine = StartCoroutine(DiveRunRoutine(playerTargetPos, pattern));

            if (Time.time - lastDiveSfxTime >= 0.22f)
            {
                lastDiveSfxTime = Time.time;
                Starfall.Audio.AudioManager.Instance?.PlayDiveBomberSwoop();
            }
        }

        private Vector3 PlayerPosition()
        {
            var p = Starfall.Player.PlayerController.Instance;
            return p != null ? p.transform.position : new Vector3(0f, -4f, 0f);
        }

        protected virtual IEnumerator DiveRunRoutine(Vector3 playerTargetPos, DivePatternType pattern)
        {
            isDiving = true;
            float exitY = PlayfieldBounds.Bottom - 0.8f;
            float diveVelocity = Mathf.Max(diveSpeed, speed * 1.6f);
            if (pattern == DivePatternType.HighSpeedStrafe) diveVelocity *= 1.6f;

            if (pattern == DivePatternType.FlankCrossfireDive)
            {
                float half = PlayfieldBounds.PlayHalfWidth;
                float crossX = transform.position.x <= 0f ? half * 0.8f : -half * 0.8f;
                Vector3 dir = (new Vector3(crossX, exitY, 0f) - transform.position).normalized;
                transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 90f);

                bool fired = false;
                while (transform.position.y > exitY)
                {
                    if (IsDead) yield break;
                    float dt = TimeWarpManager.EnemyDeltaTime;
                    transform.position = Separate(transform.position + dir * diveVelocity * dt, dt);
                    if (!fired && transform.position.y <= PlayfieldBounds.FormationBottom - 0.8f)
                    {
                        fired = true;
                        FireAimed(PlayerPosition(), 1, 1.2f);
                    }
                    yield return null;
                }
            }
            else if (pattern == DivePatternType.LoopingDive)
            {
                float loopY = PlayfieldBounds.FormationBottom - 1.9f;
                Vector3 dir = (new Vector3(playerTargetPos.x, loopY, 0f) - transform.position).normalized;
                transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 90f);

                while (transform.position.y > loopY)
                {
                    if (IsDead) yield break;
                    float dt = TimeWarpManager.EnemyDeltaTime;
                    transform.position = Separate(transform.position + dir * diveVelocity * dt, dt);
                    yield return null;
                }

                float sign = transform.position.x < 0f ? -1f : 1f;
                Vector3 center = transform.position + new Vector3(-sign * 0.9f, 0f, 0f);
                const float loopDuration = 0.85f;
                bool f1 = false, f2 = false;
                for (float lt = 0f; lt < loopDuration;)
                {
                    if (IsDead) yield break;
                    lt += TimeWarpManager.EnemyDeltaTime;
                    float p = Mathf.Clamp01(lt / loopDuration);
                    float ang = p * Mathf.PI * 2f;
                    transform.position = center + new Vector3(Mathf.Cos(ang) * 0.9f * sign, Mathf.Sin(ang) * 0.9f, 0f);
                    transform.rotation = Quaternion.Euler(0f, 0f, -sign * p * 360f);
                    if (!f1 && p >= 0.25f) { f1 = true; FireDownwards(1, 1.1f); }
                    if (!f2 && p >= 0.65f) { f2 = true; FireAimed(PlayerPosition(), 1, 1.2f); }
                    yield return null;
                }

                transform.rotation = Quaternion.identity;
                while (transform.position.y > exitY)
                {
                    if (IsDead) yield break;
                    float dt = TimeWarpManager.EnemyDeltaTime;
                    transform.position = Separate(transform.position + Vector3.down * diveVelocity * dt, dt);
                    yield return null;
                }
            }
            else if (pattern == DivePatternType.KamikazeHoming)
            {
                if (spriteRenderer != null) spriteRenderer.color = new Color(1f, 0.3f, 0.3f);
                float lockOnTime = 1.2f;
                float elapsed = 0f;
                Vector3 currentDir = (PlayerPosition() - transform.position).normalized;

                while (transform.position.y > exitY)
                {
                    if (IsDead) yield break;
                    float dt = TimeWarpManager.EnemyDeltaTime;
                    
                    if (elapsed < lockOnTime)
                    {
                        elapsed += dt;
                        Vector3 targetDir = (PlayerPosition() - transform.position).normalized;
                        if (targetDir.y > -0.2f) targetDir.y = -0.2f; // Force downwards movement
                        currentDir = Vector3.Lerp(currentDir, targetDir.normalized, dt * 4f).normalized;
                        diveVelocity = Mathf.MoveTowards(diveVelocity, speed * 0.8f, dt * 10f); // Slow down while aiming
                    }
                    else
                    {
                        diveVelocity = Mathf.MoveTowards(diveVelocity, speed * 4.0f, dt * 25f); // Massive acceleration
                    }

                    transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(currentDir.y, currentDir.x) * Mathf.Rad2Deg + 90f);
                    transform.position = Separate(transform.position + currentDir * diveVelocity * dt, dt);
                    yield return null;
                }
                if (spriteRenderer != null) spriteRenderer.color = Color.white;
            }
            else
            {
                Vector3 dir = (new Vector3(playerTargetPos.x, exitY, 0f) - transform.position).normalized;
                transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 90f);
                bool s1 = false, s2 = false;
                while (transform.position.y > exitY)
                {
                    if (IsDead) yield break;
                    float dt = TimeWarpManager.EnemyDeltaTime;
                    transform.position = Separate(transform.position + dir * diveVelocity * dt, dt);
                    if (!s1 && transform.position.y <= PlayfieldBounds.FormationBottom - 0.6f)
                    {
                        s1 = true;
                        FireAimed(PlayerPosition(), 1, 1.15f);
                    }
                    if (pattern == DivePatternType.HighSpeedStrafe && !s2 && transform.position.y <= PlayfieldBounds.FormationBottom - 1.8f)
                    {
                        s2 = true;
                        FireDownwards(1, 1.3f);
                    }
                    yield return null;
                }
            }

            yield return ReturnToSlotRoutine();
        }

        /// <summary>
        /// Return path that never crosses the formation: enter from the nearest screen edge on a
        /// lane just below the formation, slide under the home slot, then rise straight up into it.
        /// The slot's lane is guaranteed clear because only front-line members are sent out.
        /// </summary>
        protected IEnumerator ReturnToSlotRoutine()
        {
            isDiving = true;
            isReturning = true;
            transform.rotation = Quaternion.identity;

            if (!isInFormation || formationGrid == null)
            {
                DespawnSelf();
                yield break;
            }

            Vector3 slot = formationGrid.GetSlotWorldPosition(formationRow, formationCol);
            float laneY = PlayfieldBounds.FormationBottom - 0.9f;
            float side = slot.x >= 0f ? 1f : -1f;
            float edge = Mathf.Clamp(Mathf.Max(PlayfieldBounds.CameraRight, -PlayfieldBounds.CameraLeft) - 0.2f, 2f, 9f);
            transform.position = new Vector3(side * edge, laneY, 0f);

            // Slide along the lane with a 2.5s guard timer
            float slideGuard = 0f;
            while (slideGuard < 2.5f)
            {
                if (IsDead) yield break;
                float dt = TimeWarpManager.EnemyDeltaTime;
                slideGuard += dt;
                slot = formationGrid.GetSlotWorldPosition(formationRow, formationCol);
                Vector3 target = new Vector3(slot.x, laneY, 0f);
                Vector3 next = Vector3.MoveTowards(transform.position, target, ReturnSpeed * dt);
                transform.position = Separate(next, dt);
                if (Mathf.Abs(transform.position.x - slot.x) < 0.2f) break;
                yield return null;
            }

            // Rise into the slot with a 2.5s guard timer
            float guard = 0f;
            while (guard < 2.5f)
            {
                if (IsDead) yield break;
                float dt = TimeWarpManager.EnemyDeltaTime;
                guard += dt;
                slot = formationGrid.GetSlotWorldPosition(formationRow, formationCol);
                transform.position = Vector3.MoveTowards(transform.position, slot, ReturnSpeed * dt);
                if (Vector3.Distance(transform.position, slot) < 0.1f) break;
                yield return null;
            }

            transform.position = formationGrid.GetSlotWorldPosition(formationRow, formationCol);
            transform.rotation = Quaternion.identity;
            isDiving = false;
            isReturning = false;
            isStationed = true;
            diveCoroutine = null;
        }

        // ------------------------------------------------------------------
        // Raiders (enemies spawned outside a formation, e.g. boss scout bays)
        // ------------------------------------------------------------------

        /// <summary>
        /// Last stand: the final few members of a wave glow hot red. Cadence changes are owned by
        /// FormationGridController; this only handles the visual tell. Safe to call repeatedly.
        /// </summary>
        public void EnterLastStand()
        {
            if (IsDead || lastStandActive || isBonusPod) return;
            lastStandActive = true;
            originalColor = Color.Lerp(originalColor, new Color(1f, 0.22f, 0.18f, 1f), 0.6f);
            if (spriteRenderer != null) spriteRenderer.color = originalColor;
            if (gameObject.activeInHierarchy)
            {
                VFXKit.Ring(transform.position, new Color(1f, 0.3f, 0.2f, 0.8f), 0.3f, 1.3f, 0.3f);
                FlashWarning(new Color(1f, 0.95f, 0.6f), 0.35f);
            }
        }

        /// <summary>
        /// Detaches this member from its formation (cancelling any dive / blink / charge in progress)
        /// and sends it on a final raid run. Used when reinforcements overlap the previous set.
        /// </summary>
        public void BreakFromFormation()
        {
            if (IsDead || !isInFormation || isBonusPod) return;

            if (diveCoroutine != null) { StopCoroutine(diveCoroutine); diveCoroutine = null; }
            if (actionCoroutine != null) { StopCoroutine(actionCoroutine); actionCoroutine = null; }

            // Released without notifying the grid: the grid clears its own slot table.
            isInFormation = false;
            formationGrid = null;
            isStationed = false;
            isDiving = false;
            isReturning = false;
            isBusy = false;
            transformOwned = false;
            transform.rotation = Quaternion.identity;

            EnterLastStand();
            if (spriteRenderer != null) spriteRenderer.color = originalColor; // restores alpha after a cut-off blink
            BeginRaid();
        }

        /// <summary>
        /// Panics and demoralizes this enemy (e.g. when its squad Commander is eliminated).
        /// Stops shooting, cancels pending special actions, and makes it take +50% critical damage.
        /// </summary>
        public void Panick(float duration)
        {
            if (IsDead || isBonusPod) return;
            panicTimer = duration;
            if (actionCoroutine != null) { StopCoroutine(actionCoroutine); actionCoroutine = null; isBusy = false; }
            if (spriteRenderer != null) spriteRenderer.color = new Color(1f, 0.85f, 0.2f, 1f);
            if (gameObject.activeInHierarchy)
            {
                VFXKit.Ring(transform.position, new Color(1f, 0.85f, 0.2f, 0.8f), 0.3f, 1.4f, 0.3f);
                FlashWarning(new Color(1f, 0.95f, 0.4f), 0.45f);
            }
        }

        public void BeginRaid()
        {
            if (isRaiding || IsDead) return;
            isRaiding = true;
            if (diveCoroutine != null) StopCoroutine(diveCoroutine);
            diveCoroutine = StartCoroutine(RaidRoutine());
        }

        private IEnumerator RaidRoutine()
        {
            // Drop to a hover point, hover briefly, telegraph, then swoop through the player's lane.
            float hoverY = Mathf.Min(transform.position.y, PlayfieldBounds.FormationBottom + 0.4f);
            float hoverX = Mathf.Clamp(transform.position.x, -PlayfieldBounds.PlayHalfWidth + 0.5f, PlayfieldBounds.PlayHalfWidth - 0.5f);
            Vector3 hover = new Vector3(hoverX, hoverY, 0f);

            while (Vector3.Distance(transform.position, hover) > 0.1f)
            {
                if (IsDead) yield break;
                float dt = TimeWarpManager.EnemyDeltaTime;
                transform.position = Separate(Vector3.MoveTowards(transform.position, hover, 6f * dt), dt);
                if (!PlayfieldBounds.IsOnScreen(transform.position, 3f)) break;
                yield return null;
            }

            float hold = Random.Range(0.4f, 0.9f);
            FlashWarning(new Color(1f, 0.3f, 0.2f), hold);
            for (float t = 0f; t < hold; t += TimeWarpManager.EnemyDeltaTime)
            {
                if (IsDead) yield break;
                transform.position = Separate(transform.position, TimeWarpManager.EnemyDeltaTime);
                yield return null;
            }

            Vector3 target = PlayerPosition();
            FireAimed(target, 1, 1.1f);
            Vector3 dir = (target - transform.position).normalized;
            if (dir.y > -0.35f) dir = new Vector3(dir.x, -0.35f, 0f).normalized;
            transform.rotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + 90f);

            float swoop = Mathf.Max(diveSpeed, 8f);
            while (PlayfieldBounds.IsOnScreen(transform.position, 1f))
            {
                if (IsDead) yield break;
                float dt = TimeWarpManager.EnemyDeltaTime;
                transform.position = Separate(transform.position + dir * swoop * dt, dt);
                yield return null;
            }
            if (transform.position.y <= PlayfieldBounds.Bottom)
            {
                Starfall.Core.WaveManager.Instance?.NotifyEnemyEscaped(this);
            }
            DespawnSelf();
        }

        public virtual void UpdateBonusPodMovement()
        {
            float dt = Time.deltaTime;
            float newX = transform.position.x + Mathf.Sin(Time.time * 2.8f + podPhase) * 1.5f * dt;
            float newY = transform.position.y - 2.8f * dt;
            transform.position = Separate(new Vector3(newX, newY, 0f), dt);

            if (transform.position.y < PlayfieldBounds.Bottom - 2.5f) DespawnSelf();
        }

        // ------------------------------------------------------------------
        // Damage & death
        // ------------------------------------------------------------------

        protected virtual void OnTriggerEnter2D(Collider2D other)
        {
            if (IsDead) return;
            var health = other.GetComponentInParent<Starfall.Player.PlayerHealth>();
            if (health == null) return;

            if (Starfall.Core.WaveManager.IsStealthWaveActive)
            {
                if (!Starfall.Core.WaveManager.StealthAlarmTriggered)
                {
                    Starfall.Core.WaveManager.TriggerStealthAlarm();
                }

                if (!health.IsDead)
                {
                    // Fatal collision with slumbering fleet! Instantly deals lethal damage to the player
                    health.TakeDamage(999, transform.position);
                    CameraShake.Instance?.Shake(0.5f, 0.5f);
                    Starfall.Audio.AudioManager.Instance?.PlayExplosion();
                }
                currentHealth = 0;
                SpawnDeathVFX();
                DespawnSelf();
                return;
            }

            if (!health.IsDead)
            {
                int dmg = Role == EnemyRole.Charger ? contactDamage + 1 : contactDamage;
                health.TakeDamage(dmg, transform.position);
                CameraShake.Instance?.Shake(0.3f, 0.35f);
            }
            // Rammed enemies explode but award no combo (prevents ram-farming).
            currentHealth = 0;
            SpawnDeathVFX();
            DespawnSelf();
        }

        /// <summary>True if a living Shielder (other than this enemy) is projecting a field over it.</summary>
        private bool IsShielded()
        {
            if (Role == EnemyRole.Shielder) return false;
            Vector3 p = transform.position;
            float r2 = ShieldFieldRadius * ShieldFieldRadius;
            for (int i = 0; i < Active.Count; i++)
            {
                var o = Active[i];
                if (o == null || o == this || o.IsDead || o.Role != EnemyRole.Shielder) continue;
                if ((o.transform.position - p).sqrMagnitude <= r2) return true;
            }
            return false;
        }

        private void UpdateShielderProjection()
        {
            if (Role != EnemyRole.Shielder || IsDead) return;
            shielderPulseTimer -= TimeWarpManager.EnemyDeltaTime;
            if (shielderPulseTimer > 0f) return;
            shielderPulseTimer = 0.65f;

            Vector3 myPos = transform.position;
            float r2 = ShieldFieldRadius * ShieldFieldRadius;
            int linkedAllies = 0;

            for (int i = 0; i < Active.Count; i++)
            {
                var ally = Active[i];
                if (ally == null || ally == this || ally.IsDead || ally.Role == EnemyRole.Shielder) continue;
                if ((ally.transform.position - myPos).sqrMagnitude <= r2)
                {
                    linkedAllies++;
                    // Smooth visual energy tether connecting Shielder to protected ally
                    VFXKit.Beam(myPos, ally.transform.position, new Color(0.35f, 0.85f, 1f, 0.45f), 0.32f, 0.05f);
                    // Subtle transient pulse ring on the ally
                    VFXKit.Ring(ally.transform.position, new Color(0.35f, 0.85f, 1f, 0.55f), 0.25f, 0.7f, 0.22f);
                }
            }

            if (linkedAllies > 0)
            {
                // Soft expanding pulse indicating shield projection range
                VFXKit.Ring(myPos, new Color(0.35f, 0.85f, 1f, 0.4f), 0.3f, ShieldFieldRadius, 0.32f);
            }
        }

        private void TriggerShieldOverload()
        {
            Vector3 myPos = transform.position;
            float r2 = ShieldFieldRadius * ShieldFieldRadius;
            int overloadCount = 0;

            VFXKit.Ring(myPos, new Color(0.3f, 0.9f, 1f, 0.95f), 0.4f, ShieldFieldRadius * 1.5f, 0.38f);
            VFXKit.Glow(myPos, new Color(0.4f, 0.95f, 1f, 0.9f), 2.2f, 0.3f);
            CameraShake.Instance?.Shake(0.2f, 0.2f);

            var snapshot = new List<EnemyBase>(Active);
            for (int i = 0; i < snapshot.Count; i++)
            {
                var ally = snapshot[i];
                if (ally == null || ally == this || ally.IsDead || ally.Role == EnemyRole.Shielder) continue;
                if ((ally.transform.position - myPos).sqrMagnitude <= r2)
                {
                    overloadCount++;
                    VFXKit.Ring(ally.transform.position, new Color(0.2f, 0.9f, 1f, 0.9f), 0.3f, 1.2f, 0.28f);
                    ally.TakeDamage(4, ally.transform.position);
                }
            }

            if (overloadCount > 0)
            {
                int overloadBonus = 150 * overloadCount;
                GameManager.Instance?.AddScore(overloadBonus);
                VFXKit.Popup(myPos + Vector3.up * 0.45f, $"SHIELD OVERLOAD x{overloadCount}! +{overloadBonus}", new Color(0.3f, 0.9f, 1f), 3.8f);
            }
        }

        private void TriggerBomberChainReaction()
        {
            if (activeBomberChainDepth >= 10) return;

            activeBomberChainDepth++;
            int currentDepth = activeBomberChainDepth;
            Vector3 pos = transform.position;

            Color bombColor = new Color(0.85f, 0.45f, 1f, 0.95f);
            VFXKit.Ring(pos, bombColor, 0.4f, 2.8f, 0.35f);
            VFXKit.Ring(pos, new Color(0.5f, 0.9f, 1f, 0.8f), 0.2f, 1.8f, 0.25f);
            VFXKit.Glow(pos, bombColor, 2.6f, 0.28f);
            CameraShake.Instance?.Shake(0.2f + 0.05f * Mathf.Min(currentDepth, 5), 0.2f);

            if (currentDepth > 1)
            {
                int chainBonus = 250 * currentDepth;
                GameManager.Instance?.AddScore(chainBonus);
                VFXKit.Popup(pos + Vector3.up * 0.5f, $"CHAIN REACTION x{currentDepth}! +{chainBonus}", new Color(1f, 0.85f, 0.25f), 4.2f);
            }
            else
            {
                VFXKit.Popup(pos + Vector3.up * 0.4f, "BOMBER DETONATION!", new Color(0.9f, 0.5f, 1f), 3.2f);
            }

            float blastRadius = 2.4f;
            float blastRadiusSqr = blastRadius * blastRadius;

            // Damage living hostiles in blast radius
            var snapshot = new List<EnemyBase>(Active);
            for (int i = 0; i < snapshot.Count; i++)
            {
                var victim = snapshot[i];
                if (victim == null || victim == this || victim.IsDead) continue;
                if ((victim.transform.position - pos).sqrMagnitude <= blastRadiusSqr)
                {
                    victim.TakeDamage(14, victim.transform.position);
                }
            }

            // Also detonate nearby destructible hazards (e.g. asteroids)
            var filter = new ContactFilter2D();
            filter.useTriggers = true;
            int hitCount = Physics2D.OverlapCircle(pos, blastRadius, filter, blastColliderList);
            for (int i = 0; i < hitCount; i++)
            {
                var col = blastColliderList[i];
                if (col == null || col.gameObject == gameObject) continue;
                if (col.TryGetComponent<AsteroidHazard>(out var hazard))
                {
                    hazard.TakeDamage(14, pos);
                }
            }

            activeBomberChainDepth--;
        }

        public virtual void TakeDamage(int damage, Vector2 hitPoint)
        {
            if (IsDead) return;

            if (panicTimer > 0f)
            {
                damage = Mathf.RoundToInt(damage * 1.5f);
            }

            if (IsShielded())
            {
                shieldCarry += damage * 0.5f;
                int applied = Mathf.FloorToInt(shieldCarry);
                shieldCarry -= applied;
                if (applied < damage)
                {
                    VFXKit.Ring(hitPoint, new Color(0.35f, 0.85f, 1f, 0.85f), 0.25f, 0.85f, 0.2f);
                    if (Time.time - lastProtectedPopupTime >= 0.4f)
                    {
                        lastProtectedPopupTime = Time.time;
                        VFXKit.Popup(transform.position + Vector3.up * 0.35f, "PROTECTED", new Color(0.4f, 0.85f, 1f), 2.4f);
                    }
                }
                damage = applied;
                if (damage <= 0) return;
            }

            Starfall.VFX.FloatingDamageText.Spawn(transform.position, damage);
            currentHealth -= damage;
            Starfall.Audio.AudioManager.Instance?.PlayHit();

            if (spriteRenderer != null && gameObject.activeInHierarchy)
            {
                if (hitFlashCoroutine != null) StopCoroutine(hitFlashCoroutine);
                hitFlashCoroutine = StartCoroutine(HitFlashRoutine());
            }

            if (currentHealth <= 0) Die();
        }

        protected virtual IEnumerator HitFlashRoutine()
        {
            transform.localScale = baseScale * 1.12f;
            spriteRenderer.color = hitFlashColor;
            yield return new WaitForSeconds(0.06f);
            if (warningCoroutine == null) spriteRenderer.color = originalColor;
            transform.localScale = baseScale;
            hitFlashCoroutine = null;
        }

        private void SpawnDeathVFX()
        {
            Vector3 pos = transform.position;
            if (ObjectPool.Instance != null && !string.IsNullOrEmpty(explosionPoolKey))
                ObjectPool.Instance.Spawn(explosionPoolKey, pos, Quaternion.identity);

            Color core = Role == EnemyRole.Grunt ? new Color(1f, 0.75f, 0.35f, 0.9f) : EnemyRoleProfile.Get(Role).auraColor;
            core.a = 0.9f;
            VFXKit.Glow(pos, core, Role == EnemyRole.Elite ? 1.8f : 1.2f, 0.22f);
            VFXKit.Ring(pos, new Color(core.r, core.g, core.b, 0.7f), 0.3f, Role == EnemyRole.Elite ? 2.4f : 1.5f, 0.3f);
            Starfall.Audio.AudioManager.Instance?.PlayExplosion();
        }

        protected virtual void Die()
        {
            OnEnemyKilled?.Invoke(this);
            int awarded = scoreValue;
            if (GameManager.Instance != null)
                awarded = GameManager.Instance.RegisterKill(scoreValue, transform.position);

            SpawnDeathVFX();
            if (Role == EnemyRole.Commander)
            {
                CameraShake.Instance?.Shake(0.35f, 0.35f);
                Starfall.Core.WaveManager.Instance?.NotifyCommanderKilled(this);
            }
            else if (Role == EnemyRole.Bomber)
            {
                TriggerBomberChainReaction();
            }
            else if (Role == EnemyRole.Shielder)
            {
                TriggerShieldOverload();
            }
            else if (Role == EnemyRole.Elite)
                CameraShake.Instance?.Shake(0.12f, 0.12f);

            // Interception bonus if defeated mid-flight before docking into formation slot
            if (!isStationed && isInFormation)
            {
                int interceptBonus = Mathf.RoundToInt(scoreValue * 0.5f);
                if (interceptBonus > 0 && GameManager.Instance != null)
                {
                    GameManager.Instance.AddScore(interceptBonus);
                    VFXKit.Popup(transform.position + Vector3.up * 0.45f, $"INTERCEPT +{interceptBonus}", new Color(1f, 0.85f, 0.2f), 3.5f);
                }
            }

            VFXKit.Popup(transform.position, $"+{awarded}", Role == EnemyRole.Grunt ? new Color(1f, 0.95f, 0.7f) : EnemyRoleProfile.Get(Role).tint, Role == EnemyRole.Elite ? 4f : 3f);

            TryDropPowerUp();
            DespawnSelf();
        }

        private void TryDropPowerUp()
        {
            if (ObjectPool.Instance == null) return;

            if (isBonusPod)
            {
                bool rare = Random.value < 0.2f;
                string key = rare ? PickPowerUpKey() : "PowerUp_Score";
                var spawned = ObjectPool.Instance.Spawn(key, transform.position, Quaternion.identity);
                if (spawned != null && key == "PowerUp_Score" && spawned.TryGetComponent<PowerUp>(out var pup))
                    pup.SetType(PowerUpType.StarCredits);
                return;
            }

            float chance = dropChance * DropChanceMultiplier * (Role == EnemyRole.Elite ? 2.5f : Role == EnemyRole.Grunt ? 1f : 1.5f);
            if (powerUpsDroppedThisWave < MaxDropsPerWave && Random.value <= chance)
            {
                powerUpsDroppedThisWave++;
                ObjectPool.Instance.Spawn(PickPowerUpKey(), transform.position, Quaternion.identity);
            }
        }

        /// <summary>Weighted power-up choice: weapon and shield are common, screen-changers are rare.</summary>
        private static string PickPowerUpKey()
        {
            float r = Random.value;
            if (r < 0.24f) return "PowerUp_Weapon";
            if (r < 0.44f) return "PowerUp_Shield";
            if (r < 0.58f) return "PowerUp_RapidFire";
            if (r < 0.68f) return "PowerUp_Score";
            if (r < 0.76f) return "PowerUp_HomingMissiles";
            if (r < 0.83f) return "PowerUp_Bomb";
            if (r < 0.89f) return "PowerUp_Wingman";
            if (r < 0.95f) return "PowerUp_TimeWarp";
            return "PowerUp_Magnet";
        }

        /// <summary>Legacy sector index (1..3) used by older scripts.</summary>
        protected int GetActiveSector()
        {
            int lvl = CurrentLevel();
            return lvl <= 1 ? 1 : lvl == 2 ? 2 : 3;
        }

        /// <summary>Instantly clears all active hostiles (used for Survive waves when the timer expires).</summary>
        public static void DespawnAllHostiles(bool withExplosion = true)
        {
            var copy = new List<EnemyBase>(Active);
            for (int i = 0; i < copy.Count; i++)
            {
                var e = copy[i];
                if (e != null && !e.IsDead && !e.isBonusPod)
                {
                    if (withExplosion) e.SpawnDeathVFX();
                    e.DespawnSelf();
                }
            }
        }

        protected void DespawnSelf()
        {
            if (ObjectPool.Instance != null) ObjectPool.Instance.Despawn(gameObject);
            else Destroy(gameObject);
        }
    }
}
