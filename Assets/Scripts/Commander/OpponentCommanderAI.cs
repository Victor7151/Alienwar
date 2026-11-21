using System.Collections.Generic;
using UnityEngine;
using Starfall.Combat;
using Starfall.Core;
using Starfall.Audio;
using Starfall.VFX;

namespace Starfall.Commander
{
    /// <summary>
    /// AI Opponent Commander Flagship positioned at the top of the battlefield.
    /// Maneuvers horizontally, fires defense cannons, accumulates energy,
    /// and tactically deploys opposing squadron counter-waves against the player.
    /// </summary>
    public class OpponentCommanderAI : MonoBehaviour, IDamageable
    {
        [Header("Commander Profile")]
        [SerializeField] private string commanderName = "COMMANDER MALAKOR [AI]";
        [SerializeField] private float patrolSpeed = 2.5f;

        [Header("Defense Weaponry")]
        [SerializeField] private float defenseFireRate = 1.2f;
        [SerializeField] private int defenseDamage = 3;

        private SpriteRenderer spriteRenderer;
        private List<SquadronCardDef> opponentDeck;
        private float defenseFireTimer;
        private float patrolTimer;
        private float aiThinkTimer;
        private float hitFlashTimer;
        private static readonly Color HitColor = new Color(1f, 0.4f, 0.4f, 1f);

        public string CommanderName => commanderName;

        public void Initialize(List<SquadronCardDef> deck)
        {
            opponentDeck = deck ?? CommanderCatalog.GetDefaultOpponentDeck();

            spriteRenderer = GetComponent<SpriteRenderer>();
            if (spriteRenderer == null) spriteRenderer = gameObject.AddComponent<SpriteRenderer>();

            // Load high-tech boss/flagship sprite
            spriteRenderer.sprite = WarshipSpriteUtility.GetShipSprite(
                "Apex_Opponent_Flagship",
                WarshipClass.Phoenix,
                new Color(1f, 0.2f, 0.15f, 1f)
            );
            spriteRenderer.sortingLayerName = "Default";
            spriteRenderer.sortingOrder = 11;

            // Flipped 180 degrees to face DOWN
            transform.rotation = Quaternion.Euler(0f, 0f, 180f);
            transform.localScale = Vector3.one * 1.35f;

            var col = GetComponent<Collider2D>();
            if (col == null)
            {
                var box = gameObject.AddComponent<BoxCollider2D>();
                box.size = new Vector2(1.8f, 1.2f);
                box.isTrigger = true;
            }

            gameObject.layer = 8; // Enemy layer
            gameObject.tag = "Enemy";

            patrolTimer = Random.Range(0f, 10f);
            aiThinkTimer = 1.5f;
            defenseFireTimer = defenseFireRate;
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            // 1. Horizontal Evasive Patrol at Top of Arena
            patrolTimer += dt * patrolSpeed;
            float targetY = PlayfieldBounds.Top - 1.2f;
            float maxSweep = PlayfieldBounds.PlayHalfWidth - 1.2f;
            float targetX = Mathf.Sin(patrolTimer) * maxSweep;

            Vector3 currentPos = transform.position;
            transform.position = Vector3.Lerp(currentPos, new Vector3(targetX, targetY, 0f), dt * 4.0f);

            // 2. Defense Cannons (Fire downward at encroaching player units)
            defenseFireTimer -= dt;
            if (defenseFireTimer <= 0f)
            {
                defenseFireTimer = defenseFireRate;
                FireDefenseCannons();
            }

            // 3. AI Commander Tactical Deployment
            aiThinkTimer -= dt;
            if (aiThinkTimer <= 0f)
            {
                aiThinkTimer = Random.Range(1.8f, 3.2f);
                DecideTacticalSummon();
            }

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

        private void FireDefenseCannons()
        {
            if (ObjectPool.Instance == null) return;

            // Twin cannon offsets
            Vector3 leftMuzzle = transform.position + new Vector3(-0.45f, -0.3f, 0f);
            Vector3 rightMuzzle = transform.position + new Vector3(0.45f, -0.3f, 0f);

            SpawnDefenseBolt(leftMuzzle);
            SpawnDefenseBolt(rightMuzzle);
        }

        private void SpawnDefenseBolt(Vector3 muzzlePos)
        {
            var proj = ObjectPool.Instance.Spawn("EnemyProjectile", muzzlePos, Quaternion.identity);
            if (proj != null && proj.TryGetComponent<Projectile>(out var p))
            {
                // Point downward towards player defense zone
                p.Initialize(Vector2.down, defenseDamage, 1 << 6, false, 8.5f);
            }
        }

        private void DecideTacticalSummon()
        {
            if (CommanderBattleManager.Instance == null || opponentDeck == null || opponentDeck.Count == 0) return;

            float currentEnergy = CommanderBattleManager.Instance.OpponentEnergy;

            // Scan battlefield for player units to counter
            int friendlySwarmCount = 0;
            int friendlyHeavyCount = 0;

            var activeUnits = SquadronUnit.ActiveUnits;
            for (int i = 0; i < activeUnits.Count; i++)
            {
                var u = activeUnits[i];
                if (u != null && u.IsPlayerTeam)
                {
                    if (u.Spec.id == SquadronUnitId.ViperSwarm) friendlySwarmCount++;
                    else if (u.Spec.id == SquadronUnitId.TitanGunship || u.Spec.id == SquadronUnitId.TempestCruiser) friendlyHeavyCount++;
                }
            }

            // Counter strategy:
            SquadronCardDef bestCard = null;

            if (friendlySwarmCount >= 2 && currentEnergy >= 4)
            {
                // Counter swarms with Gunship
                bestCard = opponentDeck.Find(c => c.id == SquadronUnitId.TitanGunship);
            }
            else if (friendlyHeavyCount >= 1 && currentEnergy >= 5)
            {
                // Counter heavies with Sniper
                bestCard = opponentDeck.Find(c => c.id == SquadronUnitId.SpecterSniper);
            }
            else if (currentEnergy >= 8)
            {
                // Unleash Dreadnought push
                bestCard = opponentDeck.Find(c => c.id == SquadronUnitId.PhoenixColossus) ?? opponentDeck[^1];
            }
            else if (currentEnergy >= 3)
            {
                // Harass with Viper Swarm
                bestCard = opponentDeck.Find(c => c.id == SquadronUnitId.ViperSwarm);
            }

            if (bestCard != null && currentEnergy >= bestCard.energyCost)
            {
                CommanderBattleManager.Instance.DeployOpponentSquad(bestCard);
            }
        }

        public bool IsDead => CommanderBattleManager.Instance != null && CommanderBattleManager.Instance.OpponentCoreHP <= 0;

        public void TakeDamage(int damage, Vector2 hitPoint)
        {
            hitFlashTimer = 0.08f;
            if (spriteRenderer != null) spriteRenderer.color = HitColor;

            // Flagship direct damage impacts Opponent Core HP
            CommanderBattleManager.Instance?.DamageOpponentCore(damage);

            if (ObjectPool.Instance != null)
            {
                ObjectPool.Instance.Spawn("HitSparkMetalVFX", hitPoint, Quaternion.identity);
            }
        }
    }
}
