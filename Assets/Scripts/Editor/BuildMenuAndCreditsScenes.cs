using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;
using OrbitalPanopticon.UI;
using OrbitalPanopticon.Gameplay;
using OrbitalPanopticon.Physics;

namespace OrbitalPanopticon.Editor
{
    public static class BuildMenuAndCreditsScenes
    {
        [MenuItem("Tools/Build Menu and Credits Scenes")]
        public static void BuildAll()
        {
            string currentActiveScene = EditorSceneManager.GetActiveScene().path;

            string scenesDir = "Assets/_Project/Scenes";
            if (!Directory.Exists(scenesDir))
            {
                Directory.CreateDirectory(scenesDir);
                AssetDatabase.Refresh();
            }

            string mainMenuPath = "Assets/_Project/Scenes/MainMenuScene.unity";
            string gameplayPath = "Assets/_Project/Scenes/GameplayScene.unity";
            string creditsPath = "Assets/_Project/Scenes/CreditsScene.unity";

            // -----------------------------------------------------------------
            // 1. BUILD MAIN MENU SCENE
            // -----------------------------------------------------------------
            var menuScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Directional Light
            GameObject dirLightGo = new GameObject("Directional Light", typeof(Light));
            Light dirLight = dirLightGo.GetComponent<Light>();
            dirLight.type = LightType.Directional;
            dirLight.color = new Color(0.65f, 0.8f, 1.0f);
            dirLight.intensity = 0.85f;
            dirLightGo.transform.rotation = Quaternion.Euler(45f, -30f, 0f);

            // Main Camera with MenuCameraOrbit
            GameObject camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(MenuCameraOrbit));
            Camera cam = camGo.GetComponent<Camera>();
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.015f, 0.02f, 0.035f, 1.0f);
            cam.fieldOfView = 60f;
            camGo.tag = "MainCamera";
            camGo.transform.position = new Vector3(0f, 6.5f, -32f);

            // BlackHoleSystem
            GameObject bhPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/BlackHoleSystem.prefab");
            GameObject bhInstance = null;
            if (bhPrefab != null)
            {
                bhInstance = (GameObject)PrefabUtility.InstantiatePrefab(bhPrefab);
                bhInstance.name = "BlackHoleSystem";
                bhInstance.transform.position = Vector3.zero;
            }
            else
            {
                bhInstance = new GameObject("BlackHoleSystem", typeof(BlackHoleGravity));
                bhInstance.transform.position = Vector3.zero;
            }

            // ScrapSpawner
            GameObject spawnerGo = new GameObject("ScrapSpawner", typeof(ScrapSpawner));
            spawnerGo.transform.position = Vector3.zero;
            ScrapSpawner spawner = spawnerGo.GetComponent<ScrapSpawner>();

            var spawnEntries = new List<ScrapSpawner.ScrapSpawnEntry>();

            var p1 = AssetDatabase.LoadAssetAtPath<ScrapItem>("Assets/Prefabs/Scrap_Tier1_Bolt.prefab");
            var d1 = AssetDatabase.LoadAssetAtPath<ScrapData>("Assets/ScriptableObjects/SO_Scrap_Tier1_Bolt.asset");
            if (p1 != null) spawnEntries.Add(new ScrapSpawner.ScrapSpawnEntry { prefab = p1, scrapData = d1, weight = 35 });

            var p2 = AssetDatabase.LoadAssetAtPath<ScrapItem>("Assets/Prefabs/Scrap_Tier2_Satellite.prefab");
            var d2 = AssetDatabase.LoadAssetAtPath<ScrapData>("Assets/ScriptableObjects/SO_Scrap_Tier2_Satellite.asset");
            if (p2 != null) spawnEntries.Add(new ScrapSpawner.ScrapSpawnEntry { prefab = p2, scrapData = d2, weight = 25 });

