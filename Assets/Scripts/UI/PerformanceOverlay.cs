using System.Collections.Generic;
using System.IO;
using System.Text;
using Unity.Profiling;
using UnityEngine;
using UnityEngine.InputSystem;
using Starfall.Core;

namespace Starfall.UI
{
    /// <summary>
    /// Built-in performance monitor.
    ///   F3  – show / hide the overlay (three-finger tap on mobile)
    ///   F4  – turn all Rive HUD graphics on / off (A/B test; saved)
    ///   F6  – write a full snapshot to the Console
    ///
    /// Every second it samples Unity's own counters (main thread, GPU wait, GC allocations,
    /// batches, physics, UI canvas rebuild) plus timing markers around the game's systems,
    /// works out the most likely cause of slowness, and:
    ///   • appends a row to &lt;persistentDataPath&gt;/starfall_perf.csv (always)
    ///   • logs a [Perf] warning to the Console when FPS drops or a frame spikes.
    /// </summary>
    public class PerformanceOverlay : MonoBehaviour
    {
        private static PerformanceOverlay instance;

        private struct Probe
        {
            public string label;
            public ProfilerRecorder recorder;
            public double sumMs;      // accumulated this window
            public double avgMs;      // last window average
        }

        private readonly List<Probe> engineProbes = new List<Probe>();
        private readonly List<Probe> gameProbes = new List<Probe>();
        private ProfilerRecorder gcAllocRec, batchesRec, setPassRec, drawCallsRec;
        private double gcSumKB, batchesSum, setPassSum, drawSum;
        private double gcAvgKB, batchesAvg, setPassAvg, drawAvg;

        private bool visible;
        private float windowTime;
        private int windowFrames;
        private float windowWorst;
        private float windowDtSum;
        private float fpsAvg = 60f, frameMsAvg = 16.7f, worstMs;
        private string diagnosis = "collecting…";
        private string overlayText = "";
        private float lastWarnTime = -100f;
        private float lastSpikeTime = -100f;
        private readonly Dictionary<string, int> lastExpansions = new Dictionary<string, int>();
        private readonly List<ObjectPool.PoolStat> poolStats = new List<ObjectPool.PoolStat>();
        private string csvPath;
        private GUIStyle style;
        private readonly StringBuilder sb = new StringBuilder(1024);

        public static void Ensure()
        {
            if (instance != null) return;
            var go = new GameObject("PerformanceOverlay");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<PerformanceOverlay>();
        }

        // ------------------------------------------------------------------
        // Setup
        // ------------------------------------------------------------------

        private void OnEnable()
        {
            AddProbe(engineProbes, "MainThread", ProfilerCategory.Internal, "Main Thread");
            AddProbe(engineProbes, "GPU wait", ProfilerCategory.Render, "Gfx.WaitForPresentOnGfxThread");
            AddProbe(engineProbes, "Render", ProfilerCategory.Render, "Camera.Render");
            AddProbe(engineProbes, "Scripts", ProfilerCategory.Scripts, "BehaviourUpdate");
            AddProbe(engineProbes, "Physics2D", ProfilerCategory.Physics, "Physics2D.Simulate");
            AddProbe(engineProbes, "UI rebuild", ProfilerCategory.Gui, "Canvas.SendWillRenderCanvases");
            AddProbe(engineProbes, "Particles", ProfilerCategory.Particles, "ParticleSystem.Update");
            AddProbe(engineProbes, "GC collect", ProfilerCategory.Scripts, "GC.Collect");

            foreach (var m in PerfMarkers.All)
                AddProbe(gameProbes, MarkerLabel(m), ProfilerCategory.Scripts, MarkerName(m));

            gcAllocRec = ProfilerRecorder.StartNew(ProfilerCategory.Memory, "GC Allocated In Frame");
            batchesRec = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Batches Count");
            setPassRec = ProfilerRecorder.StartNew(ProfilerCategory.Render, "SetPass Calls Count");
            drawCallsRec = ProfilerRecorder.StartNew(ProfilerCategory.Render, "Draw Calls Count");

            try
            {
                csvPath = Path.Combine(Application.persistentDataPath, "starfall_perf.csv");
                File.WriteAllText(csvPath, "time,scene,wave,fps,frame_ms,worst_ms,mainthread_ms,gpu_wait_ms,render_ms,scripts_ms,physics_ms,ui_ms,particles_ms,gc_kb_frame,batches,setpass,enemies,enemy_bullets,rive_panels,top_game_system,diagnosis\n");
                Debug.Log($"[Perf] Monitor running. F3 overlay, F4 toggle Rive, F6 snapshot. CSV log: {csvPath}");
            }
            catch { csvPath = null; }
        }

