using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using TMPro;
using OrbitalPanopticon.Core;
using OrbitalPanopticon.Physics;
using OrbitalPanopticon.Player;

namespace OrbitalPanopticon.Gameplay
{
    [DisallowMultipleComponent]
    public class EndgameFinaleController : MonoBehaviour
    {
        public static EndgameFinaleController Instance { get; private set; }

        [Header("Runtime Status")]
        [SerializeField] private bool isFinaleTriggered = false;

        private BlackHoleGravity _blackHole;
        private Transform _bhVisualRoot;
        private GameObject _finaleAlertPanel;
        private TextMeshProUGUI _finaleAlertText;

        public bool IsFinaleTriggered => isFinaleTriggered;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void Start()
        {
            _blackHole = FindAnyObjectByType<BlackHoleGravity>();
            if (_blackHole != null)
            {
                Transform vr = _blackHole.transform.Find("VisualRoot");
                _bhVisualRoot = vr != null ? vr : _blackHole.transform;
            }

            BuildFinaleUI();
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Instance = null;
            }
        }

        public void TriggerEndgameFinale()
        {
            if (isFinaleTriggered) return;
            isFinaleTriggered = true;
            StopAllCoroutines();
            StartCoroutine(EndgameCollapseSequenceRoutine());
        }

        private IEnumerator EndgameCollapseSequenceRoutine()
        {
            GameBalanceConfig config = GameManager.GameBalanceConfigInstance;
            Vector3 center = _blackHole != null ? _blackHole.transform.position : Vector3.zero;

            Debug.Log("<color=#FF0055>[FINALE] Initiating Sector Collapse & Suspense Sequence...</color>");

            // 1. Shut off the 4th and final OverseerContainmentEye lock and trigger Finale comms transmission
            if (BlackHoleStageManager.Instance != null)
            {
                BlackHoleStageManager.Instance.BreakLockForStage(3);
            }

            if (NarrativeCommsUI.Instance != null)
            {
                NarrativeCommsUI.Instance.PlayFinaleTransmission();
            }

            // 2. Disable normal out-of-bounds/respawn logic on ArenaBoundaryManager and stop spawning regular scrap
            if (ArenaBoundaryManager.Instance != null)
            {
                ArenaBoundaryManager.Instance.SetBoundaryActive(false);
            }

            if (ScrapSimulationManager.Instance != null)
            {
                ScrapSimulationManager.Instance.SetSpawningEnabled(false);
            }

            // Display Flashing Alert Banner
            if (_finaleAlertPanel != null)
            {
                _finaleAlertPanel.SetActive(true);
                if (_finaleAlertText != null)
                {
                    _finaleAlertText.text = "CRITICAL MASS REACHED! ALL 4 CONTAINMENT LOCKS SHATTERED — SINGULARITY UNCONSTRAINED!";
                }
                StartCoroutine(FlashAlertRoutine());
            }

            // 3. Suspense Phase (0.0s – 4.5s):
            // All 4 deactivated OverseerContainmentEye bosses detach from the outer sphere boundary!
            // Enable GravityBody on all 4 Eyes and pull them spiraling rapidly into the Black Hole center (0,0,0).
            // As each massive Eye crosses Event Horizon, spaghettify it (ConsumeObject) with camera shake and huge score popups (+999,999!).
            Camera mainCam = Camera.main;
            Vector3 camInitialLocalPos = mainCam != null ? mainCam.transform.localPosition : Vector3.zero;

            var foundEyes = FindObjectsByType<OverseerContainmentEye>(FindObjectsInactive.Exclude);
            List<OverseerContainmentEye> activeEyes = new List<OverseerContainmentEye>();

            foreach (var eye in foundEyes)
            {
                if (eye == null) continue;
                eye.isConsumedOrCollapsing = true;
                activeEyes.Add(eye);

                var rb = eye.GetComponent<Rigidbody>();
                if (rb == null) rb = eye.gameObject.AddComponent<Rigidbody>();
                rb.isKinematic = true;

                var gb = eye.GetComponent<GravityBody>();
                if (gb == null) gb = eye.gameObject.AddComponent<GravityBody>();
            }

            float suspenseTimer = 0f;
            const float suspenseDuration = 4.5f;
            float horizonRadius = config != null ? config.eventHorizonRadius : 4.5f;

            if (FMODAudioManager.Instance != null)
            {
                FMODAudioManager.Instance.PlayFinaleEyesConsumedRumble(center);
            }

            while (suspenseTimer < suspenseDuration && activeEyes.Count > 0)
            {
                suspenseTimer += Time.deltaTime;
                float progress = suspenseTimer / suspenseDuration;

                // Dynamic Camera Shake
                if (mainCam != null)
                {
                    float shake = Mathf.Lerp(0.08f, 0.45f, progress);
                    mainCam.transform.localPosition = camInitialLocalPos + (Random.insideUnitSphere * shake);
                }

                for (int i = activeEyes.Count - 1; i >= 0; i--)
                {
                    var eye = activeEyes[i];
                    if (eye == null)
                    {
                        activeEyes.RemoveAt(i);
                        continue;
                    }

                    Vector3 toCenter = (center - eye.transform.position);
                    float dist = toCenter.magnitude;
                    Vector3 inwardDir = dist > 0.001f ? toCenter / dist : Vector3.forward;

                    // Tangential spiral motion
                    Vector3 tangent = Vector3.Cross(Vector3.up, inwardDir).normalized;
                    Vector3 moveDir = (inwardDir * 1.35f + tangent * 0.65f).normalized;

                    float speed = Mathf.Lerp(24f, 60f, progress);
                    eye.transform.position += moveDir * (speed * Time.deltaTime);

                    // Rapid spin / tumble
                    eye.transform.Rotate(Vector3.up, 360f * Time.deltaTime, Space.World);
                    eye.transform.Rotate(Vector3.right, 180f * Time.deltaTime, Space.Self);

                    // Horizon crossing check
                    if (dist <= horizonRadius + 2.0f)
                    {
                        Vector3 eyePos = eye.transform.position;

                        // Spaghettify & consume
                        var gb = eye.GetComponent<GravityBody>();
                        if (_blackHole != null && gb != null)
                        {
                            _blackHole.ConsumeObject(gb);
                        }

                        // Huge floating score popup: +999,999!
                        if (ScorePopupManager.Instance != null)
                        {
                            ScorePopupManager.Instance.SpawnPopup(eyePos, 999999f, false, false);
                        }

                        // FMOD Destruction / Suction Sound
                        if (FMODAudioManager.Instance != null)
                        {
                            FMODAudioManager.Instance.PlayFinaleEyesConsumedRumble(eyePos);
                        }

                        activeEyes.RemoveAt(i);
                    }
                }

                yield return null;
            }

            // Ensure any lingering eyes are fully consumed
            for (int i = 0; i < activeEyes.Count; i++)
            {
                if (activeEyes[i] != null)
                {
                    var gb = activeEyes[i].GetComponent<GravityBody>();
                    if (_blackHole != null && gb != null)
                    {
                        _blackHole.ConsumeObject(gb);
                    }
                    if (ScorePopupManager.Instance != null)
                    {
                        ScorePopupManager.Instance.SpawnPopup(center, 999999f, false, false);
                    }
                }
            }
            activeEyes.Clear();

            if (mainCam != null)
            {
                mainCam.transform.localPosition = camInitialLocalPos;
            }

            // 4. Player Horizon Pull (After all 4 Eyes are consumed, wait 2.0f seconds)
            yield return new WaitForSeconds(2.0f);

            DroneController playerDrone = DroneController.Instance != null ? DroneController.Instance : FindAnyObjectByType<DroneController>();
            if (playerDrone != null)
            {
                // Lock player WASD controls (keep mouse look active so the player can watch!)
                playerDrone.SetMovementLocked(true);

                // Switch Rigidbody to kinematic so physics forces don't interfere
                if (playerDrone.Rigidbody != null)
                {
                    playerDrone.Rigidbody.linearVelocity = Vector3.zero;
                    playerDrone.Rigidbody.angularVelocity = Vector3.zero;
                    playerDrone.Rigidbody.isKinematic = true;
                }

                Camera droneCam = playerDrone.MainCamera != null ? playerDrone.MainCamera : Camera.main;
                float startFov = droneCam != null ? droneCam.fieldOfView : 60f;
                const float targetFov = 115f;

                Vector3 playerStartPos = playerDrone.transform.position;
                Vector3 camLocalPos = droneCam != null ? droneCam.transform.localPosition : Vector3.zero;

                float pullElapsed = 0f;
                const float pullDuration = 3.6f;

                if (FMODAudioManager.Instance != null)
                {
                    FMODAudioManager.Instance.PlayPlayerHorizonWarp(playerStartPos);
                }

                while (pullElapsed < pullDuration && playerDrone != null)
                {
                    pullElapsed += Time.deltaTime;
                    float t = Mathf.Clamp01(pullElapsed / pullDuration);
                    float smoothT = Mathf.SmoothStep(0f, 1f, t);

                    // Smoothly pull PlayerDrone straight into the center of the Black Hole (0,0,0)
                    playerDrone.transform.position = Vector3.Lerp(playerStartPos, center, smoothT);

                    // Increase camera FOV from 60 to 115 for relativistic warp effect
                    if (droneCam != null)
                    {
                        droneCam.fieldOfView = Mathf.Lerp(startFov, targetFov, smoothT);

                        // Relativistic camera shudder
                        float shudder = Mathf.Lerp(0.01f, 0.25f, smoothT);
                        droneCam.transform.localPosition = camLocalPos + (Random.insideUnitSphere * shudder);
                    }

                    yield return null;
                }

                if (playerDrone != null)
                {
                    playerDrone.transform.position = center;
                }
                if (droneCam != null)
                {
                    droneCam.transform.localPosition = camLocalPos;
                }
            }

            // The moment the player reaches (0,0,0), call SceneTransitionManager.Instance.TransitionToScene("CreditsScene")!
            Debug.Log("<color=#FF00FF>[FINALE] Singularity crossed! Transitioning to CreditsScene...</color>");

            if (SceneTransitionManager.Instance != null)
            {
                SceneTransitionManager.Instance.TransitionToScene("CreditsScene");
            }
            else
            {
                SceneManager.LoadScene("CreditsScene");
            }
        }

