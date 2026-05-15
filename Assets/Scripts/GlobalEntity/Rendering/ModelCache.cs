using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CommandP.GlobalEntity.Rendering
{
    /// <summary>
    /// GLB 模型 Resources.Load 缓存层。
    /// 启动时预加载所有模型，运行时用缓存返回。
    /// </summary>
    public class ModelCache
    {
        private readonly Dictionary<string, GameObject> _cache = new Dictionary<string, GameObject>();

        // 类型 → 默认模型键值映射
        private static readonly Dictionary<EntityType, string> DefaultModelKeys = new Dictionary<EntityType, string>
        {
            { EntityType.Ship,      "bengaluru_class_destroyer_d67" },
            { EntityType.Aircraft,  "fa-18f" },
            { EntityType.Satellite, "satellite" },
            { EntityType.Missile,   "ugm-84" },
            { EntityType.GroundVehicle, "mim-104" },
        };

        // 备用模型 (切换类型时可选用)
        private static readonly Dictionary<EntityType, string> AlternateModelKeys = new Dictionary<EntityType, string>
        {
            { EntityType.Ship,      "the_project_941__akula__typhoon_submarine" },
            { EntityType.Aircraft,  "mig29" },
        };

        private const string ResourcesPrefix = "Models/";

        /// <summary>
        /// 同步预加载所有已知模型
        /// </summary>
        public void PreloadAll()
        {
            var allKeys = new HashSet<string>();
            foreach (var v in DefaultModelKeys.Values) allKeys.Add(v);
            foreach (var v in AlternateModelKeys.Values) allKeys.Add(v);

            foreach (var key in allKeys)
            {
                LoadModel(key);
            }
        }

        /// <summary>
        /// 获取模型 prefab (从缓存或懒加载)
        /// </summary>
        public GameObject GetModelPrefab(string assetKey)
        {
            if (string.IsNullOrEmpty(assetKey))
                return GetDefaultModel(EntityType.Ship);

            if (_cache.TryGetValue(assetKey, out var prefab))
                return prefab;

            return LoadModel(assetKey);
        }

        /// <summary>
        /// 获取类型的默认模型
        /// </summary>
        public GameObject GetDefaultModel(EntityType type)
        {
            string key = DefaultModelKeys.TryGetValue(type, out var k)
                ? k
                : "fa-18f";

            return GetModelPrefab(key);
        }

        /// <summary>
        /// 在指定父节点下实例化模型
        /// </summary>
        public GameObject InstantiateModel(string assetKey, Transform parent, string objectId)
        {
            var prefab = GetModelPrefab(assetKey);
            if (prefab == null)
            {
                Debug.LogError($"[ModelCache] Failed to load model '{assetKey}' for entity '{objectId}'");
                return null;
            }

            var instance = Object.Instantiate(prefab, parent, false);
            instance.name = $"Model_{objectId}";

            // 清理不需要的组件
            RemoveUnwantedComponents(instance);

            return instance;
        }

        /// <summary>
        /// 根据实体类型获取合适的模型
        /// </summary>
        public string GetModelKey(EntityData entity)
        {
            if (!string.IsNullOrEmpty(entity.ModelAssetKey))
                return entity.ModelAssetKey;

            if (DefaultModelKeys.TryGetValue(entity.Type, out var key))
                return key;

            return "fa-18f";
        }

        private GameObject LoadModel(string assetKey)
        {
            string path = ResourcesPrefix + assetKey;
            var prefab = Resources.Load<GameObject>(path);

            if (prefab == null)
            {
                Debug.LogWarning($"[ModelCache] Model not found at Resources/{path}");
                return null;
            }

            _cache[assetKey] = prefab;
            return prefab;
        }

        public void ClearCache()
        {
            _cache.Clear();
            Debug.Log("[ModelCache] Cache cleared.");
        }

        public void ReloadAll()
        {
            ClearCache();
            PreloadAll();
            Debug.Log("[ModelCache] All models reloaded from disk.");
        }

        private static void RemoveUnwantedComponents(GameObject instance)
        {
            if (instance == null) return;

            // 移除可能影响性能的组件
            foreach (var c in instance.GetComponentsInChildren<Collider>(true))
            {
                Object.Destroy(c);
            }
            foreach (var c in instance.GetComponentsInChildren<Rigidbody>(true))
            {
                Object.Destroy(c);
            }
            foreach (var c in instance.GetComponentsInChildren<Animator>(true))
            {
                Object.Destroy(c);
            }
            foreach (var c in instance.GetComponentsInChildren<AudioSource>(true))
            {
                Object.Destroy(c);
            }
        }
    }
}
