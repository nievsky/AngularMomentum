using System;
using UnityEngine;
using OrbitalPanopticon.Core;
using OrbitalPanopticon.Physics;

namespace OrbitalPanopticon.Gameplay
{
    /// <summary>
    /// Legacy compatibility bridge for BlackHoleStageManager.
    /// Redirects mass tracking and stages to BlackHoleStageManager with no energy battery.
    /// </summary>
    [DisallowMultipleComponent]
    public class BlackHoleMassAndEnergyManager : MonoBehaviour
    {
        public static BlackHoleMassAndEnergyManager Instance { get; private set; }

        public event Action<float, int> OnMassChanged;
        public event Action<float, float> OnEnergyChanged;
        public event Action OnCatastrophicCollapse;

        public float CurrentBlackHoleMass => BlackHoleStageManager.Instance != null ? BlackHoleStageManager.Instance.CurrentMass : 0f;
        public int CurrentStage => BlackHoleStageManager.Instance != null ? BlackHoleStageManager.Instance.CurrentStage : 1;
        public float CurrentPlayerEnergy => 0f;
        public bool IsCatastrophicCollapse => false;

        public float CurrentStageGravityMultiplier
        {
            get
            {
                if (BlackHoleStageManager.Instance != null)
                {
                    return BlackHoleStageManager.Instance.CurrentStageGravityMultiplier;
                }
                return 1.0f;
            }
        }

        private void Awake()
        {
            Instance = this;
        }

        private void Start()
        {
            if (BlackHoleStageManager.Instance != null)
            {
                BlackHoleStageManager.Instance.OnMassChanged += ForwardMassChanged;
            }
        }

        private void OnDestroy()
        {
            if (BlackHoleStageManager.Instance != null)
            {
                BlackHoleStageManager.Instance.OnMassChanged -= ForwardMassChanged;
            }
            if (Instance == this) Instance = null;
        }

        private void ForwardMassChanged(float mass, int stage)
        {
            OnMassChanged?.Invoke(mass, stage);
        }

        public void AddBlackHoleMass(float addedMass)
        {
            if (BlackHoleStageManager.Instance != null)
            {
                BlackHoleStageManager.Instance.AddMass(addedMass);
            }
        }

        public float DrainMass(float requestedMass)
        {
            return 0f;
        }

        public void AddPlayerEnergy(float amount)
        {
        }

        public bool SpendPlayerEnergy(float amount)
        {
            return false;
        }

        public float ConsumeAvailablePlayerEnergy(float requestedAmount)
        {
            return 0f;
        }

        public void TriggerCatastrophicCollapse()
        {
        }
    }
}
