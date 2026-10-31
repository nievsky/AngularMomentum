using System.IO;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEditor.SceneManagement;
using OrbitalPanopticon.Gameplay;

namespace OrbitalPanopticon.Editor
{
    public static class SetupOuterWildsBlackHole
    {
        private const string LensingMatPath = "Assets/_Project/Materials/M_OuterWildsBlackHole.mat";
        private const string AccretionMatPath = "Assets/_Project/Materials/M_AccretionDisk.mat";

        [MenuItem("Tools/Setup Outer Wilds Black Hole Materials & Scenes")]
        public static void SetupAll()
        {
            Material lensingMat = CreateOrUpdateLensingMaterial();
            Material accretionMat = CreateOrUpdateAccretionMaterial();

            string currentScene = EditorSceneManager.GetActiveScene().path;

            SetupScene("Assets/_Project/Scenes/GameplayScene.unity", lensingMat, accretionMat);
            SetupScene("Assets/_Project/Scenes/MainMenuScene.unity", lensingMat, accretionMat);
            SetupScene("Assets/OutdoorsScene.unity", lensingMat, accretionMat);

            if (!string.IsNullOrEmpty(currentScene) && File.Exists(currentScene))
            {
                EditorSceneManager.OpenScene(currentScene, OpenSceneMode.Single);
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("<color=#00FFCC>[SetupOuterWildsBlackHole] Successfully configured Outer Wilds Gravitational Lensing and Accretion Disk across all scenes!</color>");
        }

        public static Material CreateOrUpdateLensingMaterial()
        {
            Shader shader = Shader.Find("OrbitalPanopticon/OuterWildsBlackHole");
            if (shader == null)
            {
                Debug.LogError("[SetupOuterWildsBlackHole] Shader 'OrbitalPanopticon/OuterWildsBlackHole' not found!");
                return null;
            }

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(LensingMatPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, LensingMatPath);
            }
            else
            {
                mat.shader = shader;
            }

            mat.SetFloat("_CoreRadius", 0.35f);
            mat.SetFloat("_MaxLensingRadius", 1.0f);
            mat.SetFloat("_LensingStrength", 0.08f);
            mat.SetFloat("_LensingFalloff", 1.8f);
            mat.SetColor("_PhotonRingColor", new Color(1.0f, 0.92f, 0.78f, 1.0f));
            mat.SetFloat("_PhotonRingSharpness", 90.0f);
            mat.SetFloat("_PhotonRingIntensity", 1.6f);
            mat.SetFloat("_EdgeFadeSoftness", 0.1f);

            EditorUtility.SetDirty(mat);
            return mat;
        }

        public static Material CreateOrUpdateAccretionMaterial()
        {
            Shader shader = Shader.Find("OrbitalPanopticon/AccretionDisk");
            if (shader == null)
            {
                Debug.LogError("[SetupOuterWildsBlackHole] Shader 'OrbitalPanopticon/AccretionDisk' not found!");
                return null;
            }

            Material mat = AssetDatabase.LoadAssetAtPath<Material>(AccretionMatPath);
            if (mat == null)
            {
                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, AccretionMatPath);
            }
            else
            {
                mat.shader = shader;
            }

            mat.SetColor("_InnerHotColor", new Color(2.6f, 1.9f, 0.75f, 1.0f));
            mat.SetColor("_OuterCoolColor", new Color(0.85f, 0.22f, 0.04f, 1.0f));
            mat.SetFloat("_InnerRadius", 0.32f);
            mat.SetFloat("_InnerSoftness", 0.08f);
            mat.SetFloat("_OuterRadius", 0.95f);
            mat.SetFloat("_OuterSoftness", 0.22f);
            mat.SetFloat("_SwirlSpeed", 1.3f);
            mat.SetVector("_NoiseTiling", new Vector4(4.0f, 7.0f, 0f, 0f));
            mat.SetFloat("_DiskIntensity", 1.8f);

            EditorUtility.SetDirty(mat);
            return mat;
        }

        private static void SetupScene(string scenePath, Material lensingMat, Material accretionMat)
        {
            if (!File.Exists(scenePath)) return;

            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            // 1. Enable Camera Opaque Texture on Camera(s)
            Camera[] cameras = Object.FindObjectsByType<Camera>(FindObjectsInactive.Include);
            foreach (var cam in cameras)
            {
                if (cam == null) continue;
                var camData = cam.GetComponent<UniversalAdditionalCameraData>();
                if (camData == null) camData = cam.gameObject.AddComponent<UniversalAdditionalCameraData>();
                if (camData != null)
                {
                    camData.requiresColorOption = CameraOverrideOption.On;
                    camData.requiresColorTexture = true;
                    EditorUtility.SetDirty(camData);
                }
            }

            // 2. Configure BlackHoleSystem
            GameObject bhSystem = GameObject.Find("BlackHoleSystem");
            if (bhSystem != null)
            {
                Transform visualRoot = bhSystem.transform.Find("VisualRoot");
                if (visualRoot == null) visualRoot = bhSystem.transform;

                // Configure Lensing Sphere
                Transform sphereT = visualRoot.Find("BlackHoleSphere");
                if (sphereT == null && visualRoot.childCount > 0)
                {
                    for (int i = 0; i < visualRoot.childCount; i++)
                    {
                        var child = visualRoot.GetChild(i);
                        if (child.name.IndexOf("Sphere", System.StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            sphereT = child;
                            break;
                        }
                    }
                }

                if (sphereT != null)
                {
                    sphereT.localScale = Vector3.one * 2.8f;
                    var mr = sphereT.GetComponent<MeshRenderer>();
                    if (mr != null && lensingMat != null)
                    {
                        mr.sharedMaterial = lensingMat;
                        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        mr.receiveShadows = false;
                        EditorUtility.SetDirty(mr);
                    }
                    EditorUtility.SetDirty(sphereT);
                }

                // Configure AccretionDiskVisual
                Transform diskT = visualRoot.Find("AccretionDiskVisual");
                if (diskT == null)
                {
                    GameObject diskGo = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    diskGo.name = "AccretionDiskVisual";
                    var col = diskGo.GetComponent<Collider>();
                    if (col != null) Object.DestroyImmediate(col);

                    diskT = diskGo.transform;
                    diskT.SetParent(visualRoot, false);
                }

                if (diskT != null)
                {
                    diskT.localPosition = Vector3.zero;
                    diskT.localRotation = Quaternion.Euler(90f, 0f, 0f);
                    diskT.localScale = new Vector3(7.0f, 7.0f, 1f);

                    var mr = diskT.GetComponent<MeshRenderer>();
                    if (mr != null && accretionMat != null)
                    {
                        mr.sharedMaterial = accretionMat;
                        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        mr.receiveShadows = false;
                        EditorUtility.SetDirty(mr);
                    }

                    // Default to inactive (controlled by GameBalanceConfig.showAccretionDisk)
                    diskT.gameObject.SetActive(false);
                    EditorUtility.SetDirty(diskT.gameObject);
                }

                var stageMgr = bhSystem.GetComponent<BlackHoleStageManager>();
                if (stageMgr != null)
                {
                    EditorUtility.SetDirty(stageMgr);
                }

                EditorUtility.SetDirty(bhSystem);
            }

            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[SetupOuterWildsBlackHole] Updated and saved {scenePath}");
        }
    }
}
