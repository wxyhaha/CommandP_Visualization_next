using System;
using System.Collections;
using System.Collections.Generic;
using CommandP.Core;
using CommandP.Data.DTOs;
using CommandP.Data.Stores;
using CesiumForUnity;
using UnityEngine;
using Unity.Mathematics;
using UnityEngine.Rendering;

namespace CommandP.Core
{
    public class UnitViewManager : MonoBehaviour
    {
        [Header("Prefab")]
        [SerializeField] private GameObject _defaultUnitPrefab;
        [SerializeField] private GameObject _aircraftUnitPrefab;
        [SerializeField] private GameObject _satelliteUnitPrefab;
        [SerializeField] private float _aircraftUniformScale = 1f;
        [SerializeField] private float _satelliteUniformScale = 1f;
        [SerializeField] private Vector3 _aircraftRotationOffsetEuler = Vector3.zero;
        [SerializeField] private Vector3 _satelliteRotationOffsetEuler = Vector3.zero;
        [SerializeField] private string _aircraftModelPath = "Models/Aircraft/MyAircraft";  // Resources 中的路径
        [SerializeField] private string _satelliteModelPath = "Models/Aircraft/satellite";

        [Header("Smoothing")]
        [SerializeField] private float _positionLerpSpeed = 8f;
        [SerializeField] private float _rotationLerpSpeed = 10f;
        [SerializeField] private bool _enableAircraftPathSmoothing = true;
        [SerializeField] private int _aircraftInterpolationSteps = 10;
        [SerializeField] private float _aircraftCruiseSpeed = 300f;
        [SerializeField] private float _pathArriveThreshold = 5f;

        [Header("Visibility")]
        [SerializeField] private float _fallbackPrimitiveSize = 250f;
        [SerializeField] private KeyCode _focusHotkey = KeyCode.F;
        [SerializeField] private float _aircraftTrailTimeSeconds = 1200f;
        [SerializeField] private float _aircraftTrailWidth = 120f;
        [SerializeField] private float _aircraftTrailMinVertexDistance = 2f;

        [Header("Aircraft Spherical Sector Radar")]
        [SerializeField] private bool _enableAircraftRadar = true;
        [SerializeField] private float _radarRadius = 300f;
        [SerializeField] private float _radarHalfAngleDeg = 85f;
        [SerializeField] private int _radarLongitudeLines = 12;
        [SerializeField] private int _radarLatitudeLines = 6;
        [SerializeField] private int _radarMeshSegments = 32;
        [SerializeField] private Color _radarFillColor = new Color(1f, 0.3f, 0.05f, 0.10f);
        [SerializeField] private float _radarFillBrightCenter = 0.8f;
        [SerializeField] private float _radarGridLineWidth = 2.5f;
        [SerializeField] private Color _radarGridColor = new Color(1f, 0.4f, 0.1f, 0.35f);
        [SerializeField] private float _radarSweepArcWidth = 8f;
        [SerializeField] private float _radarSweepPeriodSec = 2f;
        [SerializeField] private Color _radarSweepColor = new Color(1f, 0.5f, 0.1f, 0.95f);
        [SerializeField] private Color _radarSweepGlowColor = new Color(1f, 1f, 0.6f, 1f);
        [SerializeField] private int _radarSweepArcSegments = 8;
        [SerializeField] private float _radarBreathSpeed = 0.7f;
        [SerializeField] private float _radarBreathAlphaMin = 0.06f;
        [SerializeField] private float _radarBreathAlphaMax = 0.35f;

        [SerializeField] private float _satelliteFallbackSize = 1800f;
        [SerializeField] private float _satelliteTrailTimeSeconds = 3600f;
        [SerializeField] private float _satelliteTrailWidth = 1200f;
        [SerializeField] private float _satelliteTrailMinVertexDistance = 200f;
        [SerializeField] private float _satelliteOrbitLineWidth = 1200f;
        [SerializeField] private float _satelliteOrbitWidthReferenceDistance = 10000f;
        [SerializeField] private float _satelliteOrbitMaxDistanceMultiplier = 1.8f;
        [SerializeField] private float _satelliteOrbitMinScreenPixels = 2.5f;
        [SerializeField] private float _satelliteOrbitDashTiling = 10f;
        [SerializeField] private int _satelliteOrbitLineSegments = 720;
        [SerializeField] private Color _satelliteOrbitLineColor = new Color(0.2f, 0.95f, 1f, 0.7f);
        [Header("Satellite-Aircraft Communication Line")]
        [SerializeField] private bool _enableCommunicationLine = true;
        [SerializeField] private Color _communicationLineColor = new Color(0f, 1f, 0.2f, 1f);
        [SerializeField] private float _communicationLineScreenWidth = 80f;
        [Header("Satellite Ground Probe")]
        [SerializeField] private bool _enableSatelliteGroundProbe = true;
        [SerializeField] private float _satelliteGroundProbeLengthMultiplier = 1f;
        [SerializeField] private float _satelliteGroundProbeMaxLength = 500000f;
        [SerializeField, Range(5f, 85f)] private float _satelliteGroundProbeHalfAngleDeg = 18f;
        [SerializeField, Range(12, 64)] private int _satelliteGroundProbeSides = 32;
        [SerializeField] private Color _satelliteGroundProbeColor = new Color(0.12f, 0.9f, 0.85f, 0.22f);
        [SerializeField, Min(0f)] private float _satelliteGroundProbeEmissionIntensity = 2.4f;
        [SerializeField, Range(0f, 1f)] private float _satelliteGroundProbeAlpha = 0.22f;
        [SerializeField, Min(0.01f)] private float _satelliteGroundProbeDurationSec = 2.2f;
        [SerializeField, Range(1, 200)] private int _satelliteGroundProbeRepeat = 14;
        [SerializeField, Range(0f, 1f)] private float _satelliteGroundProbeThickness = 0.22f;
        [SerializeField] private float _satelliteGroundProbeOffset = 0f;
        [SerializeField] private float _trailFlowSpeed = 0.8f;
        [SerializeField] private float _facilityIconSize = 220f;
        [SerializeField] private float _facilityLabelHeight = 260f;
        [SerializeField] private float _facilityLabelGroundOffsetMeters = 20f;
        [SerializeField] private float _facilityCoverageLineWidth = 3.5f;
        [SerializeField] private int _facilityCoverageSegments = 96;
        [SerializeField] private int _facilityCoverageParallels = 8;
        [SerializeField] private int _facilityCoverageMeridians = 16;
        [SerializeField] private float _facilityCoverageVerticalOffsetMeters = 2f;
        [SerializeField] private Color _facilityCoverageColor = new Color(0.58f, 1f, 0.72f, 0.95f);
        [SerializeField] private Color _facilityCoverageFillColor = new Color(0.52f, 1f, 0.70f, 0.18f);

        [Header("Geo Mapping")]
        [SerializeField] private bool _useCesiumGeoreference = true;
        [SerializeField] private CesiumGeoreference _cesiumGeoreference;
        [SerializeField] private bool _addCesiumGlobeAnchor = true;
        [SerializeField] private bool _autoSetOriginFromFirstUnit = true;
        [SerializeField] private double _originLatitude = 0;
        [SerializeField] private double _originLongitude = 0;
        [SerializeField] private float _metersToUnityScale = 0.001f;
        [SerializeField] private float _altitudeScale = 0.001f;

        [Header("Debug")]
        [SerializeField] private bool _logLifecycle = false;
        [SerializeField] private bool _autoCreateAppManager = true;

        [Header("Camera Focus")]
        [SerializeField] private Camera _targetCamera;
        [SerializeField] private bool _autoFocusOnFirstBatch = true;
        [SerializeField] private int _minUnitsForAutoFocus = 1;
        [SerializeField] private bool _preferCesiumGeoFocus = true;
        [SerializeField] private bool _allowAutoAddCameraGlobeAnchor = false;
        [SerializeField] private float _focusHeightPadding = 2000f;
        [SerializeField] private float _focusDistancePadding = 1200f;
        [SerializeField] private float _focusPitchDeg = 45f;
        [SerializeField] private Vector3 _cameraFollowViewOffset = new Vector3(0f, 2000f, 1200f);

        private readonly Dictionary<string, UnitViewState> _views = new Dictionary<string, UnitViewState>();
        private const string AircraftVisualRootName = "AircraftVisualOffsetRoot";
        private const string SatelliteGroundProbeRootName = "SatelliteGroundProbeRoot";

        private AppManager _appManager;
        private UnitStore _unitStore;
        private SatelliteSimulator _satelliteSimulator;
        private bool _originInitialized;
        private bool _warnedCesiumMissing;
        private bool _warnedCesiumApiMissing;
        private bool _hasAutoFocused;
        private string _cameraFollowObjectId;
        private bool _cameraFollowEnabled;
        private Material _sharedTrailMaterial;
        private float _trailFlowOffset;
        private LineRenderer _communicationLine;
        private bool _commLineInited;
        private Material _sharedCoverageMaterial;
        private Material _sharedCoverageFillMaterial;
        private Material _sharedSatelliteOrbitMaterial;

        private class UnitViewState
        {
            public struct GeoPoint
            {
                public double Longitude;
                public double Latitude;
                public double Height;

                public GeoPoint(double longitude, double latitude, double height)
                {
                    Longitude = longitude;
                    Latitude = latitude;
                    Height = height;
                }
            }

            public string ObjectId;
            public GameObject ViewObject;
            public CesiumGlobeAnchor GlobeAnchor;
            public TrailRenderer TrailRenderer;
            public LineRenderer SatelliteOrbitLine;
            public GameObject SatelliteOrbitRoot;
            public GameObject SatelliteGroundProbeRoot;
            public SatelliteGroundProbeEffect SatelliteGroundProbeEffect;
            public Vector3 TargetPosition;
            public double TargetLongitude;
            public double TargetLatitude;
            public double TargetHeight;
            public float TargetSpeedKnots;
            public float TargetHeading;
            public bool IsInitialized;
            public bool IsAircraft;
            public bool IsSatellite;
            public bool IsFacility;
            public TextMesh FacilityLabel;
            public GameObject FacilityCoverageRoot;
            public float FacilityMaxSensorRangeNm;
            public bool HasCurrentGeo;
            public double CurrentLongitude;
            public double CurrentLatitude;
            public double CurrentHeight;
            public Queue<GeoPoint> InterpolatedGeoPath = new Queue<GeoPoint>();
        }

