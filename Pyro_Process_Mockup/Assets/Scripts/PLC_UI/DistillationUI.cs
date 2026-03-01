using System.Collections;
using UnityEngine;
using TMPro;

public class DistillationUI : MonoBehaviour
{
    public Unity_PLC plc;

    // ========= 자동/수동 표시 =========
    [Header("자동/수동 라벨 오브젝트")]
    public GameObject autoBlue;    // 파란 자동
    public GameObject autoRed;     // 빨간 자동
    public GameObject manualRed;   // 빨간 수동
    public GameObject manualBlue;  // 파란 수동

    [Header("레버 이미지 (Down 없음)")]
    public GameObject leverUp;     // 올라간 레버 이미지만 사용(자동일 때만 표시)

    [Header("초기 상태")]
    public bool startAsManual = true; // true면 수동 시작, false면 자동 시작
    private bool _isAuto;

    // ========= 히터 제어(자동 모드 전용) =========
    public enum MotionState { Idle, Opening, Closing }

    [Header("Heater Transforms")]
    [SerializeField] private Transform leftHeater;
    [SerializeField] private Transform rightHeater;

    [Header("Heater Targets (Local Positions)")]
    [SerializeField] private Vector3 leftOpenPos = Vector3.zero;
    [SerializeField] private Vector3 rightOpenPos = Vector3.zero;
    [SerializeField] private Vector3 leftClosePos = new Vector3(0f, 0f, -0.36f);
    [SerializeField] private Vector3 rightClosePos = new Vector3(0f, 0f, 0.36f);

    [Header("Heater Speed")]
    [Tooltip("완전 닫힘↔완전 열림까지 걸리는 시간(초). 기본 44초")]
    [Min(0.001f)][SerializeField] private float fullTravelSeconds = 44f;

    [Header("Heater State (ReadOnly)")]
    [SerializeField, Range(0f, 1f)] private float t = 1f; // 0=완전 닫힘, 1=완전 열림
    [SerializeField] private MotionState state = MotionState.Idle;
    public float HeaterFractionOpen => t;
    public MotionState HeaterState => state;

    // ========= 끝단/텍스트 램프(빛나는 것만) =========
    [Header("완전 열림 점등 (LL, '열림')")]
    public GameObject LL_Lit;      // 빛나는 LL (끝단)
    public GameObject Open_Lit;    // 빛나는 '열림' (== 작은열림)

    [Header("완전 닫힘 점등 (RL, '닫힘')")]
    public GameObject RL_Lit;      // 빛나는 RL (끝단)
    public GameObject Close_Lit;   // 빛나는 '닫힘' (== 작은닫힘)

    [Header("끝 판정 임계값(깜빡임 방지)")]
    [Range(0f, 0.02f)] public float edgeThreshold = 0.001f;
    bool HeaterFullyOpen => t >= (1f - edgeThreshold);
    bool HeaterFullyClose => t <= edgeThreshold;

    // ========= 커다란 버튼: 동작중 빨간 오버레이 =========
    [Header("큰 버튼 빨간 오버레이(동작 중만 ON)")]
    public GameObject BigOpenRedOverlay;   // 열림 동작중 활성화
    public GameObject BigCloseRedOverlay;  // 닫힘 동작중 활성화

    // ========= PLC -> TextMeshPro (6개 값 표시) =========
    [Header("PLC Temperature Addresses (Word/U16)")]
    [Tooltip("설정온도 상부")] public string addr_Set_Upper;
    [Tooltip("설정온도 하부")] public string addr_Set_Lower;
    [Tooltip("현재온도 Left")] public string addr_Cur_LL;
    [Tooltip("현재온도 Right")] public string addr_Cur_RL;
    [Tooltip("현재온도 상부")] public string addr_Cur_LU;
    [Tooltip("현재온도 하부")] public string addr_Cur_RU;

