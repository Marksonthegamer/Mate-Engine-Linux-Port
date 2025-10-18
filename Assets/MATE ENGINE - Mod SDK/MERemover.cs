using System.Collections.Generic;
using UnityEngine;

public class MERemover : MonoBehaviour
{
    [System.Serializable]
    public class TargetEntry
    {
        public GameObject target;
        [HideInInspector] public bool hasBeenDisabled = false;
    }

    [SerializeField]
    private List<TargetEntry> targets = new List<TargetEntry>();

    void Update()
    {
        for (int i = 0; i < targets.Count; i++)
        {
            TargetEntry entry = targets[i];
            if (!entry.hasBeenDisabled && entry.target != null && entry.target.activeSelf)
            {
                entry.target.SetActive(false);
                entry.hasBeenDisabled = true;
            }
        }
    }

    void OnDisable()
    {
        for (int i = 0; i < targets.Count; i++)
        {
            TargetEntry entry = targets[i];
            if (entry.hasBeenDisabled && entry.target != null)
            {
                entry.target.SetActive(true);
                entry.hasBeenDisabled = false;
            }
        }
    }
}