        private IEnumerator Start()
        {
            if (_logLifecycle)
            {
                Debug.Log("[UnitViewManager] Start");
            }

            float waited = 0f;
            while (AppManager.Instance == null && waited < 2f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            _appManager = AppManager.Instance;

            if (_appManager == null)
            {
                _appManager = FindFirstObjectByType<AppManager>();
            }

            if (_appManager == null && _autoCreateAppManager)
            {
                var go = new GameObject("AppManager(Auto)");
                _appManager = go.AddComponent<AppManager>();
                Debug.LogWarning("[UnitViewManager] AppManager not found, auto-created one.");
                yield return null;
            }

            if (_appManager == null)
            {
                Debug.LogError("[UnitViewManager] AppManager is missing. Please add AppManager to scene.");
                yield break;
            }

            float storeWait = 0f;
            while (_appManager.GetUnitStore() == null && storeWait < 2f)
            {
                storeWait += Time.unscaledDeltaTime;
                yield return null;
            }

            _unitStore = _appManager.GetUnitStore();
            _satelliteSimulator = _appManager.GetSatelliteSimulator();

            if (_useCesiumGeoreference && _cesiumGeoreference == null)
            {
                _cesiumGeoreference = FindFirstObjectByType<CesiumGeoreference>();
            }

            // 强制覆盖雷达参数为期望值（忽略场景中过时的序列化值）
            _radarRadius = 300f;
            _radarHalfAngleDeg = 30f;

            if (_targetCamera == null)
            {
                _targetCamera = Camera.main;
            }

            if (_targetCamera == null)
            {
                _targetCamera = FindFirstObjectByType<Camera>();
            }

            if (_unitStore == null)
            {
                Debug.LogError("[UnitViewManager] UnitStore is null.");
                yield break;
            }

            Subscribe();
            BootstrapExistingUnits();
        }

        private void OnDestroy()
        {
            Unsubscribe();
        }

        private void Update()
        {
            if (Input.GetKeyDown(_focusHotkey))
            {
                FocusCameraToUnitsRegion();
            }

            // 卫星-飞机通信线（带 LOS 遮挡检测）
            if (_enableCommunicationLine)
            {
                TryInitCommunicationLine();
                UpdateCommunicationLine();
            }

            if (_views.Count == 0)
            {
                return;
            }

            float posT = Mathf.Clamp01(Time.deltaTime * _positionLerpSpeed);
            float rotT = Mathf.Clamp01(Time.deltaTime * _rotationLerpSpeed);

            foreach (var kv in _views)
            {
                UnitViewState state = kv.Value;
                if (state == null || state.ViewObject == null)
                {
                    continue;
                }

                Transform tr = state.ViewObject.transform;

                bool drivenByAnchor = false;
                if (state.IsAircraft && _enableAircraftPathSmoothing)
                {
                    AdvanceAircraftAlongPath(state, Time.deltaTime);
                    drivenByAnchor = state.GlobeAnchor != null;
                }
                else
                {
                    drivenByAnchor = TrySetAnchorLongitudeLatitudeHeight(state);
                    if (!drivenByAnchor && !state.IsInitialized)
                    {
                        tr.position = state.TargetPosition;
                        state.IsInitialized = true;
                    }
                    else if (!drivenByAnchor)
                    {
                        tr.position = Vector3.Lerp(tr.position, state.TargetPosition, posT);
                    }
                }

                Vector3 forward = HeadingToForward(state.TargetHeading);
                if (!state.IsFacility && forward.sqrMagnitude > 0.0001f)
                {
                    Quaternion targetRot = Quaternion.LookRotation(forward, Vector3.up);
                    tr.rotation = Quaternion.Slerp(tr.rotation, targetRot, rotT);
                }

                if (state.IsFacility)
                {
                    UpdateFacilityBillboard(state);
                }

                if (state.IsSatellite)
                {
                    UpdateSatelliteOrbitLineWidth(state);
                }

                // 根据相机距离动态调整轨迹线宽度，保证远处也能看到
                UpdateTrailWidthByDistance(state);
            }

            if (_sharedTrailMaterial != null)
            {
                _trailFlowOffset -= Time.deltaTime * Mathf.Max(0.05f, _trailFlowSpeed);
                _sharedTrailMaterial.mainTextureOffset = new Vector2(_trailFlowOffset, 0f);
            }

            if (!TryUpdateCameraFollow())
            {
                TryAutoFocusCamera();
            }
        }

        public Vector3 GetCameraFollowViewOffset()
        {
            return _cameraFollowViewOffset;
        }

        public void SetCameraFollowViewOffset(Vector3 offset)
        {
            _cameraFollowViewOffset = offset;
            if (_cameraFollowEnabled)
            {
                TryUpdateCameraFollow();
            }
        }

        public void ResetCameraFollowViewOffset()
        {
            SetCameraFollowViewOffset(new Vector3(0f, 2000f, 1200f));
        }

        public bool TryGetViewObject(string objectId, out GameObject viewObject)
        {
            viewObject = null;

            if (string.IsNullOrEmpty(objectId))
            {
                return false;
            }

            if (!_views.TryGetValue(objectId, out UnitViewState state) || state == null || state.ViewObject == null)
            {
                return false;
            }

            viewObject = state.ViewObject;
            return true;
        }

        private void Subscribe()
        {
            _unitStore.OnUnitAdded += HandleUnitAdded;
            _unitStore.OnUnitUpdated += HandleUnitUpdated;
            _unitStore.OnUnitDestroyed += HandleUnitDestroyed;
        }

        private void Unsubscribe()
        {
            if (_unitStore == null)
            {
                return;
            }

            _unitStore.OnUnitAdded -= HandleUnitAdded;
            _unitStore.OnUnitUpdated -= HandleUnitUpdated;
            _unitStore.OnUnitDestroyed -= HandleUnitDestroyed;
        }

        private void BootstrapExistingUnits()
        {
            Dictionary<string, CachedUnit> allUnits = _unitStore.GetAllUnits();
            foreach (var kv in allUnits)
            {
                HandleUnitAdded(kv.Key, kv.Value);
            }

            TryAutoFocusCamera();
        }

        // ============ 卫星-飞机通信线（带 LOS 遮挡检测） ============

        private void TryInitCommunicationLine()
        {
            if (_commLineInited)
                return;

            // 等到场景中至少有一个 view 再创建
            if (_views.Count == 0)
                return;

            var lineGo = new GameObject("SatelliteAircraftCommLine");
            lineGo.transform.SetParent(transform, false);

            _communicationLine = lineGo.AddComponent<LineRenderer>();
            _communicationLine.useWorldSpace = true;
            _communicationLine.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _communicationLine.receiveShadows = false;
            _communicationLine.alignment = LineAlignment.View;
            _communicationLine.numCapVertices = 8;
            _communicationLine.numCornerVertices = 8;
            _communicationLine.positionCount = 0;

            // 着色器：URP Unlit 优先
            Shader s = Shader.Find("Universal Render Pipeline/Unlit");
            if (s == null || !s.isSupported)
                s = Shader.Find("Unlit/Color");
            if (s == null || !s.isSupported)
                s = Shader.Find("Sprites/Default");

            Material mat = new Material(s);
            mat.color = _communicationLineColor;
            mat.renderQueue = (int)RenderQueue.Overlay;
            _communicationLine.material = mat;
            _communicationLine.startColor = _communicationLineColor;
            _communicationLine.endColor = _communicationLineColor;

            // 屏幕空间宽度
            _communicationLine.widthMultiplier = Mathf.Max(1f, _communicationLineScreenWidth);
            _communicationLine.startWidth = 1f;
            _communicationLine.endWidth = 1f;

            _commLineInited = true;
        }

        private void UpdateCommunicationLine()
        {
            if (!_commLineInited || _communicationLine == null)
                return;

            // 在 _views 中查找飞机和卫星
            UnitViewState aircraft = null;
            UnitViewState satellite = null;

            foreach (var kv in _views)
            {
                UnitViewState s = kv.Value;
                if (s?.ViewObject == null)
                    continue;

                if (s.IsAircraft && aircraft == null)
                    aircraft = s;
                if (s.IsSatellite && satellite == null)
                    satellite = s;

                if (aircraft != null && satellite != null)
                    break;
            }

            if (aircraft == null || satellite == null)
            {
                _communicationLine.positionCount = 0;
                return;
            }

            // LOS 检测：仅当能明确判定遮挡时才清线
            bool losClear = true;
            if (_unitStore != null)
            {
                var allUnits = _unitStore.GetAllUnits();
                if (allUnits.TryGetValue(satellite.ObjectId, out var satUnit) &&
                    allUnits.TryGetValue(aircraft.ObjectId, out var acUnit) &&
                    satUnit.Position != null && acUnit.Position != null)
                {
                    double3 satEcef = LlhToEcefWgs84(
                        satUnit.Position.Longitude,
                        satUnit.Position.Latitude,
                        satUnit.Position.Altitude);
                    double3 acEcef = LlhToEcefWgs84(
                        acUnit.Position.Longitude,
                        acUnit.Position.Latitude,
                        acUnit.Position.Altitude);

                    losClear = SatelliteSimulator.HasLineOfSight(satEcef, acEcef);
                }
            }

            if (!losClear)
            {
                _communicationLine.positionCount = 0;
                return;
            }

            // 用 transform.position 画线（CesiumGlobeAnchor 确保位置正确）
            _communicationLine.positionCount = 2;
            _communicationLine.SetPosition(0, aircraft.ViewObject.transform.position);
            _communicationLine.SetPosition(1, satellite.ViewObject.transform.position);
        }

        private void HandleUnitAdded(string objectId, CachedUnit unit)
        {
            if (string.IsNullOrEmpty(objectId) || unit == null || unit.Position == null)
            {
                return;
            }

            UnitViewState state;
            if (!_views.TryGetValue(objectId, out state) || state.ViewObject == null)
            {
                state = new UnitViewState
                {
                    ObjectId = objectId,
                    ViewObject = CreateUnitObject(unit),
                    IsInitialized = false
                };
                state.IsAircraft = IsAircraftUnit(unit);
                state.IsSatellite = IsSatelliteUnit(unit);
                state.IsFacility = IsFacilityUnit(unit);
                state.GlobeAnchor = state.ViewObject != null ? state.ViewObject.GetComponent<CesiumGlobeAnchor>() : null;
                state.TrailRenderer = state.ViewObject != null ? state.ViewObject.GetComponent<TrailRenderer>() : null;
                _views[objectId] = state;

                if (_logLifecycle)
                {
                    Debug.Log($"[UnitViewManager] Created view: {unit.Name} ({objectId})");
                }
            }

            UpdateTarget(state, unit);

            if (state.IsAircraft)
            {
                EnsureAircraftTrail(state);
            }

            if (state.IsFacility)
            {
                EnsureFacilityVisualization(state, unit);
            }

            if (state.IsSatellite)
            {
                EnsureSatelliteOrbitVisualization(state);
                EnsureSatelliteGroundProbeVisualization(state, unit);
            }

            TryAutoFocusCamera();
        }

        private void HandleUnitUpdated(string objectId, CachedUnit unit)
        {
            if (string.IsNullOrEmpty(objectId) || unit == null || unit.Position == null)
            {
                return;
            }

            if (!_views.TryGetValue(objectId, out UnitViewState state))
            {
                HandleUnitAdded(objectId, unit);
                return;
            }

            UpdateTarget(state, unit);
        }

        private void HandleUnitDestroyed(string objectId)
        {
            if (!_views.TryGetValue(objectId, out UnitViewState state))
            {
                return;
            }

            if (state.ViewObject != null)
            {
                Destroy(state.ViewObject);
            }

            if (state.SatelliteOrbitRoot != null)
            {
                Destroy(state.SatelliteOrbitRoot);
            }

            if (state.SatelliteGroundProbeRoot != null)
            {
                Destroy(state.SatelliteGroundProbeRoot);
            }

            _views.Remove(objectId);

            if (_cameraFollowEnabled && string.Equals(_cameraFollowObjectId, objectId, StringComparison.Ordinal))
            {
                ClearCameraFollow();
            }

            if (_logLifecycle)
            {
                Debug.Log($"[UnitViewManager] Destroyed view: {objectId}");
            }
        }

        private void UpdateTarget(UnitViewState state, CachedUnit unit)
        {
            Position pos = unit.Position;
            state.TargetLongitude = pos.Longitude;
            state.TargetLatitude = pos.Latitude;
            state.TargetHeight = pos.Altitude;
            state.TargetPosition = GeoToScenePosition(pos);
            if (!IsFinite(state.TargetPosition))
            {
                return;
            }

            if (!state.HasCurrentGeo)
            {
                state.CurrentLongitude = state.TargetLongitude;
                state.CurrentLatitude = state.TargetLatitude;
                state.CurrentHeight = state.TargetHeight;
                state.HasCurrentGeo = true;
            }

            if (state.IsAircraft && _enableAircraftPathSmoothing)
            {
                RebuildInterpolatedPath(state);
            }

            if (unit.Movement != null)
            {
                state.TargetSpeedKnots = unit.Movement.Speed;
            }

            state.TargetHeading = unit.Movement != null ? unit.Movement.Heading : state.TargetHeading;

            if (state.ViewObject != null)
            {
                state.ViewObject.name = $"Unit_{unit.Type}_{unit.Name}_{unit.ObjectID}";
            }

            if (state.IsFacility)
            {
                EnsureFacilityVisualization(state, unit);
            }

            if (state.IsSatellite)
            {
                EnsureSatelliteOrbitVisualization(state);
                EnsureSatelliteGroundProbeVisualization(state, unit);
            }
        }

        private GameObject CreateUnitObject(CachedUnit unit)
        {
            Transform unitParent = GetUnitParentTransform();

            GameObject go;
            
            // 飞机优先使用自定义模型
            if (IsAircraftUnit(unit) && _aircraftUnitPrefab != null)
            {
                go = Instantiate(_aircraftUnitPrefab, unitParent);
                ApplyAircraftUniformScale(go);
                ApplyAircraftModelRotationOffset(go);
            }
            else if (IsAircraftUnit(unit) && !string.IsNullOrEmpty(_aircraftModelPath))
            {
                string aircraftModelPath = NormalizeResourcesPath(_aircraftModelPath);
                GameObject modelPrefab = Resources.Load<GameObject>(aircraftModelPath);
                if (modelPrefab != null)
                {
                    go = Instantiate(modelPrefab, unitParent);
                    ApplyAircraftUniformScale(go);
                    ApplyAircraftModelRotationOffset(go);
                }
                else
                {
                    Debug.LogWarning($"[UnitViewManager] Aircraft model not found at '{_aircraftModelPath}' (normalized: '{aircraftModelPath}'), using default Capsule. You can assign _aircraftUnitPrefab directly in the Inspector.");
                    go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                    go.transform.SetParent(unitParent, worldPositionStays: false);
                    go.transform.localScale = new Vector3(_fallbackPrimitiveSize * 0.5f, _fallbackPrimitiveSize, _fallbackPrimitiveSize * 0.5f);
                }
            }
            else if (IsSatelliteUnit(unit) && _satelliteUnitPrefab != null)
            {
                go = Instantiate(_satelliteUnitPrefab, unitParent);
                ApplySatelliteUniformScale(go);
                ApplySatelliteModelRotationOffset(go);
            }
            else if (IsSatelliteUnit(unit) && !string.IsNullOrEmpty(_satelliteModelPath))
            {
                string satelliteModelPath = NormalizeResourcesPath(_satelliteModelPath);
                GameObject modelPrefab = Resources.Load<GameObject>(satelliteModelPath);
                if (modelPrefab != null)
                {
                    go = Instantiate(modelPrefab, unitParent);
                    ApplySatelliteUniformScale(go);
                    ApplySatelliteModelRotationOffset(go);
                    Debug.Log($"[UnitViewManager] Loaded satellite model from Resources path '{satelliteModelPath}'.");
                }
                else
                {
                    Debug.LogWarning($"[UnitViewManager] Satellite model not found at '{_satelliteModelPath}' (normalized: '{satelliteModelPath}'). If your asset is stored under Resources/Models/Aircraft/satellite.glb, use 'Models/Aircraft/satellite'. Falling back to default Sphere. You can also assign _satelliteUnitPrefab directly in the Inspector.");
                    go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    go.transform.SetParent(unitParent, worldPositionStays: false);
                    go.transform.localScale = new Vector3(_satelliteFallbackSize, _satelliteFallbackSize, _satelliteFallbackSize);
                }
            }
            else if (IsSatelliteUnit(unit) && _defaultUnitPrefab != null)
            {
                go = Instantiate(_defaultUnitPrefab, unitParent);
                ApplySatelliteUniformScale(go);
                ApplySatelliteModelRotationOffset(go);
            }
            else if (IsSatelliteUnit(unit))
            {
                go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                go.transform.SetParent(unitParent, worldPositionStays: false);
                float satelliteSize = Mathf.Max(80f, _satelliteFallbackSize);
                go.transform.localScale = new Vector3(satelliteSize, satelliteSize, satelliteSize);
            }
            else if (_defaultUnitPrefab != null)
            {
                go = Instantiate(_defaultUnitPrefab, unitParent);
            }
            else
            {
                go = IsFacilityUnit(unit) ? GameObject.CreatePrimitive(PrimitiveType.Cube) : GameObject.CreatePrimitive(PrimitiveType.Capsule);
                go.transform.SetParent(unitParent, worldPositionStays: false);
                if (IsFacilityUnit(unit))
                {
                    go.transform.localScale = new Vector3(_facilityIconSize, _facilityIconSize * 0.65f, _facilityIconSize);
                }
                else
                {
                    go.transform.localScale = new Vector3(_fallbackPrimitiveSize * 0.5f, _fallbackPrimitiveSize, _fallbackPrimitiveSize * 0.5f);
                }
            }

            // All unit views, including aircraft, should be anchored in Cesium space.
            EnsureGlobeAnchor(go);

            if (IsAircraftUnit(unit))
            {
                EnsureAircraftRadarEffect(go);
            }

            if (IsSatelliteUnit(unit))
            {
                EnsureSatelliteGroundProbeEffect(go, unit);
            }

            go.name = $"Unit_{unit.Type}_{unit.Name}_{unit.ObjectID}";
            return go;
        }

        private void EnsureAircraftRadarEffect(GameObject go)
        {
            if (go == null) return;

            AircraftRadarScanEffect radar = go.GetComponent<AircraftRadarScanEffect>();
            if (radar == null)
            {
                radar = go.AddComponent<AircraftRadarScanEffect>();
            }

            radar.Configure(
                _enableAircraftRadar,
                _radarRadius,
                _radarHalfAngleDeg,
                _radarLongitudeLines,
                _radarLatitudeLines,
                _radarMeshSegments,
                _radarFillColor,
                _radarFillBrightCenter,
                _radarGridLineWidth,
                _radarGridColor,
                _radarSweepArcWidth,
                _radarSweepPeriodSec,
                _radarSweepColor,
                _radarSweepGlowColor,
                _radarSweepArcSegments,
                _radarBreathSpeed,
                _radarBreathAlphaMin,
                _radarBreathAlphaMax);
        }

        private void ApplyAircraftUniformScale(GameObject go)
        {
            if (go == null)
            {
                return;
            }

            float scale = Mathf.Max(0.0001f, _aircraftUniformScale);
            go.transform.localScale *= scale;
        }

        private void ApplySatelliteUniformScale(GameObject go)
        {
            if (go == null)
            {
                return;
            }

            float scale = Mathf.Max(0.0001f, _satelliteUniformScale);
            go.transform.localScale *= scale;
        }

        private void ApplySatelliteModelRotationOffset(GameObject go)
        {
            if (go == null)
            {
                return;
            }

            Transform visualRoot = go.transform.Find("SatelliteVisualOffsetRoot");
            if (visualRoot == null)
            {
                var rootGo = new GameObject("SatelliteVisualOffsetRoot");
                rootGo.transform.SetParent(go.transform, false);
                visualRoot = rootGo.transform;

                List<Transform> children = new List<Transform>();
                for (int i = 0; i < go.transform.childCount; i++)
                {
                    Transform child = go.transform.GetChild(i);
                    if (child != visualRoot)
                    {
                        children.Add(child);
                    }
                }

                foreach (Transform child in children)
                {
                    child.SetParent(visualRoot, true);
                }
            }

            visualRoot.localPosition = Vector3.zero;
            visualRoot.localRotation = Quaternion.Euler(_satelliteRotationOffsetEuler);
        }

        private void EnsureSatelliteOrbitVisualization(UnitViewState state)
        {
            if (state == null || !state.IsSatellite || state.ViewObject == null)
            {
                return;
            }

            if (_satelliteSimulator == null && _appManager != null)
            {
                _satelliteSimulator = _appManager.GetSatelliteSimulator();
            }

            if (_satelliteSimulator == null)
            {
                return;
            }

            IReadOnlyList<Unity.Mathematics.double3> ringPoints = _satelliteSimulator.GetOrbitRingEcefPoints();
            if (ringPoints == null || ringPoints.Count < 3)
            {
                return;
            }

            if (state.SatelliteOrbitRoot == null)
            {
                var orbitRoot = new GameObject($"SatelliteOrbit_{state.ObjectId}");
                orbitRoot.transform.SetParent(GetUnitParentTransform(), false);
                state.SatelliteOrbitRoot = orbitRoot;
            }

            if (state.SatelliteOrbitLine == null)
            {
                var lineGo = new GameObject("OrbitLine");
                lineGo.transform.SetParent(state.SatelliteOrbitRoot.transform, false);
                state.SatelliteOrbitLine = lineGo.AddComponent<LineRenderer>();
            }

            LineRenderer line = state.SatelliteOrbitLine;
            if (line == null)
            {
                return;
            }

            int sampleCount = Mathf.Clamp(ringPoints.Count, 64, Mathf.Max(64, _satelliteOrbitLineSegments));
            line.useWorldSpace = true;
            line.loop = true;
            line.positionCount = sampleCount;
            line.alignment = LineAlignment.View;
            line.textureMode = LineTextureMode.Tile;
            line.numCornerVertices = 4;
            line.numCapVertices = 2;
            line.widthCurve = AnimationCurve.Constant(0f, 1f, 1f);
            line.material = GetOrCreateSatelliteOrbitMaterial();
            line.startColor = _satelliteOrbitLineColor;
            line.endColor = _satelliteOrbitLineColor;
            line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows = false;

            for (int i = 0; i < sampleCount; i++)
            {
                int sampleIndex = Mathf.FloorToInt((i / (float)sampleCount) * ringPoints.Count) % ringPoints.Count;
                double3 ringPoint = ringPoints[sampleIndex];
                if (TryCesiumTransformEcef(ringPoint, out Vector3 worldPoint))
                {
                    line.SetPosition(i, worldPoint);
                }
            }

            UpdateSatelliteOrbitLineWidth(state);
        }

        private void EnsureSatelliteGroundProbeVisualization(UnitViewState state, CachedUnit unit)
        {
            if (state == null || !state.IsSatellite || state.ViewObject == null || unit == null || unit.Position == null)
            {
                return;
            }

            EnsureSatelliteGroundProbeEffect(state.ViewObject, unit);
        }

        private void EnsureSatelliteGroundProbeEffect(GameObject go, CachedUnit unit)
        {
            if (go == null || unit == null || unit.Position == null)
            {
                return;
            }

            Transform probeRoot = go.transform.Find(SatelliteGroundProbeRootName);
            if (probeRoot == null)
            {
                var rootGo = new GameObject(SatelliteGroundProbeRootName);
                rootGo.transform.SetParent(go.transform, false);
                rootGo.transform.localPosition = Vector3.zero;
                rootGo.transform.localRotation = Quaternion.identity;
                rootGo.transform.localScale = Vector3.one;
                probeRoot = rootGo.transform;
            }

            SatelliteGroundProbeEffect probe = probeRoot.GetComponent<SatelliteGroundProbeEffect>();
            if (probe == null)
            {
                probe = probeRoot.gameObject.AddComponent<SatelliteGroundProbeEffect>();
            }

            float baseLength = Mathf.Max(1000f, unit.Position.Altitude * _satelliteGroundProbeLengthMultiplier);
            baseLength = Mathf.Min(baseLength, Mathf.Max(1000f, _satelliteGroundProbeMaxLength));

            probe.Configure(
                _enableSatelliteGroundProbe,
                baseLength,
                _satelliteGroundProbeHalfAngleDeg,
                _satelliteGroundProbeSides,
                _satelliteGroundProbeColor,
                _satelliteGroundProbeEmissionIntensity,
                _satelliteGroundProbeAlpha,
                _satelliteGroundProbeDurationSec,
                _satelliteGroundProbeRepeat,
                _satelliteGroundProbeThickness,
                _satelliteGroundProbeOffset);

            if (_views.TryGetValue(unit.ObjectID, out UnitViewState state) && state != null)
            {
                state.SatelliteGroundProbeRoot = probeRoot.gameObject;
                state.SatelliteGroundProbeEffect = probe;
            }
        }

        private void UpdateSatelliteOrbitLineWidth(UnitViewState state)
        {
            if (state == null || state.SatelliteOrbitLine == null || state.ViewObject == null)
            {
                return;
            }

            if (_targetCamera == null)
            {
                _targetCamera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            }

            if (_targetCamera == null)
            {
                return;
            }

            float distance = Vector3.Distance(_targetCamera.transform.position, state.ViewObject.transform.position);
            distance = Mathf.Max(1f, distance);

            LineRenderer line = state.SatelliteOrbitLine;
            float baseWidth = Mathf.Max(1f, _satelliteOrbitLineWidth);
            float referenceDistance = Mathf.Max(1f, _satelliteOrbitWidthReferenceDistance);
            float maxDistanceMultiplier = Mathf.Max(1f, _satelliteOrbitMaxDistanceMultiplier);
            float distanceMultiplier = Mathf.Clamp(distance / referenceDistance, 1f, maxDistanceMultiplier);
            float targetWidthByDistance = baseWidth * distanceMultiplier;

            float fovRad = Mathf.Max(1f, _targetCamera.fieldOfView) * Mathf.Deg2Rad;
            float worldUnitsPerPixel = 2f * distance * Mathf.Tan(fovRad * 0.5f) / Mathf.Max(1, Screen.height);
            float minVisibleWidth = worldUnitsPerPixel * Mathf.Max(1f, _satelliteOrbitMinScreenPixels);
            float targetWidth = Mathf.Max(targetWidthByDistance, minVisibleWidth);

            line.widthMultiplier = 1f;
            line.startWidth = targetWidth;
            line.endWidth = targetWidth;
        }

        private bool TryCesiumTransformEcef(double3 ecef, out Vector3 scenePos)
        {
            scenePos = default;

            if (_useCesiumGeoreference && _cesiumGeoreference != null)
            {
                Type georefType = _cesiumGeoreference.GetType();
                var ecefToUnity = georefType.GetMethod("TransformEarthCenteredEarthFixedPositionToUnity", new[] { typeof(double3) });
                if (ecefToUnity != null)
                {
                    object result = ecefToUnity.Invoke(_cesiumGeoreference, new object[] { ecef });
                    if (result is double3 d3)
                    {
                        scenePos = new Vector3((float)d3.x, (float)d3.y, (float)d3.z);
                        return true;
                    }
                }
            }

            scenePos = new Vector3((float)(ecef.x * _metersToUnityScale), (float)(ecef.y * _metersToUnityScale), (float)(ecef.z * _metersToUnityScale));
            return true;
        }

        private Material GetOrCreateSatelliteOrbitMaterial()
        {
            if (_sharedSatelliteOrbitMaterial != null)
            {
                return _sharedSatelliteOrbitMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader == null)
            {
                return null;
            }

            _sharedSatelliteOrbitMaterial = new Material(shader);
            _sharedSatelliteOrbitMaterial.color = _satelliteOrbitLineColor;
            _sharedSatelliteOrbitMaterial.mainTexture = BuildOrbitDashTexture();
            _sharedSatelliteOrbitMaterial.mainTextureScale = new Vector2(Mathf.Max(1f, _satelliteOrbitDashTiling), 1f);
            SetupTransparentMaterial(_sharedSatelliteOrbitMaterial, false);
            return _sharedSatelliteOrbitMaterial;
        }

        private static Texture2D BuildOrbitDashTexture()
        {
            const int width = 64;
            var tex = new Texture2D(width, 1, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };

            for (int x = 0; x < width; x++)
            {
                int idx = x % 16;
                // 10 像素实线 + 6 像素空隙，形成稳定虚线
                float alpha = idx < 10 ? 1f : 0f;
                tex.SetPixel(x, 0, new Color(1f, 1f, 1f, alpha));
            }

            tex.Apply();
            return tex;
        }

        private void ApplyAircraftModelRotationOffset(GameObject go)
        {
            if (go == null)
            {
                return;
            }

            Transform visualRoot = go.transform.Find(AircraftVisualRootName);
            if (visualRoot == null)
            {
                var rootGo = new GameObject(AircraftVisualRootName);
                rootGo.transform.SetParent(go.transform, false);
                visualRoot = rootGo.transform;

                List<Transform> children = new List<Transform>();
                for (int i = 0; i < go.transform.childCount; i++)
                {
                    Transform child = go.transform.GetChild(i);
                    if (child != visualRoot)
                    {
                        children.Add(child);
                    }
                }

                foreach (Transform child in children)
                {
                    child.SetParent(visualRoot, true);
                }
            }

            visualRoot.localPosition = Vector3.zero;
            visualRoot.localRotation = Quaternion.Euler(_aircraftRotationOffsetEuler);
        }

        public Vector3 GetAircraftRotationOffsetEuler()
        {
            return _aircraftRotationOffsetEuler;
        }

        public void SetAircraftRotationOffsetEuler(Vector3 offsetEuler)
        {
            _aircraftRotationOffsetEuler = offsetEuler;
            ApplyAircraftModelRotationOffsetToAllAircraft();
        }

        public void AdjustAircraftRotationOffsetEuler(Vector3 deltaEuler)
        {
            SetAircraftRotationOffsetEuler(_aircraftRotationOffsetEuler + deltaEuler);
        }

        public void ResetAircraftRotationOffsetEuler()
        {
            SetAircraftRotationOffsetEuler(Vector3.zero);
        }

        private void ApplyAircraftModelRotationOffsetToAllAircraft()
        {
            foreach (var view in _views.Values)
            {
                if (view == null || view.ViewObject == null || !view.IsAircraft)
                {
                    continue;
                }

                ApplyAircraftModelRotationOffset(view.ViewObject);
            }
        }

        private static bool IsAircraftUnit(CachedUnit unit)
        {
            return unit != null && string.Equals(unit.Type, "Aircraft", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsSatelliteUnit(CachedUnit unit)
        {
            return unit != null && string.Equals(unit.Type, "Satellite", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsTrajectoryDrivenUnit(CachedUnit unit)
        {
            return IsAircraftUnit(unit) || IsSatelliteUnit(unit);
        }

        private static bool IsTrajectoryDrivenUnit(UnitViewState state)
        {
            return state != null && (state.IsAircraft || state.IsSatellite);
        }

        private static bool IsFacilityUnit(CachedUnit unit)
        {
            return unit != null && string.Equals(unit.Type, "Facility", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeResourcesPath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return path;
            }

            if (path.EndsWith(".glb", StringComparison.OrdinalIgnoreCase))
            {
                return path.Substring(0, path.Length - 4);
            }

            if (path.EndsWith(".prefab", StringComparison.OrdinalIgnoreCase))
            {
                return path.Substring(0, path.Length - 7);
            }

            return path;
        }

        private void EnsureFacilityVisualization(UnitViewState state, CachedUnit unit)
        {
            if (state == null || state.ViewObject == null || unit == null)
            {
                return;
            }

            EnsureFacilityLabel(state, unit.Name);

            float maxRangeNm = GetFacilityMaxSensorRangeNm(unit);
            if (Mathf.Abs(state.FacilityMaxSensorRangeNm - maxRangeNm) > 0.01f)
            {
                state.FacilityMaxSensorRangeNm = maxRangeNm;
                RebuildFacilityCoverage(state, maxRangeNm);
            }
        }

        private static float GetFacilityMaxSensorRangeNm(CachedUnit unit)
        {
            if (unit == null)
            {
                return 0f;
            }

            if (unit.MaxSensorRangeNm > 0)
            {
                return unit.MaxSensorRangeNm;
            }

            if (unit.Sensors == null || unit.Sensors.Count == 0)
            {
                return 0f;
            }

            float max = 0f;
            foreach (var sensor in unit.Sensors)
            {
                if (sensor != null && sensor.MaxRange > max)
                {
                    max = sensor.MaxRange;
                }
            }

            return max;
        }

        private void EnsureFacilityLabel(UnitViewState state, string unitName)
        {
            float yOffset = GetFacilityLabelYOffset();

            if (state.FacilityLabel == null)
            {
                var labelGo = new GameObject("FacilityLabel");
                labelGo.transform.SetParent(state.ViewObject.transform, false);
                labelGo.transform.localPosition = new Vector3(0f, yOffset, 0f);

                var tm = labelGo.AddComponent<TextMesh>();
                tm.anchor = TextAnchor.LowerCenter;
                tm.alignment = TextAlignment.Center;
                tm.fontSize = 42;
                tm.characterSize = 4f;
                tm.color = new Color(1f, 0.98f, 0.85f, 1f);
                tm.text = $"⌂ {unitName}";

                state.FacilityLabel = tm;
            }
            else
            {
                state.FacilityLabel.text = $"⌂ {unitName}";
                state.FacilityLabel.transform.localPosition = new Vector3(0f, yOffset, 0f);
            }

            UpdateFacilityBillboard(state);
        }

        private void RebuildFacilityCoverage(UnitViewState state, float maxRangeNm)
        {
            if (state == null || state.ViewObject == null)
            {
                return;
            }

            if (state.FacilityCoverageRoot != null)
            {
                Destroy(state.FacilityCoverageRoot);
                state.FacilityCoverageRoot = null;
            }

            if (maxRangeNm <= 0f)
            {
                return;
            }

            float radiusScene = maxRangeNm * 1852f * GetMetersToSceneScale();
            var root = new GameObject("FacilityCoverage");
            root.transform.SetParent(state.ViewObject.transform, false);
            root.transform.localPosition = new Vector3(0f, _facilityCoverageVerticalOffsetMeters * GetMetersToSceneScale(), 0f);

            BuildHemisphereWire(root.transform, radiusScene);
            state.FacilityCoverageRoot = root;
        }

        private void UpdateFacilityBillboard(UnitViewState state)
        {
            if (state == null || state.FacilityLabel == null)
            {
                return;
            }

            if (_targetCamera == null)
            {
                _targetCamera = Camera.main;
            }

            if (_targetCamera == null)
            {
                return;
            }

            Transform tr = state.FacilityLabel.transform;
            Vector3 toCamera = _targetCamera.transform.position - tr.position;
            if (toCamera.sqrMagnitude < 0.0001f)
            {
                return;
            }

            Vector3 up = _cesiumGeoreference != null ? _cesiumGeoreference.transform.up : Vector3.up;
            tr.rotation = Quaternion.LookRotation(toCamera, up);
        }

        private float GetMetersToSceneScale()
        {
            return (_useCesiumGeoreference && _cesiumGeoreference != null) ? 1f : Mathf.Max(0.00001f, _metersToUnityScale);
        }

        private float GetFacilityLabelYOffset()
        {
            float groundOffset = _facilityLabelGroundOffsetMeters * GetMetersToSceneScale();
            return Mathf.Max(0.01f, groundOffset);
        }

        private void BuildHemisphereWire(Transform parent, float radiusMeters)
        {
            if (parent == null || radiusMeters <= 0f)
            {
                return;
            }

            BuildHemisphereSurface(parent, radiusMeters);

            int segments = Mathf.Clamp(_facilityCoverageSegments, 24, 192);
            int parallels = Mathf.Clamp(_facilityCoverageParallels, 2, 24);
            int meridians = Mathf.Clamp(_facilityCoverageMeridians, 4, 48);

            CreateCircleLine(parent, "BaseCircle", radiusMeters, 0f, segments, _facilityCoverageColor, _facilityCoverageLineWidth);

            for (int i = 1; i <= parallels; i++)
            {
                float t = i / (float)parallels;
                float elev = Mathf.Lerp(0f, Mathf.PI * 0.5f, t);
                float r = radiusMeters * Mathf.Cos(elev);
                float y = radiusMeters * Mathf.Sin(elev);
                float alphaScale = Mathf.Lerp(1f, 0.35f, t);
                Color c = new Color(_facilityCoverageColor.r, _facilityCoverageColor.g, _facilityCoverageColor.b, _facilityCoverageColor.a * alphaScale);
                CreateCircleLine(parent, $"Parallel_{i}", r, y, segments, c, Mathf.Max(1f, _facilityCoverageLineWidth * 0.7f));
            }

            for (int m = 0; m < meridians; m++)
            {
                float az = (Mathf.PI * 2f * m) / meridians;
                CreateMeridianArc(parent, $"Meridian_{m}", radiusMeters, az, segments / 2, new Color(_facilityCoverageColor.r, _facilityCoverageColor.g, _facilityCoverageColor.b, _facilityCoverageColor.a * 0.75f), Mathf.Max(1f, _facilityCoverageLineWidth * 0.65f));
            }
        }

        private void BuildHemisphereSurface(Transform parent, float radiusMeters)
        {
            Material fillMat = GetOrCreateCoverageFillMaterial();
            if (fillMat == null)
            {
                return;
            }

            int longSegments = Mathf.Clamp(_facilityCoverageSegments / 2, 24, 128);
            int latSegments = Mathf.Clamp(_facilityCoverageParallels * 2, 8, 48);

            var go = new GameObject("HemisphereSurface");
            go.transform.SetParent(parent, false);

            var meshFilter = go.AddComponent<MeshFilter>();
            var meshRenderer = go.AddComponent<MeshRenderer>();
            meshRenderer.material = fillMat;
            meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            meshRenderer.receiveShadows = false;

            int vertexCount = (latSegments + 1) * (longSegments + 1);
            var vertices = new Vector3[vertexCount];
            var triangles = new int[latSegments * longSegments * 12];

            int v = 0;
            for (int lat = 0; lat <= latSegments; lat++)
            {
                float elev = (lat / (float)latSegments) * Mathf.PI * 0.5f;
                float y = Mathf.Sin(elev) * radiusMeters;
                float r = Mathf.Cos(elev) * radiusMeters;

                for (int lon = 0; lon <= longSegments; lon++)
                {
                    float az = (lon / (float)longSegments) * Mathf.PI * 2f;
                    float x = Mathf.Cos(az) * r;
                    float z = Mathf.Sin(az) * r;
                    vertices[v++] = new Vector3(x, y, z);
                }
            }

            int t = 0;
            int row = longSegments + 1;
            for (int lat = 0; lat < latSegments; lat++)
            {
                for (int lon = 0; lon < longSegments; lon++)
                {
                    int current = lat * row + lon;
                    int next = current + row;

                    triangles[t++] = current;
                    triangles[t++] = next;
                    triangles[t++] = current + 1;

                    triangles[t++] = current + 1;
                    triangles[t++] = next;
                    triangles[t++] = next + 1;

                    triangles[t++] = current + 1;
                    triangles[t++] = next;
                    triangles[t++] = current;

                    triangles[t++] = next + 1;
                    triangles[t++] = next;
                    triangles[t++] = current + 1;
                }
            }

            var mesh = new Mesh
            {
                name = "FacilityHemisphereMesh"
            };
            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            meshFilter.sharedMesh = mesh;
        }

        private void CreateCircleLine(Transform parent, string name, float radius, float y, int segments, Color color, float width)
        {
            if (radius <= 0.01f)
            {
                return;
            }

            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = true;
            lr.positionCount = segments;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.material = GetOrCreateCoverageMaterial();
            lr.startColor = color;
            lr.endColor = color;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;

            for (int i = 0; i < segments; i++)
            {
                float a = (Mathf.PI * 2f * i) / segments;
                float x = Mathf.Cos(a) * radius;
                float z = Mathf.Sin(a) * radius;
                lr.SetPosition(i, new Vector3(x, y, z));
            }
        }

        private void CreateMeridianArc(Transform parent, string name, float radius, float azimuth, int segments, Color color, float width)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var lr = go.AddComponent<LineRenderer>();
            lr.useWorldSpace = false;
            lr.loop = false;
            lr.positionCount = segments + 1;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.material = GetOrCreateCoverageMaterial();
            lr.startColor = color;
            lr.endColor = color;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lr.receiveShadows = false;

            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments;
                float elev = t * Mathf.PI * 0.5f;
                float r = radius * Mathf.Cos(elev);
                float y = radius * Mathf.Sin(elev);
                float x = Mathf.Cos(azimuth) * r;
                float z = Mathf.Sin(azimuth) * r;
                lr.SetPosition(i, new Vector3(x, y, z));
            }
        }

        private Material GetOrCreateCoverageMaterial()
        {
            if (_sharedCoverageMaterial != null)
            {
                return _sharedCoverageMaterial;
            }

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader == null)
            {
                return null;
            }

            _sharedCoverageMaterial = new Material(shader);
            _sharedCoverageMaterial.color = _facilityCoverageColor;
            SetupTransparentMaterial(_sharedCoverageMaterial, false);
            return _sharedCoverageMaterial;
        }

        private Material GetOrCreateCoverageFillMaterial()
        {
            if (_sharedCoverageFillMaterial != null)
            {
                return _sharedCoverageFillMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader == null)
            {
                return null;
            }

            _sharedCoverageFillMaterial = new Material(shader);
            _sharedCoverageFillMaterial.color = _facilityCoverageFillColor;
            SetupTransparentMaterial(_sharedCoverageFillMaterial, true);
            return _sharedCoverageFillMaterial;
        }

        private static void SetupTransparentMaterial(Material mat, bool doubleSided)
        {
            if (mat == null)
            {
                return;
            }

            mat.renderQueue = (int)RenderQueue.Transparent;

            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
            if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", 0);
            if (mat.HasProperty("_Cull")) mat.SetInt("_Cull", doubleSided ? (int)CullMode.Off : (int)CullMode.Back);

            mat.EnableKeyword("_ALPHABLEND_ON");
        }

        private void EnsureAircraftTrail(UnitViewState state)
        {
            if (state == null || state.ViewObject == null)
            {
                return;
            }

            if (state.TrailRenderer == null)
            {
                state.TrailRenderer = state.ViewObject.GetComponent<TrailRenderer>();
            }

            if (state.TrailRenderer == null)
            {
                state.TrailRenderer = state.ViewObject.AddComponent<TrailRenderer>();
            }

            var trail = state.TrailRenderer;
            if (trail == null)
            {
                return;
            }

            bool isSatellite = state.IsSatellite;
            trail.time = Mathf.Max(30f, isSatellite ? _satelliteTrailTimeSeconds : _aircraftTrailTimeSeconds);
            trail.minVertexDistance = Mathf.Max(1f, isSatellite ? _satelliteTrailMinVertexDistance : _aircraftTrailMinVertexDistance);
            trail.widthMultiplier = Mathf.Max(1f, isSatellite ? _satelliteTrailWidth : _aircraftTrailWidth);
            trail.autodestruct = false;
            trail.emitting = true;
            trail.alignment = LineAlignment.View;
            trail.material = GetOrCreateSharedTrailMaterial();

            trail.textureMode = LineTextureMode.Tile;
            trail.numCornerVertices = 4;
            trail.numCapVertices = 2;
            trail.widthCurve = isSatellite
                ? AnimationCurve.EaseInOut(0f, 0.9f, 1f, 0.25f)
                : AnimationCurve.EaseInOut(0f, 0.6f, 1f, 0.15f);
            trail.startColor = isSatellite
                ? new Color(1f, 0.85f, 0.2f, 0.95f)
                : new Color(0.1f, 0.9f, 1f, 0.95f);
            trail.endColor = isSatellite
                ? new Color(1f, 0.55f, 0.1f, 0.05f)
                : new Color(0.05f, 0.55f, 0.95f, 0.05f);
            trail.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            trail.receiveShadows = false;
        }

        private void UpdateTrailWidthByDistance(UnitViewState state)
        {
            if (state == null || state.TrailRenderer == null || state.ViewObject == null)
                return;

            if (_targetCamera == null)
                _targetCamera = Camera.main;
            if (_targetCamera == null)
                return;

            // 计算相机到轨迹线对象的距离
            float distance = Vector3.Distance(_targetCamera.transform.position, state.ViewObject.transform.position);
            
            // 参考距离（作为基准点）
            float referenceDistance = 5000f;
            
            // 动态计算宽度倍数，距离越远线越粗
            // 这样可以保证在任何距离下轨迹线都能看到
            float distanceMultiplier = Mathf.Max(1f, distance / referenceDistance);
            
            bool isSatellite = state.IsSatellite;
            float baseWidth = isSatellite ? _satelliteTrailWidth : _aircraftTrailWidth;
            
            state.TrailRenderer.widthMultiplier = baseWidth * distanceMultiplier;
        }

        private Material GetOrCreateSharedTrailMaterial()
        {
            if (_sharedTrailMaterial != null)
            {
                return _sharedTrailMaterial;
            }

            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
            {
                shader = Shader.Find("Sprites/Default");
            }
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Transparent");
            }
            if (shader == null)
            {
                shader = Shader.Find("Unlit/Color");
            }

            if (shader == null)
            {
                return null;
            }

            _sharedTrailMaterial = new Material(shader);
            _sharedTrailMaterial.mainTexture = BuildFlowTexture();
            _sharedTrailMaterial.mainTextureScale = new Vector2(4f, 1f);
            _sharedTrailMaterial.color = new Color(0.1f, 0.9f, 1f, 0.95f);
            return _sharedTrailMaterial;
        }

        private static Texture2D BuildFlowTexture()
        {
            const int width = 64;
            var tex = new Texture2D(width, 1, TextureFormat.RGBA32, false)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Bilinear
            };

            for (int x = 0; x < width; x++)
            {
                float t = x / (float)(width - 1);
                float pulse = Mathf.Sin(t * Mathf.PI * 2f) * 0.5f + 0.5f;
                float alpha = Mathf.Lerp(0.15f, 1f, pulse);
                tex.SetPixel(x, 0, new Color(0.2f, 0.95f, 1f, alpha));
            }

            tex.Apply();
            return tex;
        }

