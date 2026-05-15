using System;
using System.Collections.Generic;
using CesiumForUnity;
using CommandP.GlobalEntity.Services;
using TMPro;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CommandP.GlobalEntity.Rendering
{
    /// <summary>
    /// Pool entry — EntityRoot is the single source of world position (via GlobeAnchor).
    ///
    /// Hierarchy:
    ///   EntityRoot (CesiumGlobeAnchor + EntityHandle)
    ///   ├── ModelRoot   (3D GLB, LOD near)
    ///   └── MarkerRoot  (WorldSpace tactical marker, LOD far)
    ///       ├── IconQuad
    ///       └── WorldSpaceCanvas / LabelText
    /// </summary>
    public class GoPoolEntry
    {
        public string ObjectId;
        public GameObject EntityRoot;
        public GameObject ModelRoot;
        public CesiumGlobeAnchor Anchor;
        public EntityHandle Handle;
        public GameObject ModelInstance;
        public bool IsActive;
        public EntityType EntityType;

        // Marker subsystem — configured by WorldSpaceMarkerSystem
        public GameObject MarkerRoot;
        public MeshRenderer IconRenderer;
        public TextMeshProUGUI MarkerLabel;
        public BillboardMarker Billboard;
    }

    public class ViewPool
    {
        private readonly Transform _parentTransform;
        private readonly Dictionary<string, GoPoolEntry> _active = new();
        private readonly Stack<GoPoolEntry> _inactive = new();
        private readonly List<GoPoolEntry> _poolEntryCache = new(256);

        private const int InitialPoolSize = 64;
        private const int MaxPoolSize = 2000;

        public IReadOnlyDictionary<string, GoPoolEntry> ActiveEntries => _active;

        public ViewPool(Transform parentTransform)
        {
            _parentTransform = parentTransform;
            Prewarm(InitialPoolSize);
        }

        public Dictionary<string, CesiumGlobeAnchor> GetAnchorMap()
        {
            var map = new Dictionary<string, CesiumGlobeAnchor>(_active.Count);
            foreach (var kv in _active) map[kv.Key] = kv.Value.Anchor;
            return map;
        }

        public GoPoolEntry Acquire(EntityData entity)
        {
            if (_active.TryGetValue(entity.ObjectId, out var existing))
            { Reconfigure(existing, entity); return existing; }

            var entry = _inactive.Count > 0 ? _inactive.Pop() : CreateNew(entity);
            Reconfigure(entry, entity);
            entry.IsActive = true;
            entry.ObjectId = entity.ObjectId;
            entry.EntityRoot.SetActive(true);
            _active[entity.ObjectId] = entry;
            return entry;
        }

        public void Release(string objectId)
        {
            if (!_active.TryGetValue(objectId, out var entry)) return;
            _active.Remove(objectId);
            entry.IsActive = false;
            entry.ObjectId = null;

            if (entry.ModelInstance != null) { Object.Destroy(entry.ModelInstance); entry.ModelInstance = null; }
            if (entry.Anchor != null) entry.Anchor.enabled = false;
            entry.EntityRoot.SetActive(false);
            entry.EntityRoot.name = "[POOLED]";

            if (_inactive.Count < MaxPoolSize)
                _inactive.Push(entry);
            else
                Object.Destroy(entry.EntityRoot);
        }

        public void ReleaseAll()
        {
            _poolEntryCache.Clear();
            _poolEntryCache.AddRange(_active.Values);
            foreach (var e in _poolEntryCache)
                if (e.ObjectId != null)
                    Release(e.ObjectId);
        }

        // ============================================================
        // Internal
        // ============================================================

        private void Prewarm(int count)
        {
            for (int i = 0; i < Mathf.Min(count, MaxPoolSize); i++)
            {
                var entry = CreateNew(null);
                entry.IsActive = false;
                entry.EntityRoot.SetActive(false);
                if (entry.Anchor != null) entry.Anchor.enabled = false;
                _inactive.Push(entry);
            }
        }

        private GoPoolEntry CreateNew(EntityData template)
        {
            var root = new GameObject("EntityRoot");
            root.transform.SetParent(_parentTransform, false);

            var anchor = root.AddComponent<CesiumGlobeAnchor>();
            anchor.adjustOrientationForGlobeWhenMoving = false;
            anchor.detectTransformChanges = false;

            var handle = root.AddComponent<EntityHandle>();

            if (template != null)
            {
                anchor.longitudeLatitudeHeight = new Unity.Mathematics.double3(
                    GeoCoordConverter.NormalizeLongitude(template.LongitudeDeg),
                    template.LatitudeDeg, template.HeightMeters);
            }

            // ModelRoot — 3D model container (LOD near)
            var modelRoot = new GameObject("ModelRoot");
            modelRoot.transform.SetParent(root.transform, false);
            modelRoot.transform.localPosition = Vector3.zero;
            modelRoot.SetActive(false);

            // MarkerRoot — World Space tactical marker (LOD far)
            var markerRoot = new GameObject("MarkerRoot");
            markerRoot.transform.SetParent(root.transform, false);
            var billboard = markerRoot.AddComponent<BillboardMarker>();
            markerRoot.SetActive(false);

            return new GoPoolEntry
            {
                EntityRoot  = root,
                ModelRoot   = modelRoot,
                Anchor      = anchor,
                Handle      = handle,
                EntityType  = template?.Type ?? EntityType.Ship,
                MarkerRoot  = markerRoot,
                Billboard   = billboard,
            };
        }

        private void Reconfigure(GoPoolEntry entry, EntityData entity)
        {
            entry.EntityRoot.name = $"Entity_{entity.ObjectId}";
            entry.EntityRoot.SetActive(true);
            entry.EntityType = entity.Type;

            if (entry.Anchor != null)
            {
                entry.Anchor.enabled = true;
                entry.Anchor.longitudeLatitudeHeight = new Unity.Mathematics.double3(
                    GeoCoordConverter.NormalizeLongitude(entity.LongitudeDeg),
                    entity.LatitudeDeg, entity.HeightMeters);
            }

            if (entry.Handle != null)
            {
                entry.Handle.ObjectId = entity.ObjectId;
                entry.Handle.EntityType = entity.Type;
            }
        }
    }
}
