using UnityEngine;

namespace OrbitalPanopticon.Physics
{
    public interface IGravityAffectable
    {
        Rigidbody Rigidbody { get; }
        float GravityMultiplier { get; set; }
        Transform transform { get; }
        GameObject gameObject { get; }
        void AddGravitationalForce(Vector3 force, ForceMode mode = ForceMode.Force);
    }
}
