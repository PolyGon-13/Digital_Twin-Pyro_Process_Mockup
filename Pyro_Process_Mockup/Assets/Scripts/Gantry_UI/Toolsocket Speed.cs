using UnityEngine;
using TMPro;

public class UIArticulationSpeedReadout : MonoBehaviour
{
    [Header("Target (ToolSocket)")]
    public Transform target;            // ToolSocket Transform

    [Header("UI")]
    public TMP_Text speedText;          // 출력할 TMP Text

    [Header("Options")]
    public bool useLocalSpace = false;  // 로컬 좌표 기준 여부
    [Range(0, 6)] public int decimalPlaces = 2;
    public string prefix = "Speed: ";
    public string unit = " m/s";

    [Tooltip("초당 업데이트 횟수 (0이면 매 프레임)")]
    public float updateHz = 0f;

    // 내부 상태
    private Vector3 lastPos;
    private float acc;       // 누적 시간 (unscaled)
    private float interval;  // 1 / Hz (0이면 매 프레임)

    void OnValidate()
    {
        interval = (updateHz > 0f) ? (1f / updateHz) : 0f;
    }

    void Start()
    {
        OnValidate();
        if (target)
            lastPos = useLocalSpace ? target.localPosition : target.position;
    }

    void LateUpdate()
    {
        if (!target) return;

        if (interval <= 0f)
        {
            UpdateOnce();
        }
        else
        {
            acc += Time.unscaledDeltaTime;   // UI 갱신은 unscaled 기준
            if (acc >= interval)
            {
                acc = 0f;
                UpdateOnce();
            }
        }
    }

    void UpdateOnce()
    {
        Vector3 current = useLocalSpace ? target.localPosition : target.position;

        // 속도 = 위치 변화량 / 실제 경과 시간 (scaled delta)
        float speed = (current - lastPos).magnitude / Mathf.Max(Time.deltaTime, 1e-6f);
        lastPos = current;

        if (speedText)
            speedText.text = prefix + speed.ToString("F" + decimalPlaces) + unit;
    }
}
