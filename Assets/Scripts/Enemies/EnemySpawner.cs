using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Starfall.Core;

namespace Starfall.Enemies
{
    [System.Serializable]
    public class EnemySpawnConfig
    {
        public string poolKey;
        public float weight = 1f;
    }

    /// <summary>
    /// Spawns formation sets for <see cref="WaveManager"/>.
    /// Layout and entry order come from <see cref="FormationGridController"/> (collision-free);
    /// this class picks each member's body (Scout / Fighter) and tactical role from the wave data.
    /// </summary>
    public class EnemySpawner : MonoBehaviour
    {
        [Header("Spawn Settings")]
        [Tooltip("Element 0 = light body (Scout), element 1 = heavy body (Fighter).")]
        [SerializeField] private EnemySpawnConfig[] enemyTypes;
        [SerializeField] private bool autoSpawn = false;

        private int currentWaveNumber = 1;
        private float waveHealthMultiplier = 1f;
        private float waveSpeedMultiplier = 1f;
        private float fighterRatio = 0.3f;

        public bool AutoSpawn { get => autoSpawn; set => autoSpawn = value; }

        private string ScoutKey => (enemyTypes != null && enemyTypes.Length > 0 && !string.IsNullOrEmpty(enemyTypes[0].poolKey)) ? enemyTypes[0].poolKey : "ScoutEnemy";
        private string FighterKey => (enemyTypes != null && enemyTypes.Length > 1 && !string.IsNullOrEmpty(enemyTypes[1].poolKey)) ? enemyTypes[1].poolKey : "FighterEnemy";

        private void Awake()
        {
            // All spawning is driven by WaveManager formations.
            autoSpawn = false;
        }

        private FormationGridController GetOrCreateGridController()
        {
            var grid = FormationGridController.Instance;
            if (grid == null)
            {
                var gridObj = new GameObject("FormationGridController");
                grid = gridObj.AddComponent<FormationGridController>();
            }
            return grid;
        }

        public void SetWaveTier(int waveNum, float healthMult, float speedMult)
        {
            currentWaveNumber = waveNum;
            waveHealthMultiplier = Mathf.Max(0.5f, healthMult);
            waveSpeedMultiplier = Mathf.Max(0.5f, speedMult);
        }

        /// <summary>Kept for WaveManager compatibility: sets the heavy-body share.</summary>
        public void SetWeights(float scoutWeight, float fighterWeight)
        {
            float total = Mathf.Max(0.0001f, scoutWeight + fighterWeight);
            fighterRatio = Mathf.Clamp01(fighterWeight / total);
        }

        /// <summary>No-op kept for older callers; bounds now come from <see cref="PlayfieldBounds"/>.</summary>
        public void UpdateSpawnBounds() { }

        // ------------------------------------------------------------------
        // Formation spawning
        // ------------------------------------------------------------------

        /// <summary>
        /// Builds and fills one formation set. Returns (via callback) the number of enemies spawned,
        /// which may be lower than requested on narrow screens.
        /// </summary>
        public IEnumerator SpawnFormation(FormationType shape, int requestedCount, WaveDefinition def, System.Action<int> onSpawned = null)
        {
            if (ObjectPool.Instance == null)
            {
                Debug.LogWarning("[EnemySpawner] ObjectPool.Instance is null!");
                onSpawned?.Invoke(0);
                yield break;
            }

            var grid = GetOrCreateGridController();
            int count = grid.BuildFormation(shape, requestedCount);
            var order = grid.GetSpawnOrder();
            var roles = BuildRoleList(count, def);

            EntryFlightPath path;
            if (shape == FormationType.VFormation || shape == FormationType.Pincer || shape == FormationType.FlankingColumns || shape == FormationType.Echelon)
                path = EntryFlightPath.FlankArcSwoop;
            else if (shape == FormationType.Crescent || shape == FormationType.Diamond || shape == FormationType.Checker || shape == FormationType.Swarm)
                path = EntryFlightPath.SpiralLoop;
            else
                path = (Random.value < 0.65f || grid.EntryStyle == FormationEntryStyle.Sweep) ? EntryFlightPath.SineWaveWeave : EntryFlightPath.DirectDrop;

            int spawned = 0;
            for (int i = 0; i < order.Count; i++)
            {
                int slot = order[i];
                EnemyRole role = roles[i];
                string key = PickBody(role);

                Vector3 start = grid.GetEntryStart(slot);
                var enemyObj = ObjectPool.Instance.Spawn(key, start, Quaternion.identity);
                if (enemyObj != null && enemyObj.TryGetComponent<EnemyBase>(out var enemy))
                {
                    enemy.SetWaveTier(currentWaveNumber, waveHealthMultiplier, waveSpeedMultiplier);
                    enemy.SetRole(role);
                    enemy.AssignToFormation(grid, 0, slot, path);
                    spawned++;
                }

                // Spread pool activation across frames. Spawning in fill order means any delay
                // only widens the gap to the member ahead, so spacing guarantees still hold.
                if ((i & 3) == 3) yield return null;
            }

            grid.FinalizeGridSetup();
            onSpawned?.Invoke(spawned);
        }

