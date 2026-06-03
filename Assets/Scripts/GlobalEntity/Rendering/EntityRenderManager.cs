using System.Collections.Generic;
using CesiumForUnity;
using CommandP.GlobalEntity.Data;
using CommandP.GlobalEntity.Services;
using Unity.Mathematics;
using UnityEngine;

namespace CommandP.GlobalEntity.Rendering
{
    [DisallowMultipleComponent]
    public class EntityRenderManager : MonoBehaviour
    {
        [Header("Cesium")]
        [SerializeField] private CesiumGeoreference _cesiumGeoreference;
        [SerializeField] private bool _autoFindGeoreference = true;

        [Header("Camera")]
        [SerializeField] private Camera _targetCamera;

        [Header("LOD")]
        [SerializeField] private float _defaultLodDistance = 15000f;

        [Header("Debug")]
        [SerializeField] private bool _logPerformance = false;
        [SerializeField] private bool _autoStart = true;

        private EntityData[] _entities;
        private int _entityCount;

        // Subsystems
        private WorldSpaceMarkerSystem _worldSpaceMarker;
        private ModelCache _modelCache;
        private ModelViewSystem _modelViewSystem;
        private ViewPool _viewPool;
        private LODSwitcher _lodSwitcher;
        private EntityMotionDriver _motionDriver;

        private Dictionary<string, CesiumGlobeAnchor> _anchorMap;
        private Transform _globalEntityRoot;

        private bool _initialized;
        private float _perfTimer;
        private int _perfFrameCount;

        // IMGUI
        private string _selectedId;
        private int _debugRotTypeIdx;
        
        public int EntityCount => _entityCount;
        public Camera TargetCamera => _targetCamera;
        public System.Action<string> OnMarkerClicked;

        private void Start()
        {
            if (_autoStart) StartCoroutine(InitializeRoutine());
        }

        private System.Collections.IEnumerator InitializeRoutine()
        {
            yield return null;

            if (_cesiumGeoreference == null && _autoFindGeoreference)
                _cesiumGeoreference = FindFirstObjectByType<CesiumGeoreference>();
            if (_cesiumGeoreference == null) { enabled = false; yield break; }

            if (_targetCamera == null)
                _targetCamera = Camera.main ?? FindFirstObjectByType<Camera>();

            GeoCoordConverter.Initialize(_cesiumGeoreference);

            var rootGo = new GameObject("GlobalEntityRoot");
            rootGo.transform.SetParent(_cesiumGeoreference.transform, false);
            _globalEntityRoot = rootGo.transform;

            _worldSpaceMarker = new WorldSpaceMarkerSystem(_targetCamera);
            _viewPool = new ViewPool(_globalEntityRoot);

            _modelCache = new ModelCache();
            _modelCache.PreloadAll();
            _modelViewSystem = new ModelViewSystem(_modelCache, _viewPool.ActiveEntries);

            _lodSwitcher = new LODSwitcher(_worldSpaceMarker, _modelViewSystem, _viewPool.ActiveEntries);
            _lodSwitcher.DefaultLodDistance = _defaultLodDistance;

            _entities = TestEntityDataFactory.CreateSouthChinaSeaScenario();
            _entityCount = _entities.Length;

            // Acquire pool entries + setup markers for initial LOD state
            for (int i = 0; i < _entityCount; i++)
            {
                var entry = _viewPool.Acquire(_entities[i]);
                // Initially all entities start at far LOD 鈫?show marker
                _worldSpaceMarker.ShowMarker(entry, _entities[i]);
            }
            _anchorMap = _viewPool.GetAnchorMap();

            var motionGo = new GameObject("EntityMotionDriver");
            motionGo.transform.SetParent(transform, false);
            _motionDriver = motionGo.AddComponent<EntityMotionDriver>();
            _motionDriver.Initialize(_entities, _entityCount);
            _motionDriver.IsPaused = true;

            _initialized = true;
            Debug.Log($"[EntityRenderManager] Initialized. Entities: {_entityCount} | WorldSpace Tactical Markers");
        }

