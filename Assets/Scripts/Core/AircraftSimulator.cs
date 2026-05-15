using System;
using System.Collections.Generic;
using UnityEngine;
using CommandP.Data.DTOs;
using CommandP.Data.Stores;

namespace CommandP.Core
{
    /// <summary>
    /// 飞机轨迹模拟器：生成更真实的巡逻航线。
    /// - 航点带随机偏移，非正圆
    /// - 提前转弯（anticipatory turn），避免折角
    /// - 高度/速度有自然波动
    /// - 每帧物理更新 + 高频推送保证视觉平滑
    /// </summary>
    public class AircraftSimulator : MonoBehaviour
    {
        [Header("飞机信息")]
        [SerializeField] private string _aircraftId = "AIRCRAFT_SIM_001";
        [SerializeField] private string _aircraftName = "E-2D Hawkeye";
        [SerializeField] private float _baseAltitudeM = 8000f;

        [Header("航线中心（经纬度）")]
        [SerializeField] private double _originLatitude = 39.736401;
        [SerializeField] private double _originLongitude = -105.25737;

        [Header("飞行参数")]
        [SerializeField] private float _speedKnots = 300f;
        [SerializeField] private float _turnRateDegPerSec = 2.5f;

        [Header("航线形状")]
        [SerializeField] private double _routeRadiusDeg = 0.22;
        [SerializeField] private int _waypointCount = 6;
        [SerializeField] private float _waypointJitterDeg = 0.05f;

        [Header("半球穿越轨迹（外->内->外）")]
        [SerializeField] private bool _useRadarDemoRoute = true;
        [SerializeField] private double _radarCenterLatitude = 39.736401;
        [SerializeField] private double _radarCenterLongitude = -105.25737;
        [SerializeField] private double _outsideRadiusDeg = 0.26;
        [SerializeField] private double _insideRadiusDeg = 0.14;

        [Header("推送频率")]
        [SerializeField] private float _pushInterval = 0.05f;

        private UnitStore _unitStore;
        private float _pushTimer;
        private bool _isPaused;

        // 航线定义
        private readonly List<Waypoint> _waypoints = new List<Waypoint>();
        private int _currentWpIndex;

        // 飞行状态（每帧更新）
        private double _lat, _lon;
        private float _altM;
        private float _headingDeg;
        private float _speedKnotsCurrent;
        private float _altPhase;
        private bool _initialized;

        private struct Waypoint
        {
            public double Lat;
            public double Lon;
            public Waypoint(double lat, double lon) { Lat = lat; Lon = lon; }
        }

        private void Start()
        {
            GenerateWaypoints();
            InitPosition();
            if (AppManager.Instance != null)
                _unitStore = AppManager.Instance.GetUnitStore();
        }

        #region 航点生成（非正圆，带随机偏移）

        private void GenerateWaypoints()
        {
            _waypoints.Clear();

            if (_useRadarDemoRoute)
            {
                GenerateRadarDemoWaypoints();
                return;
            }

            int count = Mathf.Max(4, _waypointCount);
            var rng = new System.Random(42);

            for (int i = 0; i < count; i++)
            {
                float angle = (360f / count) * i;
                angle += (float)(rng.NextDouble() - 0.5) * (360f / count) * 0.4f;

                double rad = angle * Mathf.Deg2Rad;
                double radiusJitter = _routeRadiusDeg * (1.0 + (rng.NextDouble() - 0.5) * 0.5);
                double lat = _originLatitude + radiusJitter * Math.Cos(rad)
                             + (rng.NextDouble() - 0.5) * _waypointJitterDeg;
                double lon = _originLongitude + radiusJitter * Math.Sin(rad)
                             + (rng.NextDouble() - 0.5) * _waypointJitterDeg;
                _waypoints.Add(new Waypoint(lat, lon));
            }
        }