        public EnemyBase SpawnStealthEnemy(Vector3 position, Vector3 direction, float speed, float oscAmp = 0f, float oscFreq = 0f, EnemyRole role = EnemyRole.Grunt)
        {
            if (ObjectPool.Instance == null) return null;
            string key = PickBody(role);
            var enemyObj = ObjectPool.Instance.Spawn(key, position, Quaternion.identity);
            if (enemyObj != null && enemyObj.TryGetComponent<EnemyBase>(out var enemy))
            {
                enemy.SetWaveTier(currentWaveNumber, waveHealthMultiplier, waveSpeedMultiplier);
                enemy.SetRole(role);
                enemy.SetStealthTrajectory(direction, speed, oscAmp, oscFreq);
                return enemy;
            }
            return null;
        }

        private string PickBody(EnemyRole role)
        {
            switch (role)
            {
                case EnemyRole.Charger:
                case EnemyRole.Blinker:
                    return ScoutKey;
                case EnemyRole.Commander:
                case EnemyRole.Shielder:
                case EnemyRole.Bomber:
                case EnemyRole.Elite:
                    return FighterKey;
                default:
                    return Random.value < fighterRatio ? FighterKey : ScoutKey;
            }
        }

        /// <summary>Role for each spawn index, honouring the wave's role ratios (rounded, then shuffled).</summary>
        private static List<EnemyRole> BuildRoleList(int count, WaveDefinition def)
        {
            var list = new List<EnemyRole>(count);
            bool hasCommander = def != null && def.hasCommander && count > 0;
            if (hasCommander)
            {
                list.Add(EnemyRole.Commander);
            }

            if (def != null)
            {
                AddRole(list, EnemyRole.Elite, def.eliteRatio, count);
                AddRole(list, EnemyRole.Shielder, def.shielderRatio, count);
                AddRole(list, EnemyRole.Gunner, def.gunnerRatio, count);
                AddRole(list, EnemyRole.Charger, def.chargerRatio, count);
                AddRole(list, EnemyRole.Bomber, def.bomberRatio, count);
                AddRole(list, EnemyRole.Blinker, def.blinkerRatio, count);
            }
            if (list.Count > count) list.RemoveRange(count, list.Count - count);
            while (list.Count < count) list.Add(EnemyRole.Grunt);

            // Shuffle escorts while keeping Commander at the apex (index 0)
            int startIndex = hasCommander ? 1 : 0;
            for (int i = list.Count - 1; i > startIndex; i--)
            {
                int j = Random.Range(startIndex, i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
            return list;
        }

        private static void AddRole(List<EnemyRole> list, EnemyRole role, float ratio, int count)
        {
            if (ratio <= 0f) return;
            int n = Mathf.Max(1, Mathf.RoundToInt(ratio * count));
            for (int i = 0; i < n; i++) list.Add(role);
        }

        // ------------------------------------------------------------------
        // Legacy single spawn
        // ------------------------------------------------------------------

        /// <summary>Spawns one enemy into the first free formation slot (or as a raider if none).</summary>
        public void SpawnRandom()
        {
            if (ObjectPool.Instance == null) return;
            var grid = GetOrCreateGridController();
            float x = Random.Range(-PlayfieldBounds.PlayHalfWidth + 0.6f, PlayfieldBounds.PlayHalfWidth - 0.6f);
            var enemyObj = ObjectPool.Instance.Spawn(Random.value < fighterRatio ? FighterKey : ScoutKey, new Vector3(x, PlayfieldBounds.SpawnY, 0f), Quaternion.identity);
            if (enemyObj != null && enemyObj.TryGetComponent<EnemyBase>(out var enemy))
            {
                enemy.SetWaveTier(currentWaveNumber, waveHealthMultiplier, waveSpeedMultiplier);
                if (grid.RegisterInFirstFreeSlot(enemy, out int r, out int c))
                    enemy.AssignToFormation(grid, r, c);
            }
        }
    }
}
