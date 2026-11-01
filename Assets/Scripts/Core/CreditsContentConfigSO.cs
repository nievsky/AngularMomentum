using System;
using UnityEngine;

namespace OrbitalPanopticon.Core
{
    /// <summary>
    /// Centralized ScriptableObject for Russian and English Credits Crawl text and scroll tuning.
    /// Provides clear Inspector TextAreas to easily paste and edit credits text in RU/EN.
    /// </summary>
    [CreateAssetMenu(fileName = "CreditsContentConfig", menuName = "OrbitalPanopticon/Config/Credits Content Config")]
    public class CreditsContentConfigSO : ScriptableObject
    {
        [Header("Russian Credits Crawl Text (RU)")]
        [TextArea(18, 40)]
        public string creditsTextRU =
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

        [Header("English Credits Crawl Text (EN)")]
        [TextArea(18, 40)]
        public string creditsTextEN =
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

        [Header("Scroll Tuning")]
        public float scrollSpeed = 28f;
        public float fastForwardMultiplier = 3.5f;
    }
}
