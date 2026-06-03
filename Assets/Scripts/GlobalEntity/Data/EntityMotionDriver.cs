using CommandP.GlobalEntity.Services;
using UnityEngine;

namespace CommandP.GlobalEntity.Data
{
    /// <summary>
    /// Entity motion driver.
    /// - Aircraft with Waypoints: Catmull-Rom spline flight, heading from curve tangent.
    /// - Aircraft without Waypoints / Ships / Missiles: straight-line (heading + speed).
    /// - Satellites: orbital mechanics.
    /// - Ground Vehicles: stationary.
    /// </summary>
    public class EntityMotionDriver : MonoBehaviour
    {
        private EntityData[] _entities;
        private int _entityCount;
        private bool _isPaused;

        private const double EarthRadiusMeters = 6378137.0;
        private const double GravitationalParameter = 3.986004418e14;

        /// <summary>
        /// Threshold distance (meters) to consider a waypoint reached.
        /// </summary>
        private const double WaypointReachThreshold = 500.0;

        public bool IsPaused
        {
            get => _isPaused;
            set => _isPaused = value;
        }

        public void Initialize(EntityData[] entities, int count)
        {
            _entities = entities;
            _entityCount = count;

            // Precompute segment arc lengths for all spline entities
            for (int i = 0; i < count; i++)
            {
                EntityData e = entities[i];
                if (e == null || e.Waypoints == null || e.Waypoints.Count < 2) continue;
                e.CurrentSegmentIndex = 0;
                e.SegmentProgress = 0.0;
            }
        }

        private void Update()
        {
            if (_isPaused || _entities == null || _entityCount == 0)
                return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            for (int i = 0; i < _entityCount; i++)
            {
                EntityData e = _entities[i];
                if (e == null || e.SpeedKnots <= 0f) continue;

                if (e.Waypoints != null && e.Waypoints.Count >= 2)
                {
                    AdvanceSpline(e, dt);
                }
                else
                {
                    switch (e.Type)
                    {
                        case EntityType.Satellite:
                            AdvanceSatellite(e, dt);
                            break;
                        default:
                            AdvanceLinear(e, dt);
                            break;
                    }
                }
            }
        }

        // ================================================================
        // Spline-based flight (aircraft with waypoints)
        // ================================================================

        private void AdvanceSpline(EntityData e, float dt)
        {
            var wp = e.Waypoints;
            int segCount = wp.Count - 1;
            bool loop = (wp.Count >= 3); // loop if 3+ waypoints

            int seg = e.CurrentSegmentIndex;
            if (seg >= segCount)
            {
                // Reached end — for loop, wrap around; otherwise stop
                if (loop)
                {
                    seg = 0;
                    e.CurrentSegmentIndex = 0;
                    e.SegmentProgress = 0.0;
                }
                else
                {
                    return; // stop at last waypoint
                }
            }

            // Compute segment arc length for speed-based progression
            double arcLen = CatmullRomSpline.SegmentArcLength(wp, seg, loop);
            if (arcLen < 1.0) arcLen = 1.0; // avoid division by zero

            double speedMs = e.SpeedKnots * 0.514444;
            double dtParam = (speedMs * dt) / arcLen; // parameter advancement per frame

            e.SegmentProgress += dtParam;

            // Check if we've passed the current segment
            if (e.SegmentProgress >= 1.0)
            {
                e.SegmentProgress -= 1.0;
                e.CurrentSegmentIndex++;

                // If wrapped past last segment
                if (e.CurrentSegmentIndex >= segCount)
                {
                    if (loop)
                    {
                        e.CurrentSegmentIndex = 0;
                    }
                    else
                    {
                        e.CurrentSegmentIndex = segCount - 1;
                        e.SegmentProgress = 1.0;
                    }
                }
            }

            // Evaluate position on spline
            double[] pos = CatmullRomSpline.Evaluate(wp, e.CurrentSegmentIndex, e.SegmentProgress, loop);
            e.LatitudeDeg = pos[0];
            e.LongitudeDeg = pos[1];
            e.HeightMeters = pos[2];
            e.LongitudeDeg = GeoCoordConverter.NormalizeLongitude(e.LongitudeDeg);
            e.EcefDirty = true;

            // Compute heading from tangent
            double[] tangent = CatmullRomSpline.Derivative(wp, e.CurrentSegmentIndex, e.SegmentProgress, loop);
            // tangent is (dLat/dt, dLon/dt) — convert to bearing
            double avgLat = pos[0];
            double cosLat = System.Math.Cos(avgLat * System.Math.PI / 180.0);
            double northComponent = tangent[0]; // dLat
            double eastComponent = tangent[1] * System.Math.Max(0.1, cosLat); // dLon adjusted for latitude
            e.HeadingDeg = (float)(System.Math.Atan2(eastComponent, northComponent) * 180.0 / System.Math.PI);
            if (e.HeadingDeg < 0f) e.HeadingDeg += 360f;
        }

        // ================================================================
        // Straight-line flight (ships, missiles, aircraft without waypoints)
        // ================================================================

