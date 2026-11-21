using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using Starfall.Core;
using Starfall.Combat;
using Starfall.Enemies;
using Starfall.VFX;
using Starfall.Audio;

namespace Starfall.Player
{
    /// <summary>
    /// Handles player weapons, dynamic warship hardpoints, tactical Starfall Bombs,
    /// and active warship special abilities.
    /// </summary>
    public class PlayerCombat : MonoBehaviour
    {
        [Header("Weapon Configuration")]
        [SerializeField] private string projectilePoolKey = "PlayerProjectile";
        [SerializeField] private float fireRate = 0.16f; // Shots per second
        [SerializeField] private int damage = 1;
        [SerializeField] private LayerMask targetLayer;

        [Header("Weapon Level (1 to 5)")]
        [SerializeField] private int weaponLevel = 1;
        [SerializeField] private int maxWeaponLevel = 5;

        [Header("Starfall Bombs")]
        [SerializeField] private int currentBombs = 2;
        [SerializeField] private int maxBombs = 5;
        [SerializeField] private string shockwavePoolKey = "ShockwaveVFX";
        [Tooltip("Damage dealt to every enemy on screen. Kills scouts, cripples tougher roles - a panic button, not a wave skip.")]
        [SerializeField] private int bombDamage = 7;

        [Header("State")]
        [SerializeField] private bool autoFire = true;

        private float fireTimer;
        private float baseFireRate;
        private Coroutine rapidFireCoroutine;
        private WarshipSpec activeShip;

        // Ability State
        private float abilityCooldown = 15f;
        private float abilityTimer = 15f; // Ready at start

        private float lastAbilityBroadcast = -1f;
        private int missileVolleyCounter;
        private float magnetSweepTimer;

        // Power-Up State
        private float homingMissilesTimer;
        private float magnetTimer;

        public bool AutoFire { get => autoFire; set => autoFire = value; }
        public int WeaponLevel => weaponLevel;
        public int CurrentBombs => currentBombs;
        public int MaxBombs => maxBombs;
        public WarshipSpec ActiveShip => activeShip;
        public float AbilityCooldown => abilityCooldown;
        public float AbilityTimer => abilityTimer;
        public bool IsAbilityReady => abilityTimer >= abilityCooldown;

        public void ChargeAbilityFull()
        {
            abilityTimer = abilityCooldown;
            currentBombs = Mathf.Max(currentBombs, 1);
            OnAbilityCooldownChanged?.Invoke(abilityTimer, abilityCooldown);
            OnBombsChanged?.Invoke(currentBombs);
        }

        public event System.Action<int> OnWeaponLevelChanged;
        public event System.Action<int> OnBombsChanged;
        public event System.Action<float, float> OnAbilityCooldownChanged; // current, max
        public static event System.Action OnBombUsed;
        public static event System.Action OnSpecialAbilityUsed;

        private void Awake()
        {
            activeShip = ShipUpgradesManager.GetActiveShipSpec();

            if (targetLayer.value == 0)
            {
                targetLayer = LayerMask.GetMask("Enemy");
            }

            // Apply effective warship modifiers
            damage = Mathf.Max(1, Mathf.RoundToInt(damage * ShipUpgradesManager.GetEffectiveDamageMultiplier()));
            baseFireRate = fireRate / ShipUpgradesManager.GetEffectiveFireRateMultiplier();
            fireRate = baseFireRate;

            abilityCooldown = ShipUpgradesManager.GetEffectiveAbilityCooldown();
            abilityTimer = abilityCooldown; // Ready immediately on mission deploy

            currentBombs = Mathf.Clamp(currentBombs + ShipUpgradesManager.GetExtraBombs(), 1, maxBombs);
            gunsJammed = false;
        }

        private bool gunsJammed = false;
        public void SetGunsJammed(bool jammed) => gunsJammed = jammed;

        private void Start()
        {
            OnWeaponLevelChanged?.Invoke(weaponLevel);
            OnBombsChanged?.Invoke(currentBombs);
            OnAbilityCooldownChanged?.Invoke(abilityTimer, abilityCooldown);
        }

