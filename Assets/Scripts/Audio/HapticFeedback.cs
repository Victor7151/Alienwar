using System;
using UnityEngine;

namespace Starfall.Audio
{
    public enum HapticType
    {
        Selection,
        LightImpact,
        MediumImpact,
        HeavyImpact,
        Success,
        Warning,
        Failure
    }

    /// <summary>
    /// Production-grade mobile haptic feedback engine for Android and iOS.
    /// Provides subtle, nuanced tactile vibrations for weapon firing, bullet hits,
    /// shield deflections, enemy explosions, tactical nukes, and UI button presses.
    /// Includes JNI vibrator caching, Android VibrationEffect support, and throttling.
    /// </summary>
    public static class HapticFeedback
    {
        private const string PREF_KEY = "Starfall_HapticsEnabled";
        private static bool? _isEnabled;
        private static float _lastHapticTime = -1f;
        private const float MinHapticInterval = 0.04f;

#if UNITY_ANDROID && !UNITY_EDITOR
        private static AndroidJavaObject _vibrator;
        private static AndroidJavaClass _vibrationEffectClass;
        private static bool _initialized = false;
        private static bool _hasVibrator = false;
        private static int _sdkInt = 0;
#endif

        public static bool Enabled
        {
            get
            {
                if (!_isEnabled.HasValue)
                {
                    _isEnabled = PlayerPrefs.GetInt(PREF_KEY, 1) == 1;
                }
                return _isEnabled.Value;
            }
            set
            {
                _isEnabled = value;
                PlayerPrefs.SetInt(PREF_KEY, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        static HapticFeedback()
        {
            InitPlatform();
        }

        private static void InitPlatform()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (_initialized) return;
            try
            {
                using (var versionClass = new AndroidJavaClass("android.os.Build$VERSION"))
                {
                    _sdkInt = versionClass.GetStatic<int>("SDK_INT");
                }

                using (var unityPlayer = new AndroidJavaClass("com.unity3d.player.UnityPlayer"))
                using (var activity = unityPlayer.GetStatic<AndroidJavaObject>("currentActivity"))
                {
                    if (activity != null)
                    {
                        _vibrator = activity.Call<AndroidJavaObject>("getSystemService", "vibrator");
                        if (_vibrator != null)
                        {
                            _hasVibrator = _vibrator.Call<bool>("hasVibrator");
                        }
                    }
                }

                if (_sdkInt >= 26)
                {
                    _vibrationEffectClass = new AndroidJavaClass("android.os.VibrationEffect");
                }
                _initialized = true;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[HapticFeedback] Failed to initialize Android vibrator: {ex.Message}");
                _hasVibrator = false;
                _initialized = true;
            }
#endif
        }

        public static void Trigger(HapticType type)
        {
            if (!Enabled) return;

            // Throttle rapidly repeating haptics to prevent hardware stutter
            if (Time.unscaledTime - _lastHapticTime < MinHapticInterval)
            {
                // Allow heavy impacts to override
                if (type != HapticType.HeavyImpact && type != HapticType.Failure)
                    return;
            }
            _lastHapticTime = Time.unscaledTime;

            switch (type)
            {
                case HapticType.Selection:
                    PlayVibration(12, 60);
                    break;
                case HapticType.LightImpact:
                    PlayVibration(22, 100);
                    break;
                case HapticType.MediumImpact:
                    PlayVibration(40, 160);
                    break;
                case HapticType.HeavyImpact:
                    PlayVibration(85, 255);
                    break;
                case HapticType.Success:
                    PlayVibration(30, 180);
                    break;
                case HapticType.Warning:
                    PlayVibration(50, 200);
                    break;
                case HapticType.Failure:
                    PlayVibration(100, 255);
                    break;
            }
        }

        public static void SelectionClick() => Trigger(HapticType.Selection);
        public static void LightImpact() => Trigger(HapticType.LightImpact);
        public static void MediumImpact() => Trigger(HapticType.MediumImpact);
        public static void HeavyImpact() => Trigger(HapticType.HeavyImpact);
        public static void Success() => Trigger(HapticType.Success);
        public static void Warning() => Trigger(HapticType.Warning);

        private static void PlayVibration(int durationMs, int amplitude)
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            if (!_initialized) InitPlatform();
            if (!_hasVibrator || _vibrator == null) return;

            try
            {
                if (_sdkInt >= 26 && _vibrationEffectClass != null)
                {
                    amplitude = Mathf.Clamp(amplitude, 1, 255);
                    using (var effect = _vibrationEffectClass.CallStatic<AndroidJavaObject>("createOneShot", (long)durationMs, amplitude))
                    {
                        if (effect != null)
                        {
                            _vibrator.Call("vibrate", effect);
                            return;
                        }
                    }
                }

                // Fallback for pre-Oreo Android
                _vibrator.Call("vibrate", (long)durationMs);
            }
            catch
            {
                // Fallback to Unity generic vibration
                Handheld.Vibrate();
            }
#elif UNITY_IOS && !UNITY_EDITOR
            // iOS default vibration
            Handheld.Vibrate();
#endif
        }
    }
}
