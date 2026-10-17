using System.Collections.Generic;
using UnityEngine;

namespace PassthroughCameraSamples.MultiObjectDetection
{
    [System.Serializable]
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

    public class SentisToTvOverlayBridge : MonoBehaviour
    {
        [SerializeField] private TvOverlayController m_tvOverlayController;
        [SerializeField] private bool m_resetOverlayOnEnable = false;

        private bool m_loggedOnce;

        private void OnEnable()
        {
            if (m_resetOverlayOnEnable && m_tvOverlayController != null)
            {
                m_tvOverlayController.ResetOverlay();
            }
        }

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

        public void ResetLockedOverlay()
        {
            if (m_tvOverlayController != null)
                m_tvOverlayController.ResetOverlay();
        }

        public void UnlockOverlayWithoutDestroy()
        {
            if (m_tvOverlayController != null)
                m_tvOverlayController.UnlockWithoutDestroyingOverlay();
        }

        public void ToggleDirections()
        {
            if (m_tvOverlayController != null)
                m_tvOverlayController.ToggleDirectionsFromCode();
        }
    }
}