    [Header("TextMeshPro Targets")]
    public TMP_Text txt_Set_Upper;
    public TMP_Text txt_Set_Lower;
    public TMP_Text txt_Cur_LL;
    public TMP_Text txt_Cur_RL;
    public TMP_Text txt_Cur_LU;
    public TMP_Text txt_Cur_RU;

    // ▼ 추가: 각 현재온도를 두 번째 텍스트에도 동일 표시
    [Header("TextMeshPro Targets (Duplicates)")]
    public TMP_Text txt_Cur_LL_2;
    public TMP_Text txt_Cur_RL_2;
    public TMP_Text txt_Cur_LU_2;
    public TMP_Text txt_Cur_RU_2;

    [Header("Format & Polling")]
    public float displayScale = 1.0f;
    public string numberFormat = "0";
    [Min(0.05f)] public float readInterval = 0.2f;
    Coroutine _readLoop;

    // ========= 상/하부 히터 가동/정지 버튼 & 램프 =========
    [Header("상부 히터 버튼 & 램프 오브젝트")]
    public GameObject upperRunBlue, upperRunRed, upperStopBlue, upperStopRed;
    [Header("하부 히터 버튼 & 램프 오브젝트")]
    public GameObject lowerRunBlue, lowerRunRed, lowerStopBlue, lowerStopRed;

    [Header("상/하부 히터 제어용 PLC 주소")]
    public string addr_UpperRun = "M02030";
    public string addr_UpperStop = "M02032";
    public string addr_UpperRunLamp = "M02031";
    public string addr_UpperStopLamp = "M02033";
    public string addr_LowerRun = "M02040";
    public string addr_LowerStop = "M02042";
    public string addr_LowerRunLamp = "M02041";
    public string addr_LowerStopLamp = "M02043";

    [Header("버튼 신호 펄스 지속시간(초)")]
    [Min(0.02f)] public float pulseWidthSec = 0.12f;

    // ========= 자동 상태 동기화 =========
    [Header("자동 모드 상태(PLC에서 읽어 동기화)")]
    public string addr_AutoState = "M02001";

    // ========= 수동 모드: ‘누르는 동안 동작’ 주소 =========
    [Header("수동 모드 유지 주소(누르는 동안 ON)")]
    public string addr_ManualOpen = "M02010";   // 수동 열림 유지
    public string addr_ManualClose = "M02012";  // 수동 닫힘 유지

    bool _holdOpenUI, _holdCloseUI;       // 유니티 버튼이 누르는 중
    bool _holdOpenPLC, _holdClosePLC;     // PLC 패널에서 온 유지 비트
    bool ManualOpenCmd => _holdOpenUI || _holdOpenPLC;
    bool ManualCloseCmd => _holdCloseUI || _holdClosePLC;

    [Header("알람 주소 (M-bit)")]
    public string addr_AlarmUpperHeater = "M02050";
    public string addr_AlarmLowerHeater = "M02051";
    public string addr_AlarmUpperSensorOpen = "M02054";
    public string addr_AlarmLowerSensorOpen = "M02055";

    [Header("알람 UI 오브젝트")]
    // 히터 알람: ON -> 비활성화, OFF -> 활성화
    public GameObject ui_AlarmUpperHeater;
    public GameObject ui_AlarmLowerHeater;
    // 센서단선 알람: ON -> 활성화, OFF -> 비활성화
    public GameObject ui_AlarmUpperSensorOpen;
    public GameObject ui_AlarmLowerSensorOpen;

    [Header("알람 폴링 간격(초)")]
    [Min(0.2f)] public float alarmPollInterval = 1.0f;

    Coroutine _alarmLoop;
    bool? _prevUpHeater, _prevLoHeater, _prevUpSensor, _prevLoSensor;

    [Header("자동 모드: PLC 패널 버튼 비트(감시용)")]
    public string addr_AutoOpenPulse = "M0201C";
    public string addr_AutoStop = "M02014";
    public string addr_AutoClosePulse = "M0201D";

    bool _prevAutoOpen, _prevAutoStop, _prevAutoClose;