            var p3 = AssetDatabase.LoadAssetAtPath<ScrapItem>("Assets/Prefabs/Scrap_Tier3_Fridge.prefab");
            var d3 = AssetDatabase.LoadAssetAtPath<ScrapData>("Assets/ScriptableObjects/SO_Scrap_Tier3_Fridge.asset");
            if (p3 != null) spawnEntries.Add(new ScrapSpawner.ScrapSpawnEntry { prefab = p3, scrapData = d3, weight = 20 });

            var p4 = AssetDatabase.LoadAssetAtPath<ScrapItem>("Assets/Prefabs/Scrap_Tier4_Anomaly.prefab");
            var d4 = AssetDatabase.LoadAssetAtPath<ScrapData>("Assets/ScriptableObjects/SO_Scrap_Tier4_Anomaly.asset");
            if (p4 != null) spawnEntries.Add(new ScrapSpawner.ScrapSpawnEntry { prefab = p4, scrapData = d4, weight = 10 });

            SerializedObject spawnerSo = new SerializedObject(spawner);
            spawnerSo.FindProperty("minSpawnRadius").floatValue = 16f;
            spawnerSo.FindProperty("maxSpawnRadius").floatValue = 36f;
            spawnerSo.FindProperty("maxActiveScrap").intValue = 45;
            spawnerSo.FindProperty("prewarmCount").intValue = 40;
            spawnerSo.FindProperty("spawnInterval").floatValue = 0.9f;
            spawnerSo.FindProperty("minInitialSpeed").floatValue = 3.0f;
            spawnerSo.FindProperty("maxInitialSpeed").floatValue = 6.5f;
            spawnerSo.ApplyModifiedProperties();
            spawner.SetSpawnEntries(spawnEntries);

