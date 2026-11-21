using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Starfall.Combat;

namespace Starfall.Audio
{
    /// <summary>
    /// Audio manager providing one-shot SFX playback for weapons, explosions,
    /// hits, tactical formation entries, dive-bomber swoops, and boss sirens.
    /// Features cross-fading background music, dedicated telegraph priority channel,
    /// pitch variation to prevent audio fatigue, and volume controls persisted in PlayerPrefs.
    /// </summary>
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Audio Sources")]
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioSource musicSource;
        [SerializeField] private AudioSource telegraphSource;

        [Header("Music Tracks")]
        [SerializeField] private AudioClip menuMusicClip;
        [SerializeField] private AudioClip combatMusicClip;
        [SerializeField] private AudioClip bossMusicClip;

        [Header("Weapon SFX")]
        [SerializeField] private AudioClip playerLaserClip;
        [SerializeField] private AudioClip[] playerLaserVariations;
        [SerializeField] private AudioClip homingMissileLaunchClip;
        [SerializeField] private AudioClip heavyLaserClip;
        [SerializeField] private AudioClip enemyLaserClip;
        [SerializeField] private AudioClip specialBombClip;

        [Header("Explosion & Impact SFX")]
        [SerializeField] private AudioClip smallExplosionClip;
        [SerializeField] private AudioClip[] smallExplosionVariations;
        [SerializeField] private AudioClip largeExplosionClip;
        [SerializeField] private AudioClip[] largeExplosionVariations;
        [SerializeField] private AudioClip hitClip;
        [SerializeField] private AudioClip[] hitVariations;
        [SerializeField] private AudioClip impactHullClip;

        [Header("Power-Up SFX")]
        [SerializeField] private AudioClip powerUpClip;

        [Header("Formation & Tactical SFX")]
        [SerializeField] private AudioClip diveBomberSwoopClip;
        [SerializeField] private AudioClip formationEntryClip;
        [SerializeField] private AudioClip waveIncomingClip;
        [SerializeField] private AudioClip reinforcementAlertClip;

        [Header("Boss & Threat Warning SFX")]
        [SerializeField] private AudioClip bossWarningSirenClip;
        [SerializeField] private AudioClip bossRoarClip;
        [SerializeField] private AudioClip hazardAlertClip;

        [Header("Settings")]
        [SerializeField] private float masterVolume = 0.85f;
        [SerializeField] private float musicVolume = 0.75f;
        [SerializeField] private float sfxVolume = 0.85f;
        [SerializeField] private float pitchVariation = 0.08f;

        public float MasterVolume => masterVolume;
        public float MusicVolume => musicVolume;
        public float SFXVolume => sfxVolume;
        public bool IsBossEncounterActive => isBossEncounterActive;

        private Coroutine bossSirenRoutine;
        private Coroutine musicFadeRoutine;
        private bool isBossEncounterActive = false;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            EnsureAudioListener();
            SceneManager.sceneLoaded += OnSceneLoaded;
            BossEvents.OnBossSpawned += OnBossSpawnedForMusic;
            BossEvents.OnBossDefeated += OnBossDefeatedForMusic;

            if (sfxSource == null)
            {
                sfxSource = gameObject.AddComponent<AudioSource>();
                sfxSource.playOnAwake = false;
            }

            if (telegraphSource == null)
            {
                telegraphSource = gameObject.AddComponent<AudioSource>();
                telegraphSource.playOnAwake = false;
            }

            if (musicSource == null)
            {
                musicSource = gameObject.AddComponent<AudioSource>();
                musicSource.playOnAwake = false;
                musicSource.loop = true;
            }

            LoadSettings();
            InitializeFallbackClips();

