using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;
using UnityEngine.Localization;

public class TutorialMenu : MonoBehaviour
{
    [Header("Main UI Inputs")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI infoText;

    public Button skipButton;
    public Button nextButton;
    public Button backButton;
    public Button finishButton;

    [Tooltip("Shared language dropdown shown only if the current step requires it")]
    public TMP_Dropdown languageDropdown;

    [Tooltip("Optional: Root GameObject for the tutorial UI (disabled in editor, enabled at runtime)")]
    public GameObject tutorialRoot;

    [Header("GameObjects to hide while tutorial is active")]
    public List<GameObject> hideWhileTutorialActive = new();

    [Header("Tutorial Steps")]
    public List<TutorialStep> steps = new List<TutorialStep>();

    public static bool IsActive { get; private set; }

    private int currentStep = 0;
    private LocalizedString _lastTitleKey, _lastInfoKey;

    [System.Serializable]
    public class TutorialStep
    {
        [Tooltip("Localization key for the title (e.g. TUTORIAL_PAGE_1_TITLE)")]
        public LocalizedString titleKey;

        [Tooltip("Localization key for the info text (e.g. TUTORIAL_PAGE_1_INFO)")]
        public LocalizedString infoKey;

        public bool showSkipButton = false;
        public bool showNextButton = false;
        public bool showBackButton = false;
        public bool showFinishButton = false;

        [Tooltip("Show the shared language dropdown in this step")]
        public bool showLanguageDropdown = false;
    }

    private void Start()
    {
        if (SaveLoadHandler.Instance != null && SaveLoadHandler.Instance.data.tutorialDone)
        {
            if (tutorialRoot != null) tutorialRoot.SetActive(false);
            gameObject.SetActive(false);
            IsActive = false;
            return;
        }

        IsActive = true;

        if (tutorialRoot != null && !tutorialRoot.activeSelf)
            tutorialRoot.SetActive(true);

        SetHideTargets(false);

        if (skipButton) skipButton.onClick.AddListener(FinishTutorial);
        if (finishButton) finishButton.onClick.AddListener(FinishTutorial);
        if (nextButton) nextButton.onClick.AddListener(NextStep);
        if (backButton) backButton.onClick.AddListener(PreviousStep);

        currentStep = 0;
        ApplyStep();
    }

    private void ApplyStep()
    {
        if (steps == null || steps.Count == 0 || currentStep < 0 || currentStep >= steps.Count)
        {
            Debug.LogWarning("TutorialMenu: No valid steps.");
            return;
        }

        TutorialStep step = steps[currentStep];

        // Unsubscribe previous events to prevent leak
        if (_lastTitleKey != null) _lastTitleKey.StringChanged -= SetTitle;
        if (_lastInfoKey != null) _lastInfoKey.StringChanged -= SetInfo;

        _lastTitleKey = step.titleKey;
        _lastInfoKey = step.infoKey;

        if (titleText)
        {
            step.titleKey.StringChanged += SetTitle;
            step.titleKey.RefreshString();
        }

        if (infoText)
        {
            step.infoKey.StringChanged += SetInfo;
            step.infoKey.RefreshString();
        }

        if (skipButton) skipButton.gameObject.SetActive(step.showSkipButton);
        if (nextButton) nextButton.gameObject.SetActive(step.showNextButton);
        if (backButton) backButton.gameObject.SetActive(step.showBackButton);
        if (finishButton) finishButton.gameObject.SetActive(step.showFinishButton);

        if (languageDropdown != null && languageDropdown.gameObject.activeSelf != step.showLanguageDropdown)
        {
            languageDropdown.gameObject.SetActive(step.showLanguageDropdown);
        }
    }

    private void SetTitle(string value)
    {
        if (titleText != null && titleText.text != value)
            titleText.text = value;
    }

    private void SetInfo(string value)
    {
        if (infoText != null && infoText.text != value)
            infoText.text = value;
    }

    private void NextStep()
    {
        if (currentStep < steps.Count - 1)
        {
            currentStep++;
            ApplyStep();
        }
    }

    private void PreviousStep()
    {
        if (currentStep > 0)
        {
            currentStep--;
            ApplyStep();
        }
    }

    private void FinishTutorial()
    {
        if (SaveLoadHandler.Instance != null)
        {
            SaveLoadHandler.Instance.data.tutorialDone = true;
            SaveLoadHandler.Instance.SaveToDisk();
        }

        if (tutorialRoot != null) tutorialRoot.SetActive(false);
        gameObject.SetActive(false);
        IsActive = false;
        SetHideTargets(true);
    }

    private void SetHideTargets(bool show)
    {
        foreach (var obj in hideWhileTutorialActive)
        {
            if (obj) obj.SetActive(show);
        }
    }
}