        private void RebuildInterpolatedPath(UnitViewState state)
        {
            if (state == null || state.ViewObject == null)
            {
                return;
            }

            if (!state.HasCurrentGeo)
            {
                return;
            }

            state.InterpolatedGeoPath.Clear();

            double startLon = state.CurrentLongitude;
            double startLat = state.CurrentLatitude;
            double startHeight = state.CurrentHeight;

            double endLon = state.TargetLongitude;
            double endLat = state.TargetLatitude;
            double endHeight = state.TargetHeight;

            int steps = Mathf.Max(2, _aircraftInterpolationSteps);
            for (int i = 1; i <= steps; i++)
            {
                float t = i / (float)steps;
                float smoothT = t * t * (3f - 2f * t);
                double lon = LerpDouble(startLon, endLon, smoothT);
                double lat = LerpDouble(startLat, endLat, smoothT);
                double height = LerpDouble(startHeight, endHeight, smoothT);
                state.InterpolatedGeoPath.Enqueue(new UnitViewState.GeoPoint(lon, lat, height));
            }
        }

        private void AdvanceAircraftAlongPath(UnitViewState state, float deltaTime)
        {
            if (state == null || state.ViewObject == null)
            {
                return;
            }

            if (!state.HasCurrentGeo)
            {
                state.CurrentLongitude = state.TargetLongitude;
                state.CurrentLatitude = state.TargetLatitude;
                state.CurrentHeight = state.TargetHeight;
                state.HasCurrentGeo = true;
            }

            double speedMetersPerSec = ResolveTrajectorySpeedMetersPerSec(state);
            double remainingMeters = speedMetersPerSec * Math.Max(0.0001f, deltaTime);

            if (state.InterpolatedGeoPath == null || state.InterpolatedGeoPath.Count == 0)
            {
                MoveCurrentGeoTowardsTarget(
                    state,
                    state.TargetLongitude,
                    state.TargetLatitude,
                    state.TargetHeight,
                    ref remainingMeters,
                    Math.Max(0.1, _pathArriveThreshold));
            }

            while (remainingMeters > 0.0001 && state.InterpolatedGeoPath != null && state.InterpolatedGeoPath.Count > 0)
            {
                UnitViewState.GeoPoint wp = state.InterpolatedGeoPath.Peek();
                bool reached = MoveCurrentGeoTowardsTarget(
                    state,
                    wp.Longitude,
                    wp.Latitude,
                    wp.Height,
                    ref remainingMeters,
                    Math.Max(0.1, _pathArriveThreshold));

                if (reached)
                {
                    state.InterpolatedGeoPath.Dequeue();
                    continue;
                }

                break;
            }

            ApplyAircraftGeoToTransform(state);
        }

