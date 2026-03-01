using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// SpeedChanger
/// - GantryIKArticulation의 통합 속도값(XZ, YUp, YDown)을 UI로 표시/증감
/// - 버튼 OnClick에 연결해서 사용
/// </summary>
public class SpeedChanger : MonoBehaviour
{
    [Header("Target (필수)")]
    public GantryIKArticulation gantry; // 통합 속도 소스

    [Header("Display Texts")]
    public TMP_Text xzText;       // "XZ Speed: 0.50 m/s"
    public TMP_Text yUpText;      // "Y↑ Speed: 0.30 m/s"
    public TMP_Text yDownText;    // "Y↓ Speed: 0.30 m/s"

    [Header("Step & Clamp")]
    [Tooltip("버튼 1회당 증감 단위 (m/s)")]
    public float step = 0.1f;
    [Tooltip("최소 속도 (음수 방지 권장)")]
    public float minSpeed = 0f;
    [Tooltip("최대 속도 (필요 없으면 크게 설정)")]
    public float maxSpeed = 10f;

    [Header("Format")]
    [Tooltip("표시 포맷 (예: 0.00 → 소수 2자리)")]
    public string numberFormat = "0.00";

    void Reset()
    {
        step = 0.1f;
        minSpeed = 0f;
        maxSpeed = 10f;
        numberFormat = "0.00";
    }

    void OnEnable()
    {
        RefreshAll(); // 처음에 UI 싱크
    }

    void Update()
    {
        // 외부에서 값이 바뀌었을 수도 있으니 UI를 주기적으로 맞춰줌
        RefreshAll();
    }

    // ===== UI 표시 갱신 =====
    public void RefreshAll()
    {
        if (!gantry) return;

        if (xzText) xzText.text = $"XZ Speed: {gantry.SpeedXZ.ToString(numberFormat)} m/s";
        if (yUpText) yUpText.text = $"Y↑ Speed: {gantry.SpeedYUp.ToString(numberFormat)} m/s";
        if (yDownText) yDownText.text = $"Y↓ Speed: {gantry.SpeedYDown.ToString(numberFormat)} m/s";
    }

    // ===== 증감 공용 유틸 =====
    void AddSpeed(ref float field, float delta)
    {
        field = Mathf.Clamp(field + delta, minSpeed, maxSpeed);
    }

    // ===== XZ =====
    public void OnClick_XZ_Minus()
    {
        if (!gantry) return;
        AddSpeed(ref gantry.speedXZ, -Mathf.Abs(step));
        RefreshAll();
    }
    public void OnClick_XZ_Plus()
    {
        if (!gantry) return;
        AddSpeed(ref gantry.speedXZ, +Mathf.Abs(step));
        RefreshAll();
    }

    // ===== Y Up =====
    public void OnClick_YUp_Minus()
    {
        if (!gantry) return;
        AddSpeed(ref gantry.speedYUp, -Mathf.Abs(step));
        RefreshAll();
    }
    public void OnClick_YUp_Plus()
    {
        if (!gantry) return;
        AddSpeed(ref gantry.speedYUp, +Mathf.Abs(step));
        RefreshAll();
    }

    // ===== Y Down =====
    public void OnClick_YDown_Minus()
    {
        if (!gantry) return;
        AddSpeed(ref gantry.speedYDown, -Mathf.Abs(step));
        RefreshAll();
    }
    public void OnClick_YDown_Plus()
    {
        if (!gantry) return;
        AddSpeed(ref gantry.speedYDown, +Mathf.Abs(step));
        RefreshAll();
    }
}
