using UnityEngine;

namespace Starfall.Enemies
{
    /// <summary>
    /// Tactical role layered on top of an enemy body (Scout / Fighter prefab).
    /// Roles give each wave a readable purpose (GDD §16): difficulty comes from what the
    /// enemies DO and how they combine, not from inflated health.
    /// </summary>
    public enum EnemyRole
    {
        /// <summary>Basic formation member. Straight shots, occasional swoop.</summary>
        Grunt,
        /// <summary>Telegraphs a laser sight, then fires an aimed 3-round burst. Punishes standing still.</summary>
        Gunner,
        /// <summary>Flashes red, locks a line on the player, then rams at high speed. Punishes predictable movement.</summary>
        Charger,
        /// <summary>Drops slow mines that burst into a ring of bullets. Controls space.</summary>
        Bomber,
        /// <summary>Projects a field that halves damage to nearby allies. Kill it first.</summary>
        Shielder,
        /// <summary>Armoured veteran: fires wide fans, tougher, worth more.</summary>
        Elite,
        /// <summary>Phases out and reappears in another slot, then snaps an aimed shot.</summary>
        Blinker,
        /// <summary>Squad commander: armored, directs the fleet. Killing it panics and demoralizes remaining escorts.</summary>
        Commander
    }

    public struct EnemyRoleProfile
    {
        public Color tint;
        public Color auraColor;
        public float healthMultiplier;
        public float scoreMultiplier;
        public float auraScale;
        public bool useRingAura;

        public static EnemyRoleProfile Get(EnemyRole role)
        {
            switch (role)
            {
                case EnemyRole.Gunner:
                    return new EnemyRoleProfile { tint = new Color(0.65f, 1f, 0.75f), auraColor = new Color(0.3f, 1f, 0.55f, 0.35f), healthMultiplier = 1.25f, scoreMultiplier = 1.5f, auraScale = 1.1f };
                case EnemyRole.Charger:
                    return new EnemyRoleProfile { tint = new Color(1f, 0.55f, 0.45f), auraColor = new Color(1f, 0.3f, 0.2f, 0.35f), healthMultiplier = 0.85f, scoreMultiplier = 1.6f, auraScale = 1.0f };
                case EnemyRole.Bomber:
                    return new EnemyRoleProfile { tint = new Color(0.85f, 0.6f, 1f), auraColor = new Color(0.75f, 0.35f, 1f, 0.35f), healthMultiplier = 1.35f, scoreMultiplier = 1.8f, auraScale = 1.15f };
                case EnemyRole.Shielder:
                    return new EnemyRoleProfile { tint = new Color(0.55f, 0.8f, 1f), auraColor = new Color(0.35f, 0.75f, 1f, 0.28f), healthMultiplier = 1.6f, scoreMultiplier = 2f, auraScale = EnemyBase.ShieldFieldRadius * 2f, useRingAura = true };
                case EnemyRole.Elite:
                    return new EnemyRoleProfile { tint = new Color(1f, 0.87f, 0.4f), auraColor = new Color(1f, 0.8f, 0.2f, 0.45f), healthMultiplier = 2.2f, scoreMultiplier = 3f, auraScale = 1.35f };
                case EnemyRole.Blinker:
                    return new EnemyRoleProfile { tint = new Color(1f, 0.5f, 0.95f), auraColor = new Color(1f, 0.35f, 0.9f, 0.35f), healthMultiplier = 1f, scoreMultiplier = 2f, auraScale = 1.1f };
                case EnemyRole.Commander:
                    return new EnemyRoleProfile { tint = new Color(0.92f, 0.72f, 1f), auraColor = new Color(0.85f, 0.55f, 1f, 0.5f), healthMultiplier = 3.2f, scoreMultiplier = 4.5f, auraScale = 1.4f };
                default:
                    return new EnemyRoleProfile { tint = Color.white, auraColor = Color.clear, healthMultiplier = 1f, scoreMultiplier = 1f, auraScale = 0f };
            }
        }

        public static string DisplayName(EnemyRole role) => role switch
        {
            EnemyRole.Gunner => "GUNNERS",
            EnemyRole.Charger => "CHARGERS",
            EnemyRole.Bomber => "BOMBERS",
            EnemyRole.Shielder => "SHIELDERS",
            EnemyRole.Elite => "ELITES",
            EnemyRole.Blinker => "BLINKERS",
            EnemyRole.Commander => "COMMANDERS",
            _ => "SCOUTS"
        };

        public static string Hint(EnemyRole role) => role switch
        {
            EnemyRole.Gunner => "GREEN GUNNERS AIM A LASER SIGHT - MOVE WHEN YOU SEE IT",
            EnemyRole.Charger => "RED CHARGERS FLASH, THEN RAM YOUR POSITION - SIDESTEP LATE",
            EnemyRole.Bomber => "PURPLE BOMBERS DROP MINES - DEATH DETONATIONS CHAIN REACT!",
            EnemyRole.Shielder => "BLUE SHIELDERS PROTECT ALLIES - KILL THEM TO TRIGGER SHIELD OVERLOAD!",
            EnemyRole.Elite => "GOLD ELITES FIRE WIDE FANS AND TAKE A BEATING",
            EnemyRole.Blinker => "PINK BLINKERS TELEPORT AND FIRE ON ARRIVAL",
            EnemyRole.Commander => "VIOLET COMMANDER LEADS THE SQUAD - KILL IT TO PANIC THE FLEET",
            _ => ""
        };
    }
}