    //───────────────────────────────────────────────────────────────
    void Awake()
    {
        _isAuto = !startAsManual;
        ApplyModeVisuals();   // PLC에도 모드 반영(초기값)

        ApplyHeaterPose();
        UpdateEdgeIndicators();
        UpdateBigButtons();
    }

    void OnEnable()
    {
        if (_readLoop == null) _readLoop = StartCoroutine(ReadTempsLoop());
        if (_alarmLoop == null) _alarmLoop = StartCoroutine(AlarmPollLoop());

        // ★ 초기 1회만: 작은열림(M02020)으로 시작 상태 정렬
        bool smallOpen = SafeReadBool("M02020");
        t = smallOpen ? 1f : 0f;         // 초기 위치 확정
        state = MotionState.Idle;

        ApplyHeaterPose();
        UpdateEdgeIndicators();          // 이후엔 t 기반으로만 점등
        UpdateBigButtons();
    }

    void OnDisable()
    {
        if (_readLoop != null) StopCoroutine(_readLoop);
        _readLoop = null;

        if (_alarmLoop != null) StopCoroutine(_alarmLoop);
        _alarmLoop = null;
    }

    void Update()
    {
        if (_isAuto)
        {
            // 자동: 기존 로직 유지 (state 에 따라 이동)
            if (state != MotionState.Idle)
            {
                float dt = Time.deltaTime / Mathf.Max(0.001f, fullTravelSeconds);
                if (state == MotionState.Opening) t += dt;
                else if (state == MotionState.Closing) t -= dt;

                float tClamped = Mathf.Clamp01(t);
                bool hitEdge = !Mathf.Approximately(t, tClamped);
                t = tClamped;

                ApplyHeaterPose();
                if (hitEdge) state = MotionState.Idle;
            }
        }
        else
        {
            // 수동: 유지 비트(유니티/PLC) 상태에 따라 ‘누르는 동안’ 이동
            float dt = Time.deltaTime / Mathf.Max(0.001f, fullTravelSeconds);
            bool any = false;

            if (ManualOpenCmd && !HeaterFullyOpen && !ManualCloseCmd)
            {
                t = Mathf.Clamp01(t + dt);
                state = MotionState.Opening;
                any = true;
            }
            else if (ManualCloseCmd && !HeaterFullyClose && !ManualOpenCmd)
            {
                t = Mathf.Clamp01(t - dt);
                state = MotionState.Closing;
                any = true;
            }

            if (!any) state = MotionState.Idle;
            ApplyHeaterPose();
        }

        UpdateEdgeIndicators(); // ← 항상 t 기반으로 점등 (완전 열림/닫힘만 켜짐)
        UpdateBigButtons();
    }

    // ---------- 레버 토글 ----------
    public void ToggleLever()
    {
        _isAuto = !_isAuto;                 // 자동<->수동
        ApplyModeVisuals();                 // PLC 쓰기 포함
        if (!_isAuto)
        {
            state = MotionState.Idle;       // 수동 전환 시 즉시 정지
            // 안전: 수동 유지 비트/플래그 초기화
            _holdOpenUI = _holdCloseUI = false;
            plc?.WriteBool(addr_ManualOpen, false);
            plc?.WriteBool(addr_ManualClose, false);
        }
        UpdateBigButtons();
        UpdateEdgeIndicators();
    }

    // writeToPlc=false : UI만 갱신(PLC로 재쓰기 방지)
    void ApplyModeVisuals(bool writeToPlc = true)
    {
        if (leverUp) leverUp.SetActive(_isAuto);

        if (_isAuto)
        {
            if (autoRed) autoRed.SetActive(true);
            if (autoBlue) autoBlue.SetActive(false);
            if (manualBlue) manualBlue.SetActive(true);
            if (manualRed) manualRed.SetActive(false);

            if (writeToPlc) plc?.WriteBool("M02001", true);  // 기존 쓰기 유지
        }
        else
        {
            if (autoRed) autoRed.SetActive(false);
            if (autoBlue) autoBlue.SetActive(true);
            if (manualBlue) manualBlue.SetActive(false);
            if (manualRed) manualRed.SetActive(true);

            if (writeToPlc) plc?.WriteBool("M02001", false); // 기존 쓰기 유지
        }
    }