        private void GenerateRadarDemoWaypoints()
        {
            // 这组点保证飞行顺序为：半球外 -> 入侵半球 -> 半球内 -> 飞出半球。
            // 同时控制路径长度，避免等待太久才进入半球。
            double centerLat = _radarCenterLatitude;
            double centerLon = _radarCenterLongitude;
            if (AppManager.Instance != null)
            {
                GroundRadarDomeController radar = AppManager.Instance.GetGroundRadarDomeController();
                if (radar != null)
                {
                    centerLat = radar.RadarLatitude;
                    centerLon = radar.RadarLongitude;
                }
            }

            double outsideR = Math.Max(_insideRadiusDeg + 0.02, _outsideRadiusDeg);
            double insideR = Math.Max(0.02, Math.Min(_insideRadiusDeg, outsideR - 0.02));

            _originLatitude = centerLat;
            _originLongitude = centerLon;

            // 短走廊路线：西侧外圈 -> 入圈 -> 圈内飞行 -> 东侧出圈 -> 东南外圈。
            // 这样一般 1~2 分钟内就能看到完整蓝->红->蓝切换。
            AddPolarWaypoint(centerLat, centerLon, outsideR, 215f);   // 半球外
            AddPolarWaypoint(centerLat, centerLon, insideR, 195f);    // 入圈
            AddPolarWaypoint(centerLat, centerLon, insideR, 160f);    // 圈内
            AddPolarWaypoint(centerLat, centerLon, insideR, 125f);    // 圈内
            AddPolarWaypoint(centerLat, centerLon, insideR, 85f);     // 圈内
            AddPolarWaypoint(centerLat, centerLon, insideR, 45f);     // 圈内接近出圈
            AddPolarWaypoint(centerLat, centerLon, outsideR, 25f);    // 出圈
            AddPolarWaypoint(centerLat, centerLon, outsideR, 355f);   // 半球外
            AddPolarWaypoint(centerLat, centerLon, outsideR, 315f);   // 半球外
        }

        private void AddPolarWaypoint(double centerLat, double centerLon, double radiusDeg, float angleDeg)
        {
            double rad = angleDeg * Mathf.Deg2Rad;
            double lonScale = Math.Max(0.1, Math.Cos(centerLat * Mathf.Deg2Rad));

            double lat = centerLat + radiusDeg * Math.Cos(rad);
            double lon = centerLon + (radiusDeg * Math.Sin(rad)) / lonScale;
            _waypoints.Add(new Waypoint(lat, lon));
        }

        #endregion

        private void InitPosition()
        {
            if (_waypoints.Count == 0) return;
            _lat = _waypoints[0].Lat;
            _lon = _waypoints[0].Lon;
            _altM = _baseAltitudeM;
            _headingDeg = BearingTo(_waypoints[0], _waypoints[1 % _waypoints.Count]);
            _speedKnotsCurrent = _speedKnots;
            _altPhase = 0f;
            _currentWpIndex = 0;
            _initialized = true;
        }

        private void Update()
        {
            if (!_initialized || _waypoints.Count == 0) return;
            if (_isPaused) return;

            if (_unitStore == null && AppManager.Instance != null)
                _unitStore = AppManager.Instance.GetUnitStore();
            if (_unitStore == null) return;

            float dt = Time.deltaTime;
            SimulateStep(dt);

            _pushTimer += dt;
            if (_pushTimer >= _pushInterval)
            {
                _pushTimer -= _pushInterval;
                PushToStore();
            }
        }

        #region 每帧物理模拟（核心）