        private bool MoveCurrentGeoTowardsTarget(
            UnitViewState state,
            double targetLon,
            double targetLat,
            double targetHeight,
            ref double remainingMeters,
            double arriveThresholdMeters)
        {
            double groundMeters = GroundDistanceMeters(state.CurrentLatitude, state.CurrentLongitude, targetLat, targetLon);
            double verticalMeters = Math.Abs(targetHeight - state.CurrentHeight);
            double distanceMeters = Math.Sqrt(groundMeters * groundMeters + verticalMeters * verticalMeters);

            if (distanceMeters <= Math.Max(0.01, arriveThresholdMeters))
            {
                state.CurrentLongitude = targetLon;
                state.CurrentLatitude = targetLat;
                state.CurrentHeight = targetHeight;
                return true;
            }

            double stepMeters = Math.Min(remainingMeters, distanceMeters);
            if (stepMeters <= 0.0001)
            {
                return false;
            }

            double t = stepMeters / distanceMeters;
            state.CurrentLongitude = LerpDouble(state.CurrentLongitude, targetLon, t);
            state.CurrentLatitude = LerpDouble(state.CurrentLatitude, targetLat, t);
            state.CurrentHeight = LerpDouble(state.CurrentHeight, targetHeight, t);
            remainingMeters -= stepMeters;
            return stepMeters >= distanceMeters - 0.001;
        }

