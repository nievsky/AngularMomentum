using UnityEngine;

public class LanguageSelector : MonoBehaviour
{
    [SerializeField] private string _englishLocaleCode = "en";
    [SerializeField] private string _russianLocaleCode = "ru";

    public void SetEnglish()
    {
        GameLocalization.SetLocale(_englishLocaleCode);
    }

    public void SetRussian()
    {
        GameLocalization.SetLocale(_russianLocaleCode);
    }

    public void ToggleLanguage()
    {
        string currentLocale = GameLocalization.CurrentLocaleCode;
        string nextLocale = currentLocale == _russianLocaleCode ? _englishLocaleCode : _russianLocaleCode;
        GameLocalization.SetLocale(nextLocale);
    }

    public void SetLanguage(string localeCode)
    {
        GameLocalization.SetLocale(localeCode);
    }
}
