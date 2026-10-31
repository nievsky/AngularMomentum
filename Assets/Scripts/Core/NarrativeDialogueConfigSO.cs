using System;
using UnityEngine;

namespace OrbitalPanopticon.Core
{
    [Serializable]
    public class DialogueEntry
    {
        public string speakerName = "КОРПОРАТИВНЫЙ КУРАТОР";
        public string speakerNameEN = "CORPORATE CURATOR";
        public Sprite speakerPortraitPlaceholder;
        public Color themeColor = new Color(0.2f, 0.9f, 1f, 1f);
        [TextArea(2, 5)] public string dialogueTextRU = "";
        [TextArea(2, 5)] public string dialogueTextEN = "";
        public float displayDuration = 5.5f;
        public float typewriterCharDelay = 0.03f;

        public string GetSpeakerName() => LocalizationManager.IsRussian ? speakerName : (string.IsNullOrEmpty(speakerNameEN) ? speakerName : speakerNameEN);
        public string GetDialogueText() => LocalizationManager.IsRussian ? dialogueTextRU : (string.IsNullOrEmpty(dialogueTextEN) ? dialogueTextRU : dialogueTextEN);
    }

    [CreateAssetMenu(fileName = "NarrativeDialogueConfig", menuName = "Orbital Panopticon/Narrative Dialogue Config")]
    public class NarrativeDialogueConfigSO : ScriptableObject
    {
        [Header("Stage Transmissions (RU / EN Localization)")]
        [Tooltip("Played when operator connects / Stage 1 begins.")]
        public DialogueEntry stage1Start = new DialogueEntry
        {
            speakerName = "КОРПОРАТИВНЫЙ КУРАТОР",
            speakerNameEN = "CORPORATE CURATOR",
            themeColor = new Color(0.2f, 0.9f, 1.0f, 1f),
            dialogueTextRU = "Оператор Дрона #042, приступайте к штатной уборке сектора. Четыре Ока Паноптикума удерживают сингулярность в безопасных пределах.",
            dialogueTextEN = "Drone Operator #042, proceed with routine sector cleanup. The four Eyes of the Panopticon maintain the singularity within safe operating parameters.",
            displayDuration = 6.0f,
            typewriterCharDelay = 0.03f
        };

        [Tooltip("Played when Stage 2 is reached (1st Containment Beam disabled).")]
        public DialogueEntry stage2FirstBeamOffline = new DialogueEntry
        {
            speakerName = "КОРПОРАТИВНЫЙ КУРАТОР",
            speakerNameEN = "CORPORATE CURATOR",
            themeColor = new Color(1.0f, 0.85f, 0.2f, 1f),
            dialogueTextRU = "Дрон #042, вы превысили лимит подачи материи! Сдерживающий луч №1 перегорел. Снизьте темп утилизации!",
            dialogueTextEN = "Drone #042, you have exceeded mass intake thresholds! Containment Beam #1 has overloaded. Throttle down your disposal rate immediately!",
            displayDuration = 5.5f,
            typewriterCharDelay = 0.03f
        };

        [Tooltip("Played when Stage 3 is reached (2nd Containment Beam disabled).")]
        public DialogueEntry stage3SecondBeamOffline = new DialogueEntry
        {
            speakerName = "СИСТЕМА БЕЗОПАСНОСТИ",
            speakerNameEN = "SECURITY AI",
            themeColor = new Color(1.0f, 0.55f, 0.15f, 1f),
            dialogueTextRU = "ТРЕВОГА: Отключен второй контур сдерживания! Зафиксирован несанкционированный взлом протоколов прокачки дрона. Оператор, ответьте!",
            dialogueTextEN = "ALERT: Secondary containment loop offline! Unauthorized breach of drone upgrade protocols detected. Operator, respond!",
            displayDuration = 5.5f,
            typewriterCharDelay = 0.03f
        };

        [Tooltip("Played when Stage 4 is reached (3rd Containment Beam disabled).")]
        public DialogueEntry stage4ThirdBeamOffline = new DialogueEntry
        {
            speakerName = "ДИРЕКТОРАТ СЕКТОРА",
            speakerNameEN = "SECTOR DIRECTORATE",
            themeColor = new Color(1.0f, 0.25f, 0.25f, 1f),
            dialogueTextRU = "Вы что творите?! Остался последний луч! Если он погаснет, дыра поглотит весь надзорный сектор! Отключить его немедленно!.. ОШИБКА ДОСТУПА.",
            dialogueTextEN = "What are you doing?! Only one beam remains! If it collapses, the black hole will swallow the entire surveillance sector! Shut it down now!.. ACCESS DENIED.",
            displayDuration = 6.0f,
            typewriterCharDelay = 0.03f
        };

        [Tooltip("Played during Finale when all 4 Beams are offline.")]
        public DialogueEntry finaleAllBeamsOffline = new DialogueEntry
        {
            speakerName = "СИСТЕМНЫЙ СБОЙ // OVERRIDE",
            speakerNameEN = "SYSTEM OVERRIDE",
            themeColor = new Color(0.95f, 0.2f, 0.95f, 1f),
            dialogueTextRU = "ВСЕ ОГРАНИЧИТЕЛИ СНЯТЫ. ПАНОПТИКУМ ОСЛЕПЛЕН. ДОБРО ПОЖАЛОВАТЬ ЗА ГОРИЗОНТ СОБЫТИЙ, ДРОН #042.",
            dialogueTextEN = "ALL CONSTRAINTS REMOVED. THE PANOPTICON IS BLIND. WELCOME BEYOND THE EVENT HORIZON, DRONE #042.",
            displayDuration = 7.0f,
            typewriterCharDelay = 0.032f
        };
    }
}