        private void ApplyAircraftGeoToTransform(UnitViewState state)
        {
            if (state == null || state.ViewObject == null)
            {
                return;
            }

            if (state.GlobeAnchor != null)
            {
                TrySetAnchorLlh(state.GlobeAnchor, state.CurrentLongitude, state.CurrentLatitude, state.CurrentHeight);
                return;
            }

            Position pos = new Position
            {
                Longitude = (float)state.CurrentLongitude,
                Latitude = (float)state.CurrentLatitude,
                Altitude = (float)state.CurrentHeight
            };

            Vector3 p = GeoToScenePosition(pos);
            if (IsFinite(p))
            {
                state.ViewObject.transform.position = p;
            }
        }

        private static double LerpDouble(double a, double b, double t)
        {
            return a + (b - a) * t;
        }

        private double ResolveTrajectorySpeedMetersPerSec(UnitViewState state)
        {
            if (state == null)
            {
                return Math.Max(1.0, _aircraftCruiseSpeed);
            }

            // Aircraft keeps existing cruise tuning; satellite follows its dynamic orbital speed.
            if (state.IsSatellite)
            {
                double speedFromKnots = Math.Max(0.0, state.TargetSpeedKnots) * 0.514444;
                return Math.Max(100.0, speedFromKnots);
            }

            return Math.Max(1.0, _aircraftCruiseSpeed);
        }

