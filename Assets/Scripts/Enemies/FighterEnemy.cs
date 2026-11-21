using UnityEngine;
using Starfall.Core;

namespace Starfall.Enemies
{
    /// <summary>
    /// Heavier "tank" body with twin wing cannons. Carries the Shielder, Bomber and Elite roles well.
    /// Movement and attack selection come from <see cref="EnemyBase"/> and the formation director.
    /// </summary>
    public class FighterEnemy : EnemyBase
    {
        private bool alternateLeft = false;

        public override void FireDownwards(int damage = 1, float speedMultiplier = 1f)
        {
            if (ObjectPool.Instance == null || IsDead || !gameObject.activeInHierarchy || !IsVisibleToPlayer()) return;

            Starfall.Audio.AudioManager.Instance?.PlayEnemyLaser();

            // Alternate wing cannons so shots read as clean individual bullets.
            float xOffset = alternateLeft ? -0.32f : 0.32f;
            alternateLeft = !alternateLeft;
            SpawnEnemyBullet(transform.position + new Vector3(xOffset, -0.45f, 0f), Vector2.down, damage, projectileSpeed * speedMultiplier);
        }
    }
}
