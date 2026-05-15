using System;
using System.Reflection;
using CesiumForUnity;
using Unity.Mathematics;
using UnityEngine;

namespace CommandP.Core
{
    /// <summary>
    /// 卫星向地面探测的半透明锥体特效。
    /// 锥体沿本地 +Z 轴延伸，并尽量朝向卫星正下方的地面点。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class SatelliteGroundProbeEffect : MonoBehaviour
    {
        [Header("启用")]
        [SerializeField] private bool _enabled = true;

        [Header("视锥参数")]
        [SerializeField, Min(100f)] private float _length = 420000f;
        [SerializeField, Range(5f, 85f)] private float _halfAngleDeg = 18f;
        [SerializeField, Range(12, 64)] private int _sides = 32;

        [Header("颜色")]
        [SerializeField] private Color _fillColor = new Color(0.12f, 0.9f, 0.85f, 0.22f);
        [SerializeField, Min(0f)] private float _emissionIntensity = 2.2f;
        [SerializeField, Range(0f, 1f)] private float _alpha = 0.22f;
        [SerializeField, Min(0.01f)] private float _duration = 2.2f;
        [SerializeField, Range(1, 200)] private int _repeat = 14;
        [SerializeField, Range(0f, 1f)] private float _thickness = 0.22f;
        [SerializeField] private float _offset = 0f;

        [Header("对准地面")]
        [SerializeField] private bool _autoAimAtGround = true;
        [SerializeField, Min(0f)] private float _groundOffsetMeters = 0f;
        [SerializeField] private bool _clampToEarthSurface = true;

        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Material _fillMat;
        private bool _inited;
        private float _timeAcc;
        private float _lastLength = -1f;
        private float _lastHalfAngleDeg = -1f;
        private int _lastSides = -1;
        private CesiumGlobeAnchor _cachedAnchor;
        private CesiumGeoreference _cachedGeoreference;
        private float _effectiveLength;  // 实际用于渲染的长度

        private const double EarthRadiusMeters = 6378137.0;

        public void Configure(
            bool enabled,
            float lengthMeters,
            float halfAngleDeg,
            int sides,
            Color fillColor,
            float emissionIntensity,
            float alpha,
            float durationSec,
            int repeat,
            float thickness,
            float offsetMeters)
        {
            _enabled = enabled;
            _length = Mathf.Max(100f, lengthMeters);
            _halfAngleDeg = halfAngleDeg;
            _sides = Mathf.Clamp(sides, 12, 64);
            _fillColor = fillColor;
            _emissionIntensity = Mathf.Max(0f, emissionIntensity);
            _alpha = Mathf.Clamp01(alpha);
            _duration = Mathf.Max(0.01f, durationSec);
            _repeat = repeat;
            _thickness = Mathf.Clamp01(thickness);
            _offset = offsetMeters;

            InternalInit();
            RebuildAll();
        }

        private void Awake()
        {
            InternalInit();
            RebuildAll();
        }

        private void OnEnable()
        {
            InternalInit();
            RebuildAll();
        }

        private void Update()
        {
            if (!_inited)
            {
                return;
            }

            SyncTransformToGround();
            
            // 计算不穿过地球的最大有效长度
            UpdateEffectiveLength();

            if (_fillMat != null)
            {
                _timeAcc += Time.deltaTime;
                float cycle = Mathf.Max(0.0001f, _duration);
                float scanTime = (_timeAcc % cycle) / cycle;
                _fillMat.SetFloat("_Length", _effectiveLength);
                _fillMat.SetColor("_Color", new Color(_fillColor.r, _fillColor.g, _fillColor.b, _alpha));
                _fillMat.SetFloat("_EmissionIntensity", _emissionIntensity);
                _fillMat.SetFloat("_ScanTime", scanTime);
                _fillMat.SetFloat("_Repeat", _repeat);
                _fillMat.SetFloat("_Thickness", _thickness);
                _fillMat.SetFloat("_Offset", _offset);
            }

            SetVisible(_enabled);
        }

        private void OnDestroy()
        {
            CleanupMesh();
            if (_fillMat != null)
            {
                Destroy(_fillMat);
                _fillMat = null;
            }
        }

        private void InternalInit()
        {
            if (_inited)
            {
                return;
            }

            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();
            if (_meshFilter == null)
            {
                _meshFilter = gameObject.AddComponent<MeshFilter>();
            }

            if (_meshRenderer == null)
            {
                _meshRenderer = gameObject.AddComponent<MeshRenderer>();
            }

            Shader shader = Shader.Find("Unlit/EmissiveCone")
                           ?? Shader.Find("Universal Render Pipeline/Unlit")
                           ?? Shader.Find("Sprites/Default");
            if (_fillMat == null && shader != null)
            {
                _fillMat = new Material(shader);
                SetupTransparent(_fillMat);
                _fillMat.SetColor("_Color", new Color(_fillColor.r, _fillColor.g, _fillColor.b, _alpha));
                _fillMat.SetFloat("_Length", _length);
                _fillMat.SetFloat("_EmissionIntensity", _emissionIntensity);
                _fillMat.SetFloat("_ScanTime", 0f);
                _fillMat.SetFloat("_Repeat", _repeat);
                _fillMat.SetFloat("_Thickness", _thickness);
                _fillMat.SetFloat("_Offset", _offset);
            }

            if (_fillMat != null)
            {
                _meshRenderer.material = _fillMat;
            }

            _meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _meshRenderer.receiveShadows = false;
            _inited = true;
        }

        private static void SetupTransparent(Material material)
        {
            if (material == null)
            {
                return;
            }

            material.SetFloat("_Surface", 1f);
            material.SetFloat("_BlendMode", 0f);
            material.SetFloat("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_AlphaClip", 0f);
            material.SetShaderPassEnabled("DepthOnly", false);
            material.SetShaderPassEnabled("SHADOWCASTER", false);
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }

        private void RebuildAll()
        {
            if (!_inited)
            {
                return;
            }

            if (Mathf.Abs(_lastLength - _length) < 0.01f
                && Mathf.Abs(_lastHalfAngleDeg - _halfAngleDeg) < 0.01f
                && _lastSides == _sides
                && _meshFilter != null
                && _meshFilter.sharedMesh != null)
            {
                return;
            }

            BuildFrustumMesh();
            _lastLength = _length;
            _lastHalfAngleDeg = _halfAngleDeg;
            _lastSides = _sides;
        }

        private void BuildFrustumMesh()
        {
            float len = Mathf.Max(100f, _length);
            float halfAngle = Mathf.Clamp(_halfAngleDeg, 2f, 85f) * Mathf.Deg2Rad;
            float radius = len * Mathf.Tan(halfAngle);
            int sides = Mathf.Clamp(_sides, 6, 64);

            int vertCount = 1 + sides;
            var verts = new Vector3[vertCount];
            var tris = new int[sides * 3];

            verts[0] = Vector3.zero;
            for (int i = 0; i < sides; i++)
            {
                float angle = (360f / sides) * i * Mathf.Deg2Rad;
                verts[1 + i] = new Vector3(
                    radius * Mathf.Cos(angle),
                    radius * Mathf.Sin(angle),
                    len);
            }

            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                tris[i * 3 + 0] = 0;
                tris[i * 3 + 1] = 1 + next;
                tris[i * 3 + 2] = 1 + i;
            }

            var mesh = new Mesh { name = "SatelliteGroundProbe" };
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            CleanupMesh();
            _meshFilter.sharedMesh = mesh;
        }

