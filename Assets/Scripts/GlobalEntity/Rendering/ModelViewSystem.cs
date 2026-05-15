using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CommandP.GlobalEntity.Rendering
{
    /// <summary>
    /// GLB 模型视图管理: 实例化模型、更新变换、启用/停用。
    /// 模型通过 ModelCache 加载 (Resources.Load 缓存)。
    /// </summary>
    public class ModelViewSystem
    {
        private readonly ModelCache _modelCache;
        private readonly IReadOnlyDictionary<string, GoPoolEntry> _viewEntries;

        private static readonly Dictionary<EntityType, float> ModelScales = new()
        {
            { EntityType.Ship,          200f },
            { EntityType.Aircraft,      20f },
            { EntityType.Satellite,     100f },
            { EntityType.Missile,       30f },
            { EntityType.GroundVehicle, 150f },
        };

        // 模型旋转修正 — 模型已在 Blender 统一朝向, 全部清零
        public static readonly Dictionary<EntityType, Vector3> ModelRotationOffsets = new()
        {
            { EntityType.Ship,          Vector3.zero },
            { EntityType.Aircraft,      Vector3.zero },
            { EntityType.Satellite,     Vector3.zero },
            { EntityType.Missile,       Vector3.zero },
            { EntityType.GroundVehicle, Vector3.zero },
        };

        public ModelViewSystem(ModelCache modelCache, IReadOnlyDictionary<string, GoPoolEntry> viewEntries)
        {
            _modelCache = modelCache;
            _viewEntries = viewEntries;
        }

        /// <summary>
        /// 为指定实体实例化模型 (LOD 切换到 near 时调用)
        /// </summary>
        public void ShowModel(EntityData entity)
        {
            if (!_viewEntries.TryGetValue(entity.ObjectId, out var entry) || entry == null)
                return;

            if (entry.ModelInstance != null)
            {
                entry.ModelInstance.SetActive(true);
                entry.ModelRoot.SetActive(true);
                return;
            }

            // ===== 层级: EntityRoot(GlobeAnchor) -> ModelRoot(heading) -> RotationFix(offset) -> GLB =====

            // ModelRoot 的 rotatoin 由 UpdateTransforms 控制 (heading), 这里保持 identity
            entry.ModelRoot.transform.localRotation = Quaternion.identity;

            // RotationFix — 修正 GLB 导入坐标系
            var rotationFix = new GameObject("RotationFix");
            rotationFix.transform.SetParent(entry.ModelRoot.transform, false);
            rotationFix.transform.localPosition = Vector3.zero;

            string modelKey = _modelCache.GetModelKey(entity);
            var instance = _modelCache.InstantiateModel(modelKey, rotationFix.transform, entity.ObjectId);

            if (instance != null)
            {
                float scale = ModelScales.TryGetValue(entity.Type, out var s) ? s : 50f;
                instance.transform.localScale = Vector3.one * scale;

                // 自动检测并设置修正旋转
                Quaternion? autoFix = ModelOrientationFixer.GetRecommendedFix(instance);
                Vector3 manualOffset = ModelRotationOffsets.TryGetValue(entity.Type, out var ro) ? ro : Vector3.zero;
                Quaternion fixRot = autoFix ?? Quaternion.Euler(manualOffset);
                rotationFix.transform.localRotation = fixRot;

                entry.ModelInstance = instance;
                entry.ModelRoot.SetActive(true);
            }
        }

        /// <summary>
        /// 隐藏指定实体的模型 (LOD 切换到 far 时调用)
        /// </summary>
        public void HideModel(EntityData entity)
        {
            if (!_viewEntries.TryGetValue(entity.ObjectId, out var entry) || entry == null)
                return;

            entry.ModelRoot.SetActive(false);

            if (entry.ModelInstance != null)
            {
                entry.ModelInstance.SetActive(false);
            }
        }

        /// <summary>
        /// 每帧更新 near LOD 实体的模型朝向 (heading)
        /// </summary>
        public void UpdateTransforms(IReadOnlyList<EntityData> entities, int count)
        {
            for (int i = 0; i < count; i++)
            {
                EntityData e = entities[i];
                if (e == null || !e.IsNearLod) continue;

                if (!_viewEntries.TryGetValue(e.ObjectId, out var entry) || entry == null)
                    continue;

                if (entry.ModelInstance == null) continue;

                // 应用 heading 旋转 (0 = North = +Z, CW)
                float headingAbs = e.HeadingDeg % 360f;
                if (headingAbs < 0) headingAbs += 360f;

                // EntityRoot 可能有 GlobeAnchor adjustOrientation，所以在本地空间旋转
                entry.ModelRoot.transform.localRotation = Quaternion.Euler(0f, headingAbs, 0f);
            }
        }

        /// <summary>
        /// 移除实体的模型实例
        /// </summary>
        public void DestroyModel(string objectId)
        {
            if (!_viewEntries.TryGetValue(objectId, out var entry) || entry == null)
                return;

            if (entry.ModelInstance != null)
            {
                Object.Destroy(entry.ModelInstance);
                entry.ModelInstance = null;
            }
        }
    }
}
