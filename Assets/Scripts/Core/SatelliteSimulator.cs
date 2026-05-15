using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using CommandP.Data.DTOs;
using CommandP.Data.Stores;

namespace CommandP.Core
{
    /// <summary>
    /// 卫星轨迹模拟器：用简化圆轨道生成绕地球飞行的数据。
    /// 数据按帧推进，并周期性写入 UnitStore，供视图层平滑插值。
    /// </summary>
    public class SatelliteSimulator : MonoBehaviour
    {
        public struct OrbitSampleView
        {
            public double LatitudeDeg;
            public double LongitudeDeg;
            public double AltitudeMeters;

            public OrbitSampleView(double latitudeDeg, double longitudeDeg, double altitudeMeters)
            {
                LatitudeDeg = latitudeDeg;
                LongitudeDeg = longitudeDeg;
                AltitudeMeters = altitudeMeters;
            }
        }

        [Header("卫星信息")]
        [SerializeField] private string _satelliteId = "SATELLITE_SIM_001";
        [SerializeField] private string _satelliteName = "LEO Observation Satellite";

        [Header("轨道参数")]
        [SerializeField] private float _orbitalAltitudeKm = 200f;
        [SerializeField] private float _inclinationDeg = 45f;
        [SerializeField] private float _raanDeg = 0f;
        [SerializeField] private float _initialPhaseDeg = 0f;
        [SerializeField] private bool _alignOrbitToCenter = true;
        [SerializeField] private bool _autoComputePeriod = true;
        [SerializeField] private float _orbitalPeriodMinutes = 95f;
        [SerializeField] private float _earthRotationDegPerHour = 15.041067f;
        [SerializeField] private int _orbitSamples = 720;
        [SerializeField] private float _headingLookAheadSeconds = 2f;

        [Header("轨道中心（经纬度，用于将轨道定位到飞机上方）")]
        [SerializeField] private double _orbitCenterLatitude = 39.736401;
        [SerializeField] private double _orbitCenterLongitude = -105.25737;

        [Header("推送频率")]
        [SerializeField] private float _pushInterval = 0.05f;

        private UnitStore _unitStore;
        private float _pushTimer;
        private bool _isPaused;
        private double _missionTimeSec;
        private bool _initialized;
        private double _orbitLengthMeters;
        private double _orbitalPeriodSec;
        private double _orbitalSpeedMetersPerSec;
        private double _distanceAlongOrbitMeters;
        private double _phaseOffsetRad;
        private double _alignedInclinationDeg;
        private double _alignedRaanDeg;

        private readonly List<OrbitSample> _orbitSamplesCache = new List<OrbitSample>();
        private readonly List<double3> _orbitEcefCache = new List<double3>();  // 轨道线ECEF点缓存（唯一数据源）

        private const double EarthRadiusMeters = 6378137.0;
        private const double EarthFlattening = 1.0 / 298.257223563;

        private struct OrbitSample
        {
            public double LatitudeDeg;
            public double LongitudeDeg;
            public double AltitudeMeters;
            public double CumulativeMeters;
        }

        private void Start()
        {
            if (AppManager.Instance != null)
            {
                _unitStore = AppManager.Instance.GetUnitStore();
            }

            BuildOrbitPath();
            _initialized = true;
            PushToStore();
        }

        private void Update()
        {
            if (!_initialized || _isPaused)
            {
                return;
            }

            if (_unitStore == null && AppManager.Instance != null)
            {
                _unitStore = AppManager.Instance.GetUnitStore();
            }

            if (_unitStore == null)
            {
                return;
            }

            float dt = Time.deltaTime;
            _missionTimeSec += dt;

            if (_orbitLengthMeters > 1.0)
            {
                _distanceAlongOrbitMeters += _orbitalSpeedMetersPerSec * dt;
                _distanceAlongOrbitMeters = RepeatDistance(_distanceAlongOrbitMeters, _orbitLengthMeters);
            }

            _pushTimer += dt;
            if (_pushTimer >= _pushInterval)
            {
                _pushTimer -= _pushInterval;
                PushToStore();
            }
        }

        public bool IsPaused => _isPaused;

