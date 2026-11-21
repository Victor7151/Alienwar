using UnityEngine;
using Starfall.Levels;

namespace Starfall.Core
{
    /// <summary>
    /// Tracks permanent spaceship fleet ownership, active selected warship,
    /// and per-ship hangar upgrades purchased in the Armory Shop.
    /// Upgrades and ownership persist across game sessions via PlayerPrefs.
    /// </summary>
    public static class ShipUpgradesManager
    {
        public const string ACTIVE_SHIP_KEY = "Starfall_ActiveShipId";

        // Per-Ship Upgrade Categories
        public const string UPGRADE_HULL = "Hull";
        public const string UPGRADE_WEAPONS = "Weapons";
        public const string UPGRADE_SPEED = "Speed";
        public const string UPGRADE_ABILITY = "Ability";
        public const string UPGRADE_BOMBS = "Bombs";
        public const string UPGRADE_MAGNET = "Magnet";

        public const int MAX_SHIP_UPGRADE_TIER = 5;

        // Upgrade costs per tier (1 through 5)
        private static readonly int[] UpgradeCosts = { 800, 1600, 3200, 6000, 10000 };

        // -------------------------------------------------------------
        // ACTIVE WARSHIP MANAGEMENT
        // -------------------------------------------------------------
        public static string GetActiveShipId()
        {
            return PlayerPrefs.GetString(ACTIVE_SHIP_KEY, FleetDatabase.SHIP_VIPER);
        }

        public static void SetActiveShipId(string shipId)
        {
            if (IsShipUnlocked(shipId))
            {
                PlayerPrefs.SetString(ACTIVE_SHIP_KEY, shipId);
                PlayerPrefs.Save();
                Debug.Log($"<color=cyan>[ShipUpgradesManager] Active Warship set to: {shipId}</color>");
            }
        }

        public static WarshipSpec GetActiveShipSpec()
        {
            return FleetDatabase.GetShip(GetActiveShipId());
        }

        // -------------------------------------------------------------
        // WARSHIP OWNERSHIP & PURCHASING
        // -------------------------------------------------------------
        public static bool IsShipUnlocked(string shipId)
        {
            // Viper MK-I is the starter ship and always unlocked
            if (shipId == FleetDatabase.SHIP_VIPER) return true;

            return PlayerPrefs.GetInt($"Starfall_ShipUnlocked_{shipId}", 0) == 1;
        }

        public static bool PurchaseShip(string shipId)
        {
            if (IsShipUnlocked(shipId)) return false;

            var spec = FleetDatabase.GetShip(shipId);
            if (spec == null) return false;

            if (LevelProgressManager.SpendCredits(spec.creditPrice))
            {
                PlayerPrefs.SetInt($"Starfall_ShipUnlocked_{shipId}", 1);
                PlayerPrefs.SetString(ACTIVE_SHIP_KEY, shipId);
                PlayerPrefs.Save();
                Debug.Log($"<color=green>[ShipUpgradesManager] Successfully commissioned {spec.name}!</color>");
                return true;
            }

            return false;
        }

        // -------------------------------------------------------------
        // PER-SHIP UPGRADE PROGRESSION
        // -------------------------------------------------------------
        public static int GetShipUpgradeLevel(string shipId, string upgradeType)
        {
            return PlayerPrefs.GetInt($"Starfall_ShipUpgrade_{shipId}_{upgradeType}", 0);
        }

        public static int GetShipUpgradeCost(string shipId, string upgradeType)
        {
            int currentTier = GetShipUpgradeLevel(shipId, upgradeType);
            if (currentTier >= MAX_SHIP_UPGRADE_TIER) return -1; // Maxed
            return UpgradeCosts[currentTier];
        }

        public static bool PurchaseShipUpgrade(string shipId, string upgradeType)
        {
            int cost = GetShipUpgradeCost(shipId, upgradeType);
            if (cost < 0) return false;

            if (LevelProgressManager.SpendCredits(cost))
            {
                int nextTier = GetShipUpgradeLevel(shipId, upgradeType) + 1;
                PlayerPrefs.SetInt($"Starfall_ShipUpgrade_{shipId}_{upgradeType}", nextTier);
                PlayerPrefs.Save();
                Debug.Log($"<color=cyan>[ShipUpgradesManager] Upgraded {shipId} {upgradeType} to Tier {nextTier}!</color>");
                return true;
            }

            return false;
        }

