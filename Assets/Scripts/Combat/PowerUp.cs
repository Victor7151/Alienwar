using System.Collections;
using UnityEngine;
using TMPro;
using Starfall.Core;
using Starfall.Player;

namespace Starfall.Combat
{
    public enum PowerUpType
    {
        RapidFire,
        ShieldRestore,
        ScoreBonus,
        WeaponUpgrade,
        Bomb,
        StarCredits,
        WingmanDrone,
        HomingMissiles,
        TimeWarp,
        Magnet,
        AttractorMagnet = Magnet
    }

    /// <summary>
    /// High-visibility collectible with pulsing scale, vibrant colored aura, floating badge, and magnetic attraction.
    /// </summary>
    [RequireComponent(typeof(Collider2D))]
    public class PowerUp : MonoBehaviour, IPooledObject
    {
        [Header("Settings")]
        [SerializeField] private PowerUpType type = PowerUpType.RapidFire;
        [SerializeField] private float driftSpeed = 2.0f;
        [SerializeField] private float lifetime = 10f;
        [SerializeField] private float bobAmplitude = 0.10f;
        [SerializeField] private float bobFrequency = 2.5f;
        [SerializeField] private float baseScale = 0.65f;

        [Header("Rapid Fire")]
        [SerializeField] private float rapidFireDuration = 5f;

        [Header("Shield Restore")]
        [SerializeField] private int shieldRestoreAmount = 2;

        [Header("Score Bonus")]
        [SerializeField] private int scoreBonusAmount = 1000;

        [Header("Magnetism & Pickup")]
        [SerializeField] private float magnetRadius = 2.5f;
        [SerializeField] private float magnetSpeed = 5.5f;

        private float lifeTimer;
        private float spawnX;
        private SpriteRenderer spriteRenderer;
        private SpriteRenderer auraRenderer;
        private TextMeshPro labelTmp;
        private GameObject labelGo;
        private GameObject auraGo;
        private Transform playerTransform;
        private Color cachedTypeColor; // cached in OnSpawn to avoid per-frame GetColorForType() calls

        private static readonly Color ColorRapidFire      = new Color(1f, 0.95f, 0.1f);   // Electric Yellow
        private static readonly Color ColorShield         = new Color(0f, 0.9f, 1f);      // Neon Cyan
        private static readonly Color ColorScore          = new Color(0.3f, 1f, 0.3f);    // Emerald Green
        private static readonly Color ColorWeaponUpgrade  = new Color(1f, 0.5f, 0.05f);   // Blaze Orange
        private static readonly Color ColorBomb           = new Color(0.85f, 0.25f, 1f);  // Magenta Purple
        private static readonly Color ColorStarCredits    = new Color(1f, 0.85f, 0.1f);   // Lustrous Gold
        private static readonly Color ColorWingman        = new Color(0.2f, 0.85f, 1f);   // Sky Cyan
        private static readonly Color ColorHomingMissiles = new Color(1f, 0.3f, 0.2f);    // Warhead Red
        private static readonly Color ColorTimeWarp       = new Color(0.75f, 0.2f, 1f);   // Chrono Violet
        private static readonly Color ColorMagnet         = new Color(0.1f, 0.65f, 1f);   // Magnetic Blue

        // Static registry so other systems (e.g. VortexMagnetSweep) can iterate without FindObjectsByType.
        public static readonly System.Collections.Generic.List<PowerUp> Active =
            new System.Collections.Generic.List<PowerUp>(32);

        public static event System.Action<PowerUpType> OnPowerUpCollected;

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer != null)
            {
                spriteRenderer.sortingOrder = 22;
            }

            var col = GetComponent<Collider2D>();
            if (col != null) col.isTrigger = true;