        private void Update()
        {
            using var __perf = Starfall.Core.PerfMarkers.PlayerUpdate.Auto();

            if (gunsJammed || Starfall.Core.WaveManager.IsStealthWaveActive)
            {
                // Weapons offline during stealth wave or when jammed
                return;
            }

            // Tactical screen-clearing bomb trigger (Spacebar, B key, or Mouse Right Click)
            bool bombInput = (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.bKey.wasPressedThisFrame))
                || (Mouse.current != null && Mouse.current.rightButton.wasPressedThisFrame);
            if (bombInput)
            {
                UseBomb();
            }

            // Active Special Ability trigger (E key or tap)
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            {
                ActivateSpecialAbility();
            }

            // Update ability cooldown
            if (abilityTimer < abilityCooldown)
            {
                abilityTimer = Mathf.Min(abilityCooldown, abilityTimer + Time.deltaTime);
                // Broadcast at 30 fps for smooth Rive dial gauge animations without frame spikes
                if (abilityTimer >= abilityCooldown || abilityTimer - lastAbilityBroadcast >= 0.033f)
                {
                    lastAbilityBroadcast = abilityTimer;
                    OnAbilityCooldownChanged?.Invoke(abilityTimer, abilityCooldown);
                }
            }

            // Power-up durations
            if (homingMissilesTimer > 0f)
            {
                homingMissilesTimer -= Time.deltaTime;
            }

            if (magnetTimer > 0f)
            {
                magnetTimer -= Time.deltaTime;
                // Scene search 6x per second instead of every frame.
                magnetSweepTimer -= Time.deltaTime;
                if (magnetSweepTimer <= 0f)
                {
                    magnetSweepTimer = 0.16f;
                    VortexMagnetSweep();
                }
            }

            if (!autoFire || gunsJammed) return;

            float currentRate = fireRate;
            if (TacticalPerkManager.Instance != null)
                currentRate /= TacticalPerkManager.Instance.FireRateMultiplier;

            fireTimer += Time.deltaTime;
            if (fireTimer >= currentRate)
            {
                fireTimer = 0f;
                Fire();
            }
        }

        public void UpgradeWeapon(int amount = 1)
        {
            if (weaponLevel >= maxWeaponLevel)
            {
                // Maxed out: convert the pickup into score so it still feels rewarding.
                GameManager.Instance?.AddScore(1500);
                VFXKit.Popup(transform.position + Vector3.up * 0.6f, "MAX POWER +1,500", new Color(1f, 0.6f, 0.1f));
                return;
            }
            weaponLevel = Mathf.Clamp(weaponLevel + amount, 1, maxWeaponLevel);
            OnWeaponLevelChanged?.Invoke(weaponLevel);
            VFXKit.Ring(transform.position, new Color(1f, 0.55f, 0.1f, 0.9f), 0.4f, 2.4f, 0.35f);
            VFXKit.Popup(transform.position + Vector3.up * 0.6f, "WEAPON LV " + weaponLevel, new Color(1f, 0.6f, 0.1f));
        }

        /// <summary>Called when a life is lost. Keeps at least level 1.</summary>
        public void DowngradeWeapon(int amount = 1)
        {
            int before = weaponLevel;
            weaponLevel = Mathf.Clamp(weaponLevel - amount, 1, maxWeaponLevel);
            if (weaponLevel != before)
            {
                OnWeaponLevelChanged?.Invoke(weaponLevel);
                VFXKit.Popup(transform.position + Vector3.up * 0.6f, "WEAPON LV " + weaponLevel, new Color(1f, 0.35f, 0.3f));
            }
        }

        public void AddBomb(int count = 1) => AddBombs(count);

        public void AddBombs(int amount = 1)
        {
            currentBombs = Mathf.Min(currentBombs + amount, maxBombs);
            OnBombsChanged?.Invoke(currentBombs);
            Debug.Log($"[PlayerCombat] Added bomb. Total: {currentBombs}");
        }