        // -------------------------------------------------------------
        // -------------------------------------------------------------
        // RUNTIME STAT ACCESSORS (Active Ship + Upgrade Buffs)
        // -------------------------------------------------------------
        public static int GetEffectiveLives(string shipId)
        {
            var ship = FleetDatabase.GetShip(shipId);
            // Capped at 5 lives as directed by player balance requirements
            return Mathf.Clamp(ship.baseLives, 3, 5);
        }
        public static int GetEffectiveMaxLives(string shipId) => GetEffectiveLives(shipId);

        public static int GetEffectiveShields(string shipId)
        {
            var ship = FleetDatabase.GetShip(shipId);
            int hullTier = GetShipUpgradeLevel(ship.id, UPGRADE_HULL);
            return ship.baseShields + hullTier;
        }

        public static float GetEffectiveSpeed(string shipId)
        {
            var ship = FleetDatabase.GetShip(shipId);
            int speedTier = GetShipUpgradeLevel(ship.id, UPGRADE_SPEED);
            return ship.baseMoveSpeed + (speedTier * 1.0f);
        }
        public static float GetEffectiveMoveSpeed(string shipId) => GetEffectiveSpeed(shipId);

        public static float GetEffectiveDamageMultiplier(string shipId)
        {
            var ship = FleetDatabase.GetShip(shipId);
            int weaponTier = GetShipUpgradeLevel(ship.id, UPGRADE_WEAPONS);
            return ship.damageMultiplier * (1f + (weaponTier * 0.15f));
        }

        public static float GetEffectiveFireRateMultiplier(string shipId)
        {
            var ship = FleetDatabase.GetShip(shipId);
            int weaponTier = GetShipUpgradeLevel(ship.id, UPGRADE_WEAPONS);
            return ship.fireRateMultiplier * (1f + (weaponTier * 0.08f));
        }

        public static float GetEffectiveAbilityCooldown(string shipId)
        {
            var ship = FleetDatabase.GetShip(shipId);
            int abilityTier = GetShipUpgradeLevel(ship.id, UPGRADE_ABILITY);
            return Mathf.Max(6f, ship.abilityCooldown - (abilityTier * 1.5f));
        }

        // Parameterless overloads defaulting to active ship
        public static int GetEffectiveLives() => GetEffectiveLives(GetActiveShipId());
        public static int GetEffectiveMaxLives() => GetEffectiveLives(GetActiveShipId());
        public static int GetEffectiveShields() => GetEffectiveShields(GetActiveShipId());
        public static float GetEffectiveSpeed() => GetEffectiveSpeed(GetActiveShipId());
        public static float GetEffectiveMoveSpeed() => GetEffectiveSpeed(GetActiveShipId());
        public static float GetEffectiveDamageMultiplier() => GetEffectiveDamageMultiplier(GetActiveShipId());
        public static float GetEffectiveFireRateMultiplier() => GetEffectiveFireRateMultiplier(GetActiveShipId());
        public static float GetEffectiveAbilityCooldown() => GetEffectiveAbilityCooldown(GetActiveShipId());

        public static int GetExtraBombs()
        {
            return PlayerPrefs.GetInt("Starfall_Upgrade_Bombs", 0);
        }

        public static float GetTractorRadiusBonus()
        {
            return PlayerPrefs.GetInt("Starfall_Upgrade_Magnet", 0) * 1.5f;
        }

        // Legacy compatibility accessors
        public static int GetStartingWeaponTier() => 0;
        public static int GetExtraLives() => GetEffectiveLives() - 3;
        public static int GetExtraShields() => (GetEffectiveShields() - 3) * 25;

        public static void ResetAllUpgrades()
        {
            PlayerPrefs.DeleteKey(ACTIVE_SHIP_KEY);
            foreach (var shipId in FleetDatabase.AllShipIds)
            {
                if (shipId != FleetDatabase.SHIP_VIPER)
                {
                    PlayerPrefs.DeleteKey($"Starfall_ShipUnlocked_{shipId}");
                }
                PlayerPrefs.DeleteKey($"Starfall_ShipUpgrade_{shipId}_{UPGRADE_HULL}");
                PlayerPrefs.DeleteKey($"Starfall_ShipUpgrade_{shipId}_{UPGRADE_WEAPONS}");
                PlayerPrefs.DeleteKey($"Starfall_ShipUpgrade_{shipId}_{UPGRADE_SPEED}");
                PlayerPrefs.DeleteKey($"Starfall_ShipUpgrade_{shipId}_{UPGRADE_ABILITY}");
            }
            PlayerPrefs.DeleteKey("Starfall_Upgrade_Bombs");
            PlayerPrefs.DeleteKey("Starfall_Upgrade_Magnet");
            PlayerPrefs.Save();
            Debug.Log("[ShipUpgradesManager] All fleet warships and upgrades reset.");
        }
    }
}
