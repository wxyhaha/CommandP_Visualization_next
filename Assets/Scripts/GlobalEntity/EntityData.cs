using System.Collections.Generic;
using Unity.Mathematics;

namespace CommandP.GlobalEntity
{
    /// <summary>
    /// Entity data — pure data layer. Unity Transform is never the source of truth.
    /// All geographic coordinates use double precision.
    /// </summary>
    public class EntityData
    {
        public string ObjectId;
        public string DisplayName;
        public EntityType Type;
        public int SideId;

        // Geographic coordinates (WGS84, double precision)
        public double LongitudeDeg;
        public double LatitudeDeg;
        public double HeightMeters;

        // Motion state
        public float HeadingDeg;
        public float SpeedKnots;

        // Satellite orbit (only meaningful when Type == Satellite)
        public double OrbitAltitudeKm;
        public double OrbitInclinationDeg;
        public double OrbitRaanDeg;
        public double OrbitPhaseDeg;
        public bool HasOrbitParams;

        // LOD state
        public bool IsNearLod;

        // Model asset key
        public string ModelAssetKey;

        // Icon key (empty = default icon by EntityType, e.g. "submarine")
        public string IconKey;

        // ECEF cache
        public double3 EcefPosition;
        public bool EcefDirty;

        // Waypoint flight path
        public List<double[]> Waypoints; // [lat, lon, alt] per waypoint
        public int CurrentSegmentIndex;  // current segment index
        public double SegmentProgress;   // 0..1 progress within current segment

        public EntityData()
        {
            EcefDirty = true;
        }
    }
}