        private void SimulateStep(float dt)
        {
            Waypoint target = _waypoints[_currentWpIndex];
            double dLat = target.Lat - _lat;
            double dLon = target.Lon - _lon;
            double distDeg = Math.Sqrt(dLat * dLat + dLon * dLon);

            // 到目标航点的真方位
            float bearingToTarget = BearingTo(new Waypoint(_lat, _lon), target);
            float headingError = Mathf.DeltaAngle(_headingDeg, bearingToTarget);

            // === 提前转弯逻辑 ===
            // 速度转换：1节 = 0.51444 m/s，1度 ≈ 111320 m
            double speedMs = _speedKnotsCurrent * 0.51444;  // m/s
            double speedDegPerSec = speedMs / 111320.0;     // degree/s
            double turnRadiusDeg = speedDegPerSec / (_turnRateDegPerSec * Mathf.Deg2Rad);
            double leadDistDeg = turnRadiusDeg * 2.2;

            if (distDeg < leadDistDeg)
            {
                // 准备转向下一航点
                int nextIdx = (_currentWpIndex + 1) % _waypoints.Count;
                float bearingToNext = BearingTo(new Waypoint(_lat, _lon), _waypoints[nextIdx]);

                float diffNext = Mathf.DeltaAngle(bearingToTarget, bearingToNext);
                if (Math.Abs(diffNext) < 100f)
                {
                    // 逐渐混合到下一航点方向
                    float t = Mathf.Clamp01((float)(1.0 - distDeg / leadDistDeg));
                    float blended = Mathf.LerpAngle(bearingToTarget, bearingToNext, t * t);
                    _headingDeg = SmoothTurn(_headingDeg, blended, dt);

                    // 如果航向已转到下一航点方向，提前切换目标
                    float errNext = Mathf.Abs(Mathf.DeltaAngle(_headingDeg, bearingToNext));
                    if (errNext < 40f || distDeg < 0.005)
                        _currentWpIndex = nextIdx;
                }
                else
                {
                    _headingDeg = SmoothTurn(_headingDeg, bearingToTarget, dt);
                }
            }
            else
            {
                _headingDeg = SmoothTurn(_headingDeg, bearingToTarget, dt);
            }

            // === 位置更新：用米制，再转为度 ===
            double stepMeters = speedMs * dt;
            double headingRad = _headingDeg * Mathf.Deg2Rad;
            double cosLat = Math.Cos(_lat * Mathf.Deg2Rad);
            
            // 沿方向移动
            double metersNorth = stepMeters * Math.Cos(headingRad);
            double metersEast = stepMeters * Math.Sin(headingRad);
            
            // 转为度（考虑纬度）
            double deltaLat = metersNorth / 111320.0;
            double deltaLon = metersEast / (111320.0 * Math.Max(0.1, cosLat));
            
            _lat += deltaLat;
            _lon += deltaLon;

            // 到达航点检测（位置硬同步）
            dLat = target.Lat - _lat;
            dLon = target.Lon - _lon;
            if (Math.Sqrt(dLat * dLat + dLon * dLon) < 0.0005)
            {
                _lat = target.Lat;
                _lon = target.Lon;
                // 如果还没切换就强制切换
                if (_currentWpIndex == _waypoints.FindIndex(w => Math.Abs(w.Lat - target.Lat) < 1e-9))
                    _currentWpIndex = (_currentWpIndex + 1) % _waypoints.Count;
            }

            // === 高度自然波动（双正弦叠加）===
            _altPhase += dt * (0.15f + 0.08f * Mathf.Sin(Time.time * 0.05f));
            _altM = _baseAltitudeM
                    + Mathf.Sin(_altPhase * 0.7f) * 150f
                    + Mathf.Sin(_altPhase * 1.3f + 1.2f) * 80f;

            // === 速度微波动 ===
            _speedKnotsCurrent = _speedKnots + Mathf.Sin(Time.time * 0.1f) * 10f;
        }

        private float SmoothTurn(float current, float target, float dt)
        {
            float diff = Mathf.DeltaAngle(current, target);
            float maxStep = _turnRateDegPerSec * dt;
            return current + Mathf.Clamp(diff, -maxStep, maxStep);
        }

        #endregion

        #region 辅助计算

        private static float BearingTo(Waypoint from, Waypoint to)
        {
            double dLon = (to.Lon - from.Lon) * Mathf.Deg2Rad;
            double lat1 = from.Lat * Mathf.Deg2Rad;
            double lat2 = to.Lat * Mathf.Deg2Rad;
            double y = Math.Sin(dLon) * Math.Cos(lat2);
            double x = Math.Cos(lat1) * Math.Sin(lat2) -
                       Math.Sin(lat1) * Math.Cos(lat2) * Math.Cos(dLon);
            float heading = Mathf.Rad2Deg * (float)Math.Atan2(y, x);
            return (heading + 360f) % 360f;
        }

        #endregion

        #region 推送数据到 UnitStore

        private void PushToStore()
        {
            var pos = new Position
            {
                Latitude = (float)_lat,
                Longitude = (float)_lon,
                Altitude = _altM
            };

            var movement = new Movement
            {
                Speed = _speedKnotsCurrent,
                Heading = _headingDeg,
                DesiredSpeed = _speedKnotsCurrent,
                DesiredHeading = _headingDeg
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
                ObjectID = _aircraftId,
                Name = _aircraftName,
                Type = "Aircraft",
                DBID = 3001,
                Position = pos,
                Movement = movement,
                Status = status,
                Sensors = new List<SensorDetail>
                {
                    new SensorDetail
                    {
                        SensorID = "RADAR_01",
                        Name = "AN/APS-145",
                        Type = "Radar",
                        TypeDescription = "Airborne Early Warning Radar",
                        Role = "Air Search",
                        MaxRange = 350f,
                        IsActive = true,
                        Capabilities = new SensorCapabilities
                        {
                            AirSearch = true, SurfaceSearch = true,
                            RangeInfo = true, HeadingInfo = true,
                            AltitudeInfo = true, SpeedInfo = true
                        }
                    }
                },
                MaxSensorRangeNm = 350f,
                HasPositionChanged = true,
                HasStatusChanged = false,
                SideID = 1
            };

            _unitStore.AddOrUpdateUnit(cached);
        }

        #endregion

        public void GetCurrentPosition(out double lat, out double lon, out float altM)
        {
            lat = _lat;
            lon = _lon;
            altM = _altM;
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
    }
}
