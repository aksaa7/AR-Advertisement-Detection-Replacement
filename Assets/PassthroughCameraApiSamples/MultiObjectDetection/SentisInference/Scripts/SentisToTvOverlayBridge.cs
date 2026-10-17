using System.Collections.Generic;
using UnityEngine;

namespace PassthroughCameraSamples.MultiObjectDetection
{
    [System.Serializable]
    /// <summary>
    /// One YOLO detection result (class, confidence and box).
    /// Kept for compatibility with <see cref="SentisToTvOverlayBridge.PushModelResults"/>.
    /// </summary>
    public struct YoloDetection
    {
        public int classId;
        public float score;
        public float x;
        public float y;
        public float width;
        public float height;
        public bool isNormalized;
    }

    /// <summary>
    /// Connects UI buttons / other scripts with <see cref="TvOverlayController"/>
    /// (reset, unlock, toggle arrow). The overlay itself reads detections directly
    /// from SentisInferenceUiManager, so PushModelResults is only a legacy entry point.
    /// </summary>
    public class SentisToTvOverlayBridge : MonoBehaviour
    {
        [Tooltip("Overlay controller that the methods below act on.")]
        [SerializeField] private TvOverlayController m_tvOverlayController;
        [Tooltip("If on, any existing overlay is removed when this component is enabled.")]
        [SerializeField] private bool m_resetOverlayOnEnable = false;

        // Ensures the compatibility message is logged only once.
        private bool m_loggedOnce;

        private void OnEnable()
        {
            if (m_resetOverlayOnEnable && m_tvOverlayController != null)
            {
                m_tvOverlayController.ResetOverlay();
            }
        }

        /// <summary>
        /// Legacy entry point for raw model results. Not needed anymore; only logs a notice once.
        /// </summary>
        public void PushModelResults(List<YoloDetection> results)
        {
            if (m_loggedOnce)
                return;

            Debug.Log(
                "SentisToTvOverlayBridge: compatibility shim active. " +
                "TvOverlayController now reads world-space boxes directly from SentisInferenceUiManager."
            );

            m_loggedOnce = true;
        }

        /// <summary>
        /// Removes the current overlay so the next detected screen gets a new one.
        /// </summary>
        public void ResetLockedOverlay()
        {
            if (m_tvOverlayController != null)
                m_tvOverlayController.ResetOverlay();
        }

        /// <summary>
        /// Unlocks the overlay but keeps it visible until a new stable pose is found.
        /// </summary>
        public void UnlockOverlayWithoutDestroy()
        {
            if (m_tvOverlayController != null)
                m_tvOverlayController.UnlockWithoutDestroyingOverlay();
        }

        /// <summary>
        /// Shows/hides the navigation arrow on the overlay.
        /// </summary>
        public void ToggleDirections()
        {
            if (m_tvOverlayController != null)
                m_tvOverlayController.ToggleDirectionsFromCode();
        }
    }
}