        private void CleanupMesh()
        {
            if (_meshFilter != null && _meshFilter.sharedMesh != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying)
                {
                    DestroyImmediate(_meshFilter.sharedMesh);
                }
                else
                {
                    Destroy(_meshFilter.sharedMesh);
                }
#else
                Destroy(_meshFilter.sharedMesh);
#endif
                _meshFilter.sharedMesh = null;
            }
        }

        private void SyncTransformToGround()
        {
            if (!_autoAimAtGround)
            {
                transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                return;
            }

            if (!TryGetGroundDirection(out Vector3 direction))
            {
                transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                return;
            }

            if (direction.sqrMagnitude < 0.0001f)
            {
                return;
            }

            transform.rotation = Quaternion.FromToRotation(Vector3.forward, direction.normalized);
        }

        private void UpdateEffectiveLength()
        {
            _effectiveLength = _length;

            if (!_clampToEarthSurface || _length < 100f)
            {
                return;
            }

            // 获取卫星与地面点的实际距离
            if (!TryGetAnchorLlh(out double longitudeDeg, out double latitudeDeg, out double altitudeMeters))
            {
                return;
            }

            double3 satelliteEcef = LlhToEcef(latitudeDeg, longitudeDeg, altitudeMeters);
            double3 groundEcef = LlhToEcef(latitudeDeg, longitudeDeg, _groundOffsetMeters);

            double dx = groundEcef.x - satelliteEcef.x;
            double dy = groundEcef.y - satelliteEcef.y;
            double dz = groundEcef.z - satelliteEcef.z;
            float distanceToGround = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);

            float minSafeDistance = 1000f;  // 至少保持1km安全距离
            _effectiveLength = Mathf.Min(_length, Mathf.Max(0f, distanceToGround - minSafeDistance));
        }

        private bool TryGetGroundDirection(out Vector3 direction)
        {
            direction = Vector3.down;

            if (!TryGetAnchorLlh(out double longitudeDeg, out double latitudeDeg, out double altitudeMeters))
            {
                return false;
            }

            double3 satelliteEcef = LlhToEcef(latitudeDeg, longitudeDeg, altitudeMeters);
            double3 groundEcef = LlhToEcef(latitudeDeg, longitudeDeg, _groundOffsetMeters);

            if (!TryTransformEcefToUnity(satelliteEcef, out Vector3 satelliteWorld))
            {
                return false;
            }

            if (!TryTransformEcefToUnity(groundEcef, out Vector3 groundWorld))
            {
                return false;
            }

            direction = groundWorld - satelliteWorld;
            return true;
        }

        private bool TryGetAnchorLlh(out double longitudeDeg, out double latitudeDeg, out double altitudeMeters)
        {
            longitudeDeg = 0.0;
            latitudeDeg = 0.0;
            altitudeMeters = 0.0;

            if (_cachedAnchor == null)
            {
                _cachedAnchor = GetComponent<CesiumGlobeAnchor>();
                if (_cachedAnchor == null)
                {
                    _cachedAnchor = GetComponentInParent<CesiumGlobeAnchor>();
                }
            }

            if (_cachedAnchor == null)
            {
                return false;
            }

            Type anchorType = _cachedAnchor.GetType();
            PropertyInfo llhProperty = anchorType.GetProperty("longitudeLatitudeHeight");
            if (llhProperty != null)
            {
                object value = llhProperty.GetValue(_cachedAnchor);
                if (value is double3 llh)
                {
                    longitudeDeg = llh.x;
                    latitudeDeg = llh.y;
                    altitudeMeters = llh.z;
                    return true;
                }
            }

            MethodInfo llhMethod = anchorType.GetMethod("GetPositionLongitudeLatitudeHeight", Type.EmptyTypes);
            if (llhMethod != null)
            {
                object value = llhMethod.Invoke(_cachedAnchor, null);
                if (value is double3 llh)
                {
                    longitudeDeg = llh.x;
                    latitudeDeg = llh.y;
                    altitudeMeters = llh.z;
                    return true;
                }
            }

            return false;
        }

        private bool TryTransformEcefToUnity(double3 ecef, out Vector3 scenePos)
        {
            scenePos = default;

            if (_cachedGeoreference == null)
            {
                _cachedGeoreference = FindFirstObjectByType<CesiumGeoreference>();
            }

            if (_cachedGeoreference != null)
            {
                Type georefType = _cachedGeoreference.GetType();
                MethodInfo method = georefType.GetMethod("TransformEarthCenteredEarthFixedPositionToUnity", new[] { typeof(double3) });
                if (method != null)
                {
                    object result = method.Invoke(_cachedGeoreference, new object[] { ecef });
                    if (result is double3 unityPos)
                    {
                        scenePos = new Vector3((float)unityPos.x, (float)unityPos.y, (float)unityPos.z);
                        return true;
                    }
                }
            }

            scenePos = new Vector3((float)(ecef.x * 0.001), (float)(ecef.y * 0.001), (float)(ecef.z * 0.001));
            return true;
        }

        private static double3 LlhToEcef(double latitudeDeg, double longitudeDeg, double altitudeMeters)
        {
            const double semiMajorAxis = 6378137.0;
            const double flattening = 1.0 / 298.257223563;
            double eccentricitySquared = flattening * (2.0 - flattening);

            double lat = latitudeDeg * Math.PI / 180.0;
            double lon = longitudeDeg * Math.PI / 180.0;

            double sinLat = Math.Sin(lat);
            double cosLat = Math.Cos(lat);
            double sinLon = Math.Sin(lon);
            double cosLon = Math.Cos(lon);

            double n = semiMajorAxis / Math.Sqrt(1.0 - eccentricitySquared * sinLat * sinLat);
            double x = (n + altitudeMeters) * cosLat * cosLon;
            double y = (n + altitudeMeters) * cosLat * sinLon;
            double z = (n * (1.0 - eccentricitySquared) + altitudeMeters) * sinLat;

            return new double3(x, y, z);
        }

        private void SetVisible(bool visible)
        {
            if (_meshRenderer != null)
            {
                _meshRenderer.enabled = visible;
            }
        }
    }
}