        private static void AdvanceLinear(EntityData e, float dt)
        {
            double speedMs = e.SpeedKnots * 0.514444;
            double distanceM = speedMs * dt;

            double headingRad = e.HeadingDeg * (Mathf.Deg2Rad);
            double cosLat = System.Math.Cos(e.LatitudeDeg * (System.Math.PI / 180.0));

            double deltaLatDeg = (distanceM * System.Math.Cos(headingRad)) / 111320.0;
            double deltaLonDeg = (distanceM * System.Math.Sin(headingRad))
                / (111320.0 * System.Math.Max(0.1, cosLat));

            double oldLat = e.LatitudeDeg;
            double oldLon = e.LongitudeDeg;

            e.LatitudeDeg += deltaLatDeg;
            e.LongitudeDeg += deltaLonDeg;
            e.LongitudeDeg = GeoCoordConverter.NormalizeLongitude(e.LongitudeDeg);
            e.EcefDirty = true;

            e.HeadingDeg = GeoCoordConverter.BearingTo(
                oldLat, oldLon, e.LatitudeDeg, e.LongitudeDeg);
        }

        // ================================================================
        // Satellite orbital mechanics
        // ================================================================

        private static void AdvanceSatellite(EntityData e, float dt)
        {
            if (!e.HasOrbitParams) return;

            double altitudeM = e.OrbitAltitudeKm * 1000.0;
            double orbitRadiusM = EarthRadiusMeters + altitudeM;

            double periodSec = 2.0 * System.Math.PI
                * System.Math.Sqrt(System.Math.Pow(orbitRadiusM, 3.0) / GravitationalParameter);

            double angularSpeedRadPerSec = (2.0 * System.Math.PI) / periodSec;

            e.OrbitPhaseDeg += angularSpeedRadPerSec * dt * (180.0 / System.Math.PI);
            e.OrbitPhaseDeg = e.OrbitPhaseDeg % 360.0;
            if (e.OrbitPhaseDeg < 0) e.OrbitPhaseDeg += 360.0;

            double earthRotationDegPerSec = 15.041067 / 3600.0;
            e.OrbitRaanDeg -= earthRotationDegPerSec * dt;
            e.OrbitRaanDeg = e.OrbitRaanDeg % 360.0;
            if (e.OrbitRaanDeg < 0) e.OrbitRaanDeg += 360.0;

            double phaseRad = e.OrbitPhaseDeg * (System.Math.PI / 180.0);
            double inclinationRad = e.OrbitInclinationDeg * (System.Math.PI / 180.0);
            double raanRad = e.OrbitRaanDeg * (System.Math.PI / 180.0);

            double xOrb = orbitRadiusM * System.Math.Cos(phaseRad);
            double yOrb = orbitRadiusM * System.Math.Sin(phaseRad);

            double cosInc = System.Math.Cos(inclinationRad);
            double sinInc = System.Math.Sin(inclinationRad);
            double yAfterInc = yOrb * cosInc;
            double zAfterInc = yOrb * sinInc;

            double cosRaan = System.Math.Cos(raanRad);
            double sinRaan = System.Math.Sin(raanRad);
            double ecefX = xOrb * cosRaan - yAfterInc * sinRaan;
            double ecefY = xOrb * sinRaan + yAfterInc * cosRaan;
            double ecefZ = zAfterInc;

            EcefToLlhWgs84(ecefX, ecefY, ecefZ,
                out double latitudeDeg, out double longitudeDeg, out double heightMeters);

            e.LatitudeDeg = latitudeDeg;
            e.LongitudeDeg = longitudeDeg;
            e.HeightMeters = heightMeters;

            double lookAheadSec = 2.0;
            double aheadPhaseDeg = e.OrbitPhaseDeg + angularSpeedRadPerSec * lookAheadSec * (180.0 / System.Math.PI);
            double aheadPhaseRad = aheadPhaseDeg * (System.Math.PI / 180.0);
            double axOrb = orbitRadiusM * System.Math.Cos(aheadPhaseRad);
            double ayOrb = orbitRadiusM * System.Math.Sin(aheadPhaseRad);
            double ayInc = ayOrb * cosInc;
            double azInc = ayOrb * sinInc;
            double aEcefX = axOrb * cosRaan - ayInc * sinRaan;
            double aEcefY = axOrb * sinRaan + ayInc * cosRaan;
            double aEcefZ = azInc;

            EcefToLlhWgs84(aEcefX, aEcefY, aEcefZ,
                out double aheadLat, out double aheadLon, out double _);

            e.HeadingDeg = GeoCoordConverter.BearingTo(
                latitudeDeg, longitudeDeg, aheadLat, aheadLon);
            e.SpeedKnots = (float)(angularSpeedRadPerSec * orbitRadiusM * 1.9438444924406);
            e.EcefDirty = true;
        }

        private static void EcefToLlhWgs84(
            double x, double y, double z,
            out double latitudeDeg, out double longitudeDeg, out double heightMeters)
        {
            const double semiMajorAxis = 6378137.0;
            const double flattening = 1.0 / 298.257223563;
            double eccentricitySquared = flattening * (2.0 - flattening);

            longitudeDeg = System.Math.Atan2(y, x) * (180.0 / System.Math.PI);

            double p = System.Math.Sqrt(x * x + y * y);
            double latitude = System.Math.Atan2(z, p * (1.0 - eccentricitySquared));
            double latitudePrev;
            heightMeters = 0.0;

            do
            {
                latitudePrev = latitude;
                double sinLat = System.Math.Sin(latitude);
                double n = semiMajorAxis / System.Math.Sqrt(1.0 - eccentricitySquared * sinLat * sinLat);
                heightMeters = p / System.Math.Max(1e-9, System.Math.Cos(latitude)) - n;
                latitude = System.Math.Atan2(z,
                    p * (1.0 - eccentricitySquared * n / (n + heightMeters)));
            }
            while (System.Math.Abs(latitude - latitudePrev) > 1e-12);

            latitudeDeg = latitude * (180.0 / System.Math.PI);
        }
    }
}