        public bool UseBomb()
        {
            if (gunsJammed || Starfall.Core.WaveManager.IsStealthWaveActive) return false;
            if (currentBombs <= 0) return false;

            currentBombs--;
            OnBombsChanged?.Invoke(currentBombs);
            Starfall.Audio.HapticFeedback.HeavyImpact();

            VFXKit.FlashScreen(new Color(1f, 0.9f, 1f, 0.75f), 0.45f);
            GetComponent<PlayerHealth>()?.TriggerInvulnerability(1.0f);

            // 1. Shockwave & Tactical Nuke VFX
            if (ObjectPool.Instance != null)
            {
                if (!string.IsNullOrEmpty(shockwavePoolKey))
                {
                    ObjectPool.Instance.Spawn(shockwavePoolKey, transform.position, Quaternion.identity);
                }
                ObjectPool.Instance.Spawn("NukeDetonationVFX", transform.position, Quaternion.identity);
            }

            // 2. Camera Shake & Audio
            if (CameraShake.Instance != null)
            {
                CameraShake.Instance.Shake(0.45f, 0.7f);
            }
            if (AudioManager.Instance != null)
            {
                AudioManager.Instance.PlaySpecialBomb();
            }
            OnBombUsed?.Invoke();

            // 3. Clear all active enemy projectiles
            for (int i = Projectile.ActiveEnemyProjectiles.Count - 1; i >= 0; i--)
            {
                var p = Projectile.ActiveEnemyProjectiles[i];
                if (p != null && p.gameObject.activeInHierarchy)
                {
                    p.Despawn();
                }
            }

            // 4. Fixed damage to every on-screen enemy (copy the list: kills modify it)
            int effectiveBombDamage = bombDamage;
            if (TacticalPerkManager.Instance != null)
                effectiveBombDamage = Mathf.RoundToInt(effectiveBombDamage * TacticalPerkManager.Instance.BombDamageMultiplier);

            var enemies = EnemyBase.Active.ToArray();
            for (int i = 0; i < enemies.Length; i++)
            {
                var e = enemies[i];
                if (e != null && e.gameObject.activeInHierarchy && !e.IsDead && PlayfieldBounds.IsOnScreen(e.transform.position))
                {
                    e.TakeDamage(effectiveBombDamage, e.transform.position);
                }
            }

            // 5. Boss tactical damage
            const int bossBombDamage = 20;
            var miniBosses = FindObjectsByType<Starfall.Enemies.MiniBossGunship>(FindObjectsInactive.Exclude);
            for (int i = 0; i < miniBosses.Length; i++)
            {
                var mb = miniBosses[i];
                if (mb != null && mb.gameObject.activeInHierarchy && !mb.IsDead)
                {
                    mb.TakeDamage(bossBombDamage, mb.transform.position);
                }
            }

            var chapterBosses = FindObjectsByType<Starfall.Enemies.InvaderCarrier>(FindObjectsInactive.Exclude);
            for (int i = 0; i < chapterBosses.Length; i++)
            {
                var cb = chapterBosses[i];
                if (cb != null && cb.gameObject.activeInHierarchy && !cb.IsDead)
                {
                    cb.TakeDamage(bossBombDamage, cb.transform.position);
                }
            }

            var dreadnoughts = FindObjectsByType<Starfall.Enemies.PlasmaDreadnought>(FindObjectsInactive.Exclude);
            for (int i = 0; i < dreadnoughts.Length; i++)
            {
                var pd = dreadnoughts[i];
                if (pd != null && pd.gameObject.activeInHierarchy && !pd.IsDead)
                {
                    pd.TakeDamage(bossBombDamage, pd.transform.position);
                }
            }

            var queens = FindObjectsByType<Starfall.Enemies.HiveQueenBoss>(FindObjectsInactive.Exclude);
            for (int i = 0; i < queens.Length; i++)
            {
                var hq = queens[i];
                if (hq != null && hq.gameObject.activeInHierarchy && !hq.IsDead)
                {
                    hq.TakeDamage(bossBombDamage, hq.transform.position);
                }
            }

            var colossi = FindObjectsByType<Starfall.Enemies.InfernoColossusBoss>(FindObjectsInactive.Exclude);
            for (int i = 0; i < colossi.Length; i++)
            {
                var ic = colossi[i];
                if (ic != null && ic.gameObject.activeInHierarchy && !ic.IsDead)
                {
                    ic.TakeDamage(bossBombDamage, ic.transform.position);
                }
            }

            return true;
        }

        /// <summary>
        /// Activates the active warship's unique special ability.
        /// </summary>
        public bool ActivateSpecialAbility()
        {
            if (gunsJammed || Starfall.Core.WaveManager.IsStealthWaveActive) return false;
            if (abilityTimer < abilityCooldown) return false;
            abilityTimer = 0f;
            lastAbilityBroadcast = -1f;
            OnAbilityCooldownChanged?.Invoke(abilityTimer, abilityCooldown);

            if (activeShip == null) activeShip = ShipUpgradesManager.GetActiveShipSpec();

            Debug.Log($"<color=yellow>[PlayerCombat] Activated Warship Ability: {activeShip.abilityName}</color>");
            Starfall.Audio.AudioManager.Instance?.PlayPowerUp();
            Starfall.Audio.HapticFeedback.HeavyImpact();
            OnSpecialAbilityUsed?.Invoke();

            if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.2f, 0.35f);

