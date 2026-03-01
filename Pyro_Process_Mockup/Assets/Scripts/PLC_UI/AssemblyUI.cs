using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class AssemblyUI : MonoBehaviour
{
    [Header("PLC Bridge")]
    [SerializeField] private Unity_PLC plc;

    [Header("UI Buttons (Unity-side)")]
    [SerializeField] private Button basketUpButton;    // 상승
    [SerializeField] private Button basketDownButton;  // 하강

    [Header("Lamp Images (read from PLC)")]
    [SerializeField] private GameObject upRedLamp;        // M02552 ON → 활성
    [SerializeField] private GameObject downRedLamp;      // M02557 ON → 활성
    [SerializeField] private GameObject upRedImageLinked; // ↑ 램프와 동기

    [Header("PLC Addresses")]
    [SerializeField] private string addrCmdUp = "M02550";   // 상승 명령 (레벨=ON)
    [SerializeField] private string addrCmdDown = "M02555"; // 하강 명령 (레벨=ON)
    [SerializeField] private string addrLampUp = "M02552";  // 상승 램프
    [SerializeField] private string addrLampDown = "M02557";// 하강 램프

    [Header("Timings")]
    [Tooltip("상반된 명령을 OFF한 뒤 ON으로 전환하기 전 대기 (초)")]
    [SerializeField, Min(0f)] private float commandSettleDelay = 0.02f;
    [Tooltip("램프 폴링 주기 (초)")]
    [SerializeField, Min(0.01f)] private float pollIntervalSeconds = 0.05f;

    [Header("ElectroRecovery_Process (time/offset source)")]
    [SerializeField] private ElectroRecovery_Process ElectroRecovery_Process; // 시간/오프셋/정밀도
    [SerializeField] private Transform assemblyCylinderOverride;              // 없으면 ElectroRecovery_Process.assemblyCylinder
    [SerializeField] private float cylinderPosEpsOverride = -1f;             // 없으면 ElectroRecovery_Process.cylinderPosEps

    [Header("(Optional) External hooks")]
    public UnityEvent OnRiseTriggered;
    public UnityEvent OnLowerTriggered;

    // 상태
    private enum LiftState { Unknown, Raised, Lowered, MovingUp, MovingDown }
    private LiftState _state = LiftState.Unknown;

    private Transform _cyl;
    private float _startY;   // 상승 기준 Y
    private float _loweredY; // 하강 목표 Y
    private Coroutine _moveCo;

    private float _pollTimer;
    private bool _prevUpLamp, _prevDownLamp;

    // ─────────────────────────────────────────────────────────────────────────────
    private void Reset() { AutoWireRefs(); }
    private void OnValidate() { AutoWireRefs(); }
    private void AutoWireRefs()
    {
#if UNITY_2023_1_OR_NEWER
        plc = plc ?? Object.FindFirstObjectByType<Unity_PLC>(FindObjectsInactive.Include);
        ElectroRecovery_Process = ElectroRecovery_Process ?? Object.FindFirstObjectByType<ElectroRecovery_Process>(FindObjectsInactive.Include);
#else
        plc       = plc       ?? Object.FindObjectOfType<Unity_PLC>();
        ElectroRecovery_Process = ElectroRecovery_Process ?? Object.FindObjectOfType<ElectroRecovery_Process>();
#endif
    }

    private void Awake()
    {
        if (basketUpButton) basketUpButton.onClick.AddListener(UI_Rise);
        if (basketDownButton) basketDownButton.onClick.AddListener(UI_Lower);
    }

    private void Start()
    {
        // 실린더 타겟
        _cyl = assemblyCylinderOverride
             ? assemblyCylinderOverride
             : (ElectroRecovery_Process ? ElectroRecovery_Process.assemblyCylinder : null);

        if (_cyl == null) Debug.LogError("[AssemblyUI] assemblyCylinder 미지정");
        if (ElectroRecovery_Process == null) Debug.LogError("[AssemblyUI] ElectroRecovery_Process 미지정");

        if (_cyl && ElectroRecovery_Process)
        {
            _startY = _cyl.position.y;
            _loweredY = _startY - Mathf.Abs(ElectroRecovery_Process.cylinderDownOffset);

            // 1) PLC 램프 읽어 UI 램프에 반영
            bool upLamp = SafeReadBool(addrLampUp);
            bool downLamp = SafeReadBool(addrLampDown);
            ApplyLamps(upLamp, downLamp);

            // 2) ★ 초기 스냅: "유니티 상승 빨강(UI)" 활성 여부를 권위로 사용
            //    - 상승 빨강이 활성(activeSelf) → 상승 시작
            //    - 아니면 → 하강 시작
            bool upUIActive = (upRedLamp != null) && upRedLamp.activeSelf;
            bool downUIActive = (downRedLamp != null) && downRedLamp.activeSelf;

            if (upUIActive) SnapToRaised();
            else SnapToLowered();

            // 3) 에지 비교 기준도 UI 활성 상태로 초기화
            _prevUpLamp = upUIActive;
            _prevDownLamp = downUIActive;
        }
    }

    private void Update()
    {
        _pollTimer += Time.deltaTime;
        if (_pollTimer >= pollIntervalSeconds)
        {
            _pollTimer = 0f;
            PollPLC();
        }
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 유니티 버튼 → "레벨 제어(상반신호 OFF→목표 ON) + 유니티 모션"
    public void UI_Rise()
    {
        // 이미 상승 상태/상승중이면 무시
        if (_state == LiftState.Raised || _state == LiftState.MovingUp) return;

        // 1) PLC: 하강 명령 OFF → (지연) → 상승 명령 ON
        if (plc != null) StartCoroutine(SetLevelCommand(addrCmdDown, false, addrCmdUp, true));

        // 2) 유니티 실린더 즉시 상승 모션
        StartRiseMotion();

        OnRiseTriggered?.Invoke();
    }

    public void UI_Lower()
    {
        if (_state == LiftState.Lowered || _state == LiftState.MovingDown) return;

        if (plc != null) StartCoroutine(SetLevelCommand(addrCmdUp, false, addrCmdDown, true));

        StartLowerMotion();

        OnLowerTriggered?.Invoke();
    }

    private IEnumerator SetLevelCommand(string firstAddr, bool firstValue, string secondAddr, bool secondValue)
    {
        SafeWriteBool(firstAddr, firstValue);
        if (commandSettleDelay > 0f) yield return new WaitForSeconds(commandSettleDelay);
        SafeWriteBool(secondAddr, secondValue);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // PLC 폴링: 램프 UI 갱신 + 램프 변화에 맞춰 상태 스냅(패널 조작 반영)
    private void PollPLC()
    {
        if (plc == null) return;

        bool upLamp = SafeReadBool(addrLampUp);
        bool downLamp = SafeReadBool(addrLampDown);
        ApplyLamps(upLamp, downLamp);

        // 이전 로직(에지 기반 스냅)
        if (upLamp && !_prevUpLamp) SnapToRaised();
        if (downLamp && !_prevDownLamp) SnapToLowered();

        _prevUpLamp = upLamp;
        _prevDownLamp = downLamp;
    }

    private void ApplyLamps(bool upOn, bool downOn)
    {
        if (upRedLamp) upRedLamp.SetActive(upOn);
        if (upRedImageLinked) upRedImageLinked.SetActive(upOn);
        if (downRedLamp) downRedLamp.SetActive(downOn);
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // 모션 코루틴 (ElectroRecovery_Process 시간 사용)
    private void StartRiseMotion()
    {
        if (_cyl == null || ElectroRecovery_Process == null) return;
        if (_moveCo != null) StopCoroutine(_moveCo);
        _moveCo = StartCoroutine(RiseRoutine());
    }

    private void StartLowerMotion()
    {
        if (_cyl == null || ElectroRecovery_Process == null) return;
        if (_moveCo != null) StopCoroutine(_moveCo);
        _moveCo = StartCoroutine(LowerRoutine());
    }

    private IEnumerator RiseRoutine()
    {
        _state = LiftState.MovingUp;

        float seconds = Mathf.Max(0.0001f, ElectroRecovery_Process.cylinderMoveSeconds);
        float eps = GetCylinderPosEps();

        Vector3 start = _cyl.position;
        Vector3 target = new Vector3(start.x, _startY, start.z);
        if (Mathf.Abs(target.y - start.y) <= eps) { _state = LiftState.Raised; yield break; }

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / seconds;
            _cyl.position = new Vector3(target.x, Mathf.Lerp(start.y, target.y, Mathf.Clamp01(t)), target.z);
            yield return null;
        }
        _cyl.position = target;
        _state = LiftState.Raised;
    }

    private IEnumerator LowerRoutine()
    {
        _state = LiftState.MovingDown;

        float seconds = Mathf.Max(0.0001f, ElectroRecovery_Process.cylinderMoveSeconds);
        float eps = GetCylinderPosEps();

        Vector3 start = _cyl.position;
        Vector3 target = new Vector3(start.x, _loweredY, start.z);
        if (Mathf.Abs(target.y - start.y) <= eps) { _state = LiftState.Lowered; yield break; }

        float t = 0f;
        while (t < 1f)
        {
            t += Time.deltaTime / seconds;
            _cyl.position = new Vector3(target.x, Mathf.Lerp(start.y, target.y, Mathf.Clamp01(t)), target.z);
            yield return null;
        }
        _cyl.position = target;
        _state = LiftState.Lowered;
    }

    // 스냅(PLC 결과를 곧바로 반영)
    private void SnapToRaised()
    {
        if (_cyl == null) return;
        _cyl.position = new Vector3(_cyl.position.x, _startY, _cyl.position.z);
        _state = LiftState.Raised;
    }
    private void SnapToLowered()
    {
        if (_cyl == null) return;
        _cyl.position = new Vector3(_cyl.position.x, _loweredY, _cyl.position.z);
        _state = LiftState.Lowered;
    }

    private void GuessStateByPosition()
    {
        if (_cyl == null) { _state = LiftState.Unknown; return; }
        float eps = GetCylinderPosEps();
        float y = _cyl.position.y;
        if (Mathf.Abs(y - _startY) <= eps) _state = LiftState.Raised;
        else if (Mathf.Abs(y - _loweredY) <= eps) _state = LiftState.Lowered;
        else _state = LiftState.Unknown;
    }

    private float GetCylinderPosEps()
    {
        if (cylinderPosEpsOverride >= 0f) return cylinderPosEpsOverride;
        return ElectroRecovery_Process ? ElectroRecovery_Process.cylinderPosEps : 0.001f;
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // PLC I/O (예외 안전)
    private bool SafeReadBool(string addr)
    {
        try { return plc.ReadBool(addr); } catch { return false; }
    }
    private void SafeWriteBool(string addr, bool v)
    {
        try { plc.WriteBool(addr, v); } catch { }
    }
}