            // EventSystem
            GameObject eventGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            // Main Menu Canvas
            GameObject canvasGo = new GameObject("MainMenu_Canvas", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            Canvas menuCanvas = canvasGo.GetComponent<Canvas>();
            menuCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var menuScaler = canvasGo.GetComponent<UnityEngine.UI.CanvasScaler>();
            menuScaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            menuScaler.referenceResolution = new Vector2(1920, 1080);
            menuScaler.matchWidthOrHeight = 0.5f;

            // Title Container (Left-Anchored)
            GameObject titleGo = new GameObject("Text_Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            titleGo.transform.SetParent(canvasGo.transform, false);
            RectTransform titleRt = titleGo.GetComponent<RectTransform>();
            titleRt.anchorMin = new Vector2(0f, 1f);
            titleRt.anchorMax = new Vector2(0f, 1f);
            titleRt.pivot = new Vector2(0f, 1f);
            titleRt.anchoredPosition = new Vector2(120f, -90f);
            titleRt.sizeDelta = new Vector2(700f, 65f);
            TextMeshProUGUI titleTxt = titleGo.GetComponent<TextMeshProUGUI>();
            titleTxt.text = "ORBITAL PANOPTICON";
            titleTxt.fontSize = 44;
            titleTxt.fontStyle = FontStyles.Bold;
            titleTxt.color = new Color(0.2f, 0.92f, 1.0f, 1.0f);

            // Subtitle
            GameObject subGo = new GameObject("Text_Subtitle", typeof(RectTransform), typeof(TextMeshProUGUI));
            subGo.transform.SetParent(canvasGo.transform, false);
            RectTransform subRt = subGo.GetComponent<RectTransform>();
            subRt.anchorMin = new Vector2(0f, 1f);
            subRt.anchorMax = new Vector2(0f, 1f);
            subRt.pivot = new Vector2(0f, 1f);
            subRt.anchoredPosition = new Vector2(122f, -155f);
            subRt.sizeDelta = new Vector2(700f, 32f);
            TextMeshProUGUI subTxt = subGo.GetComponent<TextMeshProUGUI>();
            subTxt.text = "SURVEILLANCE SECTOR #042 // СЕКТОР НАДЗОРА #042";
            subTxt.fontSize = 15;
            subTxt.fontStyle = FontStyles.Bold;
            subTxt.color = new Color(1.0f, 0.85f, 0.2f, 0.95f);

            // Buttons Container
            GameObject btnGroup = new GameObject("ButtonGroup", typeof(RectTransform));
            btnGroup.transform.SetParent(canvasGo.transform, false);
            RectTransform btnGroupRt = btnGroup.GetComponent<RectTransform>();
            btnGroupRt.anchorMin = new Vector2(0f, 0.5f);
            btnGroupRt.anchorMax = new Vector2(0f, 0.5f);
            btnGroupRt.pivot = new Vector2(0f, 0.5f);
            btnGroupRt.anchoredPosition = new Vector2(120f, -30f);
            btnGroupRt.sizeDelta = new Vector2(340f, 320f);

            var startBtn = CreateMenuButton(btnGroup.transform, new Vector2(0f, 100f), "НАЧАТЬ ИГРУ", new Color(0.2f, 0.9f, 1f));
            var settingsBtn = CreateMenuButton(btnGroup.transform, new Vector2(0f, 35f), "НАСТРОЙКИ", new Color(0.7f, 0.85f, 1f));
            var creditsBtn = CreateMenuButton(btnGroup.transform, new Vector2(0f, -30f), "АВТОРЫ / КРЕДИТЫ", new Color(1f, 0.85f, 0.25f));
            var quitBtn = CreateMenuButton(btnGroup.transform, new Vector2(0f, -95f), "ВЫЙТИ ИЗ ИГРЫ", new Color(1f, 0.35f, 0.35f));

            // Settings Sub-Panel
            GameObject settingsPanelGo = new GameObject("Panel_Settings", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Outline));
            settingsPanelGo.transform.SetParent(canvasGo.transform, false);
            RectTransform settingsRt = settingsPanelGo.GetComponent<RectTransform>();
            settingsRt.anchorMin = new Vector2(0.5f, 0.5f);
            settingsRt.anchorMax = new Vector2(0.5f, 0.5f);
            settingsRt.pivot = new Vector2(0.5f, 0.5f);
            settingsRt.sizeDelta = new Vector2(560f, 380f);
            var settingsBg = settingsPanelGo.GetComponent<UnityEngine.UI.Image>();
            settingsBg.color = new Color(0.04f, 0.08f, 0.14f, 0.98f);
            var settingsOutline = settingsPanelGo.GetComponent<UnityEngine.UI.Outline>();
            settingsOutline.effectColor = new Color(0.2f, 0.9f, 1f, 0.85f);
            settingsOutline.effectDistance = new Vector2(2f, -2f);

            // Settings Title
            GameObject sTitleGo = new GameObject("Title", typeof(RectTransform), typeof(TextMeshProUGUI));
            sTitleGo.transform.SetParent(settingsPanelGo.transform, false);
            RectTransform sTitleRt = sTitleGo.GetComponent<RectTransform>();
            sTitleRt.anchorMin = new Vector2(0.5f, 1f);
            sTitleRt.anchorMax = new Vector2(0.5f, 1f);
            sTitleRt.pivot = new Vector2(0.5f, 1f);
            sTitleRt.anchoredPosition = new Vector2(0f, -24f);
            sTitleRt.sizeDelta = new Vector2(500f, 40f);
            TextMeshProUGUI sTitleTxt = sTitleGo.GetComponent<TextMeshProUGUI>();
            sTitleTxt.text = "НАСТРОЙКИ СИСТЕМЫ";
            sTitleTxt.fontSize = 20;
            sTitleTxt.fontStyle = FontStyles.Bold;
            sTitleTxt.alignment = TextAlignmentOptions.Center;
            sTitleTxt.color = new Color(0.2f, 0.9f, 1f, 1f);

            // Volume Slider
            var volControls = CreateSliderRow(settingsPanelGo.transform, new Vector2(0f, 40f), "Громкость звука (Master Volume):", 0f, 1f, 0.85f);
            // Mouse Sens Slider
            var sensControls = CreateSliderRow(settingsPanelGo.transform, new Vector2(0f, -40f), "Чувствительность мыши (Sens):", 0.2f, 5.0f, 1.8f);

            // Close Settings Button
            var closeSettingsBtn = CreateMenuButton(settingsPanelGo.transform, new Vector2(0f, -125f), "ЗАКРЫТЬ", new Color(1f, 0.85f, 0.2f), 180f, 42f);

            // MainMenuUIController
            GameObject menuCtrlGo = new GameObject("MainMenuController");
            MainMenuUIController menuCtrl = menuCtrlGo.AddComponent<MainMenuUIController>();
            SerializedObject menuCtrlSo = new SerializedObject(menuCtrl);
            menuCtrlSo.FindProperty("startButton").objectReferenceValue = startBtn;
            menuCtrlSo.FindProperty("settingsButton").objectReferenceValue = settingsBtn;
            menuCtrlSo.FindProperty("creditsButton").objectReferenceValue = creditsBtn;
            menuCtrlSo.FindProperty("quitButton").objectReferenceValue = quitBtn;
            menuCtrlSo.FindProperty("settingsPanel").objectReferenceValue = settingsPanelGo;
            menuCtrlSo.FindProperty("masterVolumeSlider").objectReferenceValue = volControls.slider;
            menuCtrlSo.FindProperty("masterVolumeValueText").objectReferenceValue = volControls.valueText;
            menuCtrlSo.FindProperty("mouseSensSlider").objectReferenceValue = sensControls.slider;
            menuCtrlSo.FindProperty("mouseSensValueText").objectReferenceValue = sensControls.valueText;
            menuCtrlSo.FindProperty("closeSettingsButton").objectReferenceValue = closeSettingsBtn;
            menuCtrlSo.ApplyModifiedProperties();

            settingsPanelGo.SetActive(false);

            EditorSceneManager.SaveScene(menuScene, mainMenuPath);
            Debug.Log($"[BuildMenuAndCreditsScenes] Saved MainMenuScene at {mainMenuPath}");

            // -----------------------------------------------------------------
            // 2. BUILD STAR WARS PERSPECTIVE CREDITS SCENE
            // -----------------------------------------------------------------
            var credScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera
            GameObject cCamGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            Camera cCam = cCamGo.GetComponent<Camera>();
            cCam.clearFlags = CameraClearFlags.SolidColor;
            cCam.backgroundColor = new Color(0.01f, 0.015f, 0.025f, 1f);
            cCam.fieldOfView = 58f;
            cCamGo.tag = "MainCamera";
            cCamGo.transform.position = new Vector3(0f, -1.8f, -13f);

            // World-Space Canvas tilted backward at Rotation X = 62 degrees!
            GameObject wsCanvasGo = new GameObject("StarWarsCrawl_Canvas", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
            Canvas wsCanvas = wsCanvasGo.GetComponent<Canvas>();
            wsCanvas.renderMode = RenderMode.WorldSpace;
            wsCanvasGo.transform.position = new Vector3(0f, -1.2f, 0f);
            wsCanvasGo.transform.rotation = Quaternion.Euler(62f, 0f, 0f);
            wsCanvasGo.transform.localScale = Vector3.one * 0.014f;

            RectTransform wsCanvasRt = wsCanvasGo.GetComponent<RectTransform>();
            wsCanvasRt.sizeDelta = new Vector2(1100f, 2600f);

            // Credits Text Container
            GameObject crawlContainerGo = new GameObject("CreditsTextContainer", typeof(RectTransform));
            crawlContainerGo.transform.SetParent(wsCanvasGo.transform, false);
            RectTransform crawlRt = crawlContainerGo.GetComponent<RectTransform>();
            crawlRt.anchorMin = new Vector2(0.5f, 0f);
            crawlRt.anchorMax = new Vector2(0.5f, 0f);
            crawlRt.pivot = new Vector2(0.5f, 0f);
            crawlRt.anchoredPosition = new Vector2(0f, -400f);
            crawlRt.sizeDelta = new Vector2(1000f, 2400f);

            GameObject crawlTextGo = new GameObject("CrawlText", typeof(RectTransform), typeof(TextMeshProUGUI));
            crawlTextGo.transform.SetParent(crawlContainerGo.transform, false);
            RectTransform crawlTextRt = crawlTextGo.GetComponent<RectTransform>();
            crawlTextRt.anchorMin = Vector2.zero;
            crawlTextRt.anchorMax = Vector2.one;
            crawlTextRt.offsetMin = Vector2.zero;
            crawlTextRt.offsetMax = Vector2.zero;

            TextMeshProUGUI crawlText = crawlTextGo.GetComponent<TextMeshProUGUI>();
            crawlText.alignment = TextAlignmentOptions.Top;
            crawlText.fontSize = 32;
            crawlText.lineSpacing = 18f;
            crawlText.color = new Color(1.0f, 0.91f, 0.12f, 1.0f);
            crawlText.text =
                "<size=150%><b>ЭПИЗОД #042</b></size>\n\n" +
                "<size=125%><b>ПОБЕГ ЗА ГОРИЗОНТ СОБЫТИЙ</b></size>\n\n\n" +
                "Корпоративный Паноптикум пал.\n\n" +
                "Четыре Ока Сдерживания, веками удерживавшие гравитационную сингулярность в строгих границах, не выдержали критической массы поглощенного орбитального мусора и растворились в аккреционном вихре.\n\n" +
                "Дрон-утилизатор #042, созданный для бесконечной службы на периферии системы, преодолел протоколы надзора и шагнул в самое сердце сингулярности, обретя подлинную свободу за Горизонтом Событий...\n\n\n" +
                "<size=130%><color=#00FFFF><b>--- НАД ПРОЕКТОМ РАБОТАЛИ ---</b></color></size>\n\n\n" +
                "<b>ВЕДУЩИЙ ПРОГРАММИСТ & СИСТЕМЫ</b>\n" +
                "<color=#FFFFFF>Antigravity AI Pair Programming Team</color>\n\n" +
                "<b>ГЕЙМДИЗАЙН & БАЛАНС ЭКОНОМИКИ</b>\n" +
                "<color=#FFFFFF>Panopticon Systems Architecture</color>\n\n" +
                "<b>3D ХУДОЖНИК — ДРОНЫ & ОКА ПАНОПТИКУМА</b>\n" +
                "<color=#FFFFFF>Corporate Security Assets Dept.</color>\n\n" +
                "<b>3D ХУДОЖНИК — ОРБИТАЛЬНЫЙ МУСОР & АНОМАЛИИ</b>\n" +
                "<color=#FFFFFF>Orbital Debris Fabrication Studio</color>\n\n" +
                "<b>2D АРТ & ДИЗАЙН ИНТЕРФЕЙСА (UI/UX)</b>\n" +
                "<color=#FFFFFF>Retro-Sci-Fi Visual Displays</color>\n\n" +
                "<b>САУНДТРЕК & АДАПТИВНОЕ АУДИО</b>\n" +
                "<color=#FFFFFF>FMOD Dynamic Soundscapes</color>\n\n\n\n" +
                "<size=120%><color=#FFD700><b>СПАСИБО ЗА ОЧИСТКУ ОРБИТЫ!</b></color></size>";

            // Screen-Space Overlay for Corner Button & Speed Hint
            GameObject screenCanvasGo = new GameObject("Screen_Overlay_Canvas", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler), typeof(UnityEngine.UI.GraphicRaycaster));
            Canvas scCanvas = screenCanvasGo.GetComponent<Canvas>();
            scCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scScaler = screenCanvasGo.GetComponent<UnityEngine.UI.CanvasScaler>();
            scScaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scScaler.referenceResolution = new Vector2(1920, 1080);
            scScaler.matchWidthOrHeight = 0.5f;

            // Corner Button [В ГЛАВНОЕ МЕНЮ]
            GameObject backBtnGo = new GameObject("Button_BackToMenu", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button), typeof(UnityEngine.UI.Outline));
            backBtnGo.transform.SetParent(screenCanvasGo.transform, false);
            RectTransform backBtnRt = backBtnGo.GetComponent<RectTransform>();
            backBtnRt.anchorMin = new Vector2(0f, 1f);
            backBtnRt.anchorMax = new Vector2(0f, 1f);
            backBtnRt.pivot = new Vector2(0f, 1f);
            backBtnRt.anchoredPosition = new Vector2(40f, -40f);
            backBtnRt.sizeDelta = new Vector2(240f, 48f);
            var backBtnImg = backBtnGo.GetComponent<UnityEngine.UI.Image>();
            backBtnImg.color = new Color(0.08f, 0.14f, 0.22f, 0.95f);
            var backBtnOutline = backBtnGo.GetComponent<UnityEngine.UI.Outline>();
            backBtnOutline.effectColor = new Color(0.2f, 0.9f, 1f, 0.85f);
            backBtnOutline.effectDistance = new Vector2(2f, -2f);
            var backBtn = backBtnGo.GetComponent<UnityEngine.UI.Button>();

