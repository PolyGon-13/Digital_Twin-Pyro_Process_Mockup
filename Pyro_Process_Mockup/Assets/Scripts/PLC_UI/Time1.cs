using UnityEngine;
using TMPro;
using System;
using System.Globalization;

public class Time1 : MonoBehaviour
{
    [Header("Assign your TextMeshProUGUI here")]
    public TextMeshProUGUI targetText;

    [Header("Format (C# DateTime)")]
    [Tooltip("e.g. yyyy/MM/dd (ddd) HH:mm:ss")]
    public string format = "yyyy/MM/dd (ddd) HH:mm:ss";

    [Header("Locale / Language")]
    [Tooltip("Use system locale if true. If false, use cultureName below.")]
    public bool useSystemLocale = false;

    [Tooltip("Examples: en-US, ko-KR, ja-JP")]
    public string cultureName = "en-US"; // (Wed) 영어 요일을 원하면 en-US, 한글 요일은 ko-KR

    [Header("Update Interval (seconds)")]
    [Tooltip("1.0이면 1초마다 갱신. 0으로 두면 매 프레임 갱신.")]
    public float updateInterval = 0.2f;

    private CultureInfo _culture;
    private float _timer;

    void Awake()
    {
        if (!targetText) targetText = GetComponent<TextMeshProUGUI>();
        _culture = useSystemLocale ? CultureInfo.CurrentCulture : new CultureInfo(cultureName);
        // 첫 표시 즉시
        if (targetText) targetText.text = DateTime.Now.ToString(format, _culture);
    }

    void Update()
    {
        if (!targetText) return;

        if (updateInterval <= 0f)
        {
            // 매 프레임
            targetText.text = DateTime.Now.ToString(format, _culture);
            return;
        }

        _timer += Time.unscaledDeltaTime;
        if (_timer >= updateInterval)
        {
            _timer = 0f;
            targetText.text = DateTime.Now.ToString(format, _culture);
        }
    }
}