            switch (activeShip.shipClass)
            {
                case WarshipClass.Viper:
                    // Overdrive: High rapid-fire + 50% movement boost
                    ApplyRapidFire(activeShip.abilityDuration);
                    GetComponent<PlayerController>()?.ApplySpeedBoost(1.5f, activeShip.abilityDuration);
                    break;

                case WarshipClass.Titan:
                    // Aegis Shockwave: Full invulnerability + shockwave detonating screen bullets
                    GetComponent<PlayerHealth>()?.TriggerInvulnerability(activeShip.abilityDuration);
                    if (ObjectPool.Instance != null && !string.IsNullOrEmpty(shockwavePoolKey))
                    {
                        ObjectPool.Instance.Spawn(shockwavePoolKey, transform.position, Quaternion.identity);
                    }
                    ClearScreenBullets();
                    break;

                case WarshipClass.Specter:
                    // Phase Warp Cloak: Invulnerability phase + super rapid fire
                    GetComponent<PlayerHealth>()?.TriggerInvulnerability(activeShip.abilityDuration);
                    ApplyRapidFire(activeShip.abilityDuration);
                    break;

                case WarshipClass.Tempest:
                    // Vortex EMP: Pulls all powerups and gems directly to player + restores shields
                    VortexMagnetSweep();
                    GetComponent<PlayerHealth>()?.RestoreShield(2);
                    ClearScreenBullets();
                    break;

                case WarshipClass.Phoenix:
                    // Supernova Flare: Invulnerability + full bullet wipe + restores shield + rapid fire
                    GetComponent<PlayerHealth>()?.TriggerInvulnerability(activeShip.abilityDuration);
                    GetComponent<PlayerHealth>()?.RestoreShield(3);
                    ClearScreenBullets();
                    ApplyRapidFire(activeShip.abilityDuration);
                    if (ObjectPool.Instance != null && !string.IsNullOrEmpty(shockwavePoolKey))
                    {
                        ObjectPool.Instance.Spawn(shockwavePoolKey, transform.position, Quaternion.identity);
                    }
                    break;
            }
            return true;
        }

        private void ClearScreenBullets()
        {
            for (int i = Projectile.ActiveEnemyProjectiles.Count - 1; i >= 0; i--)
            {
                var p = Projectile.ActiveEnemyProjectiles[i];
                if (p != null && p.gameObject.activeInHierarchy)
                {
                    p.Despawn();
                }
            }
        }

        private void VortexMagnetSweep()
        {
            for (int i = 0; i < PowerUp.Active.Count; i++)
            {
                var pu = PowerUp.Active[i];
                if (pu != null && pu.gameObject.activeInHierarchy)
                {
                    pu.transform.position = transform.position;
                }
            }
        }

        private void Fire()
        {
            missileVolleyCounter++;

            // Heavy laser and tactical recoil for Weapon Level 5
            if (weaponLevel >= 5)
            {
                Starfall.Audio.AudioManager.Instance?.PlayHeavyLaser();
                CameraShake.Instance?.Shake(0.03f, 0.045f);
            }
            else
            {
                Starfall.Audio.AudioManager.Instance?.PlayPlayerLaser();
            }

            Vector3 origin = transform.position;

            if (activeShip == null) activeShip = ShipUpgradesManager.GetActiveShipSpec();
            var hardpoints = activeShip.hardpointOffsets;

            // 1. Spawn projectiles from all default warship hardpoints
            if (hardpoints != null && hardpoints.Length > 0)
            {
                for (int i = 0; i < hardpoints.Length; i++)
                {
                    Vector3 pos = origin + new Vector3(hardpoints[i].x, hardpoints[i].y, 0f);
                    SpawnProjectile(pos, Vector2.up, i == 0); // only flash the primary hardpoint
                }
            }
            else
            {
                SpawnProjectile(origin + new Vector3(0f, 0.5f, 0f), Vector2.up, true);
            }

            // 2. Weapon level progression - every level adds something visible:
            //    L2 angled wing blasters, L3 twin centre lances, L4 wide perimeter spread,
            //    L5 overcharged centre bolt (double damage + energy bloom).
            if (weaponLevel >= 2)
            {
                SpawnProjectile(origin + new Vector3(-0.35f, 0.4f, 0f), new Vector2(-0.18f, 1f).normalized, false);
                SpawnProjectile(origin + new Vector3(0.35f, 0.4f, 0f), new Vector2(0.18f, 1f).normalized, false);
            }
            if (weaponLevel >= 3)
            {
                SpawnProjectile(origin + new Vector3(-0.14f, 0.6f, 0f), Vector2.up, false);
                SpawnProjectile(origin + new Vector3(0.14f, 0.6f, 0f), Vector2.up, false);
            }
            if (weaponLevel >= 4)
            {
                SpawnProjectile(origin + new Vector3(-0.5f, 0.3f, 0f), new Vector2(-0.38f, 1f).normalized, false);
                SpawnProjectile(origin + new Vector3(0.5f, 0.3f, 0f), new Vector2(0.38f, 1f).normalized, false);
            }
            if (weaponLevel >= 5)
            {
                Vector3 overchargePos = origin + new Vector3(0f, 0.72f, 0f);
                SpawnProjectile(overchargePos, Vector2.up, true, damage * 2);
                VFXKit.Glow(overchargePos, new Color(0.35f, 0.85f, 1f, 0.85f), 0.52f, 0.09f);
            }

            // 3. Secondary Homing Missiles:
            // Mode A: Active Power-Up or Tactical Perk (rapid twin-seeker salvo every 2 shots)
            if (homingMissilesTimer > 0f)
            {
                if ((missileVolleyCounter % 2) == 0)
                {
                    Vector3 leftPod = origin + new Vector3(-0.45f, 0.1f, 0f);
                    Vector3 rightPod = origin + new Vector3(0.45f, 0.1f, 0f);
                    SpawnHomingMissile(leftPod, new Vector2(-0.35f, 1f).normalized);
                    SpawnHomingMissile(rightPod, new Vector2(0.35f, 1f).normalized);
                    Starfall.Audio.AudioManager.Instance?.PlayHomingMissileLaunch();
                    VFXKit.Glow(leftPod, new Color(1f, 0.65f, 0.2f, 0.8f), 0.3f, 0.08f);
                    VFXKit.Glow(rightPod, new Color(1f, 0.65f, 0.2f, 0.8f), 0.3f, 0.08f);
                }
            }
            // Mode B: Passive Built-in Micro-Seekers from High Weapon Level
            else if (weaponLevel >= 4)
            {
                if (weaponLevel >= 5 && (missileVolleyCounter % 3) == 0)
                {
                    // Level 5: Passive twin seeker pod salvo every 3 shots
                    SpawnHomingMissile(origin + new Vector3(-0.38f, 0.1f, 0f), new Vector2(-0.3f, 1f).normalized);
                    SpawnHomingMissile(origin + new Vector3(0.38f, 0.1f, 0f), new Vector2(0.3f, 1f).normalized);
                    Starfall.Audio.AudioManager.Instance?.PlayHomingMissileLaunch();
                }
                else if (weaponLevel == 4 && (missileVolleyCounter % 4) == 0)
                {
                    // Level 4: Alternating left/right wing seeker pod every 4 shots
                    bool left = (missileVolleyCounter % 8) == 0;
                    Vector3 podOffset = left ? new Vector3(-0.42f, 0.1f, 0f) : new Vector3(0.42f, 0.1f, 0f);
                    Vector2 podDir = left ? new Vector2(-0.35f, 1f).normalized : new Vector2(0.35f, 1f).normalized;
                    SpawnHomingMissile(origin + podOffset, podDir);
                    Starfall.Audio.AudioManager.Instance?.PlayHomingMissileLaunch();
                }
            }
        }

        public void DeployWingmanDrones(float duration)
        {
            var oldDrones = FindObjectsByType<WingmanDrone>(FindObjectsInactive.Exclude);
            for (int i = 0; i < oldDrones.Length; i++) Destroy(oldDrones[i].gameObject);

            CreateDrone(false, duration);
            CreateDrone(true, duration);
            Debug.Log($"<color=cyan>[PlayerCombat] Wingman Escort Drones Deployed ({duration}s)!</color>");
        }

        private void CreateDrone(bool rightWing, float duration)
        {
            var droneGo = new GameObject(rightWing ? "WingmanDrone_R" : "WingmanDrone_L");
            var sr = droneGo.AddComponent<SpriteRenderer>();
            sr.sortingOrder = 24;

            var sprite = Resources.Load<Sprite>("PowerUps/Wingman Drone") ??
                         Resources.Load<Sprite>("Wingman Drone");

#if UNITY_EDITOR
            if (sprite == null)
            {
                sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/ALienware 2D assets/Power Ups/Wingman Drone.png");
            }
#endif
            if (sprite != null)
            {
                sr.sprite = sprite;
                droneGo.transform.localScale = Vector3.one * 0.28f;
            }

            var drone = droneGo.AddComponent<WingmanDrone>();
            drone.Initialize(transform, rightWing, duration);
        }

        public void EnableHomingMissiles(float duration) => ApplyHomingMissiles(duration);
        public void ApplyHomingMissiles(float duration)
        {
            homingMissilesTimer = Mathf.Max(homingMissilesTimer, duration);
        }

        public void EnableAttractorMagnet(float duration) => ApplyMagnet(duration);
        public void ApplyMagnet(float duration)
        {
            magnetTimer = Mathf.Max(magnetTimer, duration);
        }

        private static GameObject missileTemplate;

        /// <summary>Builds (once) an inactive missile template and registers it with the pool.</summary>
        private void EnsureMissilePool()
        {
            if (ObjectPool.Instance == null || ObjectPool.Instance.HasPool("HomingMissile")) return;

            if (missileTemplate == null)
            {
                missileTemplate = new GameObject("HomingMissileTemplate");
                missileTemplate.SetActive(false);
                DontDestroyOnLoad(missileTemplate);

                var sr = missileTemplate.AddComponent<SpriteRenderer>();
                sr.sortingOrder = 23;
                // Borrow the icon from the power-up prefab (ships in builds via Resources).
                Sprite sprite = null;
                var pickup = Resources.Load<GameObject>("PowerUps/PowerUp_HomingMissiles");
                if (pickup != null)
                {
                    var psr = pickup.GetComponentInChildren<SpriteRenderer>();
                    if (psr != null) sprite = psr.sprite;
                }
#if UNITY_EDITOR
                if (sprite == null)
                    sprite = UnityEditor.AssetDatabase.LoadAssetAtPath<Sprite>("Assets/ALienware 2D assets/Power Ups/Homing Missiles.png");
#endif
                sr.sprite = sprite;
                missileTemplate.transform.localScale = Vector3.one * 0.16f;

                var col = missileTemplate.AddComponent<CircleCollider2D>();
                col.isTrigger = true;
                col.radius = 0.3f;
                missileTemplate.AddComponent<HomingMissile>();
            }

            ObjectPool.Instance.RegisterPool("HomingMissile", missileTemplate, 12);
        }

        private void SpawnHomingMissile(Vector3 position, Vector2 direction)
        {
            EnsureMissilePool();
            if (ObjectPool.Instance == null) return;
            var missileGo = ObjectPool.Instance.Spawn("HomingMissile", position, Quaternion.identity);
            if (missileGo != null && missileGo.TryGetComponent<HomingMissile>(out var hm))
                hm.Initialize(position, direction, 15);
        }

        public void ApplyRapidFire(float duration)
        {
            if (rapidFireCoroutine != null) StopCoroutine(rapidFireCoroutine);
            rapidFireCoroutine = StartCoroutine(RapidFireRoutine(duration));
        }



        private IEnumerator RapidFireRoutine(float duration)
        {
            fireRate = baseFireRate * 0.4f;
            yield return new WaitForSeconds(duration);
            fireRate = baseFireRate;
            rapidFireCoroutine = null;
        }

        private void SpawnProjectile(Vector3 position, Vector2 direction, bool muzzleFlash = true, int damageOverride = -1)
        {
            if (ObjectPool.Instance == null) return;

            GameObject bullet = ObjectPool.Instance.Spawn(projectilePoolKey, position, Quaternion.identity);
            if (bullet != null && bullet.TryGetComponent<Projectile>(out var proj))
            {
                proj.Initialize(direction, damageOverride > 0 ? damageOverride : damage, targetLayer, true);
            }

            // Muzzle flash at the hardpoint (skipped for inner lances to limit particles on mobile)
            if (muzzleFlash) ObjectPool.Instance.Spawn("MuzzleFlashVFX", position, Quaternion.identity);
        }
    }
}