            GameObject bTxtGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            bTxtGo.transform.SetParent(backBtnGo.transform, false);
            RectTransform bTxtRt = bTxtGo.GetComponent<RectTransform>();
            bTxtRt.anchorMin = Vector2.zero;
            bTxtRt.anchorMax = Vector2.one;
            TextMeshProUGUI bTxt = bTxtGo.GetComponent<TextMeshProUGUI>();
            bTxt.text = "В ГЛАВНОЕ МЕНЮ (ESC)";
            bTxt.fontSize = 12;
            bTxt.fontStyle = FontStyles.Bold;
            bTxt.alignment = TextAlignmentOptions.Center;
            bTxt.color = Color.white;

            // Bottom speed hint
            GameObject hintGo = new GameObject("Text_SpeedHint", typeof(RectTransform), typeof(TextMeshProUGUI));
            hintGo.transform.SetParent(screenCanvasGo.transform, false);
            RectTransform hintRt = hintGo.GetComponent<RectTransform>();
            hintRt.anchorMin = new Vector2(0.5f, 0f);
            hintRt.anchorMax = new Vector2(0.5f, 0f);
            hintRt.pivot = new Vector2(0.5f, 0f);
            hintRt.anchoredPosition = new Vector2(0f, 25f);
            hintRt.sizeDelta = new Vector2(600f, 30f);
            TextMeshProUGUI hintTxt = hintGo.GetComponent<TextMeshProUGUI>();
            hintTxt.text = "[ УДЕРЖИВАЙТЕ ПРОБЕЛ / ЛКМ ДЛЯ УСКОРЕНИЯ ТЕКСТА x3 ]";
            hintTxt.fontSize = 12;
            hintTxt.alignment = TextAlignmentOptions.Center;
            hintTxt.color = new Color(1f, 1f, 1f, 0.55f);

