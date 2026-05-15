using CommandP.GlobalEntity.Services;
using UnityEngine;

namespace CommandP.GlobalEntity.Data
{
    /// <summary>
    /// 实体运动驱动器: 每帧根据 heading + speed 推进所有实体的 LLH。
    /// 飞机/舰船: 直线运动
    /// 卫星: 圆轨道计算
    /// 地面车辆: 静止
    /// </summary>
    public class EntityMotionDriver : MonoBehaviour
    {
        private EntityData[] _entities;
        private int _entityCount;
        private bool _isPaused;

        // WGS84 地球参数
        private const double EarthRadiusMeters = 6378137.0;
        private const double GravitationalParameter = 3.986004418e14;

        public bool IsPaused
        {
            get => _isPaused;
            set => _isPaused = value;
        }

        public void Initialize(EntityData[] entities, int count)
        {
            _entities = entities;
            _entityCount = count;
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

        /// <summary>
        /// 直线运动 (飞机/舰船/导弹/地面车辆)
        /// </summary>
        private static void AdvanceLinear(EntityData e, float dt)
        {
            // 1 knot = 0.514444 m/s, 1 deg lat ≈ 111320 m
            double speedMs = e.SpeedKnots * 0.514444;
            double distanceM = speedMs * dt;

            double headingRad = e.HeadingDeg * (Mathf.Deg2Rad);
            double cosLat = System.Math.Cos(e.LatitudeDeg * (System.Math.PI / 180.0));

            double deltaLatDeg = (distanceM * System.Math.Cos(headingRad)) / 111320.0;
            double deltaLonDeg = (distanceM * System.Math.Sin(headingRad))
                / (111320.0 * System.Math.Max(0.1, cosLat));

            e.LatitudeDeg += deltaLatDeg;
            e.LongitudeDeg += deltaLonDeg;
            e.LongitudeDeg = GeoCoordConverter.NormalizeLongitude(e.LongitudeDeg);
            e.EcefDirty = true;
        }

        /// <summary>
        /// 卫星轨道运动 (圆轨道 + 地球自转)
        /// 按固定轨道参数计算每一帧的 LLH，不使用 heading/speed 的直线推进。
        /// </summary>
        private static void AdvanceSatellite(EntityData e, float dt)
        {
            if (!e.HasOrbitParams) return;

            double altitudeM = e.OrbitAltitudeKm * 1000.0;
            double orbitRadiusM = EarthRadiusMeters + altitudeM;

            // 轨道周期
            double periodSec = 2.0 * System.Math.PI
                * System.Math.Sqrt(System.Math.Pow(orbitRadiusM, 3.0) / GravitationalParameter);

            // 角速度
            double angularSpeedRadPerSec = (2.0 * System.Math.PI) / periodSec;

            // 每帧推进相位
            e.OrbitPhaseDeg += angularSpeedRadPerSec * dt * (180.0 / System.Math.PI);
            e.OrbitPhaseDeg = e.OrbitPhaseDeg % 360.0;
            if (e.OrbitPhaseDeg < 0) e.OrbitPhaseDeg += 360.0;

            // 地球自转补偿 (RAAN 随时间漂移 ~15.04 deg/hour)
            double earthRotationDegPerSec = 15.041067 / 3600.0;
            e.OrbitRaanDeg -= earthRotationDegPerSec * dt;
            e.OrbitRaanDeg = e.OrbitRaanDeg % 360.0;
            if (e.OrbitRaanDeg < 0) e.OrbitRaanDeg += 360.0;

            // 计算 ECEF 位置
            double phaseRad = e.OrbitPhaseDeg * (System.Math.PI / 180.0);
            double inclinationRad = e.OrbitInclinationDeg * (System.Math.PI / 180.0);
            double raanRad = e.OrbitRaanDeg * (System.Math.PI / 180.0);

            // 轨道平面坐标
            double xOrb = orbitRadiusM * System.Math.Cos(phaseRad);
            double yOrb = orbitRadiusM * System.Math.Sin(phaseRad);

            // 绕 X 轴旋转倾角
            double cosInc = System.Math.Cos(inclinationRad);
            double sinInc = System.Math.Sin(inclinationRad);
            double yAfterInc = yOrb * cosInc;
            double zAfterInc = yOrb * sinInc;

            // 绕 Z 轴旋转 RAAN
            double cosRaan = System.Math.Cos(raanRad);
            double sinRaan = System.Math.Sin(raanRad);
            double ecefX = xOrb * cosRaan - yAfterInc * sinRaan;
            double ecefY = xOrb * sinRaan + yAfterInc * cosRaan;
            double ecefZ = zAfterInc;

            // ECEF → LLH
            EcefToLlhWgs84(ecefX, ecefY, ecefZ,
                out double latitudeDeg, out double longitudeDeg, out double heightMeters);

            e.LatitudeDeg = latitudeDeg;
            e.LongitudeDeg = longitudeDeg;
            e.HeightMeters = heightMeters;

            // 计算 heading (沿轨道切向)
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
                out double aheadLat, out double aheadLon, out _);

            e.HeadingDeg = GeoCoordConverter.BearingTo(
                latitudeDeg, longitudeDeg, aheadLat, aheadLon);
            e.SpeedKnots = (float)(angularSpeedRadPerSec * orbitRadiusM * 1.9438444924406);
            e.EcefDirty = true;
        }

        /// <summary>
        /// ECEF → LLH (WGS84 椭球体, Newton-Raphson 迭代)
        /// </summary>
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
