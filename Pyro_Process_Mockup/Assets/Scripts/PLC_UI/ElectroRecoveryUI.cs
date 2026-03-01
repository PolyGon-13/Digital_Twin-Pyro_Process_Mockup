using UnityEngine;
using TMPro;
using System.Collections;

public class ElectroRecoveryUI : MonoBehaviour
{
    public Unity_PLC plc;

    // ===== 자동/수동 표시 =====
    [Header("자동/수동 라벨 오브젝트")]
    public GameObject autoBlue;
    public GameObject autoRed;
    public GameObject manualRed;
    public GameObject manualBlue;

    [Header("레버 이미지 (Up만 사용)")]
    public GameObject leverUp;

    [Header("초기 상태 설정")]
    public bool startAsManual = true;
    private bool _isAuto;

    [Header("PLC 주소(자동 상태 비트)")]
    public string addr_AutoState = "M01501";

    // ===== STEP1/2/3 위치 + 온도 4종 표시 =====
    [Header("PLC 주소(U16, D 영역)")]
    public string addr_STEP1 = "D001600";
    public string addr_STEP2 = "D001700";
    public string addr_STEP3 = "D001800";
    public string addr_HeaterSet = "D001500";
    public string addr_CurTemp_Control = "D001502";
    public string addr_CurTemp_1 = "D001506";
    public string addr_CurTemp_2 = "D001508";

    [Header("TextMeshPro 대상 - STEP")]
    public TMP_Text txt_STEP1;
    public TMP_Text txt_STEP2;
    public TMP_Text txt_STEP3;

    [Header("TextMeshPro 대상 - 온도(설정)")]
    public TMP_Text txt_HeaterSet;

    // === 현재온도 3종을 여러 개에 동시 표시 ===
    [Header("TextMeshPro 대상 - 현재온도(제어용) 여러 개")]
    public TMP_Text[] txts_CurTemp_Control = new TMP_Text[0];
    [Header("TextMeshPro 대상 - 현재온도(1) 여러 개")]
    public TMP_Text[] txts_CurTemp_1 = new TMP_Text[0];
    [Header("TextMeshPro 대상 - 현재온도(2) 여러 개")]
    public TMP_Text[] txts_CurTemp_2 = new TMP_Text[0];

    // (선택) 레거시 단일 필드도 함께 갱신
    [Header("레거시 단일 필드(선택)")]
    public TMP_Text legacy_CurTemp_Control;
    public TMP_Text legacy_CurTemp_1;
    public TMP_Text legacy_CurTemp_2;

    // ===== 그룹별 스케일 & 포맷 =====
    [Header("표시 스케일/형식(그룹별)")]
    public float stepScale = 1.0f;
    public string stepNumberFormat = "0.000";
    public float tempScale = 1.0f;
    public string tempNumberFormat = "0.0";

    [Header("폴링 주기(초)")]
    [Min(0.05f)] public float readInterval = 0.2f;

    // ===== 알람 및 탈착 상태 =====
    [Header("알람 및 탈착 PLC 주소")]
    public string addr_HeaterAlarm = "M01550";  // ON → 이미지 활성
    public string addr_SensorAlarm = "M01554";  // ON → 이미지 활성
    public string addr_Detach1 = "P0010A";
    public string addr_Detach2 = "P0010B";
    public string addr_Detach3 = "P0010C";
    public string addr_Detach4 = "P0010D";
    public string addr_Detach5 = "P0010E";
    public string addr_Detach6 = "P0010F";

    [Header("알람 및 탈착 이미지 오브젝트")]
    public GameObject ui_HeaterAlarm;   // ON → 활성화
    public GameObject ui_SensorAlarm;   // ON → 활성화
    public GameObject ui_Detach1;
    public GameObject ui_Detach2;
    public GameObject ui_Detach3;
    public GameObject ui_Detach4;
    public GameObject ui_Detach5;
    public GameObject ui_Detach6;

    // ===== (신규) 히터 가동/정지 버튼 & 램프 =====
    [Header("히터 가동/정지 버튼 색상 오브젝트")]
    public GameObject runBlue, runRed, stopBlue, stopRed; // DistillationUI 패턴과 동일 시각화

