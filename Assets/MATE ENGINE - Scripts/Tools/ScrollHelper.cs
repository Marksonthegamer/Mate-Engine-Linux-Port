using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(ScrollRect))]
public class ScrollHelper : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Smoothness (0 = instant, 1 = ultra slow)")]
    [Range(0f, 1f)] public float smoothFactor = 0.1f;

    [Header("Speed")]
    [Tooltip("Wieviele Pixel pro Mausrad-Notch gescrollt werden (unabhängig von der Contentgröße).")]
    public float pixelsPerNotch = 80f;

    [Tooltip("Optionaler Multiplikator für Touchpads/hohe Auflösung (wirkt auf Input.GetAxis-Wert).")]
    public float inputMultiplier = 1f;

    private ScrollRect scrollRect;
    private RectTransform contentRT, viewportRT;

    private bool isPointerOver = false;
    private float pixelVelocity = 0f; // Pixel/Frame (wird geglättet)

    void Awake()
    {
        scrollRect = GetComponent<ScrollRect>();
        contentRT = scrollRect.content;
        viewportRT = scrollRect.viewport != null ? scrollRect.viewport : GetComponent<RectTransform>();

        // Empfehlung: ScrollRect-Inertia aus, da wir die Glättung selber machen
        // Du kannst das auskommentieren, wenn du die eingebaute Inertia behalten willst.
        scrollRect.inertia = false;
    }

    void Update()
    {
        if (contentRT == null || viewportRT == null) return;

        // 1) Input einsammeln (Mausrad)
        if (isPointerOver)
        {
            float wheel = Input.GetAxis("Mouse ScrollWheel"); // + hoch, - runter (je nach OS)
            if (Mathf.Abs(wheel) > 0.0001f)
            {
                // in Pixel-Geschwindigkeit umrechnen
                pixelVelocity += wheel * pixelsPerNotch * inputMultiplier;
            }
        }

        // 2) Pixelgeschwindigkeit auf normalized Position anwenden
        if (Mathf.Abs(pixelVelocity) > 0.001f)
        {
            float scrollable = Mathf.Max(1f, contentRT.rect.height - viewportRT.rect.height); // in Pixel
            // Unitys verticalNormalizedPosition: 1 = Top, 0 = Bottom
            // Ein positiver wheel-Wert soll nach oben scrollen → normalized nach OBEN = +delta
            float deltaNormalized = (pixelVelocity / scrollable);

            float v = scrollRect.verticalNormalizedPosition + deltaNormalized;
            scrollRect.verticalNormalizedPosition = Mathf.Clamp01(v);

            // 3) Glättung/Abklingen (exponential)
            float decay = 1f - Mathf.Pow(1f - Mathf.Clamp01(smoothFactor), Time.unscaledDeltaTime * 60f);
            pixelVelocity = Mathf.Lerp(pixelVelocity, 0f, decay);
        }
    }

    public void OnPointerEnter(PointerEventData eventData) => isPointerOver = true;
    public void OnPointerExit(PointerEventData eventData) => isPointerOver = false;
}