            EnsureVisualElements();
        }

        private void EnsureVisualElements()
        {
            if (auraGo == null)
            {
                auraGo = new GameObject("AuraGlow");
                auraGo.transform.SetParent(transform, false);
                auraGo.transform.localPosition = Vector3.zero;
                auraGo.transform.localScale = Vector3.one * 1.15f;
                auraRenderer = auraGo.AddComponent<SpriteRenderer>();
                auraRenderer.sortingOrder = 21;
                if (spriteRenderer != null)
                {
                    auraRenderer.sprite = spriteRenderer.sprite;
                    auraRenderer.material = spriteRenderer.material;
                }
            }

            if (labelGo == null)
            {
                labelGo = new GameObject("PowerUpLabel");
                labelGo.transform.SetParent(transform, false);
                labelGo.transform.localPosition = new Vector3(0f, 0.26f, 0f);
                labelTmp = labelGo.AddComponent<TextMeshPro>();
                labelTmp.alignment = TextAlignmentOptions.Center;
                labelTmp.fontSize = 1.1f;
                labelTmp.fontStyle = FontStyles.Normal;
                labelTmp.sortingOrder = 25;
            }
        }

        public void OnSpawn()
        {
            Active.Add(this);
            lifeTimer = lifetime;
            spawnX = transform.position.x;
            transform.rotation = Quaternion.identity;
            transform.localScale = Vector3.one * baseScale;

            EnsureVisualElements();

            cachedTypeColor = GetColorForType(type);

            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.white; // Keep sprite icons vibrant and crisp
                spriteRenderer.sortingOrder = 22;
            }

            if (auraRenderer != null)
            {
                if (spriteRenderer != null && spriteRenderer.sprite != null)
                {
                    auraRenderer.sprite = spriteRenderer.sprite;
                }
                var c = cachedTypeColor;
                c.a = 0.6f;
                auraRenderer.color = c;
            }

            if (labelTmp != null)
            {
                labelTmp.text = GetLabelForType(type);
            }

            // Use the cached PlayerController singleton instead of a slow scene search.
            var pc = Starfall.Player.PlayerController.Instance;
            playerTransform = pc != null ? pc.transform : null;
            if (playerTransform == null)
            {
                var playerObj = GameObject.FindWithTag("Player") ?? GameObject.Find("PlayerShip");
                if (playerObj != null) playerTransform = playerObj.transform;
            }
        }

        public void OnDespawn()
        {
            Active.Remove(this);
        }

        private void Update()
        {
            using var __perf = Starfall.Core.PerfMarkers.PickupUpdate.Auto();
            // Pulsing scale animation (subtle and compact)
            float pulse = 1f + 0.05f * Mathf.Sin(Time.time * 4f);
            transform.localScale = Vector3.one * (baseScale * pulse);

            // Aura pulsation
            if (auraRenderer != null)
            {
                float auraAlpha = 0.45f + 0.35f * Mathf.Sin(Time.time * 8f);
                var c = cachedTypeColor;
                c.a = auraAlpha;
                auraRenderer.color = c;
            }

            // Magnetic attraction to player
            bool attracted = false;
            if (playerTransform != null && playerTransform.gameObject.activeInHierarchy)
            {
                float dist = Vector2.Distance(transform.position, playerTransform.position);
                float activeMagnetRadius = magnetRadius + Starfall.Core.ShipUpgradesManager.GetTractorRadiusBonus();
                if (dist < activeMagnetRadius)
                {
                    transform.position = Vector3.MoveTowards(transform.position, playerTransform.position, magnetSpeed * Time.deltaTime);
                    attracted = true;
                }
            }

            if (!attracted)
            {
                // Drift downwards with a gentle bob and spin
                float y = transform.position.y - driftSpeed * Time.deltaTime;
                float x = spawnX + Mathf.Sin(Time.time * bobFrequency) * bobAmplitude;
                transform.position = new Vector3(x, y, 0f);
            }

            // Slowly rotate the icon for visual appeal
            transform.Rotate(0f, 0f, 75f * Time.deltaTime);

            // Keep label upright and unrotated above the power-up
            if (labelGo != null)
            {
                labelGo.transform.rotation = Quaternion.identity;
                labelGo.transform.position = transform.position + Vector3.up * 0.28f;
            }

            // Proximity pickup is handled by OnTriggerEnter2D — no per-frame OverlapCircle needed.

            // Lifetime & off-screen check
            lifeTimer -= Time.deltaTime;
            if (lifeTimer <= 0f || transform.position.y < -9f)
            {
                Despawn();
            }
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            var health = collision.GetComponentInParent<PlayerHealth>();
            if (health == null || health.IsDead) return;

            ApplyEffect(health, health.gameObject);
            Despawn();
        }

        private void ApplyEffect(PlayerHealth health, GameObject playerObj)
        {
            Starfall.Audio.AudioManager.Instance?.PlayPowerUp();
            OnPowerUpCollected?.Invoke(type);

            switch (type)
            {
                case PowerUpType.RapidFire:
                    var combat = playerObj.GetComponentInParent<PlayerCombat>();
                    combat?.ApplyRapidFire(rapidFireDuration);
                    break;

                case PowerUpType.ShieldRestore:
                    health.RestoreShield(shieldRestoreAmount > 0 ? shieldRestoreAmount : -1);
                    break;

                case PowerUpType.ScoreBonus:
                    GameManager.Instance?.AddScore(scoreBonusAmount);
                    break;

                case PowerUpType.WeaponUpgrade:
                    var combatUpgrade = playerObj.GetComponentInParent<PlayerCombat>();
                    combatUpgrade?.UpgradeWeapon(1);
                    break;

                case PowerUpType.Bomb:
                    var combatBomb = playerObj.GetComponentInParent<PlayerCombat>();
                    combatBomb?.AddBomb(1);
                    break;

                case PowerUpType.StarCredits:
                    Starfall.Levels.LevelManager.Instance?.AddStarCredits(50);
                    GameManager.Instance?.AddScore(250);
                    break;

                case PowerUpType.WingmanDrone:
                    var combatWingman = playerObj.GetComponentInParent<PlayerCombat>();
                    combatWingman?.DeployWingmanDrones(15f);
                    break;

                case PowerUpType.HomingMissiles:
                    var combatMissiles = playerObj.GetComponentInParent<PlayerCombat>();
                    combatMissiles?.EnableHomingMissiles(12f);
                    break;

                case PowerUpType.TimeWarp:
                    TimeWarpManager.TriggerTimeWarp(6f);
                    break;

                case PowerUpType.Magnet:
                    var combatMagnet = playerObj.GetComponentInParent<PlayerCombat>();
                    combatMagnet?.EnableAttractorMagnet(12f);
                    break;
            }
        }

        private Color GetColorForType(PowerUpType t) => t switch
        {
            PowerUpType.RapidFire      => ColorRapidFire,
            PowerUpType.ShieldRestore => ColorShield,
            PowerUpType.ScoreBonus     => ColorScore,
            PowerUpType.WeaponUpgrade  => ColorWeaponUpgrade,
            PowerUpType.Bomb           => ColorBomb,
            PowerUpType.StarCredits    => ColorStarCredits,
            PowerUpType.WingmanDrone   => ColorWingman,
            PowerUpType.HomingMissiles => ColorHomingMissiles,
            PowerUpType.TimeWarp       => ColorTimeWarp,
            PowerUpType.Magnet         => ColorMagnet,
            _                          => Color.white
        };

        private string GetLabelForType(PowerUpType t) => t switch
        {
            PowerUpType.RapidFire      => "<color=#ffee00><b>RAPID FIRE</b></color>",
            PowerUpType.ShieldRestore => "<color=#00e5ff><b>+SHIELD</b></color>",
            PowerUpType.ScoreBonus     => "<color=#00ff66><b>+1,000</b></color>",
            PowerUpType.WeaponUpgrade  => "<color=#ff8800><b>+WEAPON</b></color>",
            PowerUpType.Bomb           => "<color=#d444ff><b>+BOMB</b></color>",
            PowerUpType.StarCredits    => "<color=#ffd700><b>+50 CREDITS</b></color>",
            PowerUpType.WingmanDrone   => "<color=#33ddff><b>WINGMAN DRONES</b></color>",
            PowerUpType.HomingMissiles => "<color=#ff4444><b>HOMING MISSILES</b></color>",
            PowerUpType.TimeWarp       => "<color=#bb44ff><b>TIME WARP</b></color>",
            PowerUpType.Magnet         => "<color=#00e5ff><b>TRACTOR MAGNET</b></color>",
            _                          => "<b>ITEM</b>"
        };

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

        public void SetType(PowerUpType newType)
        {
            type = newType;
            if (labelTmp != null)
            {
                labelTmp.text = GetLabelForType(type);
            }
        }
    }
}
