using UnityEngine;

namespace OrbitalPanopticon.UI
{
    [DisallowMultipleComponent]
    public class MenuCameraOrbit : MonoBehaviour
    {
        [Header("Orbit Settings")]
        [SerializeField] private Vector3 orbitCenter = Vector3.zero;
        [SerializeField] private float orbitRadius = 32f;
        [SerializeField] private float orbitHeight = 6.5f;
        [SerializeField] private float orbitSpeed = 0.08f;
        [SerializeField] private float verticalBobSpeed = 0.25f;
        [SerializeField] private float verticalBobAmplitude = 2.0f;

        private float _currentAngle = 0f;

        private void Start()
        {
            GameObject bh = GameObject.Find("BlackHoleSystem");
            if (bh != null)
            {
                orbitCenter = bh.transform.position;
            }
        }

        private void LateUpdate()
        {
            _currentAngle += orbitSpeed * Time.deltaTime;

            float x = Mathf.Cos(_currentAngle) * orbitRadius;
            float z = Mathf.Sin(_currentAngle) * orbitRadius;
            float y = orbitHeight + Mathf.Sin(_currentAngle * verticalBobSpeed) * verticalBobAmplitude;

            transform.position = orbitCenter + new Vector3(x, y, z);
            transform.LookAt(orbitCenter + Vector3.up * 1.2f);
        }
    }
}