        public void SetPaused(bool paused)
        {
            _isPaused = paused;
        }

        public void TogglePaused()
        {
            _isPaused = !_isPaused;
        }

        public double GetMissionTimeSeconds()
        {
            return _missionTimeSec;
        }

        public static bool HasLineOfSight(double3 fromEcef, double3 toEcef)
        {
            double3 rayStart = fromEcef;
            double3 rayEnd = toEcef;
            double3 rayDir = rayEnd - rayStart;
            double rayLength = Math.Sqrt(rayDir.x * rayDir.x + rayDir.y * rayDir.y + rayDir.z * rayDir.z);

            if (rayLength < 1e-6)
                return false; // 位置太接近

            rayDir = rayDir / rayLength; // 归一化

            // 地球中心到射线的最远距离
            double3 toStart = rayStart - new double3(0, 0, 0);
            double t = -(toStart.x * rayDir.x + toStart.y * rayDir.y + toStart.z * rayDir.z);
            t = Math.Max(0, Math.Min(t, rayLength)); // 限制在射线段内

            double3 closestPoint = rayStart + rayDir * t;

            // 用 WGS84 椭球体高度判断：三个点都在椭球面上或以上才有视线
            double hStart = Wgs84HeightMeters(rayStart);
            double hEnd = Wgs84HeightMeters(rayEnd);
            double hClosest = Wgs84HeightMeters(closestPoint);

            return hStart >= 0.0 && hEnd >= 0.0 && hClosest >= 0.0;
        }

        private static double Wgs84HeightMeters(double3 ecef)
        {
            const double a = 6378137.0;
            const double f = 1.0 / 298.257223563;
            const double e2 = f * (2.0 - f);

            double x = ecef.x;
            double y = ecef.y;
            double z = ecef.z;
            double p = Math.Sqrt(x * x + y * y);

            if (p < 1e-12)
            {
                // 在极点上
                double absZ = Math.Abs(z);
                return absZ - a * (1.0 - f);
            }

            double latitude = Math.Atan2(z, p * (1.0 - e2));
            double height = 0.0;
            double prevLat;

            do
            {
                prevLat = latitude;
                double sinLat = Math.Sin(latitude);
                double n = a / Math.Sqrt(1.0 - e2 * sinLat * sinLat);
                height = p / Math.Cos(latitude) - n;
                latitude = Math.Atan2(z, p * (1.0 - e2 * n / (n + height)));
            }
            while (Math.Abs(latitude - prevLat) > 1e-12);

            return height;
        }

        public IReadOnlyList<OrbitSampleView> GetOrbitPathSamples()
        {
            var samples = new List<OrbitSampleView>(_orbitSamplesCache.Count);
            foreach (OrbitSample sample in _orbitSamplesCache)
            {
                samples.Add(new OrbitSampleView(sample.LatitudeDeg, sample.LongitudeDeg, sample.AltitudeMeters));
            }

            return samples;
        }

        public IReadOnlyList<double3> GetOrbitRingPoints()
        {
            int samples = Mathf.Clamp(_orbitSamples, 64, 4096);
            var points = new List<double3>(samples);

            double altitudeMeters = Math.Max(160000.0, _orbitalAltitudeKm * 1000.0);
            double orbitRadiusMeters = EarthRadiusMeters + altitudeMeters;
            double basePhaseRad = Mathf.Deg2Rad * _initialPhaseDeg + _phaseOffsetRad;

            for (int i = 0; i < samples; i++)
            {
                double t = i / (double)samples;
                double phaseRad = basePhaseRad + t * Math.PI * 2.0;

                points.Add(BuildOrbitRingPoint(phaseRad, orbitRadiusMeters));
            }

            return points;
        }

        public IReadOnlyList<double3> GetOrbitRingEcefPoints()
        {
            // 直接返回预计算的ECEF缓存（轨道线和卫星位置的唯一数据源）
            return _orbitEcefCache.AsReadOnly();
        }

