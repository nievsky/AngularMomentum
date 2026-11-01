using System;
using UnityEngine;
using FMODUnity;
using FMOD.Studio;
using OrbitalPanopticon.Player;
using STOP_MODE = FMOD.Studio.STOP_MODE;

namespace OrbitalPanopticon.Gameplay
{
    /// <summary>
    /// Projects a 3D FMOD audio event instance along a beam line segment, polyline, or LineRenderer spline.
    /// Tracks the closest point on the beam "rails" to the player / audio listener, ensuring the 3D sound
    /// is always heard with maximum spatial clarity and proximity along the beam's full length.
    /// </summary>
    [DisallowMultipleComponent]
    public class BeamSplineAudioEmitter : MonoBehaviour
    {
        [Header("Spline / Rail Audio Tracking")]
        [Tooltip("Optional override transform for the listener (defaults to Player Drone or Main Camera).")]
        [SerializeField] private Transform listenerOverride;

        [Tooltip("Smooth position follow speed (0 = instantaneous tracking).")]
        [SerializeField] private float positionSmoothing = 0f;

        private EventInstance _beamLoopInstance;
        private bool _isPlaying = false;
        private Vector3 _currentEmitterPosition;
        private bool _hasPosition = false;

        public bool IsPlaying => _isPlaying;
        public Vector3 CurrentEmitterPosition => _currentEmitterPosition;

