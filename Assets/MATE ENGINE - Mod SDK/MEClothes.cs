using UnityEngine;

public class MEClothes : MonoBehaviour
{
    [Tooltip("Used only to keep the script in builds. This instance will be ignored at runtime.")]
    public bool isScriptLoader = false;
    [System.Serializable]

    public class OutfitEntry
    {
        public string name;
        public string tag;
        public GameObject[] gameObjects;

    }

    [Header("Outfit Entries (Max 8)")]
    public OutfitEntry[] entries = new OutfitEntry[8];

    public void ActivateOutfit(int index)
    {
        if (index < 0 || index >= entries.Length) return;

        OutfitEntry selected = entries[index];
        if (selected == null || selected.gameObjects == null) return;

        bool isCurrentlyOn = IsAnyActive(selected.gameObjects);
        bool hasTag = !string.IsNullOrEmpty(selected.tag);

        // Turn OFF all entries with the same tag if tag is present
        if (hasTag)
        {
            for (int i = 0; i < entries.Length; i++)
            {
                if (i == index) continue;

                OutfitEntry entry = entries[i];
                if (entry == null || entry.gameObjects == null) continue;
                if (entry.tag == selected.tag)
                {
                    foreach (var obj in entry.gameObjects)
                        if (obj != null) obj.SetActive(false);
                }
            }
        }

        // Toggle current entry
        foreach (var obj in selected.gameObjects)
            if (obj != null) obj.SetActive(!isCurrentlyOn);
    }

    private bool IsAnyActive(GameObject[] targets)
    {
        foreach (var obj in targets)
            if (obj != null && obj.activeSelf) return true;
        return false;
    }
}