        private void PushToStore()
        {
            if (_unitStore == null)
            {
                return;
            }

            OrbitState state = EvaluateOrbitFromPrecomputedPath();

            var pos = new Position
            {
                Latitude = (float)state.LatitudeDeg,
                Longitude = (float)state.LongitudeDeg,
                Altitude = (float)state.AltitudeMeters
            };

            var movement = new Movement
            {
                Speed = (float)state.SpeedKnots,
                Heading = (float)state.HeadingDeg,
                DesiredSpeed = (float)state.SpeedKnots,
                DesiredHeading = (float)state.HeadingDeg
            };

            var status = new UnitStatus
            {
                Primary = "Operational",
                Fuel = "Normal",
                Weapon = "Normal",
                IsOperative = true,
                IsDestroyed = false,
                DamageLevel = "None",
                DamagePts = 0,
                InitialDP = 100
            };

            var cached = new CachedUnit
            {
                ObjectID = _satelliteId,
                Name = _satelliteName,
                Type = "Satellite",
                DBID = 9001,
                Position = pos,
                Movement = movement,
                Status = status,
                Sensors = new List<SensorDetail>
                {
                    new SensorDetail
                    {
                        SensorID = "OPT_01",
                        Name = "Optical Payload",
                        Type = "Imaging",
                        TypeDescription = "Orbital observation payload",
                        Role = "Earth Observation",
                        MaxRange = 0f,
                        IsActive = true,
                        Capabilities = new SensorCapabilities
                        {
                            SurfaceSearch = true,
                            LandSearchFixed = true,
                            RangeInfo = true,
                            HeadingInfo = true,
                            AltitudeInfo = true,
                            SpeedInfo = true
                        }
                    }
                },
                MaxSensorRangeNm = 0f,
                HasPositionChanged = true,
                HasStatusChanged = false,
                SideID = 1
            };

            _unitStore.AddOrUpdateUnit(cached);
        }

        private void BuildOrbitPath()
        {
            _orbitSamplesCache.Clear();

            double altitudeMeters = Math.Max(160000.0, _orbitalAltitudeKm * 1000.0);
            double orbitRadiusMeters = EarthRadiusMeters + altitudeMeters;
            _orbitalPeriodSec = _autoComputePeriod
                ? 2.0 * Math.PI * Math.Sqrt(Math.Pow(orbitRadiusMeters, 3.0) / 3.986004418e14)
                : Math.Max(60.0, _orbitalPeriodMinutes * 60.0);

            RefreshOrbitAlignment();
            _phaseOffsetRad = ComputePhaseOffsetRad(orbitRadiusMeters);
            double basePhaseRad = Mathf.Deg2Rad * _initialPhaseDeg + _phaseOffsetRad;

            int samples = Mathf.Clamp(_orbitSamples, 64, 4096);
            for (int i = 0; i < samples; i++)
            {
                double t = i / (double)samples;
                double phaseRad = basePhaseRad + t * Math.PI * 2.0;
                double sampleTime = t * _orbitalPeriodSec;
                OrbitState state = EvaluateOrbitAtPhase(phaseRad, sampleTime, orbitRadiusMeters);
                double3 ecef = BuildOrbitRingPoint(phaseRad, orbitRadiusMeters);

                _orbitSamplesCache.Add(new OrbitSample
                {
                    LatitudeDeg = state.LatitudeDeg,
                    LongitudeDeg = state.LongitudeDeg,
                    AltitudeMeters = state.AltitudeMeters,
                    CumulativeMeters = 0.0
                });

                _orbitEcefCache.Add(ecef);
            }

            _orbitLengthMeters = 0.0;
            if (_orbitSamplesCache.Count > 1)
            {
                _orbitSamplesCache[0] = WithCumulative(_orbitSamplesCache[0], 0.0);
                for (int i = 1; i < _orbitSamplesCache.Count; i++)
                {
                    OrbitSample prev = _orbitSamplesCache[i - 1];
                    OrbitSample cur = _orbitSamplesCache[i];
                    _orbitLengthMeters += DistanceMeters(prev, cur);
                    _orbitSamplesCache[i] = WithCumulative(cur, _orbitLengthMeters);
                }

                OrbitSample last = _orbitSamplesCache[_orbitSamplesCache.Count - 1];
                OrbitSample first = _orbitSamplesCache[0];
                _orbitLengthMeters += DistanceMeters(last, first);
            }

            _orbitalSpeedMetersPerSec = _orbitalPeriodSec > 0.1 ? _orbitLengthMeters / _orbitalPeriodSec : 0.0;
            _distanceAlongOrbitMeters = 0.0;
        }

