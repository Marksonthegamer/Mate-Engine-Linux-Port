using UnityEngine;
using UnityEngine.UI;
public class AvatarScaleController : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private Slider avatarSizeSlider;

    [Header("Scroll Settings")]
    [SerializeField] private float scrollSensitivity = 0.1f;
    [SerializeField] private float smoothFactor = 0.1f; 

    private float minSize;
    private float maxSize;
    private float targetSize;
    private Transform modelRoot;
    private GameObject currentModel;
    private AvatarAnimatorController controller;

    void Start()
    {
        if (avatarSizeSlider == null) return;

        minSize = avatarSizeSlider.minValue;
        maxSize = avatarSizeSlider.maxValue;
        targetSize = avatarSizeSlider.value;

        var modelRootGO = GameObject.Find("Model");
        if (modelRootGO != null)
            modelRoot = modelRootGO.transform;
        avatarSizeSlider.onValueChanged.AddListener(v => targetSize = v);
    }

    public void SyncWithSlider()
    {
        if (avatarSizeSlider != null)
            targetSize = avatarSizeSlider.value;
    }

    void Update()
    {
        if (avatarSizeSlider == null)
            return;

        if (MenuActions.IsMovementBlocked())
            return;

        if (modelRoot != null)
        {
            GameObject activeModel = null;
            for (int i = 0; i < modelRoot.childCount; i++)
            {
                var child = modelRoot.GetChild(i);
                if (child.gameObject.activeInHierarchy)
                {
                    activeModel = child.gameObject;
                    break;
                }
            }

            if (activeModel != currentModel)
            {
                currentModel = activeModel;
                controller = currentModel != null ? currentModel.GetComponent<AvatarAnimatorController>() : null;
            }
        }

        if (controller != null && controller.isDragging)
            return;


        float scroll = Input.mouseScrollDelta.y;
        if (scroll != 0f)
        {
            targetSize = Mathf.Clamp(
                targetSize + scroll * scrollSensitivity,
                minSize, maxSize
            );
        }

        float current = avatarSizeSlider.value;
        float smoothed = Mathf.Lerp(
            current,
            targetSize,
            1f - Mathf.Pow(1f - smoothFactor, Time.deltaTime * 60f)
        );

        if (Mathf.Abs(smoothed - current) > 0.0001f)
        {
            avatarSizeSlider.SetValueWithoutNotify(smoothed);
            avatarSizeSlider.value = smoothed;

            SaveLoadHandler.Instance.data.avatarSize = smoothed;
            SaveLoadHandler.Instance.SaveToDisk();
            SaveLoadHandler.ApplyAllSettingsToAllAvatars();
        }
    }
}
