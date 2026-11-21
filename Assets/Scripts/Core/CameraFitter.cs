using UnityEngine;

namespace Starfall.Core
{
    /// <summary>
    /// Keeps the orthographic camera wide enough to fit a 5-column enemy formation on
    /// tall portrait phones, while leaving landscape (web / Windows) at the authored size.
    /// Re-evaluates when the window or device orientation changes.
    /// </summary>
    [DefaultExecutionOrder(-900)]
    [RequireComponent(typeof(Camera))]
    public class CameraFitter : MonoBehaviour
    {
        [SerializeField] private float baseOrthographicSize = 5f;
        [Tooltip("Minimum visible half-width in world units. 3.4 fits five enemy columns.")]
        [SerializeField] private float minHalfWidth = 3.4f;

        private Camera cam;
        private int lastWidth;
        private int lastHeight;

        public static void Ensure()
        {
            var main = Camera.main;
            if (main != null && main.GetComponent<CameraFitter>() == null)
                main.gameObject.AddComponent<CameraFitter>();
        }

        private void Awake()
        {
            cam = GetComponent<Camera>();
            if (cam.orthographic && cam.orthographicSize > 0f)
                baseOrthographicSize = cam.orthographicSize;
            Apply();
        }

        private void Update()
        {
            if (Screen.width != lastWidth || Screen.height != lastHeight)
                Apply();
        }

        private void Apply()
        {
            lastWidth = Screen.width;
            lastHeight = Screen.height;
            if (cam == null || !cam.orthographic || cam.aspect <= 0f) return;

            float needed = minHalfWidth / cam.aspect;
            cam.orthographicSize = Mathf.Max(baseOrthographicSize, needed);

            var player = Starfall.Player.PlayerController.Instance;
            if (player != null) player.UpdateScreenBounds();
        }
    }
}
