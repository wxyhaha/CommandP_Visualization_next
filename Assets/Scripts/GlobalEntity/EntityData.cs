using Unity.Mathematics;

namespace CommandP.GlobalEntity
{
    /// <summary>
    /// 实体数据 — 纯数据层，Unity Transform 永远不作为真实数据源。
    /// 所有地理坐标使用 double 精度。
    /// </summary>
    public class EntityData
    {
        public string ObjectId;
        public string DisplayName;
        public EntityType Type;
        public int SideId;

        // 地理坐标 (WGS84, double 精度, 唯一数据源)
        public double LongitudeDeg;
        public double LatitudeDeg;
        public double HeightMeters;

        // 运动状态
        public float HeadingDeg;
        public float SpeedKnots;

        // 卫星轨道专用 (仅 Type==Satellite 时有效)
        public double OrbitAltitudeKm;
        public double OrbitInclinationDeg;
        public double OrbitRaanDeg;
        public double OrbitPhaseDeg;
        public bool HasOrbitParams;

        // LOD 状态
        public bool IsNearLod;

        // 模型键值
        public string ModelAssetKey;

        // 图标键值 (为空则按 EntityType 选择默认图标, 设置如 "submarine")
        public string IconKey;

        // ECEF 缓存
        public double3 EcefPosition;
        public bool EcefDirty;

        public EntityData()
        {
            EcefDirty = true;
        }
    }
}
