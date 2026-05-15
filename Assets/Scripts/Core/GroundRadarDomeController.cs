using System;
using System.Collections.Generic;
using CesiumForUnity;
using Unity.Mathematics;
using UnityEngine;

namespace CommandP.Core
{
    /// <summary>
    /// 地面雷达半球：默认蓝色半透明，飞机进入探测体积后切换为红色。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class GroundRadarDomeController : MonoBehaviour
    {
        [Header("雷达阵地（经纬度）")]
        [SerializeField] private double _radarLatitude = 39.736401;
        [SerializeField] private double _radarLongitude = -105.25737;

        [Header("探测参数")]
        [SerializeField, Min(1000f)] private float _detectionRadiusM = 26000f;
        [SerializeField, Min(100f)] private float _detectionCeilingM = 12000f;

        [Header("外观")]
        [SerializeField] private Color _idleColor = new Color(0.22f, 0.62f, 1f, 0.22f);
        [SerializeField] private Color _alertColor = new Color(1f, 0.2f, 0.2f, 0.28f);
        [SerializeField] private bool _showGrid = true;
        [SerializeField] private Color _gridColor = new Color(0.7f, 0.95f, 1f, 0.55f);
        [SerializeField, Min(1f)] private float _gridLineWidth = 120f;
        [SerializeField, Range(2, 12)] private int _gridParallelCount = 6;
        [SerializeField, Range(4, 24)] private int _gridMeridianCount = 12;
        [SerializeField, Range(8, 96)] private int _longitudeSegments = 36;
        [SerializeField, Range(4, 48)] private int _latitudeSegments = 18;
        [SerializeField, Min(0.1f)] private float _colorLerpSpeed = 8f;

        [Header("Cesium")]
        [SerializeField] private bool _useCesiumAnchor = true;
        [SerializeField] private bool _showLog = false;
        [SerializeField] private bool _logEnterExit = true;

        private MeshFilter _meshFilter;
        private MeshRenderer _meshRenderer;
        private Material _material;
        private Material _gridMaterial;
        private AircraftSimulator _aircraftSimulator;
        private CesiumGlobeAnchor _globeAnchor;
        private bool _anchorInitialized;
        private Color _currentColor;
        private bool _lastInside;
        private Transform _gridRoot;
        private readonly List<LineRenderer> _gridLines = new List<LineRenderer>();

        public double RadarLatitude => _radarLatitude;
        public double RadarLongitude => _radarLongitude;
        public float DetectionRadiusM => _detectionRadiusM;
        public float DetectionCeilingM => _detectionCeilingM;
        public bool IsAircraftInside => _lastInside;

        private void Awake()
        {
            EnsureComponents();
            BuildDomeMesh();
            BuildGridLines();
            _currentColor = _idleColor;
            ApplyColor(_currentColor);
        }

        private void Start()
        {
            TryBindAircraftSimulator();
            TryInitAnchor();
        }

        private void Update()
        {
            if (_aircraftSimulator == null)
            {
                TryBindAircraftSimulator();
            }

            if (!_anchorInitialized)
            {
                TryInitAnchor();
            }

            bool inside = IsAircraftInsideDome();
            if (inside != _lastInside)
            {
                if (_logEnterExit)
                {
                    if (inside)
                        Debug.Log("[GroundRadarDomeController] 飞机进入雷达半球，颜色切换为红色。");
                    else
                        Debug.Log("[GroundRadarDomeController] 飞机离开雷达半球，颜色切换为蓝色。");
                }
                _lastInside = inside;
            }

            Color targetColor = inside ? _alertColor : _idleColor;
            _currentColor = Color.Lerp(_currentColor, targetColor, Time.deltaTime * _colorLerpSpeed);
            ApplyColor(_currentColor);
        }

        private void OnDestroy()
        {
            if (_meshFilter != null && _meshFilter.sharedMesh != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) DestroyImmediate(_meshFilter.sharedMesh);
                else Destroy(_meshFilter.sharedMesh);
#else
                Destroy(_meshFilter.sharedMesh);
#endif
                _meshFilter.sharedMesh = null;
            }

            if (_material != null)
            {
                Destroy(_material);
                _material = null;
            }

            if (_gridMaterial != null)
            {
                Destroy(_gridMaterial);
                _gridMaterial = null;
            }
        }