    // ---------- 자동 모드: 개폐 버튼 ----------
    public void OnHeaterOpen()
    {
        if (!_isAuto) return;
        if (HeaterFullyOpen) { state = MotionState.Idle; return; }
        state = MotionState.Opening;
        UpdateBigButtons();

        plc?.WriteBool("M02014", false);   // 기존 쓰기 유지
        plc?.WriteBool("M0201C", true);    // OPEN pulse
    }
    public void OnHeaterStop()
    {
        state = MotionState.Idle;
        ApplyHeaterPose();
        UpdateBigButtons();
        UpdateEdgeIndicators();

        plc?.WriteBool("M02014", true);    // 기존 쓰기 유지(Stop)
    }
    public void OnHeaterClose()
    {
        if (!_isAuto) return;
        if (HeaterFullyClose) { state = MotionState.Idle; return; }
        state = MotionState.Closing;
        UpdateBigButtons();

        plc?.WriteBool("M02014", false);   // 기존 쓰기 유지
        plc?.WriteBool("M0201D", true);    // CLOSE pulse
    }

    // ---------- 수동 모드: 누르고 있는 동안 ON/OFF (UI 이벤트에 연결) ----------
    public void OnManualOpenDown()
    {
        if (_isAuto) return;
        _holdOpenUI = true;
        if (!string.IsNullOrWhiteSpace(addr_ManualOpen)) plc?.WriteBool(addr_ManualOpen, true);
    }
    public void OnManualOpenUp()
    {
        if (_isAuto) return;
        _holdOpenUI = false;
        if (!string.IsNullOrWhiteSpace(addr_ManualOpen)) plc?.WriteBool(addr_ManualOpen, false);
    }
    public void OnManualCloseDown()
    {
        if (_isAuto) return;
        _holdCloseUI = true;
        if (!string.IsNullOrWhiteSpace(addr_ManualClose)) plc?.WriteBool(addr_ManualClose, true);
    }
    public void OnManualCloseUp()
    {
        if (_isAuto) return;
        _holdCloseUI = false;
        if (!string.IsNullOrWhiteSpace(addr_ManualClose)) plc?.WriteBool(addr_ManualClose, false);
    }

    // ---------- 내부 보조 ----------
    void ApplyHeaterPose()
    {
        if (leftHeater)
            leftHeater.localPosition = Vector3.Lerp(leftClosePos, leftOpenPos, t);
        if (rightHeater)
            rightHeater.localPosition = Vector3.Lerp(rightClosePos, rightOpenPos, t);
    }

    // ★ 램프는 '항상 t 기반' (완전 열림/닫힘만 켜짐, 중간값이면 둘 다 OFF)
    void UpdateEdgeIndicators()
    {
        bool openLit = HeaterFullyOpen;
        bool closeLit = HeaterFullyClose;

        if (LL_Lit) LL_Lit.SetActive(openLit);
        if (Open_Lit) Open_Lit.SetActive(openLit);

        if (RL_Lit) RL_Lit.SetActive(closeLit);
        if (Close_Lit) Close_Lit.SetActive(closeLit);
    }

    void UpdateBigButtons()
    {
        bool opening = (_isAuto && state == MotionState.Opening)
                       || (!_isAuto && ManualOpenCmd && !ManualCloseCmd);
        bool closing = (_isAuto && state == MotionState.Closing)
                       || (!_isAuto && ManualCloseCmd && !ManualOpenCmd);

        if (BigOpenRedOverlay) BigOpenRedOverlay.SetActive(opening);
        if (BigCloseRedOverlay) BigCloseRedOverlay.SetActive(closing);
    }

