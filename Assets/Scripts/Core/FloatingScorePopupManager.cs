using UnityEngine;

namespace OrbitalPanopticon.Core
{
    /// <summary>
    /// Explicit alias and wrapper for ScorePopupManager to guarantee all naming conventions resolve cleanly.
    /// </summary>
    public class FloatingScorePopupManager : MonoBehaviour
    {
        public static FloatingScorePopupManager Instance { get; private set; }

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
            if (ScorePopupManager.Instance != null)
            {
                ScorePopupManager.Instance.SpawnPopup(worldPos, score, isKamikaze, isObserved);
            }
        }
    }
}
