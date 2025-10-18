using UnityEngine;
using UnityEngine.UI;
using System.Collections;

[ExecuteAlways]
public class StickyAutoScroll : MonoBehaviour
{
    [Header("Refs")]
    public ScrollRect scrollRect;
    public RectTransform content;
    public RectTransform viewport;

    [Header("Behaviour")]
    public bool forceAlways = true;
    [Range(0f, 1f)] public float bottomTolerance = 0.05f;

    [Header("Smoothing")]
    public float smoothDuration = 0.25f;
    public float settleTime = 0.08f;

    float _lastHeight = -1f;
    int _lastChildCount = -1;
    float _settleTimer = 0f;

    Coroutine _settleCo, _smoothCo;

    void Reset()
    {
        scrollRect = GetComponentInParent<ScrollRect>();
        if (scrollRect != null)
        {
            content = scrollRect.content;
            viewport = scrollRect.viewport;
        }
    }

    void Awake()
    {
        if (!scrollRect) scrollRect = GetComponentInParent<ScrollRect>();
        if (scrollRect)
        {
            if (!content) content = scrollRect.content;
            if (!viewport) viewport = scrollRect.viewport;
        }
    }

    void OnEnable()
    {
        if (isActiveAndEnabled)
            StartCoroutine(ScrollAfterLayout());
    }

    IEnumerator ScrollAfterLayout()
    {
        yield return null;
        Canvas.ForceUpdateCanvases();
        yield return null;
        Canvas.ForceUpdateCanvases();
        SmoothToBottom();
    }

    void Update()
    {
        if (!scrollRect || !content || !viewport) return;

        float h = content.rect.height;
        int cc = content.childCount;

        if (!Mathf.Approximately(h, _lastHeight) || cc != _lastChildCount)
        {
            _settleTimer = settleTime;
            if (_settleCo == null) _settleCo = StartCoroutine(SettleThenStick());
        }

        _lastHeight = h;
        _lastChildCount = cc;
    }

    IEnumerator SettleThenStick()
    {
        while (_settleTimer > 0f)
        {
            _settleTimer -= Time.unscaledDeltaTime;
            yield return null;
        }
        _settleCo = null;

        if (forceAlways || IsAtBottom(bottomTolerance))
            SmoothToBottom();
    }

    bool IsAtBottom(float tol)
    {
        return scrollRect.verticalNormalizedPosition <= tol;
    }

    void SmoothToBottom()
    {
        if (_smoothCo != null) StopCoroutine(_smoothCo);
        _smoothCo = StartCoroutine(SmoothContentToBottom());
    }

    IEnumerator SmoothContentToBottom()
    {
        if (!content || !viewport) yield break;

        Canvas.ForceUpdateCanvases();
        float viewH = viewport.rect.height;
        float contH = content.rect.height;
        float targetY = Mathf.Max(0f, contH - viewH);

        float startY = content.anchoredPosition.y;
        float dur = Mathf.Max(0.01f, smoothDuration);
        float t = 0f;

        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float u = t / dur;
            float k = 1f - Mathf.Pow(1f - u, 3f);

            float y = Mathf.Lerp(startY, targetY, k);
            content.anchoredPosition = new Vector2(content.anchoredPosition.x, y);

            yield return null;

            Canvas.ForceUpdateCanvases();
            viewH = viewport.rect.height;
            contH = content.rect.height;
            targetY = Mathf.Max(0f, contH - viewH);
        }

        content.anchoredPosition = new Vector2(content.anchoredPosition.x, targetY);
        _smoothCo = null;
    }

    public void JumpToBottomImmediate()
    {
        if (!content || !viewport) return;
        Canvas.ForceUpdateCanvases();
        float targetY = Mathf.Max(0f, content.rect.height - viewport.rect.height);
        content.anchoredPosition = new Vector2(content.anchoredPosition.x, targetY);
    }
}
