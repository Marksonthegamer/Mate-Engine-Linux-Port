using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class AvatarClothesHandler : MonoBehaviour
{
    [Header("Optional UI Panel for this menu")]
    public GameObject menuPanel;

    [Header("Button References (Max 8)")]
    public Button[] outfitButtons = new Button[8];
    public TextMeshProUGUI[] buttonLabels = new TextMeshProUGUI[8];

    [Header("Animated Button Settings")]
    [Range(0f, 10f)] public float floatSpeedX = 1f;
    [Range(0f, 10f)] public float floatSpeedY = 1.5f;
    [Range(0f, 10f)] public float floatAmplitudeX = 5f;
    [Range(0f, 10f)] public float floatAmplitudeY = 5f;

    public static bool IsMenuOpen { get; private set; }

    private Vector3[] initialButtonPositions = new Vector3[8];
    private float[] buttonTimeOffsets = new float[8];

    private bool lastPanelState = false;

    void Start()
    {
        if (menuPanel != null)
        {
            menuPanel.SetActive(false);
            IsMenuOpen = false;
            lastPanelState = false;
        }

        for (int i = 0; i < outfitButtons.Length; i++)
        {
            if (outfitButtons[i] != null)
            {
                initialButtonPositions[i] = outfitButtons[i].transform.localPosition;
                buttonTimeOffsets[i] = Random.Range(0f, 100f);
            }
        }

        RefreshButtons();
    }

    void Update()
    {
        AnimateButtons();
        if (menuPanel != null)
        {
            bool currentState = menuPanel.activeSelf;
            if (currentState && !lastPanelState)
            {
                RefreshButtons();
            }

            lastPanelState = currentState;
        }
    }

    private void AnimateButtons()
    {
        if (!Application.isPlaying) return;

        float time = Time.time;

        for (int i = 0; i < outfitButtons.Length; i++)
        {
            if (outfitButtons[i] != null && outfitButtons[i].gameObject.activeSelf)
            {
                Vector3 basePos = initialButtonPositions[i];
                float offset = buttonTimeOffsets[i];

                float x = Mathf.Sin(time * floatSpeedX + offset) * floatAmplitudeX;
                float y = Mathf.Cos(time * floatSpeedY + offset) * floatAmplitudeY;

                outfitButtons[i].transform.localPosition = basePos + new Vector3(x, y, 0);
            }
        }
    }

    public void RefreshButtons()
    {
        var clothesComponent = FindClothesComponent(out var clothesType);
        if (clothesComponent == null)
        {
            HideAllButtons();
            IsMenuOpen = false;
            return;
        }

        var entriesField = clothesType.GetField("entries");
        if (entriesField == null)
        {
            HideAllButtons();
            IsMenuOpen = false;
            return;
        }

        var entries = entriesField.GetValue(clothesComponent) as System.Array;
        if (entries == null)
        {
            HideAllButtons();
            IsMenuOpen = false;
            return;
        }

        int visibleCount = 0;
        int count = Mathf.Min(entries.Length, outfitButtons.Length);
        for (int i = 0; i < outfitButtons.Length; i++)
        {
            if (i < count)
            {
                var entry = entries.GetValue(i);
                if (entry == null) { outfitButtons[i].gameObject.SetActive(false); continue; }

                var nameField = entry.GetType().GetField("name");
                string outfitName = nameField?.GetValue(entry) as string;
                if (string.IsNullOrEmpty(outfitName))
                {
                    outfitButtons[i].gameObject.SetActive(false);
                    continue;
                }

                int index = i;
                outfitButtons[i].gameObject.SetActive(true);
                buttonLabels[i].text = outfitName;
                outfitButtons[i].onClick.RemoveAllListeners();
                outfitButtons[i].onClick.AddListener(() =>
                {
                    ActivateOutfit(clothesComponent, clothesType, index);
                    PlayClothesClickSound();
                });

                initialButtonPositions[i] = outfitButtons[i].transform.localPosition;
                visibleCount++;
            }
            else
            {
                outfitButtons[i].gameObject.SetActive(false);
            }
        }

        IsMenuOpen = visibleCount > 0 && menuPanel != null && menuPanel.activeSelf;
    }
    private void HideAllButtons()
    {
        foreach (var btn in outfitButtons)
            if (btn != null) btn.gameObject.SetActive(false);
    }

    private Component FindClothesComponent(out System.Type foundType)
    {
        foundType = null;
        foreach (var comp in GameObject.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
        {
            var type = comp.GetType();
            if (type.Name == "MEClothes" &&
                type.GetField("entries") != null &&
                type.GetField("isScriptLoader") != null)
            {
                bool isScriptLoader = (bool)type.GetField("isScriptLoader").GetValue(comp);
                if (!isScriptLoader)
                {
                    foundType = type;
                    return comp;
                }
            }
        }
        return null;
    }

    private void ActivateOutfit(Component clothesComponent, System.Type clothesType, int index)
    {
        var method = clothesType.GetMethod("ActivateOutfit");
        if (method != null)
            method.Invoke(clothesComponent, new object[] { index });
    }

    private void PlayClothesClickSound()
    {
        var menuAudio = FindFirstObjectByType<MenuAudioHandler>();
        if (menuAudio == null || menuAudio.audioSource == null) return;
        if (menuAudio.buttonSounds == null || menuAudio.buttonSounds.Count == 0) return;

        float pitch = Random.Range(menuAudio.buttonPitchMin, menuAudio.buttonPitchMax);
        float volume = menuAudio.buttonVolume * (SaveLoadHandler.Instance?.data.menuVolume ?? 1f);

        if (volume > 0f)
        {
            menuAudio.audioSource.pitch = pitch;
            menuAudio.audioSource.PlayOneShot(menuAudio.buttonSounds[Random.Range(0, menuAudio.buttonSounds.Count)], volume);
        }
    }
}
