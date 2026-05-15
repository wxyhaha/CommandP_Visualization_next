using CesiumForUnity;
using CommandP.GlobalEntity.Services;
using Unity.Mathematics;
using UnityEngine;

namespace CommandP.GlobalEntity.Rendering
{
    public enum CameraMode { Idle, Follow }

    public class CameraFollowSystem
    {
        private readonly Camera _camera;
        private readonly CesiumGeoreference _georeference;
        private CesiumGlobeAnchor _camAnchor;

        public CameraMode Mode { get; private set; } = CameraMode.Idle;
        public EntityData Target { get; private set; }
        public bool IsActive => Mode == CameraMode.Follow;

        public float SmoothTime = 0.8f;

        // 跟随偏移
        public float FollowDistance = 5000f;  // 后方距离
        public float FollowHeight = 3000f;    // 上方高度

        private double _curLon, _curLat, _curAlt;
        private bool _snapNextFrame;

        public CameraFollowSystem(Camera camera, CesiumGeoreference georeference)
        {
            _camera = camera;
            _georeference = georeference;
        }

        private void EnsureAnchor()
        {
            if (_camAnchor == null)
            {
                _camAnchor = _camera.GetComponent<CesiumGlobeAnchor>();
                if (_camAnchor == null)
                    _camAnchor = _camera.gameObject.AddComponent<CesiumGlobeAnchor>();
                _camAnchor.adjustOrientationForGlobeWhenMoving = false;
                double3 cur = _camAnchor.longitudeLatitudeHeight;
                _curLon = cur.x; _curLat = cur.y; _curAlt = cur.z;
            }
        }

        // ==================== Public API ====================

        public void FollowTarget(EntityData entity)
        {
            Target = entity;
            EnsureAnchor();
            _snapNextFrame = true; // 下一帧直接跳到目标位置
            Mode = CameraMode.Follow;
            Debug.Log($"[CameraFollow] Following: {entity.DisplayName} ({entity.ObjectId})");
        }

        public void FocusTarget(EntityData entity)
        {
            // 聚焦: 快照到目标, 但不持续跟随
            Target = entity;
            EnsureAnchor();
            ComputeTargetLlh(out double lon, out double lat, out double alt);
            _curLon = lon; _curLat = lat; _curAlt = alt;
            _camAnchor.longitudeLatitudeHeight = new double3(
                GeoCoordConverter.NormalizeLongitude(lon), lat, alt);
            _camera.farClipPlane = Mathf.Clamp((float)(alt * 3f), 1000f, 5000000f);
            Debug.Log($"[CameraFollow] Focused: {entity.DisplayName} ({entity.ObjectId})");
        }

        public void Cancel()
        {
            Mode = CameraMode.Idle;
            Target = null;
            Debug.Log("[CameraFollow] Cancelled");
        }

        // ==================== Update ====================

        public void Update(float dt)
        {
            if (Mode == CameraMode.Idle || Target == null) return;
            EnsureAnchor();

            // 计算目标相机 LLH
            ComputeTargetLlh(out double targetLon, out double targetLat, out double targetAlt);

            if (_snapNextFrame)
            {
                // 直接跳到位
                _curLon = targetLon; _curLat = targetLat; _curAlt = targetAlt;
                _snapNextFrame = false;
            }
            else
            {
                // 指数平滑
                float f = Mathf.Clamp01(dt / SmoothTime);
                double dLon = targetLon - _curLon;
                if (dLon > 180.0) dLon -= 360.0;
                if (dLon < -180.0) dLon += 360.0;
                _curLon += dLon * f;
                _curLat += (targetLat - _curLat) * f;
                _curAlt += (targetAlt - _curAlt) * f;
            }

            _curLon = GeoCoordConverter.NormalizeLongitude(_curLon);
            _camAnchor.longitudeLatitudeHeight = new double3(_curLon, _curLat, _curAlt);

            // 更新裁剪面
            _camera.farClipPlane = Mathf.Clamp((float)(_curAlt * 3f), 1000f, 5000000f);

            // 看向目标实体
            double3 targetEcef = GeoCoordConverter.LlhToEcefWgs84(
                Target.LongitudeDeg, Target.LatitudeDeg, Target.HeightMeters);
            double3 targetUnity = _georeference.TransformEarthCenteredEarthFixedPositionToUnity(targetEcef);
            Vector3 tp = new Vector3((float)targetUnity.x, (float)targetUnity.y, (float)targetUnity.z);
            _camera.transform.rotation = Quaternion.LookRotation(tp - _camera.transform.position, _georeference.transform.up);
        }

        private void ComputeTargetLlh(out double camLon, out double camLat, out double camAlt)
        {
            double tLon = Target.LongitudeDeg;
            double tLat = Target.LatitudeDeg;
            double tAlt = Target.HeightMeters;

            // 相机在目标后方 + 上方
            float hdgRad = Target.HeadingDeg * Mathf.Deg2Rad;
            double offsetLon = FollowDistance * System.Math.Sin(hdgRad);
            double offsetLat = FollowDistance * System.Math.Cos(hdgRad);

            double mPerDegLat = 111320.0;
            double mPerDegLon = 111320.0 * System.Math.Max(0.1, System.Math.Cos(tLat * (System.Math.PI / 180.0)));

            camLon = tLon + offsetLon / mPerDegLon;
            camLat = tLat - offsetLat / mPerDegLat; // 南方向 (后方) = 纬度减小
            camAlt = System.Math.Max(100.0, tAlt + FollowHeight);
            camLon = GeoCoordConverter.NormalizeLongitude(camLon);
        }
    }
}
