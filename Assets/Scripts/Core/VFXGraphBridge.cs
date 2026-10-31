using System.Collections.Generic;
using UnityEngine;
#if UNITY_VFX_GRAPH || true
using UnityEngine.VFX;
#endif

namespace OrbitalPanopticon.Core
{
    /// <summary>
    /// Universal bridge connecting gameplay systems with Unity Visual Effect Graph (UnityEngine.VFX)
    /// alongside fallback particle system and trail layers.
    /// Safely feeds dynamic parameters (TargetPosition, Intensity, Color, SpawnRate) every frame.
    /// </summary>
    [DisallowMultipleComponent]
    public class VFXGraphBridge : MonoBehaviour
    {
        [Header("VFX Graph Asset & Component")]
        [SerializeField] private VisualEffect visualEffect;
        [SerializeField] private VisualEffectAsset defaultVFXAsset;

        [Header("Fallback Particle System Layers")]
        [SerializeField] private ParticleSystem[] particleLayers;
        [SerializeField] private TrailRenderer[] trailLayers;

        [Header("Live Parameters")]
        [SerializeField] private bool autoUpdateTarget = true;
        [SerializeField] private Transform targetTransform;
        [SerializeField] private Vector3 targetPosition;
        [SerializeField] private float intensity = 1.0f;
        [SerializeField] private Color vfxColor = Color.cyan;
        [SerializeField] private float spawnRate = 1.0f;

        // Cached Property IDs for zero-garbage parameter binding
        private static readonly int TargetPositionId = Shader.PropertyToID("TargetPosition");
        private static readonly int TargetPosId = Shader.PropertyToID("TargetPos");
        private static readonly int IntensityId = Shader.PropertyToID("Intensity");
        private static readonly int ColorId = Shader.PropertyToID("Color");
        private static readonly int BaseColorId = Shader.PropertyToID("BaseColor");
        private static readonly int SpawnRateId = Shader.PropertyToID("SpawnRate");

        public VisualEffect VisualEffectComponent => visualEffect;
        public VisualEffectAsset DefaultVFXAsset
        {
            get => defaultVFXAsset;
            set
            {
                defaultVFXAsset = value;
                EnsureVisualEffect();
            }
        }

        private void Awake()
        {
            EnsureVisualEffect();
        }

        private void OnEnable()
        {
            UpdateAllParameters();
        }

        private void Update()
        {
            if (autoUpdateTarget && targetTransform != null)
            {
                targetPosition = targetTransform.position;
            }

            if (visualEffect != null && visualEffect.isActiveAndEnabled)
            {
                UpdateAllParameters();
            }
        }

        public void EnsureVisualEffect()
        {
            if (visualEffect == null)
            {
                visualEffect = GetComponent<VisualEffect>();
                if (visualEffect == null && defaultVFXAsset != null)
                {
                    visualEffect = gameObject.AddComponent<VisualEffect>();
                    visualEffect.visualEffectAsset = defaultVFXAsset;
                }
            }
            else if (visualEffect.visualEffectAsset == null && defaultVFXAsset != null)
            {
                visualEffect.visualEffectAsset = defaultVFXAsset;
            }
        }

        public void SetTarget(Transform target)
        {
            targetTransform = target;
            if (targetTransform != null)
            {
                targetPosition = targetTransform.position;
            }
            UpdateTargetPosition();
        }

        public void SetTargetPosition(Vector3 pos)
        {
            targetPosition = pos;
            targetTransform = null;
            UpdateTargetPosition();
        }

        public void SetIntensity(float val)
        {
            intensity = val;
            if (visualEffect != null && visualEffect.HasFloat(IntensityId))
            {
                visualEffect.SetFloat(IntensityId, intensity);
            }
        }

        public void SetColor(Color col)
        {
            vfxColor = col;
            if (visualEffect != null)
            {
                if (visualEffect.HasVector4(ColorId)) visualEffect.SetVector4(ColorId, col);
                else if (visualEffect.HasVector4(BaseColorId)) visualEffect.SetVector4(BaseColorId, col);
            }

            if (particleLayers != null)
            {
                for (int i = 0; i < particleLayers.Length; i++)
                {
                    if (particleLayers[i] != null)
                    {
                        var main = particleLayers[i].main;
                        main.startColor = col;
                    }
                }
            }

            if (trailLayers != null)
            {
                for (int i = 0; i < trailLayers.Length; i++)
                {
                    if (trailLayers[i] != null)
                    {
                        trailLayers[i].startColor = col;
                    }
                }
            }
        }

        public void SetSpawnRate(float rate)
        {
            spawnRate = rate;
            if (visualEffect != null && visualEffect.HasFloat(SpawnRateId))
            {
                visualEffect.SetFloat(SpawnRateId, spawnRate);
            }
        }

        public void Play()
        {
            EnsureVisualEffect();
            if (visualEffect != null)
            {
                visualEffect.Play();
            }

            if (particleLayers != null)
            {
                for (int i = 0; i < particleLayers.Length; i++)
                {
                    if (particleLayers[i] != null && !particleLayers[i].isPlaying)
                    {
                        particleLayers[i].Play();
                    }
                }
            }
        }

        public void Stop()
        {
            if (visualEffect != null)
            {
                visualEffect.Stop();
            }

            if (particleLayers != null)
            {
                for (int i = 0; i < particleLayers.Length; i++)
                {
                    if (particleLayers[i] != null && particleLayers[i].isPlaying)
                    {
                        particleLayers[i].Stop();
                    }
                }
            }
        }

        public void SendVFXEvent(string eventName)
        {
            if (visualEffect != null && !string.IsNullOrEmpty(eventName))
            {
                visualEffect.SendEvent(eventName);
            }
        }

        private void UpdateTargetPosition()
        {
            if (visualEffect == null) return;

            if (visualEffect.HasVector3(TargetPositionId))
            {
                visualEffect.SetVector3(TargetPositionId, targetPosition);
            }
            else if (visualEffect.HasVector3(TargetPosId))
            {
                visualEffect.SetVector3(TargetPosId, targetPosition);
            }
        }

        private void UpdateAllParameters()
        {
            UpdateTargetPosition();
            SetIntensity(intensity);
            SetColor(vfxColor);
            SetSpawnRate(spawnRate);
        }
    }
}
