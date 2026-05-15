using UnityEngine;

namespace CommandP.GlobalEntity.Rendering
{
    /// <summary>
    /// World-Space Billboard with optional distance-based scale compensation.
    ///
    /// Rotation:  markerRoot.rotation = camera.rotation  (no LookAt, no yaw-only)
    /// Scale:     uniform scale proportional to camera distance, keeping the
    ///            marker at constant screen-space size regardless of range.
    ///
    /// Attached to MarkerRoot, executes in LateUpdate after camera settles.
    /// </summary>
    public class BillboardMarker : MonoBehaviour
    {
        private Camera _targetCamera;

        [Header("Distance Scale")]
        [SerializeField] private bool _enableDistanceScale = true;
        [SerializeField] private float _referenceDistance = 5000f;
        [SerializeField] private float _minScale = 0.5f;
        [SerializeField] private float _maxScale = 20f;

        public void AssignCamera(Camera cam) => _targetCamera = cam;
        public bool HasCamera => _targetCamera != null;

        /// <summary>
        /// Configure distance-based scale compensation.
        /// At referenceDistance, scale = 1.0. Closer → smaller, farther → larger.
        /// </summary>
        public void ConfigureScale(bool enable, float referenceDist, float min, float max)
        {
            _enableDistanceScale = enable;
            _referenceDistance = referenceDist;
            _minScale = min;
            _maxScale = max;
        }

        private void LateUpdate()
        {
            if (_targetCamera == null) return;

            // Billboard: always face camera — no LookRotation, no yaw-only, no flip
            transform.rotation = _targetCamera.transform.rotation;

            // Distance scale: keep screen-space size constant
            if (_enableDistanceScale)
            {
                float distance = Vector3.Distance(_targetCamera.transform.position, transform.position);
                float scale = distance / _referenceDistance;
                scale = Mathf.Clamp(scale, _minScale, _maxScale);
                transform.localScale = Vector3.one * scale;
            }
        }
    }
}