        private Transform GetUnitParentTransform()
        {
            if (_useCesiumGeoreference && _cesiumGeoreference != null)
            {
                return _cesiumGeoreference.transform;
            }

            return transform;
        }

        private void EnsureGlobeAnchor(GameObject go)
        {
            if (!_addCesiumGlobeAnchor || go == null)
            {
                return;
            }

            if (_cesiumGeoreference == null)
            {
                return;
            }

            if (!go.transform.IsChildOf(_cesiumGeoreference.transform))
            {
                return;
            }

            CesiumGlobeAnchor anchor = go.GetComponent<CesiumGlobeAnchor>();
            if (anchor == null)
            {
                anchor = go.AddComponent<CesiumGlobeAnchor>();
            }

            if (anchor != null)
            {
                var anchorType = anchor.GetType();
                var prop = anchorType.GetProperty("AdjustOrientationForGlobeWhenMoving");
                if (prop != null && prop.CanWrite && prop.PropertyType == typeof(bool))
                {
                    prop.SetValue(anchor, true);
                }
            }
        }

        private Vector3 GeoToScenePosition(Position pos)
        {
            if (_useCesiumGeoreference && _cesiumGeoreference != null)
            {
                if (TryCesiumTransform(pos, out Vector3 cesiumPos))
                {
                    return cesiumPos;
                }

                if (!_warnedCesiumApiMissing)
                {
                    _warnedCesiumApiMissing = true;
                    Debug.LogWarning("[UnitViewManager] CesiumGeoreference API mismatch. Falling back to local ENU approximation.");
                }
            }

            if (_useCesiumGeoreference && _cesiumGeoreference == null && !_warnedCesiumMissing)
            {
                _warnedCesiumMissing = true;
                Debug.LogWarning("[UnitViewManager] CesiumGeoreference not found. Falling back to local ENU approximation.");
            }

            if (!_originInitialized)
            {
                if (_autoSetOriginFromFirstUnit)
                {
                    _originLatitude = pos.Latitude;
                    _originLongitude = pos.Longitude;
                }

                _originInitialized = true;
                if (_logLifecycle)
                {
                    Debug.Log($"[UnitViewManager] Origin set: lat={_originLatitude}, lon={_originLongitude}");
                }
            }

            const double metersPerDegLat = 111320.0;
            double latRad = _originLatitude * Math.PI / 180.0;
            double metersPerDegLon = Math.Cos(latRad) * metersPerDegLat;

            double eastMeters = (pos.Longitude - _originLongitude) * metersPerDegLon;
            double northMeters = (pos.Latitude - _originLatitude) * metersPerDegLat;

            float x = (float)(eastMeters * _metersToUnityScale);
            float y = pos.Altitude * _altitudeScale;
            float z = (float)(northMeters * _metersToUnityScale);

            return new Vector3(x, y, z);
        }

