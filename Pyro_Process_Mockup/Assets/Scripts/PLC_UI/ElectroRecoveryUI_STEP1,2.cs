using UnityEngine;
using TMPro;
using System.Collections;

public class ElectroRecoveryUI_STEP1_2 : MonoBehaviour
{
    [Header("PLC 연결")]
    public Unity_PLC plc;

    // ===== 자동/수동 토글 (ElectroRecoveryUI와 동일 패턴) =====
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

    [Header("PLC 주소(자동 상태 비트) - ElectroRecoveryUI와 동일")]
    public string addr_AutoState = "M01501";

    // ===== STEP1 주소 =====
    [Header("STEP1 - 주소")]
    // 위치(32bit)
    public string addr_S1_Pos1 = "D001604";
    public string addr_S1_Pos2 = "D001606";
    public string addr_S1_Pos3 = "D001608";
    // 속도/조그(16bit)
    public string addr_S1_Spd1 = "D001610";
    public string addr_S1_Spd2 = "D001612";
    public string addr_S1_Spd3 = "D001614";
    public string addr_S1_Jog = "D001618";
    // 숫자표시기(32bit)
    public string addr_S1_Num = "D001600";

    // ===== STEP1 상태등(LL/ALARM/ORG/RL) =====
    [Header("STEP1 - 상태등 주소 (LL/ALARM/ORG/RL)")]
    public string addr_S1_LL = "M0161F";
    public string addr_S1_ALARM = "M01610";
    public string addr_S1_ORG = "M01630"; // ORG 펄스와 구분: 상태램프 비트는 아래 별도
    public string addr_S1_RL = "M0161E";

    [Header("STEP1 - 상태등 이미지 오브젝트")]
    public GameObject s1_LL_Image;
    public GameObject s1_ALARM_Image;
    public GameObject s1_ORG_Image;
    public GameObject s1_RL_Image;

    // ===== STEP2 주소 =====
    [Header("STEP2 - 주소")]
    // 위치(32bit)
    public string addr_S2_Pos1 = "D001704";
    public string addr_S2_Pos2 = "D001706";
    public string addr_S2_Pos3 = "D001708";
    // 속도/조그(16bit)
    public string addr_S2_Spd1 = "D001710";
    public string addr_S2_Spd2 = "D001712";
    public string addr_S2_Spd3 = "D001714";
    public string addr_S2_Jog = "D001718";
    // 숫자표시기(32bit)
    public string addr_S2_Num = "D001700";

    // ===== STEP2 상태등(LL/ALARM/ORG/RL) =====
    [Header("STEP2 - 상태등 주소 (LL/ALARM/ORG/RL)")]
    public string addr_S2_LL = "M0171F";
    public string addr_S2_ALARM = "M01710";
    public string addr_S2_ORG = "M01730";
    public string addr_S2_RL = "M0171E";

    [Header("STEP2 - 상태등 이미지 오브젝트")]
    public GameObject s2_LL_Image;
    public GameObject s2_ALARM_Image;
    public GameObject s2_ORG_Image;
    public GameObject s2_RL_Image;

    // ===== MOVE (펄스/램프/이미지) =====
    [Header("STEP1 - MOVE 주소 (펄스/램프)")]
    public string addr_S1_Move1_Pulse = "M01617";
    public string addr_S1_Move1_Lamp = "M01618";
    public string addr_S1_Move2_Pulse = "M01619";
    public string addr_S1_Move2_Lamp = "M0161A";
    public string addr_S1_Move3_Pulse = "M0161B";
    public string addr_S1_Move3_Lamp = "M0161C";

    [Header("STEP1 - MOVE 빨간 버튼 이미지")]
    public GameObject s1_Move1_Image;
    public GameObject s1_Move2_Image;
    public GameObject s1_Move3_Image;

    [Header("STEP2 - MOVE 주소 (펄스/램프)")]
    public string addr_S2_Move1_Pulse = "M01717";
    public string addr_S2_Move1_Lamp = "M01718";
    public string addr_S2_Move2_Pulse = "M01719";
    public string addr_S2_Move2_Lamp = "M0171A";
    public string addr_S2_Move3_Pulse = "M0171B";
    public string addr_S2_Move3_Lamp = "M0171C";

    [Header("STEP2 - MOVE 빨간 버튼 이미지")]
    public GameObject s2_Move1_Image;
    public GameObject s2_Move2_Image;
    public GameObject s2_Move3_Image;

