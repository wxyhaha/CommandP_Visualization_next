using CesiumForUnity;
using Unity.Mathematics;

namespace CommandP.GlobalEntity.Services
{
    /// <summary>
    /// CesiumGlobeAnchor 静态辅助方法。
    /// 直接调用 Cesium 1.23.1 公开 API，不使用反射。
    /// </summary>
    public static class GlobeAnchorHelper
    {
        /// <summary>
        /// 直接设置 GlobeAnchor 的经纬度高度 (无反射)
        /// </summary>
        public static void SetLongitudeLatitudeHeight(
            this CesiumGlobeAnchor anchor,
            double longitudeDeg, double latitudeDeg, double heightMeters)
        {
            if (anchor == null) return;

            // 360 度连续经度: 先归一化到 [-180, 180]
            longitudeDeg = GeoCoordConverter.NormalizeLongitude(longitudeDeg);

            anchor.longitudeLatitudeHeight = new double3(longitudeDeg, latitudeDeg, heightMeters);
        }

        /// <summary>
        /// 在 GameObject 上创建并配置 GlobeAnchor
        /// </summary>
        public static CesiumGlobeAnchor AttachGlobeAnchor(
            UnityEngine.GameObject go,
            double longitudeDeg, double latitudeDeg, double heightMeters)
        {
            longitudeDeg = GeoCoordConverter.NormalizeLongitude(longitudeDeg);

            var anchor = go.AddComponent<CesiumGlobeAnchor>();
            anchor.adjustOrientationForGlobeWhenMoving = true;
            anchor.detectTransformChanges = false;
            anchor.longitudeLatitudeHeight = new double3(longitudeDeg, latitudeDeg, heightMeters);
            return anchor;
        }

        /// <summary>
        /// 批量更新所有实体的 GlobeAnchor 位置
        /// </summary>
        public static void ApplyAllPositions(
            EntityData[] entities, int count,
            System.Collections.Generic.IReadOnlyDictionary<string, CesiumGlobeAnchor> anchors)
        {
            for (int i = 0; i < count; i++)
            {
                EntityData e = entities[i];
                if (e == null) continue;

                if (anchors.TryGetValue(e.ObjectId, out var anchor) && anchor != null)
                {
                    double lon = GeoCoordConverter.NormalizeLongitude(e.LongitudeDeg);
                    anchor.longitudeLatitudeHeight = new double3(lon, e.LatitudeDeg, e.HeightMeters);
                }
            }
        }
    }
}
