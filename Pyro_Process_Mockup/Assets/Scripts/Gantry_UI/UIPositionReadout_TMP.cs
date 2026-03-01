using UnityEngine;
using System.Text;
using TMPro;

public class UIPositionReadout_TMP : MonoBehaviour
{
    [Header("Target")]
    public Transform target;                  // 좌표를 표시할 대상

    [Header("Output Texts (assign 3 TMP_Texts)")]
    public TMP_Text xText;
    public TMP_Text yText;
    public TMP_Text zText;

    [Header("Options")]
    public bool useLocalSpace = false;        // false: World Position, true: Local Position
    [Range(0, 6)] public int decimalPlaces = 3;
    [Tooltip("초당 업데이트 횟수 (0이면 매 프레임)")]
    public float updateHz = 0f;
    public bool showUnits = false;            // 단위(m) 표시

    [Header("Prefix labels")]
    public string prefixX = "X: ";
    public string prefixY = "Y: ";
    public string prefixZ = "Z: ";

    float _acc;
    float _interval;
    StringBuilder _sb = new StringBuilder(32);

    void OnValidate()
    {
        _interval = (updateHz > 0f) ? (1f / updateHz) : 0f;
    }

    void Awake()
    {
        OnValidate();
    }

    void LateUpdate()
    {
        if (!target) return;

        if (_interval <= 0f)
        {
            UpdateTexts();
        }
        else
        {
            _acc += Time.unscaledDeltaTime; // UI는 unscaled로 갱신하는 것이 깔끔
            if (_acc >= _interval)
            {
                _acc = 0f;
                UpdateTexts();
            }
        }
    }

    void UpdateTexts()
    {
        Vector3 p = useLocalSpace ? target.localPosition : target.position;
        string fmt = "F" + decimalPlaces;

        if (xText) xText.text = BuildLine(prefixX, p.x, fmt);
        if (yText) yText.text = BuildLine(prefixY, p.y, fmt);
        if (zText) zText.text = BuildLine(prefixZ, p.z, fmt);
    }

    string BuildLine(string prefix, float v, string fmt)
    {
        _sb.Clear();
        _sb.Append(prefix);
        _sb.Append(v.ToString(fmt));
        if (showUnits) _sb.Append(" m");
        return _sb.ToString();
    }
}
