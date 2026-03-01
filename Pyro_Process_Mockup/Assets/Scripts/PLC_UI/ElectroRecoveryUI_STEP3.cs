using UnityEngine;
using TMPro;
using System.Collections;

public class ElectroRecoveryUI_STEP3 : MonoBehaviour
{
    [Header("PLC 연결")]
    public Unity_PLC plc;

    // ===== 자동/수동 (공용) =====
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

    [Header("PLC 주소(자동 상태 비트, 공용)")]
    public string addr_AutoState = "M01501";

    // ===== STEP3 주소 (표 참고) =====
    [Header("STEP3 - 수치 주소")]
    // 위치(32bit)
    public string addr_Pos1 = "D001804";
    public string addr_Pos2 = "D001806";
    public string addr_Pos3 = "D001808";
    // 속도/조그(16bit)
    public string addr_Spd1 = "D001810";
    public string addr_Spd2 = "D001812";
    public string addr_Spd3 = "D001814";
    public string addr_Jog = "D001818";
    // 숫자표시기(32bit)
    public string addr_Number = "D001800";

    // ===== STEP3 상태등 =====
    [Header("STEP3 - 상태등 주소 (LL/ALARM/ORG/RL)")]
    public string addr_LL = "M0181F";
    public string addr_ALARM = "M01810";
    public string addr_ORG = "M01811"; // 상태 램프(참고: ORG 펄스/램프는 아래 별도)
    public string addr_RL = "M0181E";

    [Header("STEP3 - 상태등 이미지 오브젝트")]
    public GameObject img_LL;
    public GameObject img_ALARM;
    public GameObject img_ORG;
    public GameObject img_RL;

    // ===== MOVE (펄스/램프/이미지) =====
    [Header("STEP3 - MOVE 주소 (펄스/램프)")]
    public string addr_Move1_Pulse = "M01817";
    public string addr_Move1_Lamp = "M01818";
    public string addr_Move2_Pulse = "M01819";
    public string addr_Move2_Lamp = "M0181A";
    public string addr_Move3_Pulse = "M0181B";
    public string addr_Move3_Lamp = "M0181C";

    [Header("STEP3 - MOVE 빨간 버튼 이미지")]
    public GameObject img_Move1;
    public GameObject img_Move2;
    public GameObject img_Move3;

    [Header("MOVE 펄스 폭(초)")]
    [Min(0.01f)] public float movePulseWidth = 0.05f;

    // 펄스 상승엣지 감지용
    bool _p_m1, _p_m2, _p_m3;

    // ===== ORG 버튼 =====
    [Header("STEP3 - ORG (펄스/램프)")]
    public string addr_Org_Pulse = "M01830";
    public string addr_Org_Lamp = "M01811";
    public GameObject img_OrgBtn;
    bool _p_org;

    // ===== 서보알람 리셋 (길게누름) =====
    [Header("STEP3 - 서보알람 리셋")]
    public string addr_ServoReset_Hold = "M0180F"; // 누르는 동안 ON
    public string addr_ServoLamp_Cond = "M01810"; // 조건 ON이면 빨강 이미지 활성
    public GameObject img_ServoReset;

    // ===== 알람 리셋 (공용, 길게누름) =====
    [Header("알람 리셋 (공통)")]
    public string addr_AlarmReset_Hold = "M01620";
    public string addr_AlarmReset_LampCond = "M01621";
    public GameObject img_AlarmReset;

    // ===== 비상정지 (공용, 토글) =====
    [Header("비상정지 (공용, 토글)")]
    public string addr_EStop = "M01508";
    public GameObject img_EStop;

    // ===== 표시 대상 =====
    [Header("STEP3 - TMP 텍스트")]
    public TMP_Text txt_pos1;
    public TMP_Text txt_pos2;
    public TMP_Text txt_pos3;
    public TMP_Text txt_spd1;
    public TMP_Text txt_spd2;
    public TMP_Text txt_spd3;
    public TMP_Text txt_jog;
    public TMP_Text txt_number;

    // ===== 포맷/스케일 =====
    [Header("표시 스케일/형식")]
    public float positionScale = 1.0f;       // 위치(U32)
    public string positionFormat = "0.000";
    public float speedScale = 1.0f;          // 속도(U16)
    public string speedFormat = "0.000";
    public float jogScale = 1.0f;            // JOG(U16)
    public string jogFormat = "0.000";
    public float numberScale = 1.0f;         // 숫자표시기(U32)
    public string numberFormat = "0.000";

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

