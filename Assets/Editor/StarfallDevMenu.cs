using UnityEditor;
using UnityEngine;
using Starfall.Levels;
using Starfall.Core;
using Starfall.UI;

namespace Starfall.Editor
{
    public static class StarfallDevMenu
    {
        [MenuItem("STARFALL/Hangar & Credits/Add 50,000 Credits", false, 10)]
        public static void AddCreditsCheat()
        {
            LevelProgressManager.AddCredits(50000);
            Debug.Log($"<color=green>[DevCheat] Added 50,000 Star Credits! Current Balance: {LevelProgressManager.GetCredits():N0} CR</color>");
            RefreshOpenHomeHub();
        }

        [MenuItem("STARFALL/Hangar & Credits/Unlock All 5 Warships", false, 11)]
        public static void UnlockAllWarshipsCheat()
        {
            foreach (var shipId in FleetDatabase.AllShipIds)
            {
                PlayerPrefs.SetInt($"Starfall_ShipUnlocked_{shipId}", 1);
            }
            PlayerPrefs.Save();
            Debug.Log("<color=cyan>[DevCheat] All 5 Fleet Warships Unlocked!</color>");
            RefreshOpenHomeHub();
        }

        [MenuItem("STARFALL/Hangar & Credits/Max Out All Upgrades (Active Ship)", false, 12)]
        public static void MaxOutActiveShipCheat()
        {
            string shipId = ShipUpgradesManager.GetActiveShipId();
            string[] upgrades = {
                ShipUpgradesManager.UPGRADE_HULL,
                ShipUpgradesManager.UPGRADE_WEAPONS,
                ShipUpgradesManager.UPGRADE_SPEED,
                ShipUpgradesManager.UPGRADE_ABILITY
            };
            foreach (var u in upgrades)
            {
                PlayerPrefs.SetInt($"Starfall_ShipUpgrade_{shipId}_{u}", ShipUpgradesManager.MAX_SHIP_UPGRADE_TIER);
            }
            PlayerPrefs.Save();
            Debug.Log($"<color=gold>[DevCheat] Active warship ({shipId}) upgraded to MAX Tier 5 in all categories!</color>");
            RefreshOpenHomeHub();
        }

        [MenuItem("STARFALL/Hangar & Credits/Reset All Fleet Data", false, 20)]
        public static void ResetFleetCheat()
        {
            ShipUpgradesManager.ResetAllUpgrades();
            PlayerPrefs.SetInt("Starfall_StarCredits", 1000);
            PlayerPrefs.Save();
            Debug.Log("<color=orange>[DevCheat] All Warships and Upgrades reset to defaults.</color>");
            RefreshOpenHomeHub();
        }

        [MenuItem("STARFALL/Campaign Sectors/Unlock All Sectors (1, 2, 3)", false, 30)]
        public static void UnlockAllSectorsCheat()
        {
            LevelProgressManager.UnlockAllLevels();
            Debug.Log("<color=green>[DevCheat] All campaign sectors (1, 2, 3) UNLOCKED!</color>");
            RefreshOpenHomeHub();
        }

        [MenuItem("STARFALL/Campaign Sectors/Unlock All Waves Across All Sectors", false, 31)]
        public static void UnlockAllWavesCheat()
        {
            LevelProgressManager.UnlockAllLevels();
            for (int lvl = 1; lvl <= 3; lvl++)
            {
                PlayerPrefs.SetInt($"Starfall_Level_{lvl}_HighestWave", 10);
                PlayerPrefs.SetInt($"Starfall_Level_{lvl}_CurrentWave", 10);
            }
            PlayerPrefs.Save();
            Debug.Log("<color=cyan>[DevCheat] All 10 waves unlocked across Sectors 1, 2, and 3!</color>");
            RefreshOpenHomeHub();
        }

        [MenuItem("STARFALL/Campaign Sectors/Reset Campaign to Sector 1 Only", false, 32)]
        public static void ResetCampaignToLevelOneCheat()
        {
            LevelProgressManager.ResetProgress(resetToLevelOne: true);
            Debug.Log("<color=orange>[DevCheat] Campaign progression reset: Only Sector 1 unlocked.</color>");
            RefreshOpenHomeHub();
        }

        private static void RefreshOpenHomeHub()
        {
            var hub = Object.FindAnyObjectByType<HomeHubUI>();
            if (hub != null)
            {
                hub.RefreshAllViews();
            }
        }
    }
}