        /// <summary>
        /// Starts playing the looping 3D beam sound along the beam segment or LineRenderer.
        /// </summary>
        public void StartLoop(EventReference eventRef, Vector3 startPos, Vector3 endPos)
        {
            StopLoop(FMOD.Studio.STOP_MODE.IMMEDIATE);

            if (!RuntimeManager.HaveAllBanksLoaded)
            {
                Debug.LogWarning("[BeamSplineAudioEmitter] Banks not yet loaded, cannot start beam loop.");
                return;
            }

            try
            {
                if (!eventRef.IsNull)
                {
                    _beamLoopInstance = RuntimeManager.CreateInstance(eventRef);
                }

                if (!_beamLoopInstance.isValid())
                {
                    _beamLoopInstance = RuntimeManager.CreateInstance("event:/Boss/Overseer_BeamLoop");
                }

                if (_beamLoopInstance.isValid())
                {
                    Vector3 closest = GetClosestPoint(startPos, endPos, GetListenerPosition());
                    _currentEmitterPosition = closest;
                    _hasPosition = true;

                    _beamLoopInstance.set3DAttributes(RuntimeUtils.To3DAttributes(closest));
                    _beamLoopInstance.start();
                    _isPlaying = true;
                }
                else
                {
                    Debug.LogWarning("[BeamSplineAudioEmitter] Failed to create event instance for Overseer_BeamLoop (neither Guid nor Path resolved).");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[BeamSplineAudioEmitter] Failed to start beam loop: {ex.Message}");
            }
        }

        /// <summary>
        /// Updates the 3D audio emitter position along the single line segment [startPos, endPos]
        /// to the point closest to the listener.
        /// </summary>
        public void UpdatePosition(Vector3 startPos, Vector3 endPos)
        {
            if (!_isPlaying || !_beamLoopInstance.isValid()) return;

            Vector3 listenerPos = GetListenerPosition();
            Vector3 targetPoint = GetClosestPoint(startPos, endPos, listenerPos);

            ApplyEmitterPosition(targetPoint);
        }

        /// <summary>
        /// Updates the 3D audio emitter position along all segments of a multi-vertex LineRenderer
        /// to the point closest to the listener.
        /// </summary>
        public void UpdatePosition(LineRenderer lineRenderer)
        {
            if (!_isPlaying || !_beamLoopInstance.isValid()) return;
            if (lineRenderer == null || lineRenderer.positionCount == 0) return;

            Vector3 listenerPos = GetListenerPosition();
            Vector3 targetPoint = GetClosestPoint(lineRenderer, listenerPos);

            ApplyEmitterPosition(targetPoint);
        }

        /// <summary>
        /// Updates the 3D audio emitter position along an array of polyline / spline points
        /// to the point closest to the listener.
        /// </summary>
        public void UpdatePosition(Vector3[] points)
        {
            if (!_isPlaying || !_beamLoopInstance.isValid()) return;
            if (points == null || points.Length == 0) return;

            Vector3 listenerPos = GetListenerPosition();
            Vector3 targetPoint = GetClosestPoint(points, listenerPos);

            ApplyEmitterPosition(targetPoint);
        }

        private void ApplyEmitterPosition(Vector3 targetPoint)
        {
            if (!_hasPosition || positionSmoothing <= 0.001f)
            {
                _currentEmitterPosition = targetPoint;
                _hasPosition = true;
            }
            else
            {
                _currentEmitterPosition = Vector3.Lerp(_currentEmitterPosition, targetPoint, Time.deltaTime * positionSmoothing);
            }

            _beamLoopInstance.set3DAttributes(RuntimeUtils.To3DAttributes(_currentEmitterPosition));
        }

        /// <summary>
        /// Stops the looping beam audio with the requested stop mode (default: ALLOWFADEOUT) and releases the handle.
        /// </summary>
        public void StopLoop(FMOD.Studio.STOP_MODE stopMode = FMOD.Studio.STOP_MODE.ALLOWFADEOUT)
        {
            if (_beamLoopInstance.isValid())
            {
                _beamLoopInstance.stop(stopMode);
                _beamLoopInstance.release();
                _beamLoopInstance.clearHandle();
            }
            _isPlaying = false;
            _hasPosition = false;
        }

        private Vector3 GetListenerPosition()
        {
            if (listenerOverride != null) return listenerOverride.position;

            DroneController drone = DroneController.Instance;
            if (drone != null) return drone.transform.position;

            Camera cam = Camera.main;
            if (cam != null) return cam.transform.position;

            return transform.position;
        }

        public static Vector3 GetClosestPoint(Vector3 a, Vector3 b, Vector3 p)
        {
            Vector3 ab = b - a;
            float abLenSq = ab.sqrMagnitude;
            if (abLenSq < 0.0001f) return a;
            float t = Mathf.Clamp01(Vector3.Dot(p - a, ab) / abLenSq);
            return a + ab * t;
        }

        public static Vector3 GetClosestPoint(Vector3[] points, Vector3 p)
        {
            if (points == null || points.Length == 0) return p;
            if (points.Length == 1) return points[0];

            float minDistanceSq = float.MaxValue;
            Vector3 closest = points[0];

            for (int i = 0; i < points.Length - 1; i++)
            {
                Vector3 a = points[i];
                Vector3 b = points[i + 1];
                Vector3 candidate = GetClosestPoint(a, b, p);
                float distSq = (p - candidate).sqrMagnitude;
                if (distSq < minDistanceSq)
                {
                    minDistanceSq = distSq;
                    closest = candidate;
                }
            }

            return closest;
        }

        public static Vector3 GetClosestPoint(LineRenderer lr, Vector3 p)
        {
            if (lr == null || lr.positionCount == 0) return p;
            int count = lr.positionCount;
            if (count == 1) return lr.GetPosition(0);

            float minDistanceSq = float.MaxValue;
            Vector3 closest = lr.GetPosition(0);

            for (int i = 0; i < count - 1; i++)
            {
                Vector3 a = lr.GetPosition(i);
                Vector3 b = lr.GetPosition(i + 1);
                Vector3 candidate = GetClosestPoint(a, b, p);
                float distSq = (p - candidate).sqrMagnitude;
                if (distSq < minDistanceSq)
                {
                    minDistanceSq = distSq;
                    closest = candidate;
                }
            }

            return closest;
        }

        private void OnDisable()
        {
            StopLoop(FMOD.Studio.STOP_MODE.IMMEDIATE);
        }

        private void OnDestroy()
        {
            StopLoop(FMOD.Studio.STOP_MODE.IMMEDIATE);
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            if (_isPlaying && _hasPosition)
            {
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(_currentEmitterPosition, 1.5f);
                Vector3 listener = GetListenerPosition();
                Gizmos.DrawLine(_currentEmitterPosition, listener);
            }
        }
#endif
    }
}
