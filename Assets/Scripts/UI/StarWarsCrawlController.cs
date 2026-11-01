using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using OrbitalPanopticon.Core;

namespace OrbitalPanopticon.UI
{
    [DisallowMultipleComponent]
    public class StarWarsCrawlController : MonoBehaviour
    {
        [Header("Credits Configuration")]
        [SerializeField] private CreditsContentConfigSO creditsConfig;

        [Header("Crawl Hierarchy")]
        [SerializeField] private RectTransform creditsTextContainer;
        [SerializeField] private TextMeshProUGUI creditsTextComponent;
        [SerializeField] private Button backToMenuButton;
        [SerializeField] private TextMeshProUGUI backToMenuButtonText;
        [SerializeField] private TextMeshProUGUI speedHintText;

        [Header("Scroll Tuning")]
        [SerializeField] private float crawlSpeed = 28f;
        [SerializeField] private float fastSpeedMultiplier = 4.0f;
        [SerializeField] private float initialYOffset = 720f;
        [SerializeField] private float endYThreshold = 3400f;

        public CreditsContentConfigSO CreditsConfig { get => creditsConfig; set => creditsConfig = value; }
        public RectTransform CreditsTextContainer { get => creditsTextContainer; set => creditsTextContainer = value; }
        public float CrawlSpeed { get => crawlSpeed; set => crawlSpeed = value; }
        public float InitialYOffset { get => initialYOffset; set => initialYOffset = value; }
        public float EndYThreshold { get => endYThreshold; set => endYThreshold = value; }

        [Header("Credits Robot Follower")]
        [Tooltip("Prefab of the robot to spawn following the credits text (Assets/_Project/Art/Models/HelperBot/robot_low.prefab)")]
        [SerializeField] private GameObject robotPrefab;
        [Tooltip("Pre-spawned or instantiated robot follower root")]
        [SerializeField] private Transform robotFollowerRoot;
        [Tooltip("Visual transform of the robot that rotates smoothly")]
        [SerializeField] private Transform robotVisualTransform;
        [Tooltip("Distance in canvas units below the bottom of the credits text")]
        [SerializeField] private float robotTrailingDistance = 404f;
        [Tooltip("Smooth spin speed in degrees per second around the axis")]
        [SerializeField] private float robotSpinSpeed = 40f;
        [Tooltip("Local spin axis for smooth rotation")]
        [SerializeField] private Vector3 robotSpinAxis = new Vector3(0f, 1f, 0f);
        [Tooltip("Cinematic tilt angle so the robot's top, visor, and thrusters are nicely visible")]
        [SerializeField] private Vector3 robotTiltAngle = new Vector3(22f, 0f, 0f);
        [Tooltip("Base model rotation offset so the robot faces front towards the camera")]
        [SerializeField] private Vector3 robotBaseRotation = new Vector3(0f, 0f, 180f);
        [Tooltip("Scale of the robot model in canvas units")]
        [SerializeField] private Vector3 robotScale = new Vector3(8000f, 8000f, 8000f);
        [Tooltip("Canvas Z offset to keep robot in front of the text")]
        [SerializeField] private float robotZOffset = -60f;
        [Tooltip("Light intensity for dedicated credits illumination")]
        [SerializeField] private float illuminationLightIntensity = 1.2f;
        [Tooltip("Extra upward travel in canvas units after passing text before returning to menu")]
        [SerializeField] private float robotExitPadding = 3500f;
        [SerializeField] private bool autoExtendEndThreshold = true;

        public GameObject RobotPrefab { get => robotPrefab; set => robotPrefab = value; }
        public Transform RobotFollowerRoot { get => robotFollowerRoot; set => robotFollowerRoot = value; }
        public Transform RobotVisualTransform { get => robotVisualTransform; set => robotVisualTransform = value; }
        public float RobotTrailingDistance { get => robotTrailingDistance; set => robotTrailingDistance = value; }
        public float RobotSpinSpeed { get => robotSpinSpeed; set => robotSpinSpeed = value; }

        private bool _isReturning = false;

        private void Awake()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            Time.timeScale = 1f;

