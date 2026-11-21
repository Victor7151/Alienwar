using UnityEngine;

namespace Starfall.Combat
{
    /// <summary>
    /// Contract for any entity in Starfall that can receive damage.
    /// </summary>
    public interface IDamageable
    {
        void TakeDamage(int damage, Vector2 hitPoint);
        bool IsDead { get; }
    }
}
