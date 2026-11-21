using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Starfall.Audio;
using System.IO;

namespace Starfall.EditorTools
{
    public static class AudioSetupUtility
    {
        [MenuItem("Tools/STARFALL/Wire All Real War Audio Clips")]
        public static void WireAllAudioClips()
        {
            var menuMusic = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/bgm_menu_drydock.mp3");
            var combatMusic = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/bgm_combat_mission.mp3");
            var bossMusic = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/Music/bgm_boss_climax.mp3");

            var playerLaser = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/sfx_player_cannon.mp3");
            var enemyLaser = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/sfx_enemy_blaster.mp3");
            var specialBomb = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/sfx_special_bomb.mp3");

            var smallExplosion = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/sfx_explosion_small.mp3");
            var largeExplosion = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/sfx_explosion_large.mp3");
            var impactHull = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/sfx_impact_hull.mp3");

            var powerUp = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/sfx_powerup_pickup.mp3");
            var swoopBomber = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/sfx_swoop_bomber.mp3");
            var alertTactical = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/sfx_alert_tactical.mp3");
            var sirenBoss = AssetDatabase.LoadAssetAtPath<AudioClip>("Assets/Audio/SFX/sfx_siren_boss.mp3");

            // Wire in active scene
            var activeManagers = Object.FindObjectsByType<AudioManager>(FindObjectsInactive.Include);
            foreach (var mgr in activeManagers)
            {
                ApplyToManager(mgr, menuMusic, combatMusic, bossMusic, playerLaser, enemyLaser, specialBomb,
                    smallExplosion, largeExplosion, impactHull, powerUp, swoopBomber, alertTactical, sirenBoss);
                EditorUtility.SetDirty(mgr);
            }

            Debug.Log($"[AudioSetupUtility] Successfully wired real war audio clips to {activeManagers.Length} AudioManager instance(s) in active scene!");
        }

        private static void ApplyToManager(AudioManager mgr, AudioClip menuMusic, AudioClip combatMusic, AudioClip bossMusic,
            AudioClip playerLaser, AudioClip enemyLaser, AudioClip specialBomb, AudioClip smallExplosion, AudioClip largeExplosion,
            AudioClip impactHull, AudioClip powerUp, AudioClip swoopBomber, AudioClip alertTactical, AudioClip sirenBoss)
        {
            var so = new SerializedObject(mgr);
            so.Update();

            SetProp(so, "menuMusicClip", menuMusic);
            SetProp(so, "combatMusicClip", combatMusic);
            SetProp(so, "bossMusicClip", bossMusic);

            SetProp(so, "playerLaserClip", playerLaser);
            SetProp(so, "enemyLaserClip", enemyLaser);
            SetProp(so, "specialBombClip", specialBomb);

            SetProp(so, "smallExplosionClip", smallExplosion);
            SetProp(so, "largeExplosionClip", largeExplosion);
            SetProp(so, "hitClip", impactHull);
            SetProp(so, "impactHullClip", impactHull);

            SetProp(so, "powerUpClip", powerUp);
            SetProp(so, "diveBomberSwoopClip", swoopBomber);
            SetProp(so, "formationEntryClip", swoopBomber);

            SetProp(so, "waveIncomingClip", alertTactical);
            SetProp(so, "reinforcementAlertClip", alertTactical);
            SetProp(so, "hazardAlertClip", alertTactical);

            SetProp(so, "bossWarningSirenClip", sirenBoss);
            SetProp(so, "bossRoarClip", sirenBoss);

            so.ApplyModifiedProperties();
        }

        private static void SetProp(SerializedObject so, string propName, AudioClip clip)
        {
            var prop = so.FindProperty(propName);
            if (prop != null && clip != null)
            {
                prop.objectReferenceValue = clip;
            }
        }
    }
}
