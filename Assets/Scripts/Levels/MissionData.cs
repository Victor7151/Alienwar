using UnityEngine;
using Starfall.Core;

namespace Starfall.Levels
{
    /// <summary>
    /// Data-driven mission definition as specified in Section 17 & 32 of Master Directive v1.0.
    /// Defines waves, formations, hazards, boss encounters, and rewards for a single campaign mission.
    /// </summary>
    [CreateAssetMenu(fileName = "NewMissionData", menuName = "STARFALL/Mission Data")]
    public class MissionData : ScriptableObject
    {
        [Header("Identity (§49)")]
        [Tooltip("Canonical identifier e.g. MISSION_EARTH_01")]
        [SerializeField] private string missionId = "MISSION_EARTH_01";
        [Tooltip("Canonical world identifier e.g. WORLD_EARTH")]
        [SerializeField] private string worldId = "WORLD_EARTH";
        [SerializeField] private int missionNumber = 1;
        [SerializeField] private string displayName = "First Contact";
        [TextArea(2, 4)]
        [SerializeField] private string description = "Interplanetary radar detects anomalous objects approaching Earth Orbit.";

        [Header("Story / Cinematic Video")]
        [Tooltip("Optional cutscene video played before this mission begins")]
        [SerializeField] private UnityEngine.Video.VideoClip introCutscene;

        [Header("Pacing & Waves (§33 & §34)")]
        [SerializeField] private WaveDefinition[] waves;
        [SerializeField] private float restBetweenWaves = 2.5f;

        [Header("Boss Encounter (§22)")]
        [SerializeField] private bool hasBoss = false;
        [SerializeField] private string bossPoolKey = "InvaderCarrier";
        [SerializeField] private float bossSpawnDelay = 2.0f;
        [SerializeField] private Vector3 bossSpawnPosition = new Vector3(0f, 8.5f, 0f);

        [Header("Progression & Rewards (§14 & §21)")]
        [SerializeField] private int starCreditsReward = 150;
        [SerializeField] private int scoreTarget = 1000;
        [Tooltip("If true, completing this mission activates the signature Warp sequence to the next world.")]
        [SerializeField] private bool isWarpMission = false;

        // Public Accessors
        public string MissionId => missionId;
        public string WorldId => worldId;
        public int MissionNumber => missionNumber;
        public string DisplayName => displayName;
        public string Description => description;
        public WaveDefinition[] Waves => waves;
        public float RestBetweenWaves => restBetweenWaves;
        public bool HasBoss => hasBoss;
        public string BossPoolKey => bossPoolKey;
        public float BossSpawnDelay => bossSpawnDelay;
        public Vector3 BossSpawnPosition => bossSpawnPosition;
        public int StarCreditsReward => starCreditsReward;
        public int ScoreTarget => scoreTarget;
        public bool IsWarpMission => isWarpMission;
        public UnityEngine.Video.VideoClip IntroCutscene => introCutscene;
    }
}
