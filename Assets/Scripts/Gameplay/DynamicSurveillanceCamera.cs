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
        [SerializeField] private Transform lensTransform;
        [SerializeField] private Material beamMaterial;

        [Header("Beam & Tracking Tuning")]
        [SerializeField] private float cameraLockRotationSpeed = 25f;
        [SerializeField] private float cameraSearchRotationSpeed = 4f;
        [SerializeField] private float spotRadiusAtTarget = 2.5f;
        [SerializeField] private float idleBeamDistance = 25f;

        [Header("Runtime Status")]
        [SerializeField] private ScrapItem currentTarget;
        [SerializeField] private bool _isLocked = false;
        [SerializeField] private float graviFlashTimer = 0f;

        public ScrapItem CurrentTarget => currentTarget;
        public bool IsLocked => _isLocked;

        private float _flashVisualTimer = 0f;
        private MeshRenderer _beamRenderer;
        private MaterialPropertyBlock _beamPropertyBlock;
        private readonly Color _standardBeamColor = new Color(1f, 0.92f, 0.15f, 0.25f);
        private readonly Color _flashBeamColor = new Color(0.4f, 0.95f, 1f, 0.85f);

        private static UnityEngine.Mesh _sharedConeMesh;

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

            if (lensTransform == null)
            {
                lensTransform = transform.Find("VisualRoot/Lens");
                if (lensTransform == null) lensTransform = transform;
            }

            if (beamTransform == null)
            {
                beamTransform = transform.Find("BeamMesh");
            }

            InitializeBeamMesh();
        }

        private void InitializeBeamMesh()
        {
            if (beamTransform == null) return;

            // Remove any Collider attached so it never interferes with physics or raycasts
            Collider col = beamTransform.GetComponent<Collider>();
            if (col != null)
            {
                if (Application.isPlaying)
                {
                    Destroy(col);
                }
                else
                {
                    DestroyImmediate(col);
                }
            }

            MeshFilter mf = beamTransform.GetComponent<MeshFilter>();
            if (mf == null) mf = beamTransform.gameObject.AddComponent<MeshFilter>();
            mf.sharedMesh = GetOrCreateConeMesh();

            MeshRenderer mr = beamTransform.GetComponent<MeshRenderer>();
            if (mr == null) mr = beamTransform.gameObject.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            _beamRenderer = mr;

            if (beamMaterial == null)
            {
                beamMaterial = LoadOrFallbackBeamMaterial();
            }

            if (beamMaterial != null)
            {
                mr.sharedMaterial = beamMaterial;
            }

            Vector3 lensLocalPos = lensTransform != null ? transform.InverseTransformPoint(lensTransform.position) : new Vector3(0f, 0f, 1.4f);
            beamTransform.localPosition = lensLocalPos;
            beamTransform.localRotation = Quaternion.identity;
        }

        private Material LoadOrFallbackBeamMaterial()
        {
#if UNITY_EDITOR
            Material mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/M_CameraBeamYellow.mat");
            if (mat != null) return mat;
            mat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/M_CameraBeamYellow.mat");
            if (mat != null) return mat;
#endif
            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Transparent");

            if (shader != null)
            {
                Material runtimeMat = new Material(shader) { name = "M_CameraBeamYellow_Runtime" };
                Color yellow = new Color(1.0f, 0.9f, 0.1f, 0.22f);
                runtimeMat.color = yellow;
                if (runtimeMat.HasProperty("_Color")) runtimeMat.SetColor("_Color", yellow);
                if (runtimeMat.HasProperty("_BaseColor")) runtimeMat.SetColor("_BaseColor", yellow);
                if (shader.name.Contains("Universal Render Pipeline"))
                {
                    runtimeMat.SetFloat("_Surface", 1f);
                    runtimeMat.SetFloat("_Blend", 0f);
                    runtimeMat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    runtimeMat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    runtimeMat.SetFloat("_ZWrite", 0f);
                    runtimeMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    runtimeMat.renderQueue = 3000;
                }
                return runtimeMat;
            }
            return null;
        }

        public static UnityEngine.Mesh CreateConeMesh(int segments = 24, float tipRadius = 0.08f, float baseRadius = 1.0f, float length = 1.0f)
        {
            UnityEngine.Mesh mesh = new UnityEngine.Mesh { name = "ProceduralSurveillanceBeamCone" };

            int mantleVertCount = (segments + 1) * 2;
            int baseCapOffset = mantleVertCount;
            int baseCapVertCount = segments + 2;
            int tipCapOffset = baseCapOffset + baseCapVertCount;
            int tipCapVertCount = segments + 2;

            int totalVerts = tipCapOffset + tipCapVertCount;
            Vector3[] vertices = new Vector3[totalVerts];
            Vector2[] uvs = new Vector2[totalVerts];
            Color[] colors = new Color[totalVerts];
            Color coneVertexColor = new Color(1f, 0.92f, 0.15f, 0.25f);

            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float angle = t * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                // Mantle vertices: tip at z=0, base at z=length
                vertices[i * 2] = new Vector3(cos * tipRadius, sin * tipRadius, 0f);
                uvs[i * 2] = new Vector2(t, 0f);
                colors[i * 2] = coneVertexColor;

                vertices[i * 2 + 1] = new Vector3(cos * baseRadius, sin * baseRadius, length);
                uvs[i * 2 + 1] = new Vector2(t, 1f);
                colors[i * 2 + 1] = coneVertexColor;

                // Base cap perimeter (z=length)
                int bIdx = baseCapOffset + 1 + i;
                vertices[bIdx] = new Vector3(cos * baseRadius, sin * baseRadius, length);
                uvs[bIdx] = new Vector2(0.5f + 0.5f * cos, 0.5f + 0.5f * sin);
                colors[bIdx] = coneVertexColor;

                // Tip cap perimeter (z=0)
                int tIdx = tipCapOffset + 1 + i;
                vertices[tIdx] = new Vector3(cos * tipRadius, sin * tipRadius, 0f);
                uvs[tIdx] = new Vector2(0.5f + 0.5f * cos, 0.5f + 0.5f * sin);
                colors[tIdx] = coneVertexColor;
            }

            // Base cap center
            vertices[baseCapOffset] = new Vector3(0f, 0f, length);
            uvs[baseCapOffset] = new Vector2(0.5f, 0.5f);
            colors[baseCapOffset] = coneVertexColor;

            // Tip cap center
            vertices[tipCapOffset] = new Vector3(0f, 0f, 0f);
            uvs[tipCapOffset] = new Vector2(0.5f, 0.5f);
            colors[tipCapOffset] = coneVertexColor;

            List<int> tris = new List<int>();

            // Mantle triangles (double-sided: both front and back faces)
            for (int i = 0; i < segments; i++)
            {
                int t0 = i * 2;
                int b0 = i * 2 + 1;
                int t1 = (i + 1) * 2;
                int b1 = (i + 1) * 2 + 1;

                // Front face
                tris.Add(t0); tris.Add(t1); tris.Add(b1);
                tris.Add(t0); tris.Add(b1); tris.Add(b0);

                // Back face
                tris.Add(t0); tris.Add(b1); tris.Add(t1);
                tris.Add(t0); tris.Add(b0); tris.Add(b1);
            }

            // Base cap triangles (double-sided)
            int baseCenter = baseCapOffset;
            for (int i = 0; i < segments; i++)
            {
                int p0 = baseCapOffset + 1 + i;
                int p1 = baseCapOffset + 1 + i + 1;

                tris.Add(baseCenter); tris.Add(p0); tris.Add(p1);
                tris.Add(baseCenter); tris.Add(p1); tris.Add(p0);
            }

            // Tip cap triangles (double-sided)
            int tipCenter = tipCapOffset;
            for (int i = 0; i < segments; i++)
            {
                int p0 = tipCapOffset + 1 + i;
                int p1 = tipCapOffset + 1 + i + 1;

                tris.Add(tipCenter); tris.Add(p1); tris.Add(p0);
                tris.Add(tipCenter); tris.Add(p0); tris.Add(p1);
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.triangles = tris.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            return mesh;
        }

        public static UnityEngine.Mesh GetOrCreateConeMesh()
        {
            if (_sharedConeMesh == null)
            {
                _sharedConeMesh = CreateConeMesh(24, 0.08f, 1.0f, 1.0f);
            }
            return _sharedConeMesh;
        }

        private void LateUpdate()
        {
            GameBalanceConfig config = GameManager.GameBalanceConfigInstance;
            float speedMult = config != null ? config.globalSimulationSpeed : 1.0f;
            float lockSpeed = (config != null && config.cameraLockRotationSpeed > 0f ? config.cameraLockRotationSpeed : cameraLockRotationSpeed) * speedMult;
            float searchSpeed = (config != null && config.cameraSearchRotationSpeed > 0f ? config.cameraSearchRotationSpeed : cameraSearchRotationSpeed) * speedMult;
            float spotRadius = (config != null && config.cameraSpotRadiusAtTarget > 0f ? config.cameraSpotRadiusAtTarget : spotRadiusAtTarget);

            if (currentTarget == null || !IsTargetValid(currentTarget))
            {
                ReleaseTarget();
                FindNewTarget();
            }

            if (currentTarget != null)
            {
                TrackCurrentTarget(lockSpeed, searchSpeed, spotRadius);
            }
            else
            {
                PerformIdleSweep(searchSpeed, speedMult, spotRadius);
            }

            UpdateGraviFlash(config);
        }

        private void UpdateGraviFlash(GameBalanceConfig config)
        {
            if (config != null && config.cameraGraviFlashUnlocked)
            {
                graviFlashTimer += Time.deltaTime;
                float interval = config.cameraGraviFlashInterval > 0f ? config.cameraGraviFlashInterval : 10f;
                if (graviFlashTimer >= interval)
                {
                    graviFlashTimer = 0f;
                    if (currentTarget != null && IsTargetValid(currentTarget))
                    {
                        TriggerGraviFlash(config);
                    }
                }
            }

            if (_flashVisualTimer > 0f)
            {
                _flashVisualTimer -= Time.deltaTime;
                if (_flashVisualTimer <= 0f)
                {
                    ResetBeamVisual();
                }
            }
        }

        private void TriggerGraviFlash(GameBalanceConfig config)
        {
            _flashVisualTimer = 0.25f;
            SetBeamColor(_flashBeamColor);
            if (spotLight != null)
            {
                spotLight.color = Color.cyan;
                spotLight.intensity = 12f;
            }

            Vector3 center = centerReference != null ? centerReference.position : Vector3.zero;
            Vector3 pushDir = (center - currentTarget.transform.position).normalized;
            float pushImpulse = 26f;

            // Direct impulse pushing current target toward black hole accretion threshold
            currentTarget.TransitionToLaunched(pushDir * pushImpulse);

            // Also push nearby scrap inside the cone
            Collider[] colliders = UnityEngine.Physics.OverlapSphere(currentTarget.transform.position, 6.0f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (colliders[i] == null) continue;
                ScrapItem scrap = colliders[i].GetComponentInParent<ScrapItem>();
                if (scrap != null && scrap != currentTarget && scrap.CurrentState == ScrapState.Floating)
                {
                    Vector3 toCenter = (center - scrap.transform.position).normalized;
                    scrap.TransitionToLaunched(toCenter * pushImpulse);
                }
            }
        }

        private void ResetBeamVisual()
        {
            SetBeamColor(_standardBeamColor);
            if (spotLight != null)
            {
                spotLight.color = new Color(1f, 0.92f, 0.15f);
                spotLight.intensity = 4f;
            }
        }

        private void SetBeamColor(Color color)
        {
            if (_beamRenderer == null && beamTransform != null)
            {
                _beamRenderer = beamTransform.GetComponent<MeshRenderer>();
            }

            if (_beamRenderer != null)
            {
                if (_beamPropertyBlock == null) _beamPropertyBlock = new MaterialPropertyBlock();
                _beamRenderer.GetPropertyBlock(_beamPropertyBlock);
                _beamPropertyBlock.SetColor("_BaseColor", color);
                _beamPropertyBlock.SetColor("_Color", color);
                _beamRenderer.SetPropertyBlock(_beamPropertyBlock);
            }
        }

        private void TrackCurrentTarget(float lockSpeed, float searchSpeed, float spotRadius)
        {
            Vector3 toTarget = currentTarget.transform.position - transform.position;
            float dist = toTarget.magnitude;

            if (dist > 0.001f)
            {
                Quaternion targetRot = Quaternion.LookRotation(toTarget.normalized, Vector3.up);
                float angle = Quaternion.Angle(transform.rotation, targetRot);

                if (!_isLocked && angle < 10f)
                {
                    _isLocked = true;
                }
                else if (_isLocked && angle > 50f)
                {
                    _isLocked = false;
                }

                float currentSpeed = _isLocked ? lockSpeed : searchSpeed;
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Mathf.Clamp01(currentSpeed * Time.deltaTime));
            }

            Vector3 lensPos = lensTransform != null ? lensTransform.position : transform.position;
            Vector3 lensLocalPos = lensTransform != null ? transform.InverseTransformPoint(lensTransform.position) : new Vector3(0f, 0f, 1.4f);

            // True Cone Geometry scaling & positioning
            if (beamTransform != null)
            {
                beamTransform.localPosition = lensLocalPos;
                beamTransform.localRotation = Quaternion.identity;

                float targetDist = Vector3.Distance(lensPos, currentTarget.transform.position);
                beamTransform.localScale = new Vector3(spotRadius, spotRadius, targetDist);
            }

            if (spotLight != null)
            {
                float targetDist = Vector3.Distance(lensPos, currentTarget.transform.position);
                spotLight.range = targetDist + 10f;
            }

            // Ensure observed status is maintained on the scrap item
            currentTarget.IsTargetedByCamera = true;
            if (!currentTarget.IsObserved)
            {
                currentTarget.SetObserved(true);
            }
        }

        private void PerformIdleSweep(float searchSpeed, float speedMult, float spotRadius)
        {
            _isLocked = false;

            Vector3 center = centerReference != null ? centerReference.position : Vector3.zero;
            Vector3 toCenter = center - transform.position;

            if (toCenter.sqrMagnitude > 0.001f)
            {
                Quaternion baseRot = Quaternion.LookRotation(toCenter.normalized, Vector3.up);
                float sweepOffset = Mathf.Sin(Time.time * (0.8f * speedMult)) * 12f;
                Quaternion sweepRot = baseRot * Quaternion.Euler(0f, sweepOffset, 0f);
                transform.rotation = Quaternion.Slerp(transform.rotation, sweepRot, searchSpeed * Time.deltaTime);
            }

            Vector3 lensPos = lensTransform != null ? lensTransform.position : transform.position;
            Vector3 lensLocalPos = lensTransform != null ? transform.InverseTransformPoint(lensTransform.position) : new Vector3(0f, 0f, 1.4f);

            if (beamTransform != null)
            {
                beamTransform.localPosition = lensLocalPos;
                beamTransform.localRotation = Quaternion.identity;
                beamTransform.localScale = new Vector3(spotRadius, spotRadius, idleBeamDistance);
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
            float accretionRadius = config != null ? config.accretionThresholdRadius : 15f;
            Vector3 center = centerReference != null ? centerReference.position : Vector3.zero;
            float distFromCenter = Vector3.Distance(scrap.transform.position, center);

            if (distFromCenter > maxRadius)
            {
                return false;
            }

            // If the scrap entered the accretion zone on its own, camera loses interest
            // unless the player is currently holding it
            if (distFromCenter <= accretionRadius && scrap.CurrentState != ScrapState.Held)
            {
                return false;
            }

            return true;
        }

        private void FindNewTarget()
        {
            GameBalanceConfig config = GameManager.GameBalanceConfigInstance;
            float maxRadius = config != null ? config.arenaMaxRadius : 48f;
            float accretionRadius = config != null ? config.accretionThresholdRadius : 15f;
            Vector3 center = centerReference != null ? centerReference.position : Vector3.zero;

            List<ScrapItem> candidates = new List<ScrapItem>();

            IReadOnlyList<ScrapItem> activeList = ScrapSimulationManager.Instance != null
                ? ScrapSimulationManager.Instance.ActiveScrap
                : Object.FindObjectsByType<ScrapItem>(FindObjectsInactive.Exclude);

            for (int i = 0; i < activeList.Count; i++)
            {
                ScrapItem s = activeList[i];
                if (s == null) continue;

                // Priority target selection: Cameras should only pick ScrapItem targets that are currently in the calm Outer Zone (dist > config.accretionThresholdRadius)
                if (s.CurrentState == ScrapState.Floating && !s.IsTargetedByCamera && s.CurrentState != ScrapState.Consumed)
                {
                    float dist = Vector3.Distance(s.transform.position, center);
                    if (dist > accretionRadius && dist <= maxRadius)
                    {
                        candidates.Add(s);
                    }
                }
            }

            if (candidates.Count > 0)
            {
                int index = Random.Range(0, candidates.Count);
                currentTarget = candidates[index];
                currentTarget.IsTargetedByCamera = true;
                currentTarget.SetObserved(true);
                _isLocked = false;
            }
        }

        public void ReleaseTarget()
        {
            _isLocked = false;
            if (currentTarget != null)
            {
                currentTarget.IsTargetedByCamera = false;
                // If it was captured by the player or launched toward black hole, keep observation bonus!
                // Only if it remained floating untouched do we un-observe it
                if (currentTarget.CurrentState == ScrapState.Floating)
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
