using System.Collections.Generic;
using UnityEngine;
using Starfall.Combat;
using Starfall.Core;
using Starfall.Audio;
using Starfall.VFX;

namespace Starfall.Commander
{
    /// <summary>
    /// Autonomous fleet combatant deployed in Fleet Commander battle mode.
    /// Operates on either the Player's team (flying UP) or Opponent's team (flying DOWN).
    /// Autonomously navigates, targets opposing units, fires plasma/missiles,
    /// and breaches the enemy baseline to inflict direct Core damage.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class SquadronUnit : MonoBehaviour, IDamageable
    {
        public static readonly List<SquadronUnit> ActiveUnits = new List<SquadronUnit>(64);

        [Header("State")]
        [SerializeField] private bool isPlayerTeam = true;
        [SerializeField] private int currentHealth;
        [SerializeField] private int maxHealth;
        private SquadronCardDef spec;

        // Visuals
        private SpriteRenderer spriteRenderer;
        private Transform healthBarFill;
        private GameObject healthBarRoot;
        private float hitFlashTimer;
        private static readonly Color HitFlashColor = new Color(1f, 0.3f, 0.3f, 1f);

        // Combat Timers
        private float fireTimer;
        private float lateralSwayTimer;
        private float lateralOffset;

        public bool IsPlayerTeam => isPlayerTeam;
        public int CurrentHealth => currentHealth;
        public int MaxHealth => maxHealth;
        public SquadronCardDef Spec => spec;

        public void Initialize(SquadronCardDef definition, bool playerTeam, Vector3 startPos)
        {
            spec = definition;
            isPlayerTeam = playerTeam;
            maxHealth = definition.maxHealth;
            currentHealth = maxHealth;
            transform.position = startPos;

            // Rotation & Orientation:
            // Player units face UP (identity); Opponent units face DOWN (180 deg around Z)
            transform.rotation = playerTeam ? Quaternion.identity : Quaternion.Euler(0f, 0f, 180f);

            // Scale based on ship role
            float baseScale = definition.id switch
            {
                SquadronUnitId.ViperSwarm => 0.55f,
                SquadronUnitId.TitanGunship => 0.95f,
                SquadronUnitId.SpecterSniper => 0.85f,
                SquadronUnitId.TempestCruiser => 1.15f,
                SquadronUnitId.PhoenixColossus => 1.45f,
                _ => 0.8f
            };
            transform.localScale = Vector3.one * baseScale;

            // Setup Sprite Renderer
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null) spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

            Color theme = playerTeam ? definition.themeColor : new Color(1f, 0.25f, 0.15f, 1f);
            spriteRenderer.sprite = WarshipSpriteUtility.GetShipSprite(
                $"{definition.shipClass}_{(playerTeam ? "P" : "E")}",
                definition.shipClass,
                theme
            );
            spriteRenderer.sortingLayerName = "Default";
            spriteRenderer.sortingOrder = 12;

            // Setup Collider2D & Layer
            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;

            // Layer: Layer 6 is Player; Layer 8 or 9 for Enemies
            gameObject.layer = playerTeam ? 6 : 8;
            gameObject.tag = playerTeam ? "Player" : "Enemy";

            // Build overhead mini-healthbar
            BuildMiniHealthBar();

            fireTimer = Random.Range(0.1f, definition.fireRate * 0.5f);
            lateralSwayTimer = Random.Range(0f, Mathf.PI * 2f);
            lateralOffset = Random.Range(-0.35f, 0.35f);

            if (!ActiveUnits.Contains(this))
                ActiveUnits.Add(this);
        }