        private void EnsureComponents()
        {
            _meshFilter = GetComponent<MeshFilter>();
            _meshRenderer = GetComponent<MeshRenderer>();
            if (_meshFilter == null) _meshFilter = gameObject.AddComponent<MeshFilter>();
            if (_meshRenderer == null) _meshRenderer = gameObject.AddComponent<MeshRenderer>();

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit")
                           ?? Shader.Find("Sprites/Default")
                           ?? Shader.Find("Unlit/Color");
            if (shader != null && _material == null)
            {
                _material = new Material(shader);
                SetupTransparent(_material);
                _material.color = _idleColor;
                _meshRenderer.material = _material;
            }

            _meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _meshRenderer.receiveShadows = false;

            if (_gridMaterial == null && shader != null)
            {
                _gridMaterial = new Material(shader);
                SetupTransparent(_gridMaterial);
                _gridMaterial.color = _gridColor;
            }
        }

        private static void SetupTransparent(Material m)
        {
            if (m == null) return;

            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_BlendMode", 0f);
            m.SetFloat("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_AlphaClip", 0f);
            m.SetShaderPassEnabled("DepthOnly", false);
            m.SetShaderPassEnabled("SHADOWCASTER", false);
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.EnableKeyword("_ALPHABLEND_ON");
        }

