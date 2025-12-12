using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

public class AutoFadeOutTMP : MonoBehaviour
{
    public float delay = 0f;
    public float duration = 1.5f;

    private IEnumerator Start() {
        // 텍스트 찾기(자식 포함)
        var text = GetComponentInChildren<TextMeshProUGUI>(true);
        if (text == null) yield break;

        if (delay > 0f) yield return new WaitForSeconds(delay);

        var color = text.color;
        float startA = color.a;
        float t = 0f;

        while (t < duration) {
            t += Time.deltaTime;
            color.a = Mathf.Lerp(startA, 0f, t / duration);
            text.color = color;
            yield return null;
        }

        Destroy(gameObject);
    }
}
