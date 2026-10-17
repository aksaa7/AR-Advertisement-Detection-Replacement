// Copyright (c) Meta Platforms, Inc. and affiliates.

using System.Collections.Generic;
using Meta.XR;
using Meta.XR.Samples;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace PassthroughCameraSamples.MultiObjectDetection
{
    [MetaCodeSample("PassthroughCameraApiSamples-MultiObjectDetection")]
    public class SentisInferenceUiManager : MonoBehaviour
    {
        [Header("Placement configuration")]
        [SerializeField] private EnvironmentRayCastSampleManager m_environmentRaycast;
        [SerializeField] private PassthroughCameraAccess m_cameraAccess;

        [SerializeField] private RectTransform m_detectionBoxPrefab;
        [Header("TV candidate filtering")]
        [SerializeField, Range(0f, 1f)] private float m_tvMinimumViewportArea = 0.02f;
        [SerializeField, Range(0f, 1f)] private float m_tvMinimumConfidence = 0.3f;
        [SerializeField, Min(1f)] private float m_tvMaximumAspectRatio = 3f;
        [SerializeField, Range(0f, 1f)] private float m_tvLaptopOverlapThreshold = 0.6f;
        [SerializeField, Range(0f, 1f)] private float m_tvMinimumNormalConfidence = 0.4f;
        [Header("TV position stabilization")]
        [SerializeField, Min(2)] private int m_tvStableSamplesRequired = 4;
        [SerializeField, Min(0.1f)] private float m_tvStableDurationSeconds = 0.75f;
        [SerializeField, Min(0.1f)] private float m_tvMaxSampleGapSeconds = 1f;
        [SerializeField, Min(0.01f)] private float m_tvMinimumPositionToleranceMeters = 0.05f;
        [SerializeField, Range(0.01f, 0.25f)] private float m_tvRelativePositionTolerance = 0.05f;
        [SerializeField, Min(0.01f)] private float m_tvMinimumSizeToleranceMeters = 0.03f;
        [SerializeField, Range(0.01f, 0.5f)] private float m_tvRelativeSizeTolerance = 0.12f;
        [SerializeField, Range(1f, 45f)] private float m_tvMaximumNormalAngleDegrees = 12f;
        [SerializeField, Min(1.1f)] private float m_tvReacquireAreaRatio = 1.5f;
        [SerializeField, Min(0.1f)] private float m_tvReacquireDistanceMeters = 0.35f;
        [Space(10)]
        public UnityEvent<int> OnObjectsDetected;

        internal readonly List<BoundingBoxData> m_boxDrawn = new();
        private string[] m_labels;
        private readonly List<BoundingBoxData> m_boxPool = new();

        internal class BoundingBoxData
        {
            public string ClassName;
            public int ClassId;
            public RectTransform BoxRectTransform;
            public float lastUpdateTime;

            // Added properties for auto-locking
            public bool isNewlyCreated;
            public float firstDetectionTime;
            public bool isLocked;

            // TV-only candidate state. Each sample comes from a new inference result.
            public int tvStableSamples;
            public float tvStableSince;
            public float tvLastSampleTime;
            public Vector3 tvAveragePosition;
            public Vector3 tvAverageForward;
            public Vector2 tvAverageSize;
        }

        private void Awake() => m_detectionBoxPrefab.gameObject.SetActive(false);

        private void Update()
        {
            // We leave this commented out so boxes NEVER disappear when you look away
            /*
            for (int i = m_boxDrawn.Count - 1; i >= 0; i--)
            {
                var box = m_boxDrawn[i];
                const float timeToPersistBoxes = 3f;
                if (Time.time - box.lastUpdateTime > timeToPersistBoxes)
                {
                    ReturnToPool(box);
                    m_boxDrawn.RemoveAt(i);
                }
            }*/
        }

        public void SetLabels(TextAsset labelsAsset)
        {
            m_labels = labelsAsset.text.Split('\n');
        }

        public void DrawUIBoxes(
            List<(int classId, float score, Vector4 boundingBox)> detections,
            Vector2 inputSize,
            Pose cameraPose)
        {
            if (detections.Count == 0)
            {
                OnObjectsDetected?.Invoke(0);
                return;
            }

            OnObjectsDetected?.Invoke(detections.Count);

            // Rank TV candidates using both confidence and visible size. Candidates
            // that are better explained by an overlapping laptop detection are
            // excluded before selecting the single TV target for this frame.
            int bestTvDetectionIndex = FindBestTvDetectionIndex(detections, inputSize);

            for (var i = 0; i < detections.Count; i++)
            {
                var detection = detections[i];
                float x1 = detection.boundingBox[0];
                float y1 = detection.boundingBox[1];
                float x2 = detection.boundingBox[2];
                float y2 = detection.boundingBox[3];
                Rect rect = new Rect(x1, y1, x2 - x1, y2 - y1);

                Vector2 normalizedCenter = rect.center / inputSize;
                var classname = m_labels[detection.classId].Replace(" ", "_");
                bool isTvDetection = string.Equals(classname.Trim(), "tvmonitor", System.StringComparison.OrdinalIgnoreCase);
                if (isTvDetection && i != bestTvDetectionIndex)
                {
                    continue;
                }

                var ray = m_cameraAccess.ViewportPointToRay(new Vector2(normalizedCenter.x, 1.0f - normalizedCenter.y), cameraPose);
                var environmentHit = m_environmentRaycast.Raycast(ray);
                if (!environmentHit.HasValue)
                {
                    continue;
                }

                var normRect = new Rect(
                    rect.x / inputSize.x,
                    1f - rect.yMax / inputSize.y,
                    rect.width / inputSize.x,
                    rect.height / inputSize.y
                );

                if (isTvDetection &&
                    (environmentHit.Value.normal.sqrMagnitude <= 0.0001f ||
                     environmentHit.Value.normalConfidence < m_tvMinimumNormalConfidence))
                {
                    continue;
                }

                float distance = Vector3.Distance(cameraPose.position, environmentHit.Value.point);
                var worldSpaceCenter = isTvDetection
                    ? environmentHit.Value.point
                    : m_cameraAccess.ViewportPointToRay(normRect.center, cameraPose).GetPoint(distance);
                var viewForward = (worldSpaceCenter - cameraPose.position).normalized;
                var placementForward = viewForward;

                if (isTvDetection && environmentHit.Value.normal.sqrMagnitude > 0.0001f)
                {
                    placementForward = environmentHit.Value.normal.normalized;

                    // Keep the RectTransform forward convention used by the overlay:
                    // forward points from the camera into the detected surface.
                    if (Vector3.Dot(placementForward, viewForward) < 0f)
                        placementForward = -placementForward;
                }

                Vector3 placementUp = Vector3.ProjectOnPlane(Vector3.up, placementForward).normalized;
                if (placementUp.sqrMagnitude < 0.0001f)
                {
                    placementUp = Vector3.ProjectOnPlane(
                        cameraPose.rotation * Vector3.up,
                        placementForward).normalized;
                }

                Quaternion placementRotation = Quaternion.LookRotation(placementForward, placementUp);

                var plane = new Plane(placementForward, worldSpaceCenter);
                Vector2 size;

                if (isTvDetection)
                {
                    if (!TryProjectTvRectOntoSurface(
                            normRect,
                            cameraPose,
                            plane,
                            placementRotation,
                            worldSpaceCenter,
                            out worldSpaceCenter,
                            out size))
                    {
                        continue;
                    }
                }
                else
                {
                    var minRay = m_cameraAccess.ViewportPointToRay(normRect.min);
                    var maxRay = m_cameraAccess.ViewportPointToRay(normRect.max);
                    if (!TryGetPointOnPlane(minRay, plane, out Vector3 min) ||
                        !TryGetPointOnPlane(maxRay, plane, out Vector3 max))
                    {
                        continue;
                    }

                    size = GetCameraAlignedSize(min, max, cameraPose);
                }

                var boxData = GetOrCreateBoundingBoxData(detection.classId, worldSpaceCenter, size);
                var boxRectTransform = boxData.BoxRectTransform;
                boxRectTransform.GetComponentInChildren<Text>().text = $"Id: {detection.classId} Class: {classname}";

                if (isTvDetection)
                {
                    if (boxData.isLocked &&
                        ShouldReacquireTv(boxData, worldSpaceCenter, size))
                    {
                        ResetTvCandidate(boxData);
                    }

                    // Lock only after fresh TV measurements form a stable cluster.
                    // Instability delays locking but never hides the detection.
                    if (!boxData.isLocked)
                    {
                        UpdateTvCandidate(boxData, worldSpaceCenter, placementForward, size);
                    }
                }
                else
                {
                    // Preserve the original behavior for every non-TV detection.
                    if (!boxData.isLocked && Time.time - boxData.firstDetectionTime > 1.0f)
                    {
                        boxData.isLocked = true;
                    }

                    if (!boxData.isLocked)
                    {
                        if (boxData.isNewlyCreated)
                        {
                            boxRectTransform.SetPositionAndRotation(worldSpaceCenter, placementRotation);
                            boxRectTransform.sizeDelta = size;
                            boxData.isNewlyCreated = false;
                        }
                        else
                        {
                            float lerpSpeed = 5f * Time.deltaTime;
                            boxRectTransform.position = Vector3.Lerp(boxRectTransform.position, worldSpaceCenter, lerpSpeed);
                            boxRectTransform.rotation = Quaternion.Slerp(boxRectTransform.rotation, placementRotation, lerpSpeed);
                            boxRectTransform.sizeDelta = Vector2.Lerp(boxRectTransform.sizeDelta, size, lerpSpeed);
                        }
                    }
                }

                boxData.lastUpdateTime = Time.time;
            }
        }

        private bool TryProjectTvRectOntoSurface(
            Rect viewportRect,
            Pose cameraPose,
            Plane surfacePlane,
            Quaternion surfaceRotation,
            Vector3 planeOrigin,
            out Vector3 center,
            out Vector2 size)
        {
            Vector3 right = surfaceRotation * Vector3.right;
            Vector3 up = surfaceRotation * Vector3.up;
            float minX = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float minY = float.PositiveInfinity;
            float maxY = float.NegativeInfinity;

            bool AccumulateCorner(Vector2 viewportPoint)
            {
                Ray cornerRay = m_cameraAccess.ViewportPointToRay(viewportPoint, cameraPose);
                if (!TryGetPointOnPlane(cornerRay, surfacePlane, out Vector3 point))
                    return false;

                Vector3 offset = point - planeOrigin;
                float x = Vector3.Dot(offset, right);
                float y = Vector3.Dot(offset, up);
                minX = Mathf.Min(minX, x);
                maxX = Mathf.Max(maxX, x);
                minY = Mathf.Min(minY, y);
                maxY = Mathf.Max(maxY, y);
                return true;
            }

            if (!AccumulateCorner(new Vector2(viewportRect.xMin, viewportRect.yMin)) ||
                !AccumulateCorner(new Vector2(viewportRect.xMin, viewportRect.yMax)) ||
                !AccumulateCorner(new Vector2(viewportRect.xMax, viewportRect.yMin)) ||
                !AccumulateCorner(new Vector2(viewportRect.xMax, viewportRect.yMax)))
            {
                center = default;
                size = default;
                return false;
            }

            center = planeOrigin + right * ((minX + maxX) * 0.5f) + up * ((minY + maxY) * 0.5f);
            size = new Vector2(maxX - minX, maxY - minY);
            return size.x > 0f && size.y > 0f;
        }

        private static bool TryGetPointOnPlane(Ray ray, Plane plane, out Vector3 point)
        {
            if (plane.Raycast(ray, out float distance) && distance > 0f)
            {
                point = ray.GetPoint(distance);
                return true;
            }

            point = default;
            return false;
        }

        private static Vector2 GetCameraAlignedSize(Vector3 min, Vector3 max, Pose cameraPose)
        {
            Vector3 minLocal = Quaternion.Inverse(cameraPose.rotation) * (min - cameraPose.position);
            Vector3 maxLocal = Quaternion.Inverse(cameraPose.rotation) * (max - cameraPose.position);
            return new Vector2(
                Mathf.Abs(maxLocal.x - minLocal.x),
                Mathf.Abs(maxLocal.y - minLocal.y));
        }

        private bool ShouldReacquireTv(BoundingBoxData box, Vector3 position, Vector2 size)
        {
            Vector2 lockedSize = box.BoxRectTransform.sizeDelta;
            float lockedArea = Mathf.Abs(lockedSize.x * lockedSize.y);
            float candidateArea = Mathf.Abs(size.x * size.y);
            if (lockedArea <= 0.0001f || candidateArea <= 0.0001f)
                return true;

            float areaRatio = Mathf.Max(candidateArea / lockedArea, lockedArea / candidateArea);
            float distanceThreshold = Mathf.Max(
                m_tvReacquireDistanceMeters,
                lockedSize.magnitude * 0.35f);

            return areaRatio >= m_tvReacquireAreaRatio ||
                   Vector3.Distance(box.BoxRectTransform.position, position) >= distanceThreshold;
        }

        private static void ResetTvCandidate(BoundingBoxData box)
        {
            box.isLocked = false;
            box.tvStableSamples = 0;
            box.tvStableSince = 0f;
            box.tvLastSampleTime = 0f;
            box.tvAveragePosition = Vector3.zero;
            box.tvAverageForward = Vector3.zero;
            box.tvAverageSize = Vector2.zero;
        }

        private int FindBestTvDetectionIndex(
            List<(int classId, float score, Vector4 boundingBox)> detections,
            Vector2 inputSize)
        {
            if (m_labels == null || inputSize.x <= 0f || inputSize.y <= 0f)
                return -1;

            int bestIndex = -1;
            float bestRank = 0f;
            float inputArea = inputSize.x * inputSize.y;

            for (int i = 0; i < detections.Count; i++)
            {
                var detection = detections[i];
                if (detection.classId < 0 || detection.classId >= m_labels.Length)
                    continue;

                string className = m_labels[detection.classId].Trim().Replace(" ", "_");
                if (!string.Equals(className, "tvmonitor", System.StringComparison.OrdinalIgnoreCase))
                    continue;

                Rect candidateRect = GetClampedDetectionRect(detection.boundingBox, inputSize);
                float width = candidateRect.width;
                float height = candidateRect.height;
                if (width <= 0f || height <= 0f)
                    continue;

                float normalizedArea = width * height / inputArea;
                float aspectRatio = Mathf.Max(width / height, height / width);
                if (detection.score < m_tvMinimumConfidence ||
                    normalizedArea < m_tvMinimumViewportArea ||
                    aspectRatio > m_tvMaximumAspectRatio)
                {
                    continue;
                }

                if (IsCoveredByLaptopCandidate(candidateRect, detections, inputSize))
                    continue;

                float rank = detection.score * Mathf.Sqrt(normalizedArea);
                if (rank > bestRank)
                {
                    bestRank = rank;
                    bestIndex = i;
                }
            }

            return bestIndex;
        }

        private bool IsCoveredByLaptopCandidate(
            Rect tvRect,
            List<(int classId, float score, Vector4 boundingBox)> detections,
            Vector2 inputSize)
        {
            float tvArea = tvRect.width * tvRect.height;
            if (tvArea <= 0f)
                return false;

            foreach (var detection in detections)
            {
                if (detection.classId < 0 || detection.classId >= m_labels.Length)
                    continue;

                string className = m_labels[detection.classId].Trim().Replace(" ", "_");
                if (!string.Equals(className, "laptop", System.StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                Rect laptopRect = GetClampedDetectionRect(detection.boundingBox, inputSize);
                float intersectionWidth = Mathf.Max(
                    0f,
                    Mathf.Min(tvRect.xMax, laptopRect.xMax) - Mathf.Max(tvRect.xMin, laptopRect.xMin));
                float intersectionHeight = Mathf.Max(
                    0f,
                    Mathf.Min(tvRect.yMax, laptopRect.yMax) - Mathf.Max(tvRect.yMin, laptopRect.yMin));
                float tvCoverage = intersectionWidth * intersectionHeight / tvArea;

                if (tvCoverage >= m_tvLaptopOverlapThreshold)
                    return true;
            }

            return false;
        }

        private static Rect GetClampedDetectionRect(Vector4 box, Vector2 inputSize)
        {
            float xMin = Mathf.Clamp(Mathf.Min(box.x, box.z), 0f, inputSize.x);
            float yMin = Mathf.Clamp(Mathf.Min(box.y, box.w), 0f, inputSize.y);
            float xMax = Mathf.Clamp(Mathf.Max(box.x, box.z), 0f, inputSize.x);
            float yMax = Mathf.Clamp(Mathf.Max(box.y, box.w), 0f, inputSize.y);
            return Rect.MinMaxRect(xMin, yMin, xMax, yMax);
        }

        private void UpdateTvCandidate(BoundingBoxData box, Vector3 position, Vector3 forward, Vector2 size)
        {
            float now = Time.time;
            bool hasCandidate = box.tvStableSamples > 0;
            bool sampleGapIsValid = hasCandidate &&
                                    now - box.tvLastSampleTime <= Mathf.Max(0.1f, m_tvMaxSampleGapSeconds);

            float positionTolerance = hasCandidate
                ? Mathf.Max(m_tvMinimumPositionToleranceMeters, box.tvAverageSize.magnitude * m_tvRelativePositionTolerance)
                : 0f;
            float widthTolerance = hasCandidate
                ? Mathf.Max(m_tvMinimumSizeToleranceMeters, box.tvAverageSize.x * m_tvRelativeSizeTolerance)
                : 0f;
            float heightTolerance = hasCandidate
                ? Mathf.Max(m_tvMinimumSizeToleranceMeters, box.tvAverageSize.y * m_tvRelativeSizeTolerance)
                : 0f;

            bool isConsistent = sampleGapIsValid &&
                                Vector3.Distance(position, box.tvAveragePosition) <= positionTolerance &&
                                Vector3.Angle(forward, box.tvAverageForward) <= m_tvMaximumNormalAngleDegrees &&
                                Mathf.Abs(size.x - box.tvAverageSize.x) <= widthTolerance &&
                                Mathf.Abs(size.y - box.tvAverageSize.y) <= heightTolerance;

            if (!isConsistent)
            {
                box.tvStableSamples = 1;
                box.tvStableSince = now;
                box.tvAveragePosition = position;
                box.tvAverageForward = forward;
                box.tvAverageSize = size;
            }
            else
            {
                box.tvStableSamples++;
                float sampleWeight = 1f / box.tvStableSamples;
                box.tvAveragePosition = Vector3.Lerp(box.tvAveragePosition, position, sampleWeight);
                box.tvAverageForward = Vector3.Slerp(box.tvAverageForward, forward, sampleWeight).normalized;
                box.tvAverageSize = Vector2.Lerp(box.tvAverageSize, size, sampleWeight);
            }

            box.tvLastSampleTime = now;
            box.BoxRectTransform.SetPositionAndRotation(
                box.tvAveragePosition,
                GetSurfaceRotation(box.tvAverageForward));
            box.BoxRectTransform.sizeDelta = box.tvAverageSize;
            box.isNewlyCreated = false;

            int requiredSamples = Mathf.Max(2, m_tvStableSamplesRequired);
            float requiredDuration = Mathf.Max(0.1f, m_tvStableDurationSeconds);
            if (box.tvStableSamples >= requiredSamples && now - box.tvStableSince >= requiredDuration)
            {
                box.isLocked = true;
            }
        }

        private static Quaternion GetSurfaceRotation(Vector3 forward)
        {
            Vector3 up = Vector3.ProjectOnPlane(Vector3.up, forward).normalized;
            if (up.sqrMagnitude < 0.0001f)
                up = Vector3.ProjectOnPlane(Vector3.forward, forward).normalized;

            return Quaternion.LookRotation(forward, up);
        }

        private BoundingBoxData GetOrCreateBoundingBoxData(int classId, Vector3 worldSpaceCenter, Vector2 worldSpaceSize)
        {
            BoundingBoxData reusedBox = null;
            bool isTvClass = classId >= 0 && classId < m_labels.Length &&
                             string.Equals(
                                 m_labels[classId].Trim().Replace(" ", "_"),
                                 "tvmonitor",
                                 System.StringComparison.OrdinalIgnoreCase);
            float maximumReuseDistance = Mathf.Clamp(worldSpaceSize.magnitude * 0.5f, 0.1f, 0.4f);

            for (int i = m_boxDrawn.Count - 1; i >= 0; i--)
            {
                var box = m_boxDrawn[i];

                // Calculate real-world physical distance to prevent duplicate boxes when looking back
                float physicalDistance = Vector3.Distance(box.BoxRectTransform.position, worldSpaceCenter);

                var localPos = box.BoxRectTransform.InverseTransformPoint(worldSpaceCenter);
                var newBox = new Vector4(
                    localPos.x - worldSpaceSize.x * 0.5f,
                    localPos.y - worldSpaceSize.y * 0.5f,
                    localPos.x + worldSpaceSize.x * 0.5f,
                    localPos.y + worldSpaceSize.y * 0.5f
                );

                var sizeDelta = box.BoxRectTransform.sizeDelta;
                var currentBox = new Vector4(
                    -sizeDelta.x * 0.5f,
                    -sizeDelta.y * 0.5f,
                    sizeDelta.x * 0.5f,
                    sizeDelta.y * 0.5f);

                if (box.ClassId == classId)
                {
                    float overlap = SentisInferenceRunManager.CalculateIoU(newBox, currentBox);
                    bool isSamePhysicalObject = isTvClass
                        // This app intentionally maintains one persistent TV target.
                        // Reuse it so a stronger candidate can trigger reacquisition.
                        ? true
                        : overlap > 0.25f || physicalDistance < maximumReuseDistance;

                    if (isSamePhysicalObject)
                    {
                        if (reusedBox == null)
                        {
                            reusedBox = box;
                        }
                        else
                        {
                            ReturnToPool(box);
                            m_boxDrawn.RemoveAt(i);
                        }
                    }
                }
                // Different classes may legitimately overlap (for example a TV
                // and a potted plant in front of it). Keep both annotations; NMS
                // already removes duplicate predictions within the same class.
            }

            if (reusedBox != null)
            {
                return reusedBox;
            }

            var newData = GetBoxFromPoolOrCreate();
            newData.ClassId = classId;
            newData.ClassName = m_labels[classId].Replace(" ", "_");
            m_boxDrawn.Add(newData);
            return newData;
        }

        private BoundingBoxData GetBoxFromPoolOrCreate()
        {
            if (m_boxPool.Count > 0)
            {
                var pooled = m_boxPool[m_boxPool.Count - 1];
                pooled.BoxRectTransform.gameObject.SetActive(true);

                // Reset timers and flags for reused objects
                pooled.isNewlyCreated = true;
                pooled.firstDetectionTime = Time.time;
                pooled.isLocked = false;
                pooled.tvStableSamples = 0;
                pooled.tvStableSince = 0f;
                pooled.tvLastSampleTime = 0f;
                pooled.tvAveragePosition = Vector3.zero;
                pooled.tvAverageForward = Vector3.zero;
                pooled.tvAverageSize = Vector2.zero;

                m_boxPool.RemoveAt(m_boxPool.Count - 1);
                return pooled;
            }

            var boxRectTransform = Instantiate(m_detectionBoxPrefab, ContentParent);
            boxRectTransform.gameObject.SetActive(true);
            return new BoundingBoxData
            {
                BoxRectTransform = boxRectTransform,
                isNewlyCreated = true,
                firstDetectionTime = Time.time,
                isLocked = false,
                tvStableSamples = 0
            };
        }

        internal Transform ContentParent => m_detectionBoxPrefab.parent;

        private void ReturnToPool(BoundingBoxData box)
        {
            box.BoxRectTransform.gameObject.SetActive(false);
            m_boxPool.Add(box);
        }

        internal void ClearAnnotations()
        {
            foreach (var box in m_boxDrawn)
            {
                ReturnToPool(box);
            }
            m_boxDrawn.Clear();
        }
    }
}