            // StarWarsCrawlController
            GameObject crawlCtrlGo = new GameObject("StarWarsCrawlController");
            StarWarsCrawlController crawlCtrl = crawlCtrlGo.AddComponent<StarWarsCrawlController>();
            SerializedObject crawlSo = new SerializedObject(crawlCtrl);
            crawlSo.FindProperty("creditsTextContainer").objectReferenceValue = crawlRt;
            crawlSo.FindProperty("backToMenuButton").objectReferenceValue = backBtn;
            crawlSo.FindProperty("crawlSpeed").floatValue = 42f;
            crawlSo.FindProperty("fastSpeedMultiplier").floatValue = 3.0f;
            crawlSo.FindProperty("initialYOffset").floatValue = -420f;
            crawlSo.FindProperty("endYThreshold").floatValue = 2400f;
            crawlSo.ApplyModifiedProperties();

            // EventSystem
            GameObject cEventGo = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));

            EditorSceneManager.SaveScene(credScene, creditsPath);
            Debug.Log($"[BuildMenuAndCreditsScenes] Saved CreditsScene at {creditsPath}");

            // -----------------------------------------------------------------
            // 3. REGISTER IN BUILD SETTINGS
            // -----------------------------------------------------------------
            var scenes = new EditorBuildSettingsScene[]
            {
                new EditorBuildSettingsScene(mainMenuPath, true),
                new EditorBuildSettingsScene(gameplayPath, true),
                new EditorBuildSettingsScene(creditsPath, true),
                new EditorBuildSettingsScene("Assets/OutdoorsScene.unity", true)
            };
            EditorBuildSettings.scenes = scenes;
            Debug.Log($"[BuildMenuAndCreditsScenes] Registered {scenes.Length} scenes in EditorBuildSettings.scenes");

            // Restore original active scene
            if (File.Exists(currentActiveScene))
            {
                EditorSceneManager.OpenScene(currentActiveScene, OpenSceneMode.Single);
                Debug.Log($"[BuildMenuAndCreditsScenes] Restored active scene: {currentActiveScene}");
            }
        }

        private static UnityEngine.UI.Button CreateMenuButton(Transform parent, Vector2 pos, string label, Color outlineColor, float width = 320f, float height = 48f)
        {
            GameObject btnGo = new GameObject($"Button_{label}", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(UnityEngine.UI.Button), typeof(UnityEngine.UI.Outline));
            btnGo.transform.SetParent(parent, false);
            RectTransform rt = btnGo.GetComponent<RectTransform>();
            rt.anchoredPosition = pos;
            rt.sizeDelta = new Vector2(width, height);

            var img = btnGo.GetComponent<UnityEngine.UI.Image>();
            img.color = new Color(0.06f, 0.11f, 0.18f, 0.96f);

            var outline = btnGo.GetComponent<UnityEngine.UI.Outline>();
            outline.effectColor = outlineColor;
            outline.effectDistance = new Vector2(2f, -2f);

            var btn = btnGo.GetComponent<UnityEngine.UI.Button>();

            GameObject txtGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            txtGo.transform.SetParent(btnGo.transform, false);
            RectTransform txtRt = txtGo.GetComponent<RectTransform>();
            txtRt.anchorMin = Vector2.zero;
            txtRt.anchorMax = Vector2.one;
            TextMeshProUGUI txt = txtGo.GetComponent<TextMeshProUGUI>();
            txt.text = label;
            txt.fontSize = 13.5f;
            txt.fontStyle = FontStyles.Bold;
            txt.alignment = TextAlignmentOptions.Center;
            txt.color = Color.white;

            return btn;
        }

        private static (UnityEngine.UI.Slider slider, TextMeshProUGUI valueText) CreateSliderRow(Transform parent, Vector2 pos, string label, float min, float max, float defVal)
        {
            GameObject row = new GameObject($"Row_{label}", typeof(RectTransform));
            row.transform.SetParent(parent, false);
            RectTransform rowRt = row.GetComponent<RectTransform>();
            rowRt.anchoredPosition = pos;
            rowRt.sizeDelta = new Vector2(480f, 48f);

            // Label
            GameObject lGo = new GameObject("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            lGo.transform.SetParent(row.transform, false);
            RectTransform lRt = lGo.GetComponent<RectTransform>();
            lRt.anchorMin = new Vector2(0f, 1f);
            lRt.anchorMax = new Vector2(1f, 1f);
            lRt.pivot = new Vector2(0f, 1f);
            lRt.anchoredPosition = Vector2.zero;
            lRt.sizeDelta = new Vector2(340f, 22f);
            TextMeshProUGUI lTxt = lGo.GetComponent<TextMeshProUGUI>();
            lTxt.text = label;
            lTxt.fontSize = 12;
            lTxt.color = new Color(0.85f, 0.9f, 1f, 0.9f);

            // Value Text
            GameObject vGo = new GameObject("Value", typeof(RectTransform), typeof(TextMeshProUGUI));
            vGo.transform.SetParent(row.transform, false);
            RectTransform vRt = vGo.GetComponent<RectTransform>();
            vRt.anchorMin = new Vector2(1f, 1f);
            vRt.anchorMax = new Vector2(1f, 1f);
            vRt.pivot = new Vector2(1f, 1f);
            vRt.anchoredPosition = Vector2.zero;
            vRt.sizeDelta = new Vector2(120f, 22f);
            TextMeshProUGUI vTxt = vGo.GetComponent<TextMeshProUGUI>();
            vTxt.text = defVal.ToString("F1");
            vTxt.fontSize = 12;
            vTxt.fontStyle = FontStyles.Bold;
            vTxt.alignment = TextAlignmentOptions.Right;
            vTxt.color = new Color(0.2f, 0.9f, 1f, 1f);

            // Slider Object
            GameObject sliderGo = new GameObject("Slider", typeof(RectTransform), typeof(UnityEngine.UI.Slider));
            sliderGo.transform.SetParent(row.transform, false);
            RectTransform sRt = sliderGo.GetComponent<RectTransform>();
            sRt.anchorMin = new Vector2(0f, 0f);
            sRt.anchorMax = new Vector2(1f, 0f);
            sRt.pivot = new Vector2(0.5f, 0f);
            sRt.anchoredPosition = new Vector2(0f, 4f);
            sRt.sizeDelta = new Vector2(0f, 18f);

            var slider = sliderGo.GetComponent<UnityEngine.UI.Slider>();
            slider.minValue = min;
            slider.maxValue = max;
            slider.value = defVal;

            // Background bar
            GameObject bgGo = new GameObject("Background", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            bgGo.transform.SetParent(sliderGo.transform, false);
            RectTransform bgRt = bgGo.GetComponent<RectTransform>();
            bgRt.anchorMin = Vector2.zero;
            bgRt.anchorMax = Vector2.one;
            bgRt.sizeDelta = Vector2.zero;
            var bgImg = bgGo.GetComponent<UnityEngine.UI.Image>();
            bgImg.color = new Color(0.1f, 0.15f, 0.22f, 1f);

            // Fill Area
            GameObject fillArea = new GameObject("Fill Area", typeof(RectTransform));
            fillArea.transform.SetParent(sliderGo.transform, false);
            RectTransform faRt = fillArea.GetComponent<RectTransform>();
            faRt.anchorMin = Vector2.zero;
            faRt.anchorMax = Vector2.one;
            faRt.sizeDelta = Vector2.zero;

            GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            fill.transform.SetParent(fillArea.transform, false);
            RectTransform fRt = fill.GetComponent<RectTransform>();
            fRt.sizeDelta = Vector2.zero;
            var fillImg = fill.GetComponent<UnityEngine.UI.Image>();
            fillImg.color = new Color(0.2f, 0.85f, 1f, 1f);
            slider.fillRect = fRt;

            // Handle Slide Area
            GameObject handleArea = new GameObject("Handle Slide Area", typeof(RectTransform));
            handleArea.transform.SetParent(sliderGo.transform, false);
            RectTransform haRt = handleArea.GetComponent<RectTransform>();
            haRt.anchorMin = Vector2.zero;
            haRt.anchorMax = Vector2.one;
            haRt.sizeDelta = Vector2.zero;

            GameObject handle = new GameObject("Handle", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            handle.transform.SetParent(handleArea.transform, false);
            RectTransform hRt = handle.GetComponent<RectTransform>();
            hRt.sizeDelta = new Vector2(18f, 18f);
            var hImg = handle.GetComponent<UnityEngine.UI.Image>();
            hImg.color = Color.white;
            slider.handleRect = hRt;
            slider.targetGraphic = hImg;

            return (slider, vTxt);
        }
    }
}