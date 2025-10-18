using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class UIThemeApplier : MonoBehaviour
{
    [Header("========== BACKGROUND ==========")]
    public Color backgroundPanelColor = new Color(0.2f, 0.1f, 0.3f, 1f);
    public GameObject menuPanel; // Panel containing all UI elements

    [Header("========== TITLE TEXT ==========")]
    public Color titleTextColor = Color.white;
    public GameObject titleTextObject; // TextMeshProUGUI for title

    [Header("========== SLIDER COLORS ==========")]
    public Color sliderNormalColor = Color.magenta;
    public Color sliderHighlightedColor = Color.white;
    public Color sliderPressedColor = Color.white;
    public Color sliderSelectedColor = Color.white;
    public Color sliderDisabledColor = Color.gray;
    public Color sliderLabelColor = Color.white;
    public Color sliderBackgroundColor = new Color(0.3f, 0.2f, 0.4f, 1f);
    public Color sliderFillColor = new Color(1f, 0.5f, 1f, 1f);

    [Header("========== TOGGLE COLORS ==========")]
    public Color toggleNormalColor = Color.magenta;
    public Color toggleHighlightedColor = Color.white;
    public Color togglePressedColor = Color.white;
    public Color toggleSelectedColor = Color.white;
    public Color toggleDisabledColor = Color.gray;
    public Color toggleLabelColor = Color.white;
    public Color toggleBackgroundColor = new Color(0.3f, 0.2f, 0.4f, 1f);

    [Header("========== BUTTON COLORS ==========")]
    public Color buttonNormalColor = Color.magenta;
    public Color buttonHighlightedColor = Color.white;
    public Color buttonPressedColor = Color.white;
    public Color buttonSelectedColor = Color.white;
    public Color buttonDisabledColor = Color.gray;
    public Color buttonTextColor = Color.white;

    [Header("========== SCROLLBAR COLORS ==========")]
    public Color scrollbarNormalColor = Color.magenta;
    public Color scrollbarHighlightedColor = Color.white;
    public Color scrollbarPressedColor = Color.white;
    public Color scrollbarSelectedColor = Color.white;
    public Color scrollbarDisabledColor = Color.gray;
    public Color scrollbarHandleColor = new Color(0.8f, 0.6f, 1f, 1f);
    public Color scrollbarBackgroundColor = new Color(0.3f, 0.2f, 0.4f, 1f);

    [Header("========== DROPDOWN COLORS ==========")]
    public Color dropdownNormalColor = Color.magenta;
    public Color dropdownHighlightedColor = Color.white;
    public Color dropdownPressedColor = Color.white;
    public Color dropdownSelectedColor = Color.white;
    public Color dropdownDisabledColor = Color.gray;
    public Color dropdownBackgroundColor = new Color(0.3f, 0.2f, 0.4f, 1f);
    public Color dropdownTextColor = Color.white;




    [ContextMenu("Apply Theme Colors")]
    public void ApplyTheme()
    {
        if (menuPanel == null)
        {
            Debug.LogError("Menu Panel is not assigned.");
            return;
        }

        // Background Panel
        Image panelImage = menuPanel.GetComponent<Image>();
        if (panelImage != null)
            panelImage.color = backgroundPanelColor;

        // Title Text
        if (titleTextObject != null)
        {
            TextMeshProUGUI titleTMP = titleTextObject.GetComponent<TextMeshProUGUI>();
            if (titleTMP != null)
                titleTMP.color = titleTextColor;
        }

        // Sliders
        foreach (Slider slider in menuPanel.GetComponentsInChildren<Slider>(true))
        {
            var colors = slider.colors;
            colors.normalColor = sliderNormalColor;
            colors.highlightedColor = sliderHighlightedColor;
            colors.pressedColor = sliderPressedColor;
            colors.selectedColor = sliderSelectedColor;
            colors.disabledColor = sliderDisabledColor;
            slider.colors = colors;

            // Slider Label (Text)
            TextMeshProUGUI label = slider.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
                label.color = sliderLabelColor;

            // Background
            var bgImage = slider.transform.Find("Background")?.GetComponent<Image>();
            if (bgImage != null)
                bgImage.color = sliderBackgroundColor;

            // Fill
            var fillImage = slider.transform.Find("Fill Area/Fill")?.GetComponent<Image>();
            if (fillImage != null)
                fillImage.color = sliderFillColor;
        }

        // Toggles
        foreach (Toggle toggle in menuPanel.GetComponentsInChildren<Toggle>(true))
        {
            var colors = toggle.colors;
            colors.normalColor = toggleNormalColor;
            colors.highlightedColor = toggleHighlightedColor;
            colors.pressedColor = togglePressedColor;
            colors.selectedColor = toggleSelectedColor;
            colors.disabledColor = toggleDisabledColor;
            toggle.colors = colors;

            // Label
            TextMeshProUGUI label = toggle.GetComponentInChildren<TextMeshProUGUI>();
            if (label != null)
                label.color = toggleLabelColor;

            // Background
            var bgImage = toggle.GetComponentInChildren<Image>();
            if (bgImage != null)
                bgImage.color = toggleBackgroundColor;
        }

        // Buttons
        foreach (Button button in menuPanel.GetComponentsInChildren<Button>(true))
        {
            var colors = button.colors;
            colors.normalColor = buttonNormalColor;
            colors.highlightedColor = buttonHighlightedColor;
            colors.pressedColor = buttonPressedColor;
            colors.selectedColor = buttonSelectedColor;
            colors.disabledColor = buttonDisabledColor;
            button.colors = colors;

            // Text
            TextMeshProUGUI text = button.GetComponentInChildren<TextMeshProUGUI>();
            if (text != null)
                text.color = buttonTextColor;
        }

        // Scrollbars
        foreach (Scrollbar scrollbar in menuPanel.GetComponentsInChildren<Scrollbar>(true))
        {
            var colors = scrollbar.colors;
            colors.normalColor = scrollbarNormalColor;
            colors.highlightedColor = scrollbarHighlightedColor;
            colors.pressedColor = scrollbarPressedColor;
            colors.selectedColor = scrollbarSelectedColor;
            colors.disabledColor = scrollbarDisabledColor;
            scrollbar.colors = colors;

            // Handle
            var handle = scrollbar.transform.Find("Sliding Area/Handle")?.GetComponent<Image>();
            if (handle != null)
                handle.color = scrollbarHandleColor;

            // Background
            var background = scrollbar.GetComponent<Image>();
            if (background != null)
                background.color = scrollbarBackgroundColor;
        }

        // TMP Dropdowns
        foreach (TMP_Dropdown dropdown in menuPanel.GetComponentsInChildren<TMP_Dropdown>(true))
        {
            var colors = dropdown.colors;
            colors.normalColor = dropdownNormalColor;
            colors.highlightedColor = dropdownHighlightedColor;
            colors.pressedColor = dropdownPressedColor;
            colors.selectedColor = dropdownSelectedColor;
            colors.disabledColor = dropdownDisabledColor;
            dropdown.colors = colors;

            // Label text
            var label = dropdown.transform.Find("Label")?.GetComponent<TextMeshProUGUI>();
            if (label != null)
                label.color = dropdownTextColor;

            // Background
            var background = dropdown.GetComponent<Image>();
            if (background != null)
                background.color = dropdownBackgroundColor;

            // Arrow (optional)
            var arrow = dropdown.transform.Find("Arrow")?.GetComponent<Image>();
            if (arrow != null)
                arrow.color = dropdownTextColor;
        }



        Debug.Log("✔ All UI theme colors applied!");
    }
}
