using System;
using System.Collections.Generic;

namespace CommandP.GlobalEntity.Services
{
    /// <summary>
    /// Catmull-Rom spline interpolation for lat/lon/alt flight paths.
    /// Produces smooth curves through all waypoints with tangent-based heading.
    /// </summary>
    public static class CatmullRomSpline
    {
        /// <summary>
        /// Compute position on the spline at parameter t within segment [segIndex, segIndex+1].
        /// </summary>
        /// <param name="waypoints">Full waypoint list (loop or non-loop)</param>
        /// <param name="segIndex">Segment index (from waypoint[segIndex] to waypoint[segIndex+1])</param>
        /// <param name="t">Parameter 0..1 within this segment</param>
        /// <param name="loop">Whether the path loops back to start</param>
        /// <returns>(lat, lon, alt)</returns>
        public static double[] Evaluate(List<double[]> waypoints, int segIndex, double t, bool loop)
        {
            int n = waypoints.Count;
            GetCatmullRomPoints(waypoints, segIndex, loop, out double[] p0, out double[] p1, out double[] p2, out double[] p3);

            double t2 = t * t;
            double t3 = t2 * t;

            double lat = 0.5 * ((2.0 * p1[0])
                + (-p0[0] + p2[0]) * t
                + (2.0 * p0[0] - 5.0 * p1[0] + 4.0 * p2[0] - p3[0]) * t2
                + (-p0[0] + 3.0 * p1[0] - 3.0 * p2[0] + p3[0]) * t3);

            double lon = 0.5 * ((2.0 * p1[1])
                + (-p0[1] + p2[1]) * t
                + (2.0 * p0[1] - 5.0 * p1[1] + 4.0 * p2[1] - p3[1]) * t2
                + (-p0[1] + 3.0 * p1[1] - 3.0 * p2[1] + p3[1]) * t3);

            double alt = 0.5 * ((2.0 * p1[2])
                + (-p0[2] + p2[2]) * t
                + (2.0 * p0[2] - 5.0 * p1[2] + 4.0 * p2[2] - p3[2]) * t2
                + (-p0[2] + 3.0 * p1[2] - 3.0 * p2[2] + p3[2]) * t3);

            return new double[] { lat, lon, alt };
        }

        /// <summary>
        /// Compute tangent (derivative) on the spline at parameter t within segment.
        /// Returns (dLat/dt, dLon/dt, dAlt/dt) — not normalized.
        /// </summary>
        public static double[] Derivative(List<double[]> waypoints, int segIndex, double t, bool loop)
        {
            GetCatmullRomPoints(waypoints, segIndex, loop, out double[] p0, out double[] p1, out double[] p2, out double[] p3);

            double t2 = t * t;

            double dLat = 0.5 * (
                (-p0[0] + p2[0])
                + (2.0 * p0[0] - 5.0 * p1[0] + 4.0 * p2[0] - p3[0]) * 2.0 * t
                + (-p0[0] + 3.0 * p1[0] - 3.0 * p2[0] + p3[0]) * 3.0 * t2);

            double dLon = 0.5 * (
                (-p0[1] + p2[1])
                + (2.0 * p0[1] - 5.0 * p1[1] + 4.0 * p2[1] - p3[1]) * 2.0 * t
                + (-p0[1] + 3.0 * p1[1] - 3.0 * p2[1] + p3[1]) * 3.0 * t2);

            double dAlt = 0.5 * (
                (-p0[2] + p2[2])
                + (2.0 * p0[2] - 5.0 * p1[2] + 4.0 * p2[2] - p3[2]) * 2.0 * t
                + (-p0[2] + 3.0 * p1[2] - 3.0 * p2[2] + p3[2]) * 3.0 * t2);

            return new double[] { dLat, dLon, dAlt };
        }

        /// <summary>
        /// Compute the approximate arc length of a segment (used to convert speed to dt).
        /// Samples the curve at N points and sums distances.
        /// </summary>
        public static double SegmentArcLength(List<double[]> waypoints, int segIndex, bool loop, int samples = 20)
        {
            double totalDist = 0.0;
            double[] prev = Evaluate(waypoints, segIndex, 0.0, loop);

            for (int i = 1; i <= samples; i++)
            {
                double t = (double)i / samples;
                double[] cur = Evaluate(waypoints, segIndex, t, loop);
                totalDist += DistanceMeters(prev[0], prev[1], cur[0], cur[1]);
                prev = cur;
            }

            return totalDist;
        }

        // ================================================================
        // Internal
        // ================================================================

        private static void GetCatmullRomPoints(
            List<double[]> wp, int segIndex, bool loop,
            out double[] p0, out double[] p1, out double[] p2, out double[] p3)
        {
            int n = wp.Count;
            p1 = wp[segIndex];
            p2 = wp[segIndex + 1];

            if (loop)
            {
                p0 = wp[(segIndex - 1 + n) % n];
                p3 = wp[(segIndex + 2) % n];
            }
            else
            {
                p0 = segIndex > 0 ? wp[segIndex - 1] : wp[segIndex];
                p3 = segIndex + 2 < n ? wp[segIndex + 2] : wp[segIndex + 1];
            }
        }

        /// <summary>
        /// Haversine distance between two lat/lon points (meters).
        /// </summary>
        private static double DistanceMeters(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6371000.0;
            double dLat = (lat2 - lat1) * Math.PI / 180.0;
            double dLon = (lon2 - lon1) * Math.PI / 180.0;
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                     + Math.Cos(lat1 * Math.PI / 180.0) * Math.Cos(lat2 * Math.PI / 180.0)
                     * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            double c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
            return R * c;
        }
    }
}