        private void Update()
        {
            if (!_initialized) return;

#if UNITY_EDITOR
            var sw = _logPerformance ? System.Diagnostics.Stopwatch.StartNew() : null;
#endif
            float dt = Time.deltaTime;
            GeoCoordConverter.RefreshEcef(_entities, _entityCount);
            GlobeAnchorHelper.ApplyAllPositions(_entities, _entityCount, _anchorMap);
            _lodSwitcher.EvaluateAll(_entities, _entityCount, _targetCamera);
            _modelViewSystem.UpdateTransforms(_entities, _entityCount);
            HandleMarkerClick();

#if UNITY_EDITOR
            if (_logPerformance && sw != null)
            {
                sw.Stop();
                _perfTimer += sw.ElapsedMilliseconds;
                if (++_perfFrameCount >= 60)
                {
                    Debug.Log($"[EntityRenderManager] Avg update: {_perfTimer / _perfFrameCount:F2}ms ({_entityCount} entities)");
                    _perfTimer = 0; _perfFrameCount = 0;
                }
            }
#endif
        }


        private void OnDestroy()
        {
            _viewPool?.ReleaseAll();
            _worldSpaceMarker?.Dispose();
        }

        // ============================================================
        // World Space Marker Click (raycast against IconQuad colliders)
        // ============================================================

        private void HandleMarkerClick()
        {
            if (!Input.GetMouseButtonDown(0)) return;
            if (_targetCamera == null) return;

            var ray = _targetCamera.ScreenPointToRay(Input.mousePosition);
            if (!Physics.Raycast(ray, out var hit)) return;

            var handle = hit.collider.GetComponentInParent<EntityHandle>();
            if (handle == null || string.IsNullOrEmpty(handle.ObjectId)) return;

            Debug.Log($"[EntityRenderManager] Marker clicked: {handle.ObjectId}");
            _selectedId = handle.ObjectId;
            FocusOnEntity(handle.ObjectId);
            OnMarkerClicked?.Invoke(handle.ObjectId);
        }

        // ============================================================
        // Public API
        // ============================================================

        public IReadOnlyList<EntityData> GetEntities() => _entities;

        public EntityData GetEntity(string objectId)
        {
            for (int i = 0; i < _entityCount; i++)
                if (_entities[i]?.ObjectId == objectId) return _entities[i];
            return null;
        }

        public void AddEntity(EntityData entity)
        {
            if (entity == null || string.IsNullOrEmpty(entity.ObjectId)) return;
            for (int i = 0; i < _entityCount; i++)
            { if (_entities[i]?.ObjectId == entity.ObjectId) { UpdateEntity(entity); return; } }
            if (_entityCount >= _entities.Length)
            { var n = new EntityData[_entities.Length * 2]; System.Array.Copy(_entities, n, _entityCount); _entities = n; }
            entity.EcefDirty = true;
            _entities[_entityCount++] = entity;
            var entry = _viewPool.Acquire(entity);
            _worldSpaceMarker.ShowMarker(entry, entity);
            _anchorMap = _viewPool.GetAnchorMap();
        }

        public void UpdateEntity(EntityData entity)
        {
            for (int i = 0; i < _entityCount; i++)
            { if (_entities[i]?.ObjectId == entity.ObjectId) { entity.EcefDirty = true; _entities[i] = entity; return; } }
            AddEntity(entity);
        }

        public void RemoveEntity(string objectId)
        {
            for (int i = 0; i < _entityCount; i++)
            {
                if (_entities[i]?.ObjectId == objectId)
                {
                    _worldSpaceMarker.HideMarker(_viewPool.ActiveEntries.TryGetValue(objectId, out var e) ? e : null);
                    _viewPool.Release(objectId);
                    _entities[i] = _entities[_entityCount - 1];
                    _entities[_entityCount - 1] = null;
                    _entityCount--;
                    _anchorMap = _viewPool.GetAnchorMap();
                    return;
                }
            }
        }

        public void SetLodDistance(float d) { if (_lodSwitcher != null) _lodSwitcher.DefaultLodDistance = d; }
        public void SetTypeLodDistance(EntityType t, float d) => _lodSwitcher?.SetTypeDistance(t, d);

        // ============================================================
        // Camera
        // ============================================================

