using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace Xamin
{
    [RequireComponent(typeof(Image))]
    public class Button : MonoBehaviour
    {
        [Tooltip("Your actions, that will be executed when the buttons is pressed")]
        public UnityEvent action;
        [Tooltip("The icon of this button")]
        public Sprite image;
        [Tooltip("If this button can be pressed or not. False = grayed out button")]
        public bool unlocked;
        [Tooltip("Can be used to reference the button via code.")]
        public string id;

        public Color customColor;
        public bool useCustomColor;

        // --- NEU: HIDE CONDITION FIELDS ---
        [Header("Button Hide Conditions")]
        [Tooltip("Button wird ausgeblendet, wenn einer dieser Animator-Bool-Parameter true ist (z.B. IsSitting, IsWindowsSit)")]
        public string[] hideIfAnimatorBool;
        [Tooltip("Button wird ausgeblendet, wenn einer dieser States im Base Layer aktiv ist (z.B. Sit, WindowsSit)")]
        public string[] hideIfStateName;

        private UnityEngine.UI.Image imageComponent;
        private bool _isimageComponentNotNull;

        void Start()
        {
            imageComponent = GetComponent<UnityEngine.UI.Image>();
            if (image)
                imageComponent.sprite = image;
            _isimageComponentNotNull = imageComponent != null;
        }

        public Color currentColor
        {
            get { return imageComponent.color; }
        }

        public void SetColor(Color c)
        {
            if (_isimageComponentNotNull)
                imageComponent.color = c;
        }

        public void ExecuteAction()
        {
            action.Invoke();
        }
    }
}
