using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using UnityEngine.Assertions;
using X11;

namespace Xamin
{
    public class CircleSelector : MonoBehaviour
    {
        [Range(2, 10)] private int buttonCount;
        private int startButCount;

        [Header("Customization")]
        public Color AccentColor = Color.red, DisabledColor = Color.gray, BackgroundColor = Color.white;
        [Space(10)] public bool UseSeparators = true;
        [SerializeField] private GameObject separatorPrefab;

        [Header("Animations")]
        [Range(0.0001f, 1)]
        public float LerpAmount = .145f;
        public AnimationType OpenAnimation, CloseAnimation;
        public float Size = 1f;
        private Image _cursor, _background;
        private float _desiredFill;
        float radius = 120f;

        [Header("Interaction")]
        public List<GameObject> Buttons = new List<GameObject>();
        public ButtonSource buttonSource;
        private List<Xamin.Button> buttonsInstances = new List<Xamin.Button>();
        private Vector2 _menuCenter;
        public bool RaiseOnSelection;
        private GameObject _selectedSegment;
        private bool _previousUseSeparators;
        public bool flip = true;

        [HideInInspector]
        public GameObject SelectedSegment
        {
            get => _selectedSegment;
            set
            {
                if (value != null && value != SelectedSegment)
                    _selectedSegment = value;
            }
        }
        public bool selectOnlyOnHover;
        public float pieThickness = 85;
        public bool snap, tiltTowardsMouse;
        public float tiltAmount = 15;
        private bool opened;
        public enum ControlType { mouseAndTouch, gamepad, customVector }
        public enum ButtonSource { prefabs, scene }
        public enum AnimationType { zoomIn, zoomOut }
        [Header("Controls")]
        public string activationButton = "Fire1";
        public ControlType controlType;
        public string gamepadAxisX, gamepadAxisY;
        public Vector2 CustomInputVector;
        private Dictionary<GameObject, Button> instancedButtons;
        private AvatarAnimatorReceiver animatorReceiver;
        public float zRotation = 180;
        public bool rotateButtons = false;
        private List<bool> lastButtonVisibility = new List<bool>();

        void Start()
        {
            instancedButtons = new Dictionary<GameObject, Button>();
            transform.localScale = Vector3.zero;
            _cursor = transform.Find("Cursor").GetComponent<Image>();
            _background = transform.Find("Background").GetComponent<Image>();
            EnsureAnimatorReceiver();
            BuildButtons();
        }

        public bool Open()
        {
            RefreshAllButtonColorsDelayed();
            EnsureAnimatorReceiver();
            BuildButtons();

            if (buttonsInstances.Count == 0)
            {
                opened = false;
                transform.localScale = Vector3.zero;
                return false;
            }
            _menuCenter = new Vector2(Screen.width / 2f, Screen.height / 2f);
            opened = true;
            transform.localScale = (OpenAnimation == AnimationType.zoomIn) ? Vector3.zero : Vector3.one * 10;
            return true;
        }

        public bool Open(Vector2 origin)
        {
            if (!Open()) return false;
            _menuCenter = origin;
            transform.localPosition = _menuCenter - new Vector2(Screen.width / 2f, Screen.height / 2f);
            return true;
        }

        public void Close() => opened = false;

        public Xamin.Button GetButtonWithId(string id) =>
            buttonsInstances.Find(btn => btn.id == id);