    public void SetHeaterFraction(float fraction01, bool stop = true)
    {
        t = Mathf.Clamp01(fraction01);
        if (stop) state = MotionState.Idle;
        ApplyHeaterPose();
        UpdateEdgeIndicators(); // 램프는 t기반
        UpdateBigButtons();
    }
    public void SetFullTravelSeconds(float seconds)
    {
        fullTravelSeconds = Mathf.Max(0.001f, seconds);
    }
    void OnValidate()
    {
        t = Mathf.Clamp01(t);
        fullTravelSeconds = Mathf.Max(0.001f, fullTravelSeconds);
        if (Application.isEditor && !Application.isPlaying)
        {
            ApplyHeaterPose();
            UpdateEdgeIndicators();
            UpdateBigButtons();
        }
    }

    // ========= PLC 폴링 =========
    IEnumerator ReadTempsLoop()
    {
        int slice = 0; // 0~3
        var wait = new WaitForSeconds(Mathf.Max(0.05f, readInterval));

        while (true)
        {
            if (plc == null) { yield return wait; continue; }

            switch (slice)
            {
                case 0:
                    // 자동 상태 동기화 (PLC -> Unity)
                    if (!string.IsNullOrWhiteSpace(addr_AutoState))
                    {
                        bool plcAuto = SafeReadBool(addr_AutoState);
                        if (plcAuto != _isAuto)
                        {
                            _isAuto = plcAuto;
                            ApplyModeVisuals(writeToPlc: false);
                            UpdateBigButtons();
                            UpdateEdgeIndicators();
                        }
                    }

                    // 온도 (설정)
                    SafeReadToTMP(addr_Set_Upper, txt_Set_Upper);
                    SafeReadToTMP(addr_Set_Lower, txt_Set_Lower);
                    break;

                case 1:
                    // 온도 (현재 4종) — 각 항목을 2개 텍스트에 동일 표시
                    SafeReadToTMP_Double(addr_Cur_LL, txt_Cur_LL, txt_Cur_LL_2);
                    SafeReadToTMP_Double(addr_Cur_RL, txt_Cur_RL, txt_Cur_RL_2);
                    SafeReadToTMP_Double(addr_Cur_LU, txt_Cur_LU, txt_Cur_LU_2);
                    SafeReadToTMP_Double(addr_Cur_RU, txt_Cur_RU, txt_Cur_RU_2);
                    break;

                case 2:
                    // 가동/정지 램프 동기화
                    {
                        bool uRun = SafeReadBool(addr_UpperRunLamp);
                        bool uStop = SafeReadBool(addr_UpperStopLamp);
                        bool lRun = SafeReadBool(addr_LowerRunLamp);
                        bool lStop = SafeReadBool(addr_LowerStopLamp);

                        if (uRun) SetUpperRunVisual(true);
                        else if (uStop) SetUpperRunVisual(false);

                        if (lRun) SetLowerRunVisual(true);
                        else if (lStop) SetLowerRunVisual(false);
                    }
                    break;

                case 3:
                    // (초기 이후엔 작은열림 주소는 읽지 않음) → 빈 슬라이스로 두어 부하 분산 유지
                    break;
            }

            // 슬라이스 간 프레임 분리 → 부하 분산
            slice = (slice + 1) & 3;   // 0~3 순환
            yield return null;

            // 한 바퀴 돌면 readInterval 대기 (기존 주기 유지)
            if (slice == 0) yield return wait;
        }
    }

    // ========= 알람(느린 폴링) =========
    IEnumerator AlarmPollLoop()
    {
        while (true)
        {
            yield return new WaitForSeconds(Mathf.Max(0.2f, alarmPollInterval));
            if (plc == null) continue;

            bool upHeater = SafeReadBool(addr_AlarmUpperHeater);
            bool loHeater = SafeReadBool(addr_AlarmLowerHeater);
            bool upSensor = SafeReadBool(addr_AlarmUpperSensorOpen);
            bool loSensor = SafeReadBool(addr_AlarmLowerSensorOpen);

            if (_prevUpHeater != upHeater)
            {
                // 히터 알람: ON -> 비활성화, OFF -> 활성화
                SafeSetActive(ui_AlarmUpperHeater, !upHeater);
                _prevUpHeater = upHeater;
            }
            if (_prevLoHeater != loHeater)
            {
                SafeSetActive(ui_AlarmLowerHeater, !loHeater);
                _prevLoHeater = loHeater;
            }
            if (_prevUpSensor != upSensor)
            {
                // 센서단선 알람: ON -> 활성화, OFF -> 비활성화
                SafeSetActive(ui_AlarmUpperSensorOpen, upSensor);
                _prevUpSensor = upSensor;
            }
            if (_prevLoSensor != loSensor)
            {
                SafeSetActive(ui_AlarmLowerSensorOpen, loSensor);
                _prevLoSensor = loSensor;
            }
        }
    }

