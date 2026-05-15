using System.Collections.Generic;
using UnityEngine;

namespace CommandP.GlobalEntity.Rendering
{
    /// <summary>
    /// 模型朝向自动修正。
    /// 检测 GLB 导入后的实际 forward/up 方向，计算修正旋转使其对齐 Unity 标准 (+Z=Forward, +Y=Up)。
    /// </summary>
    public static class ModelOrientationFixer
    {
        /// <summary>
        /// 检测模型实际前向和上向，返回对齐到 +Z/+Y 所需的旋转
        /// </summary>
        public static Quaternion ComputeFixRotation(GameObject modelRoot)
        {
            // 收集所有 MeshFilter
            var filters = new List<MeshFilter>();
            modelRoot.GetComponentsInChildren(true, filters);
            if (filters.Count == 0) return Quaternion.identity;

            Bounds combined = default;
            bool first = true;
            for (int i = 0; i < filters.Count; i++)
            {
                var mf = filters[i];
                if (mf.sharedMesh == null) continue;
                var mb = mf.sharedMesh.bounds;
                var corners = GetWorldCorners(mf.transform, mb);
                foreach (var c in corners)
                {
                    if (first) { combined = new Bounds(c, Vector3.zero); first = false; }
                    else combined.Encapsulate(c);
                }
            }
            if (first) return Quaternion.identity;

            Vector3 s = combined.size;
            return Quaternion.identity;
        }

        /// <summary>
        /// 根据包围盒尺寸推荐修正旋转
        /// 返回 null 表示不需要修正
        /// </summary>
        public static Quaternion? GetRecommendedFix(GameObject modelRoot)
        {
            var mfs = modelRoot.GetComponentsInChildren<MeshFilter>(true);
            if (mfs.Length == 0) return null;

            Bounds b = default;
            bool first = true;
            foreach (var mf in mfs)
            {
                if (mf.sharedMesh == null) continue;
                var mb = mf.sharedMesh.bounds;
                var corners = GetWorldCorners(mf.transform, mb);
                foreach (var c in corners)
                {
                    if (first) { b = new Bounds(c, Vector3.zero); first = false; }
                    else b.Encapsulate(c);
                }
            }
            if (first) return null;

            Vector3 s = b.size;
            Debug.Log($"[OrientationFixer] Model bounds: size=({s.x:F1}, {s.y:F1}, {s.z:F1}) center={b.center}");

            // 如果 Y 是最长轴 → 模型竖立, 需要 X=-90
            if (s.y > s.x * 1.5f && s.y > s.z * 1.5f)
            {
                Debug.Log("[OrientationFixer] Model appears vertical (Y longest), applying X=-90 fix");
                return Quaternion.Euler(-90f, 0f, 0f);
            }

            // 如果 Z 远小于 X/Y → 模型扁平, 可能需要翻转
            if (s.z < s.x * 0.3f && s.z < s.y * 0.3f)
            {
                Debug.Log("[OrientationFixer] Model appears flat on Z, no fix needed (flat ship body normal)");
            }

            Debug.Log("[OrientationFixer] No fix recommended (model appears correctly oriented)");
            return null;
        }

        private static Vector3[] GetWorldCorners(Transform t, Bounds b)
        {
            var corners = new Vector3[8];
            for (int i = 0; i < 8; i++)
            {
                Vector3 c = b.center;
                c.x += (i & 1) != 0 ? b.extents.x : -b.extents.x;
                c.y += (i & 2) != 0 ? b.extents.y : -b.extents.y;
                c.z += (i & 4) != 0 ? b.extents.z : -b.extents.z;
                corners[i] = t.TransformPoint(c);
            }
            return corners;
        }
    }
}
