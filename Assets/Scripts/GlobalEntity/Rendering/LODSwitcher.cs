using System.Collections.Generic;
using UnityEngine;

namespace CommandP.GlobalEntity.Rendering
{
    /// <summary>
    /// Two-level LOD system.
    ///
    /// Near  (within threshold): ModelRoot visible, MarkerRoot hidden.
    /// Far   (beyond threshold):  ModelRoot hidden,  MarkerRoot visible.
    ///
    /// Per-entity-type distance thresholds are configurable.
    /// MarkerRoot is a WorldSpace child of EntityRoot — it has real position,
    /// is depth-tested, and is naturally occluded by terrain/earth/buildings.
    /// </summary>
    public class LODSwitcher
    {
        private float _defaultLodDistance = 5000f;
        private readonly Dictionary<EntityType, float> _typeDistances = new();

        private readonly WorldSpaceMarkerSystem _markerSystem;
        private readonly ModelViewSystem _modelView;
        private readonly IReadOnlyDictionary<string, GoPoolEntry> _viewEntries;

        public float DefaultLodDistance
        {
            get => _defaultLodDistance;
            set => _defaultLodDistance = Mathf.Max(100f, value);
        }

        public LODSwitcher(
            WorldSpaceMarkerSystem markerSystem,
            ModelViewSystem modelView,
            IReadOnlyDictionary<string, GoPoolEntry> viewEntries)
        {
            _markerSystem = markerSystem;
            _modelView = modelView;
            _viewEntries = viewEntries;

            _typeDistances[EntityType.Ship]          = 6000f;
            _typeDistances[EntityType.Aircraft]      = 10000f;
            _typeDistances[EntityType.Satellite]     = 80000f;
            _typeDistances[EntityType.Missile]       = 4000f;
            _typeDistances[EntityType.GroundVehicle] = 5000f;
        }

        public void SetTypeDistance(EntityType type, float d) => _typeDistances[type] = Mathf.Max(100f, d);

        /// <summary>
        /// Evaluate LOD for all entities. Call every frame from Update.
        /// </summary>
        public void EvaluateAll(IReadOnlyList<EntityData> entities, int count, Camera cam)
        {
            if (cam == null) return;
            Vector3 cp = cam.transform.position;

            for (int i = 0; i < count; i++)
            {
                EntityData e = entities[i];
                if (e == null) continue;
                if (!_viewEntries.TryGetValue(e.ObjectId, out var entry) || entry?.EntityRoot == null) continue;

                float threshold = _typeDistances.TryGetValue(e.Type, out var d) ? d : _defaultLodDistance;
                float sqrDist = (cp - entry.EntityRoot.transform.position).sqrMagnitude;
                bool shouldBeNear = sqrDist < threshold * threshold;

                if (shouldBeNear)
                {
                    if (!e.IsNearLod)
                    {
                        _markerSystem.HideMarker(entry);
                        _modelView.ShowModel(e);
                        e.IsNearLod = true;
                    }
                }
                else
                {
                    if (e.IsNearLod)
                    {
                        _modelView.HideModel(e);
                        _markerSystem.ShowMarker(entry, e);
                        e.IsNearLod = false;
                    }
                }
            }
        }
    }
}