    // ===== UI 핸들러 =====
    // MOVE (원샷)
    public void OnClick_Move1() => StartCoroutine(SendPulse(addr_Move1_Pulse, img_Move1));
    public void OnClick_Move2() => StartCoroutine(SendPulse(addr_Move2_Pulse, img_Move2));
    public void OnClick_Move3() => StartCoroutine(SendPulse(addr_Move3_Pulse, img_Move3));

    // ORG (원샷)
    public void OnClick_Org() => StartCoroutine(SendPulse(addr_Org_Pulse, img_OrgBtn));

    // 서보알람리셋 (길게 누르는 동안 ON)
    public void OnDown_ServoReset() { TryWriteBool(addr_ServoReset_Hold, true); }
    public void OnUp_ServoReset() { TryWriteBool(addr_ServoReset_Hold, false); }

    // 알람리셋 (공용, 길게 누르는 동안 ON)
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
        int slice = 0; // 0~3
        var wait = new WaitForSeconds(Mathf.Max(0.05f, readInterval));

        while (true)
        {
            if (plc == null) { yield return wait; continue; }

            switch (slice)
            {
                case 0:
                    // 자동/수동 동기화 (PLC 우선)
                    {
                        bool plcAuto = SafeReadBool(addr_AutoState);
                        if (plcAuto != _isAuto)
                        {
                            _isAuto = plcAuto;
                            ApplyModeVisuals(writeToPlc: false);
                        }
                    }
                    // STEP3 수치 (위치/속도/조그/숫자)
                    SafeReadU32ToTMP(addr_Pos1, txt_pos1, positionScale, positionFormat);
                    SafeReadU32ToTMP(addr_Pos2, txt_pos2, positionScale, positionFormat);
                    SafeReadU32ToTMP(addr_Pos3, txt_pos3, positionScale, positionFormat);
                    SafeReadU16ToTMP(addr_Spd1, txt_spd1, speedScale, speedFormat);
                    SafeReadU16ToTMP(addr_Spd2, txt_spd2, speedScale, speedFormat);
                    SafeReadU16ToTMP(addr_Spd3, txt_spd3, speedScale, speedFormat);
                    SafeReadU16ToTMP(addr_Jog, txt_jog, jogScale, jogFormat);
                    SafeReadU32ToTMP(addr_Number, txt_number, numberScale, numberFormat);
                    break;

                case 1:
                    // MOVE/ORG (엣지 + 램프 게이트)
                    EdgeAndLampGate(addr_Move1_Pulse, addr_Move1_Lamp, img_Move1, ref _p_m1);
                    EdgeAndLampGate(addr_Move2_Pulse, addr_Move2_Lamp, img_Move2, ref _p_m2);
                    EdgeAndLampGate(addr_Move3_Pulse, addr_Move3_Lamp, img_Move3, ref _p_m3);
                    EdgeAndLampGate(addr_Org_Pulse, addr_Org_Lamp, img_OrgBtn, ref _p_org);
                    break;

                case 2:
                    // 상태등 (LL/ALARM/ORG/RL)
                    SafeSetActiveFromBool(addr_LL, img_LL);
                    SafeSetActiveFromBool(addr_ALARM, img_ALARM);
                    SafeSetActiveFromBool(addr_RL, img_RL);
                    SafeSetActiveFromBool(addr_ORG, img_ORG);
                    break;

                case 3:
                    // 공용 표시 (서보알람 램프/알람리셋/비상정지)
                    SafeSetActiveFromBool(addr_ServoLamp_Cond, img_ServoReset);
                    SafeSetActiveFromBool(addr_AlarmReset_LampCond, img_AlarmReset);
                    SafeSetActiveFromBool(addr_EStop, img_EStop);
                    break;
            }

            // 슬라이스 간 프레임 분리 → 렉 분산
            slice = (slice + 1) & 3; // 0~3 루프
            yield return null;

            // 한 바퀴 돌았으면 readInterval만큼 대기
            if (slice == 0) yield return wait;
        }
    }


    void EdgeAndLampGate(string pulseAddr, string lampAddr, GameObject img, ref bool prevPulse)
    {
        if (img == null || string.IsNullOrWhiteSpace(pulseAddr) || string.IsNullOrWhiteSpace(lampAddr)) return;

        // 램프가 켜지면(완료) 이미지 OFF
        bool lamp = SafeReadBool(lampAddr);
        if (lamp && img.activeSelf) img.SetActive(false);

        // 상승엣지 감지 → 이미지 ON
        bool p = SafeReadBool(pulseAddr);
        if (p && !prevPulse) img.SetActive(true);
        prevPulse = p;
    }

    // ===== 보조 =====
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
