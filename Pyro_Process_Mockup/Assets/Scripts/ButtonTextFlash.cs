using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

[RequireComponent(typeof(Button))]
public class ButtonTextFlash : MonoBehaviour
{
    public TMP_Text targetText;                 // 버튼 안의 TMP_Text
    public Color flashColor = Color.yellow;     // 눌렀을 때 색
    public float flashTime = 0.1f;              // 유지시간

    private Color originalColor;
    private Coroutine flashCo;

    void Awake()
    {
        if (targetText == null)
            targetText = GetComponentInChildren<TMP_Text>();

        if (targetText != null)
            originalColor = targetText.color;

        // 버튼 클릭 시 Flash 실행
        GetComponent<Button>().onClick.AddListener(() => Flash());
    }

    public void Flash()
    {
        if (targetText == null) return;
        if (flashCo != null) StopCoroutine(flashCo);
        flashCo = StartCoroutine(FlashRoutine());
    }

    IEnumerator FlashRoutine()
    {
        targetText.color = flashColor;
        yield return new WaitForSecondsRealtime(flashTime);
        targetText.color = originalColor;
        flashCo = null;
    }
}
