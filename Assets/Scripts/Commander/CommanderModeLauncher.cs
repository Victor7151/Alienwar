using UnityEngine;
using UnityEngine.SceneManagement;
using Starfall.Core;
using Starfall.Levels;
using Starfall.Enemies;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Starfall.Commander
{
    /// <summary>
    /// Utility to launch and initialize Fleet Commander battle mode.
    /// Supports:
    /// 1. In-game launch from Home Hub.
    /// 2. Unity Editor menu item: [STARFALL > Launch Fleet Commander Mode in Active Scene].
    /// 3. Automatically suppresses standard mission wave spawns so the arena is dedicated to the Commander Duel.
    /// </summary>
    public class CommanderModeLauncher : MonoBehaviour
    {
        public static bool AutoStartOnSceneLoad = false;

        private void Start()
        {
            if (AutoStartOnSceneLoad)
            {
                AutoStartOnSceneLoad = false;
                InitializeCommanderMode();
            }
        }

        public static void StartCommanderModeFromHub()
        {
            AutoStartOnSceneLoad = true;
            SceneManager.LoadScene("Level1_EarthOrbit");
        }

        public static void InitializeCommanderMode()
        {
            Debug.Log("<color=cyan>[CommanderModeLauncher] Initializing Fleet Commander Battle Mode...</color>");

            // 1. Suppress standard campaign enemy spawner waves
            var spawner = Object.FindAnyObjectByType<EnemySpawner>();
            if (spawner != null)
            {
                spawner.enabled = false;
            }

            var waveMgr = Object.FindAnyObjectByType<WaveManager>();
            if (waveMgr != null)
            {
                waveMgr.enabled = false;
            }

            // 2. Clear any lingering standard campaign enemies
            var enemies = Object.FindObjectsByType<EnemyBase>();
            for (int i = 0; i < enemies.Length; i++)
            {
                if (enemies[i] != null) Object.Destroy(enemies[i].gameObject);
            }

            // 3. Ensure CommanderBattleManager exists
            var battleMgr = Object.FindAnyObjectByType<CommanderBattleManager>();
            if (battleMgr == null)
            {
                var mgrGo = new GameObject("CommanderBattleManager");
                mgrGo.AddComponent<CommanderBattleManager>();
            }

            // 4. Ensure CommanderHUD exists
            var hud = Object.FindAnyObjectByType<CommanderHUD>();
            if (hud == null)
            {
                var hudGo = new GameObject("CommanderHUD");
                hudGo.AddComponent<CommanderHUD>();
            }

            // 5. Reposition Player flagship at lower baseline
            var player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                float bottomY = PlayfieldBounds.Bottom + 1.25f;
                player.transform.position = new Vector3(0f, bottomY, 0f);
            }

            Debug.Log("<color=green>[CommanderModeLauncher] Fleet Commander Duel is LIVE! Use [1], [2], [3], [4] or tap HUD cards to summon units.</color>");
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void OnSceneLoaded()
        {
            if (AutoStartOnSceneLoad)
            {
                AutoStartOnSceneLoad = false;
                InitializeCommanderMode();
            }
        }

#if UNITY_EDITOR
        [MenuItem("STARFALL/Launch Fleet Commander Mode in Active Scene")]
        public static void EditorLaunchInActiveScene()
        {
            if (!Application.isPlaying)
            {
                EditorUtility.DisplayDialog(
                    "Play Mode Required",
                    "Please press Play in the Unity Editor first, then select this menu item to start Commander Mode in the running scene.",
                    "OK"
                );
                return;
            }

            InitializeCommanderMode();
        }
#endif
    }
}
