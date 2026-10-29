using UnityEngine;

namespace OrbitalPanopticon.Physics
{
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public class GravityBody : MonoBehaviour, IGravityAffectable
    {
        [Header("Gravity Settings")]
        [Tooltip("Multiplier for gravitational forces applied by black holes/attractors.")]
        [SerializeField] private float gravityMultiplier = 1f;

        [SerializeField] private Rigidbody rb;

        public float GravityMultiplier
        {
            get => gravityMultiplier;
            set => gravityMultiplier = Mathf.Max(0f, value);
        }

        public Rigidbody Rigidbody => rb;

        private void Reset()
        {
            if (rb == null) rb = GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.useGravity = false;
                rb.linearDamping = 0.5f;
                rb.angularDamping = 0.5f;
            }
        }

        private void Awake()
        {
            if (rb == null)
            {
                rb = GetComponent<Rigidbody>();
            }

            if (rb != null && rb.useGravity)
            {
                rb.useGravity = false;
            }
        }

        private void OnValidate()
        {
            if (rb == null)
            {
                rb = GetComponent<Rigidbody>();
            }
        }

        /// <summary>
        /// Applies an external gravitational attraction force scaled by this body's gravityMultiplier.
        /// </summary>
        public void AddGravitationalForce(Vector3 force, ForceMode mode = ForceMode.Force)
        {
            if (rb != null)
            {
                rb.AddForce(force * gravityMultiplier, mode);
            }
        }
    }
}
