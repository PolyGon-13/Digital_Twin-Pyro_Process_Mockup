using UnityEngine;
using TMPro;

public class UILogger : MonoBehaviour
{
    public static UILogger Instance;

    [Header("UI Target")]
    public TMP_Text messageText;

    [Header("Options")]
    [Tooltip("최대 라인 수 (넘으면 오래된 로그부터 삭제)")]
    public int maxLines = 10;

    string _logBuffer = "";

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void Log(string msg)
    {
        Debug.Log(msg); // 기존 콘솔에도 출력
        _logBuffer += "Message: " + msg + "\n";

        // 최대 라인 수 관리
        string[] lines = _logBuffer.Split('\n');
        if (lines.Length > maxLines)
        {
            _logBuffer = string.Join("\n", lines, lines.Length - maxLines, maxLines);
        }

        if (messageText) messageText.text = _logBuffer;
    }
}
