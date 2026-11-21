using UnityEngine;

namespace Starfall.VFX
{
    public enum ParallaxDirection
    {
        LeftToRight, // Moving from left to right (along +X)
        RightToLeft, // Moving from right to left (along -X)
        TopToBottom, // Moving from top to bottom (along -Y)
        BottomToTop  // Moving from bottom to top (along +Y)
    }

    /// <summary>
    /// Scrolls sprite tiles continuously, looping seamlessly using a dual-tile technique.
    /// Supports left-to-right, right-to-left, and vertical parallax.
    /// Zero-allocation: only transforms are moved at runtime.
    /// </summary>
    public class ParallaxLayer : MonoBehaviour
    {
        [Header("Scroll Settings")]
        [SerializeField] private float scrollSpeed = 2f;
        [SerializeField] private ParallaxDirection direction = ParallaxDirection.TopToBottom;

        [Header("Tile References")]
        [SerializeField] private Transform tileA;
        [SerializeField] private Transform tileB;
        [SerializeField] private float tileWidth = 20f;
        [SerializeField] private float tileHeight = 11.25f;

        public ParallaxDirection Direction
        {
            get => direction;
            set => direction = value;
        }

        public float ScrollSpeed
        {
            get => scrollSpeed;
            set => scrollSpeed = value;
        }

        public float TileWidth
        {
            get => tileWidth;
            set => tileWidth = value;
        }

        public float TileHeight
        {
            get => tileHeight;
            set => tileHeight = value;
        }

        private void Awake()
        {
            if ((tileA == null || tileB == null) && transform.childCount >= 2)
            {
                tileA = transform.GetChild(0);
                tileB = transform.GetChild(1);
            }

            ResetTilePositions();
        }

        public void ResetTilePositions()
        {
            if (tileA == null || tileB == null) return;

            switch (direction)
            {
                case ParallaxDirection.LeftToRight:
                    tileA.localPosition = Vector3.zero;
                    tileB.localPosition = new Vector3(-tileWidth, 0f, 0f);
                    break;
                case ParallaxDirection.RightToLeft:
                    tileA.localPosition = Vector3.zero;
                    tileB.localPosition = new Vector3(tileWidth, 0f, 0f);
                    break;
                case ParallaxDirection.TopToBottom:
                    tileA.localPosition = Vector3.zero;
                    tileB.localPosition = new Vector3(0f, tileHeight, 0f);
                    break;
                case ParallaxDirection.BottomToTop:
                    tileA.localPosition = Vector3.zero;
                    tileB.localPosition = new Vector3(0f, -tileHeight, 0f);
                    break;
            }
        }

        /// <summary>Initialize from ParallaxController after tiles are set up.</summary>
        public void Initialize(Transform a, Transform b, float width, float height, float speed, ParallaxDirection dir = ParallaxDirection.TopToBottom)
        {
            tileA = a;
            tileB = b;
            tileWidth = width;
            tileHeight = height;
            scrollSpeed = speed;
            direction = dir;
            ResetTilePositions();
        }

        /// <summary>Legacy overload with 4 arguments for backwards compatibility.</summary>
        public void Initialize(Transform a, Transform b, float height, float speed)
        {
            Initialize(a, b, 20f, height, speed, ParallaxDirection.TopToBottom);
        }

        private void Update()
        {
            using var __perf = Starfall.Core.PerfMarkers.BackgroundUpdate.Auto();
            if (!Application.isPlaying) return;
            if (tileA == null || tileB == null) return;

            float delta = scrollSpeed * Time.deltaTime;

            switch (direction)
            {
                case ParallaxDirection.LeftToRight:
                    tileA.localPosition += Vector3.right * delta;
                    tileB.localPosition += Vector3.right * delta;

                    if (tileA.localPosition.x > tileWidth)
                    {
                        tileA.localPosition = tileB.localPosition - Vector3.right * tileWidth;
                    }
                    if (tileB.localPosition.x > tileWidth)
                    {
                        tileB.localPosition = tileA.localPosition - Vector3.right * tileWidth;
                    }
                    break;

                case ParallaxDirection.RightToLeft:
                    tileA.localPosition += Vector3.left * delta;
                    tileB.localPosition += Vector3.left * delta;

                    if (tileA.localPosition.x < -tileWidth)
                    {
                        tileA.localPosition = tileB.localPosition + Vector3.right * tileWidth;
                    }
                    if (tileB.localPosition.x < -tileWidth)
                    {
                        tileB.localPosition = tileA.localPosition + Vector3.right * tileWidth;
                    }
                    break;

                case ParallaxDirection.TopToBottom:
                    tileA.localPosition += Vector3.down * delta;
                    tileB.localPosition += Vector3.down * delta;

                    if (tileA.localPosition.y < -tileHeight)
                    {
                        tileA.localPosition = tileB.localPosition + Vector3.up * tileHeight;
                    }
                    if (tileB.localPosition.y < -tileHeight)
                    {
                        tileB.localPosition = tileA.localPosition + Vector3.up * tileHeight;
                    }
                    break;

                case ParallaxDirection.BottomToTop:
                    tileA.localPosition += Vector3.up * delta;
                    tileB.localPosition += Vector3.up * delta;

                    if (tileA.localPosition.y > tileHeight)
                    {
                        tileA.localPosition = tileB.localPosition - Vector3.up * tileHeight;
                    }
                    if (tileB.localPosition.y > tileHeight)
                    {
                        tileB.localPosition = tileA.localPosition - Vector3.up * tileHeight;
                    }
                    break;
            }
        }
    }
}
