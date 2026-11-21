using UnityEngine;

namespace Starfall.UI
{
    /// <summary>
    /// Helper component referenced in level scenes for testing and driving the Rive special ability button.
    /// </summary>
    [DisallowMultipleComponent]
    public class RiveSpecialButtonTester : MonoBehaviour
    {
        [Tooltip("Reference to the active RiveSpecialButton controller.")]
        [SerializeField] private RiveSpecialButton buttonController;

        public RiveSpecialButton ButtonController => buttonController;

        private void Awake()
        {
            if (buttonController == null)
            {
                buttonController = GetComponent<RiveSpecialButton>();
            }
        }
    }
}
