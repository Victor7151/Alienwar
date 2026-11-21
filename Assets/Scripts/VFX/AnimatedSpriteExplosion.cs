using System.Collections;
using UnityEngine;
using Starfall.Core;

namespace Starfall.VFX
{
    /// <summary>
    /// Plays sequential sprite explosion frames with Additive blend material.
    /// Automatically returns to ObjectPool on completion.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class AnimatedSpriteExplosion : MonoBehaviour, IPooledObject
    {
        [SerializeField] private Sprite[] frames;
        [SerializeField] private float frameRate = 30f;
        [SerializeField] private Vector3 defaultScale = Vector3.one;

        private SpriteRenderer sr;
        private Coroutine playRoutine;

        private void Awake()
        {
            sr = GetComponent<SpriteRenderer>();
        }

        public void SetFrames(Sprite[] newFrames, float fps = 30f, float scale = 1f)
        {
            frames = newFrames;
            frameRate = fps;

            // Auto-guard against 1080p full-screen frames (19.2 units wide)
            if (newFrames != null && newFrames.Length > 0 && newFrames[0] != null)
            {
                float nativeWidth = newFrames[0].rect.width / newFrames[0].pixelsPerUnit;
                if (nativeWidth > 2.0f)
                {
                    // Scale down to natural world size (~1.35m * scale)
                    float safeScale = (1.35f * scale) / nativeWidth;
                    defaultScale = Vector3.one * safeScale;
                    transform.localScale = defaultScale;
                    return;
                }
            }

            defaultScale = Vector3.one * scale;
            transform.localScale = defaultScale;
        }

        public void OnSpawn()
        {
            transform.localScale = defaultScale;
            if (playRoutine != null) StopCoroutine(playRoutine);
            playRoutine = StartCoroutine(PlayAnimationRoutine());
        }

        public void OnDespawn()
        {
            if (playRoutine != null)
            {
                StopCoroutine(playRoutine);
                playRoutine = null;
            }
            if (sr != null) sr.sprite = null;
        }

        private IEnumerator PlayAnimationRoutine()
        {
            if (frames == null || frames.Length == 0)
            {
                DespawnSelf();
                yield break;
            }

            float delay = 1f / Mathf.Max(1f, frameRate);

            for (int i = 0; i < frames.Length; i++)
            {
                if (sr != null) sr.sprite = frames[i];
                yield return new WaitForSeconds(delay);
            }

            DespawnSelf();
        }

        private void DespawnSelf()
        {
            if (ObjectPool.Instance != null)
            {
                ObjectPool.Instance.Despawn(gameObject);
            }
            else
            {
                Destroy(gameObject);
            }
        }
    }
}
