using UnityEngine;

namespace CommandP.GlobalEntity.Rendering
{
    /// <summary>
    /// World-Space Billboard: 每帧将 MarkerRoot 旋转对齐到主相机。
    /// 使用 markerRoot.rotation = mainCamera.rotation 实现完美公告板。
    /// 不产生自转、镜像、抖动，无需 LookAt / yaw-only 修正。
    /// </summary>
    public class BillboardMarker : MonoBehaviour
    {
        private Camera _targetCamera;

        public void AssignCamera(Camera cam) => _targetCamera = cam;
        public bool HasCamera => _targetCamera != null;

        private void LateUpdate()
        {
            if (_targetCamera != null)
                transform.rotation = _targetCamera.transform.rotation;
        }
    }
}