        private OrbitState EvaluateOrbitFromPrecomputedPath()
        {
            if (_orbitEcefCache.Count < 2 || _orbitalPeriodSec <= 0.1)
            {
                return EvaluateOrbit(_missionTimeSec);
            }

            // 直接按轨道相位从同一条轨道线缓存采样，避免和渲染线分离
            double normalizedPhase = Repeat(_missionTimeSec / _orbitalPeriodSec, 1.0);
            double lookAheadPhase = Math.Max(0.0001, _headingLookAheadSeconds / _orbitalPeriodSec);
            double3 currentEcef = SampleEcefAtPhase(normalizedPhase);
            double3 aheadEcef = SampleEcefAtPhase(normalizedPhase + lookAheadPhase);

            // 转换回LLH
            EcefToLlhWgs84(currentEcef, out double latDeg, out double lonDeg, out double altMeters);
            EcefToLlhWgs84(aheadEcef, out double aheadLat, out double aheadLon, out _);

            double headingDeg = BearingTo(latDeg, lonDeg, aheadLat, aheadLon);

            return new OrbitState
            {
                LatitudeDeg = latDeg,
                LongitudeDeg = lonDeg,
                AltitudeMeters = altMeters,
                HeadingDeg = headingDeg,
                SpeedKnots = _orbitalSpeedMetersPerSec * 1.9438444924406
            };
        }

        private OrbitState EvaluateOrbit(double timeSec)
        {
            double altitudeMeters = Math.Max(160000.0, _orbitalAltitudeKm * 1000.0);
            double orbitRadiusMeters = EarthRadiusMeters + altitudeMeters;
            double orbitalPeriodSec = _autoComputePeriod
                ? 2.0 * Math.PI * Math.Sqrt(Math.Pow(orbitRadiusMeters, 3.0) / 3.986004418e14)
                : Math.Max(60.0, _orbitalPeriodMinutes * 60.0);

            double meanMotion = 2.0 * Math.PI / orbitalPeriodSec;
            double phaseRad = Mathf.Deg2Rad * _initialPhaseDeg + _phaseOffsetRad + timeSec * meanMotion;
            double3 ecef = BuildOrbitRingPoint(phaseRad, orbitRadiusMeters);

            EcefToLlhWgs84(ecef, out double latitudeDeg, out double longitudeDeg, out double heightMeters);

            double futureTime = timeSec + 1.0;
            double futurePhaseRad = Mathf.Deg2Rad * _initialPhaseDeg + _phaseOffsetRad + futureTime * meanMotion;
            double3 futureEcef = BuildOrbitRingPoint(futurePhaseRad, orbitRadiusMeters);
            EcefToLlhWgs84(futureEcef, out double futureLat, out double futureLon, out _);

            double headingDeg = BearingTo(latitudeDeg, longitudeDeg, futureLat, futureLon);
            double speedMetersPerSec = orbitRadiusMeters * meanMotion;

            return new OrbitState
            {
                LatitudeDeg = latitudeDeg,
                LongitudeDeg = longitudeDeg,
                AltitudeMeters = heightMeters,
                HeadingDeg = headingDeg,
                SpeedKnots = speedMetersPerSec * 1.9438444924406
            };
        }

        private OrbitState EvaluateOrbitAtPhase(double phaseRad, double timeSec, double orbitRadiusMeters)
        {
            double3 ecef = BuildOrbitRingPoint(phaseRad, orbitRadiusMeters);

            EcefToLlhWgs84(ecef, out double latitudeDeg, out double longitudeDeg, out double heightMeters);

            return new OrbitState
            {
                LatitudeDeg = latitudeDeg,
                LongitudeDeg = longitudeDeg,
                AltitudeMeters = heightMeters,
                HeadingDeg = 0.0,
                SpeedKnots = 0.0
            };
        }

