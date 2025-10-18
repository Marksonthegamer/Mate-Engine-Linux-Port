using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;

public class BlendshapeUIBlock : MonoBehaviour
{
    [Header("9 Slots (Label + Slider)")]
    public TMP_Text[] labels = new TMP_Text[9];
    public Slider[] sliders = new Slider[9];

    // (Optional) Wenn du einen separaten Value-Text pro Slot hast, hier zuweisen.
    // Wenn leer, hängt der Manager " (NN)" an den Labeltext an.
    public TMP_Text[] valueTexts = new TMP_Text[9];

    // Interne Wurzeln der Slots (wird automatisch aus Slider oder Label hergeleitet)
    private GameObject[] slotRoots = new GameObject[9];

    private void Awake()
    {
        for (int i = 0; i < 9; i++)
        {
            if (sliders[i] != null)
                slotRoots[i] = sliders[i].transform.parent != null ? sliders[i].transform.parent.gameObject : sliders[i].gameObject;
            else if (labels[i] != null)
                slotRoots[i] = labels[i].transform.parent != null ? labels[i].transform.parent.gameObject : labels[i].gameObject;
            else
                slotRoots[i] = null;
        }
    }

    public void SetSlotActive(int index, bool active)
    {
        if (index < 0 || index >= 9) return;
        if (slotRoots[index] != null)
            slotRoots[index].SetActive(active);
    }

    public void SetupSlot(int index, string displayName, float initialValue, Action<float> onChanged)
    {
        if (index < 0 || index >= 9) return;

        // aktivieren
        SetSlotActive(index, true);

        // Label
        if (labels[index] != null)
            labels[index].text = displayName;

        // Slider
        if (sliders[index] != null)
        {
            sliders[index].minValue = 0f;
            sliders[index].maxValue = 100f;
            sliders[index].wholeNumbers = true;
            sliders[index].SetValueWithoutNotify(initialValue);
            sliders[index].onValueChanged.RemoveAllListeners();
            sliders[index].onValueChanged.AddListener(v =>
            {
                UpdateValueText(index, v);
                onChanged?.Invoke(v);
            });
            UpdateValueText(index, initialValue);
        }
    }

    public void ClearUnusedFrom(int startIndex)
    {
        for (int i = startIndex; i < 9; i++)
            SetSlotActive(i, false);
    }

    private void UpdateValueText(int index, float value)
    {
        if (index < 0 || index >= 9) return;

        if (valueTexts != null && valueTexts.Length == 9 && valueTexts[index] != null)
        {
            valueTexts[index].text = ((int)value).ToString();
        }
        else
        {
            // Fallback: Hänge den Wert an den Labeltext an (z.B. "Smile (80)")
            if (labels[index] != null)
            {
                // Entferne evtl. alten Wert in Klammern
                string baseName = labels[index].text;
                int p = baseName.LastIndexOf(" (");
                if (p >= 0) baseName = baseName.Substring(0, p);
                labels[index].text = $"{baseName} ({(int)value})";
            }
        }
    }
}
