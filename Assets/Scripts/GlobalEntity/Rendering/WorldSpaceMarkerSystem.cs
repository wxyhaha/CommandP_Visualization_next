using System;
using System.Collections.Generic;
using CommandP.GlobalEntity.Icons;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace CommandP.GlobalEntity.Rendering
{
    /// <summary>
    /// Per-entity-type tactical marker configuration. All units in meters (world space).
    /// </summary>
    [Serializable]
    public class MarkerConfig
    {
        public float HeightOffset;
        public float IconSize;
        public float LabelWidth;
        public float LabelHeight;
        public float FontSize;
        public float LabelGap;
        public float ReferenceDistance; // distance at which scale=1 (typically the LOD switch distance)
    }

    /// <summary>
    /// World Space Tactical Marker System.
    ///
    /// Each entity's MarkerRoot is a child of EntityRoot (CesiumGlobeAnchor), giving it:
    ///   - Real world position with true altitude
    ///   - Natural occlusion by terrain, buildings, and Earth curvature
    ///   - Automatic CesiumOriginShift participation
    ///
    /// Billboard facing: markerRoot.rotation = camera.rotation (no LookRotation, no yaw-only).
    /// Canvas: RenderMode.WorldSpace, child of MarkerRoot.
    /// </summary>
    public class WorldSpaceMarkerSystem : IDisposable
    {
        private readonly Camera _targetCamera;
        private readonly Dictionary<string, Material> _iconMaterials = new();
        private static Mesh _quadMesh;

        public static readonly Dictionary<EntityType, MarkerConfig> Configs = new()
        {
            [EntityType.Ship] = new MarkerConfig
                { HeightOffset = 200f,  IconSize = 500f,  LabelWidth = 800f,  LabelHeight = 160f, FontSize = 120f, LabelGap = 100f,  ReferenceDistance = 6000f },
            [EntityType.Aircraft] = new MarkerConfig
                { HeightOffset = 500f,  IconSize = 1000f, LabelWidth = 1000f, LabelHeight = 200f, FontSize = 150f, LabelGap = 150f, ReferenceDistance = 10000f },
            [EntityType.Satellite] = new MarkerConfig
                { HeightOffset = 5000f, IconSize = 5000f, LabelWidth = 2000f, LabelHeight = 400f, FontSize = 300f, LabelGap = 500f, ReferenceDistance = 80000f },
            [EntityType.Missile] = new MarkerConfig
                { HeightOffset = 500f,  IconSize = 500f,  LabelWidth = 600f,  LabelHeight = 120f, FontSize = 90f,  LabelGap = 80f,   ReferenceDistance = 4000f },
            [EntityType.GroundVehicle] = new MarkerConfig
                { HeightOffset = 200f,  IconSize = 400f,  LabelWidth = 600f,  LabelHeight = 120f, FontSize = 90f,  LabelGap = 80f,   ReferenceDistance = 5000f },
        };

        public WorldSpaceMarkerSystem(Camera camera)
        {
            _targetCamera = camera;
            EnsureQuadMesh();
        }

        // ============================================================
        // Public API
        // ============================================================

        /// <summary>
        /// Configure (or reconfigure) MarkerRoot visuals for an entity.
        /// Safe to call on pool-recycled entries.
        /// </summary>
        public void SetupMarker(GoPoolEntry entry, EntityData entity)
        {
            if (entry?.MarkerRoot == null || entity == null) return;

            var cfg = Configs.TryGetValue(entity.Type, out var c) ? c : Configs[EntityType.Ship];

            entry.MarkerRoot.transform.localPosition = new Vector3(0, cfg.HeightOffset, 0);

            if (entry.Billboard != null)
            {
                entry.Billboard.AssignCamera(_targetCamera);
                // Distance scale: at ReferenceDistance, scale=1; closer→smaller, farther→larger
                entry.Billboard.ConfigureScale(
                    enable: true,
                    referenceDist: cfg.ReferenceDistance,
                    min: 0.3f,
                    max: 50f);
            }

            SetupIcon(entry, entity, cfg);
            SetupLabel(entry, entity, cfg);
        }

        public void ShowMarker(GoPoolEntry entry, EntityData entity)
        {
            if (entry?.MarkerRoot == null) return;
            SetupMarker(entry, entity);
            entry.MarkerRoot.SetActive(true);
        }

        public void HideMarker(GoPoolEntry entry)
        {
            if (entry?.MarkerRoot != null)
                entry.MarkerRoot.SetActive(false);
        }

        public void UpdateLabel(GoPoolEntry entry, EntityData entity)
        {
            if (entry?.MarkerLabel != null && entity != null)
                entry.MarkerLabel.text = entity.DisplayName;
        }

        // ============================================================
        // Icon Quad
        // ============================================================

        private void SetupIcon(GoPoolEntry entry, EntityData entity, MarkerConfig cfg)
        {
            var iconGo = EnsureChild(entry.MarkerRoot, "IconQuad");

            var mf = iconGo.GetComponent<MeshFilter>();
            if (mf == null) mf = iconGo.AddComponent<MeshFilter>();
            mf.sharedMesh = _quadMesh;

            var mr = iconGo.GetComponent<MeshRenderer>();
            if (mr == null) mr = iconGo.AddComponent<MeshRenderer>();
            mr.sharedMaterial = GetOrCreateMaterial(entity);

            iconGo.transform.localPosition = Vector3.zero;
            iconGo.transform.localRotation = Quaternion.identity;
            iconGo.transform.localScale = new Vector3(cfg.IconSize, cfg.IconSize, 1);

            if (iconGo.GetComponent<BoxCollider>() == null)
            {
                var bc = iconGo.AddComponent<BoxCollider>();
                bc.size = new Vector3(1, 1, 0.1f);
            }

            entry.IconRenderer = mr;
        }

        // ============================================================
        // World Space Canvas Label
        // ============================================================

        private void SetupLabel(GoPoolEntry entry, EntityData entity, MarkerConfig cfg)
        {
            var canvasGo = EnsureChild(entry.MarkerRoot, "WorldSpaceCanvas");

            var canvas = canvasGo.GetComponent<Canvas>();
            if (canvas == null) canvas = canvasGo.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;

            float yOff = -cfg.IconSize * 0.5f - cfg.LabelGap - cfg.LabelHeight * 0.5f;
            canvasGo.transform.localPosition = new Vector3(0, yOff, 0);
            canvasGo.transform.localRotation = Quaternion.identity;

            var rt = canvasGo.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(cfg.LabelWidth, cfg.LabelHeight);

            var labelGo = EnsureChild(canvasGo, "LabelText");
            var label = labelGo.GetComponent<TextMeshProUGUI>();
            if (label == null) label = labelGo.AddComponent<TextMeshProUGUI>();

            label.text = entity.DisplayName;
            label.fontSize = cfg.FontSize;
            label.alignment = TextAlignmentOptions.Center;
            label.color = Color.white;
            label.fontStyle = FontStyles.Bold;

            var lrt = labelGo.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;

            entry.MarkerLabel = label;
        }

        // ============================================================
        // Material Cache
        // ============================================================

        private Material GetOrCreateMaterial(EntityData entity)
        {
            string iconKey = entity.IconKey ?? IconGenerator.GetIconKey(entity.Type);
            if (_iconMaterials.TryGetValue(iconKey, out var cached))
                return cached;

            var tex = IconGenerator.GetIconByKey(iconKey)
                   ?? IconGenerator.GetIcon(EntityType.Ship);
            if (tex == null) return null;

            var mat = BuildIconMaterial(tex);
            _iconMaterials[iconKey] = mat;
            return mat;
        }

        private static Material BuildIconMaterial(Texture2D tex)
        {
            var shader = Shader.Find("Universal Render Pipeline/Unlit")
                      ?? Shader.Find("Unlit/Transparent")
                      ?? Shader.Find("Unlit/Texture")
                      ?? Shader.Find("Sprites/Default");

            if (shader == null)
            {
                Debug.LogError("[WorldSpaceMarker] No usable shader for icon material");
                return null;
            }

            var mat = new Material(shader) { mainTexture = tex, color = Color.white };

            if (shader.name == "Universal Render Pipeline/Unlit" && mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.SetInt("_Cull", (int)CullMode.Off);
                mat.renderQueue = 3000;
            }

            mat.name = $"IconMat_{tex.name}";
            return mat;
        }

        // ============================================================
        // Shared Quad Mesh
        // ============================================================

        private static void EnsureQuadMesh()
        {
            if (_quadMesh != null) return;
            _quadMesh = new Mesh
            {
                name = "BillboardQuad",
                vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, 0),
                    new Vector3( 0.5f, -0.5f, 0),
                    new Vector3(-0.5f,  0.5f, 0),
                    new Vector3( 0.5f,  0.5f, 0),
                },
                triangles = new[] { 0, 2, 1, 2, 3, 1 },
                normals = new[] { Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward },
                uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) },
            };
            _quadMesh.RecalculateBounds();
        }

        // ============================================================
        // Helpers
        // ============================================================

        private static GameObject EnsureChild(GameObject parent, string name)
        {
            var t = parent.transform.Find(name);
            if (t != null) return t.gameObject;
            var go = new GameObject(name);
            go.transform.SetParent(parent.transform, false);
            return go;
        }

        public void Dispose()
        {
            foreach (var kv in _iconMaterials)
            {
                if (kv.Value != null)
                    UnityEngine.Object.Destroy(kv.Value);
            }
            _iconMaterials.Clear();
        }
    }
}
