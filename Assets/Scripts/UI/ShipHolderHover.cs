using UnityEngine;

namespace Starfall.UI
{
    /// <summary>
    /// Idle hover for the Home Hub dock-pad ship ("ShipHolder").
    /// Bobs the holder up and down around its start position.
    /// </summary>
    public class ShipHolderHover : MonoBehaviour
    {
        public float amplitude = 12f, speed = 1.5f;
        Vector3 startPos;

        void Start() => startPos = transform.localPosition;
        void Update() => transform.localPosition =
            startPos + Vector3.up * Mathf.Sin(Time.time * speed) * amplitude;

        /// <summary>
        /// Re-anchors the hover to the holder's current local position.
        /// Needed because HomeHubUI re-positions the holder when the screen size changes
        /// (the amplitude is given in world units, so it is scaled by <see cref="Rescale"/>).
        /// </summary>
        public void Rebase() => startPos = transform.localPosition;

        /// <summary>Sets the hover amplitude in world units (keeps the bob proportional to screen size).</summary>
        public void Rescale(float worldAmplitude) => amplitude = worldAmplitude;
    }
}
