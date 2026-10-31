using UnityEngine;

namespace OrbitalPanopticon.Player
{
    /// <summary>
    /// Bridges input from GravityManipulatorController to the Robo-Arm's AnimatorController.
    /// Drives 'IsPullingRMB' (bool) for tractor-beam hold and 'FirePuntLMB' (trigger) for kinetic blast.
    /// </summary>
    [DisallowMultipleComponent]
    public class RoboArmAnimatorHook : MonoBehaviour
    {
        [Header("Animator Reference")]
        [SerializeField] private Animator animator;

        private static readonly int ParamIsPullingRMB = Animator.StringToHash("IsPullingRMB");
        private static readonly int ParamFirePuntLMB = Animator.StringToHash("FirePuntLMB");

        public Animator TargetAnimator
        {
            get => animator;
            set => animator = value;
        }

        private void Awake()
        {
            FindAnimatorIfNeeded();
        }

        public void FindAnimatorIfNeeded()
        {
            if (animator == null)
            {
                animator = GetComponent<Animator>();
                if (animator == null) animator = GetComponentInChildren<Animator>(true);
            }
        }

        public void SetPulling(bool isPulling)
        {
            FindAnimatorIfNeeded();
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.SetBool(ParamIsPullingRMB, isPulling);
            }
        }

        public void TriggerPunt()
        {
            FindAnimatorIfNeeded();
            if (animator != null && animator.runtimeAnimatorController != null)
            {
                animator.SetTrigger(ParamFirePuntLMB);
            }
        }
    }
}
