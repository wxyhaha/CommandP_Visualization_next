using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

namespace CommandP.GlobalEntity.Rendering
{
    /// <summary>
    /// GLB model view manager.
    /// Hierarchy: EntityRoot(GlobeAnchor ENU) -> ModelRoot(heading) -> RotationFix(model fix) -> GLB
    ///
    /// Orientation:
    ///   GlobeAnchor (adjustOrientation=true) sets EntityRoot.rotation to local ENU.
    ///   In ENU: +X=East, +Y=Up, +Z=North.
    ///   We apply heading as Euler(0, heading, 0) on ModelRoot, rotating around Up (Y).
    ///   RotationFix corrects each model's forward axis to align with +Z (Unity forward).
    /// </summary>
    public class ModelViewSystem
    {
        private readonly ModelCache _modelCache;
        private readonly IReadOnlyDictionary<string, GoPoolEntry> _viewEntries;

        private static readonly Dictionary<EntityType, float> ModelScales = new()
        {
            { EntityType.Ship,          25f },
            { EntityType.Aircraft,      25f },
            { EntityType.Satellite,     25f },
            { EntityType.Missile,       25f },
            { EntityType.GroundVehicle, 25f },
        };

        /// <summary>
        /// Per-model rotation correction applied on RotationFix.
        /// Aligns model's nose with Unity +Z so heading rotation works.
        /// </summary>
        private static readonly Dictionary<string, Vector3> ModelCorrections = new()
        {
            { "fa-18f",                                     new Vector3(0f, -80f, 0f) },
            { "mig29",                                      new Vector3(0f, 53f, 0f) },
            { "bengaluru_class_destroyer_d67",              Vector3.zero },
            { "the_project_941__akula__typhoon_submarine",  Vector3.zero },
            { "ugm-84",                                     Vector3.zero },
            { "mim-104",                                    Vector3.zero },
            { "satellite",                                  Vector3.zero },
        };

        /// <summary>
        /// Per-entity-type runtime rotation offset (debug panel).
        /// </summary>
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
        /// Instantiate model for entity (LOD near switch).
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

            entry.ModelRoot.transform.localRotation = Quaternion.identity;

            var rotationFix = new GameObject("RotationFix");
            rotationFix.transform.SetParent(entry.ModelRoot.transform, false);
            rotationFix.transform.localPosition = Vector3.zero;

            string modelKey = _modelCache.GetModelKey(entity);
            var instance = _modelCache.InstantiateModel(modelKey, rotationFix.transform, entity.ObjectId);

            if (instance != null)
            {
                float scale = ModelScales.TryGetValue(entity.Type, out var s) ? s : 25f;
                instance.transform.localScale = Vector3.one * scale;

                Vector3 modelCorr = ModelCorrections.TryGetValue(modelKey, out var mc) ? mc : Vector3.zero;
                Vector3 typeOffset = ModelRotationOffsets.TryGetValue(entity.Type, out var to) ? to : Vector3.zero;
                rotationFix.transform.localRotation = Quaternion.Euler(modelCorr + typeOffset);

                entry.ModelInstance = instance;
                entry.ModelRoot.SetActive(true);
            }
        }

        /// <summary>
        /// Hide entity model (LOD far switch).
        /// </summary>
        public void HideModel(EntityData entity)
        {
            if (!_viewEntries.TryGetValue(entity.ObjectId, out var entry) || entry == null)
                return;

            entry.ModelRoot.SetActive(false);
            if (entry.ModelInstance != null)
                entry.ModelInstance.SetActive(false);
        }

        /// <summary>
        /// Remove model instance.
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

        /// <summary>
        /// Per-frame: apply heading rotation on ModelRoot.
        /// GlobeAnchor (adjustOrientation=true) handles ENU on EntityRoot.
        /// In ENU: +Y=Up, +Z=North. So Euler(0, heading, 0) rotates around Up axis.
        /// heading=0 -> +Z points North; heading=90 -> +Z points East.
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

                float headingAbs = e.HeadingDeg % 360f;
                if (headingAbs < 0f) headingAbs += 360f;
                entry.ModelRoot.transform.localRotation = Quaternion.Euler(0f, headingAbs, 0f);
            }
        }
    }
}