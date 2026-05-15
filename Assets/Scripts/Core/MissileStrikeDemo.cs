using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using CesiumForUnity;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace CommandP.Core
{
    /// <summary>
    /// 导弹打击演示系统
    /// 从发射点向上飞行，然后飞向打击点并爆炸
    /// </summary>
    public class MissileStrikeDemo : MonoBehaviour
    {
        [Header("Strike Points")]
        [SerializeField] private Transform _launchPointMarker;
        [SerializeField] private Transform _strikePointMarker;

        [Header("Missile Settings")]
        [SerializeField] private float _flightDuration = 8f;
        [SerializeField] private float _maxAltitude = 500f;
        [SerializeField] private float _strikeEffectScale = 2f;

        [Header("Asset References")]
        [SerializeField] private GameObject _missileModelPrefab;
        [SerializeField] private GameObject _destroyEffectPrefab;
        [SerializeField] private GameObject _smokePrefab;
        [SerializeField] private Vector3 _missileRotationOffsetEuler = new Vector3(0f, 90f, 0f);

        [Header("Burning Effect")]
        [SerializeField] private GameObject _burningFirePrefab;
        [SerializeField] private float _burningDuration = 0f;

        [Header("Smoke Trail Color")]
        [SerializeField] private Color _smokeTrailColor = new Color(0.7f, 0.7f, 0.7f, 1f);

        [Header("Missile Label Board")]
        [SerializeField] private string _missileDisplayName = "AGM-84 Harpoon";
        [SerializeField] private string _missileTypeIcon = "◆";
        [SerializeField] private Color _factionColor = new Color(0.9f, 0.15f, 0.1f, 0.25f);
        [SerializeField] private float _boardScreenScale = 0.032f;
        [SerializeField] private float _boardVerticalOffset = 5f;

        private CesiumGlobeAnchor _cesiumAnchor;
        private GameObject _activeMissile;
        private GameObject _smokeTrail;
        private GameObject _activeBurningFire;
        private GameObject _missileBoard;
        private TextMesh _boardLabel;
        private GameObject _boardBackground;
        private Renderer _boardBackgroundRenderer;
        private float _flightTimer;
        private bool _isFlying;
        private bool _isPaused;

        private const string MissileVisualRootName = "MissileVisualOffsetRoot";

        private void ApplyMissileModelRotationOffset(GameObject go)
        {
            if (go == null)
            {
                return;
            }

            Transform visualRoot = go.transform.Find(MissileVisualRootName);
            if (visualRoot == null)
            {
                GameObject rootGo = new GameObject(MissileVisualRootName);
                rootGo.transform.SetParent(go.transform, false);
                visualRoot = rootGo.transform;

                List<Transform> children = new List<Transform>();
                for (int i = 0; i < go.transform.childCount; i++)
                {
                    Transform child = go.transform.GetChild(i);
                    if (child != visualRoot)
                    {
                        children.Add(child);
                    }
                }

                foreach (Transform child in children)
                {
                    child.SetParent(visualRoot, true);
                }
            }

            visualRoot.localPosition = Vector3.zero;
            visualRoot.localRotation = Quaternion.Euler(_missileRotationOffsetEuler);
        }

        private void ApplySmokeTrailColor(GameObject smokeTrail)
        {
            if (smokeTrail == null)
            {
                return;
            }

            ParticleSystem[] particleSystems = smokeTrail.GetComponentsInChildren<ParticleSystem>(true);
            foreach (ParticleSystem ps in particleSystems)
            {
                if (ps == null)
                {
                    continue;
                }

                ParticleSystem.MainModule main = ps.main;
                main.startColor = new ParticleSystem.MinMaxGradient(_smokeTrailColor);

                ParticleSystem.ColorOverLifetimeModule colorOverLifetime = ps.colorOverLifetime;
                colorOverLifetime.enabled = true;

                Gradient gradient = new Gradient();
                gradient.SetKeys(
                    new[]
                    {
                        new GradientColorKey(_smokeTrailColor, 0f),
                        new GradientColorKey(_smokeTrailColor, 1f)
                    },
                    new[]
                    {
                        new GradientAlphaKey(_smokeTrailColor.a, 0f),
                        new GradientAlphaKey(0f, 1f)
                    });
                colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

                ParticleSystem.ColorBySpeedModule colorBySpeed = ps.colorBySpeed;
                colorBySpeed.enabled = false;

                ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
                if (renderer != null)
                {
                    Shader smokeShader = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                        ?? Shader.Find("Universal Render Pipeline/Unlit")
                        ?? Shader.Find("Sprites/Default")
                        ?? Shader.Find("Unlit/Color");

                    if (smokeShader != null)
                    {
                        Material smokeMaterial = new Material(smokeShader);
                        Material sourceMaterial = renderer.sharedMaterial;
                        if (sourceMaterial != null)
                        {
                            Texture mainTexture = sourceMaterial.mainTexture;
                            if (mainTexture != null)
                            {
                                if (smokeMaterial.HasProperty("_BaseMap"))
                                {
                                    smokeMaterial.SetTexture("_BaseMap", mainTexture);
                                }

                                if (smokeMaterial.HasProperty("_MainTex"))
                                {
                                    smokeMaterial.SetTexture("_MainTex", mainTexture);
                                }
                            }
                        }

                        if (smokeMaterial.HasProperty("_BaseColor"))
                        {
                            smokeMaterial.SetColor("_BaseColor", _smokeTrailColor);
                        }

                        if (smokeMaterial.HasProperty("_Color"))
                        {
                            smokeMaterial.SetColor("_Color", _smokeTrailColor);
                        }

                        renderer.sharedMaterial = smokeMaterial;
                    }
                    else if (renderer.material != null)
                    {
                        renderer.material.color = _smokeTrailColor;
                    }
                }
            }
        }

        private void Start()
        {
            _cesiumAnchor = GetComponent<CesiumGlobeAnchor>();
            if (_cesiumAnchor == null)
            {
                _cesiumAnchor = gameObject.AddComponent<CesiumGlobeAnchor>();
            }

            // 确保序列化旧值的字段使用更新后的默认值
            if (_boardVerticalOffset > 5f) _boardVerticalOffset = 0.3f;

            if (_missileModelPrefab == null)
            {
#if UNITY_EDITOR
                _missileModelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Models/ugm-84.glb");
                if (_missileModelPrefab == null)
                {
                    _missileModelPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/homing missile/prefabs/Missil_05.prefab");
                }
#endif
            }

            if (_destroyEffectPrefab == null)
            {
#if UNITY_EDITOR
                _destroyEffectPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/homing missile/prefabs/rocket_destroy_effect.prefab");
#endif
            }

            if (_smokePrefab == null)
            {
#if UNITY_EDITOR
                _smokePrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/homing missile/prefabs/rocket_smoke.prefab");
#endif
            }

            if (_launchPointMarker == null)
            {
                _launchPointMarker = CreateMarker("LaunchPoint", Vector3.zero);
            }

            if (_strikePointMarker == null)
            {
                _strikePointMarker = CreateMarker("StrikePoint", new Vector3(500, 0, 500));
            }
        }

        private void Update()
        {
            if (_activeMissile != null)
            {
                if (_missileBoard != null)
                {
                    UpdateMissileBoard();
                }

                if (_isFlying && !_isPaused)
                {
                    UpdateMissileTrajectory();
                }
            }
        }

        /// <summary>
        /// 启动导弹发射演示
        /// </summary>
        public void LaunchDemo()
        {
            if (_isFlying)
            {
                return;
            }

            if (_activeMissile != null)
            {
                Destroy(_activeMissile);
            }
            _activeMissile = null;
            _missileBoard = null;
            _boardLabel = null;
            _boardBackground = null;
            _boardBackgroundRenderer = null;

            if (_activeBurningFire != null)
            {
                Destroy(_activeBurningFire);
                _activeBurningFire = null;
            }

            if (_smokeTrail != null)
            {
                Destroy(_smokeTrail);
                _smokeTrail = null;
            }

            _activeMissile = CreateMissileVisual();
            _isFlying = true;
            _flightTimer = 0f;

            Debug.Log("[MissileStrikeDemo] Missile launched!");
        }

        /// <summary>
        /// 更新导弹飞行轨迹
        /// </summary>
        private void UpdateMissileTrajectory()
        {
            _flightTimer += Time.deltaTime;
            float progress = _flightTimer / _flightDuration;

            if (progress >= 1f)
            {
                _isFlying = false;
                CreateStrikeEffect();
                return;
            }

            Vector3 launchPos = _launchPointMarker.position;
            Vector3 strikePos = _strikePointMarker.position;
            Vector3 horizontalDelta = strikePos - launchPos;

            float altitudeProgress;
            Vector3 targetPos;

            if (progress < 0.5f)
            {
                altitudeProgress = progress * 2f;
                float currentAltitude = Mathf.Lerp(0, _maxAltitude, Mathf.Pow(altitudeProgress, 1.5f));
                targetPos = launchPos + horizontalDelta * (altitudeProgress * 0.3f) + Vector3.up * currentAltitude;
            }
            else
            {
                altitudeProgress = (progress - 0.5f) * 2f;
                float currentAltitude = Mathf.Lerp(_maxAltitude, 0, altitudeProgress);
                targetPos = launchPos + horizontalDelta * (0.3f + altitudeProgress * 0.7f) + Vector3.up * currentAltitude;
            }

            _activeMissile.transform.position = targetPos;

            if (progress >= 0.45f && progress <= 0.55f && _smokeTrail == null && _smokePrefab != null)
            {
                _smokeTrail = Instantiate(_smokePrefab, targetPos, Quaternion.identity);
                _smokeTrail.transform.SetParent(_activeMissile.transform);
                _smokeTrail.transform.localPosition = Vector3.zero;

                ApplySmokeTrailColor(_smokeTrail);

                ParticleSystem ps = _smokeTrail.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    ParticleSystem.MainModule main = ps.main;
                    main.startColor = _smokeTrailColor;
                    ps.Play();
                }
            }

            if (progress < 0.99f)
            {
                Vector3 nextPos = _activeMissile.transform.position;
                float nextProgress = Mathf.Min(progress + 0.01f, 1f);
                float nextAltitudeProgress;
                Vector3 nextTargetPos;

                if (nextProgress < 0.5f)
                {
                    nextAltitudeProgress = nextProgress * 2f;
                    float nextAltitude = Mathf.Lerp(0, _maxAltitude, Mathf.Pow(nextAltitudeProgress, 1.5f));
                    nextTargetPos = launchPos + horizontalDelta * (nextAltitudeProgress * 0.3f) + Vector3.up * nextAltitude;
                }
                else
                {
                    nextAltitudeProgress = (nextProgress - 0.5f) * 2f;
                    float nextAltitude = Mathf.Lerp(_maxAltitude, 0, nextAltitudeProgress);
                    nextTargetPos = launchPos + horizontalDelta * (0.3f + nextAltitudeProgress * 0.7f) + Vector3.up * nextAltitude;
                }

                Vector3 direction = (nextTargetPos - nextPos).normalized;
                if (direction.sqrMagnitude > 0.001f)
                {
                    _activeMissile.transform.rotation = Quaternion.LookRotation(direction);
                }
            }
        }

        /// <summary>
        /// 创建导弹视觉表现
        /// </summary>
        private GameObject CreateMissileVisual()
        {
            GameObject missile;

            if (_missileModelPrefab != null)
            {
                missile = Instantiate(_missileModelPrefab);
                missile.name = "DemoMissile";
                missile.transform.localScale *= 1.5f;

                foreach (Collider collider in missile.GetComponentsInChildren<Collider>())
                {
                    Destroy(collider);
                }
                foreach (Rigidbody rb in missile.GetComponentsInChildren<Rigidbody>())
                {
                    Destroy(rb);
                }
                foreach (Animator animator in missile.GetComponentsInChildren<Animator>())
                {
                    Destroy(animator);
                }
            }
            else
            {
                missile = new GameObject("DemoMissile");
                GameObject capsule = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                capsule.transform.SetParent(missile.transform);
                capsule.transform.localPosition = Vector3.zero;
                capsule.transform.localRotation = Quaternion.Euler(90, 0, 0);
                capsule.transform.localScale = new Vector3(0.3f, 1.2f, 0.3f);

                Collider collider = capsule.GetComponent<Collider>();
                if (collider != null)
                {
                    Destroy(collider);
                }

                Material missileMat = new Material(Shader.Find("Standard"));
                missileMat.color = new Color(1f, 0.2f, 0.2f, 1f);
                capsule.GetComponent<Renderer>().material = missileMat;
            }

            if (_launchPointMarker != null)
            {
                missile.transform.position = _launchPointMarker.position;
            }

            TrailRenderer trail = missile.AddComponent<TrailRenderer>();
            Shader trailShader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default") ?? Shader.Find("Unlit/Color");
            Material trailMat = trailShader != null ? new Material(trailShader) : new Material(Shader.Find("Standard"));
            trail.material = trailMat;

            Color tailColor = new Color(0.82f, 0.82f, 0.85f, 0.95f);
            Gradient grad = new Gradient();
            grad.SetKeys(
                new[]
                {
                    new GradientColorKey(tailColor, 0f),
                    new GradientColorKey(tailColor, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(tailColor.a, 0f),
                    new GradientAlphaKey(0.35f, 1f)
                }
            );
            trail.colorGradient = grad;
            trail.time = 2f;
            trail.startWidth = 0.15f;
            trail.endWidth = 0.05f;
            trail.alignment = LineAlignment.View;
            trail.autodestruct = false;

            CesiumGeoreference georeference = FindObjectOfType<CesiumGeoreference>();
            if (georeference != null)
            {
                missile.transform.SetParent(georeference.transform);
            }

            ApplyMissileModelRotationOffset(missile);
            missile.transform.rotation = Quaternion.identity;

            CreateMissileBoard(missile);

            Debug.Log("[MissileStrikeDemo] Missile created with model: " + ((_missileModelPrefab != null) ? _missileModelPrefab.name : "fallback capsule"));

            return missile;
        }

        private void CreateMissileBoard(GameObject parentMissile)
        {
            if (parentMissile == null)
            {
                return;
            }

            _missileBoard = new GameObject("MissileBoard");
            _missileBoard.transform.SetParent(parentMissile.transform, false);
            _missileBoard.transform.localPosition = Vector3.up * _boardVerticalOffset;
            _missileBoard.transform.localRotation = Quaternion.identity;
            _missileBoard.transform.localScale = Vector3.one;

            _boardBackground = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _boardBackground.name = "BoardBackground";
            _boardBackground.transform.SetParent(_missileBoard.transform, false);
            _boardBackground.transform.localPosition = Vector3.zero;
            _boardBackground.transform.localRotation = Quaternion.Euler(0, 180, 0);
            _boardBackground.transform.localScale = new Vector3(0.35f, 0.1f, 1f);

            Collider bgCollider = _boardBackground.GetComponent<Collider>();
            if (bgCollider != null)
            {
                Destroy(bgCollider);
            }

            Shader bgShader = Shader.Find("Universal Render Pipeline/Unlit")
                ?? Shader.Find("Sprites/Default")
                ?? Shader.Find("Unlit/Color");

            Material bgMat = new Material(bgShader);
            SetupTransparentMaterial(bgMat);
            SetMaterialColor(bgMat, _factionColor);
            _boardBackgroundRenderer = _boardBackground.GetComponent<Renderer>();
            _boardBackgroundRenderer.material = bgMat;

            GameObject labelGo = new GameObject("BoardLabel");
            labelGo.transform.SetParent(_missileBoard.transform, false);
            labelGo.transform.localPosition = new Vector3(0f, 0f, -0.01f);
            labelGo.transform.localRotation = Quaternion.identity;
            labelGo.transform.localScale = new Vector3(-1f, 1f, 1f);

            _boardLabel = labelGo.AddComponent<TextMesh>();
            _boardLabel.text = _missileTypeIcon + " " + _missileDisplayName;
            _boardLabel.fontSize = 44;
            _boardLabel.characterSize = 0.1f;
            _boardLabel.anchor = TextAnchor.MiddleCenter;
            _boardLabel.alignment = TextAlignment.Center;
            _boardLabel.color = Color.white;

            Renderer labelRenderer = _boardLabel.GetComponent<Renderer>();
            labelRenderer.material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent + 1;

            float textWidth = _boardLabel.text.Length * _boardLabel.characterSize * 2.5f;
            float textHeight = _boardLabel.characterSize * 4f;
            float bgWidth = Mathf.Max(0.4f, textWidth + 0.06f);
            float bgHeight = Mathf.Max(0.12f, textHeight + 0.03f);
            _boardBackground.transform.localScale = new Vector3(bgWidth, bgHeight, 1f);

            Debug.Log("[MissileStrikeDemo] Missile board created: " + _boardLabel.text);
        }

        private void UpdateMissileBoard()
        {
            if (_missileBoard == null || _activeMissile == null)
            {
                return;
            }

            Camera cam = Camera.main;
            if (cam == null)
            {
                return;
            }

            Vector3 missilePos = _activeMissile.transform.position;
            float distance = Vector3.Distance(cam.transform.position, missilePos);
            float scale = Mathf.Clamp(distance * _boardScreenScale, 1f, 300f);

            // 在世界空间设置 Board 位置：导弹正上方固定距离（不受导弹旋转影响）
            _missileBoard.transform.position = missilePos + Vector3.up * _boardVerticalOffset;

            Vector3 toCamera = cam.transform.position - _missileBoard.transform.position;
            if (toCamera.sqrMagnitude < 0.0001f)
            {
                return;
            }

            _missileBoard.transform.rotation = Quaternion.LookRotation(toCamera, Vector3.up);
            _missileBoard.transform.localScale = new Vector3(scale, scale, scale);
        }

        private static void SetMaterialColor(Material mat, Color color)
        {
            if (mat == null)
            {
                return;
            }

            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", color);
            }

            if (mat.HasProperty("_Color"))
            {
                mat.SetColor("_Color", color);
            }

            mat.color = color;
        }

        private static void SetupTransparentMaterial(Material mat)
        {
            if (mat == null)
            {
                return;
            }

            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            if (mat.HasProperty("_Surface")) mat.SetFloat("_Surface", 1f);
            if (mat.HasProperty("_Blend")) mat.SetFloat("_Blend", 0f);
            if (mat.HasProperty("_SrcBlend")) mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (mat.HasProperty("_DstBlend")) mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            if (mat.HasProperty("_ZWrite")) mat.SetInt("_ZWrite", 0);

            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_SURFACE_TYPE_OPAQUE");
        }

        /// <summary>
        /// 创建打击效果
        /// </summary>
        private void CreateStrikeEffect()
        {
            if (_strikePointMarker == null)
            {
                return;
            }

            Vector3 strikePos = _strikePointMarker.position;

            if (_destroyEffectPrefab != null)
            {
                Instantiate(_destroyEffectPrefab, strikePos + Vector3.up * 2f, Quaternion.identity);
            }
            else
            {
                GameObject explosion = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                explosion.transform.position = strikePos + Vector3.up * 2f;
                explosion.transform.localScale = Vector3.one * _strikeEffectScale;

                Collider collider = explosion.GetComponent<Collider>();
                if (collider != null)
                {
                    Destroy(collider);
                }

                Material explosionMat = new Material(Shader.Find("Standard"));
                explosionMat.color = new Color(1f, 0.8f, 0.1f, 0.8f);
                explosion.GetComponent<Renderer>().material = explosionMat;

                Destroy(explosion, 2f);
            }

            GameObject shockwave = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            shockwave.transform.position = strikePos + Vector3.up * 0.1f;
            shockwave.transform.localScale = Vector3.one * 0.1f;

            Collider shockwaveCollider = shockwave.GetComponent<Collider>();
            if (shockwaveCollider != null)
            {
                Destroy(shockwaveCollider);
            }

            Material shockwaveMat = new Material(Shader.Find("Standard"));
            shockwaveMat.color = new Color(1f, 0.5f, 0f, 0.6f);
            shockwave.GetComponent<Renderer>().material = shockwaveMat;

            StartCoroutine(AnimateShockwave(shockwave));

            if (_burningFirePrefab != null)
            {
                _activeBurningFire = Instantiate(_burningFirePrefab, strikePos, Quaternion.identity);
                if (_burningDuration > 0f)
                {
                    Destroy(_activeBurningFire, _burningDuration);
                }
            }

            Debug.Log("[MissileStrikeDemo] Strike effect created at: " + strikePos);

            if (_smokeTrail != null)
            {
                ParticleSystem ps = _smokeTrail.GetComponent<ParticleSystem>();
                if (ps != null)
                {
                    ps.Stop();
                }
                Destroy(_smokeTrail, 3f);
                _smokeTrail = null;
            }

            if (_activeMissile != null)
            {
                Destroy(_activeMissile);
            }
            _activeMissile = null;
            _missileBoard = null;
            _boardLabel = null;
            _boardBackground = null;
            _boardBackgroundRenderer = null;
        }

        /// <summary>
        /// 冲击波扩散动画
        /// </summary>
        private IEnumerator AnimateShockwave(GameObject shockwave)
        {
            float duration = 1.5f;
            float elapsed = 0f;

            while (elapsed < duration && shockwave != null)
            {
                elapsed += Time.deltaTime;
                float progress = elapsed / duration;

                shockwave.transform.localScale = Vector3.one * 0.3f * (1f + progress * 3f);

                Material mat = shockwave.GetComponent<Renderer>().material;
                Color color = mat.color;
                color.a = 0.6f * (1f - progress);
                mat.color = color;

                yield return null;
            }

            Destroy(shockwave);
        }

        /// <summary>
        /// 创建标记点（可视化）
        /// </summary>
        private Transform CreateMarker(string name, Vector3 offset)
        {
            GameObject marker = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            marker.name = name;
            marker.transform.SetParent(transform);
            marker.transform.localPosition = offset;
            marker.transform.localScale = new Vector3(1f, 0.2f, 1f);

            Collider collider = marker.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            Material mat = new Material(Shader.Find("Standard"));
            if (name == "LaunchPoint")
            {
                mat.color = new Color(0.2f, 1f, 0.2f, 1f);
            }
            else
            {
                mat.color = new Color(1f, 0.2f, 0.2f, 1f);
            }

            marker.GetComponent<Renderer>().material = mat;
            return marker.transform;
        }

        /// <summary>
        /// 获取打击区域中心点，用于相机定位
        /// </summary>
        public Vector3 GetStrikeViewPoint()
        {
            if (_launchPointMarker == null || _strikePointMarker == null)
            {
                return transform.position;
            }

            Vector3 center = (_launchPointMarker.position + _strikePointMarker.position) * 0.5f;
            Vector3 delta = _strikePointMarker.position - _launchPointMarker.position;
            float distance = delta.magnitude * 1.2f;

            return center + Vector3.up * (distance * 0.6f) - delta.normalized * distance * 0.5f;
        }

        /// <summary>
        /// 获取推荐的相机观看方向
        /// </summary>
        public Vector3 GetStrikeViewDirection()
        {
            Vector3 viewPoint = GetStrikeViewPoint();
            if (_strikePointMarker != null)
            {
                return (_strikePointMarker.position - viewPoint).normalized;
            }

            return Vector3.forward;
        }

        public bool IsFlying => _isFlying;

        /// <summary>
        /// 暂停或恢复导弹飞行
        /// </summary>
        public void TogglePause()
        {
            if (_isFlying)
            {
                _isPaused = !_isPaused;
                Debug.Log("[MissileStrikeDemo] Flight " + (_isPaused ? "PAUSED" : "RESUMED"));
            }
        }

        public bool IsPaused => _isPaused;

        /// <summary>
        /// 设置导弹模型旋转偏移
        /// </summary>
        public void SetMissileRotationOffset(Vector3 eulerAngles)
        {
            _missileRotationOffsetEuler = eulerAngles;
            if (_activeMissile != null)
            {
                ApplyMissileModelRotationOffset(_activeMissile);
            }
        }

        public Vector3 GetMissileRotationOffset() => _missileRotationOffsetEuler;

        public Vector3 GetMissileRotationOffsetEuler()
        {
            return _missileRotationOffsetEuler;
        }

        public void SetMissileRotationOffsetEuler(Vector3 offsetEuler)
        {
            SetMissileRotationOffset(offsetEuler);
        }

        public void AdjustMissileRotationOffsetEuler(Vector3 deltaEuler)
        {
            SetMissileRotationOffset(_missileRotationOffsetEuler + deltaEuler);
        }

        public void ResetMissileRotationOffsetEuler()
        {
            SetMissileRotationOffset(Vector3.zero);
        }
    }
}
