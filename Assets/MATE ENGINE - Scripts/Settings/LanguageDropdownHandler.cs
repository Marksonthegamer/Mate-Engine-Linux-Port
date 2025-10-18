using UnityEngine;
using TMPro;
using UnityEngine.Localization.Settings;
using System.Collections.Generic;

public class LanguageDropdownHandler : MonoBehaviour
{
    [Tooltip("Add all TMP_Dropdowns that should reflect the selected language")]
    [SerializeField] private List<TMP_Dropdown> languageDropdowns = new List<TMP_Dropdown>();

    private bool isInitializing = true;

    private void Start()
    {
        var locales = LocalizationSettings.AvailableLocales.Locales;
        string savedCode = SaveLoadHandler.Instance.data.selectedLocaleCode;
        int index = locales.FindIndex(locale => locale.Identifier.Code == savedCode);
        if (index < 0) index = 0;

        // Set value without triggering
        foreach (var dropdown in languageDropdowns)
        {
            if (dropdown != null)
            {
                dropdown.SetValueWithoutNotify(index);
                dropdown.onValueChanged.AddListener(OnLanguageChanged);
            }
        }

        LocalizationSettings.SelectedLocale = locales[index];
        isInitializing = false;
    }

    private void OnLanguageChanged(int index)
    {
        if (isInitializing) return;

        var locales = LocalizationSettings.AvailableLocales.Locales;
        if (index < 0 || index >= locales.Count) return;

        var selected = locales[index];
        LocalizationSettings.SelectedLocale = selected;

        // Sync all dropdowns
        foreach (var dropdown in languageDropdowns)
        {
            if (dropdown != null && dropdown.value != index)
            {
                dropdown.SetValueWithoutNotify(index);
            }
        }

        SaveLoadHandler.Instance.data.selectedLocaleCode = selected.Identifier.Code;
        SaveLoadHandler.Instance.SaveToDisk();
    }
}
