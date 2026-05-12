using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public sealed class UiJuice : MonoBehaviour
{
    public Coroutine Fade(CanvasGroup group, float targetAlpha, float duration)
    {
        return StartCoroutine(FadeRoutine(group, targetAlpha, duration));
    }

    public Coroutine Shake(RectTransform target, float strength, float duration)
    {
        return StartCoroutine(ShakeRoutine(target, strength, duration));
    }

    public Coroutine Pulse(Transform target, float scaleMultiplier, float duration)
    {
        return StartCoroutine(PulseRoutine(target, scaleMultiplier, duration));
    }

    public Coroutine Flash(Graphic graphic, Color flashColor, float duration)
    {
        return StartCoroutine(FlashRoutine(graphic, flashColor, duration));
    }

    private static IEnumerator FadeRoutine(CanvasGroup group, float targetAlpha, float duration)
    {
        if (group == null)
        {
            yield break;
        }

        float startAlpha = group.alpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            group.alpha = Mathf.Lerp(startAlpha, targetAlpha, Mathf.Clamp01(elapsed / duration));
            yield return null;
        }

        group.alpha = targetAlpha;
    }

    private static IEnumerator ShakeRoutine(RectTransform target, float strength, float duration)
    {
        if (target == null)
        {
            yield break;
        }

        Vector2 startPosition = target.anchoredPosition;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float fade = 1f - Mathf.Clamp01(elapsed / duration);
            target.anchoredPosition = startPosition + Random.insideUnitCircle * strength * fade;
            yield return null;
        }

        target.anchoredPosition = startPosition;
    }

    private static IEnumerator PulseRoutine(Transform target, float scaleMultiplier, float duration)
    {
        if (target == null)
        {
            yield break;
        }

        Vector3 startScale = target.localScale;
        Vector3 peakScale = startScale * scaleMultiplier;
        float halfDuration = duration * 0.5f;

        for (float elapsed = 0f; elapsed < halfDuration; elapsed += Time.deltaTime)
        {
            target.localScale = Vector3.Lerp(startScale, peakScale, Mathf.Clamp01(elapsed / halfDuration));
            yield return null;
        }

        for (float elapsed = 0f; elapsed < halfDuration; elapsed += Time.deltaTime)
        {
            target.localScale = Vector3.Lerp(peakScale, startScale, Mathf.Clamp01(elapsed / halfDuration));
            yield return null;
        }

        target.localScale = startScale;
    }

    private static IEnumerator FlashRoutine(Graphic graphic, Color flashColor, float duration)
    {
        if (graphic == null)
        {
            yield break;
        }

        Color startColor = graphic.color;
        float halfDuration = duration * 0.5f;

        for (float elapsed = 0f; elapsed < halfDuration; elapsed += Time.deltaTime)
        {
            graphic.color = Color.Lerp(startColor, flashColor, Mathf.Clamp01(elapsed / halfDuration));
            yield return null;
        }

        for (float elapsed = 0f; elapsed < halfDuration; elapsed += Time.deltaTime)
        {
            graphic.color = Color.Lerp(flashColor, startColor, Mathf.Clamp01(elapsed / halfDuration));
            yield return null;
        }

        graphic.color = startColor;
    }
}

public sealed class UiButtonJuice : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
{
    private Vector3 startScale;

    private void Awake()
    {
        startScale = transform.localScale;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        transform.localScale = startScale * 1.025f;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        transform.localScale = startScale;
    }

    public void OnPointerDown(PointerEventData eventData)
    {
        transform.localScale = startScale * 0.985f;
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        transform.localScale = startScale * 1.025f;
    }
}
