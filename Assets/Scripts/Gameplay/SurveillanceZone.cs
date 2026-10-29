using UnityEngine;

namespace OrbitalPanopticon.Gameplay
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Collider))]
    public class SurveillanceZone : MonoBehaviour
    {
        [Header("Observation Settings")]
        [Tooltip("Value multiplier awarded to launched scrap passing through this surveillance beam.")]
        [SerializeField] private float cameraMultiplier = 3.0f;

        [Header("Feedback")]
        [SerializeField] private Light spotFeedbackLight;

        public float CameraMultiplier => cameraMultiplier;

        private void OnTriggerEnter(Collider other)
        {
            ScrapItem scrap = other.GetComponentInParent<ScrapItem>();
            if (scrap != null && scrap.CurrentState == ScrapState.Launched)
            {
                if (!scrap.IsObserved)
                {
                    scrap.ApplyObservationBonus(cameraMultiplier);
                    FlashFeedback();
                }
            }
        }

        private void FlashFeedback()
        {
            if (spotFeedbackLight != null)
            {
                spotFeedbackLight.intensity = 5f;
            }
        }

        private void Update()
        {
            // Dim feedback light back over time if present
            if (spotFeedbackLight != null && spotFeedbackLight.intensity > 1f)
            {
                spotFeedbackLight.intensity = Mathf.MoveTowards(spotFeedbackLight.intensity, 1f, Time.deltaTime * 8f);
            }
        }
    }
}