            // Start appropriate music for current active scene
            PlaySceneAppropriateMusic(SceneManager.GetActiveScene().name);
        }

        private void EnsureAudioListener()
        {
            var existing = FindObjectsByType<AudioListener>(FindObjectsInactive.Include);
            foreach (var al in existing)
            {
                if (al.gameObject != gameObject)
                    al.enabled = false;
            }

            if (GetComponent<AudioListener>() == null)
                gameObject.AddComponent<AudioListener>();
        }

        private void OnEnable()
        {
            var listener = GetComponent<AudioListener>();
            if (listener != null) listener.enabled = true;
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            BossEvents.OnBossSpawned -= OnBossSpawnedForMusic;
            BossEvents.OnBossDefeated -= OnBossDefeatedForMusic;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureAudioListener();
            isBossEncounterActive = false;
            PlaySceneAppropriateMusic(scene.name);
        }

        private void PlaySceneAppropriateMusic(string sceneName)
        {
            string lower = (sceneName ?? "").ToLower();
            if (lower.Contains("home") || lower.Contains("select") || lower.Contains("menu") || lower.Contains("hudtest"))
            {
                PlayMenuMusic(1.2f);
            }
            else
            {
                PlayCombatMusic(1.0f);
            }
        }

        private void OnBossSpawnedForMusic(string bossName, int curHp, int maxHp)
        {
            isBossEncounterActive = true;
            PlayBossMusic(0.8f);
        }

        private void OnBossDefeatedForMusic(string bossName)
        {
            isBossEncounterActive = false;
            PlayCombatMusic(1.5f);
        }

        public void LoadSettings()
        {
            masterVolume = PlayerPrefs.GetFloat("Starfall_MasterVolume", 0.85f);
            musicVolume = PlayerPrefs.GetFloat("Starfall_MusicVolume", 0.75f);
            sfxVolume = PlayerPrefs.GetFloat("Starfall_SFXVolume", 0.85f);

            AudioListener.volume = masterVolume;
            if (musicSource != null) musicSource.volume = musicVolume;
        }

        public void SetMasterVolume(float val)
        {
            masterVolume = Mathf.Clamp01(val);
            AudioListener.volume = masterVolume;
            PlayerPrefs.SetFloat("Starfall_MasterVolume", masterVolume);
            PlayerPrefs.Save();
        }

        public void SetMusicVolume(float val)
        {
            musicVolume = Mathf.Clamp01(val);
            if (musicSource != null && musicFadeRoutine == null) 
                musicSource.volume = musicVolume;
            PlayerPrefs.SetFloat("Starfall_MusicVolume", musicVolume);
            PlayerPrefs.Save();
        }

        public void SetSFXVolume(float val)
        {
            sfxVolume = Mathf.Clamp01(val);
            PlayerPrefs.SetFloat("Starfall_SFXVolume", sfxVolume);
            PlayerPrefs.Save();
        }

        // ------------------------------------------------------------------
        // Background Music Controls
        // ------------------------------------------------------------------
        public void PlayMenuMusic(float fadeDuration = 1.0f)
        {
            if (menuMusicClip != null)
            {
                PlayMusic(menuMusicClip, fadeDuration);
            }
        }

        public void PlayCombatMusic(float fadeDuration = 1.0f)
        {
            if (combatMusicClip != null)
            {
                PlayMusic(combatMusicClip, fadeDuration);
            }
            else if (menuMusicClip != null && !musicSource.isPlaying)
            {
                PlayMusic(menuMusicClip, fadeDuration);
            }
        }

        public void PlayBossMusic(float fadeDuration = 0.8f)
        {
            if (bossMusicClip != null)
            {
                PlayMusic(bossMusicClip, fadeDuration);
            }
        }

        public void PlayMusic(AudioClip clip, float fadeDuration = 1.0f)
        {
            if (clip == null || musicSource == null) return;
            if (musicSource.clip == clip && musicSource.isPlaying) return;

            if (musicFadeRoutine != null) StopCoroutine(musicFadeRoutine);
            musicFadeRoutine = StartCoroutine(CrossfadeMusicRoutine(clip, fadeDuration));
        }

        public void StopMusic(float fadeDuration = 0.8f)
        {
            if (musicFadeRoutine != null) StopCoroutine(musicFadeRoutine);
            musicFadeRoutine = StartCoroutine(FadeOutMusicRoutine(fadeDuration));
        }

        private IEnumerator CrossfadeMusicRoutine(AudioClip newClip, float duration)
        {
            float halfDuration = Mathf.Max(0.1f, duration * 0.5f);
            float startVol = musicSource.isPlaying ? musicSource.volume : 0f;

            // Fade out current track
            if (musicSource.isPlaying && startVol > 0.01f)
            {
                float timer = 0f;
                while (timer < halfDuration)
                {
                    timer += Time.unscaledDeltaTime;
                    musicSource.volume = Mathf.Lerp(startVol, 0f, timer / halfDuration);
                    yield return null;
                }
                musicSource.volume = 0f;
                musicSource.Stop();
            }

            // Switch to new track
            musicSource.clip = newClip;
            musicSource.loop = true;
            musicSource.Play();

            // Fade in new track up to user musicVolume
            float targetVol = musicVolume;
            float inTimer = 0f;
            while (inTimer < halfDuration)
            {
                inTimer += Time.unscaledDeltaTime;
                musicSource.volume = Mathf.Lerp(0f, targetVol, inTimer / halfDuration);
                yield return null;
            }
            musicSource.volume = targetVol;
            musicFadeRoutine = null;
        }

        private IEnumerator FadeOutMusicRoutine(float duration)
        {
            if (musicSource != null && musicSource.isPlaying)
            {
                float startVol = musicSource.volume;
                float timer = 0f;
                while (timer < duration)
                {
                    timer += Time.unscaledDeltaTime;
                    musicSource.volume = Mathf.Lerp(startVol, 0f, timer / duration);
                    yield return null;
                }
                musicSource.volume = 0f;
                musicSource.Stop();
            }
            musicFadeRoutine = null;
        }

        // ------------------------------------------------------------------
        // Weapons & Impacts
        // ------------------------------------------------------------------
        private int laserVariationIndex = 0;
        private bool laserSideToggle = false;
        private int hitVariationIndex = 0;
        private float lastHitSoundTime = -1f;
        private const float MinHitSoundInterval = 0.025f;

        private int smallExplosionVariationIndex = 0;
        private int largeExplosionVariationIndex = 0;
        private int lastExplosionFrame = -1;
        private int explosionsThisFrame = 0;

        public void PlayPlayerLaser(float pitchMod = 1.0f)
        {
            AudioClip clip = playerLaserClip;
            if (playerLaserVariations != null && playerLaserVariations.Length > 0)
            {
                laserVariationIndex = (laserVariationIndex + 1) % playerLaserVariations.Length;
                clip = playerLaserVariations[laserVariationIndex] ?? playerLaserClip;
            }

            if (clip == null) return;
            laserSideToggle = !laserSideToggle;
            float wingPitchOffset = laserSideToggle ? 0.025f : -0.025f;
            float pitch = pitchMod * (1.0f + wingPitchOffset + Random.Range(-pitchVariation, pitchVariation));
            PlaySFXWithPitch(clip, 0.78f, pitch);
        }

        public void PlayLaserShoot(float pitchMod = 1.0f)
        {
            PlayPlayerLaser(pitchMod);
        }

        public void PlayHeavyLaser()
        {
            if (heavyLaserClip != null)
            {
                PlaySFXWithPitch(heavyLaserClip, 0.95f, 1.0f + Random.Range(-0.04f, 0.04f));
            }
            else
            {
                PlayPlayerLaser(0.82f);
            }
            HapticFeedback.LightImpact();
        }

        public void PlayHomingMissileLaunch()
        {
            if (homingMissileLaunchClip != null)
            {
                PlaySFXWithPitch(homingMissileLaunchClip, 0.78f, 1.12f + Random.Range(-0.05f, 0.05f));
            }
            else
            {
                PlayTelegraphSFX(diveBomberSwoopClip, 0.6f, 1.45f);
            }
        }

        public void PlayEnemyLaser()
        {
            PlaySFX(enemyLaserClip, 0.65f);
        }

        public void PlaySpecialBomb()
        {
            if (specialBombClip != null)
                PlaySFXWithPitch(specialBombClip, 1.0f, Random.Range(0.96f, 1.04f));
            else
                PlayLargeExplosion();

            HapticFeedback.HeavyImpact();
        }

        public void PlayExplosion(bool isLarge = false)
        {
            if (Time.frameCount != lastExplosionFrame)
            {
                lastExplosionFrame = Time.frameCount;
                explosionsThisFrame = 0;
            }

            // Voice limiter: avoid harsh distortion when a fleet detonates in a single frame
            explosionsThisFrame++;
            if (explosionsThisFrame > 3 && !isLarge) return;

            AudioClip clip = isLarge ? largeExplosionClip : smallExplosionClip;
            var variations = isLarge ? largeExplosionVariations : smallExplosionVariations;
            if (variations != null && variations.Length > 0)
            {
                if (isLarge)
                {
                    largeExplosionVariationIndex = (largeExplosionVariationIndex + 1) % variations.Length;
                    clip = variations[largeExplosionVariationIndex] ?? clip;
                }
                else
                {
                    smallExplosionVariationIndex = (smallExplosionVariationIndex + 1) % variations.Length;
                    clip = variations[smallExplosionVariationIndex] ?? clip;
                }
            }

            if (clip != null)
            {
                float pitch = isLarge ? Random.Range(0.85f, 1.08f) : Random.Range(0.92f, 1.18f);
                float vol = isLarge ? 1.0f : 0.85f;
                PlaySFXWithPitch(clip, vol, pitch);
            }

            if (isLarge)
                HapticFeedback.HeavyImpact();
            else
                HapticFeedback.LightImpact();
        }

        public void PlayLargeExplosion()
        {
            PlayExplosion(true);
        }

        public void PlaySmallExplosion()
        {
            PlayExplosion(false);
        }

        public void PlayEnemyExplosion()
        {
            PlayExplosion(false);
        }

        public void PlayHit(bool heavy = false)
        {
            if (Time.unscaledTime - lastHitSoundTime < MinHitSoundInterval) return;
            lastHitSoundTime = Time.unscaledTime;

            AudioClip clip = hitClip;
            if (hitVariations != null && hitVariations.Length > 0)
            {
                hitVariationIndex = (hitVariationIndex + 1) % hitVariations.Length;
                clip = hitVariations[hitVariationIndex] ?? hitClip;
            }

            float pitch = heavy ? Random.Range(0.75f, 0.85f) : Random.Range(0.92f, 1.14f);
            float vol = heavy ? 0.8f : 0.65f;
            PlaySFXWithPitch(clip, vol, pitch);

            if (heavy)
                HapticFeedback.MediumImpact();
        }

        public void PlayImpactHull()
        {
            if (impactHullClip != null)
                PlaySFX(impactHullClip, 0.85f);
            else
                PlayHit(true);

            HapticFeedback.HeavyImpact();
        }

        public void PlayPowerUp()
        {
            PlaySFX(powerUpClip, 0.85f);
            HapticFeedback.Success();
        }

        // ------------------------------------------------------------------
        // Tactical Formations & Dive Bombers
        // ------------------------------------------------------------------
        public void PlayDiveBomberSwoop(float volumeScale = 0.85f)
        {
            PlayTelegraphSFX(diveBomberSwoopClip, volumeScale, Random.Range(0.95f, 1.15f));
        }

        public void PlayFormationEntry(float volumeScale = 0.75f)
        {
            PlayTelegraphSFX(formationEntryClip, volumeScale, Random.Range(0.9f, 1.1f));
        }

        public void PlayWaveIncoming(float volumeScale = 0.9f)
        {
            PlayTelegraphSFX(waveIncomingClip, volumeScale, 1.0f);
        }

        public void PlayReinforcementAlert(float volumeScale = 0.85f)
        {
            PlayTelegraphSFX(reinforcementAlertClip, volumeScale, 1.05f);
        }

        public void PlayHazardAlert(float volumeScale = 0.9f)
        {
            PlayTelegraphSFX(hazardAlertClip, volumeScale, 1.0f);
        }

        // ------------------------------------------------------------------
        // Boss Warnings & Sirens
        // ------------------------------------------------------------------
        public void PlayBossWarningSiren(int pulseCount = 3)
        {
            if (bossSirenRoutine != null) StopCoroutine(bossSirenRoutine);
            bossSirenRoutine = StartCoroutine(BossSirenSequence(pulseCount));
        }

        private IEnumerator BossSirenSequence(int pulses)
        {
            for (int i = 0; i < pulses; i++)
            {
                PlayTelegraphSFX(bossWarningSirenClip, 1.0f, 1.0f + (i * 0.05f));
                yield return new WaitForSeconds(0.45f);
            }
            bossSirenRoutine = null;
        }

        public void PlayBossRoar(float volumeScale = 1.0f)
        {
            PlayTelegraphSFX(bossRoarClip, volumeScale, 0.85f);
        }

        // ------------------------------------------------------------------
        // Audio Execution Helpers
        // ------------------------------------------------------------------
        public void PlaySFX(AudioClip clip, float volumeScale = 1.0f)
        {
            if (clip == null || sfxSource == null) return;
            sfxSource.pitch = 1.0f + Random.Range(-pitchVariation, pitchVariation);
            sfxSource.PlayOneShot(clip, sfxVolume * volumeScale);
        }

        public void PlaySFXWithPitch(AudioClip clip, float volumeScale = 1.0f, float pitch = 1.0f)
        {
            if (clip == null || sfxSource == null) return;
            sfxSource.pitch = pitch;
            sfxSource.PlayOneShot(clip, sfxVolume * volumeScale);
        }

        public void PlayTelegraphSFX(AudioClip clip, float volumeScale = 1.0f, float pitch = 1.0f)
        {
            var source = telegraphSource != null ? telegraphSource : sfxSource;
            if (clip == null || source == null) return;
            source.pitch = pitch;
            source.PlayOneShot(clip, sfxVolume * volumeScale);
        }

        // ------------------------------------------------------------------
        // Dynamic Clip Assignment & Procedural Fallbacks
        // ------------------------------------------------------------------
        [ContextMenu("Rebind War Audio Clips")]
        public void RebindWarAudioClips()
        {
            InitializeFallbackClips();
#if UNITY_EDITOR
            UnityEditor.EditorUtility.SetDirty(this);
#endif
        }

        private static bool IsLegacyOrMissing(AudioClip clip)
        {
            if (clip == null) return true;
            string n = clip.name;
            return n.StartsWith("laser") || n.Contains("Death") || n.Contains("low") || 
                   n.StartsWith("powerUp") || n.StartsWith("Procedural") || n.Contains("pepSound") || n.Contains("zap");
        }

        private static AudioClip FetchClip(string resourceRelPath, string assetFullPath)
        {
            var clip = Resources.Load<AudioClip>(resourceRelPath);
            if (clip != null) return clip;

            clip = Resources.Load<AudioClip>(System.IO.Path.GetFileNameWithoutExtension(resourceRelPath));
            if (clip != null) return clip;

#if UNITY_EDITOR
            if (!string.IsNullOrEmpty(assetFullPath))
            {
                clip = UnityEditor.AssetDatabase.LoadAssetAtPath<AudioClip>(assetFullPath);
                if (clip != null) return clip;
            }
#endif
            return null;
        }

        private void InitializeFallbackClips()
        {
            // Background Music Tracks
            if (IsLegacyOrMissing(menuMusicClip))
                menuMusicClip = FetchClip("Audio/Music/bgm_menu_drydock", "Assets/Audio/Music/bgm_menu_drydock.mp3") ?? menuMusicClip;

            if (IsLegacyOrMissing(combatMusicClip))
                combatMusicClip = FetchClip("Audio/Music/bgm_combat_mission", "Assets/Audio/Music/bgm_combat_mission.mp3") ?? combatMusicClip;

            if (IsLegacyOrMissing(bossMusicClip))
                bossMusicClip = FetchClip("Audio/Music/bgm_boss_climax", "Assets/Audio/Music/bgm_boss_climax.mp3") ?? bossMusicClip;

            // Weapons
            if (IsLegacyOrMissing(playerLaserClip))
                playerLaserClip = FetchClip("Audio/SFX/sfx_player_cannon", "Assets/Audio/SFX/sfx_player_cannon.mp3") ?? playerLaserClip;

            if (playerLaserVariations == null || playerLaserVariations.Length == 0)
            {
                var list = new System.Collections.Generic.List<AudioClip>();
                string[] laserPaths = new string[]
                {
                    "Assets/Audio/SFX/Laser1 shortened.ogg",
                    "Assets/Audio/SFX/laser2.ogg",
                    "Assets/Audio/SFX/laser3.ogg",
                    "Assets/Audio/SFX/laser4.ogg",
                    "Assets/Audio/SFX/Laser9 shortened.ogg",
                    "Assets/Audio/SFX/sfx_player_cannon.mp3"
                };
                for (int i = 0; i < laserPaths.Length; i++)
                {
                    string p = laserPaths[i];
                    string resName = System.IO.Path.GetFileNameWithoutExtension(p);
                    var c = FetchClip("Audio/SFX/" + resName, p);
                    if (c != null && !list.Contains(c)) list.Add(c);
                }
                if (list.Count > 0) playerLaserVariations = list.ToArray();
            }

            if (homingMissileLaunchClip == null)
            {
                homingMissileLaunchClip = FetchClip("Audio/SFX/phaseJump2", "Assets/Audio/SFX/phaseJump2.ogg")
                                       ?? FetchClip("Audio/SFX/phaseJump1", "Assets/Audio/SFX/phaseJump1.ogg")
                                       ?? FetchClip("Audio/SFX/phaserUp2", "Assets/Audio/SFX/phaserUp2.ogg");
            }

            if (heavyLaserClip == null)
            {
                heavyLaserClip = FetchClip("Audio/SFX/zapThreeToneDown", "Assets/Audio/SFX/zapThreeToneDown.ogg")
                              ?? FetchClip("Audio/SFX/zap2", "Assets/Audio/SFX/zap2.ogg")
                              ?? FetchClip("Audio/SFX/laser9", "Assets/Audio/SFX/laser9.ogg");
            }

            if (IsLegacyOrMissing(enemyLaserClip))
                enemyLaserClip = FetchClip("Audio/SFX/sfx_enemy_blaster", "Assets/Audio/SFX/sfx_enemy_blaster.mp3") ?? enemyLaserClip;

            if (IsLegacyOrMissing(specialBombClip))
                specialBombClip = FetchClip("Audio/SFX/sfx_special_bomb", "Assets/Audio/SFX/sfx_special_bomb.mp3") ?? specialBombClip;

            // Explosions & Hits
            if (IsLegacyOrMissing(smallExplosionClip))
                smallExplosionClip = FetchClip("Audio/SFX/sfx_explosion_small", "Assets/Audio/SFX/sfx_explosion_small.mp3") ?? smallExplosionClip;

            if (smallExplosionVariations == null || smallExplosionVariations.Length == 0)
            {
                var sList = new System.Collections.Generic.List<AudioClip>();
                string[] expPaths = new string[]
                {
                    "Assets/Audio/SFX/sfx_explosion_small.mp3",
                    "Assets/Audio/SFX/Death Effect.ogg",
                    "Assets/Audio/SFX/spaceTrash1.ogg",
                    "Assets/Audio/SFX/spaceTrash2.ogg",
                    "Assets/Audio/SFX/spaceTrash3.ogg",
                    "Assets/Audio/SFX/lowDown.ogg",
                    "Assets/Audio/SFX/lowRandom.ogg"
                };
                for (int i = 0; i < expPaths.Length; i++)
                {
                    string p = expPaths[i];
                    string resName = System.IO.Path.GetFileNameWithoutExtension(p);
                    var c = FetchClip("Audio/SFX/" + resName, p);
                    if (c != null && !sList.Contains(c)) sList.Add(c);
                }
                if (sList.Count > 0) smallExplosionVariations = sList.ToArray();
            }

            if (IsLegacyOrMissing(largeExplosionClip))
                largeExplosionClip = FetchClip("Audio/SFX/sfx_explosion_large", "Assets/Audio/SFX/sfx_explosion_large.mp3") ?? largeExplosionClip;

            if (largeExplosionVariations == null || largeExplosionVariations.Length == 0)
            {
                var lList = new System.Collections.Generic.List<AudioClip>();
                string[] lPaths = new string[]
                {
                    "Assets/Audio/SFX/sfx_explosion_large.mp3",
                    "Assets/Audio/SFX/sfx_special_bomb.mp3",
                    "Assets/Audio/SFX/lowThreeTone.ogg"
                };
                for (int i = 0; i < lPaths.Length; i++)
                {
                    string p = lPaths[i];
                    string resName = System.IO.Path.GetFileNameWithoutExtension(p);
                    var c = FetchClip("Audio/SFX/" + resName, p);
                    if (c != null && !lList.Contains(c)) lList.Add(c);
                }
                if (lList.Count > 0) largeExplosionVariations = lList.ToArray();
            }

            if (IsLegacyOrMissing(hitClip))
                hitClip = FetchClip("Audio/SFX/sfx_impact_hull", "Assets/Audio/SFX/sfx_impact_hull.mp3") ?? hitClip;

            if (hitVariations == null || hitVariations.Length == 0)
            {
                var hList = new System.Collections.Generic.List<AudioClip>();
                string[] hitPaths = new string[]
                {
                    "Assets/Audio/SFX/sfx_impact_hull.mp3",
                    "Assets/Audio/SFX/spaceTrash1.ogg",
                    "Assets/Audio/SFX/spaceTrash2.ogg",
                    "Assets/Audio/SFX/pepSound1.ogg"
                };
                for (int i = 0; i < hitPaths.Length; i++)
                {
                    string p = hitPaths[i];
                    string resName = System.IO.Path.GetFileNameWithoutExtension(p);
                    var c = FetchClip("Audio/SFX/" + resName, p);
                    if (c != null && !hList.Contains(c)) hList.Add(c);
                }
                if (hList.Count > 0) hitVariations = hList.ToArray();
            }

            if (IsLegacyOrMissing(impactHullClip))
                impactHullClip = FetchClip("Audio/SFX/sfx_impact_hull", "Assets/Audio/SFX/sfx_impact_hull.mp3") ?? impactHullClip;

            if (IsLegacyOrMissing(powerUpClip))
                powerUpClip = FetchClip("Audio/SFX/sfx_powerup_pickup", "Assets/Audio/SFX/sfx_powerup_pickup.mp3") ?? powerUpClip;

            // Tactical Formations & Telegraphs
            if (IsLegacyOrMissing(diveBomberSwoopClip))
                diveBomberSwoopClip = FetchClip("Audio/SFX/sfx_swoop_bomber", "Assets/Audio/SFX/sfx_swoop_bomber.mp3") ?? diveBomberSwoopClip;

            if (IsLegacyOrMissing(formationEntryClip))
                formationEntryClip = FetchClip("Audio/SFX/sfx_swoop_bomber", "Assets/Audio/SFX/sfx_swoop_bomber.mp3") ?? diveBomberSwoopClip;

            if (IsLegacyOrMissing(waveIncomingClip))
                waveIncomingClip = FetchClip("Audio/SFX/sfx_alert_tactical", "Assets/Audio/SFX/sfx_alert_tactical.mp3") ?? waveIncomingClip;

            if (IsLegacyOrMissing(reinforcementAlertClip))
                reinforcementAlertClip = FetchClip("Audio/SFX/sfx_alert_tactical", "Assets/Audio/SFX/sfx_alert_tactical.mp3") ?? waveIncomingClip;

            if (IsLegacyOrMissing(hazardAlertClip))
                hazardAlertClip = FetchClip("Audio/SFX/sfx_alert_tactical", "Assets/Audio/SFX/sfx_alert_tactical.mp3") ?? waveIncomingClip;

            // Boss & Siren
            if (IsLegacyOrMissing(bossWarningSirenClip))
                bossWarningSirenClip = FetchClip("Audio/SFX/sfx_siren_boss", "Assets/Audio/SFX/sfx_siren_boss.mp3") ?? bossWarningSirenClip;

            if (IsLegacyOrMissing(bossRoarClip))
                bossRoarClip = FetchClip("Audio/SFX/sfx_siren_boss", "Assets/Audio/SFX/sfx_siren_boss.mp3") ?? bossWarningSirenClip;

            // Final fallback to synthetic tones only if no external audio files found
            if (diveBomberSwoopClip == null)
                diveBomberSwoopClip = GenerateToneClip("Procedural_DiveSwoop", 0.5f, 900f, 250f);

            if (formationEntryClip == null)
                formationEntryClip = GenerateToneClip("Procedural_FormationEntry", 0.4f, 350f, 750f);

            if (bossWarningSirenClip == null)
                bossWarningSirenClip = GenerateToneClip("Procedural_BossSiren", 0.35f, 600f, 480f);

            if (waveIncomingClip == null)
                waveIncomingClip = GenerateToneClip("Procedural_WaveIncoming", 0.35f, 450f, 600f);

            if (reinforcementAlertClip == null)
                reinforcementAlertClip = GenerateToneClip("Procedural_Reinforcement", 0.3f, 380f, 520f);

            if (hazardAlertClip == null)
                hazardAlertClip = GenerateToneClip("Procedural_HazardAlert", 0.35f, 700f, 400f);
        }

        private AudioClip GenerateToneClip(string clipName, float duration, float startFreq, float endFreq)
        {
            int sampleRate = 44100;
            int totalSamples = Mathf.RoundToInt(duration * sampleRate);
            float[] samples = new float[totalSamples];

            for (int i = 0; i < totalSamples; i++)
            {
                float t = (float)i / totalSamples;
                float currentFreq = Mathf.Lerp(startFreq, endFreq, t);
                float envelope = Mathf.Sin(t * Mathf.PI);
                samples[i] = Mathf.Sin(2f * Mathf.PI * currentFreq * (i / (float)sampleRate)) * envelope * 0.45f;
            }

            AudioClip clip = AudioClip.Create(clipName, totalSamples, 1, sampleRate, false);
            clip.SetData(samples, 0);
            return clip;
        }
    }
}
