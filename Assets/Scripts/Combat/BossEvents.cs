using UnityEngine;

namespace Starfall.Combat
{
    /// <summary>
    /// Unified event bus for mini-bosses and level flagship bosses.
    /// Drives the HUD Boss Health Bar, phase alerts, and mission progression.
    /// </summary>
    public static class BossEvents
    {
        public static event System.Action<string, int, int> OnBossSpawned;       // (bossName, currentHP, maxHP)
        public static event System.Action<int, int>         OnBossHealthChanged; // (currentHP, maxHP)
        public static event System.Action<string, Color>    OnBossPhaseChanged;   // (phaseName, alertColor)
        public static event System.Action<string>           OnBossDefeated;      // (bossName)

        public static void TriggerBossSpawned(string bossName, int currentHP, int maxHP)
        {
            OnBossSpawned?.Invoke(bossName, currentHP, maxHP);
        }

        public static void TriggerBossHealthChanged(int currentHP, int maxHP)
        {
            OnBossHealthChanged?.Invoke(currentHP, maxHP);
        }

        public static void TriggerBossPhaseChanged(string phaseName, Color alertColor)
        {
            OnBossPhaseChanged?.Invoke(phaseName, alertColor);
        }

        public static void TriggerBossDefeated(string bossName = "")
        {
            OnBossDefeated?.Invoke(bossName);
        }
    }
}
