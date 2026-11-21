using UnityEngine;
using Starfall.Core;
using Starfall.Player;

namespace Starfall.Combat
{
    /// <summary>
    /// Companion combat drone spawned by the Wingman Drone power-up.
    /// Hovers alongside the player warship and fires support lasers.
    /// </summary>
    public class WingmanDrone : MonoBehaviour
    {
        [Header("Follow Settings")]
        [SerializeField] private Vector3 offset = new Vector3(-0.95f, -0.1f, 0f);
        [SerializeField] private float followSpeed = 12f;
        [SerializeField] private float bobAmplitude = 0.08f;
        [SerializeField] private float bobFrequency = 4f;

        [Header("Weaponry")]
        [SerializeField] private string projectilePoolKey = "PlayerProjectile";
        [SerializeField] private float fireRate = 0.22f;
        [SerializeField] private int damage = 2;

        private Transform playerTarget;
        private PlayerCombat playerCombat;
        private float fireTimer;
        private float bobTimer;
        private float lifeTimer;
        private bool isRightWing;

        public void Initialize(Transform player, bool rightWing, float duration)
        {
            playerTarget = player;
            isRightWing = rightWing;
            offset = new Vector3(rightWing ? 0.95f : -0.95f, -0.1f, 0f);
            lifeTimer = duration;
            bobTimer = rightWing ? Mathf.PI : 0f;

            if (player != null)
            {
                playerCombat = player.GetComponent<PlayerCombat>();
                transform.position = player.position + offset;
            }
        }

        private void Update()
        {
            using var __perf = Starfall.Core.PerfMarkers.PlayerUpdate.Auto();
            if (playerTarget == null || !playerTarget.gameObject.activeInHierarchy)
            {
                Destroy(gameObject);
                return;
            }

            lifeTimer -= Time.deltaTime;
            if (lifeTimer <= 0f)
            {
                // Play warp despawn flash
                if (ObjectPool.Instance != null)
                {
                    ObjectPool.Instance.Spawn("ShockwaveVFX", transform.position, Quaternion.identity);
                }
                Destroy(gameObject);
                return;
            }

            bobTimer += Time.deltaTime * bobFrequency;
            float bobY = Mathf.Sin(bobTimer) * bobAmplitude;
            Vector3 targetPos = playerTarget.position + offset + new Vector3(0f, bobY, 0f);

            transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * followSpeed);

            // Fire support lasers
            fireTimer -= Time.deltaTime;
            if (fireTimer <= 0f)
            {
                fireTimer = fireRate;
                Fire();
            }
        }

        private void Fire()
        {
            if (ObjectPool.Instance == null) return;

            Vector3 spawnPos = transform.position + Vector3.up * 0.35f;
            GameObject bullet = ObjectPool.Instance.Spawn(projectilePoolKey, spawnPos, Quaternion.identity);
            if (bullet != null && bullet.TryGetComponent<Projectile>(out var proj))
            {
                proj.Initialize(Vector2.up, damage, LayerMask.GetMask("Enemy"), true);
            }

            ObjectPool.Instance.Spawn("MuzzleFlashVFX", spawnPos, Quaternion.identity);
        }
    }
}