    [Header("MOVE 펄스 폭(초)")]
    [Min(0.01f)] public float movePulseWidth = 0.05f;

    // 펄스 상승엣지 감지를 위한 이전 상태
    bool _p_s1m1, _p_s1m2, _p_s1m3, _p_s2m1, _p_s2m2, _p_s2m3;

    // ===== ORG 버튼(각 STEP) =====
    [Header("ORG 버튼 (펄스/램프)")]
    public string addr_S1_Org_Pulse = "M01630";
    public string addr_S1_Org_Lamp = "M01611";
    public GameObject s1_Org_Image;

    public string addr_S2_Org_Pulse = "M01730";
    public string addr_S2_Org_Lamp = "M01711";
    public GameObject s2_Org_Image;

    bool _p_s1org, _p_s2org;

    // ===== 서보알람 리셋(각 STEP, 길게누름) =====
    [Header("서보알람 리셋 (STEP1/2)")]
    public string addr_S1_ServoReset_Hold = "M0160F"; // 누르는 동안 ON
    public string addr_S1_ServoLampCond = "M01610"; // 조건 ON일 때 빨강 활성
    public GameObject s1_ServoReset_Image;

    public string addr_S2_ServoReset_Hold = "M0170F";
    public string addr_S2_ServoLampCond = "M01710";
    public GameObject s2_ServoReset_Image;

    // ===== 알람 리셋(공통, 길게누름) =====
    [Header("알람 리셋 (공통)")]
    public string addr_AlarmReset_Hold = "M01620";
    public string addr_AlarmReset_LampCond = "M01621";
    public GameObject alarmReset_Image;

    // ===== 비상정지(토글) =====
    [Header("비상정지 (토글)")]
    public string addr_EStop = "M01508";
    public GameObject eStop_Image;

    // ===== 표시 대상 =====
    [Header("STEP1 - TMP 텍스트")]
    public TMP_Text s1_pos1;
    public TMP_Text s1_pos2;
    public TMP_Text s1_pos3;
    public TMP_Text s1_spd1;
    public TMP_Text s1_spd2;
    public TMP_Text s1_spd3;
    public TMP_Text s1_jog;
    public TMP_Text s1_num;

    [Header("STEP2 - TMP 텍스트")]
    public TMP_Text s2_pos1;
    public TMP_Text s2_pos2;
    public TMP_Text s2_pos3;
    public TMP_Text s2_spd1;
    public TMP_Text s2_spd2;
    public TMP_Text s2_spd3;
    public TMP_Text s2_jog;
    public TMP_Text s2_num;

    // ===== 포맷/스케일 =====
    [Header("표시 스케일/형식")]
    public float positionScale = 1.0f;       // 위치 값 스케일 (U32)
    public string positionFormat = "0.000";  // 위치 값 포맷
    public float speedScale = 1.0f;          // 속도 값 스케일 (U16)
    public string speedFormat = "0.000";     // 속도 값 포맷
    public float jogScale = 1.0f;            // JOG SPEED 스케일 (U16)
    public string jogFormat = "0.000";       // JOG SPEED 포맷
    public float numberScale = 1.0f;         // 숫자표시기 스케일 (U32)
    public string numberFormat = "0.000";    // 숫자표시기 포맷

    [Header("폴링 주기(초)")]
    [Min(0.05f)] public float readInterval = 0.2f;

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

    // ===== UI 버튼 핸들러 =====
    // MOVE (원샷)
    public void OnClick_S1_Move1() => StartCoroutine(SendPulse(addr_S1_Move1_Pulse, s1_Move1_Image));
    public void OnClick_S1_Move2() => StartCoroutine(SendPulse(addr_S1_Move2_Pulse, s1_Move2_Image));
    public void OnClick_S1_Move3() => StartCoroutine(SendPulse(addr_S1_Move3_Pulse, s1_Move3_Image));
    public void OnClick_S2_Move1() => StartCoroutine(SendPulse(addr_S2_Move1_Pulse, s2_Move1_Image));
    public void OnClick_S2_Move2() => StartCoroutine(SendPulse(addr_S2_Move2_Pulse, s2_Move2_Image));
    public void OnClick_S2_Move3() => StartCoroutine(SendPulse(addr_S2_Move3_Pulse, s2_Move3_Image));