        void ChangeSeparatorsState()
        {
            var sep = transform.Find("Separators");
            if (sep) sep.gameObject.SetActive(UseSeparators);
            else Debug.LogError("Can't find Separators");
        }
        void Update()
        {
            if (opened && Buttons != null && Buttons.Count > 0)
            {
                bool needsRebuild = false;
                if (lastButtonVisibility.Count != Buttons.Count)
                    lastButtonVisibility = new List<bool>(new bool[Buttons.Count]);

                for (int i = 0; i < Buttons.Count; i++)
                {
                    var btnComp = Buttons[i] != null ? Buttons[i].GetComponent<Xamin.Button>() : null;
                    bool shouldBeVisible = btnComp != null ? !ShouldHideButton(btnComp) : false;
                    if (shouldBeVisible != lastButtonVisibility[i])
                    {
                        needsRebuild = true;
                        lastButtonVisibility[i] = shouldBeVisible;
                    }
                }
                if (needsRebuild)
                {
                    BuildButtons();
                    return;
                }
            }

            if (opened)
            {
                transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * Size, 0.2f);
                if (Vector3.Distance(transform.localScale, Vector3.one * Size) < 0.001f)
                    transform.localScale = Vector3.one * Size;
                _background.color = BackgroundColor;
                if (UseSeparators != _previousUseSeparators)
                    ChangeSeparatorsState();

                if (transform.localScale.x >= Size - 0.2f)
                {
                    buttonCount = buttonsInstances.Count;
                    if (startButCount != buttonCount && buttonSource == ButtonSource.prefabs)
                    {
                        Start();
                        return;
                    }

                    _cursor.fillAmount = Mathf.Lerp(_cursor.fillAmount, _desiredFill, 0.2f);

                    Vector3 screenBounds = Camera.main.WorldToScreenPoint(transform.position);
                    Vector2 vector = Input.mousePosition - screenBounds;
                    if (tiltTowardsMouse)
                    {
                        float x = vector.x / screenBounds.x, y = vector.y / screenBounds.y;
                        Vector3 tilt = new Vector3(y, -x, 0) * -tiltAmount;
                        Vector3 rotation = tilt + new Vector3(0, 0, zRotation);
                        transform.localRotation = Quaternion.Slerp(
                            transform.localRotation,
                            Quaternion.Euler(rotation),
                            LerpAmount
                        );
                    }
                    else
                    {
                        transform.localRotation = Quaternion.Euler(Vector3.forward * zRotation);
                    }

                    float mouseRotation = zRotation + 57.29578f * (
                        controlType == ControlType.mouseAndTouch
                            ? Mathf.Atan2(vector.x, vector.y)
                            : controlType == ControlType.gamepad
                                ? Mathf.Atan2(Input.GetAxis(gamepadAxisX), Input.GetAxis(gamepadAxisY))
                                : Mathf.Atan2(CustomInputVector.x, CustomInputVector.y)
                    );
                    if (mouseRotation < 0f) mouseRotation += 360f;
                    float cursorRotation = -(mouseRotation - _cursor.fillAmount * 360f / 2f) + zRotation;

                    float mouseDistanceFromCenter = Vector2.Distance(Camera.main.WorldToScreenPoint(transform.position), Input.mousePosition);

                    if ((selectOnlyOnHover && controlType == ControlType.mouseAndTouch && mouseDistanceFromCenter > pieThickness) ||
                        (selectOnlyOnHover && controlType == ControlType.gamepad &&
                         (Mathf.Abs(Input.GetAxisRaw(gamepadAxisX) + Mathf.Abs(Input.GetAxisRaw(gamepadAxisY)))) != 0) ||
                        !selectOnlyOnHover)
                    {
                        _cursor.enabled = true;

                        float difference = float.MaxValue;
                        GameObject nearest = null;
                        for (int i = 0; i < buttonCount; i++)
                        {
                            var btn = buttonsInstances[i];
                            GameObject b = btn.gameObject;
                            b.transform.localScale = Vector3.one;
                            float rotation = System.Convert.ToSingle(b.name);
                            float diff = Mathf.Abs(rotation - mouseRotation);
                            if (diff < difference)
                            {
                                nearest = b;
                                difference = diff;
                            }
                            if (rotateButtons)
                                b.transform.localEulerAngles = new Vector3(0, 0, -zRotation);
                        }
                        SelectedSegment = nearest;

                        if (snap && SelectedSegment != null)
                            cursorRotation = -(System.Convert.ToSingle(SelectedSegment.name) - _cursor.fillAmount * 360f / 2f);
                        _cursor.transform.localRotation = Quaternion.Slerp(_cursor.transform.localRotation,
                            Quaternion.Euler(0, 0, cursorRotation), LerpAmount);

                        if (SelectedSegment != null && instancedButtons.ContainsKey(SelectedSegment))
                            instancedButtons[SelectedSegment].SetColor(
                                Color.Lerp(instancedButtons[SelectedSegment].currentColor, BackgroundColor, LerpAmount));

                        for (int i = 0; i < buttonCount; i++)
                        {
                            Button b = buttonsInstances[i];
                            if (b.gameObject != SelectedSegment)
                            {
                                b.SetColor(Color.Lerp(b.currentColor,
                                    b.unlocked
                                        ? (b.useCustomColor ? b.customColor : AccentColor)
                                        : DisabledColor, LerpAmount));
                            }
                        }
                        try
                        {
                            if (SelectedSegment != null && instancedButtons.TryGetValue(SelectedSegment, out var segBtn) && segBtn.unlocked)
                                _cursor.color = Color.Lerp(_cursor.color, segBtn.useCustomColor ? segBtn.customColor : AccentColor, LerpAmount);
                            else
                                _cursor.color = Color.Lerp(_cursor.color, DisabledColor, LerpAmount);
                        }
                        catch { }
                    }
                    else if (_cursor.enabled && SelectedSegment != null)
                    {
                        _cursor.enabled = false;
                        if (instancedButtons.TryGetValue(SelectedSegment, out var segBtn))
                        {
                            segBtn.SetColor(segBtn.unlocked
                                ? (segBtn.useCustomColor ? segBtn.customColor : AccentColor)
                                : DisabledColor);
                        }
                        for (int i = 0; i < buttonCount; i++)
                        {
                            Button b = buttonsInstances[i];
                            if (b.gameObject != SelectedSegment)
                                b.SetColor(b.unlocked
                                    ? (buttonsInstances[SelectedSegment != null ? buttonsInstances.IndexOf(segBtn) : 0].useCustomColor
                                        ? buttonsInstances[SelectedSegment != null ? buttonsInstances.IndexOf(segBtn) : 0].customColor
                                        : AccentColor)
                                    : DisabledColor);
                        }
                    }
                    if (_cursor.isActiveAndEnabled)
                        CheckForInput();
                    else if (Input.GetButtonUp(activationButton))
                        Close();
                }
                _previousUseSeparators = UseSeparators;
            }
            else
            {
                transform.localScale = Vector3.Lerp(transform.localScale,
                    (CloseAnimation == AnimationType.zoomIn) ? Vector3.zero : Vector3.one * 10, 0.2f);
                Vector3 target = (CloseAnimation == AnimationType.zoomIn) ? Vector3.zero : Vector3.one * 10;
                if (Vector3.Distance(transform.localScale, target) < 0.001f)
                    transform.localScale = target;
                _cursor.color = Color.Lerp(_cursor.color, Color.clear, LerpAmount / 3f);
                _background.color = Color.Lerp(_background.color, Color.clear, LerpAmount / 3f);
            }
        }