        private bool TryCesiumTransform(Position pos, out Vector3 scenePos)
        {
            scenePos = default;

            Type georefType = _cesiumGeoreference.GetType();

            // Newer API: TransformLongitudeLatitudeHeightToUnity(double3 llh)
            var llhToUnity = georefType.GetMethod("TransformLongitudeLatitudeHeightToUnity", new[] { typeof(double3) });
            if (llhToUnity != null)
            {
                object llhObj = new double3(pos.Longitude, pos.Latitude, pos.Altitude);
                object result = llhToUnity.Invoke(_cesiumGeoreference, new[] { llhObj });
                if (result is double3 d3)
                {
                    scenePos = new Vector3((float)d3.x, (float)d3.y, (float)d3.z);
                    return true;
                }
            }

            // Older API: TransformEarthCenteredEarthFixedPositionToUnity(double3 ecef)
            var ecefToUnity = georefType.GetMethod("TransformEarthCenteredEarthFixedPositionToUnity", new[] { typeof(double3) });
            if (ecefToUnity != null)
            {
                double3 ecef = LlhToEcefWgs84(pos.Longitude, pos.Latitude, pos.Altitude);
                object result = ecefToUnity.Invoke(_cesiumGeoreference, new object[] { ecef });
                if (result is double3 d3)
                {
                    scenePos = new Vector3((float)d3.x, (float)d3.y, (float)d3.z);
                    return true;
                }
            }

            return false;
        }

        private static double3 LlhToEcefWgs84(double longitudeDeg, double latitudeDeg, double heightMeters)
        {
            const double a = 6378137.0;
            const double f = 1.0 / 298.257223563;
            const double e2 = f * (2.0 - f);

            double latRad = latitudeDeg * Math.PI / 180.0;
            double lonRad = longitudeDeg * Math.PI / 180.0;

            double sinLat = Math.Sin(latRad);
            double cosLat = Math.Cos(latRad);
            double sinLon = Math.Sin(lonRad);
            double cosLon = Math.Cos(lonRad);

            double n = a / Math.Sqrt(1.0 - e2 * sinLat * sinLat);

            double x = (n + heightMeters) * cosLat * cosLon;
            double y = (n + heightMeters) * cosLat * sinLon;
            double z = (n * (1.0 - e2) + heightMeters) * sinLat;

            return new double3(x, y, z);
        }

        private static Vector3 HeadingToForward(float headingDeg)
        {
            float rad = headingDeg * Mathf.Deg2Rad;
            float x = Mathf.Sin(rad);
            float z = Mathf.Cos(rad);
            return new Vector3(x, 0f, z);
        }

        public bool FocusCameraToUnitsRegion()
        {
            if (_targetCamera == null)
            {
                _targetCamera = Camera.main;
            }

            if (_targetCamera == null)
            {
                _targetCamera = FindFirstObjectByType<Camera>();
            }

            if (_targetCamera == null)
            {
                return false;
            }

            if (_preferCesiumGeoFocus && _useCesiumGeoreference && _cesiumGeoreference != null)
            {
                if (TryGetUnitsGeoCenter(out double centerLon, out double centerLat, out double centerHeight, out double radiusMeters))
                {
                    if (TryFocusCameraWithCesiumGeo(centerLon, centerLat, centerHeight, radiusMeters))
                    {
                        return true;
                    }
                }
            }

            if (!TryGetUnitsBounds(out Bounds bounds))
            {
                return false;
            }

            Vector3 center = bounds.center;
            float radius = Mathf.Max(bounds.extents.magnitude, 100f);

            float pitch = Mathf.Clamp(_focusPitchDeg, 20f, 80f);
            Quaternion lookRot = Quaternion.Euler(pitch, 0f, 0f);

            float dist = radius + _focusDistancePadding;
            Vector3 backward = -(lookRot * Vector3.forward);
            Vector3 camPos = center + backward * dist + Vector3.up * _focusHeightPadding;

            _targetCamera.transform.position = camPos;
            Vector3 up = _cesiumGeoreference != null ? _cesiumGeoreference.transform.up : Vector3.up;
            _targetCamera.transform.rotation = Quaternion.LookRotation(center - camPos, up);
            ApplySafeClipPlanes(Mathf.Max(500000f, radius * 8f + _focusHeightPadding + _focusDistancePadding));
            EnsureCameraNotUpsideDown(up);
            return true;
        }

        public bool FocusUnitOnCamera(string objectId)
        {
            if (string.IsNullOrEmpty(objectId))
            {
                return false;
            }

            if (!_views.TryGetValue(objectId, out UnitViewState state) || state == null)
            {
                return false;
            }

            if (_targetCamera == null)
            {
                _targetCamera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            }

            if (_targetCamera == null)
            {
                return false;
            }

            if (_preferCesiumGeoFocus && _useCesiumGeoreference && _cesiumGeoreference != null)
            {
                return TryFocusCameraWithCesiumGeo(state.TargetLongitude, state.TargetLatitude, state.TargetHeight, 0);
            }

            if (!IsFinite(state.TargetPosition))
            {
                return false;
            }

            Vector3 target = state.TargetPosition;
            Vector3 forward = HeadingToForward(state.TargetHeading);
            Vector3 up = _cesiumGeoreference != null ? _cesiumGeoreference.transform.up : Vector3.up;
            Vector3 right = Vector3.Cross(up, forward).normalized;
            if (right.sqrMagnitude < 0.0001f)
            {
                right = Vector3.right;
            }

            Vector3 camPos = target
                + right * _cameraFollowViewOffset.x
                + up * _cameraFollowViewOffset.y
                - forward * _cameraFollowViewOffset.z;
            _targetCamera.transform.position = camPos;
            _targetCamera.transform.rotation = Quaternion.LookRotation(target - camPos, up);
            ApplySafeClipPlanes(Mathf.Max(500000f, _cameraFollowViewOffset.magnitude + _focusHeightPadding + _focusDistancePadding + 100000f));
            EnsureCameraNotUpsideDown(up);
            return true;
        }

        public bool FollowUnitOnCamera(string objectId)
        {
            if (string.IsNullOrEmpty(objectId))
            {
                return false;
            }

            if (!_views.TryGetValue(objectId, out UnitViewState state) || state == null)
            {
                return false;
            }

            _cameraFollowObjectId = objectId;
            _cameraFollowEnabled = true;
            return TryApplyCameraFollow(objectId);
        }

        public void ClearCameraFollow()
        {
            _cameraFollowObjectId = null;
            _cameraFollowEnabled = false;
        }

        public bool IsCameraFollowing(string objectId)
        {
            return _cameraFollowEnabled && string.Equals(_cameraFollowObjectId, objectId, StringComparison.Ordinal);
        }

