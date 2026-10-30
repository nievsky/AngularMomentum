using System.Collections.Generic;
using UnityEngine;
using OrbitalPanopticon.Core;

namespace OrbitalPanopticon.Gameplay
{
    [DisallowMultipleComponent]
    public class DynamicSurveillanceCamera : MonoBehaviour
    {
        [Header("Scene References")]
        [SerializeField] private Light spotLight;
        [SerializeField] private Transform beamTransform;
        [SerializeField] private Transform centerReference;

        [Header("Beam Tuning")]
        [SerializeField] private float beamBaseRadius = 1.2f;
        [SerializeField] private float idleBeamDistance = 25f;

        [Header("Runtime Status")]
        [SerializeField] private ScrapItem currentTarget;

        public ScrapItem CurrentTarget => currentTarget;

        private void Awake()
        {
            if (centerReference == null)
            {
                GameObject bh = GameObject.Find("BlackHoleSystem");
                if (bh != null) centerReference = bh.transform;
            }

            if (spotLight == null)
            {
                spotLight = GetComponentInChildren<Light>();
            }
        }

        private void Update()
        {
            GameBalanceConfig config = GameManager.GameBalanceConfigInstance;
            float rotSpeed = config != null ? config.cameraRotationSpeed : 4.0f;

            if (currentTarget == null || !IsTargetValid(currentTarget))
            {
                ReleaseTarget();
                FindNewTarget();
            }

            if (currentTarget != null)
            {
                TrackCurrentTarget(rotSpeed);
            }
            else
            {
                PerformIdleSweep();
            }
        }

        private void TrackCurrentTarget(float rotSpeed)
        {
            Vector3 toTarget = currentTarget.transform.position - transform.position;
            float dist = toTarget.magnitude;

            if (dist > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotSpeed * Time.deltaTime);
            }

            // Adjust beam geometry
            if (beamTransform != null)
            {
                beamTransform.localPosition = new Vector3(0f, 0f, dist * 0.5f);
                beamTransform.localScale = new Vector3(beamBaseRadius, dist * 0.5f, beamBaseRadius);
            }

            if (spotLight != null)
            {
                spotLight.range = dist + 8f;
            }

            // Ensure observed status is maintained on the scrap item
            currentTarget.IsTargetedByCamera = true;
            if (!currentTarget.IsObserved)
            {
                currentTarget.SetObserved(true);
            }
        }

        private void PerformIdleSweep()
        {
            Vector3 center = centerReference != null ? centerReference.position : Vector3.zero;
            Vector3 toCenter = center - transform.position;

            if (toCenter.sqrMagnitude > 0.001f)
            {
                Quaternion baseRot = Quaternion.LookRotation(toCenter.normalized, Vector3.up);
                float sweepOffset = Mathf.Sin(Time.time * 0.8f) * 12f;
                Quaternion sweepRot = baseRot * Quaternion.Euler(0f, sweepOffset, 0f);
                transform.rotation = Quaternion.Slerp(transform.rotation, sweepRot, Time.deltaTime * 2f);
            }

            if (beamTransform != null)
            {
                beamTransform.localPosition = new Vector3(0f, 0f, idleBeamDistance * 0.5f);
                beamTransform.localScale = new Vector3(beamBaseRadius, idleBeamDistance * 0.5f, beamBaseRadius);
            }

            if (spotLight != null)
            {
                spotLight.range = idleBeamDistance + 5f;
            }
        }

        private bool IsTargetValid(ScrapItem scrap)
        {
            if (scrap == null || scrap.gameObject == null) return false;
            if (scrap.CurrentState == ScrapState.Consumed || scrap.CurrentState == ScrapState.FadingOut) return false;

            GameBalanceConfig config = GameManager.GameBalanceConfigInstance;
            float maxRadius = config != null ? config.arenaMaxRadius : 48f;
            Vector3 center = centerReference != null ? centerReference.position : Vector3.zero;

            if (Vector3.Distance(scrap.transform.position, center) > maxRadius)
            {
                return false;
            }

            return true;
        }

        private void FindNewTarget()
        {
            GameBalanceConfig config = GameManager.GameBalanceConfigInstance;
            float maxRadius = config != null ? config.arenaMaxRadius : 48f;
            Vector3 center = centerReference != null ? centerReference.position : Vector3.zero;

            List<ScrapItem> candidates = new List<ScrapItem>();

            if (ScrapSimulationManager.Instance != null)
            {
                var activeList = ScrapSimulationManager.Instance.ActiveScrap;
                for (int i = 0; i < activeList.Count; i++)
                {
                    ScrapItem s = activeList[i];
                    if (s == null) continue;

                    if (s.CurrentState == ScrapState.Floating && !s.IsTargetedByCamera && s.CurrentState != ScrapState.Consumed)
                    {
                        if (Vector3.Distance(s.transform.position, center) <= maxRadius)
                        {
                            candidates.Add(s);
                        }
                    }
                }
            }
            else
            {
                ScrapItem[] allScrap = Object.FindObjectsByType<ScrapItem>(FindObjectsInactive.Exclude);
                for (int i = 0; i < allScrap.Length; i++)
                {
                    ScrapItem s = allScrap[i];
                    if (s == null) continue;

                    // Pick random active floating scrap not already targeted
                    if (s.CurrentState == ScrapState.Floating && !s.IsTargetedByCamera && s.CurrentState != ScrapState.Consumed)
                    {
                        if (Vector3.Distance(s.transform.position, center) <= maxRadius)
                        {
                            candidates.Add(s);
                        }
                    }
                }
            }

            if (candidates.Count > 0)
            {
                int index = Random.Range(0, candidates.Count);
                currentTarget = candidates[index];
                currentTarget.IsTargetedByCamera = true;
                currentTarget.SetObserved(true);
            }
        }

        public void ReleaseTarget()
        {
            if (currentTarget != null)
            {
                currentTarget.IsTargetedByCamera = false;
                if (currentTarget.CurrentState != ScrapState.Consumed)
                {
                    currentTarget.SetObserved(false);
                }
                currentTarget = null;
            }
        }

        private void OnDisable()
        {
            ReleaseTarget();
        }
    }
}