        private double3 SampleEcefAtPhase(double normalizedPhase)
        {
            if (_orbitEcefCache.Count == 0)
            {
                return default;
            }

            if (_orbitEcefCache.Count == 1)
            {
                return _orbitEcefCache[0];
            }

            double t = Repeat(normalizedPhase, 1.0);
            double scaledIndex = t * _orbitEcefCache.Count;
            int startIndex = (int)Math.Floor(scaledIndex) % _orbitEcefCache.Count;
            int endIndex = (startIndex + 1) % _orbitEcefCache.Count;
            double lerpT = scaledIndex - Math.Floor(scaledIndex);

            return LerpDouble3(_orbitEcefCache[startIndex], _orbitEcefCache[endIndex], lerpT);
        }

        private double3 BuildOrbitRingPoint(double phaseRad, double orbitRadiusMeters)
        {
            double inclinationRad = Mathf.Deg2Rad * GetActiveInclinationDeg();
            double raanRad = Mathf.Deg2Rad * GetActiveRaanDeg();

            // 地心坐标系中的圆轨道点（确保始终在地球表面外）
            double3 eci = new double3(
                orbitRadiusMeters * Math.Cos(phaseRad),
                orbitRadiusMeters * Math.Sin(phaseRad),
                0.0);

            eci = RotateAroundX(eci, inclinationRad);
            eci = RotateAroundZ(eci, raanRad);
            return eci;
        }

        private double GetActiveInclinationDeg()
        {
            return _alignOrbitToCenter ? _alignedInclinationDeg : _inclinationDeg;
        }

        private double GetActiveRaanDeg()
        {
            return _alignOrbitToCenter ? _alignedRaanDeg : _raanDeg;
        }

        private void RefreshOrbitAlignment()
        {
            if (!_alignOrbitToCenter)
            {
                _alignedInclinationDeg = _inclinationDeg;
                _alignedRaanDeg = _raanDeg;
                return;
            }

            double targetLat = _orbitCenterLatitude;
            double targetLon = _orbitCenterLongitude;

            // 让轨道平面覆盖目标纬度，最小保持 5 度倾角避免奇异点
            double desiredInclination = Math.Max(5.0, Math.Abs(targetLat));
            _alignedInclinationDeg = Math.Clamp(desiredInclination, 0.1, 89.9);
            _alignedRaanDeg = ComputeRaanForTarget(targetLat, targetLon, _alignedInclinationDeg);
        }

        private static double ComputeRaanForTarget(double targetLatDeg, double targetLonDeg, double inclinationDeg)
        {
            double incRad = inclinationDeg * Mathf.Deg2Rad;
            double latRad = targetLatDeg * Mathf.Deg2Rad;
            double lonRad = targetLonDeg * Mathf.Deg2Rad;

            double sinInc = Math.Sin(incRad);
            if (Math.Abs(sinInc) < 1e-6)
            {
                return targetLonDeg;
            }

            double sinU = Math.Sin(latRad) / sinInc;
            sinU = Math.Clamp(sinU, -1.0, 1.0);
            double u = Math.Asin(sinU);

            double lonOffset = Math.Atan2(Math.Cos(incRad) * Math.Sin(u), Math.Cos(u));
            double raanRad = lonRad - lonOffset;
            double raanDeg = raanRad * Mathf.Rad2Deg;

            return NormalizeLongitude(raanDeg);
        }

        private double ComputePhaseOffsetRad(double orbitRadiusMeters)
        {
            int steps = 360;
            double bestPhase = 0.0;
            double bestDistance = double.MaxValue;

            for (int i = 0; i < steps; i++)
            {
                double phase = i * (Math.PI * 2.0) / steps;
                double3 ecef = BuildOrbitRingPoint(phase, orbitRadiusMeters);
                EcefToLlhWgs84(ecef, out double latDeg, out double lonDeg, out _);
                double distance = GroundDistanceMeters(latDeg, lonDeg, _orbitCenterLatitude, _orbitCenterLongitude);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestPhase = phase;
                }
            }

            return bestPhase;
        }

