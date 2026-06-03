using CesiumForUnity;
using Unity.Mathematics;

namespace CommandP.GlobalEntity.Services
{
    /// <summary>
    /// CesiumGlobeAnchor 闈欐€佽緟鍔╂柟娉曘€?    /// 鐩存帴璋冪敤 Cesium 1.23.1 鍏紑 API锛屼笉浣跨敤鍙嶅皠銆?    /// </summary>
    public static class GlobeAnchorHelper
    {
        /// <summary>
        /// 鐩存帴璁剧疆 GlobeAnchor 鐨勭粡绾害楂樺害 (鏃犲弽灏?
        /// </summary>
        public static void SetLongitudeLatitudeHeight(
            this CesiumGlobeAnchor anchor,
            double longitudeDeg, double latitudeDeg, double heightMeters)
        {
            if (anchor == null) return;

            // 360 搴﹁繛缁粡搴? 鍏堝綊涓€鍖栧埌 [-180, 180]
            longitudeDeg = GeoCoordConverter.NormalizeLongitude(longitudeDeg);

            anchor.longitudeLatitudeHeight = new double3(longitudeDeg, latitudeDeg, heightMeters);
        }

        /// <summary>
        /// 鍦?GameObject 涓婂垱寤哄苟閰嶇疆 GlobeAnchor
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
        /// 鎵归噺鏇存柊鎵€鏈夊疄浣撶殑 GlobeAnchor 浣嶇疆
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