    void SafeReadToTMP(string addr, TMP_Text target)
    {
        if (string.IsNullOrWhiteSpace(addr) || target == null) return;
        try
        {
            ushort raw = plc.ReadU16(addr);
            float scaled = raw * displayScale;
            target.text = scaled.ToString(numberFormat);
        }
        catch { /* ignore */ }
    }

    // ▼ 추가: 같은 주소를 두 개 TMP_Text에 동시에 표시
    void SafeReadToTMP_Double(string addr, TMP_Text t1, TMP_Text t2)
    {
        if (string.IsNullOrWhiteSpace(addr) || (t1 == null && t2 == null)) return;
        try
        {
            ushort raw = plc.ReadU16(addr);
            float scaled = raw * displayScale;
            string txt = scaled.ToString(numberFormat);
            if (t1 != null) t1.text = txt;
            if (t2 != null) t2.text = txt;
        }
        catch { /* ignore */ }
    }

    bool SafeReadBool(string addr)
    {
        if (string.IsNullOrWhiteSpace(addr)) return false;
        try { return plc.ReadBool(addr); }
        catch { return false; }
    }

    // 상/하부 히터: 유니티 버튼 → PLC 펄스 + 색전환
    public void OnUpperHeaterRun()
    {
        SetUpperRunVisual(true);
        if (!string.IsNullOrEmpty(addr_UpperRun))
            StartCoroutine(PulseBit(addr_UpperRun));
    }
    public void OnUpperHeaterStop()
    {
        SetUpperRunVisual(false);
        if (!string.IsNullOrEmpty(addr_UpperStop))
            StartCoroutine(PulseBit(addr_UpperStop));
    }
    public void OnLowerHeaterRun()
    {
        SetLowerRunVisual(true);
        if (!string.IsNullOrEmpty(addr_LowerRun))
            StartCoroutine(PulseBit(addr_LowerRun));
    }
    public void OnLowerHeaterStop()
    {
        SetLowerRunVisual(false);
        if (!string.IsNullOrEmpty(addr_LowerStop))
            StartCoroutine(PulseBit(addr_LowerStop));
    }

    void SetUpperRunVisual(bool running)
    {
        if (upperRunBlue) upperRunBlue.SetActive(!running);
        if (upperRunRed) upperRunRed.SetActive(running);
        if (upperStopBlue) upperStopBlue.SetActive(running);
        if (upperStopRed) upperStopRed.SetActive(!running);
    }
    void SetLowerRunVisual(bool running)
    {
        if (lowerRunBlue) lowerRunBlue.SetActive(!running);
        if (lowerRunRed) lowerRunRed.SetActive(running);
        if (lowerStopBlue) lowerStopBlue.SetActive(running);
        if (lowerStopRed) lowerStopRed.SetActive(!running);
    }

    IEnumerator PulseBit(string addr)
    {
        if (plc == null || string.IsNullOrWhiteSpace(addr)) yield break;
        plc.WriteBool(addr, true);
        yield return new WaitForSeconds(Mathf.Max(0.02f, pulseWidthSec));
        plc.WriteBool(addr, false);
    }

    // ===== 보조 =====
    void SafeSetActive(GameObject go, bool active)
    {
        if (go && go.activeSelf != active) go.SetActive(active);
    }
}