        private void OnDisable()
        {
            foreach (var p in engineProbes) p.recorder.Dispose();
            foreach (var p in gameProbes) p.recorder.Dispose();
            engineProbes.Clear();
            gameProbes.Clear();
            gcAllocRec.Dispose(); batchesRec.Dispose(); setPassRec.Dispose(); drawCallsRec.Dispose();
        }

        private static string MarkerName(ProfilerMarker m)
        {
            // ProfilerMarker doesn't expose its name; keep the list in sync with PerfMarkers.
            if (m.Handle == PerfMarkers.EnemyUpdate.Handle) return "Starfall.Enemies";
            if (m.Handle == PerfMarkers.FormationUpdate.Handle) return "Starfall.Formation";
            if (m.Handle == PerfMarkers.ProjectileUpdate.Handle) return "Starfall.Projectiles";
            if (m.Handle == PerfMarkers.PoolSpawn.Handle) return "Starfall.PoolSpawn";
            if (m.Handle == PerfMarkers.VFXUpdate.Handle) return "Starfall.VFX";
            if (m.Handle == PerfMarkers.PlayerUpdate.Handle) return "Starfall.Player";
            if (m.Handle == PerfMarkers.PickupUpdate.Handle) return "Starfall.PowerUps";
            if (m.Handle == PerfMarkers.HazardUpdate.Handle) return "Starfall.Hazards";
            if (m.Handle == PerfMarkers.HudUpdate.Handle) return "Starfall.HUD";
            if (m.Handle == PerfMarkers.BackgroundUpdate.Handle) return "Starfall.Background";
            if (m.Handle == PerfMarkers.BossUpdate.Handle) return "Starfall.Bosses";
            return "Starfall.WaveCount";
        }

        private static string MarkerLabel(ProfilerMarker m) => MarkerName(m).Replace("Starfall.", "");

        private static void AddProbe(List<Probe> list, string label, ProfilerCategory cat, string name)
        {
            list.Add(new Probe { label = label, recorder = ProfilerRecorder.StartNew(cat, name) });
        }

        private static double LastMs(ProfilerRecorder r) => r.Valid ? r.LastValue * 1e-6 : 0.0;

        // ------------------------------------------------------------------
        // Per frame
        // ------------------------------------------------------------------

        private void Update()
        {
            HandleKeys();

            float dt = Time.unscaledDeltaTime;
            windowTime += dt;
            windowFrames++;
            windowDtSum += dt;
            windowWorst = Mathf.Max(windowWorst, dt);

            for (int i = 0; i < engineProbes.Count; i++) { var p = engineProbes[i]; p.sumMs += LastMs(p.recorder); engineProbes[i] = p; }
            for (int i = 0; i < gameProbes.Count; i++) { var p = gameProbes[i]; p.sumMs += LastMs(p.recorder); gameProbes[i] = p; }
            if (gcAllocRec.Valid) gcSumKB += gcAllocRec.LastValue / 1024.0;
            if (batchesRec.Valid) batchesSum += batchesRec.LastValue;
            if (setPassRec.Valid) setPassSum += setPassRec.LastValue;
            if (drawCallsRec.Valid) drawSum += drawCallsRec.LastValue;

            // Individual spike: log what this frame spent its time on.
            if (dt > 0.1f && Time.realtimeSinceStartup - lastSpikeTime > 1f && Time.frameCount > 120)
            {
                lastSpikeTime = Time.realtimeSinceStartup;
                Debug.LogWarning($"[Perf] SPIKE {dt * 1000f:F0} ms frame. {SpikeBreakdown()}");
            }

            if (windowTime >= 1f) CloseWindow();
        }

        private void HandleKeys()
        {
            var kb = Keyboard.current;
            var ts = Touchscreen.current;
            bool toggle = kb != null && kb.f3Key.wasPressedThisFrame;
            if (!toggle && ts != null && ts.touches.Count >= 3 && ts.touches[2].press.wasPressedThisFrame) toggle = true;
            if (toggle) visible = !visible;

            if (kb != null && kb.f4Key.wasPressedThisFrame)
                Starfall.UI.Rive.RiveAnimationManager.SetRiveEnabled(!Starfall.UI.Rive.RiveAnimationManager.RiveEnabled);

            if (kb != null && kb.f6Key.wasPressedThisFrame)
                Debug.Log("[Perf] SNAPSHOT\n" + BuildReport(true));
        }

