using UnityEngine;

namespace CommandP.Core
{
    /// <summary>
    /// 半透明视锥体雷达探测区域。
    /// 锥尖位于挂载点原点（紧贴机头），沿本地 +Z 向前延伸。
    /// 纯色填充，无网格/扫描/脉冲。
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public class AircraftRadarScanEffect : MonoBehaviour
    {
        [Header("启用")]
        [SerializeField] private bool _enabled = true;

        [Header("视锥参数")]
        [SerializeField, Min(5f)] private float _length = 300f;
        [SerializeField, Range(5f, 85f)] private float _halfAngleDeg = 30f;
        [SerializeField, Range(12, 64)] private int _sides = 32;

        [Header("半透明材质")]
        [SerializeField] private Color _fillColor = new Color(0.95f, 0.22f, 0.06f, 0.35f);
        [SerializeField, Min(0.01f)] private float _duration = 2f; // seconds per cycle
        [SerializeField, Range(1, 200)] private int _repeat = 30; // number of rings
        [SerializeField] private float _offset = 0f;
        [SerializeField, Range(0f, 1f)] private float _thickness = 0.3f;

        // 内部
        private MeshFilter _mf;
        private MeshRenderer _mr;
        private Material _fillMat;
        private bool _inited;
        private float _timeAcc;

        #region 公开配置

        public void Configure(
            bool enabled,
            float radius,
            float halfAngleDeg,
            int longitudeLines,
            int latitudeLines,
            int meshSegments,
            Color fillColor,
            float fillBrightCenter,
            float gridLineWidth,
            Color gridColor,
            float sweepArcWidth,
            float sweepPeriodSec,
            Color sweepColor,
            Color sweepGlowColor,
            int sweepArcSegments,
            float breathSpeed,
            float breathAlphaMin,
            float breathAlphaMax)
        {
            _enabled = enabled;
            _length = radius;
            _halfAngleDeg = halfAngleDeg;
            _sides = Mathf.Clamp(meshSegments, 12, 64);
            _fillColor = fillColor;

            // map scan motion params to shader uniforms
            _duration = Mathf.Max(0.01f, sweepPeriodSec);
            _thickness = Mathf.Clamp01(sweepArcWidth);

            InternalInit();
            RebuildAll();
        }

        #endregion

        #region MonoBehaviour

        private void Awake() { InternalInit(); RebuildAll(); }
        private void OnEnable() { InternalInit(); RebuildAll(); }

        private void Update()
        {
            if (!_inited) return;
            // animate shader time and update uniforms
            if (_fillMat != null)
            {
                _timeAcc += Time.deltaTime;
                float dur = Mathf.Max(0.0001f, _duration);
                float t = (_timeAcc % dur) / dur; // normalized [0,1)
                _fillMat.SetFloat("_ScanTime", t);
                _fillMat.SetFloat("_Length", _length);
                _fillMat.SetFloat("_Repeat", _repeat);
                _fillMat.SetFloat("_Offset", _offset);
                _fillMat.SetFloat("_Thickness", _thickness);
                _fillMat.color = _fillColor;
            }
            SetVisible(_enabled);
        }

        private void OnDestroy()
        {
            CleanupMesh();
            if (_fillMat != null) { Destroy(_fillMat); _fillMat = null; }
        }

        #endregion

        #region 初始化

        private void InternalInit()
        {
            if (_inited) return;

            _mf = GetComponent<MeshFilter>();
            _mr = GetComponent<MeshRenderer>();
            if (_mf == null) _mf = gameObject.AddComponent<MeshFilter>();
            if (_mr == null) _mr = gameObject.AddComponent<MeshRenderer>();

            Shader shader = Shader.Find("Unlit/RadarRadial")
                           ?? Shader.Find("Universal Render Pipeline/Unlit")
                           ?? Shader.Find("Sprites/Default");
            if (_fillMat == null && shader != null)
            {
                _fillMat = new Material(shader);
                SetupTransparent(_fillMat);
                _fillMat.color = _fillColor;
                _fillMat.SetFloat("_Length", _length);
                _fillMat.SetFloat("_Repeat", _repeat);
                _fillMat.SetFloat("_Offset", _offset);
                _fillMat.SetFloat("_Thickness", _thickness);
                _fillMat.SetFloat("_ScanTime", 0f);
            }
            if (_fillMat != null) _mr.material = _fillMat;
            _mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _mr.receiveShadows = false;

            _inited = true;
        }

        private static void SetupTransparent(Material m)
        {
            if (m == null) return;
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_BlendMode", 0f);
            m.SetFloat("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_AlphaClip", 0f);
            m.SetShaderPassEnabled("DepthOnly", false);
            m.SetShaderPassEnabled("SHADOWCASTER", false);
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.EnableKeyword("_ALPHABLEND_ON");
        }

        #endregion

        #region 构建

        private void RebuildAll()
        {
            if (!_inited) return;
            BuildFrustumMesh();
        }

        #endregion

        #region 视锥体 Mesh

        /// <summary>
        /// 构建平顶视锥体（frustum/cone）。
        /// 锥尖在原点(0,0,0)，沿 +Z 延伸到底面半径为 radius 的圆。
        /// </summary>
        private void BuildFrustumMesh()
        {
            float len = Mathf.Max(10f, _length);
            float halfAngle = Mathf.Clamp(_halfAngleDeg, 2f, 85f) * Mathf.Deg2Rad;
            float radius = len * Mathf.Tan(halfAngle);
            int sides = Mathf.Clamp(_sides, 6, 64);

            int vertCount = 1 + sides;
            var verts = new Vector3[vertCount];
            var tris = new int[sides * 3];

            // 顶点0: 锥尖
            verts[0] = Vector3.zero;

            // 底面一圈
            for (int i = 0; i < sides; i++)
            {
                float a = (360f / sides) * i * Mathf.Deg2Rad;
                verts[1 + i] = new Vector3(
                    radius * Mathf.Cos(a),
                    radius * Mathf.Sin(a),
                    len);
            }

            // 三角形扇: 锥尖 → 底面连续边
            for (int i = 0; i < sides; i++)
            {
                int next = (i + 1) % sides;
                tris[i * 3 + 0] = 0;
                tris[i * 3 + 1] = 1 + next;
                tris[i * 3 + 2] = 1 + i;
            }

            var mesh = new Mesh();
            mesh.vertices = verts;
            mesh.triangles = tris;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            mesh.name = "RadarFrustum";

            CleanupMesh();
            _mf.sharedMesh = mesh;
        }

        private void CleanupMesh()
        {
            if (_mf != null && _mf.sharedMesh != null)
            {
#if UNITY_EDITOR
                if (!Application.isPlaying) DestroyImmediate(_mf.sharedMesh);
                else Destroy(_mf.sharedMesh);
#else
                Destroy(_mf.sharedMesh);
#endif
                _mf.sharedMesh = null;
            }
        }

        #endregion

        #region 可见性

        private void SetVisible(bool v)
        {
            if (_mr != null) _mr.enabled = v;
        }

        #endregion
    }
}