        public void FocusOnLocation(double lon, double lat, double alt,
            double? lookLon = null, double? lookLat = null, double? lookAlt = null)
        {
            if (_targetCamera == null || _cesiumGeoreference == null) return;
            _targetCamera.nearClipPlane = 0.5f;
            _targetCamera.farClipPlane = Mathf.Clamp((float)(alt * 2.5 + 100000f), 1000f, 5000000f);

            double3 camEcef = GeoCoordConverter.LlhToEcefWgs84(lon, lat, alt);
            double3 camUnity = GeoCoordConverter.EcefToUnity(camEcef);
            _targetCamera.transform.position = new Vector3((float)camUnity.x, (float)camUnity.y, (float)camUnity.z);

            if (lookLon.HasValue && lookLat.HasValue)
            {
                double3 tEcef = GeoCoordConverter.LlhToEcefWgs84(lookLon.Value, lookLat.Value, lookAlt ?? 0);
                double3 tUnity = GeoCoordConverter.EcefToUnity(tEcef);
                Vector3 tp = new Vector3((float)tUnity.x, (float)tUnity.y, (float)tUnity.z);
                double3 upEcef = math.normalize(camEcef);
                double3 upUnityD = GeoCoordConverter.EcefToUnity(camEcef + upEcef * 10.0) - camUnity;
                Vector3 up = new Vector3((float)upUnityD.x, (float)upUnityD.y, (float)upUnityD.z).normalized;
                Vector3 forward = (tp - _targetCamera.transform.position).normalized;
                forward = Vector3.ProjectOnPlane(forward, up).normalized;
                if (forward.sqrMagnitude < 1e-6f)
                {
                    forward = Vector3.ProjectOnPlane(_targetCamera.transform.forward, up).normalized;
                }
                if (forward.sqrMagnitude < 1e-6f)
                {
                    forward = Vector3.Cross(up, Vector3.right).normalized;
                    if (forward.sqrMagnitude < 1e-6f)
                        forward = Vector3.Cross(up, Vector3.forward).normalized;
                }
                _targetCamera.transform.rotation = Quaternion.LookRotation(forward, up);
            }
        }

        public void FocusOnSouthChinaSea() => FocusOnLocation(114.5, 11.8, 250000.0);

        public void FocusOnEntity(string objectId)
        {
            var e = GetEntity(objectId);
            if (e == null) return;
            _selectedId = objectId;
            float hdgRad = e.HeadingDeg * Mathf.Deg2Rad;
            double behind = 1000.0;
            double up = 200.0;
            double camLat = e.LatitudeDeg - behind * System.Math.Cos(hdgRad) / 111320.0;
            double cosLat = System.Math.Cos(e.LatitudeDeg * (System.Math.PI / 180.0));
            double camLon = e.LongitudeDeg - behind * System.Math.Sin(hdgRad) / (111320.0 * System.Math.Max(0.1, cosLat));
            double camAlt = e.HeightMeters + up;
            FocusOnLocation(camLon, camLat, camAlt, e.LongitudeDeg, e.LatitudeDeg, e.HeightMeters);
        }

        // ============================================================
        // IMGUI Debug
        // ============================================================

