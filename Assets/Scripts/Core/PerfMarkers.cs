using Unity.Profiling;

namespace Starfall.Core
{
    /// <summary>
    /// Named timing markers around the game's hot code paths. They show up in the Unity Profiler
    /// and are read live by <see cref="Starfall.UI.PerformanceOverlay"/> to report per-system cost.
    /// Cost when not recording is negligible.
    /// </summary>
    public static class PerfMarkers
    {
        public static readonly ProfilerMarker EnemyUpdate = new ProfilerMarker("Starfall.Enemies");
        public static readonly ProfilerMarker FormationUpdate = new ProfilerMarker("Starfall.Formation");
        public static readonly ProfilerMarker ProjectileUpdate = new ProfilerMarker("Starfall.Projectiles");
        public static readonly ProfilerMarker PoolSpawn = new ProfilerMarker("Starfall.PoolSpawn");
        public static readonly ProfilerMarker VFXUpdate = new ProfilerMarker("Starfall.VFX");
        public static readonly ProfilerMarker PlayerUpdate = new ProfilerMarker("Starfall.Player");
        public static readonly ProfilerMarker PickupUpdate = new ProfilerMarker("Starfall.PowerUps");
        public static readonly ProfilerMarker HazardUpdate = new ProfilerMarker("Starfall.Hazards");
        public static readonly ProfilerMarker HudUpdate = new ProfilerMarker("Starfall.HUD");
        public static readonly ProfilerMarker BackgroundUpdate = new ProfilerMarker("Starfall.Background");
        public static readonly ProfilerMarker BossUpdate = new ProfilerMarker("Starfall.Bosses");
        public static readonly ProfilerMarker WaveBookkeeping = new ProfilerMarker("Starfall.WaveCount");

        public static readonly ProfilerMarker[] All =
        {
            EnemyUpdate, FormationUpdate, ProjectileUpdate, PoolSpawn, VFXUpdate, PlayerUpdate,
            PickupUpdate, HazardUpdate, HudUpdate, BackgroundUpdate, BossUpdate, WaveBookkeeping
        };
    }
}