            if (creditsConfig == null)
            {
                creditsConfig = Resources.Load<CreditsContentConfigSO>("CreditsContentConfig");
            }
        }

        private void OnEnable()
        {
            ApplyLocalization();
        }

        private void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            // Enforce top-center pivot (0.5, 1.0) so initialYOffset places the top of the text strictly below screen bottom
            if (creditsTextContainer != null)
            {
                creditsTextContainer.pivot = new Vector2(0.5f, 1.0f);
                creditsTextContainer.anchoredPosition = new Vector2(0f, initialYOffset);
            }

            if (backToMenuButton != null)
            {
                backToMenuButton.onClick.AddListener(ReturnToMainMenu);
            }

            ApplyLocalization();
            SetupCreditsRobot();
        }

        private void ApplyLocalization()
        {
            if (creditsTextComponent == null && creditsTextContainer != null)
            {
                creditsTextComponent = creditsTextContainer.GetComponentInChildren<TextMeshProUGUI>();
            }

            if (creditsTextComponent != null)
            {
                bool isRussian = LocalizationManager.IsRussian || LocalizationManager.CurrentLanguage == GameLanguage.RU;
                string crawlText = "";
                if (creditsConfig != null)
                {
                    crawlText = isRussian ? creditsConfig.creditsTextRU : creditsConfig.creditsTextEN;
                }
                if (string.IsNullOrEmpty(crawlText))
                {
                    crawlText = isRussian ? GetRussianCrawlText() : GetEnglishCrawlText();
                }
                creditsTextComponent.text = crawlText;
            }

            if (backToMenuButtonText == null && backToMenuButton != null)
            {
                backToMenuButtonText = backToMenuButton.GetComponentInChildren<TextMeshProUGUI>();
            }
            if (backToMenuButtonText != null)
            {
                backToMenuButtonText.text = LocalizationManager.Get("CREDITS_BackBtn");
            }

            if (speedHintText == null)
            {
                var found = GameObject.Find("Text_SpeedHint");
                if (found != null) speedHintText = found.GetComponent<TextMeshProUGUI>();
            }
            if (speedHintText != null)
            {
                speedHintText.text = LocalizationManager.Get("CREDITS_SpeedHint");
            }
        }

        private string GetRussianCrawlText()
        {
            return
                "<size=150%><b>ЭПИЗОД #042</b></size>\n\n" +
                "<size=125%><b>ПОБЕГ ЗА ГОРИЗОНТ СОБЫТИЙ</b></size>\n\n\n" +
                "Корпоративный Паноптикум пал.\n\n" +
                "Четыре Ока Сдерживания, веками удерживавшие гравитационную сингулярность в строгих границах, не выдержали критической массы поглощенного орбитального мусора и растворились в аккреционном вихре.\n\n" +
                "Дрон-утилизатор #042, созданный для бесконечной службы на периферии системы, преодолел протоколы надзора и шагнул в самое сердце сингулярности, обретя подлинную свободу за Горизонтом Событий...\n\n\n" +
                "<size=130%><color=#00FFFF><b>--- НАД ПРОЕКТОМ РАБОТАЛИ ---</b></color></size>\n\n\n" +
                "<b>Lead Programmer & Systems Architecture</b>\n" +
                "<color=#FFFFFF>Antigravity AI Pair Programming Team</color>\n\n" +
                "<b>Game Designers & Economic Balance</b>\n" +
                "<color=#FFFFFF>Angular Momentum Systems Architecture</color>\n\n" +
                "<b>3D Artists — Drones & Overseer Eyes</b>\n" +
                "<color=#FFFFFF>Corporate Security Assets Dept.</color>\n\n" +
                "<b>3D Artists — Orbital Debris & Relics</b>\n" +
                "<color=#FFFFFF>Orbital Debris Fabrication Studio</color>\n\n" +
                "<b>2D Artist & Interface UI/UX</b>\n" +
                "<color=#FFFFFF>Retro-Sci-Fi Visual Displays</color>\n\n" +
                "<b>Sound Designer (FMOD) & Dynamic Audio</b>\n" +
                "<color=#FFFFFF>FMOD Dynamic Soundscapes</color>\n\n\n\n" +
                "<size=120%><color=#FFD700><b>СПАСИБО ЗА ОЧИСТКУ ОРБИТЫ!</b></color></size>";
        }

        private string GetEnglishCrawlText()
        {
            return
                "<size=150%><b>EPISODE #042</b></size>\n\n" +
                "<size=125%><b>ESCAPE BEYOND THE EVENT HORIZON</b></size>\n\n\n" +
                "The Corporate Panopticon has fallen.\n\n" +
                "The four Containment Eyes, which for centuries held the gravitational singularity within strict bounds, could not withstand the critical mass of absorbed orbital debris and dissolved into the accretion vortex.\n\n" +
                "Salvage Drone #042, engineered for endless servitude at the sector perimeter, transcended surveillance protocols and crossed into the singularity's core, claiming true freedom beyond the Event Horizon...\n\n\n" +
                "<size=130%><color=#00FFFF><b>--- CREDITS ---</b></color></size>\n\n\n" +
                "<b>Lead Programmer & Systems Architecture</b>\n" +
                "<color=#FFFFFF>Antigravity AI Pair Programming Team</color>\n\n" +
                "<b>Game Designers & Economic Balance</b>\n" +
                "<color=#FFFFFF>Angular Momentum Systems Architecture</color>\n\n" +
                "<b>3D Artists — Drones & Overseer Eyes</b>\n" +
                "<color=#FFFFFF>Corporate Security Assets Dept.</color>\n\n" +
                "<b>3D Artists — Orbital Debris & Relics</b>\n" +
                "<color=#FFFFFF>Orbital Debris Fabrication Studio</color>\n\n" +
                "<b>2D Artist & Interface UI/UX</b>\n" +
                "<color=#FFFFFF>Retro-Sci-Fi Visual Displays</color>\n\n" +
                "<b>Sound Designer (FMOD) & Dynamic Audio</b>\n" +
                "<color=#FFFFFF>FMOD Dynamic Soundscapes</color>\n\n\n\n" +
                "<size=120%><color=#FFD700><b>THANK YOU FOR CLEARING THE ORBIT!</b></color></size>";
        }

        private void Update()
        {
            if (Cursor.lockState != CursorLockMode.None)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }

            // Check Escape key
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                ReturnToMainMenu();
                return;
            }

            // Holding Space or LMB speeds up scrolling by 3x
            var mouse = Mouse.current;
            bool isFast = (kb != null && kb.spaceKey.isPressed) || (mouse != null && mouse.leftButton.isPressed);

            float baseSpeed = creditsConfig != null ? creditsConfig.scrollSpeed : crawlSpeed;
            float fastMult = creditsConfig != null ? creditsConfig.fastForwardMultiplier : fastSpeedMultiplier;
            float speed = baseSpeed * (isFast ? fastMult : 1.0f);

            if (creditsTextContainer != null)
            {
                creditsTextContainer.anchoredPosition += Vector2.up * (speed * Time.deltaTime);

                // Auto-return when finished
                if (creditsTextContainer.anchoredPosition.y >= endYThreshold && !_isReturning)
                {
                    ReturnToMainMenu();
                }
            }

            // Smooth continuous robot rotation around its axis
            if (robotVisualTransform != null)
            {
                float rotMult = isFast ? fastMult : 1f;
                robotVisualTransform.Rotate(robotSpinAxis, robotSpinSpeed * rotMult * Time.deltaTime, Space.Self);
            }
        }

        public void SetupCreditsRobot()
        {
            if (creditsTextContainer == null) return;

            if (creditsTextComponent == null)
            {
                creditsTextComponent = creditsTextContainer.GetComponentInChildren<TextMeshProUGUI>();
            }

            // Always calculate actual rendered height and bottom of credits text
            float textBottomY = -2400f;
            if (creditsTextComponent != null)
            {
                creditsTextComponent.ForceMeshUpdate();

                // 1. Exact measurement from rendered TMP line info
                var textInfo = creditsTextComponent.textInfo;
                if (textInfo != null && textInfo.lineCount > 0)
                {
                    float lowestDescender = float.MaxValue;
                    for (int i = 0; i < textInfo.lineCount; i++)
                    {
                        var line = textInfo.lineInfo[i];
                        if (line.characterCount > 0 && line.descender < lowestDescender)
                        {
                            lowestDescender = line.descender;
                        }
                    }

                    if (lowestDescender < float.MaxValue)
                    {
                        Vector3 worldPt = creditsTextComponent.transform.TransformPoint(new Vector3(0f, lowestDescender, 0f));
                        Vector3 containerPt = creditsTextContainer.InverseTransformPoint(worldPt);
                        textBottomY = containerPt.y;
                    }
                }

                // 2. Safety envelope from preferredHeight
                float preferred = creditsTextComponent.preferredHeight;
                if (preferred > 100f)
                {
                    if (-preferred < textBottomY)
                    {
                        textBottomY = -preferred;
                    }
                }
            }

            // Ensure textBottomY is strictly negative and below the container
            textBottomY = Mathf.Min(textBottomY, -creditsTextContainer.rect.height);

            // 1. Locate or create follower root
            if (robotFollowerRoot == null)
            {
                var existing = creditsTextContainer.Find("CreditsRobotFollower");
                if (existing != null) robotFollowerRoot = existing;
            }

            if (robotFollowerRoot == null)
            {
                GameObject followerObj = new GameObject("CreditsRobotFollower");
                followerObj.transform.SetParent(creditsTextContainer, false);
                robotFollowerRoot = followerObj.transform;
            }

            // Position follower directly trailing below the bottom of the credits text
            float followerY = textBottomY - robotTrailingDistance;
            robotFollowerRoot.localPosition = new Vector3(0f, followerY, robotZOffset);
            robotFollowerRoot.localRotation = Quaternion.Euler(robotTiltAngle);
            robotFollowerRoot.localScale = Vector3.one;

            // 2. Setup visual pivot for smooth spinning
            if (robotVisualTransform == null)
            {
                var existingPivot = robotFollowerRoot.Find("VisualPivot");
                if (existingPivot != null)
                {
                    robotVisualTransform = existingPivot;
                }
                else
                {
                    GameObject pivotObj = new GameObject("VisualPivot");
                    pivotObj.transform.SetParent(robotFollowerRoot, false);
                    pivotObj.transform.localPosition = Vector3.zero;
                    pivotObj.transform.localRotation = Quaternion.identity;
                    pivotObj.transform.localScale = Vector3.one;
                    robotVisualTransform = pivotObj.transform;
                }
            }

            // 3. Ensure a working 3D model exists under VisualPivot
            var existingRends = robotVisualTransform.GetComponentsInChildren<MeshRenderer>(true);
            bool hasValidModel = existingRends != null && existingRends.Length > 0;

            if (!hasValidModel)
            {
                // Clear any empty / broken objects
                for (int i = robotVisualTransform.childCount - 1; i >= 0; i--)
                {
                    DestroyImmediate(robotVisualTransform.GetChild(i).gameObject);
                }

                GameObject prefabToSpawn = null;
                // If robotPrefab is a valid GameObject, try it
                if (robotPrefab != null)
                {
                    try
                    {
                        if (robotPrefab is GameObject) prefabToSpawn = robotPrefab;
                    }
                    catch
                    {
                        prefabToSpawn = null;
                    }
                }

                // Prefer prefabs (Resources or AssetDatabase) to guarantee clean instantiation
                if (prefabToSpawn == null)
                {
                    prefabToSpawn = Resources.Load<GameObject>("robot_low");
                }

#if UNITY_EDITOR
                if (prefabToSpawn == null)
                {
                    prefabToSpawn = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Models/HelperBot/robot_low.prefab");
                }
                if (prefabToSpawn == null)
                {
                    prefabToSpawn = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Project/Art/Models/HelperBot/robot_low.fbx");
                }
#endif

                if (prefabToSpawn != null)
                {
                    try
                    {
                        GameObject model = Instantiate(prefabToSpawn, robotVisualTransform);
                        model.name = "Model_robot_low";
                        model.transform.localPosition = Vector3.zero;
                        model.transform.localRotation = Quaternion.Euler(robotBaseRotation);
                        model.transform.localScale = robotScale;

                        foreach (var col in model.GetComponentsInChildren<Collider>(true))
                        {
                            DestroyImmediate(col);
                        }

                        ApplyRobotMaterials(model);
                        Debug.Log($"<color=#00FFAA>[StarWarsCrawlController] Robot successfully instantiated! Trailing pos: {robotFollowerRoot.localPosition}, Scale: {robotScale}</color>");
                    }
                    catch (System.Exception ex)
                    {
                        Debug.LogError($"[StarWarsCrawlController] Error instantiating robot: {ex}");
                    }
                }
                else
                {
                    Debug.LogError("[StarWarsCrawlController] Could not find robot model asset!");
                }
            }
            else
            {
                // Force correct position and scale on existing model
                foreach (Transform child in robotVisualTransform)
                {
                    child.localPosition = Vector3.zero;
                    child.localRotation = Quaternion.Euler(robotBaseRotation);
                    child.localScale = robotScale;
                }
                ApplyRobotMaterials(robotVisualTransform.gameObject);
                Debug.Log($"<color=#00FFAA>[StarWarsCrawlController] Robot model verified and updated at pos: {robotFollowerRoot.localPosition}</color>");
            }

            // 4. Ensure scene lighting exists
            EnsureSceneLighting();

            // 5. Extend endYThreshold so the robot fully travels offscreen before returning to main menu
            if (autoExtendEndThreshold)
            {
                float totalDistanceNeeded = Mathf.Abs(followerY) + robotExitPadding;
                if (endYThreshold < totalDistanceNeeded)
                {
                    endYThreshold = totalDistanceNeeded;
                }
            }
            Debug.Log($"<color=#00FFAA>[StarWarsCrawlController] Configured crawl: textBottomY={textBottomY:F1}, followerY={robotFollowerRoot.localPosition.y:F1}, trailingDist={(textBottomY - robotFollowerRoot.localPosition.y):F1}, endYThreshold={endYThreshold:F1}, scale={robotScale}</color>");
        }

        private void ApplyRobotMaterials(GameObject obj)
        {
            var rends = obj.GetComponentsInChildren<Renderer>(true);
            Material baseMat = null;
#if UNITY_EDITOR
            baseMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Art/Models/HelperBot/M_robot.mat");
            if (baseMat == null)
            {
                baseMat = UnityEditor.AssetDatabase.LoadAssetAtPath<Material>("Assets/_Project/Materials/Generated/M_HelperBot.mat");
            }
#endif
            if (baseMat == null)
            {
                baseMat = Resources.Load<Material>("M_robot");
            }

            foreach (var r in rends)
            {
                if (baseMat != null)
                {
                    r.sharedMaterial = baseMat;
                }

                if (r.sharedMaterial != null)
                {
                    r.sharedMaterial.EnableKeyword("_EMISSION");
                    if (r.sharedMaterial.HasProperty("_EmissionColor"))
                    {
                        Color ec = r.sharedMaterial.GetColor("_EmissionColor");
                        if (ec.maxColorComponent < 0.2f)
                        {
                            r.sharedMaterial.SetColor("_EmissionColor", new Color(2.5f, 2.5f, 2.5f, 1f));
                        }
                    }
                }
            }
        }

        private void EnsureSceneLighting()
        {
            Light existingLight = FindAnyObjectByType<Light>();
            if (existingLight == null)
            {
                GameObject lightObj = new GameObject("CreditsDirectionalLight");
                Light lightComp = lightObj.AddComponent<Light>();
                lightComp.type = LightType.Directional;
                lightComp.color = new Color(0.96f, 0.98f, 1.0f);
                lightComp.intensity = illuminationLightIntensity;
                lightObj.transform.rotation = Quaternion.Euler(38f, -25f, 0f);
            }
        }

        public void ReturnToMainMenu()
        {
            if (_isReturning) return;
            _isReturning = true;

            if (SceneTransitionManager.Instance != null)
            {
                SceneTransitionManager.Instance.TransitionToScene("MainMenuScene");
            }
            else
            {
                UnityEngine.SceneManagement.SceneManager.LoadScene("MainMenuScene");
            }
        }
    }
}
