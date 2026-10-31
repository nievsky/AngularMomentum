using System;
using System.Collections.Generic;
using UnityEngine;

namespace OrbitalPanopticon.Core
{
    public enum GameLanguage
    {
        RU = 0,
        EN = 1
    }

    /// <summary>
    /// Centralized localization manager supporting Russian (default) and English.
    /// Manages language state, persistence via PlayerPrefs, event broadcasting,
    /// and provides an extensible dictionary lookup for all in-game text.
    /// </summary>
    [DisallowMultipleComponent]
    public class LocalizationManager : MonoBehaviour
    {
        public static LocalizationManager Instance { get; private set; }

        public const string PREFS_KEY = "SelectedLanguage";
        private static GameLanguage _currentLanguage = GameLanguage.RU;
        private static bool _isInitialized = false;

        public static event Action<GameLanguage> OnLanguageChanged;

        public static GameLanguage CurrentLanguage
        {
            get
            {
                if (!_isInitialized)
                {
                    InitializeLanguage();
                }
                return _currentLanguage;
            }
        }

        public static bool IsRussian => CurrentLanguage == GameLanguage.RU;

        private static readonly Dictionary<string, (string ru, string en)> _translations = new Dictionary<string, (string ru, string en)>
        {
            // Main Menu
            { "MM_Title", ("ORBITAL PANOPTICON", "ORBITAL PANOPTICON") },
            { "MM_Subtitle", ("СЕКТОР НАДЗОРА #042 // ОРБИТАЛЬНЫЙ ПАНОПТИКУМ", "SURVEILLANCE SECTOR #042 // ORBITAL PANOPTICON") },
            { "MM_Start", ("НАЧАТЬ ИГРУ", "START GAME") },
            { "MM_Settings", ("НАСТРОЙКИ", "SETTINGS") },
            { "MM_Credits", ("АВТОРЫ / ТИТРЫ", "CREDITS") },
            { "MM_Quit", ("ВЫЙТИ ИЗ ИГРЫ", "QUIT GAME") },
            { "MM_Settings_Title", ("НАСТРОЙКИ СИСТЕМЫ", "SYSTEM SETTINGS") },
            { "MM_Settings_Volume", ("Громкость звука (Master Volume):", "Master Volume:") },
            { "MM_Settings_MusicVolume", ("Громкость музыки (Music):", "Music Volume:") },
            { "MM_Settings_SoundsVolume", ("Громкость звуков (Sounds):", "Sound Effects (SFX):") },
            { "MM_Settings_Sens", ("Чувствительность мыши (Sens):", "Mouse Sensitivity:") },
            { "MM_Settings_Close", ("ЗАКРЫТЬ", "CLOSE") },

            // HUD Core Minimalist
            { "HUD_ScrapCoins", ("ЛОМ-МОНЕТЫ: {0:N0}", "SCRAP COINS: {0:N0}") },
            { "HUD_MassBar", ("МАССА ДЫРЫ: {0:N0} / {1:N0} ТОНН", "BLACK HOLE MASS: {0:N0} / {1:N0} TONS") },
            { "HUD_Stage", ("СТАДИЯ {0} / 4", "STAGE {0} / 4") },
            { "HUD_Stage4_Critical", ("СТАДИЯ 4 / 4 — КРИТИЧЕСКАЯ МАССА", "STAGE 4 / 4 — CRITICAL MASS") },

            // Transient In-Game Notifications
            { "HUD_StartPrompt", ("[ НАЖМИТЕ ЛКМ ДЛЯ ПОДКЛЮЧЕНИЯ К ДРОНУ #042 ]", "[ CLICK LMB TO CONNECT TO DRONE #042 ]") },
            { "HUD_BoundaryWarning", ("ВНИМАНИЕ: ВЫХОД ЗА ПРЕДЕЛЫ СЕКТОРА\nПОТЕРЯ СИГНАЛА ЧЕРЕЗ {0:F1}с", "WARNING: BOUNDARY BREACH DETECTED\nSIGNAL LOSS IN {0:F1}s") },
            { "HUD_BeamWarning", ("ВНИМАНИЕ: ПОВРЕЖДЕНИЕ ОТ ЛУЧА СДЕРЖИВАНИЯ! [{0}/3]", "WARNING: CONTAINMENT BEAM DAMAGE! [{0}/3]") },
            { "HUD_SignalLoss_Beam", ("КРИТИЧЕСКАЯ ПЕРЕГРУЗКА ЛУЧОМ — ДРОН УНИЧТОЖЕН", "CRITICAL BEAM OVERLOAD — DRONE DESTROYED") },
            { "HUD_SignalLoss_Boundary", ("ПОТЕРЯ СИГНАЛА — ВЫХОД ЗА ЗОНУ НАДЗОРА", "SIGNAL TIMEOUT — OUT OF BOUNDS") },
            { "HUD_SignalLoss_Hole", ("СВЕРХКРИТИЧЕСКАЯ МАССА — ДРОН ПОГЛОЩЕН", "CRITICAL MASS — DRONE CONSUMED") },
            { "HUD_ObservedNotice", ("ЗАХВАЧЕНА ОСОБАЯ АНОМАЛИЯ — ЦЕННОСТЬ x3!", "VALUABLE ANOMALY GRABBED — 3x VALUE!") },
            { "HUD_QuotaCelebration", ("КВОТА #{0} ВЫПОЛНЕНА!\n<size=80%>+{1} ЛОМ-МОНЕТ  <color=#FFD700>(БОНУС СКОРОСТИ +{2}%)</color></size>", "QUOTA #{0} COMPLETED!\n<size=80%>+{1} SCRAP COINS  <color=#FFD700>(SPEED BONUS +{2}%)</color></size>") },

            // Upgrade Tree
            { "TREE_Header", ("ДЕРЕВО УЛУЧШЕНИЙ СИСТЕМЫ", "SYSTEM UPGRADE TREE") },
            { "TREE_Balance", ("Баланс: <color=#FFE640>{0}</color> Лом-монет", "Balance: <color=#FFE640>{0}</color> Scrap Coins") },
            { "TREE_ResetCam", ("[СБРОСИТЬ КАМЕРУ]", "[RESET VIEW]") },
            { "TREE_BuyRobot", ("<b>Купить робота</b>\n<size=10><color={0}>{1}©</color> (В строю: {2})</size>", "<b>Buy Helper Bot</b>\n<size=10><color={0}>{1}©</color> (Active: {2})</size>") },
            { "TREE_Active", ("[АКТИВНО]", "[ACTIVE]") },
            { "TREE_MaxLevel", ("[МАКС. УР.]", "[MAX LEVEL]") },
            { "TREE_Locked", ("[ЗАКРЫТО]", "[LOCKED]") },

            // Credits Scene
            { "CREDITS_BackBtn", ("В ГЛАВНОЕ МЕНЮ (ESC)", "RETURN TO MENU (ESC)") },
            { "CREDITS_SpeedHint", ("[ УДЕРЖИВАЙТЕ ПРОБЕЛ / ЛКМ ДЛЯ УСКОРЕНИЯ ТЕКСТА x3 ]", "[ HOLD SPACE / LMB TO SPEED UP TEXT x3 ]") },
            { "CREDITS_Thanks", ("СПАСИБО ЗА ОЧИСТКУ ОРБИТЫ!", "THANK YOU FOR CLEARING THE ORBIT!") },

            // In-Game ESC Pause Menu
            { "PAUSE_Title", ("ПАУЗА СИСТЕМЫ", "SYSTEM PAUSE") },
            { "PAUSE_Resume", ("ПРОДОЛЖИТЬ", "RESUME") },
            { "PAUSE_Settings", ("НАСТРОЙКИ", "SETTINGS") },
            { "PAUSE_MainMenu", ("В ГЛАВНОЕ МЕНЮ", "MAIN MENU") },
            { "PAUSE_Quit", ("ВЫЙТИ ИЗ ИГРЫ", "QUIT GAME") }
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void AutoInitialize()
        {
            InitializeLanguage();
            if (Instance == null)
            {
                var existing = FindAnyObjectByType<LocalizationManager>();
                if (existing != null)
                {
                    Instance = existing;
                }
                else
                {
                    GameObject go = new GameObject("[LOCALIZATION_MANAGER]");
                    Instance = go.AddComponent<LocalizationManager>();
                    DontDestroyOnLoad(go);
                }
            }
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            transform.SetParent(null);
            DontDestroyOnLoad(gameObject);
            InitializeLanguage();
        }

        private static void InitializeLanguage()
        {
            if (_isInitialized) return;
            string saved = PlayerPrefs.GetString(PREFS_KEY, "RU");
            if (Enum.TryParse<GameLanguage>(saved, out var parsed))
            {
                _currentLanguage = parsed;
            }
            else
            {
                _currentLanguage = GameLanguage.RU;
            }
            _isInitialized = true;
        }

        /// <summary>
        /// Switches the active project language, persists to PlayerPrefs, and broadcasts OnLanguageChanged.
        /// </summary>
        public static void SetLanguage(GameLanguage lang)
        {
            _currentLanguage = lang;
            _isInitialized = true;
            PlayerPrefs.SetString(PREFS_KEY, lang.ToString());
            PlayerPrefs.Save();
            Debug.Log($"<color=#00FFAA>[LOCALIZATION] Language switched to: {lang}</color>");
            OnLanguageChanged?.Invoke(lang);
        }

        /// <summary>
        /// Retrieves the localized string for a given key, with optional string.Format arguments.
        /// </summary>
        public static string Get(string key, params object[] args)
        {
            if (_translations.TryGetValue(key, out var pair))
            {
                string template = IsRussian ? pair.ru : pair.en;
                if (args != null && args.Length > 0)
                {
                    try
                    {
                        return string.Format(template, args);
                    }
                    catch
                    {
                        return template;
                    }
                }
                return template;
            }
            return key;
        }
    }
}