        private OrbitSample SampleAtDistance(double distanceMeters)
        {
            if (_orbitSamplesCache.Count == 0)
            {
                return default;
            }

            if (_orbitSamplesCache.Count == 1 || _orbitLengthMeters <= 0.0)
            {
                return _orbitSamplesCache[0];
            }

            double distance = RepeatDistance(distanceMeters, _orbitLengthMeters);
            for (int i = 1; i < _orbitSamplesCache.Count; i++)
            {
                OrbitSample end = _orbitSamplesCache[i];
                if (distance <= end.CumulativeMeters)
                {
                    OrbitSample start = _orbitSamplesCache[i - 1];
                    double segmentLength = Math.Max(0.0001, end.CumulativeMeters - start.CumulativeMeters);
                    double t = Math.Clamp((distance - start.CumulativeMeters) / segmentLength, 0.0, 1.0);
                    return LerpSample(start, end, t);
                }
            }

            OrbitSample last = _orbitSamplesCache[_orbitSamplesCache.Count - 1];
            OrbitSample first = _orbitSamplesCache[0];
            double wrapLength = Math.Max(0.0001, _orbitLengthMeters - last.CumulativeMeters);
            double wrapT = Math.Clamp((distance - last.CumulativeMeters) / wrapLength, 0.0, 1.0);
            return LerpSample(last, first, wrapT);
        }

        private static OrbitSample LerpSample(OrbitSample start, OrbitSample end, double t)
        {
            return new OrbitSample
            {
                LatitudeDeg = LerpDouble(start.LatitudeDeg, end.LatitudeDeg, t),
                LongitudeDeg = LerpLongitude(start.LongitudeDeg, end.LongitudeDeg, t),
                AltitudeMeters = LerpDouble(start.AltitudeMeters, end.AltitudeMeters, t),
                CumulativeMeters = LerpDouble(start.CumulativeMeters, end.CumulativeMeters, t)
            };
        }

        private static OrbitSample WithCumulative(OrbitSample sample, double cumulativeMeters)
        {
            sample.CumulativeMeters = cumulativeMeters;
            return sample;
        }