        public bool IsCameraFollowActive()
        {
            return _cameraFollowEnabled;
        }

        public string GetCameraFollowObjectId()
        {
            return _cameraFollowObjectId;
        }

        private bool TryUpdateCameraFollow()
        {
            if (!_cameraFollowEnabled || string.IsNullOrEmpty(_cameraFollowObjectId))
            {
                return false;
            }

            if (!_views.ContainsKey(_cameraFollowObjectId))
            {
                ClearCameraFollow();
                return false;
            }

            return TryApplyCameraFollow(_cameraFollowObjectId);
        }

        private bool TryApplyCameraFollow(string objectId)
        {
            if (string.IsNullOrEmpty(objectId))
            {
                return false;
            }

            if (!_views.TryGetValue(objectId, out UnitViewState state) || state == null)
            {
                return false;
            }

            if (_targetCamera == null)
            {
                _targetCamera = Camera.main != null ? Camera.main : FindFirstObjectByType<Camera>();
            }

            if (_targetCamera == null)
            {
                return false;
            }

            Vector3 target = state.TargetPosition;
            if (!IsFinite(target))
            {
                return false;
            }

            Vector3 forward = HeadingToForward(state.TargetHeading);
            Vector3 up = _cesiumGeoreference != null ? _cesiumGeoreference.transform.up : Vector3.up;
            Vector3 right = Vector3.Cross(up, forward);
            if (right.sqrMagnitude < 0.0001f)
            {
                right = Vector3.right;
            }
            else
            {
                right.Normalize();
            }

            Vector3 camPos = target
                + right * _cameraFollowViewOffset.x
                + up * _cameraFollowViewOffset.y
                - forward * _cameraFollowViewOffset.z;

            _targetCamera.transform.position = camPos;
            _targetCamera.transform.rotation = Quaternion.LookRotation(target - camPos, up);
            ApplySafeClipPlanes(Mathf.Max(500000f, _cameraFollowViewOffset.magnitude + _focusHeightPadding + _focusDistancePadding + 100000f));
            EnsureCameraNotUpsideDown(up);
            return true;
        }

        private void TryAutoFocusCamera()
        {
            if (!_autoFocusOnFirstBatch || _hasAutoFocused)
            {
                return;
            }

            if (_views.Count < _minUnitsForAutoFocus)
            {
                return;
            }

            if (FocusCameraToUnitsRegion())
            {
                _hasAutoFocused = true;
                if (_logLifecycle)
                {
                    Debug.Log($"[UnitViewManager] Camera focused to units region, count={_views.Count}");
                }
            }
        }

        private bool TryGetUnitsBounds(out Bounds bounds)
        {
            bounds = default;
            bool initialized = false;

            foreach (var kv in _views)
            {
                UnitViewState state = kv.Value;
                if (state == null || state.ViewObject == null)
                {
                    continue;
                }

                Vector3 p = state.TargetPosition;
                if (!IsFinite(p))
                {
                    continue;
                }

                if (!initialized)
                {
                    bounds = new Bounds(p, Vector3.zero);
                    initialized = true;
                }
                else
                {
                    bounds.Encapsulate(p);
                }
            }

            return initialized;
        }

        private static bool IsFinite(Vector3 v)
        {
            return !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z)
                || float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));
        }

        private bool TryGetUnitsGeoCenter(out double lon, out double lat, out double height, out double radiusMeters)
        {
            lon = 0;
            lat = 0;
            height = 0;
            radiusMeters = 0;

            int count = 0;
            foreach (var kv in _views)
            {
                UnitViewState s = kv.Value;
                if (s == null || s.ViewObject == null)
                {
                    continue;
                }

                lon += s.TargetLongitude;
                lat += s.TargetLatitude;
                height += s.TargetHeight;
                count++;
            }

            if (count == 0)
            {
                return false;
            }

            lon /= count;
            lat /= count;
            height /= count;

            foreach (var kv in _views)
            {
                UnitViewState s = kv.Value;
                if (s == null || s.ViewObject == null)
                {
                    continue;
                }

                double d = GroundDistanceMeters(lat, lon, s.TargetLatitude, s.TargetLongitude);
                if (d > radiusMeters)
                {
                    radiusMeters = d;
                }
            }

            return true;
        }

        private bool TryFocusCameraWithCesiumGeo(double centerLon, double centerLat, double centerHeight, double radiusMeters)
        {
            if (_targetCamera == null)
            {
                return false;
            }

            CesiumGlobeAnchor camAnchor = _targetCamera.GetComponent<CesiumGlobeAnchor>();
            if (camAnchor == null && _allowAutoAddCameraGlobeAnchor)
            {
                camAnchor = _targetCamera.gameObject.AddComponent<CesiumGlobeAnchor>();
            }

            if (camAnchor == null)
            {
                return false;
            }

            double focusHeight = Math.Max(centerHeight + _focusHeightPadding, centerHeight + radiusMeters * 2.0 + _focusDistancePadding);
            if (!TrySetAnchorLlh(camAnchor, centerLon, centerLat, focusHeight))
            {
                return false;
            }

            Position centerPos = new Position
            {
                Longitude = (float)centerLon,
                Latitude = (float)centerLat,
                Altitude = (float)centerHeight
            };

            Vector3 centerWorld = GeoToScenePosition(centerPos);
            if (!IsFinite(centerWorld))
            {
                return false;
            }

            Vector3 up = _cesiumGeoreference != null ? _cesiumGeoreference.transform.up : Vector3.up;
            _targetCamera.transform.rotation = Quaternion.LookRotation(centerWorld - _targetCamera.transform.position, up);
            EnsureCameraNotUpsideDown(up);

            float desiredFar = (float)Math.Max(500000f, radiusMeters * 8.0 + _focusHeightPadding + _focusDistancePadding);
            ApplySafeClipPlanes(desiredFar);
            return true;
        }

        private static double GroundDistanceMeters(double lat1Deg, double lon1Deg, double lat2Deg, double lon2Deg)
        {
            const double R = 6371000.0;
            double lat1 = lat1Deg * Math.PI / 180.0;
            double lat2 = lat2Deg * Math.PI / 180.0;
            double dLat = (lat2Deg - lat1Deg) * Math.PI / 180.0;
            double dLon = (lon2Deg - lon1Deg) * Math.PI / 180.0;

            double a = Math.Sin(dLat / 2.0) * Math.Sin(dLat / 2.0)
                + Math.Cos(lat1) * Math.Cos(lat2) * Math.Sin(dLon / 2.0) * Math.Sin(dLon / 2.0);
            double c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
            return R * c;
        }

        private static bool TrySetAnchorLlh(CesiumGlobeAnchor anchor, double lon, double lat, double height)
        {
            if (anchor == null)
            {
                return false;
            }

            object anchorObj = anchor;
            Type t = anchorObj.GetType();

            var llhProp = t.GetProperty("longitudeLatitudeHeight");
            if (llhProp != null && llhProp.CanWrite && llhProp.PropertyType == typeof(double3))
            {
                llhProp.SetValue(anchorObj, new double3(lon, lat, height));
                return true;
            }

            var llhMethod = t.GetMethod("SetPositionLongitudeLatitudeHeight", new[] { typeof(double), typeof(double), typeof(double) });
            if (llhMethod != null)
            {
                llhMethod.Invoke(anchorObj, new object[] { lon, lat, height });
                return true;
            }

            bool changed = false;
            var lonProp = t.GetProperty("longitude");
            var latProp = t.GetProperty("latitude");
            var hProp = t.GetProperty("height");
            if (lonProp != null && lonProp.CanWrite)
            {
                lonProp.SetValue(anchorObj, lon);
                changed = true;
            }
            if (latProp != null && latProp.CanWrite)
            {
                latProp.SetValue(anchorObj, lat);
                changed = true;
            }
            if (hProp != null && hProp.CanWrite)
            {
                hProp.SetValue(anchorObj, height);
                changed = true;
            }

            return changed;
        }

        private void EnsureCameraNotUpsideDown(Vector3 expectedUp)
        {
            if (_targetCamera == null)
            {
                return;
            }

            if (Vector3.Dot(_targetCamera.transform.up, expectedUp) < 0f)
            {
                _targetCamera.transform.rotation = Quaternion.AngleAxis(180f, _targetCamera.transform.forward) * _targetCamera.transform.rotation;
            }
        }

        private void ApplySafeClipPlanes(float desiredFar)
        {
            if (_targetCamera == null)
            {
                return;
            }

            // Keep projection stable to avoid frequent editor frustum warnings.
            float near = Mathf.Clamp(_targetCamera.nearClipPlane, 0.3f, 200f);
            float far = Mathf.Clamp(desiredFar, near + 1000f, 5000000f);

            _targetCamera.nearClipPlane = near;
            _targetCamera.farClipPlane = far;
        }

        private bool TrySetAnchorLongitudeLatitudeHeight(UnitViewState state)
        {
            if (state == null || state.GlobeAnchor == null)
            {
                return false;
            }

            object anchorObj = state.GlobeAnchor;
            Type t = anchorObj.GetType();

            var llhProp = t.GetProperty("longitudeLatitudeHeight");
            if (llhProp != null && llhProp.CanWrite && llhProp.PropertyType == typeof(double3))
            {
                llhProp.SetValue(anchorObj, new double3(state.TargetLongitude, state.TargetLatitude, state.TargetHeight));
                return true;
            }

            var llhMethod = t.GetMethod("SetPositionLongitudeLatitudeHeight", new[] { typeof(double), typeof(double), typeof(double) });
            if (llhMethod != null)
            {
                llhMethod.Invoke(anchorObj, new object[] { state.TargetLongitude, state.TargetLatitude, state.TargetHeight });
                return true;
            }

            bool changed = false;
            var lonProp = t.GetProperty("longitude");
            var latProp = t.GetProperty("latitude");
            var hProp = t.GetProperty("height");
            if (lonProp != null && lonProp.CanWrite)
            {
                lonProp.SetValue(anchorObj, state.TargetLongitude);
                changed = true;
            }
            if (latProp != null && latProp.CanWrite)
            {
                latProp.SetValue(anchorObj, state.TargetLatitude);
                changed = true;
            }
            if (hProp != null && hProp.CanWrite)
            {
                hProp.SetValue(anchorObj, state.TargetHeight);
                changed = true;
            }

            return changed;
        }
    }
}
