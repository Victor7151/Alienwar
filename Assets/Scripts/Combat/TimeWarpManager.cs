using System.Collections;
using UnityEngine;
using Starfall.Enemies;
using Starfall.Combat;
using Starfall.VFX;

namespace Starfall.Combat
{
    /// <summary>
    /// Manages the Time Warp / Chrono Freeze power-up.
    /// Distorts temporal fields, slowing all enemies and enemy bullets by 65% while the player moves freely.
    /// </summary>
    public class TimeWarpManager : MonoBehaviour
    {
        public static TimeWarpManager Instance { get; private set; }

        private Coroutine activeWarpCoroutine;
        private bool isTimeWarpActive;

        public bool IsTimeWarpActive => isTimeWarpActive;
        public float TimeScaleMultiplier => isTimeWarpActive ? 0.35f : 1.0f;

        /// <summary>Scale applied to all hostile motion, fire cadence and enemy bullets.</summary>
        public static float EnemyTimeScale => (Instance != null && Instance.isTimeWarpActive) ? 0.35f : 1f;

        /// <summary>Time.deltaTime as experienced by enemies (slowed while Time Warp is active).</summary>
        public static float EnemyDeltaTime => Time.deltaTime * EnemyTimeScale;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public static void TriggerTimeWarp(float duration)
        {
            if (Instance == null)
            {
                var go = new GameObject("TimeWarpManager");
                Instance = go.AddComponent<TimeWarpManager>();
            }

            Instance.StartWarp(duration);
        }

        public void StartWarp(float duration)
        {
            if (activeWarpCoroutine != null) StopCoroutine(activeWarpCoroutine);
            activeWarpCoroutine = StartCoroutine(WarpRoutine(duration));
        }

        private IEnumerator WarpRoutine(float duration)
        {
            isTimeWarpActive = true;
            Debug.Log($"<color=#d444ff>[TimeWarp] Temporal Field Dilation Active! ({duration}s)</color>");

            if (CameraShake.Instance != null) CameraShake.Instance.Shake(0.15f, 0.2f);
            Starfall.Audio.AudioManager.Instance?.PlayPowerUp();

            Starfall.VFX.VFXKit.FlashScreen(new Color(0.7f, 0.25f, 1f, 0.35f), 0.35f);

            yield return new WaitForSeconds(duration);

            isTimeWarpActive = false;
            activeWarpCoroutine = null;
            Debug.Log("<color=cyan>[TimeWarp] Temporal Field Restored to normal time.</color>");
        }
    }
}