        private string SpikeBreakdown()
        {
            sb.Clear();
            for (int i = 0; i < engineProbes.Count; i++)
            {
                double ms = LastMs(engineProbes[i].recorder);
                if (ms >= 1.0) sb.Append(engineProbes[i].label).Append(' ').Append(ms.ToString("F1")).Append("ms  ");
            }
            for (int i = 0; i < gameProbes.Count; i++)
            {
                double ms = LastMs(gameProbes[i].recorder);
                if (ms >= 0.5) sb.Append(gameProbes[i].label).Append(' ').Append(ms.ToString("F1")).Append("ms  ");
            }
            if (gcAllocRec.Valid) sb.Append("GC alloc ").Append((gcAllocRec.LastValue / 1024.0).ToString("F0")).Append("KB  ");
            sb.Append("enemies ").Append(Starfall.Enemies.EnemyBase.Active.Count);
            sb.Append(" bullets ").Append(Starfall.Combat.Projectile.ActiveEnemyCount);
            if (System.GC.CollectionCount(0) != lastGcCount) { sb.Append("  (GC collection ran)"); }
            return sb.ToString();
        }

        private int lastGcCount;

        // ------------------------------------------------------------------
        // Once per second
        // ------------------------------------------------------------------

        private void CloseWindow()
        {
            float frames = Mathf.Max(1, windowFrames);
            frameMsAvg = windowDtSum / frames * 1000f;
            fpsAvg = 1000f / Mathf.Max(0.01f, frameMsAvg);
            worstMs = windowWorst * 1000f;

            for (int i = 0; i < engineProbes.Count; i++) { var p = engineProbes[i]; p.avgMs = p.sumMs / frames; p.sumMs = 0; engineProbes[i] = p; }
            for (int i = 0; i < gameProbes.Count; i++) { var p = gameProbes[i]; p.avgMs = p.sumMs / frames; p.sumMs = 0; gameProbes[i] = p; }
            gcAvgKB = gcSumKB / frames; gcSumKB = 0;
            batchesAvg = batchesSum / frames; batchesSum = 0;
            setPassAvg = setPassSum / frames; setPassSum = 0;
            drawAvg = drawSum / frames; drawSum = 0;
            lastGcCount = System.GC.CollectionCount(0);

            diagnosis = Diagnose(out string topSystem);
            overlayText = BuildReport(false);
            WriteCsv(topSystem);

            bool slow = fpsAvg < 45f || worstMs > 80f;
            if (slow && Time.realtimeSinceStartup - lastWarnTime > 5f && Time.frameCount > 120)
            {
                lastWarnTime = Time.realtimeSinceStartup;
                Debug.LogWarning($"[Perf] {fpsAvg:F0} FPS ({frameMsAvg:F1} ms, worst {worstMs:F0} ms). LIKELY CAUSE: {diagnosis}\n{BuildReport(true)}");
            }

            windowTime = 0f;
            windowFrames = 0;
            windowDtSum = 0f;
            windowWorst = 0f;
        }

        private double Engine(string label)
        {
            foreach (var p in engineProbes) if (p.label == label) return p.recorder.Valid ? p.avgMs : -1;
            return -1;
        }

        private string Diagnose(out string topSystem)
        {
            topSystem = "-";
            double topMs = 0;
            foreach (var p in gameProbes)
                if (p.avgMs > topMs) { topMs = p.avgMs; topSystem = $"{p.label} {p.avgMs:F2}ms"; }

            double frame = frameMsAvg;
            double gpuWait = Engine("GPU wait");
            double physics = Engine("Physics2D");
            double ui = Engine("UI rebuild");
            double particles = Engine("Particles");
            double scripts = Engine("Scripts");

            var causes = new List<string>();
            if (frame < 18.5) return "none — running at full speed";

            if (gpuWait > frame * 0.35)
                causes.Add($"GPU-bound: {gpuWait:F1} ms/frame waiting on the graphics card. Lower the Game view resolution (e.g. 1280x720), close other GPU apps, or reduce full-screen layers (backgrounds, glow).");
            if (topMs > 2.0)
                causes.Add($"Game code: {topSystem} per frame.");
            if (scripts > 6.0 && topMs <= 2.0)
                causes.Add($"Scripts {scripts:F1} ms — mostly outside Starfall markers (editor tools / packages such as Unity MCP).");
            if (gcAvgKB > 20)
                causes.Add($"Garbage: {gcAvgKB:F0} KB allocated per frame (causes GC hitches).");
            if (physics > 3.0)
                causes.Add($"Physics2D {physics:F1} ms (too many colliders / bullets).");
            if (ui > 2.0)
                causes.Add($"UI canvas rebuilds {ui:F1} ms (HUD text/images changing every frame).");
            if (particles > 2.0)
                causes.Add($"Particle systems {particles:F1} ms.");
            if (batchesAvg > 400)
                causes.Add($"{batchesAvg:F0} draw batches per frame.");

            // Pool leak check: a pool that keeps expanding is not getting objects back.
            if (ObjectPool.Instance != null)
            {
                ObjectPool.Instance.GetStats(poolStats);
                foreach (var ps in poolStats)
                {
                    lastExpansions.TryGetValue(ps.key, out int prev);
                    if (ps.expansions - prev >= 10 && ps.active > 0)
                        causes.Add($"Pool '{ps.key}' grew by {ps.expansions - prev} this second ({ps.active} active) — check if instances are returning.");
                    lastExpansions[ps.key] = ps.expansions;
                }
            }

            if (causes.Count == 0)
                causes.Add(Application.isEditor
                    ? "No single game system stands out — likely Unity Editor overhead (Scene view open, Inspector repainting, packages). Compare with a Windows build."
                    : "No single system stands out — check device thermal throttling / power saving.");
            return string.Join(" | ", causes);
        }