        private IEnumerator FlashAlertRoutine()
        {
            if (_finaleAlertText == null) yield break;

            Color c1 = new Color(1f, 0.2f, 0.2f, 1f);
            Color c2 = new Color(1f, 0.85f, 0.1f, 1f);

            while (isFinaleTriggered && _finaleAlertPanel != null && _finaleAlertPanel.activeSelf)
            {
                float t = 0.5f + (0.5f * Mathf.Sin(Time.time * 8f));
                _finaleAlertText.color = Color.Lerp(c1, c2, t);
                yield return null;
            }
        }

        private void BuildFinaleUI()
        {
            GameObject hudCanvas = GameObject.Find("HUD_Canvas");
            if (hudCanvas == null) return;

            Transform existingAlert = hudCanvas.transform.Find("Panel_FinaleAlert");
            if (existingAlert == null)
            {
                _finaleAlertPanel = new GameObject("Panel_FinaleAlert", typeof(RectTransform), typeof(Image));
                _finaleAlertPanel.transform.SetParent(hudCanvas.transform, false);

                RectTransform rt = _finaleAlertPanel.GetComponent<RectTransform>();
                rt.anchorMin = new Vector2(0.5f, 1f);
                rt.anchorMax = new Vector2(0.5f, 1f);
                rt.pivot = new Vector2(0.5f, 1f);
                rt.anchoredPosition = new Vector2(0f, -95f);
                rt.sizeDelta = new Vector2(850f, 48f);

                Image bg = _finaleAlertPanel.GetComponent<Image>();
                bg.color = new Color(0.12f, 0.02f, 0.02f, 0.92f);

                GameObject textGo = new GameObject("Text_Alert", typeof(RectTransform), typeof(TextMeshProUGUI));
                textGo.transform.SetParent(_finaleAlertPanel.transform, false);
                RectTransform textRt = textGo.GetComponent<RectTransform>();
                textRt.anchorMin = Vector2.zero;
                textRt.anchorMax = Vector2.one;
                textRt.offsetMin = new Vector2(8f, 4f);
                textRt.offsetMax = new Vector2(-8f, -4f);

                _finaleAlertText = textGo.GetComponent<TextMeshProUGUI>();
                _finaleAlertText.alignment = TextAlignmentOptions.Center;
                _finaleAlertText.fontSize = 17;
                _finaleAlertText.fontStyle = FontStyles.Bold;
                _finaleAlertText.text = "CRITICAL MASS REACHED! SINGULARITY OVERFLOW — INITIATING SECTOR COLLAPSE!";
                _finaleAlertText.color = new Color(1f, 0.3f, 0.2f, 1f);

                _finaleAlertPanel.SetActive(false);
            }
            else
            {
                _finaleAlertPanel = existingAlert.gameObject;
                _finaleAlertText = _finaleAlertPanel.GetComponentInChildren<TextMeshProUGUI>();
                _finaleAlertPanel.SetActive(false);
            }
        }
    }
}