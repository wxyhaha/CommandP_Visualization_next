using System;
using CesiumForUnity;
using Unity.Mathematics;
using UnityEngine;

namespace CommandP.GlobalEntity.Services
{
    /// <summary>
    /// 坐标转换服务 — LLH/ECEF/Unity 之间转换。
    /// 直接调用 Cesium 1.23.1 公开 API，不使用反射。
    /// </summary>
    public static class GeoCoordConverter
    {
        // WGS84 椭球体参数
        private const double SemiMajorAxis = 6378137.0;
        private const double Flattening = 1.0 / 298.257223563;
        private const double EccentricitySquared = Flattening * (2.0 - Flattening);

        private static CesiumGeoreference _georeference;

        public static void Initialize(CesiumGeoreference georeference)
        {
            _georeference = georeference;
        }

        /// <summary>
        /// 归一化经度到 [-180, 180]
        /// </summary>
        public static double NormalizeLongitude(double lon)
        {
            lon = (lon + 180.0) % 360.0;
            if (lon < 0) lon += 360.0;
            return lon - 180.0;
        }

        /// <summary>
        /// LLH → ECEF (WGS84 椭球体，纯数学)
        /// </summary>
        public static double3 LlhToEcefWgs84(double longitudeDeg, double latitudeDeg, double heightMeters)
        {
            double latRad = latitudeDeg * (Math.PI / 180.0);
            double lonRad = longitudeDeg * (Math.PI / 180.0);

            double sinLat = Math.Sin(latRad);
            double cosLat = Math.Cos(latRad);
            double sinLon = Math.Sin(lonRad);
            double cosLon = Math.Cos(lonRad);

            double n = SemiMajorAxis / Math.Sqrt(1.0 - EccentricitySquared * sinLat * sinLat);

            double x = (n + heightMeters) * cosLat * cosLon;
            double y = (n + heightMeters) * cosLat * sinLon;
            double z = (n * (1.0 - EccentricitySquared) + heightMeters) * sinLat;

            return new double3(x, y, z);
        }

        /// <summary>
        /// LLH → Unity 世界坐标 (ECEF 中转)
        /// </summary>
        public static double3 LlhToUnity(double longitudeDeg, double latitudeDeg, double heightMeters)
        {
            if (_georeference == null)
            {
                Debug.LogError("[GeoCoordConverter] CesiumGeoreference not initialized.");
                return double3.zero;
            }

            double3 ecef = LlhToEcefWgs84(longitudeDeg, latitudeDeg, heightMeters);
            return _georeference.TransformEarthCenteredEarthFixedPositionToUnity(ecef);
        }

        /// <summary>
        /// ECEF → Unity 世界坐标 (直接 Cesium API)
        /// </summary>
        public static double3 EcefToUnity(double3 ecef)
        {
            if (_georeference == null)
            {
                Debug.LogError("[GeoCoordConverter] CesiumGeoreference not initialized.");
                return double3.zero;
            }

            return _georeference.TransformEarthCenteredEarthFixedPositionToUnity(ecef);
        }

        /// <summary>
        /// 批量刷新 ECEF 缓存，仅处理 dirty 的实体
        /// </summary>
        public static void RefreshEcef(EntityData[] entities, int count)
        {
            for (int i = 0; i < count; i++)
            {
                EntityData e = entities[i];
                if (e == null || !e.EcefDirty) continue;

                e.EcefPosition = LlhToEcefWgs84(e.LongitudeDeg, e.LatitudeDeg, e.HeightMeters);
                e.EcefDirty = false;
            }
        }

        /// <summary>
        /// 计算两个经纬度之间的地面距离 (Haversine 公式, 米)
        /// </summary>
        public static double GroundDistanceMeters(double lat1Deg, double lon1Deg, double lat2Deg, double lon2Deg)
        {
            const double R = 6371000.0;
            double lat1 = lat1Deg * (Math.PI / 180.0);
            double lat2 = lat2Deg * (Math.PI / 180.0);
            double dLat = (lat2Deg - lat1Deg) * (Math.PI / 180.0);
            double dLon = (lon2Deg - lon1Deg) * (Math.PI / 180.0);

            double a = Math.Sin(dLat / 2.0) * Math.Sin(dLat / 2.0)
                     + Math.Cos(lat1) * Math.Cos(lat2)
                     * Math.Sin(dLon / 2.0) * Math.Sin(dLon / 2.0);
            double c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
            return R * c;
        }

        /// <summary>
        /// 按 ground heading (deg) 和 distance (m) 推进一个经纬度点
        /// </summary>
        public static void AdvancePosition(
            ref double lonDeg, ref double latDeg,
            float headingDeg, float speedKnots, float deltaTimeSec)
        {
            double speedMs = speedKnots * 0.514444;
            double distanceM = speedMs * deltaTimeSec;

            double headingRad = headingDeg * (Math.PI / 180.0);
            double latRad = latDeg * (Math.PI / 180.0);

            // 1 纬度 ≈ 111320 米
            double deltaLatDeg = (distanceM * Math.Cos(headingRad)) / 111320.0;
            double deltaLonDeg = (distanceM * Math.Sin(headingRad)) / (111320.0 * Math.Max(0.1, Math.Cos(latRad)));

            latDeg += deltaLatDeg;
            lonDeg += deltaLonDeg;
            lonDeg = NormalizeLongitude(lonDeg);
        }

        /// <summary>
        /// 计算两点之间的方位角 (从 from 指向 to, 度)
        /// </summary>
        public static float BearingTo(double fromLatDeg, double fromLonDeg, double toLatDeg, double toLonDeg)
        {
            double lat1 = fromLatDeg * (Math.PI / 180.0);
            double lat2 = toLatDeg * (Math.PI / 180.0);
            double dLon = (toLonDeg - fromLonDeg) * (Math.PI / 180.0);

            double y = Math.Sin(dLon) * Math.Cos(lat2);
            double x = Math.Cos(lat1) * Math.Sin(lat2) - Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(dLon);
            double heading = Math.Atan2(y, x) * (180.0 / Math.PI);

            heading = heading % 360.0;
            if (heading < 0) heading += 360.0;
            return (float)heading;
        }
    }
}
