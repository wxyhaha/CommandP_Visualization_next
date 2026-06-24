using System.Collections.Generic;
using CesiumForUnity;
using CommandP.GlobalEntity.Rendering;
using Unity.Mathematics;
using UnityEngine;

namespace CommandP.GlobalEntity.Rendering
{
    /// <summary>
    /// Trail system using ECEF position history + LineRenderer.
    ///
    /// Why ECEF: Cesium OriginShift re-anchors the Unity world each frame around the
    /// camera, so Unity world positions of old trail vertices become meaningless.
    /// ECEF is an Earth-fixed frame — old points stay valid regardless of OriginShift.
    ///
    /// Each frame:
    ///   1. For each entity with enabled trail type, if EcefDirty:
    ///      - Compute ECEF distance from last sample
    ///      - If >= sample distance threshold, push EcefPosition into history
    ///      - Cap history length at TrailMaxPoints (drop oldest)
    ///   2. Convert all history ECEF → Unity world, set on LineRenderer
    /// </summary>
    public class EntityTrailSystem
    {
        private readonly CesiumGeoreference _georeference;
        private readonly IReadOnlyDictionary<string, GoPoolEntry> _viewEntries;
        private Vector3[] _unityArray = new Vector3[512];

        public EntityTrailSystem(CesiumGeoreference georeference, IReadOnlyDictionary<string, GoPoolEntry> viewEntries)
        {
            _georeference = georeference;
            _viewEntries = viewEntries;
        }

        /// <summary>
        /// Sample ECEF history from dirty entities. Call after motion update +
        /// GeoCoordConverter.RefreshEcef, before rendering.
        /// </summary>
        public void SampleHistory(IReadOnlyList<EntityData> entities, int count)
        {
            for (int i = 0; i < count; i++)
            {
                EntityData e = entities[i];
                if (e == null) continue;
                if (!ViewPool.TrailTypes.Contains(e.Type)) continue;
                if (e.EcefTrailHistory == null)
                {
                    e.EcefTrailHistory = new List<double3>();
                    e.LastTrailSampleEcef = e.EcefPosition;
                }

                // First sample: seed with current position
                if (e.EcefTrailHistory.Count == 0)
                {
                    e.EcefTrailHistory.Add(e.EcefPosition);
                    e.LastTrailSampleEcef = e.EcefPosition;
                    e.TrailAccumulatedDistanceM = 0.0;
                    continue;
                }

                double dx = e.EcefPosition.x - e.LastTrailSampleEcef.x;
                double dy = e.EcefPosition.y - e.LastTrailSampleEcef.y;
                double dz = e.EcefPosition.z - e.LastTrailSampleEcef.z;
                double dist = System.Math.Sqrt(dx * dx + dy * dy + dz * dz);

                float threshold = ViewPool.TrailSampleDistanceMeters.TryGetValue(e.Type, out var s) ? s : 10f;
                if (dist < threshold) continue;

                e.EcefTrailHistory.Add(e.EcefPosition);
                e.LastTrailSampleEcef = e.EcefPosition;

                int maxPts = ViewPool.TrailMaxPoints.TryGetValue(e.Type, out var m) ? m : 300;
                while (e.EcefTrailHistory.Count > maxPts)
                    e.EcefTrailHistory.RemoveAt(0);
            }
        }

        /// <summary>
        /// Refresh LineRenderer positions from ECEF history. Call every frame
        /// (Cesium OriginShift means Unity positions change even when ECEF doesn't).
        ///
        /// Note: LineRenderer.useWorldSpace=true means positions are absolute world
        /// coords — TrailPivot's localPosition does NOT lift the trail. We apply the
        /// per-type height offset here by offsetting along ENU Up at each sample.
        /// </summary>
        public void RefreshLineRenderers(IReadOnlyList<EntityData> entities, int count)
        {
            if (_georeference == null) return;

            for (int i = 0; i < count; i++)
            {
                EntityData e = entities[i];
                if (e == null) continue;
                if (!ViewPool.TrailTypes.Contains(e.Type)) continue;
                if (e.EcefTrailHistory == null || e.EcefTrailHistory.Count == 0) continue;
                if (!_viewEntries.TryGetValue(e.ObjectId, out var entry) || entry == null) continue;
                if (entry.Trail == null || !entry.Trail.enabled) continue;

                int n = e.EcefTrailHistory.Count;
                if (_unityArray.Length < n) _unityArray = new Vector3[n];

                // Per-type height offset (ENU Up) — applied in Unity space by walking
                // up along EntityRoot's local Y, which GlobeAnchor aligns to ENU Up.
                float heightOff = ViewPool.TrailHeightOffsets.TryGetValue(e.Type, out var h) ? h : 0f;
                Vector3 upDir = entry.EntityRoot != null ? entry.EntityRoot.transform.up : Vector3.up;
                Vector3 upOffset = upDir * heightOff;

                for (int j = 0; j < n; j++)
                {
                    double3 u = _georeference.TransformEarthCenteredEarthFixedPositionToUnity(e.EcefTrailHistory[j]);
                    _unityArray[j] = new Vector3((float)u.x, (float)u.y, (float)u.z) + upOffset;
                }

                entry.Trail.positionCount = n;
                entry.Trail.SetPositions(_unityArray);
            }
        }
    }
}
