using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class SequenceController : MonoBehaviour
{
    [Header("Process References")]
    public ElectroRecovery_Process electroRecoveryProcess;   // S1~S4
    public Distillation_Process distillationProcess;       // S5, S7, S8
    public LIBS libsProcess;               // S6 (분리된 LIBS)

    [Header("UI Buttons (0~8)")]
    public Button btn0_All;
    public Button btn1;
    public Button btn2;
    public Button btn3;
    public Button btn4;
    public Button btn5_BC_Load;
    public Button btn6_LIBS;
    public Button btn7_BC_Unload;
    public Button btn8_CollectAndFinish;

    [Header("Pause")]
    public Button btnPause;
    public Color pauseTint = new Color(0.4f, 0.7f, 1f, 1f);

    [Header("Running Visual")]
    public Color runningTint = new Color(1f, 0.9f, 0.3f, 1f);
    public bool disableButtonWhileRunning = true;

    // 지금 뭔가 하나라도 돌고 있는가
    private bool isRunning;

    // 지금 돌고 있는 코루틴 핸들
    private Coroutine currentRoutine;

    // 지금 돌고 있는 버튼 (0~8 중 하나, All 포함)
    private Button currentRunningButton;

    // 지금 돌고 있는 버튼의 인디케이터
    private ButtonIndicator currentIndicator;

    // 일시정지 상태
    private bool isPaused;

    // static 으로 노출해서 다른 공정들이 한 줄씩 끼워 넣을 수 있게 함
    private static bool s_Paused;

    private void Update()
    {
        // 실행 중일 때는 0~8 키보드 입력 안 받게 돼 있었음 → 이건 유지
        if (isRunning) return;

        if (Input.GetKeyDown(KeyCode.Alpha0)) OnClick_All(btn0_All);
        else if (Input.GetKeyDown(KeyCode.Alpha1)) OnClick_1(btn1);
        else if (Input.GetKeyDown(KeyCode.Alpha2)) OnClick_2(btn2);
        else if (Input.GetKeyDown(KeyCode.Alpha3)) OnClick_3(btn3);
        else if (Input.GetKeyDown(KeyCode.Alpha4)) OnClick_4(btn4);
        else if (Input.GetKeyDown(KeyCode.Alpha5)) OnClick_5_BCLoad(btn5_BC_Load);
        else if (Input.GetKeyDown(KeyCode.Alpha6)) OnClick_6_LIBS(btn6_LIBS);
        else if (Input.GetKeyDown(KeyCode.Alpha7)) OnClick_7_BCUnload(btn7_BC_Unload);
        else if (Input.GetKeyDown(KeyCode.Alpha8)) OnClick_8_Collect(btn8_CollectAndFinish);
    }

    // ========== Pause 버튼 ==========

    public void OnClick_Pause()
    {
        isPaused = !isPaused;
        s_Paused = isPaused;

        // 버튼 색 토글
        if (btnPause)
        {
            var g = btnPause.targetGraphic;
            if (g)
            {
                if (isPaused)
                    g.color = pauseTint;
                else
                    g.color = Color.white;
            }
        }
    }

    /// <summary>
    /// 다른 코루틴(공정)에서 이걸 한 줄씩 끼워 넣으면 SequenceController 의 Pause 에 맞춰 멈춘다.
    /// 예)
    /// while(어쩌구) {
    ///     ... 작업 ...
    ///     yield return SequenceController.WaitWhilePaused();
    /// }
    /// </summary>
    public static IEnumerator WaitWhilePaused()
    {
        while (s_Paused)
            yield return null;
    }

    // =====================================================
    // 버튼 핸들러
    // =====================================================

    public void OnClick_All(Button btn)
    {
        // 이미 All 이나 다른 게 돌고 있는 상태에서 같은 걸 다시 누르면 → 취소
        if (isRunning && currentRunningButton == btn)
        {
            CancelCurrent();
            return;
        }

        if (isRunning) return; // 다른 거 도는 중
        isRunning = true;
        currentRunningButton = btn;
        currentRoutine = StartCoroutine(RunWithIndicator(Run_All_Sequence(), btn));
    }

    public void OnClick_1(Button btn) => StartElectroIfPossible("RunSequence1", btn);
    public void OnClick_2(Button btn) => StartElectroIfPossible("RunSequence2", btn);
    public void OnClick_3(Button btn) => StartElectroIfPossible("RunSequence3", btn);
    public void OnClick_4(Button btn) => StartElectroIfPossible("RunSequence4", btn);

    public void OnClick_5_BCLoad(Button btn) =>
        StartDistillationIfPossible("Run_BC_Load", "Distillation_Process에 Run_BC_Load()가 필요합니다.", btn);

    // ★ 여기 6번이 이제 LIBS로 감
    public void OnClick_6_LIBS(Button btn) =>
        StartLibsIfPossible("Do_LIBS_Process", "LIBS에 Do_LIBS_Process()가 필요합니다.", btn);

    public void OnClick_7_BCUnload(Button btn) =>
        StartDistillationIfPossible("Run_BC_Unload", "Distillation_Process에 Run_BC_Unload()가 필요합니다.", btn);

    public void OnClick_8_Collect(Button btn) =>
        StartDistillationIfPossible("Run_Collect_And_Finish", "Distillation_Process에 Run_Collect_And_Finish()가 필요합니다.", btn);

    // =====================================================
    // 실행 래퍼들
    // =====================================================

    private void StartElectroIfPossible(string coroutineName, Button btn)
    {
        // 같은 버튼 다시 눌러서 취소
        if (isRunning && currentRunningButton == btn)
        {
            CancelCurrent();
            return;
        }

        if (isRunning) return;
        if (!electroRecoveryProcess)
        {
            Debug.LogWarning("[SequenceController] ElectroRecovery_Process 참조 없음.");
            return;
        }

        isRunning = true;
        currentRunningButton = btn;
        currentRoutine = StartCoroutine(RunWithIndicator(electroRecoveryProcess, coroutineName, btn));
    }

    private void StartDistillationIfPossible(string coroutineName, string warn, Button btn)
    {
        // 같은 버튼 다시 눌러서 취소
        if (isRunning && currentRunningButton == btn)
        {
            CancelCurrent();
            return;
        }

        if (isRunning) return;
        if (!distillationProcess)
        {
            Debug.LogWarning("[SequenceController] Distillation_Process 참조 없음.");
            return;
        }

        var hasMethod = distillationProcess.GetType().GetMethod(
            coroutineName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
        );
        if (hasMethod == null)
        {
            Debug.LogWarning($"[SequenceController] {warn}");
            return;
        }

        isRunning = true;
        currentRunningButton = btn;
        currentRoutine = StartCoroutine(RunWithIndicator(distillationProcess, coroutineName, btn));
    }

    private void StartLibsIfPossible(string coroutineName, string warn, Button btn)
    {
        // 같은 버튼 다시 눌러서 취소
        if (isRunning && currentRunningButton == btn)
        {
            CancelCurrent();
            return;
        }

        if (isRunning) return;
        if (!libsProcess)
        {
            Debug.LogWarning("[SequenceController] LIBS 참조 없음. (6번용)");
            return;
        }

        var hasMethod = libsProcess.GetType().GetMethod(
            coroutineName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic
        );
        if (hasMethod == null)
        {
            Debug.LogWarning($"[SequenceController] {warn}");
            return;
        }

        isRunning = true;
        currentRunningButton = btn;
        currentRoutine = StartCoroutine(RunWithIndicator(libsProcess, coroutineName, btn));
    }

    // ① 코루틴 자체를 내가 직접 만든 경우
    private IEnumerator RunWithIndicator(IEnumerator targetRoutine, Button btn)
    {
        currentIndicator = new ButtonIndicator(btn, runningTint, disableButtonWhileRunning);
        currentIndicator.On();

        yield return targetRoutine;

        currentIndicator.Off();
        currentIndicator = default;
        isRunning = false;
        currentRunningButton = null;
        currentRoutine = null;
    }

    // ② 다른 컴포넌트의 코루틴 이름만 아는 경우
    private IEnumerator RunWithIndicator(MonoBehaviour host, string coroutineName, Button btn)
    {
        currentIndicator = new ButtonIndicator(btn, runningTint, disableButtonWhileRunning);
        currentIndicator.On();

        // 이 코루틴 안에서는 pause 여부는 host 쪽에서 SequenceController.WaitWhilePaused() 를 호출해줘야 진짜 멈춘다.
        yield return host.StartCoroutine(coroutineName);

        currentIndicator.Off();
        currentIndicator = default;
        isRunning = false;
        currentRunningButton = null;
        currentRoutine = null;
    }

    // =====================================================
    // 0번 전체 실행
    // =====================================================
    private IEnumerator Run_All_Sequence()
    {
        // 1~4 : Electro
        if (electroRecoveryProcess)
        {
            yield return electroRecoveryProcess.StartCoroutine("RunSequence1");
            yield return electroRecoveryProcess.StartCoroutine("RunSequence2");
            yield return electroRecoveryProcess.StartCoroutine("RunSequence3");
            yield return electroRecoveryProcess.StartCoroutine("RunSequence4");
        }
        else
        {
            Debug.LogWarning("[SequenceController] electroRecoveryProcess 없음 → S1~S4 건너뜀");
        }

        // 5 : Distillation - BC Load
        if (distillationProcess)
        {
            var m = distillationProcess.GetType().GetMethod("Run_BC_Load",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (m != null)
                yield return distillationProcess.StartCoroutine("Run_BC_Load");
            else
                Debug.LogWarning("[SequenceController] Distillation에 Run_BC_Load 없음 → S5 건너뜀");
        }
        else
        {
            Debug.LogWarning("[SequenceController] distillationProcess 없음 → S5 건너뜀");
        }

        // 6 : 분리된 LIBS
        if (libsProcess)
        {
            var m = libsProcess.GetType().GetMethod("Do_LIBS_Process",
                System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (m != null)
                yield return libsProcess.StartCoroutine("Do_LIBS_Process");
            else
                Debug.LogWarning("[SequenceController] LIBS에 Do_LIBS_Process 없음 → S6 건너뜀");
        }
        else
        {
            Debug.LogWarning("[SequenceController] libsProcess 없음 → S6 건너뜀");
        }

        // 7, 8 : Distillation - BC Unload, Collect & Finish
        if (distillationProcess)
        {
            string[] tail = { "Run_BC_Unload", "Run_Collect_And_Finish" };
            foreach (var step in tail)
            {
                var m = distillationProcess.GetType().GetMethod(step,
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                if (m != null)
                    yield return distillationProcess.StartCoroutine(step);
                else
                    Debug.LogWarning($"[SequenceController] Distillation에 {step} 없음 → 건너뜀");
            }
        }
        else
        {
            Debug.LogWarning("[SequenceController] distillationProcess 없음 → S7~S8 건너뜀");
        }
    }

    // =====================================================
    // 취소 공통 처리
    // =====================================================
    private void CancelCurrent()
    {
        if (currentRoutine != null)
        {
            StopCoroutine(currentRoutine);
            currentRoutine = null;
        }

        if (!currentIndicator.Equals(default(ButtonIndicator)))
        {
            currentIndicator.Off();
            currentIndicator = default;
        }

        isRunning = false;
        currentRunningButton = null;
    }

    // =====================================================
    // 버튼 인디케이터
    // =====================================================
    private struct ButtonIndicator
    {
        private readonly Button btn;
        private readonly Graphic g;
        private readonly Color originalColor;
        private readonly bool originalInteractable;
        private readonly Color tint;
        private readonly bool disable;

        public ButtonIndicator(Button button, Color runningColor, bool disableWhileRunning)
        {
            btn = button;
            g = button ? button.targetGraphic : null;
            originalColor = g ? g.color : Color.white;
            originalInteractable = button ? button.interactable : true;
            tint = runningColor;
            disable = disableWhileRunning;
        }

        public void On()
        {
            if (!btn) return;
            if (g) g.color = tint;
            if (disable) btn.interactable = false;
        }

        public void Off()
        {
            if (!btn) return;
            if (g) g.color = originalColor;
            btn.interactable = originalInteractable;
        }
    }
}
