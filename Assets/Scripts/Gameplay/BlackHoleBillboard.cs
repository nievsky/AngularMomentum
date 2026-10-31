using UnityEngine;

namespace OrbitalPanopticon.Gameplay
{
    /// <summary>
    /// Keeps the BlackHoleLensQuad billboard oriented directly toward the active rendering camera.
    /// Operates in LateUpdate to guarantee alignment with camera position regardless of update order.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class BlackHoleBillboard : MonoBehaviour
    {
        private Camera _cam;

        private void LateUpdate()
        {
            if (_cam == null || !_cam.isActiveAndEnabled)
            {
                _cam = Camera.main;
            }

            if (_cam != null)
            {
                transform.rotation = _cam.transform.rotation;
            }
        }
    }
}