        public void RefreshAllButtonColors()
        {
            if (buttonsInstances.Count == 0) return;
            foreach (var btn in buttonsInstances)
                btn.SetColor(Color.Lerp(btn.currentColor, btn.unlocked ? (btn.useCustomColor ? btn.customColor : AccentColor) : DisabledColor, LerpAmount));
        }

        public void RefreshAllButtonColorsDelayed() => StartCoroutine(DoRefreshAllButtonColorsDelayed());
        private System.Collections.IEnumerator DoRefreshAllButtonColorsDelayed() { yield return null; RefreshAllButtonColors(); }

        void CheckForInput()
        {
            if (SelectedSegment == null || instancedButtons == null || !instancedButtons.ContainsKey(SelectedSegment))
                return;

            var btn = instancedButtons[SelectedSegment];
            _cursor.rectTransform.localPosition = Vector3.Lerp(_cursor.rectTransform.localPosition,
                Input.GetButton(activationButton) ? new Vector3(0, 0, RaiseOnSelection ? -10 : 0) : Vector3.zero, LerpAmount);

            if (Input.GetButton(activationButton))
            {
                if (btn.unlocked)
                    SelectedSegment.transform.localScale = new Vector2(.8f, .8f);
            }

            if (Input.GetButtonUp(activationButton))
            {
                if (btn.unlocked)
                {
                    btn.ExecuteAction();
                    var audio = FindFirstObjectByType<MenuAudioHandler>();
                    if (audio != null) audio.PlayButtonSound();
                }
                Close();
            }
        }

