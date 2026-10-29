using System;
using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using UnityEngine.ResourceManagement.AsyncOperations;

public static class GameLocalization
{
    public const string TableName = "Table1";
    public const string PreferredLocalePrefsKey = "Game.Locale";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void ApplySavedLocale()
    {
        if (!PlayerPrefs.HasKey(PreferredLocalePrefsKey))
            return;

        SetLocale(PlayerPrefs.GetString(PreferredLocalePrefsKey), false);
    }

    public static string CurrentLocaleCode
    {
        get
        {
            var selectedLocaleOperation = LocalizationSettings.SelectedLocaleAsync;
            if (!selectedLocaleOperation.IsDone)
                return string.Empty;

            Locale locale = selectedLocaleOperation.Result;
            return locale != null ? locale.Identifier.Code : string.Empty;
        }
    }

    public static bool SetLocale(string localeCode, bool savePreference = true)
    {
        if (string.IsNullOrWhiteSpace(localeCode))
        {
            Debug.LogWarning("Localization: locale code is empty.");
            return false;
        }

        if (!LocalizationSettings.InitializationOperation.IsDone)
        {
            var initializationOperation = LocalizationSettings.InitializationOperation;
            initializationOperation.Completed += _ => SetLocale(localeCode, savePreference);
            return true;
        }

        Locale locale = LocalizationSettings.AvailableLocales.GetLocale(localeCode);
        if (locale == null)
        {
            Debug.LogWarning($"Localization: locale '{localeCode}' is not configured.");
            return false;
        }

        LocalizationSettings.SelectedLocale = locale;

        if (savePreference)
        {
            PlayerPrefs.SetString(PreferredLocalePrefsKey, locale.Identifier.Code);
            PlayerPrefs.Save();
        }

        return true;
    }

    public static void GetStringAsync(string key, string fallback, Action<string> completed)
    {
        if (completed == null)
            return;

        if (string.IsNullOrEmpty(key))
        {
            completed(fallback ?? string.Empty);
            return;
        }

        AsyncOperationHandle<string> stringOperation =
            LocalizationSettings.StringDatabase.GetLocalizedStringAsync(TableName, key);

        if (stringOperation.IsDone)
        {
            CompleteStringOperation(key, fallback, completed, stringOperation);
        }
        else
        {
            stringOperation.Completed += operation =>
                CompleteStringOperation(key, fallback, completed, operation);
        }
    }

    public static string GetString(string key, string fallback = null)
    {
        if (string.IsNullOrEmpty(key))
            return fallback ?? string.Empty;

        string localized = LocalizationSettings.StringDatabase.GetLocalizedString(TableName, key);
        if (IsMissingTranslation(localized))
            return fallback ?? key;

        return localized;
    }

    private static void CompleteStringOperation(
        string key,
        string fallback,
        Action<string> completed,
        AsyncOperationHandle<string> operation)
    {
        string localized = operation.Status == AsyncOperationStatus.Succeeded ? operation.Result : null;
        completed(IsMissingTranslation(localized) ? fallback ?? key : localized);
    }

    private static bool IsMissingTranslation(string localized)
    {
        return string.IsNullOrEmpty(localized) ||
               localized.StartsWith("No translation found for '", StringComparison.Ordinal);
    }
}
