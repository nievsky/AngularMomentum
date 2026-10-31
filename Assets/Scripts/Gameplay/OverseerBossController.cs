using System;
using UnityEngine;

namespace OrbitalPanopticon.Gameplay
{
    /// <summary>
    /// Deprecated boss controller. Superseded by OverseerContainmentEye.
    /// Preserved as a stub to guarantee compile safety during refactoring.
    /// </summary>
    [Obsolete("Superseded by OverseerContainmentEye")]
    [DisallowMultipleComponent]
    public class OverseerBossController : MonoBehaviour
    {
        [SerializeField] private int bossIndex = 0;
        public bool isConsumedOrCollapsing = false;

        public int BossIndex => bossIndex;
        public string BossName => $"CONTAINMENT LOCK EYE-0{bossIndex + 1}";
        public float MaxHP => 1000f;
        public float CurrentHP => 1000f;
        public float VisualScale => 9f;
        public bool IsDestroyed => false;
        public Vector3 PupilPosition => transform.position + transform.forward * 4.2f;

        public void TakeDamage(float damage)
        {
        }
    }
}
