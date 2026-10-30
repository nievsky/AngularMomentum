using UnityEngine;

namespace OrbitalPanopticon.Core
{
    [DisallowMultipleComponent]
    public class ScorePopupManager : MonoBehaviour
    {
        public static ScorePopupManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        public void SpawnPopup(Vector3 worldPos, float score, bool isKamikaze, bool isObserved)
        {
            if (score <= 0f) return;

            // If consumed deep within the singularity center, lift position slightly so it is clearly visible
            if (worldPos.sqrMagnitude < 9.0f)
            {
                Camera cam = Camera.main;
                Vector3 toCam = cam != null ? (cam.transform.position - worldPos).normalized : Vector3.up;
                worldPos += toCam * 2.8f + Random.insideUnitSphere * 0.5f;
            }

            GameObject popupObj = new GameObject("ScorePopup");
            popupObj.transform.position = worldPos;

            WorldScorePopup popup = popupObj.AddComponent<WorldScorePopup>();
            popup.Setup(score, isKamikaze, isObserved);
        }
    }
}
