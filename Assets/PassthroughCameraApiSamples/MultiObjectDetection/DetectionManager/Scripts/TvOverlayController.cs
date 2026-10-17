using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace PassthroughCameraSamples.MultiObjectDetection
{
    /// <summary>
    /// Finds the detected advertisement screen (YOLO class "tvmonitor") in the boxes drawn by
    /// <see cref="SentisInferenceUiManager"/> and places a blur overlay exactly on top of it.
    /// Once the screen pose is stable, the overlay is locked so it does not jitter.
    /// Optionally shows a navigation arrow on the overlay that can be toggled with the A button.
    /// </summary>
    public class TvOverlayController : MonoBehaviour
    {
        [Header("Source")]
        [Tooltip("Inference UI manager that provides the detected bounding boxes.")]
        [SerializeField] private SentisInferenceUiManager m_uiInference;
        [Tooltip("YOLO class name to track. Advertisement screens are detected as 'tvmonitor'.")]
        [SerializeField] private string m_targetClassName = "tvmonitor";
        [Tooltip("Number of valid frames needed before the overlay is locked in place.")]
        [SerializeField, Min(1)] private int m_framesToLock = 1;
        [Tooltip("Boxes not updated for longer than this (seconds) are ignored as stale.")]
        [SerializeField, Min(0.01f)] private float m_maxBoxAgeSeconds = 0.25f;
        [Tooltip("If enabled, the overlay stays fixed once a stable screen pose was found.")]
        [SerializeField] private bool m_lockAfterDetection = true;

        [Header("Blur Overlay")]
        [Tooltip("Prefab placed over the detected advertisement (blur panel).")]
        [SerializeField] private GameObject m_blurPrefab;
        [Tooltip("Optional parent for the overlay. Defaults to the inference UI's content parent.")]
        [SerializeField] private Transform m_spawnParent;
        [Tooltip("Overlay width relative to the detected box (1 = same width).")]
        [SerializeField] private float m_widthMultiplier = 1.0f;
        [Tooltip("Overlay height relative to the detected box (1 = same height).")]
        [SerializeField] private float m_heightMultiplier = 1.0f;
        [Tooltip("Moves the overlay slightly toward the viewer so it renders in front of the box.")]
        [SerializeField] private float m_offsetTowardCamera = 0.01f;
        [Tooltip("Extra rotation applied to the overlay, in degrees.")]
        [SerializeField] private Vector3 m_rotationOffsetEuler = Vector3.zero;
        [Tooltip("Scale factor, only used when the overlay prefab has no RectTransform.")]
        [SerializeField] private Vector3 m_worldScaleMultiplier = Vector3.one;

        [Header("Dummy Directions")]
        [Tooltip("Optional navigation arrow shown on top of the overlay.")]
        [SerializeField] private GameObject m_directionsPrefab;
        [Tooltip("Position of the arrow relative to the overlay.")]
        [SerializeField] private Vector3 m_directionsLocalOffset = new Vector3(0f, 0.15f, 0f);
        [Tooltip("Allow showing/hiding the arrow with the controller's primary button (A).")]
        [SerializeField] private bool m_enableDirectionsToggle = true;
        [Tooltip("Controller whose primary button toggles the arrow.")]
        [SerializeField] private XRNode m_controllerNode = XRNode.RightHand;

        // Cached XR input devices for the configured controller.
        private readonly List<InputDevice> m_devices = new List<InputDevice>();

        // Runtime instances created from the prefabs above.
        private GameObject m_overlayInstance;
        private RectTransform m_overlayRect;
        private GameObject m_directionsInstance;
        private Vector3 m_originalLocalScale = Vector3.one;

        // Lock state: frames with a valid box, whether the overlay is locked,
        // and last button state (to react only to the press, not while held).
        private int m_validFrames;
        private bool m_locked;
        private bool m_lastPrimaryPressed;

        // Warn early if required references are missing in the Inspector.
        private void Start()
        {
            if (m_uiInference == null)
                Debug.LogError("TvOverlayController: SentisInferenceUiManager reference is missing.");

            if (m_blurPrefab == null)
                Debug.LogError("TvOverlayController: Blur Prefab is missing.");
        }

        // Collect controller devices when the component becomes active.
        private void OnEnable()
        {
            RefreshDevices();
        }

        // Every frame: follow the detected screen and handle the arrow toggle.
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

        /// <summary>
        /// Moves the overlay onto the best detected screen and locks it once the pose is stable.
        /// While locked, the overlay only moves again when the detector reports a new stable pose.
        /// </summary>
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

        /// <summary>
        /// Returns the largest active, recently updated box of the target class, or null if none.
        /// </summary>
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

                // Prefer the largest box - usually the closest / most relevant screen.
                if (area > bestArea)
                {
                    bestArea = area;
                    best = box;
                }
            }

            return best;
        }

        /// <summary>
        /// Makes class names comparable: trimmed, lower case, spaces replaced by underscores.
        /// </summary>
        private static string NormalizeClassName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return string.Empty;

            return value.Trim().Replace(" ", "_").ToLowerInvariant();
        }

        /// <summary>
        /// Creates the blur overlay (and the optional arrow) the first time a screen is found.
        /// </summary>
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

        /// <summary>
        /// Copies position, rotation and size of the detected box onto the overlay.
        /// Works in local UI space when both share a parent, otherwise via world space.
        /// </summary>
        private void ApplyPoseAndSize(RectTransform sourceBox)
        {
            if (sourceBox == null || m_overlayInstance == null)
                return;

            Transform overlayTransform = m_overlayInstance.transform;
            Quaternion rotationOffset = Quaternion.Euler(m_rotationOffsetEuler);
            bool sameParent = overlayTransform.parent == sourceBox.parent;

            // Same parent: copy the local layout directly.
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
                // Different parent: place the overlay using world coordinates.
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

        /// <summary>
        /// Shows/hides the navigation arrow when the controller's primary button is pressed.
        /// </summary>
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

            // Toggle only on the press itself, not on every frame while the button is held.
            if (primaryPressed && !m_lastPrimaryPressed)
            {
                m_directionsInstance.SetActive(!m_directionsInstance.activeSelf);
            }

            m_lastPrimaryPressed = primaryPressed;
        }

        /// <summary>
        /// Re-reads the input devices of the configured controller.
        /// </summary>
        private void RefreshDevices()
        {
            m_devices.Clear();
            InputDevices.GetDevicesAtXRNode(m_controllerNode, m_devices);
        }

        /// <summary>
        /// True if at least one cached controller device is still connected.
        /// </summary>
        private bool HasValidDevice()
        {
            for (int i = 0; i < m_devices.Count; i++)
            {
                if (m_devices[i].isValid)
                    return true;
            }

            return false;
        }

        /// <summary>
        /// Removes the overlay and unlocks, so the next detected screen gets a fresh overlay.
        /// </summary>
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

        /// <summary>
        /// Unlocks the overlay but keeps it visible until a new stable pose is found.
        /// </summary>
        public void UnlockWithoutDestroyingOverlay()
        {
            m_locked = false;
            m_validFrames = 0;
        }

        /// <summary>
        /// Shows/hides the navigation arrow (e.g. from a UI button or another script).
        /// </summary>
        public void ToggleDirectionsFromCode()
        {
            if (m_directionsInstance != null)
                m_directionsInstance.SetActive(!m_directionsInstance.activeSelf);
        }
    }
}
