using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// 유니티 <-> PLC 화면전환 양방향 동기화 (다중 화면 일반화, 모드 토글/레이스 방지)
public class ScreenSyncPLC : MonoBehaviour
{
    [Header("PLC 연결 (Unity_PLC 인스턴스)")]
    [SerializeField] private Unity_PLC plc;

    [Header("PLC 주소 (LS XGT)")]
    [Tooltip("현재 화면번호 Word (PLC→Unity 동기화용 읽기 전용)")]
    [SerializeField] private string word_ScreenNumber = "D00020";

    // enum 선언 앞의 [Header] 제거 —> 필드 쪽으로 이동
    public enum FollowMode { OnChange, ForceEveryPoll }

    [Header("PLC -> Unity (폴링)")]
    [Tooltip("OnChange: 값 변할 때만 적용 / ForceEveryPoll: 매 폴링마다 강제 적용")]
    [SerializeField] private FollowMode followMode = FollowMode.OnChange;

    [Min(0.05f)]
    [Tooltip("PLC Word 폴링 주기(초)")]
    [SerializeField] private float pollInterval = 0.2f;

    public enum WriteMode { LevelHold, Pulse }

    [Header("Unity -> PLC (쓰기)")]
    [Tooltip("LevelHold: M을 ON으로 유지(권장, Unity_PLC가 상호배타 정리)\nPulse: 상승엣지 필요 시 펄스 출력")]
    [SerializeField] private WriteMode writeMode = WriteMode.LevelHold;

    [Min(0.01f)]
    [Tooltip("Pulse 모드일 때 펄스 폭(초). PLC 스캔타임보다 약간 크게")]
    [SerializeField] private float pulseWidth = 0.06f;

    [Min(0.05f)]
    [Tooltip("버튼 전환 직후 이 시간 동안 폴링 무시(레이스 방지)")]
    [SerializeField] private float ignorePollDuration = 0.30f;

    [Header("기본 화면 번호(알 수 없는 번호 수신 시 fallback)")]
    [SerializeField] private ushort defaultScreenNumber = 1;

    [System.Serializable]
    public class ScreenMap
    {
        [Tooltip("PLC 화면 번호 (예: 1,2,41,42...)")]
        public ushort screenNumber;
        [Tooltip("해당 화면을 트리거하는 PLC 비트(M주소). 비우면 '유니티만 전환'")]
        public string mBit;
        [Tooltip("이 화면의 유니티 패널 루트 오브젝트")]
        public GameObject panel;
    }

    [Header("화면 매핑 테이블")]
    [Tooltip("필요한 만큼 요소를 추가하고 번호/M비트/패널을 연결")]
    public ScreenMap[] maps;

    // ---- 내부 상태 ----
    private readonly Dictionary<ushort, ScreenMap> mapByNumber = new Dictionary<ushort, ScreenMap>();
    private ushort lastScreenValue = 0;
    private Coroutine pollLoop;
    private float ignorePollUntil = 0f;

    void Awake()
    {
        // 맵 빌드(중복 번호 경고)
        mapByNumber.Clear();
        var seen = new HashSet<ushort>();
        foreach (var m in maps)
        {
            if (m == null) continue;
            if (!seen.Add(m.screenNumber))
                Debug.LogWarning($"[ScreenSyncPLC] 중복 screenNumber: {m.screenNumber}");
            mapByNumber[m.screenNumber] = m;
        }

        // 시작 시 기본 화면만 ON
        ActivateOnly(defaultScreenNumber);
        lastScreenValue = defaultScreenNumber;
    }

    void OnEnable()
    {
        pollLoop = StartCoroutine(PollScreenWord());
    }

    void OnDisable()
    {
        if (pollLoop != null) StopCoroutine(pollLoop);
        pollLoop = null;
    }

    // ============= PLC -> Unity (폴링) =============
    IEnumerator PollScreenWord()
    {
        var wait = new WaitForSeconds(pollInterval);
        while (true)
        {
            yield return wait;

            if (Time.time < ignorePollUntil) // 버튼 직후 레이스 회피
                continue;

            ushort v;
            try
            {
                v = (plc != null) ? plc.ReadU16(word_ScreenNumber) : defaultScreenNumber;
            }
            catch
            {
                // 통신 에러시 다음 주기 재시도
                continue;
            }

            if (followMode == FollowMode.OnChange)
            {
                if (v != lastScreenValue)
                {
                    lastScreenValue = v;
                    ApplyScreen(v);
                }
            }
            else // ForceEveryPoll
            {
                lastScreenValue = v;
                ApplyScreen(v);
            }
        }
    }

    // ============= Unity -> PLC (버튼) =============
    /// <summary>인스펙터 OnClick: 화면 '번호'로 전환</summary>
    public void OnClick_GotoScreenByNumber(int screenNumber)
    {
        ushort sn = (ushort)Mathf.Clamp(screenNumber, 0, 65535);
        DoLocalAndPLC(sn);
    }

    /// <summary>인스펙터 OnClick: 맵 '인덱스'로 전환</summary>
    public void OnClick_GotoScreenByIndex(int mapIndex)
    {
        if (mapIndex < 0 || mapIndex >= maps.Length) return;
        var m = maps[mapIndex];
        if (m == null) return;
        DoLocalAndPLC(m.screenNumber);
    }

    private void DoLocalAndPLC(ushort screenNumber)
    {
        // 1) 로컬 UI 즉시 전환
        ApplyScreen(screenNumber);

        // 2) 폴링 무시 윈도우로 레이스 방지
        ignorePollUntil = Time.time + ignorePollDuration;

        // 3) PLC 쓰기
        ScreenMap m;
        if (mapByNumber.TryGetValue(screenNumber, out m) && !string.IsNullOrEmpty(m.mBit) && plc != null)
        {
            if (writeMode == WriteMode.LevelHold)
            {
                // Unity_PLC가 같은 그룹의 이전 M을 자동 OFF 해주므로 레벨 고정 ON만으로도 화면 전환됨
                SafeWriteBool(m.mBit, true);
            }
            else // Pulse
            {
                StartCoroutine(PulseBit(m.mBit));
            }
        }
    }

    private IEnumerator PulseBit(string mAddr)
    {
        // 강제 상승엣지: false -> (한 프레임) -> true -> (pulseWidth) -> false
        SafeWriteBool(mAddr, false);
        yield return null; // 한 프레임 대기(버퍼링/스캔타임 여유)
        SafeWriteBool(mAddr, true);
        yield return new WaitForSeconds(pulseWidth);
        SafeWriteBool(mAddr, false);
    }

    private void SafeWriteBool(string mAddr, bool value)
    {
        if (plc == null || string.IsNullOrEmpty(mAddr)) return;
        try { plc.WriteBool(mAddr, value); }
        catch (System.Exception e) { Debug.LogError($"[ScreenSyncPLC] WriteBool 실패 {mAddr}: {e.Message}"); }
    }

    // ============= 공통: 화면 적용 =============
    private void ApplyScreen(ushort scr)
    {
        if (mapByNumber.ContainsKey(scr))
        {
            ActivateOnly(scr);
        }
        else
        {
            Debug.LogWarning($"[ScreenSyncPLC] 미등록 화면번호 {scr}. 기본 화면으로 대체.");
            ActivateOnly(defaultScreenNumber);
        }
    }

    private void ActivateOnly(ushort target)
    {
        for (int i = 0; i < maps.Length; i++)
        {
            var m = maps[i];
            if (m == null || m.panel == null) continue;
            bool on = (m.screenNumber == target);
            if (m.panel.activeSelf != on)
                m.panel.SetActive(on);
        }
    }
}