        private static Sprite _unitWhitePixel;
        private static Sprite GetWhitePixel()
        {
            if (_unitWhitePixel != null) return _unitWhitePixel;
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false) { name = "Squadron_WhitePixel" };
            tex.SetPixel(0, 0, Color.white);
            tex.Apply(false, true);
            _unitWhitePixel = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f, 0, SpriteMeshType.FullRect);
            return _unitWhitePixel;
        }

        private void BuildMiniHealthBar()
        {
            if (healthBarRoot != null) Destroy(healthBarRoot);

            healthBarRoot = new GameObject("UnitHealthBar");
            healthBarRoot.transform.SetParent(transform, false);
            // Place healthbar above the unit depending on orientation
            healthBarRoot.transform.localPosition = new Vector3(0f, isPlayerTeam ? -0.65f : 0.65f, 0f);
            healthBarRoot.transform.localRotation = Quaternion.identity; // Keep text/bar upright

            // Background
            var bgGo = new GameObject("Bg");
            bgGo.transform.SetParent(healthBarRoot.transform, false);
            var bgSr = bgGo.AddComponent<SpriteRenderer>();
            bgSr.sprite = GetWhitePixel();
            bgSr.color = new Color(0f, 0f, 0f, 0.65f);
            bgSr.sortingOrder = 14;
            bgGo.transform.localScale = new Vector3(0.75f, 0.08f, 1f);

            // Fill
            var fillGo = new GameObject("Fill");
            fillGo.transform.SetParent(healthBarRoot.transform, false);
            var fillSr = fillGo.AddComponent<SpriteRenderer>();
            fillSr.sprite = GetWhitePixel();
            fillSr.color = isPlayerTeam ? new Color(0.15f, 0.9f, 1f, 0.9f) : new Color(1f, 0.25f, 0.2f, 0.9f);
            fillSr.sortingOrder = 15;
            fillGo.transform.localScale = new Vector3(0.72f, 0.06f, 1f);
            healthBarFill = fillGo.transform;
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            // 1. Forward Navigation & Gentle Lateral Sway
            lateralSwayTimer += dt * 2.2f;
            float forwardDir = isPlayerTeam ? 1.0f : -1.0f;
            float sway = Mathf.Sin(lateralSwayTimer) * 0.45f + lateralOffset;

            Vector3 step = new Vector3(sway * dt, forwardDir * spec.moveSpeed * dt, 0f);
            transform.position += step;

            // 2. Scan & Fire at Opposing Targets
            fireTimer -= dt;
            if (fireTimer <= 0f)
            {
                var target = FindBestTarget();
                if (target != null)
                {
                    ExecuteAttack(target);
                    fireTimer = spec.fireRate;
                }
            }

            // 3. Baseline Breach Check (Direct Core Damage)
            CheckBaselineBreach();

            // 4. Hit Flash Recovery
            if (hitFlashTimer > 0f)
            {
                hitFlashTimer -= dt;
                if (hitFlashTimer <= 0f && spriteRenderer != null)
                {
                    spriteRenderer.color = Color.white;
                }
            }
        }

        private Transform FindBestTarget()
        {
            Transform closest = null;
            float closestDistSqr = spec.attackRange * spec.attackRange;

            // 1. Scan opposing squadron units
            for (int i = 0; i < ActiveUnits.Count; i++)
            {
                var other = ActiveUnits[i];
                if (other == null || other.isPlayerTeam == isPlayerTeam) continue;

                // Only target enemies ahead in our flight path
                float dy = other.transform.position.y - transform.position.y;
                if ((isPlayerTeam && dy < 0.2f) || (!isPlayerTeam && dy > -0.2f)) continue;

                float distSqr = (other.transform.position - transform.position).sqrMagnitude;
                if (distSqr < closestDistSqr)
                {
                    closestDistSqr = distSqr;
                    closest = other.transform;
                }
            }

            // 2. If no unit in range, check opposing Commander Flagship
            if (closest == null && CommanderBattleManager.Instance != null)
            {
                Transform flagship = isPlayerTeam 
                    ? CommanderBattleManager.Instance.OpponentFlagshipTransform 
                    : CommanderBattleManager.Instance.PlayerFlagshipTransform;

                if (flagship != null)
                {
                    float distSqr = (flagship.position - transform.position).sqrMagnitude;
                    if (distSqr < closestDistSqr)
                    {
                        closest = flagship;
                    }
                }
            }

            return closest;
        }

        private void ExecuteAttack(Transform target)
        {
            if (ObjectPool.Instance == null) return;

            Vector3 aimDir = (target.position - transform.position).normalized;
            string poolKey = isPlayerTeam ? "PlayerProjectile" : "EnemyProjectile";
            Vector3 spawnPos = transform.position + (isPlayerTeam ? Vector3.up : Vector3.down) * 0.35f;

            var projObj = ObjectPool.Instance.Spawn(poolKey, spawnPos, Quaternion.identity);
            if (projObj != null && projObj.TryGetComponent<Projectile>(out var p))
            {
                LayerMask targetMask = isPlayerTeam ? (LayerMask)(1 << 8 | 1 << 9) : (LayerMask)(1 << 6);
                p.Initialize(aimDir, spec.attackDamage, targetMask, isPlayerTeam, 10f);
            }

            if (isPlayerTeam)
                AudioManager.Instance?.PlayPlayerLaser();
            else
                AudioManager.Instance?.PlayEnemyLaser();
        }

        private void CheckBaselineBreach()
        {
            if (CommanderBattleManager.Instance == null) return;

            // Player units breach at Top of arena
            if (isPlayerTeam && transform.position.y >= PlayfieldBounds.Top - 0.45f)
            {
                int breachDamage = spec.attackDamage * 2 + 4;
                CommanderBattleManager.Instance.DamageOpponentCore(breachDamage);
                TriggerBreachDetonation();
            }
            // Hostile units breach at Bottom of arena
            else if (!isPlayerTeam && transform.position.y <= PlayfieldBounds.Bottom + 0.45f)
            {
                int breachDamage = spec.attackDamage * 2 + 4;
                CommanderBattleManager.Instance.DamagePlayerCore(breachDamage);
                TriggerBreachDetonation();
            }
        }

        private void TriggerBreachDetonation()
        {
            if (ObjectPool.Instance != null)
            {
                ObjectPool.Instance.Spawn("ShockwaveVFX", transform.position, Quaternion.identity);
                ObjectPool.Instance.Spawn("ExplosionVFX", transform.position, Quaternion.identity);
            }
            CameraShake.Instance?.Shake(0.12f, 0.2f);
            AudioManager.Instance?.PlayExplosion(false);
            Destroy(gameObject);
        }

        public bool IsDead => currentHealth <= 0;

        public void TakeDamage(int damage, Vector2 hitPoint)
        {
            currentHealth -= damage;

            // Flash red on hit
            hitFlashTimer = 0.08f;
            if (spriteRenderer != null) spriteRenderer.color = HitFlashColor;

            // Update mini healthbar
            if (healthBarFill != null && maxHealth > 0)
            {
                float pct = Mathf.Clamp01((float)currentHealth / maxHealth);
                healthBarFill.localScale = new Vector3(0.72f * pct, 0.06f, 1f);
            }

            if (currentHealth <= 0)
            {
                Die();
            }
        }

        private void Die()
        {
            if (ObjectPool.Instance != null)
            {
                ObjectPool.Instance.Spawn("ExplosionVFX", transform.position, Quaternion.identity);
            }
            AudioManager.Instance?.PlayExplosion(false);
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            ActiveUnits.Remove(this);
        }
    }
}
