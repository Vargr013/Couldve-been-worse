using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public sealed class TypewriterTextEffect : MonoBehaviour
{
    private Coroutine activeRoutine;

    public void Play(Text target, string fullText, float characterDelay, Action completed = null)
    {
        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
        }

        activeRoutine = StartCoroutine(PlayRoutine(target, fullText, characterDelay, completed));
    }

    public void Stop(Text target = null)
    {
        if (activeRoutine != null)
        {
            StopCoroutine(activeRoutine);
            activeRoutine = null;
        }

        if (target != null)
        {
            target.text = string.Empty;
        }
    }

    private IEnumerator PlayRoutine(Text target, string fullText, float characterDelay, Action completed)
    {
        if (target == null)
        {
            activeRoutine = null;
            completed?.Invoke();
            yield break;
        }

        target.text = string.Empty;
        string safeText = fullText ?? string.Empty;

        for (int i = 0; i < safeText.Length; i++)
        {
            target.text = safeText.Substring(0, i + 1);
            yield return new WaitForSeconds(characterDelay);
        }

        activeRoutine = null;
        completed?.Invoke();
    }
}