        private static double DistanceMeters(OrbitSample a, OrbitSample b)
        {
            // 使用3D笛卡尔坐标计算准确距离（大圆弧+高度差）
            double3 posA = LlhToEcef(a.LatitudeDeg, a.LongitudeDeg, a.AltitudeMeters);
            double3 posB = LlhToEcef(b.LatitudeDeg, b.LongitudeDeg, b.AltitudeMeters);
            double dx = posB.x - posA.x;
            double dy = posB.y - posA.y;
            double dz = posB.z - posA.z;
            return Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private static double GroundDistanceMeters(double lat1Deg, double lon1Deg, double lat2Deg, double lon2Deg)
        {
            const double R = 6371000.0;
            double lat1 = lat1Deg * Math.PI / 180.0;
            double lat2 = lat2Deg * Math.PI / 180.0;
            double dLat = (lat2Deg - lat1Deg) * Math.PI / 180.0;
            double dLon = (lon2Deg - lon1Deg) * Math.PI / 180.0;

            double sinDLat = Math.Sin(dLat / 2.0);
            double sinDLon = Math.Sin(dLon / 2.0);
            double a = sinDLat * sinDLat + Math.Cos(lat1) * Math.Cos(lat2) * sinDLon * sinDLon;
            double c = 2.0 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1.0 - a));
            return R * c;
        }

        private static double LerpLongitude(double from, double to, double t)
        {
            double delta = Repeat(to - from + 180.0, 360.0) - 180.0;
            return NormalizeLongitude(from + delta * t);
        }

        private static double NormalizeLongitude(double lon)
        {
            double result = Repeat(lon + 180.0, 360.0) - 180.0;
            if (result <= -180.0)
            {
                return 180.0;
            }

            return result;
        }

        private static double Repeat(double value, double length)
        {
            if (length <= 0.0)
            {
                return 0.0;
            }

            return value - Math.Floor(value / length) * length;
        }

        private static double RepeatDistance(double distance, double length)
        {
            return Repeat(distance, length);
        }

        private static double LerpDouble(double a, double b, double t)
        {
            return a + (b - a) * t;
        }

        private struct OrbitState
        {
            public double LatitudeDeg;
            public double LongitudeDeg;
            public double AltitudeMeters;
            public double HeadingDeg;
            public double SpeedKnots;
        }

        private static double3 RotateAroundX(double3 value, double radians)
        {
            double c = Math.Cos(radians);
            double s = Math.Sin(radians);
            return new double3(
                value.x,
                value.y * c - value.z * s,
                value.y * s + value.z * c);
        }

        private static double3 RotateAroundZ(double3 value, double radians)
        {
            double c = Math.Cos(radians);
            double s = Math.Sin(radians);
            return new double3(
                value.x * c - value.y * s,
                value.x * s + value.y * c,
                value.z);
        }

        private static void EcefToLlhWgs84(double3 ecef, out double latitudeDeg, out double longitudeDeg, out double heightMeters)
        {
            const double semiMajorAxis = 6378137.0;
            const double flattening = EarthFlattening;
            double eccentricitySquared = flattening * (2.0 - flattening);

            double x = ecef.x;
            double y = ecef.y;
            double z = ecef.z;

            longitudeDeg = Math.Atan2(y, x) * Mathf.Rad2Deg;
            double p = Math.Sqrt(x * x + y * y);

            double latitude = Math.Atan2(z, p * (1.0 - eccentricitySquared));
            double latitudePrev;
            heightMeters = 0.0;

            do
            {
                latitudePrev = latitude;
                double sinLat = Math.Sin(latitude);
                double n = semiMajorAxis / Math.Sqrt(1.0 - eccentricitySquared * sinLat * sinLat);
                heightMeters = p / Math.Max(1e-9, Math.Cos(latitude)) - n;
                latitude = Math.Atan2(z, p * (1.0 - eccentricitySquared * n / (n + heightMeters)));
            }
            while (Math.Abs(latitude - latitudePrev) > 1e-12);

            double sinFinal = Math.Sin(latitude);
            double nFinal = semiMajorAxis / Math.Sqrt(1.0 - eccentricitySquared * sinFinal * sinFinal);
            heightMeters = p / Math.Max(1e-9, Math.Cos(latitude)) - nFinal;
            latitudeDeg = latitude * Mathf.Rad2Deg;
        }

        private static double3 LlhToEcef(double latitudeDeg, double longitudeDeg, double altitudeMeters)
        {
            const double semiMajorAxis = 6378137.0;
            const double flattening = EarthFlattening;
            double eccentricitySquared = flattening * (2.0 - flattening);
            
            double lat = latitudeDeg * Math.PI / 180.0;
            double lon = longitudeDeg * Math.PI / 180.0;
            
            double sinLat = Math.Sin(lat);
            double cosLat = Math.Cos(lat);
            double sinLon = Math.Sin(lon);
            double cosLon = Math.Cos(lon);
            
            double n = semiMajorAxis / Math.Sqrt(1.0 - eccentricitySquared * sinLat * sinLat);
            double x = (n + altitudeMeters) * cosLat * cosLon;
            double y = (n + altitudeMeters) * cosLat * sinLon;
            double z = (n * (1.0 - eccentricitySquared) + altitudeMeters) * sinLat;
            
            return new double3(x, y, z);
        }
        
        private static double3 LerpDouble3(double3 a, double3 b, double t)
        {
            return new double3(
                LerpDouble(a.x, b.x, t),
                LerpDouble(a.y, b.y, t),
                LerpDouble(a.z, b.z, t));
        }

        private static double BearingTo(double lat1Deg, double lon1Deg, double lat2Deg, double lon2Deg)
        {
            double lat1 = lat1Deg * Math.PI / 180.0;
            double lat2 = lat2Deg * Math.PI / 180.0;
            double dLon = (lon2Deg - lon1Deg) * Math.PI / 180.0;

            double y = Math.Sin(dLon) * Math.Cos(lat2);
            double x = Math.Cos(lat1) * Math.Sin(lat2) - Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(dLon);
            double heading = Math.Atan2(y, x) * 180.0 / Math.PI;
            heading %= 360.0;
            if (heading < 0.0)
            {
                heading += 360.0;
            }

            return heading;
        }
    }
}