    // ORG (MOVE와 동일한 원샷)
    public void OnClick_S1_Org() => StartCoroutine(SendPulse(addr_S1_Org_Pulse, s1_Org_Image));
    public void OnClick_S2_Org() => StartCoroutine(SendPulse(addr_S2_Org_Pulse, s2_Org_Image));

    // 서보알람리셋 (길게 누르는 동안 ON)
    public void OnDown_S1_ServoReset() { TryWriteBool(addr_S1_ServoReset_Hold, true); }
    public void OnUp_S1_ServoReset() { TryWriteBool(addr_S1_ServoReset_Hold, false); }
    public void OnDown_S2_ServoReset() { TryWriteBool(addr_S2_ServoReset_Hold, true); }
    public void OnUp_S2_ServoReset() { TryWriteBool(addr_S2_ServoReset_Hold, false); }

    // 알람리셋 (공통, 길게 누르는 동안 ON)
    public void OnDown_AlarmReset() { TryWriteBool(addr_AlarmReset_Hold, true); }
    public void OnUp_AlarmReset() { TryWriteBool(addr_AlarmReset_Hold, false); }

    // 비상정지 (토글)
    public void OnClick_EStopToggle()
    {
        bool cur = SafeReadBool(addr_EStop);
        TryWriteBool(addr_EStop, !cur);
    }

    IEnumerator SendPulse(string pulseAddr, GameObject pulseImage)
    {
        if (plc == null || string.IsNullOrWhiteSpace(pulseAddr)) yield break;

        // UI 피드백: 즉시 ON
        if (pulseImage) pulseImage.SetActive(true);

        bool wroteOn = false;
        try
        {
            plc.WriteBool(pulseAddr, true);
            wroteOn = true;
            yield return new WaitForSeconds(Mathf.Max(0.01f, movePulseWidth));
        }
        finally
        {
            if (wroteOn)
            {
                try { plc.WriteBool(pulseAddr, false); } catch { }
            }
        }
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
                    // 자동/수동 동기화
                    {
                        bool plcAuto = SafeReadBool(addr_AutoState);
                        if (plcAuto != _isAuto) { _isAuto = plcAuto; ApplyModeVisuals(writeToPlc: false); }
                    }
                    // STEP1 수치
                    SafeReadU32ToTMP(addr_S1_Pos1, s1_pos1, positionScale, positionFormat);
                    SafeReadU32ToTMP(addr_S1_Pos2, s1_pos2, positionScale, positionFormat);
                    SafeReadU32ToTMP(addr_S1_Pos3, s1_pos3, positionScale, positionFormat);
                    SafeReadU16ToTMP(addr_S1_Spd1, s1_spd1, speedScale, speedFormat);
                    SafeReadU16ToTMP(addr_S1_Spd2, s1_spd2, speedScale, speedFormat);
                    SafeReadU16ToTMP(addr_S1_Spd3, s1_spd3, speedScale, speedFormat);
                    SafeReadU16ToTMP(addr_S1_Jog, s1_jog, jogScale, jogFormat);
                    SafeReadU32ToTMP(addr_S1_Num, s1_num, numberScale, numberFormat);
                    break;

                case 1:
                    // STEP2 수치
                    SafeReadU32ToTMP(addr_S2_Pos1, s2_pos1, positionScale, positionFormat);
                    SafeReadU32ToTMP(addr_S2_Pos2, s2_pos2, positionScale, positionFormat);
                    SafeReadU32ToTMP(addr_S2_Pos3, s2_pos3, positionScale, positionFormat);
                    SafeReadU16ToTMP(addr_S2_Spd1, s2_spd1, speedScale, speedFormat);
                    SafeReadU16ToTMP(addr_S2_Spd2, s2_spd2, speedScale, speedFormat);
                    SafeReadU16ToTMP(addr_S2_Spd3, s2_spd3, speedScale, speedFormat);
                    SafeReadU16ToTMP(addr_S2_Jog, s2_jog, jogScale, jogFormat);
                    SafeReadU32ToTMP(addr_S2_Num, s2_num, numberScale, numberFormat);
                    break;

                case 2:
                    // MOVE/ORG (엣지 + 램프 게이트)
                    EdgeAndLampGate(addr_S1_Move1_Pulse, addr_S1_Move1_Lamp, s1_Move1_Image, ref _p_s1m1);
                    EdgeAndLampGate(addr_S1_Move2_Pulse, addr_S1_Move2_Lamp, s1_Move2_Image, ref _p_s1m2);
                    EdgeAndLampGate(addr_S1_Move3_Pulse, addr_S1_Move3_Lamp, s1_Move3_Image, ref _p_s1m3);
                    EdgeAndLampGate(addr_S2_Move1_Pulse, addr_S2_Move1_Lamp, s2_Move1_Image, ref _p_s2m1);
                    EdgeAndLampGate(addr_S2_Move2_Pulse, addr_S2_Move2_Lamp, s2_Move2_Image, ref _p_s2m2);
                    EdgeAndLampGate(addr_S2_Move3_Pulse, addr_S2_Move3_Lamp, s2_Move3_Image, ref _p_s2m3);
                    EdgeAndLampGate(addr_S1_Org_Pulse, addr_S1_Org_Lamp, s1_Org_Image, ref _p_s1org);
                    EdgeAndLampGate(addr_S2_Org_Pulse, addr_S2_Org_Lamp, s2_Org_Image, ref _p_s2org);
                    break;

                case 3:
                    // 상태등/서보/알람/비상정지
                    SafeSetActiveFromBool(addr_S1_LL, s1_LL_Image);
                    SafeSetActiveFromBool(addr_S1_ALARM, s1_ALARM_Image);
                    SafeSetActiveFromBool(addr_S1_RL, s1_RL_Image);
                    SafeSetActiveFromBool(addr_S1_ORG, s1_ORG_Image);

                    SafeSetActiveFromBool(addr_S2_LL, s2_LL_Image);
                    SafeSetActiveFromBool(addr_S2_ALARM, s2_ALARM_Image);
                    SafeSetActiveFromBool(addr_S2_RL, s2_RL_Image);
                    SafeSetActiveFromBool(addr_S2_ORG, s2_ORG_Image);

                    SafeSetActiveFromBool(addr_S1_ServoLampCond, s1_ServoReset_Image);
                    SafeSetActiveFromBool(addr_S2_ServoLampCond, s2_ServoReset_Image);
                    SafeSetActiveFromBool(addr_AlarmReset_LampCond, alarmReset_Image);

                    SafeSetActiveFromBool(addr_EStop, eStop_Image);
                    break;
            }

