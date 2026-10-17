using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace PassthroughCameraSamples.MultiObjectDetection
{
    public class TvOverlayController : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField] private SentisInferenceUiManager m_uiInference;
        [SerializeField] private string m_targetClassName = "tvmonitor";
        [SerializeField, Min(1)] private int m_framesToLock = 1;
        [SerializeField, Min(0.01f)] private float m_maxBoxAgeSeconds = 0.25f;
        [SerializeField] private bool m_lockAfterDetection = true;

        [Header("Blur Overlay")]
        [SerializeField] private GameObject m_blurPrefab;
        [SerializeField] private Transform m_spawnParent;
        [SerializeField] private float m_widthMultiplier = 1.0f;
        [SerializeField] private float m_heightMultiplier = 1.0f;
        [SerializeField] private float m_offsetTowardCamera = 0.01f;
        [SerializeField] private Vector3 m_rotationOffsetEuler = Vector3.zero;
        [SerializeField] private Vector3 m_worldScaleMultiplier = Vector3.one;

        [Header("Dummy Directions")]
        [SerializeField] private GameObject m_directionsPrefab;
        [SerializeField] private Vector3 m_directionsLocalOffset = new Vector3(0f, 0.15f, 0f);
        [SerializeField] private bool m_enableDirectionsToggle = true;
        [SerializeField] private XRNode m_controllerNode = XRNode.RightHand;

        private readonly List<InputDevice> m_devices = new List<InputDevice>();

        private GameObject m_overlayInstance;
        private RectTransform m_overlayRect;
        private GameObject m_directionsInstance;
        private Vector3 m_originalLocalScale = Vector3.one;

        private int m_validFrames;
        private bool m_locked;
        private bool m_lastPrimaryPressed;

        private void Start()
        {
            if (m_uiInference == null)
                Debug.LogError("TvOverlayController: SentisInferenceUiManager reference is missing.");

            if (m_blurPrefab == null)
                Debug.LogError("TvOverlayController: Blur Prefab is missing.");
        }

        private void OnEnable()
        {
            RefreshDevices();
        }

        private void Update()
        {
            if (m_uiInference != null && m_blurPrefab != null)
            {
                TrackTvAndUpdateOverlay();
            }

            if (m_enableDirectionsToggle)
            {
                HandleDirectionsToggle();
            }
        }

        private void TrackTvAndUpdateOverlay()
        {
            SentisInferenceUiManager.BoundingBoxData bestTvBox = FindBestTvBox();

            if (bestTvBox == null || bestTvBox.BoxRectTransform == null)
            {
                if (!m_locked)
                    m_validFrames = 0;

                return;
            }

            // During reacquisition, retain the last stable overlay until the new
            // TV pose has passed the UI manager's stability checks.
            if (m_locked && m_lockAfterDetection && !bestTvBox.isLocked)
                return;

            EnsureOverlayExists();
            ApplyPoseAndSize(bestTvBox.BoxRectTransform);

            m_validFrames++;

            bool requiresStableTvPose = string.Equals(
                NormalizeClassName(m_targetClassName),
                "tvmonitor",
                StringComparison.Ordinal);

            // Only tvmonitor uses the coherent-sample gate. Any other configured
            // target keeps the controller's original frame-based lock behavior.
            if (!m_locked &&
                m_lockAfterDetection &&
                (!requiresStableTvPose || bestTvBox.isLocked) &&
                m_validFrames >= m_framesToLock)
            {
                m_locked = true;
                Debug.Log("TvOverlayController: locked on a stable tvmonitor pose.");
            }
        }

        private SentisInferenceUiManager.BoundingBoxData FindBestTvBox()
        {
            if (m_uiInference == null || m_uiInference.m_boxDrawn == null || m_uiInference.m_boxDrawn.Count == 0)
                return null;

            string target = NormalizeClassName(m_targetClassName);
            SentisInferenceUiManager.BoundingBoxData best = null;
            float bestArea = 0f;

            foreach (SentisInferenceUiManager.BoundingBoxData box in m_uiInference.m_boxDrawn)
            {
                if (box == null || box.BoxRectTransform == null)
                    continue;

                if (!box.BoxRectTransform.gameObject.activeInHierarchy)
                    continue;

                if (!string.Equals(NormalizeClassName(box.ClassName), target, StringComparison.Ordinal))
                    continue;

                if (Time.time - box.lastUpdateTime > m_maxBoxAgeSeconds)
                    continue;

                Vector2 size = box.BoxRectTransform.sizeDelta;
                float area = Mathf.Abs(size.x * size.y);

                if (area > bestArea)
                {
                    bestArea = area;
                    best = box;
                }
            }

            return best;
        }

        private static string NormalizeClassName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return value.Trim().Replace(" ", "_").ToLowerInvariant();
        }

        private void EnsureOverlayExists()
        {
            if (m_overlayInstance != null)
                return;

            Transform parent = m_spawnParent != null
                ? m_spawnParent
                : (m_uiInference != null ? m_uiInference.ContentParent : null);

            m_overlayInstance = parent != null
                ? Instantiate(m_blurPrefab, parent)
                : Instantiate(m_blurPrefab);

            m_overlayRect = m_overlayInstance.GetComponent<RectTransform>();
            m_originalLocalScale = m_overlayInstance.transform.localScale;

            if (m_directionsPrefab != null)
            {
                m_directionsInstance = Instantiate(m_directionsPrefab, m_overlayInstance.transform);
                m_directionsInstance.transform.localPosition = m_directionsLocalOffset;
                m_directionsInstance.transform.localRotation = Quaternion.identity;
                m_directionsInstance.transform.localScale = Vector3.one;
                m_directionsInstance.SetActive(false);
            }
        }

        private void ApplyPoseAndSize(RectTransform sourceBox)
        {
            if (sourceBox == null || m_overlayInstance == null)
                return;

            Transform overlayTransform = m_overlayInstance.transform;
            Quaternion rotationOffset = Quaternion.Euler(m_rotationOffsetEuler);
            bool sameParent = overlayTransform.parent == sourceBox.parent;

            if (m_overlayRect != null && sameParent)
            {
                Vector3 localOffset = sourceBox.localRotation * Vector3.forward * m_offsetTowardCamera;

                m_overlayRect.anchoredPosition3D = sourceBox.anchoredPosition3D - localOffset;
                m_overlayRect.localRotation = sourceBox.localRotation * rotationOffset;
                m_overlayRect.localScale = m_originalLocalScale;

                m_overlayRect.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Horizontal,
                    sourceBox.sizeDelta.x * m_widthMultiplier
                );

                m_overlayRect.SetSizeWithCurrentAnchors(
                    RectTransform.Axis.Vertical,
                    sourceBox.sizeDelta.y * m_heightMultiplier
                );
            }
            else
            {
                Vector3 worldPosition = sourceBox.position - sourceBox.forward * m_offsetTowardCamera;
                Quaternion worldRotation = sourceBox.rotation * rotationOffset;

                overlayTransform.SetPositionAndRotation(worldPosition, worldRotation);

                if (m_overlayRect != null)
                {
                    m_overlayRect.localScale = m_originalLocalScale;

                    m_overlayRect.SetSizeWithCurrentAnchors(
                        RectTransform.Axis.Horizontal,
                        sourceBox.sizeDelta.x * m_widthMultiplier
                    );

                    m_overlayRect.SetSizeWithCurrentAnchors(
                        RectTransform.Axis.Vertical,
                        sourceBox.sizeDelta.y * m_heightMultiplier
                    );
                }
                else
                {
                    overlayTransform.localScale = new Vector3(
                        m_originalLocalScale.x * sourceBox.sizeDelta.x * m_widthMultiplier * m_worldScaleMultiplier.x,
                        m_originalLocalScale.y * sourceBox.sizeDelta.y * m_heightMultiplier * m_worldScaleMultiplier.y,
                        m_originalLocalScale.z * m_worldScaleMultiplier.z
                    );
                }
            }
        }

        private void HandleDirectionsToggle()
        {
            if (m_directionsInstance == null || m_overlayInstance == null)
                return;

            if (m_devices.Count == 0 || !HasValidDevice())
                RefreshDevices();

            bool primaryPressed = false;

            for (int i = 0; i < m_devices.Count; i++)
            {
                InputDevice device = m_devices[i];
                if (!device.isValid)
                    continue;

                if (device.TryGetFeatureValue(CommonUsages.primaryButton, out bool value) && value)
                {
                    primaryPressed = true;
                    break;
                }
            }

            if (primaryPressed && !m_lastPrimaryPressed)
            {
                m_directionsInstance.SetActive(!m_directionsInstance.activeSelf);
            }

            m_lastPrimaryPressed = primaryPressed;
        }

        private void RefreshDevices()
        {
            m_devices.Clear();
            InputDevices.GetDevicesAtXRNode(m_controllerNode, m_devices);
        }

        private bool HasValidDevice()
        {
            for (int i = 0; i < m_devices.Count; i++)
            {
                if (m_devices[i].isValid)
                    return true;
            }

            return false;
        }

        public void ResetOverlay()
        {
            m_locked = false;
            m_validFrames = 0;

            if (m_overlayInstance != null)
                Destroy(m_overlayInstance);

            m_overlayInstance = null;
            m_overlayRect = null;
            m_directionsInstance = null;
        }

        public void UnlockWithoutDestroyingOverlay()
        {
            m_locked = false;
            m_validFrames = 0;
        }

        public void ToggleDirectionsFromCode()
        {
            if (m_directionsInstance != null)
                m_directionsInstance.SetActive(!m_directionsInstance.activeSelf);
        }
    }
}