    [Header("히터 가동/정지 제어용 PLC 주소")]
    public string addr_Run = "M01540";   // 버튼 펄스: 가동
    public string addr_Stop = "M01542";  // 버튼 펄스: 정지
    public string addr_RunLamp = "M01541"; // 가동 램프(상태 표시)
    public string addr_StopLamp = "M01543"; // 정지 램프(상태 표시)

    [Header("버튼 신호 펄스 지속시간(초)")]
    [Min(0.02f)] public float pulseWidthSec = 0.12f;

    [Header("히터가동 아이콘(별도)")]
    public GameObject ui_HeaterRunning; // RunLamp=ON → 활성, StopLamp=ON → 비활성

    // ===== (신규) 알람리셋(공통) =====
    [Header("알람 리셋(공통)")]
    public string addr_AlarmReset_Hold = "M01620"; // 누르는 동안 ON
    public string addr_AlarmReset_LampCond = "M01621"; // 조건 ON이면 빨강 이미지 활성
    public GameObject alarmReset_Image;

    Coroutine _pollLoop;

    // ===== 생명주기 =====
    void Awake()
    {
        _isAuto = !startAsManual;
        ApplyModeVisuals();
    }

    void OnEnable()
    {
        if (_pollLoop == null) _pollLoop = StartCoroutine(PollLoop());
    }

    void OnDisable()
    {
        if (_pollLoop != null) StopCoroutine(_pollLoop);
        _pollLoop = null;
    }

    // ===== 레버 토글 =====
    public void ToggleLever()
    {
        _isAuto = !_isAuto;
        ApplyModeVisuals();
    }

    void ApplyModeVisuals(bool writeToPlc = true)
    {
        if (leverUp) leverUp.SetActive(_isAuto);

        if (_isAuto)
        {
            if (autoRed) autoRed.SetActive(true);
            if (autoBlue) autoBlue.SetActive(false);
            if (manualBlue) manualBlue.SetActive(true);
            if (manualRed) manualRed.SetActive(false);
            if (writeToPlc) plc?.WriteBool(addr_AutoState, true);
        }
        else
        {
            if (autoRed) autoRed.SetActive(false);
            if (autoBlue) autoBlue.SetActive(true);
            if (manualBlue) manualBlue.SetActive(false);
            if (manualRed) manualRed.SetActive(true);
            if (writeToPlc) plc?.WriteBool(addr_AutoState, false);
        }
    }

    // ===== 버튼 핸들러(가동/정지) =====
    public void OnHeaterRun()
    {
        SetRunVisual(true);
        if (!string.IsNullOrEmpty(addr_Run))
            StartCoroutine(PulseBit(addr_Run));
    }
    public void OnHeaterStop()
    {
        SetRunVisual(false);
        if (!string.IsNullOrEmpty(addr_Stop))
            StartCoroutine(PulseBit(addr_Stop));
    }

    // ===== 알람리셋(공통, 길게 누르는 동안 ON) =====
    public void OnDown_AlarmReset() { TryWriteBool(addr_AlarmReset_Hold, true); }
    public void OnUp_AlarmReset() { TryWriteBool(addr_AlarmReset_Hold, false); }

    void SetRunVisual(bool running)
    {
        // 버튼 색상 토글 (가동=Red on, 정지=Blue on)
        if (runBlue) runBlue.SetActive(!running);
        if (runRed) runRed.SetActive(running);
        if (stopBlue) stopBlue.SetActive(running);
        if (stopRed) stopRed.SetActive(!running);
    }

    IEnumerator PulseBit(string addr)
    {
        if (plc == null || string.IsNullOrWhiteSpace(addr)) yield break;
        plc.WriteBool(addr, true);
        yield return new WaitForSeconds(Mathf.Max(0.02f, pulseWidthSec));
        plc.WriteBool(addr, false);
    }