            // 다음 슬라이스로 / 슬라이스 사이를 프레임 분리
            slice = (slice + 1) & 3; // 0~3
            yield return null;       // 다음 프레임로 넘김
            if (slice == 0) yield return wait; // 한 바퀴 돌았으면 readInterval 대기
        }
    }


    void EdgeAndLampGate(string pulseAddr, string lampAddr, GameObject img, ref bool prevPulse)
    {
        if (img == null || string.IsNullOrWhiteSpace(pulseAddr) || string.IsNullOrWhiteSpace(lampAddr)) return;

        // 1) 램프 ON이면 무조건 OFF (완료)
        bool lamp = SafeReadBool(lampAddr);
        if (lamp && img.activeSelf) img.SetActive(false);

        // 2) 펄스 상승엣지 감지
        bool p = SafeReadBool(pulseAddr);
        if (p && !prevPulse) img.SetActive(true);
        prevPulse = p;
    }

    // ===== 보조 함수 =====
    void SafeReadU16ToTMP(string addr, TMP_Text target, float scale, string format)
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

    void SafeReadU32ToTMP(string addr, TMP_Text target, float scale, string format)
    {
        if (string.IsNullOrWhiteSpace(addr) || target == null) return;
        try
        {
            uint raw = plc.ReadU32(addr);
            float scaled = raw * scale;
            target.text = scaled.ToString(string.IsNullOrEmpty(format) ? "0" : format);
        }
        catch { }
    }

    void SafeSetActiveFromBool(string addr, GameObject go)
    {
        if (go == null || string.IsNullOrWhiteSpace(addr)) return;
        try
        {
            bool on = plc.ReadBool(addr);
            if (go.activeSelf != on) go.SetActive(on);
        }
        catch { }
    }

    bool SafeReadBool(string addr)
    {
        if (string.IsNullOrWhiteSpace(addr)) return false;
        try { return plc.ReadBool(addr); }
        catch { return false; }
    }

    void TryWriteBool(string addr, bool v)
    {
        if (string.IsNullOrWhiteSpace(addr) || plc == null) return;
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
