using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections;
using System.Collections.Generic;

public class MenuAudioHandler : MonoBehaviour
{
    [Header("Audio Source GameObject (drag here)")]
    public AudioSource audioSource;

    [Range(0f, 10f)]
    public float disableDelay = 1f;

    [Header("Startup Sounds (plays once on app start)")]
    public List<AudioClip> startupSounds = new List<AudioClip>();
    public float startupPitchMin = 1f;
    public float startupPitchMax = 1f;
    [Range(0f, 1f)] public float startupVolume = 1f;

    [Header("Open Menu Sounds")]
    public List<AudioClip> openMenuSounds = new List<AudioClip>();
    public float openMenuPitchMin = 1f;
    public float openMenuPitchMax = 1f;
    [Range(0f, 1f)] public float openMenuVolume = 1f;

    [Header("Close Menu Sounds")]
    public List<AudioClip> closeMenuSounds = new List<AudioClip>();
    public float closeMenuPitchMin = 1f;
    public float closeMenuPitchMax = 1f;
    [Range(0f, 1f)] public float closeMenuVolume = 1f;

    [Header("Button Sounds")]
    public List<AudioClip> buttonSounds = new List<AudioClip>();
    public float buttonPitchMin = 1f;
    public float buttonPitchMax = 1f;
    [Range(0f, 1f)] public float buttonVolume = 1f;

    [Header("Toggle Sounds")]
    public List<AudioClip> toggleSounds = new List<AudioClip>();
    public float togglePitchMin = 1f;
    public float togglePitchMax = 1f;
    [Range(0f, 1f)] public float toggleVolume = 1f;

    [Header("Slider Sounds")]
    public List<AudioClip> sliderSounds = new List<AudioClip>();
    public float sliderPitchMin = 1f;
    public float sliderPitchMax = 1f;
    [Range(0f, 1f)] public float sliderVolume = 1f;

    [Header("Dropdown Sounds")]
    public List<AudioClip> dropdownSounds = new List<AudioClip>();
    public float dropdownPitchMin = 1f;
    public float dropdownPitchMax = 1f;
    [Range(0f, 1f)] public float dropdownVolume = 1f;

    private HashSet<Slider> activeSliders = new HashSet<Slider>();
    private bool wasMenuOpenLastFrame = false;
    private float disableTimer = 0f;
    private bool hasPlayedStartupSound = false;

    private void OnEnable()
    {
        SetupUIListeners();
        StartCoroutine(MenuMonitor());
    }

    private IEnumerator MenuMonitor()
    {
        while (true)
        {
            bool isOpen = /* AvatarSettingsMenu.IsMenuOpen || */AvatarClothesHandler.IsMenuOpen || TutorialMenu.IsActive;


            if (!hasPlayedStartupSound && SaveLoadHandler.Instance?.data != null)
            {
                // Only play once menuVolume is actually set
                float menuVol = SaveLoadHandler.Instance.data.menuVolume;
                if (menuVol > 0f)
                {
                    PlaySound(startupSounds, startupPitchMin, startupPitchMax, startupVolume);
                    hasPlayedStartupSound = true;
                }
            }


            if (isOpen)
            {
                if (audioSource != null && !audioSource.gameObject.activeSelf)
                    audioSource.gameObject.SetActive(true);

                if (!wasMenuOpenLastFrame)
                    PlaySound(openMenuSounds, openMenuPitchMin, openMenuPitchMax, openMenuVolume);

                disableTimer = 0f;
            }
            else
            {
                if (wasMenuOpenLastFrame)
                {
                    disableTimer = Time.time + disableDelay;
                    PlaySound(closeMenuSounds, closeMenuPitchMin, closeMenuPitchMax, closeMenuVolume);
                }

                if (disableTimer != 0f && Time.time >= disableTimer && audioSource != null && audioSource.gameObject.activeSelf)
                {
                    audioSource.gameObject.SetActive(false);
                    disableTimer = 0f;
                }
            }

            wasMenuOpenLastFrame = isOpen;
            yield return null;
        }
    }

    private void SetupUIListeners()
    {
        if (audioSource == null)
        {
            Debug.LogWarning("MenuAudioHandler: AudioSource not assigned.");
            return;
        }

        foreach (var button in GetComponentsInChildren<Button>(true))
        {
            if (button.GetComponent<ButtonLinker>() == null)
                button.onClick.AddListener(() => PlaySound(buttonSounds, buttonPitchMin, buttonPitchMax, buttonVolume));
        }


        foreach (var toggle in GetComponentsInChildren<Toggle>(true))
            toggle.onValueChanged.AddListener((_) => PlaySound(toggleSounds, togglePitchMin, togglePitchMax, toggleVolume));

        foreach (var slider in GetComponentsInChildren<Slider>(true))
            AddSliderEvents(slider);

        foreach (var dropdown in GetComponentsInChildren<Dropdown>(true))
            dropdown.onValueChanged.AddListener((_) => PlaySound(dropdownSounds, dropdownPitchMin, dropdownPitchMax, dropdownVolume));
    }

    private void AddSliderEvents(Slider slider)
    {
        EventTrigger trigger = slider.gameObject.GetComponent<EventTrigger>() ?? slider.gameObject.AddComponent<EventTrigger>();

        var pointerDown = new EventTrigger.Entry { eventID = EventTriggerType.PointerDown };
        pointerDown.callback.AddListener((_) =>
        {
            if (!activeSliders.Contains(slider))
            {
                activeSliders.Add(slider);
                PlaySound(sliderSounds, sliderPitchMin, sliderPitchMax, sliderVolume);
            }
        });
        trigger.triggers.Add(pointerDown);

        var pointerUp = new EventTrigger.Entry { eventID = EventTriggerType.PointerUp };
        pointerUp.callback.AddListener((_) => activeSliders.Remove(slider));
        trigger.triggers.Add(pointerUp);
    }

    private void PlaySound(List<AudioClip> clips, float pitchMin, float pitchMax, float volume)
    {
        if (clips == null || clips.Count == 0 || audioSource == null || !audioSource.gameObject.activeSelf)
            return;

        float menuVolumeMultiplier = 1f;
        if (SaveLoadHandler.Instance != null)
        {
            menuVolumeMultiplier = SaveLoadHandler.Instance.data.menuVolume;
        }

        float finalVolume = volume * menuVolumeMultiplier;
        if (finalVolume <= 0f) return;

        audioSource.pitch = Random.Range(pitchMin, pitchMax);
        audioSource.PlayOneShot(clips[Random.Range(0, clips.Count)], finalVolume);
    }

    public void PlayOpenSound()
    {
        if (audioSource != null && !audioSource.gameObject.activeSelf)
            audioSource.gameObject.SetActive(true);

        PlaySound(openMenuSounds, openMenuPitchMin, openMenuPitchMax, openMenuVolume);
    }

    public void PlayCloseSound()
    {
        if (audioSource != null && !audioSource.gameObject.activeSelf)
            audioSource.gameObject.SetActive(true);

        PlaySound(closeMenuSounds, closeMenuPitchMin, closeMenuPitchMax, closeMenuVolume);
    }

    public void PlayButtonSound()
    {
        if (audioSource != null && !audioSource.gameObject.activeSelf)
            audioSource.gameObject.SetActive(true);

        PlaySound(buttonSounds, buttonPitchMin, buttonPitchMax, buttonVolume);
    }


}