        void EnsureAnimatorReceiver()
        {
            RefreshAllButtonColorsDelayed();
            animatorReceiver = null;
            foreach (var recv in Object.FindObjectsByType<AvatarAnimatorReceiver>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (recv != null && recv.isActiveAndEnabled && recv.gameObject.activeInHierarchy
                    && recv.avatarAnimator != null && recv.avatarAnimator.isActiveAndEnabled
                    && recv.avatarAnimator.gameObject.activeInHierarchy)
                {
                    animatorReceiver = recv;
                    break;
                }
            }
        }
        void BuildButtons()
        {
            RefreshAllButtonColorsDelayed();
            foreach (Transform child in transform.Find("Buttons")) Destroy(child.gameObject);
            foreach (Transform sep in transform.Find("Separators")) Destroy(sep.gameObject);
            buttonsInstances.Clear();
            instancedButtons = new Dictionary<GameObject, Button>();

            int visibleCount = 0;
            List<GameObject> visibleButtonObjects = new List<GameObject>();
            foreach (var btnObj in Buttons)
            {
                GameObject buttonObj = buttonSource == ButtonSource.prefabs
                    ? Instantiate(btnObj, Vector2.zero, transform.rotation)
                    : btnObj;
                Xamin.Button btn = buttonObj.GetComponent<Xamin.Button>();
                if (ShouldHideButton(btn)) { Destroy(buttonObj); continue; }
                visibleButtonObjects.Add(buttonObj);
                visibleCount++;
            }
            buttonCount = visibleCount;

            if (buttonCount > 0 && buttonCount < 11)
            {
                startButCount = buttonCount;
                _desiredFill = 1f / buttonCount;
                float fillRadius = _desiredFill * 360f;
                float previousRotation = 0;

                for (int i = 0; i < visibleCount; i++)
                {
                    GameObject buttonObj = visibleButtonObjects[i];
                    Xamin.Button btn = buttonObj.GetComponent<Xamin.Button>();
                    buttonObj.transform.SetParent(transform.Find("Buttons"));
                    float bRot = previousRotation + fillRadius / 2;
                    previousRotation = bRot + fillRadius / 2;
                    var separator = Instantiate(separatorPrefab, Vector3.zero, Quaternion.identity);
                    separator.transform.SetParent(transform.Find("Separators"));
                    separator.transform.localScale = Vector3.one;
                    separator.transform.localPosition = Vector3.zero;
                    separator.transform.localRotation = Quaternion.Euler(0, 0, previousRotation);
                    buttonObj.transform.localPosition = new Vector2(radius * Mathf.Cos((bRot - 90) * Mathf.Deg2Rad),
                        -radius * Mathf.Sin((bRot - 90) * Mathf.Deg2Rad));
                    buttonObj.transform.localScale = Vector3.one;
                    if (bRot > 360) bRot -= 360;
                    buttonObj.name = bRot.ToString();
                    if (btn)
                    {
                        instancedButtons[buttonObj] = btn;
                        btn.SetColor(btn.useCustomColor ? btn.customColor : AccentColor);
                        buttonsInstances.Add(btn);
                    }
                    else buttonObj.GetComponent<Image>().color = DisabledColor;
                }
            }
            SelectedSegment = buttonsInstances.Count != 0 ? buttonsInstances[buttonsInstances.Count - 1].gameObject : null;
            if (SelectedSegment == null)
            {
                opened = false;
                transform.localScale = Vector3.zero;
            }
        }

        bool ShouldHideButton(Xamin.Button btn)
        {
            if (animatorReceiver == null || animatorReceiver.avatarAnimator == null)
                return false;
            var animator = animatorReceiver.avatarAnimator;
            if (btn.hideIfAnimatorBool != null)
            {
                foreach (var param in btn.hideIfAnimatorBool)
                {
                    if (!string.IsNullOrEmpty(param))
                        foreach (var animatorParam in animator.parameters)
                            if (animatorParam.type == AnimatorControllerParameterType.Bool && animatorParam.name == param && animator.GetBool(param))
                                return true;
                }
            }
            if (btn.hideIfStateName != null)
            {
                AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
                foreach (var state in btn.hideIfStateName)
                    if (!string.IsNullOrEmpty(state) && stateInfo.IsName(state))
                        return true;
            }
            if (btn != null && btn.id == "clothes")
            {
                GameObject avatarGO = animatorReceiver?.avatarAnimator?.gameObject;
                bool hasClothes = avatarGO != null && (avatarGO.GetComponent<MEClothes>() ?? avatarGO.GetComponentInChildren<MEClothes>(true)) != null;
                if (!hasClothes) return true;
            }
            return false;
        }
    }
}