        private void BuildDomeMesh()
        {
            int lonSeg = Mathf.Clamp(_longitudeSegments, 8, 96);
            int latSeg = Mathf.Clamp(_latitudeSegments, 4, 48);
            float radius = Mathf.Max(1000f, _detectionRadiusM);

            int ringVerts = lonSeg + 1;
            int domeVerts = (latSeg + 1) * ringVerts;
            int baseCenterIndex = domeVerts;
            int totalVerts = domeVerts + ringVerts + 1;

            Vector3[] verts = new Vector3[totalVerts];
            Vector3[] normals = new Vector3[totalVerts];
            Vector2[] uvs = new Vector2[totalVerts];

            int v = 0;
            for (int lat = 0; lat <= latSeg; lat++)
            {
                float tLat = lat / (float)latSeg;
                float theta = tLat * Mathf.PI * 0.5f;
                float y = Mathf.Cos(theta) * radius;
                float ringR = Mathf.Sin(theta) * radius;

                for (int lon = 0; lon <= lonSeg; lon++)
                {
                    float tLon = lon / (float)lonSeg;
                    float phi = tLon * Mathf.PI * 2f;
                    float x = Mathf.Cos(phi) * ringR;
                    float z = Mathf.Sin(phi) * ringR;

                    Vector3 p = new Vector3(x, y, z);
                    verts[v] = p;
                    normals[v] = p.normalized;
                    uvs[v] = new Vector2(tLon, 1f - tLat);
                    v++;
                }
            }

            verts[baseCenterIndex] = Vector3.zero;
            normals[baseCenterIndex] = Vector3.down;
            uvs[baseCenterIndex] = new Vector2(0.5f, 0.5f);

            int baseStart = baseCenterIndex + 1;
            for (int i = 0; i <= lonSeg; i++)
            {
                float tLon = i / (float)lonSeg;
                float phi = tLon * Mathf.PI * 2f;
                float x = Mathf.Cos(phi) * radius;
                float z = Mathf.Sin(phi) * radius;

                verts[baseStart + i] = new Vector3(x, 0f, z);
                normals[baseStart + i] = Vector3.down;
                uvs[baseStart + i] = new Vector2((x / radius + 1f) * 0.5f, (z / radius + 1f) * 0.5f);
            }

            int domeTriCount = latSeg * lonSeg * 2;
            int baseTriCount = lonSeg;
            int[] tris = new int[(domeTriCount + baseTriCount) * 3];
            int ti = 0;

            for (int lat = 0; lat < latSeg; lat++)
            {
                int row = lat * ringVerts;
                int nextRow = (lat + 1) * ringVerts;
                for (int lon = 0; lon < lonSeg; lon++)
                {
                    int a = row + lon;
                    int b = row + lon + 1;
                    int c = nextRow + lon;
                    int d = nextRow + lon + 1;

                    tris[ti++] = a;
                    tris[ti++] = c;
                    tris[ti++] = b;

                    tris[ti++] = b;
                    tris[ti++] = c;
                    tris[ti++] = d;
                }
            }

            for (int lon = 0; lon < lonSeg; lon++)
            {
                int a = baseStart + lon;
                int b = baseStart + lon + 1;

                tris[ti++] = baseCenterIndex;
                tris[ti++] = b;
                tris[ti++] = a;
            }

            var mesh = new Mesh { name = "GroundRadarDome" };
            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.RecalculateBounds();

            if (_meshFilter.sharedMesh != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) DestroyImmediate(_meshFilter.sharedMesh);
                else Destroy(_meshFilter.sharedMesh);
#else
                Destroy(_meshFilter.sharedMesh);
#endif
            }
            _meshFilter.sharedMesh = mesh;
        }

        private void TryBindAircraftSimulator()
        {
            if (_aircraftSimulator != null) return;

            if (AppManager.Instance != null)
            {
                _aircraftSimulator = AppManager.Instance.GetAircraftSimulator();
            }

            if (_aircraftSimulator == null)
            {
                _aircraftSimulator = FindFirstObjectByType<AircraftSimulator>();
            }
        }

        private void BuildGridLines()
        {
            ClearGridLines();
            if (!_showGrid) return;

            if (_gridRoot == null)
            {
                var gridGo = new GameObject("DomeGrid");
                gridGo.transform.SetParent(transform, false);
                _gridRoot = gridGo.transform;
            }

            float radius = Mathf.Max(1000f, _detectionRadiusM);
            int pointsPerLine = Mathf.Clamp(_longitudeSegments, 18, 128);
            int parallelCount = Mathf.Clamp(_gridParallelCount, 2, 12);
            int meridianCount = Mathf.Clamp(_gridMeridianCount, 4, 24);

            for (int i = 1; i <= parallelCount; i++)
            {
                float t = i / (float)(parallelCount + 1);
                float theta = t * Mathf.PI * 0.5f;
                float y = Mathf.Cos(theta) * radius;
                float rr = Mathf.Sin(theta) * radius;

                var pts = new Vector3[pointsPerLine + 1];
                for (int p = 0; p <= pointsPerLine; p++)
                {
                    float a = (p / (float)pointsPerLine) * Mathf.PI * 2f;
                    pts[p] = new Vector3(Mathf.Cos(a) * rr, y, Mathf.Sin(a) * rr);
                }

                CreateGridLine($"Parallel_{i}", pts, true);
            }

            int meridianSamples = Mathf.Clamp(_latitudeSegments * 3, 18, 180);
            for (int i = 0; i < meridianCount; i++)
            {
                float phi = (i / (float)meridianCount) * Mathf.PI * 2f;
                var pts = new Vector3[meridianSamples + 1];
                for (int s = 0; s <= meridianSamples; s++)
                {
                    float t = s / (float)meridianSamples;
                    float theta = t * Mathf.PI * 0.5f;
                    float y = Mathf.Cos(theta) * radius;
                    float rr = Mathf.Sin(theta) * radius;
                    pts[s] = new Vector3(Mathf.Cos(phi) * rr, y, Mathf.Sin(phi) * rr);
                }

                CreateGridLine($"Meridian_{i}", pts, false);
            }
        }

        private void CreateGridLine(string lineName, Vector3[] points, bool loop)
        {
            if (_gridRoot == null || points == null || points.Length < 2) return;

            var go = new GameObject(lineName);
            go.transform.SetParent(_gridRoot, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = loop;
            lr.positionCount = points.Length;
            lr.SetPositions(points);
            lr.alignment = LineAlignment.View;
            lr.textureMode = LineTextureMode.Stretch;
            lr.numCornerVertices = 2;
            lr.numCapVertices = 2;
            lr.widthMultiplier = Mathf.Max(1f, _gridLineWidth);
            lr.startWidth = lr.widthMultiplier;
            lr.endWidth = lr.widthMultiplier;
            lr.startColor = _gridColor;
            lr.endColor = _gridColor;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.material = _gridMaterial;
            _gridLines.Add(lr);
        }

        private void ClearGridLines()
        {
            for (int i = 0; i < _gridLines.Count; i++)
            {
                if (_gridLines[i] != null)
                {
#if UNITY_EDITOR
                    if (!Application.isPlaying) DestroyImmediate(_gridLines[i].gameObject);
                    else Destroy(_gridLines[i].gameObject);
#else
                    Destroy(_gridLines[i].gameObject);
#endif
                }
            }
            _gridLines.Clear();

            if (_gridRoot != null && _gridRoot.childCount == 0)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) DestroyImmediate(_gridRoot.gameObject);
                else Destroy(_gridRoot.gameObject);
#else
                Destroy(_gridRoot.gameObject);
#endif
                _gridRoot = null;
            }
        }

        private void TryInitAnchor()
        {
            if (_anchorInitialized) return;

            if (_useCesiumAnchor)
            {
                if (_globeAnchor == null)
                {
                    _globeAnchor = GetComponent<CesiumGlobeAnchor>();
                }
                if (_globeAnchor == null)
                {
                    _globeAnchor = gameObject.AddComponent<CesiumGlobeAnchor>();
                }

                if (_globeAnchor != null)
                {
                    _anchorInitialized = TrySetAnchorLlh(_globeAnchor, _radarLongitude, _radarLatitude, 0.0);
                    if (_showLog && !_anchorInitialized)
                    {
                        Debug.LogWarning("[GroundRadarDomeController] Cesium 锚点设置失败，雷达阵地可能位置不正确。");
                    }
                }
            }
            else
            {
                transform.position = Vector3.zero;
                _anchorInitialized = true;
            }
        }

        private bool IsAircraftInsideDome()
        {
            if (_aircraftSimulator == null) return false;

            _aircraftSimulator.GetCurrentPosition(out double aircraftLat, out double aircraftLon, out float aircraftAltM);

            double latRad = _radarLatitude * Mathf.Deg2Rad;
            double metersNorth = (aircraftLat - _radarLatitude) * 111320.0;
            double metersEast = (aircraftLon - _radarLongitude) * 111320.0 * Math.Max(0.1, Math.Cos(latRad));
            double horizontalDist = Math.Sqrt(metersNorth * metersNorth + metersEast * metersEast);

            float radius = Mathf.Max(1000f, _detectionRadiusM);
            float ceiling = Mathf.Max(100f, _detectionCeilingM);
            bool belowCeiling = aircraftAltM <= ceiling;
            bool insideHemisphere = (horizontalDist * horizontalDist + aircraftAltM * aircraftAltM) <= (radius * radius);
            return belowCeiling && insideHemisphere;
        }

        private void ApplyColor(Color color)
        {
            if (_material != null)
            {
                _material.color = color;
            }

            if (_gridMaterial != null)
            {
                // 网格保持更高可见度，但跟随告警态变色。
                Color gridBlend = Color.Lerp(_gridColor, new Color(_alertColor.r, _alertColor.g, _alertColor.b, Mathf.Max(_gridColor.a, _alertColor.a)),
                    Mathf.InverseLerp(_idleColor.r + _idleColor.g + _idleColor.b, _alertColor.r + _alertColor.g + _alertColor.b, color.r + color.g + color.b));
                _gridMaterial.color = gridBlend;
            }
        }

        private static bool TrySetAnchorLlh(CesiumGlobeAnchor anchor, double lon, double lat, double height)
        {
            if (anchor == null) return false;

            Type t = anchor.GetType();

            var llhProp = t.GetProperty("longitudeLatitudeHeight");
            if (llhProp != null)
            {
                llhProp.SetValue(anchor, new double3(lon, lat, height));
                return true;
            }

            var llhMethod = t.GetMethod("SetPositionLongitudeLatitudeHeight", new[] { typeof(double), typeof(double), typeof(double) });
            if (llhMethod != null)
            {
                llhMethod.Invoke(anchor, new object[] { lon, lat, height });
                return true;
            }

            return false;
        }

#if UNITY_EDITOR
        private void OnValidate()
        {
            if (!isActiveAndEnabled) return;
            EnsureComponents();
            BuildDomeMesh();
            BuildGridLines();
            _currentColor = _idleColor;
            ApplyColor(_currentColor);
        }
#endif
    }
}