        private void OnGUI()
        {
            if (!_initialized) return;

            GUILayout.BeginArea(new Rect(Screen.width - 200, 10, 185, 120));
            GUI.backgroundColor = new Color(0.15f, 0.45f, 0.85f);
            if (GUILayout.Button("Jump to South China Sea", GUILayout.Height(35))) FocusOnSouthChinaSea();
            GUI.backgroundColor = Color.white;
            GUILayout.Space(4);
            bool paused = _motionDriver != null && _motionDriver.IsPaused;
            GUI.backgroundColor = paused ? new Color(0.2f, 0.8f, 0.2f) : new Color(0.9f, 0.3f, 0.2f);
            if (GUILayout.Button(paused ? "Resume Motion" : "Pause Motion", GUILayout.Height(35)))
            {
                if (_motionDriver != null) _motionDriver.IsPaused = !_motionDriver.IsPaused;
            }
            GUILayout.Space(4);
            GUI.backgroundColor = Color.yellow;
            if (GUILayout.Button("Reload Models", GUILayout.Height(35)))
            {
#if UNITY_EDITOR
                UnityEditor.AssetDatabase.Refresh();
#endif
                _modelCache.ReloadAll();
                _viewPool.ReleaseAll();
                for (int i = 0; i < _entityCount; i++)
                {
                    var entry = _viewPool.Acquire(_entities[i]);
                    _worldSpaceMarker.ShowMarker(entry, _entities[i]);
                }
                _anchorMap = _viewPool.GetAnchorMap();
                Debug.Log("[EntityRenderManager] All views rebuilt with reloaded models.");
            }
            GUI.backgroundColor = Color.white;
            GUILayout.EndArea();

            DrawRotationOffsetPanel();

            GUILayout.BeginArea(new Rect(Screen.width - 310, 65, 300, Screen.height - 250));
            GUILayout.Box("Global Entities (WorldSpace Markers)", GUILayout.Width(290));
            foreach (var t in new[] { EntityType.Ship, EntityType.Aircraft, EntityType.Satellite, EntityType.Missile, EntityType.GroundVehicle })
                DrawTypeSection(t);
            GUILayout.EndArea();
        }

        private void DrawTypeSection(EntityType type)
        {
            var list = new List<EntityData>();
            for (int i = 0; i < _entityCount; i++) if (_entities[i]?.Type == type) list.Add(_entities[i]);
            GUILayout.Label($"{type} ({list.Count})");
            foreach (var e in list)
            {
                bool sel = _selectedId == e.ObjectId;
                GUI.backgroundColor = sel ? new Color(0.25f, 0.55f, 0.9f) : Color.white;
                GUILayout.BeginVertical(GUI.skin.box);
                GUILayout.Label($"{e.DisplayName}  [{e.SpeedKnots:F0}kn {e.HeadingDeg:F0}掳]");
                GUILayout.Label($"Lat:{e.LatitudeDeg:F3} Lon:{e.LongitudeDeg:F3} Alt:{e.HeightMeters:F0}m");
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Jump To", GUILayout.Width(140))) { _selectedId = e.ObjectId; FocusOnEntity(e.ObjectId); }
                GUILayout.EndHorizontal();
                GUILayout.EndVertical();
                GUILayout.Space(2);
            }
        }

        private void DrawRotationOffsetPanel()
        {
            var types = new[] { EntityType.Ship, EntityType.Aircraft, EntityType.Satellite, EntityType.Missile, EntityType.GroundVehicle };
            EntityType selType = types[_debugRotTypeIdx % types.Length];

            GUILayout.BeginArea(new Rect(Screen.width - 200, 145, 185, 220));
            GUILayout.Box("Model Rotation Fix", GUILayout.Width(175));

            if (GUILayout.Button($"Type: {selType}", GUILayout.Width(170)))
                _debugRotTypeIdx = (_debugRotTypeIdx + 1) % types.Length;

            var offsets = ModelViewSystem.ModelRotationOffsets;
            Vector3 cur = offsets.TryGetValue(selType, out var o) ? o : Vector3.zero;
            float rx = RotSlider("X", cur.x);
            float ry = RotSlider("Y", cur.y);
            float rz = RotSlider("Z", cur.z);
            offsets[selType] = new Vector3(rx, ry, rz);

            foreach (var kv in _viewPool.ActiveEntries)
            {
                var entry = kv.Value;
                if (entry == null || entry.EntityType != selType || entry.ModelInstance == null) continue;
                var rotationFix = entry.ModelInstance.transform.parent;
                if (rotationFix != null && rotationFix.name == "RotationFix")
                    rotationFix.localRotation = Quaternion.Euler(rx, ry, rz);
            }

            GUILayout.Label($"Current: ({rx:F0}, {ry:F0}, {rz:F0})");
            GUILayout.EndArea();
        }

        private static float RotSlider(string lbl, float v)
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label(lbl, GUILayout.Width(14));
            float r = GUILayout.HorizontalSlider(v, -180f, 180f, GUILayout.Width(120));
            GUILayout.Label($"{r:F0}", GUILayout.Width(35));
            GUILayout.EndHorizontal();
            return r;
        }
    }
}
