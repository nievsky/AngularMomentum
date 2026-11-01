using UnityEngine;
using OrbitalPanopticon.Physics;

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

            GameBalanceConfig config = GameManager.GameBalanceConfigInstance ?? GameBalanceConfig.Instance;
            float ehRadius = config != null ? config.eventHorizonRadius : 5.8f;
            Vector3 center = Vector3.zero;
            var bh = Object.FindAnyObjectByType<BlackHoleGravity>();
            if (bh != null) center = bh.blackHoleCenter;

            // Spawn the popup near the outer rim of the Black Hole where the item entered the horizon
            // (offset radially outward by eventHorizonRadius * 1.15f)
            Vector3 fromCenter = worldPos - center;
            if (fromCenter.sqrMagnitude < 0.01f) fromCenter = Vector3.up;
            Vector3 radialDir = fromCenter.normalized;

            Vector3 popupPos = center + radialDir * (ehRadius * 1.15f);

            // Also offset toward Camera.main along view vector so it sits clearly on top of the Black Hole
            Camera cam = Camera.main;
            if (cam != null)
            {
                Vector3 toCam = (cam.transform.position - center).normalized;
                popupPos += toCam * 1.5f;
            }

            GameObject popupObj = new GameObject("ScorePopup");
            popupObj.transform.position = popupPos;

            WorldScorePopup popup = popupObj.AddComponent<WorldScorePopup>();
            popup.Setup(score, isKamikaze, isObserved, radialDir);
        }
    }
}
