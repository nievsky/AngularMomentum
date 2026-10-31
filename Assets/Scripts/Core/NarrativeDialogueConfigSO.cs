using System;
using UnityEngine;

namespace OrbitalPanopticon.Core
{
    [Serializable]
    public class DialogueEntry
    {
        public string speakerName = "КОРПОРАТИВНЫЙ КУРАТОР";
        public Sprite speakerPortraitPlaceholder;
        public Color themeColor = new Color(0.2f, 0.9f, 1f, 1f);
        [TextArea(2, 5)] public string dialogueTextRU = "";
        public float displayDuration = 5.5f;
        public float typewriterCharDelay = 0.03f;
    }

    [CreateAssetMenu(fileName = "NarrativeDialogueConfig", menuName = "Orbital Panopticon/Narrative Dialogue Config")]
    public class NarrativeDialogueConfigSO : ScriptableObject
    {
        [Header("Stage Transmissions (Russian Localization)")]
        [Tooltip("Played when operator connects / Stage 1 begins.")]
        public DialogueEntry stage1Start = new DialogueEntry
        {
            speakerName = "КОРПОРАТИВНЫЙ КУРАТОР",
            themeColor = new Color(0.2f, 0.9f, 1.0f, 1f),
            dialogueTextRU = "Оператор Дрона #042, приступайте к штатной уборке сектора. Четыре Ока Паноптикума удерживают сингулярность в безопасных пределах.",
            displayDuration = 6.0f,
            typewriterCharDelay = 0.03f
        };

        [Tooltip("Played when Stage 2 is reached (1st Containment Beam disabled).")]
        public DialogueEntry stage2FirstBeamOffline = new DialogueEntry
        {
            speakerName = "КОРПОРАТИВНЫЙ КУРАТОР",
            themeColor = new Color(1.0f, 0.85f, 0.2f, 1f),
            dialogueTextRU = "Дрон #042, вы превысили лимит подачи материи! Сдерживающий луч №1 перегорел. Снизьте темп утилизации!",
            displayDuration = 5.5f,
            typewriterCharDelay = 0.03f
        };

        [Tooltip("Played when Stage 3 is reached (2nd Containment Beam disabled).")]
        public DialogueEntry stage3SecondBeamOffline = new DialogueEntry
        {
            speakerName = "СИСТЕМА БЕЗОПАСНОСТИ",
            themeColor = new Color(1.0f, 0.55f, 0.15f, 1f),
            dialogueTextRU = "ТРЕВОГА: Отключен второй контур сдерживания! Зафиксирован несанкционированный взлом протоколов прокачки дрона. Оператор, ответьте!",
            displayDuration = 5.5f,
            typewriterCharDelay = 0.03f
        };

        [Tooltip("Played when Stage 4 is reached (3rd Containment Beam disabled).")]
        public DialogueEntry stage4ThirdBeamOffline = new DialogueEntry
        {
            speakerName = "ДИРЕКТОРАТ СЕКТОРА",
            themeColor = new Color(1.0f, 0.25f, 0.25f, 1f),
            dialogueTextRU = "Вы что творите?! Остался последний луч! Если он погаснет, дыра поглотит весь надзорный сектор! Отключить его немедленно!.. ОШИБКА ДОСТУПА.",
            displayDuration = 6.0f,
            typewriterCharDelay = 0.03f
        };

        [Tooltip("Played during Finale when all 4 Beams are offline.")]
        public DialogueEntry finaleAllBeamsOffline = new DialogueEntry
        {
            speakerName = "СИСТЕМНЫЙ СБОЙ // OVERRIDE",
            themeColor = new Color(0.95f, 0.2f, 0.95f, 1f),
            dialogueTextRU = "ВСЕ ОГРАНИЧИТЕЛИ СНЯТЫ. ПАНОПТИКУМ ОСЛЕПЛЕН. ДОБРО ПОЖАЛОВАТЬ ЗА ГОРИЗОНТ СОБЫТИЙ, ДРОН #042.",
            displayDuration = 7.0f,
            typewriterCharDelay = 0.032f
        };
    }
}