        private string BuildReport(bool full)
        {
            sb.Clear();
            sb.Append($"FPS {fpsAvg:F0}  frame {frameMsAvg:F1} ms  worst {worstMs:F0} ms\n");
            foreach (var p in engineProbes)
                if (p.recorder.Valid) sb.Append(p.label).Append(' ').Append(p.avgMs.ToString("F1")).Append("  ");
            sb.Append('\n');
            sb.Append($"GC {gcAvgKB:F1} KB/f  batches {batchesAvg:F0}  setpass {setPassAvg:F0}  draws {drawAvg:F0}\n");

            sb.Append("Game: ");
            foreach (var p in gameProbes)
                if (full || p.avgMs >= 0.05) sb.Append(p.label).Append(' ').Append(p.avgMs.ToString("F2")).Append("  ");
            sb.Append('\n');

            var rive = Starfall.UI.Rive.RiveAnimationManager.Instance;
            sb.Append($"enemies {Starfall.Enemies.EnemyBase.Active.Count}  enemy bullets {Starfall.Combat.Projectile.ActiveEnemyCount}  ");
            sb.Append($"Rive {(Starfall.UI.Rive.RiveAnimationManager.RiveEnabled ? "ON" : "OFF")} ({(rive != null ? rive.ActiveRivePanels : 0)} panels)  timeScale {Time.timeScale:F2}\n");

            if (full && ObjectPool.Instance != null)
            {
                ObjectPool.Instance.GetStats(poolStats);
                poolStats.Sort((a, b) => b.active.CompareTo(a.active));
                sb.Append("Pools (active/created/expansions): ");
                for (int i = 0; i < poolStats.Count && i < 10; i++)
                    sb.Append(poolStats[i].key).Append(' ').Append(poolStats[i].active).Append('/').Append(poolStats[i].created).Append('/').Append(poolStats[i].expansions).Append("  ");
                sb.Append('\n');
            }

            sb.Append("Cause: ").Append(diagnosis);
            return sb.ToString();
        }

        private void WriteCsv(string topSystem)
        {
            if (csvPath == null) return;
            try
            {
                var rive = Starfall.UI.Rive.RiveAnimationManager.Instance;
                int wave = WaveManager.Instance != null ? WaveManager.Instance.CurrentWaveNumber : 0;
                string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                string line = string.Join(",",
                    Time.realtimeSinceStartup.ToString("F1"), scene, wave.ToString(),
                    fpsAvg.ToString("F1"), frameMsAvg.ToString("F2"), worstMs.ToString("F1"),
                    Engine("MainThread").ToString("F2"), Engine("GPU wait").ToString("F2"), Engine("Render").ToString("F2"),
                    Engine("Scripts").ToString("F2"), Engine("Physics2D").ToString("F2"), Engine("UI rebuild").ToString("F2"),
                    Engine("Particles").ToString("F2"), gcAvgKB.ToString("F1"), batchesAvg.ToString("F0"), setPassAvg.ToString("F0"),
                    Starfall.Enemies.EnemyBase.Active.Count.ToString(), Starfall.Combat.Projectile.ActiveEnemyCount.ToString(),
                    (rive != null ? rive.ActiveRivePanels : 0).ToString(),
                    "\"" + topSystem + "\"", "\"" + diagnosis.Replace("\"", "'") + "\"");
                File.AppendAllText(csvPath, line + "\n");
            }
            catch { /* disk full / sandboxed web build: ignore */ }
        }

        private void OnGUI()
        {
            if (!visible) return;
            if (style == null)
            {
                style = new GUIStyle(GUI.skin.box)
                {
                    fontSize = Mathf.Max(12, Screen.height / 70),
                    alignment = TextAnchor.UpperLeft,
                    wordWrap = true
                };
                style.normal.textColor = new Color(0.4f, 1f, 0.5f);
            }
            GUI.Box(new Rect(10, Screen.height * 0.3f, Screen.width * 0.55f, Screen.height * 0.3f),
                overlayText + "\n[F3] hide  [F4] Rive on/off  [F6] log snapshot", style);
        }
    }
}