    // ===== PLC 폴링 =====
    IEnumerator PollLoop()
    {
        int slice = 0;
        var wait = new WaitForSeconds(Mathf.Max(0.05f, readInterval));

        while (true)
        {
            if (plc == null) { yield return wait; continue; }

            switch (slice)
            {
                case 0:
                    // 자동/수동
                    {
                        bool plcAuto = SafeReadBool(addr_AutoState);
                        if (plcAuto != _isAuto) { _isAuto = plcAuto; ApplyModeVisuals(writeToPlc: false); }
                    }
                    // STEP1~3
                    SafeReadToTMP(addr_STEP1, txt_STEP1, stepScale, stepNumberFormat);
                    SafeReadToTMP(addr_STEP2, txt_STEP2, stepScale, stepNumberFormat);
                    SafeReadToTMP(addr_STEP3, txt_STEP3, stepScale, stepNumberFormat);
                    break;

                case 1:
                    // 온도 4종
                    SafeReadToTMP(addr_HeaterSet, txt_HeaterSet, tempScale, tempNumberFormat);
                    SafeReadToTMPMulti(addr_CurTemp_Control, txts_CurTemp_Control, legacy_CurTemp_Control, tempScale, tempNumberFormat);
                    SafeReadToTMPMulti(addr_CurTemp_1, txts_CurTemp_1, legacy_CurTemp_1, tempScale, tempNumberFormat);
                    SafeReadToTMPMulti(addr_CurTemp_2, txts_CurTemp_2, legacy_CurTemp_2, tempScale, tempNumberFormat);
                    break;

                case 2:
                    // 알람/탈착
                    SafeSetActive(ui_HeaterAlarm, SafeReadBool(addr_HeaterAlarm));
                    SafeSetActive(ui_SensorAlarm, SafeReadBool(addr_SensorAlarm));
                    SafeSetActive(ui_Detach1, SafeReadBool(addr_Detach1));
                    SafeSetActive(ui_Detach2, SafeReadBool(addr_Detach2));
                    SafeSetActive(ui_Detach3, SafeReadBool(addr_Detach3));
                    SafeSetActive(ui_Detach4, SafeReadBool(addr_Detach4));
                    SafeSetActive(ui_Detach5, SafeReadBool(addr_Detach5));
                    SafeSetActive(ui_Detach6, SafeReadBool(addr_Detach6));
                    break;

                case 3:
                    // 가동/정지 램프 + 히터가동 아이콘 + 알람리셋 램프
                    {
                        bool runLamp = SafeReadBool(addr_RunLamp);
                        bool stopLamp = SafeReadBool(addr_StopLamp);
                        if (runLamp) SetRunVisual(true);
                        else if (stopLamp) SetRunVisual(false);
                        if (ui_HeaterRunning)
                        {
                            if (runLamp) SafeSetActive(ui_HeaterRunning, true);
                            else if (stopLamp) SafeSetActive(ui_HeaterRunning, false);
                        }
                        SafeSetActive(alarmReset_Image, SafeReadBool(addr_AlarmReset_LampCond));
                    }
                    break;
            }

            slice = (slice + 1) & 3;
            yield return null;       // 슬라이스 간 프레임 분리
            if (slice == 0) yield return wait; // 한 바퀴 후 readInterval 대기
        }
    }


    // ===== 보조 함수 =====
    void SafeReadToTMP(string addr, TMP_Text target, float scale, string format)
    {
        if (string.IsNullOrWhiteSpace(addr) || target == null) return;
        try
        {
            ushort raw = plc.ReadU16(addr);
            float scaled = raw * scale;
            target.text = scaled.ToString(string.IsNullOrEmpty(format) ? "0" : format);
        }
        catch { }
    }

    void SafeReadToTMPMulti(string addr, TMP_Text[] targets, TMP_Text legacy,
                            float scale, string format)
    {
        if (string.IsNullOrWhiteSpace(addr)) return;
        try
        {
            ushort raw = plc.ReadU16(addr);
            float scaled = raw * scale;
            string txt = scaled.ToString(string.IsNullOrEmpty(format) ? "0" : format);

            if (targets != null)
                for (int i = 0; i < targets.Length; i++)
                    if (targets[i] != null) targets[i].text = txt;

            if (legacy != null) legacy.text = txt;
        }
        catch { }
    }

    bool SafeReadBool(string addr)
    {
        if (string.IsNullOrWhiteSpace(addr)) return false;
        try { return plc.ReadBool(addr); }
        catch { return false; }
    }

    void SafeSetActive(GameObject go, bool active)
    {
        if (go && go.activeSelf != active)
            go.SetActive(active);
    }

    void TryWriteBool(string addr, bool v)
    {
        if (plc == null || string.IsNullOrWhiteSpace(addr)) return;
        try { plc.WriteBool(addr, v); } catch { }
    }

    void OnValidate()
    {
        readInterval = Mathf.Max(0.05f, readInterval);
        if (Application.isEditor && !Application.isPlaying)
        {
            if (leverUp) leverUp.SetActive(!startAsManual);
        }
    }
}
