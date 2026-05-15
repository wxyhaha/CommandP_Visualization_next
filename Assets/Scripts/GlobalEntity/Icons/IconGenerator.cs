using System.Collections.Generic;
using UnityEngine;

namespace CommandP.GlobalEntity.Icons
{
    /// <summary>
    /// 从 Assets/Resources/icons/ 加载 PNG 图片作为 Billboard 图标。
    /// 支持按 EntityType 默认映射和按 iconKey 字符串直接查找。
    /// </summary>
    public static class IconGenerator
    {
        // 图标文件名 → Texture2D 缓存
        private static readonly Dictionary<string, Texture2D> _cache = new Dictionary<string, Texture2D>();

        // EntityType → 默认图标文件名
        private static readonly Dictionary<EntityType, string> _defaultIconKeys = new Dictionary<EntityType, string>
        {
            { EntityType.Ship,          "warship" },
            { EntityType.Aircraft,      "warplane" },
            { EntityType.Satellite,     "satellite" },
            { EntityType.Missile,       "missile" },
            { EntityType.GroundVehicle, "militaryvehicle" },
        };

        /// <summary>
        /// 获取指定类型的默认图标
        /// </summary>
        public static Texture2D GetIcon(EntityType type)
        {
            string key = GetIconKey(type);
            return GetIconByKey(key);
        }

        /// <summary>
        /// 按字符串 key 获取图标 (如 "submarine", "warship")
        /// </summary>
        public static Texture2D GetIconByKey(string iconKey)
        {
            if (string.IsNullOrEmpty(iconKey))
                return GetIconByKey("warship");

            if (_cache.TryGetValue(iconKey, out var cached))
                return cached;

            string path = "icons/" + iconKey;
            var tex = Resources.Load<Texture2D>(path);

            if (tex == null)
            {
                Debug.LogWarning($"[IconGenerator] Icon not found at Resources/{path}, falling back to warship");
                if (_cache.TryGetValue("warship", out var fallback))
                    return fallback;

                // Last resort: load warship
                tex = Resources.Load<Texture2D>("icons/warship");
                if (tex == null)
                {
                    Debug.LogError("[IconGenerator] No icons found in Resources/icons/");
                    return null;
                }
            }

            _cache[iconKey] = tex;
            return tex;
        }

        /// <summary>
        /// 根据实体数据获取其图标 (优先使用 IconKey 字段)
        /// </summary>
        public static Texture2D GetIconForEntity(EntityData entity)
        {
            if (entity == null)
                return GetIcon(EntityType.Ship);

            // 优先使用实体特定的 IconKey
            if (!string.IsNullOrEmpty(entity.IconKey))
                return GetIconByKey(entity.IconKey);

            return GetIcon(entity.Type);
        }

        /// <summary>
        /// EntityType → 默认 icon key
        /// </summary>
        public static string GetIconKey(EntityType type)
        {
            return _defaultIconKeys.TryGetValue(type, out var key) ? key : "warship";
        }

        /// <summary>
        /// 获取所有已知图标 key (用于初始化预加载)
        /// </summary>
        public static HashSet<string> GetAllKnownKeys()
        {
            return new HashSet<string>
            {
                "warship", "warplane", "satellite", "missile", "militaryvehicle", "submarine"
            };
        }

        public static void ClearCache()
        {
            // Resources.Load 的 Texture2D 若在 Resources 目录下，Unity 自动管理生命周期
            _cache.Clear();
        }
    }
}
