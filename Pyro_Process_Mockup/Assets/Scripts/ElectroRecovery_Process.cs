using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ElectroRecovery_Process : MonoBehaviour
{
    [Header("PLC (Assembly Cylinder)")]
    public Unity_PLC plc;
    [Tooltip("상승 명령 (레벨=ON)")] public string addrCmdUp = "M02550";
    [Tooltip("하강 명령 (레벨=ON)")] public string addrCmdDown = "M02555";
    [Tooltip("상승 램프(완료/동작 상태 판정)")] public string addrLampUp = "M02552";
    [Tooltip("하강 램프(완료/동작 상태 판정)")] public string addrLampDown = "M02557";

    [Header("PLC timings")]
    [Min(0f)] public float plcCommandSettleDelay = 0.02f;
    [Min(0.05f)] public float plcPollInterval = 0.1f;
    [Min(0.5f)] public float plcMaxWaitSec = 15f;

    [Header("Shared components")]
    public DockAndAttach dock;
    public GripAndMoveTest gripMoveTest;

    [Header("Actuator Raise (smoothing)")]
    public bool useSmoothRaise = true;
    public float smoothRaiseDuration = 0.35f;
    public AnimationCurve raiseEase = AnimationCurve.EaseInOut(0, 0, 1, 1);

    // =========================
    // (A) Sequence1 (UB.*)
    // =========================
    [Header("(S1-2) UB.L move (GripAndMoveTest)")]
    public Transform UB_L_GripPoint;
    public Transform UB_L_ReleasePoint;
    public Rigidbody UB_L_Rigidbody;
    public Transform UB_L_ReleaseParent;
    public float UB_L_LeftJawClose = 0.16f;
    public float UB_L_RightJawClose = -0.16f;

    [Header("(S1-3) UB.U move (GripAndMoveTest)")]
    public Transform UB_U_GripPoint;
    public Transform UB_U_ReleasePoint;
    public Rigidbody UB_U_Rigidbody;
    public Transform UB_U_ReleaseParent;
    public float UB_U_LeftJawClose = 0.16f;
    public float UB_U_RightJawClose = -0.16f;

    [Header("(S1) Merge: UB_L → UB.U_bottom")]
    public Transform UB_BottomRef;
    public Transform UB_L_HeadRef;
    public Transform UB_BottomParent;
    public bool disableUBLPhysicsWhenParented = true;
    public bool zeroVelocityOnTeleport = true;

    [Header("(S1) UB Actuator (shared, time-based)")]
    public Transform UB_Actuator;
    public float UB_ActuatorRaise = 0.2f;
    [Min(0.0001f)] public float UB_ActuatorMoveSeconds = 4.6f;
    public float UB_ActuatorPosEps = 0.001f;

    [Header("(S1-5) Dummy move (GripAndMoveTest)")]
    public Transform Dummy_GripPoint;
    public Transform Dummy_ReleasePoint;
    public Rigidbody Dummy_Rigidbody;
    public Transform Dummy_ReleaseParent;
    public float Dummy_LeftJawClose = 0.16f;
    public float Dummy_RightJawClose = -0.16f;

    [Header("(S1-6) Merged UB move (GripAndMoveTest)")]
    public Transform UB_GripPoint;
    public Transform UB_ReleasePoint_ForMove;
    public Rigidbody UB_Rigidbody;
    public Transform UB_ReleaseParent;
    public float UB_LeftJawClose = 0.16f;
    public float UB_RightJawClose = -0.16f;

    [Header("Assembly Cylinder (shared)")]
    public Transform assemblyCylinder;
    public float cylinderDownOffset = 0.3585665f;
    public float cylinderMoveSeconds = 4.6f;
    public float cylinderPosEps = 0.001f;

    [Header("(S1) Misc")]
    public float s1_gripToReleaseDelay = 1f;

    // =========================
    // (B) Sequence2 (USC.*)
    // =========================
    [Header("(S2) USC fixed params")]
    public Rigidbody USC_L_Rigidbody;
    public Transform USC_L_GripPoint;
    public Transform USC_L_ReleaseParent;
    public float USC_L_LeftJawClose = 0.16f;
    public float USC_L_RightJawClose = -0.16f;

    public Rigidbody USC_U_Rigidbody;
    public Transform USC_U_GripPoint;
    public Transform USC_U_ReleaseParent;
    public float USC_U_LeftJawClose = 0.16f;
    public float USC_U_RightJawClose = -0.16f;

    public Rigidbody USC_Rigidbody;
    public Transform USC_GripPoint;
    public Transform USC_ReleaseParent;
    public float USC_LeftJawClose = 0.16f;
    public float USC_RightJawClose = -0.16f;

    [Header("(S2) ReleasePoints per step")]
    public Transform USC_L_ReleasePoint_S2;
    public Transform USC_U_ReleasePoint_S2;
    public Transform S2_Dummy_ReleasePoint;
    public Transform USC_ReleasePoint_ForMove_S2;

    [Header("(S2) Dummy fixed params)")]
    public Rigidbody S2_Dummy_Rigidbody;
    public Transform S2_Dummy_GripPoint;
    public Transform S2_Dummy_ReleaseParent;
    public float S2_Dummy_LeftJawClose = 0.16f;
    public float S2_Dummy_RightJawClose = -0.16f;

    [Header("(S2) Merge: USC_L → USC.U_bottom")]
    public Transform USC_BottomRef;
    public Transform USC_L_HeadRef;
    public Transform USC_BottomParent;
    public bool disableUSCLPhysicsWhenParented = true;

    [Header("(S2) USC Actuator / Scraper (shared, time-based)")]
    public Transform USC_Actuator;
    public float USC_ActuatorRaise = 0.2f;
    [Min(0.0001f)] public float USC_ActuatorMoveSeconds = 4.6f;
    public float USC_ActuatorPosEps = 0.001f;

    public Transform USC_Scraper;
    public float scraperTravel = 0.05f;
    public float scraperCycleSeconds = 0.6f;
    public int scraperCycles = 5;
    public float scraperPosEps = 0.001f;

    [Header("(S2) Misc")]
    public float s2_gripToReleaseDelay = 1f;

    // =========================
    // (C) Sequence3 (USC.* → 분해/이동)
    // =========================
    [Header("(S3) USC merged move")]
    public Transform USC_GripPoint_S3;
    public Transform USC_A_ReleasePoint_S3;
    public Transform USC_ReleaseParent_S3;
    public float USC_LeftJawClose_S3 = 0.16f;
    public float USC_RightJawClose_S3 = -0.16f;

    [Header("(S3) Detach USC.L")]
    public Transform USCL_DetachedParent_S3;
    public Transform USC_L_Teleport_S3;
    public bool reenableUSCLPhysicsOnDetach_S3 = true;
    public bool reenableUSCLChildColliders_S3 = true;
    public bool zeroVelocityOnTeleport_S3 = true;
    public string layerName_USC_L = "USC.L";

    [Header("(S3) USC.D / USC.U / USC.L moves")]
    public Transform USC_D_GripPoint_S3;
    public Transform ER_USC_D_ReleasePoint_S3;
    public Rigidbody USC_D_Rigidbody_S3;
    public Transform USC_D_ReleaseParent_S3;
    public float USC_D_LeftJawClose_S3 = 0.16f;
    public float USC_D_RightJawClose_S3 = -0.16f;

    public Transform USC_U_GripPoint_S3;
    public Transform R_USC_U_ReleasePoint_S3;
    public Transform USC_U_ReleaseParent_S3;
    public float USC_U_LeftJawClose_S3 = 0.16f;
    public float USC_U_RightJawClose_S3 = -0.16f;

    public Transform USC_L_GripPoint_S3;
    public Transform R_USC_L_ReleasePoint_S3;
    public Transform USC_L_ReleaseParent_S3;
    public float USC_L_LeftJawClose_S3 = 0.16f;
    public float USC_L_RightJawClose_S3 = -0.16f;

    [Header("(S3) Misc")]
    public float s3_gripToReleaseDelay = 1f;

    // =========================
    // (D) Sequence4 (UB.* → 분해/이동)
    // =========================
    [Header("(S4) UB merged move")]
    public Transform UB_GripPoint_S4;
    public Transform A_ReleasePoint_S4;
    public Transform UB_ReleaseParent_S4;
    public float UB_LeftJawClose_S4 = 0.16f;
    public float UB_RightJawClose_S4 = -0.16f;

    [Header("(S4) Detach UB.L")]
    public Transform UBL_DetachedParent_S4;
    public Transform UB_L_Teleport_S4;
    public bool reenableUBLPhysicsOnDetach_S4 = true;
    public bool reenableUBLChildColliders_S4 = true;
    public bool zeroVelocityOnTeleport_S4 = true;
    public string layerName_UB_L = "UB.L";

    [Header("(S4) UB.D / UB.U / UB.L moves")]
    public Transform UB_D_GripPoint_S4;
    public Transform ER_UB_D_ReleasePoint_S4;
    public Rigidbody UB_D_Rigidbody_S4;
    public Transform UB_D_ReleaseParent_S4;
    public float UB_D_LeftJawClose_S4 = 0.16f;
    public float UB_D_RightJawClose_S4 = -0.16f;

    public Transform UB_U_GripPoint_S4;
    public Transform R_UB_U_ReleasePoint_S4;
    public Transform UB_U_ReleaseParent_S4;
    public float UB_U_LeftJawClose_S4 = 0.16f;
    public float UB_U_RightJawClose_S4 = -0.16f;

    public Transform UB_L_GripPoint_S4;
    public Transform R_UB_L_ReleasePoint_S4;
    public Transform UB_L_ReleaseParent_S4;
    public float UB_L_LeftJawClose_S4 = 0.16f;
    public float UB_L_RightJawClose_S4 = -0.16f;

    [Header("(S4) Misc")]
    public float s4_gripToReleaseDelay = 1f;

    [Header("(S3/S4) Explicit parent handle (optional)")]
    public Transform USCL_ParentHandle;
    public Transform UBL_ParentHandle;
    public Transform USCD_ParentHandle;
    public Transform UBD_ParentHandle;

    [Header("Debug")]
    public bool seqDebugLogs = false;

    bool _running;

    bool PLC_Read(string a) { try { return plc != null && !string.IsNullOrWhiteSpace(a) && plc.ReadBool(a); } catch { return false; } }
    void PLC_Write(string a, bool v) { try { if (plc != null && !string.IsNullOrWhiteSpace(a)) plc.WriteBool(a, v); } catch { } }

    // -------------------------------------------------
    // PLC 묶음 명령 (Pause 먼저)
    // -------------------------------------------------
    IEnumerator PLC_SetLevel(string firstAddr, bool firstVal, string secondAddr, bool secondVal, float settle)
    {
        yield return SequenceController.WaitWhilePaused();

        PLC_Write(firstAddr, firstVal);
        if (settle > 0f)
        {
            float t = 0f;
            while (t < settle)
            {
                yield return SequenceController.WaitWhilePaused();
                t += Time.deltaTime;
                yield return null;
            }
        }
        PLC_Write(secondAddr, secondVal);
    }

    IEnumerator PLC_WaitLamp(string lampAddr)
    {
        float t = 0f;
        while (t < plcMaxWaitSec)
        {
            yield return SequenceController.WaitWhilePaused();

            if (PLC_Read(lampAddr)) yield break;
            yield return new WaitForSeconds(plcPollInterval);
            t += plcPollInterval;
        }
        UILogger.Instance?.Log($"[PLC] 램프 타임아웃: {lampAddr}");
    }

    IEnumerator PLC_HoldRiseUntilDone(System.Func<IEnumerator> unityMove)
    {
        yield return SequenceController.WaitWhilePaused();

        if (plc)
            yield return PLC_SetLevel(addrCmdDown, false, addrCmdUp, true, plcCommandSettleDelay);

        if (unityMove != null)
        {
            yield return SequenceController.WaitWhilePaused();
            yield return unityMove();
        }

        if (!string.IsNullOrWhiteSpace(addrLampUp))
            yield return PLC_WaitLamp(addrLampUp);

        PLC_Write(addrCmdUp, false);
    }

    IEnumerator PLC_HoldLowerUntilDone(System.Func<IEnumerator> unityMove)
    {
        yield return SequenceController.WaitWhilePaused();

        if (plc)
            yield return PLC_SetLevel(addrCmdUp, false, addrCmdDown, true, plcCommandSettleDelay);

        if (unityMove != null)
        {
            yield return SequenceController.WaitWhilePaused();
            yield return unityMove();
        }

        if (!string.IsNullOrWhiteSpace(addrLampDown))
            yield return PLC_WaitLamp(addrLampDown);

        PLC_Write(addrCmdDown, false);
    }

    // -------------------------------------------------
    // Sequence 1 (UB.*)
    // -------------------------------------------------
    IEnumerator RunSequence1()
    {
        _running = true;

        // (1) Dock
        yield return SequenceController.WaitWhilePaused();
        if (dock && !dock.Is_Hg_Attached)
        {
            yield return dock.Start_Dock_Heavy_Gripper();
            float t = 0f;
            while (!dock.Is_Hg_Attached && t < 15f)
            {
                yield return SequenceController.WaitWhilePaused();
                t += Time.deltaTime;
                yield return null;
            }
        }

        // (2) UB.L
        yield return SequenceController.WaitWhilePaused();
        if (gripMoveTest && UB_L_GripPoint && UB_L_ReleasePoint && UB_L_Rigidbody)
            yield return RunGMT(UB_L_GripPoint, UB_L_ReleasePoint, UB_L_Rigidbody,
                UB_L_LeftJawClose, UB_L_RightJawClose, UB_L_ReleaseParent, s1_gripToReleaseDelay);

        // (3) Cylinder ↓ + UB.U
        var pre = new List<Coroutine>();
        if (assemblyCylinder && cylinderDownOffset > 0f)
        {
            var c = StartCoroutine(
                PLC_HoldLowerUntilDone(() => LowerCylinder_BySeconds(assemblyCylinder, cylinderDownOffset, cylinderMoveSeconds, cylinderPosEps))
            );
            pre.Add(c);
        }
        if (gripMoveTest && UB_U_GripPoint && UB_U_ReleasePoint && UB_U_Rigidbody)
        {
            var c = StartCoroutine(RunGMT(UB_U_GripPoint, UB_U_ReleasePoint, UB_U_Rigidbody,
                UB_U_LeftJawClose, UB_U_RightJawClose, UB_U_ReleaseParent, s1_gripToReleaseDelay));
            pre.Add(c);
        }
        foreach (var c in pre)
        {
            yield return SequenceController.WaitWhilePaused();
            yield return c;
        }

        // (5) Dummy
        yield return SequenceController.WaitWhilePaused();
        if (gripMoveTest && Dummy_GripPoint && Dummy_ReleasePoint && Dummy_Rigidbody)
            yield return RunGMT(Dummy_GripPoint, Dummy_ReleasePoint, Dummy_Rigidbody,
                Dummy_LeftJawClose, Dummy_RightJawClose, Dummy_ReleaseParent, s1_gripToReleaseDelay);

        // Merge → 1s → Actuator ↑
        yield return SequenceController.WaitWhilePaused();
        yield return MergeThenRaise_UB();

        // (6) UB merged
        yield return SequenceController.WaitWhilePaused();
        var ubRb = UB_Rigidbody ? UB_Rigidbody : UB_U_Rigidbody;
        if (gripMoveTest && UB_GripPoint && UB_ReleasePoint_ForMove && ubRb)
            yield return RunGMT(UB_GripPoint, UB_ReleasePoint_ForMove, ubRb,
                UB_LeftJawClose, UB_RightJawClose, UB_ReleaseParent, s1_gripToReleaseDelay);

        // (7) UB Actuator ↓
        yield return SequenceController.WaitWhilePaused();
        if (UB_Actuator) yield return LowerUBActuator();

        // (8) Cylinder ↑
        yield return SequenceController.WaitWhilePaused();
        if (assemblyCylinder && cylinderDownOffset > 0f)
            yield return PLC_HoldRiseUntilDone(() => RaiseCylinder_BySeconds(assemblyCylinder, cylinderDownOffset, cylinderMoveSeconds, cylinderPosEps));

        _running = false;
    }

    // -------------------------------------------------
    // Sequence 2 (USC.*)
    // -------------------------------------------------
    IEnumerator RunSequence2()
    {
        _running = true;

        // (2) USC.L
        yield return SequenceController.WaitWhilePaused();
        if (gripMoveTest && USC_L_GripPoint && USC_L_ReleasePoint_S2 && USC_L_Rigidbody)
            yield return RunGMT(USC_L_GripPoint, USC_L_ReleasePoint_S2, USC_L_Rigidbody,
                USC_L_LeftJawClose, USC_L_RightJawClose, USC_L_ReleaseParent, s2_gripToReleaseDelay);

        // (3) Cylinder ↓ + USC.U
        var pre2 = new List<Coroutine>();
        if (assemblyCylinder && cylinderDownOffset > 0f)
        {
            var c = StartCoroutine(
                PLC_HoldLowerUntilDone(() => LowerCylinder_BySeconds(assemblyCylinder, cylinderDownOffset, cylinderMoveSeconds, cylinderPosEps))
            );
            pre2.Add(c);
        }
        if (gripMoveTest && USC_U_GripPoint && USC_U_ReleasePoint_S2 && USC_U_Rigidbody)
        {
            var c = StartCoroutine(RunGMT(USC_U_GripPoint, USC_U_ReleasePoint_S2, USC_U_Rigidbody,
                USC_U_LeftJawClose, USC_U_RightJawClose, USC_U_ReleaseParent, s2_gripToReleaseDelay));
            pre2.Add(c);
        }
        foreach (var c in pre2)
        {
            yield return SequenceController.WaitWhilePaused();
            yield return c;
        }

        // (4) Dummy
        yield return SequenceController.WaitWhilePaused();
        if (gripMoveTest && S2_Dummy_GripPoint && S2_Dummy_ReleasePoint && S2_Dummy_Rigidbody)
            yield return RunGMT(S2_Dummy_GripPoint, S2_Dummy_ReleasePoint, S2_Dummy_Rigidbody,
                S2_Dummy_LeftJawClose, S2_Dummy_RightJawClose, S2_Dummy_ReleaseParent, s2_gripToReleaseDelay);

        // Merge → 1s → USC Actuator ↑
        yield return SequenceController.WaitWhilePaused();
        yield return MergeThenRaise_USC();

        // (6) USC merged
        yield return SequenceController.WaitWhilePaused();
        var uscRb = USC_Rigidbody ? USC_Rigidbody : USC_U_Rigidbody;
        if (gripMoveTest && USC_GripPoint && USC_ReleasePoint_ForMove_S2 && uscRb)
            yield return RunGMT(USC_GripPoint, USC_ReleasePoint_ForMove_S2, uscRb,
                USC_LeftJawClose, USC_RightJawClose, USC_ReleaseParent, s2_gripToReleaseDelay);

        // (7) USC Actuator ↓
        yield return SequenceController.WaitWhilePaused();
        if (USC_Actuator) yield return LowerUSCActuator();

        // (7-1) 전해정련 공정시작 & Scraper 왕복
        UILogger.Instance?.Log("전해정련 진행중");

        yield return SequenceController.WaitWhilePaused();
        yield return new WaitForSeconds(3f);

        if (USC_Scraper && scraperCycles > 0 && scraperTravel > 0f && scraperCycleSeconds > 0.0001f)
            yield return OscillateUSCScraper();

        _running = false;
    }

    // -------------------------------------------------
    // Sequence 3 (USC.* 분해/이동)
    // -------------------------------------------------
    IEnumerator RunSequence3()
    {
        _running = true;

        // (1) USC Actuator ↑
        yield return SequenceController.WaitWhilePaused();
        if (USC_Actuator) yield return RaiseUSCActuator();

        // (2) USC(merged) → A_ReleasePoint
        yield return SequenceController.WaitWhilePaused();
        if (gripMoveTest && USC_GripPoint_S3 && USC_A_ReleasePoint_S3 && (USC_Rigidbody || USC_U_Rigidbody))
        {
            var merged = USC_Rigidbody ? USC_Rigidbody : USC_U_Rigidbody;
            yield return RunGMT(USC_GripPoint_S3, USC_A_ReleasePoint_S3, merged,
                USC_LeftJawClose_S3, USC_RightJawClose_S3, USC_ReleaseParent_S3, s3_gripToReleaseDelay);
        }
        // USC.L freeze
        {
            var usclHandle = GetHandle(USC_L_Rigidbody, USCL_ParentHandle) ?? (USC_L_Rigidbody ? USC_L_Rigidbody.transform : null);
            if (usclHandle)
            {
                yield return SequenceController.WaitWhilePaused();
                EnsureRb(usclHandle, ref USC_L_Rigidbody, true);
                SetCollidersEnabled(usclHandle, false);
                ApplyFreezeAll(USC_L_Rigidbody);
                ApplyFreezeAll(USC_U_Rigidbody);
                Physics.SyncTransforms();
                yield return new WaitForFixedUpdate();
            }
        }

        // (3) USC Actuator ↓
        yield return SequenceController.WaitWhilePaused();
        if (USC_Actuator) yield return LowerUSCActuator();

        // (4) USC.L Detach
        yield return SequenceController.WaitWhilePaused();
        yield return Detach_USCL_From_USC_S3();

        // (5) USC.D → ER.USC.D
        Transform dRoot = USCD_ParentHandle
                          ? USCD_ParentHandle
                          : (USC_D_Rigidbody_S3 ? USC_D_Rigidbody_S3.transform
                                                : (USC_D_GripPoint_S3 ? USC_D_GripPoint_S3.root : null));
        if (dRoot)
        {
            yield return SequenceController.WaitWhilePaused();
            EnsureRb(dRoot, ref USC_D_Rigidbody_S3, applyStandard: false);
            ApplyCarryRbSettings(USC_D_Rigidbody_S3);
            SetCollidersEnabled(dRoot, true);
            USC_D_Rigidbody_S3.detectCollisions = true;
            USC_D_Rigidbody_S3.isKinematic = false;

            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
        }

        yield return SequenceController.WaitWhilePaused();
        if (gripMoveTest && USC_D_GripPoint_S3 && ER_USC_D_ReleasePoint_S3 && USC_D_Rigidbody_S3)
            yield return RunGMT(USC_D_GripPoint_S3, ER_USC_D_ReleasePoint_S3, USC_D_Rigidbody_S3,
                USC_D_LeftJawClose_S3, USC_D_RightJawClose_S3, USC_D_ReleaseParent_S3, s3_gripToReleaseDelay);

        // (6) USC.U → R_USC.U
        yield return SequenceController.WaitWhilePaused();
        if (gripMoveTest && USC_U_GripPoint_S3 && R_USC_U_ReleasePoint_S3 && USC_U_Rigidbody)
            yield return RunGMT(USC_U_GripPoint_S3, R_USC_U_ReleasePoint_S3, USC_U_Rigidbody,
                USC_U_LeftJawClose_S3, USC_U_RightJawClose_S3, USC_U_ReleaseParent_S3, s3_gripToReleaseDelay);

        // (7) USC.L 텔레포트 → 승객 상승
        {
            var t = GetHandle(USC_L_Rigidbody, USCL_ParentHandle) ?? (USC_L_Rigidbody ? USC_L_Rigidbody.transform : null);
            if (t)
            {
                yield return SequenceController.WaitWhilePaused();
                t.position = new Vector3(t.position.x, 0.4515f, t.position.z);
                Physics.SyncTransforms();
                yield return null;

                Restore_USCL_Layer_To_USCL_S3();

                if (reenableUSCLPhysicsOnDetach_S3 && USC_L_Rigidbody) ApplyStandardRbSettings(USC_L_Rigidbody);
                if (reenableUSCLChildColliders_S3) SetCollidersEnabled(t, true);
                USC_L_Rigidbody.linearVelocity = Vector3.zero;
                USC_L_Rigidbody.angularVelocity = Vector3.zero;
                USC_L_Rigidbody.isKinematic = true;

                if (assemblyCylinder && cylinderDownOffset > 0f)
                    yield return PLC_HoldRiseUntilDone(() => RaiseWithPassengerSmooth_BySeconds(assemblyCylinder, t, cylinderDownOffset, cylinderMoveSeconds, raiseEase));

                USC_L_Rigidbody.isKinematic = false;
                ApplyStandardRbSettings(USC_L_Rigidbody);
                Physics.SyncTransforms();
                yield return new WaitForFixedUpdate();
            }
        }

        // (8) USC.L → R.USC.L
        yield return SequenceController.WaitWhilePaused();
        if (gripMoveTest && USC_L_GripPoint_S3 && R_USC_L_ReleasePoint_S3 && USC_L_Rigidbody)
            yield return RunGMT(USC_L_GripPoint_S3, R_USC_L_ReleasePoint_S3, USC_L_Rigidbody,
                USC_L_LeftJawClose_S3, USC_L_RightJawClose_S3, USC_L_ReleaseParent_S3, s3_gripToReleaseDelay);

        // (9) 실린더 ↓
        yield return SequenceController.WaitWhilePaused();
        if (assemblyCylinder && cylinderDownOffset > 0f)
            yield return PLC_HoldLowerUntilDone(() => LowerCylinder_BySeconds(assemblyCylinder, cylinderDownOffset, cylinderMoveSeconds, cylinderPosEps));

        _running = false;
    }

    // -------------------------------------------------
    // Sequence 4 (UB.* 분해/이동)
    // -------------------------------------------------
    IEnumerator RunSequence4()
    {
        _running = true;

        // (1) UB Actuator ↑
        yield return SequenceController.WaitWhilePaused();
        if (UB_Actuator) yield return RaiseUBActuator();

        // (2) UB(merged) → A_ReleasePoint
        yield return SequenceController.WaitWhilePaused();
        if (gripMoveTest && UB_GripPoint_S4 && A_ReleasePoint_S4 && (UB_Rigidbody || UB_U_Rigidbody))
        {
            var merged = UB_Rigidbody ? UB_Rigidbody : UB_U_Rigidbody;
            yield return RunGMT(UB_GripPoint_S4, A_ReleasePoint_S4, merged,
                UB_LeftJawClose_S4, UB_RightJawClose_S4, UB_ReleaseParent_S4, s4_gripToReleaseDelay);
        }
        // UB.L freeze
        {
            var h = GetHandle(UB_L_Rigidbody, UBL_ParentHandle) ?? (UB_L_Rigidbody ? UB_L_Rigidbody.transform : null);
            if (h)
            {
                yield return SequenceController.WaitWhilePaused();
                EnsureRb(h, ref UB_L_Rigidbody, true);
                SetCollidersEnabled(h, false);
                ApplyFreezeAll(UB_L_Rigidbody);
                ApplyFreezeAll(UB_U_Rigidbody);
                Physics.SyncTransforms();
                yield return new WaitForFixedUpdate();
            }
        }

        // (3) UB Actuator ↓
        yield return SequenceController.WaitWhilePaused();
        if (UB_Actuator) yield return LowerUBActuator();

        // (4) UB.L Detach
        yield return SequenceController.WaitWhilePaused();
        yield return Detach_UBL_From_UB_S4();

        // (5) UB.D → ER.UB.D
        Transform dRoot = UBD_ParentHandle
                      ? UBD_ParentHandle
                      : (UB_D_Rigidbody_S4 ? UB_D_Rigidbody_S4.transform
                                           : (UB_D_GripPoint_S4 ? UB_D_GripPoint_S4.root : null));
        if (dRoot)
        {
            yield return SequenceController.WaitWhilePaused();
            EnsureRb(dRoot, ref UB_D_Rigidbody_S4, applyStandard: false);
            ApplyCarryRbSettings(UB_D_Rigidbody_S4);
            SetCollidersEnabled(dRoot, true);
            UB_D_Rigidbody_S4.detectCollisions = true;
            UB_D_Rigidbody_S4.isKinematic = false;

            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate();
        }

        yield return SequenceController.WaitWhilePaused();
        if (gripMoveTest && UB_D_GripPoint_S4 && ER_UB_D_ReleasePoint_S4 && UB_D_Rigidbody_S4)
            yield return RunGMT(UB_D_GripPoint_S4, ER_UB_D_ReleasePoint_S4, UB_D_Rigidbody_S4,
                UB_D_LeftJawClose_S4, UB_D_RightJawClose_S4, UB_D_ReleaseParent_S4, s4_gripToReleaseDelay);

        // (6) UB.U → R.UB.U
        yield return SequenceController.WaitWhilePaused();
        if (gripMoveTest && UB_U_GripPoint_S4 && R_UB_U_ReleasePoint_S4 && UB_U_Rigidbody)
            yield return RunGMT(UB_U_GripPoint_S4, R_UB_U_ReleasePoint_S4, UB_U_Rigidbody,
                UB_U_LeftJawClose_S4, UB_U_RightJawClose_S4, UB_U_ReleaseParent_S4, s4_gripToReleaseDelay);

        // (7) UB.L 텔레포트 → 승객 상승
        {
            var t = GetHandle(UB_L_Rigidbody, UBL_ParentHandle) ?? (UB_L_Rigidbody ? UB_L_Rigidbody.transform : null);
            if (t)
            {
                yield return SequenceController.WaitWhilePaused();
                t.position = new Vector3(t.position.x, 0.4961f, t.position.z);
                Physics.SyncTransforms();
                yield return null;

                Restore_UBL_Layer_To_UBL_S4();

                if (reenableUBLPhysicsOnDetach_S4 && UB_L_Rigidbody) ApplyStandardRbSettings(UB_L_Rigidbody);
                if (reenableUBLChildColliders_S4) SetCollidersEnabled(t, true);

                UB_L_Rigidbody.linearVelocity = Vector3.zero;
                UB_L_Rigidbody.angularVelocity = Vector3.zero;
                UB_L_Rigidbody.isKinematic = true;

                if (assemblyCylinder && cylinderDownOffset > 0f)
                    yield return PLC_HoldRiseUntilDone(() => RaiseWithPassengerSmooth_BySeconds(assemblyCylinder, t, cylinderDownOffset, cylinderMoveSeconds, raiseEase));

                UB_L_Rigidbody.isKinematic = false;
                ApplyStandardRbSettings(UB_L_Rigidbody);
                Physics.SyncTransforms();
                yield return new WaitForFixedUpdate();
            }
        }

        // (8) UB.L → R.UB.L
        yield return SequenceController.WaitWhilePaused();
        if (gripMoveTest && UB_L_GripPoint_S4 && R_UB_L_ReleasePoint_S4 && UB_L_Rigidbody)
            yield return RunGMT(UB_L_GripPoint_S4, R_UB_L_ReleasePoint_S4, UB_L_Rigidbody,
                UB_L_LeftJawClose_S4, UB_L_RightJawClose_S4, UB_L_ReleaseParent_S4, s4_gripToReleaseDelay);

        _running = false;
    }

    // -------------------------------------------------
    // 공통: GMT 호출 + 새 RB 참조 갱신
    // -------------------------------------------------
    IEnumerator RunGMT(
        Transform gripPoint, Transform releasePoint, Rigidbody rb,
        float leftJawClose, float rightJawClose, Transform releaseParent,
        float delayAfter = 0f)
    {
        yield return SequenceController.WaitWhilePaused();

        gripMoveTest.moveRb = rb;
        gripMoveTest.gripPoint = gripPoint;
        gripMoveTest.releasePoint = releasePoint;
        gripMoveTest.releaseParent = releaseParent;
        gripMoveTest.LeftJawClosePos = leftJawClose;
        gripMoveTest.RightJawClosePos = rightJawClose;

        yield return gripMoveTest.StartCoroutine(gripMoveTest.RunOnce());

        var updated = gripMoveTest.moveRb;
        if (updated && updated != rb)
        {
            if (rb == UB_L_Rigidbody) UB_L_Rigidbody = updated;
            else if (rb == UB_U_Rigidbody) UB_U_Rigidbody = updated;
            else if (rb == Dummy_Rigidbody) Dummy_Rigidbody = updated;
            else if (rb == UB_Rigidbody) UB_Rigidbody = updated;
            else if (rb == USC_L_Rigidbody) USC_L_Rigidbody = updated;
            else if (rb == USC_U_Rigidbody) USC_U_Rigidbody = updated;
            else if (rb == USC_Rigidbody) USC_Rigidbody = updated;
            else if (rb == S2_Dummy_Rigidbody) S2_Dummy_Rigidbody = updated;
            else if (rb == USC_D_Rigidbody_S3) USC_D_Rigidbody_S3 = updated;
            else if (rb == UB_D_Rigidbody_S4) UB_D_Rigidbody_S4 = updated;
        }

        if (delayAfter > 0f)
        {
            float t = 0f;
            while (t < delayAfter)
            {
                yield return SequenceController.WaitWhilePaused();
                t += Time.deltaTime;
                yield return null;
            }
        }
    }

    // -------------------------------------------------
    // S1: Merge & Raise (UB)
    // -------------------------------------------------
    IEnumerator MergeThenRaise_UB()
    {
        yield return SequenceController.WaitWhilePaused();
        yield return AlignAndParent_UBL_to_UB();

        yield return SequenceController.WaitWhilePaused();
        yield return null;
        yield return new WaitForEndOfFrame();

        if (UB_Actuator)
        {
            if (useSmoothRaise)
            {
                yield return SequenceController.WaitWhilePaused();
                yield return RaiseSmooth(UB_Actuator, UB_ActuatorRaise, UB_ActuatorMoveSeconds, raiseEase);
            }
            else
            {
                yield return SequenceController.WaitWhilePaused();
                yield return RaiseUBActuator();
            }
        }
    }

    IEnumerator AlignAndParent_UBL_to_UB()
    {
        if (!UB_L_Rigidbody || !UB_U_Rigidbody || !UB_L_HeadRef || !UB_BottomRef) yield break;

        yield return SequenceController.WaitWhilePaused();

        Vector3 delta = UB_BottomRef.position - UB_L_HeadRef.position;

        if (zeroVelocityOnTeleport)
        {
            UB_L_Rigidbody.linearVelocity = Vector3.zero;
            UB_L_Rigidbody.angularVelocity = Vector3.zero;
        }

        UB_L_Rigidbody.transform.position += delta;
        yield return null;

        Transform parentT = UB_BottomParent ? UB_BottomParent : UB_U_Rigidbody.transform;
        UB_L_Rigidbody.transform.SetParent(parentT, true);

        if (disableUBLPhysicsWhenParented && UB_L_Rigidbody)
        {
            UB_L_Rigidbody.linearVelocity = Vector3.zero;
            UB_L_Rigidbody.angularVelocity = Vector3.zero;
            UB_L_Rigidbody.useGravity = false;
            UB_L_Rigidbody.detectCollisions = false;
            UB_L_Rigidbody.isKinematic = true;
            UB_L_Rigidbody.constraints = RigidbodyConstraints.FreezeAll;
            UB_L_Rigidbody.interpolation = RigidbodyInterpolation.None;
        }
    }

    // -------------------------------------------------
    // S2: Merge & Raise (USC)
    // -------------------------------------------------
    IEnumerator MergeThenRaise_USC()
    {
        yield return SequenceController.WaitWhilePaused();
        yield return AlignAndParent_USCL_to_USC();

        yield return SequenceController.WaitWhilePaused();
        yield return null;
        yield return new WaitForEndOfFrame();

        if (USC_Actuator)
        {
            if (useSmoothRaise)
            {
                yield return SequenceController.WaitWhilePaused();
                yield return RaiseSmooth(USC_Actuator, USC_ActuatorRaise, USC_ActuatorMoveSeconds, raiseEase);
            }
            else
            {
                yield return SequenceController.WaitWhilePaused();
                yield return RaiseUSCActuator();
            }
        }
    }

    IEnumerator AlignAndParent_USCL_to_USC()
    {
        if (!USC_L_Rigidbody || !USC_U_Rigidbody || !USC_L_HeadRef || !USC_BottomRef) yield break;

        yield return SequenceController.WaitWhilePaused();

        Vector3 delta = USC_BottomRef.position - USC_L_HeadRef.position;

        USC_L_Rigidbody.linearVelocity = Vector3.zero;
        USC_L_Rigidbody.angularVelocity = Vector3.zero;

        USC_L_Rigidbody.transform.position += delta;
        yield return null;

        Transform parentT = USC_BottomParent ? USC_BottomParent : USC_U_Rigidbody.transform;
        USC_L_Rigidbody.transform.SetParent(parentT, true);

        if (disableUSCLPhysicsWhenParented && USC_L_Rigidbody)
        {
            USC_L_Rigidbody.linearVelocity = Vector3.zero;
            USC_L_Rigidbody.angularVelocity = Vector3.zero;
            USC_L_Rigidbody.useGravity = false;
            USC_L_Rigidbody.detectCollisions = false;
            USC_L_Rigidbody.isKinematic = true;
            USC_L_Rigidbody.constraints = RigidbodyConstraints.FreezeAll;
            USC_L_Rigidbody.interpolation = RigidbodyInterpolation.None;
        }
    }

    // -------------------------------------------------
    // 공용 실린더 / 액추에이터 / 스크레이퍼
    // -------------------------------------------------
    IEnumerator SmoothMoveYBySeconds(Transform t, float deltaY, float seconds, AnimationCurve ease)
    {
        if (!t) yield break;
        float dur = Mathf.Max(0.0001f, seconds);
        if (ease == null) ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

        Physics.SyncTransforms();
        yield return SequenceController.WaitWhilePaused();
        yield return null;
        yield return new WaitForEndOfFrame();

        Vector3 start = t.position;
        Vector3 target = new Vector3(start.x, start.y + deltaY, start.z);

        float elapsed = 0f;
        while (elapsed < dur)
        {
            yield return SequenceController.WaitWhilePaused();

            elapsed += Time.deltaTime;
            float k = ease.Evaluate(Mathf.Clamp01(elapsed / dur));
            t.position = Vector3.LerpUnclamped(start, target, k);
            yield return null;
        }
        t.position = target;
        Physics.SyncTransforms();
    }

    IEnumerator LowerCylinder_BySeconds(Transform cyl, float downOffset, float seconds, float eps)
    {
        if (!cyl) yield break;
        float dist = Mathf.Abs(downOffset);
        yield return SmoothMoveYBySeconds(cyl, -dist, seconds, raiseEase);
    }

    IEnumerator RaiseCylinder_BySeconds(Transform cyl, float upOffset, float seconds, float eps)
    {
        if (!cyl) yield break;
        float dist = Mathf.Abs(upOffset);
        yield return SmoothMoveYBySeconds(cyl, +dist, seconds, raiseEase);
    }

    IEnumerator RaiseUBActuator()
    {
        if (!UB_Actuator) yield break;
        float dist = Mathf.Abs(UB_ActuatorRaise);
        yield return SmoothMoveYBySeconds(UB_Actuator, +dist, UB_ActuatorMoveSeconds, raiseEase);
    }

    IEnumerator LowerUBActuator()
    {
        if (!UB_Actuator) yield break;
        float dist = Mathf.Abs(UB_ActuatorRaise);
        yield return SmoothMoveYBySeconds(UB_Actuator, -dist, UB_ActuatorMoveSeconds, raiseEase);
    }

    IEnumerator RaiseUSCActuator()
    {
        if (!USC_Actuator) yield break;
        float dist = Mathf.Abs(USC_ActuatorRaise);
        yield return SmoothMoveYBySeconds(USC_Actuator, +dist, USC_ActuatorMoveSeconds, raiseEase);
    }

    IEnumerator LowerUSCActuator()
    {
        if (!USC_Actuator) yield break;
        float dist = Mathf.Abs(USC_ActuatorRaise);
        yield return SmoothMoveYBySeconds(USC_Actuator, -dist, USC_ActuatorMoveSeconds, raiseEase);
    }

    IEnumerator OscillateUSCScraper()
    {
        float halfCycleTime = scraperCycleSeconds * 0.5f;
        float speed = Mathf.Max(0.0001f, scraperTravel / Mathf.Max(0.0001f, halfCycleTime));

        Vector3 start = USC_Scraper.position;
        Vector3 down = new Vector3(start.x, start.y - scraperTravel, start.z);

        for (int i = 0; i < scraperCycles; i++)
        {
            UILogger.Instance?.Log($"[Scraper] Cycle {i + 1}/{scraperCycles} 시작");

            yield return SequenceController.WaitWhilePaused();
            yield return Move.MoveTo_Target(
                USC_Scraper,
                () => speed,
                down,
                scraperPosEps
            );

            yield return SequenceController.WaitWhilePaused();
            yield return Move.MoveTo_Target(
                USC_Scraper,
                () => speed,
                start,
                scraperPosEps
            );
        }
    }

    // -------------------------------------------------
    // S3 Detach USC.L
    // -------------------------------------------------
    IEnumerator Detach_USCL_From_USC_S3()
    {
        var t = GetHandle(USC_L_Rigidbody, USCL_ParentHandle) ?? (USC_L_Rigidbody ? USC_L_Rigidbody.transform : null);
        if (!t) yield break;

        yield return SequenceController.WaitWhilePaused();

        if (!USC_L_Rigidbody) EnsureRb(t, ref USC_L_Rigidbody, true);
        if (!USC_L_Rigidbody) yield break;

        SetCollidersEnabled(t, false);
        t.SetParent(null, true);
        yield return null;
        t.SetParent(USCL_DetachedParent_S3 ? USCL_DetachedParent_S3 : null, true);
        yield return null;

        if (USC_L_Teleport_S3)
        {
            if (zeroVelocityOnTeleport_S3)
            {
                USC_L_Rigidbody.linearVelocity = Vector3.zero;
                USC_L_Rigidbody.angularVelocity = Vector3.zero;
            }
            t.position = USC_L_Teleport_S3.position;
            t.rotation = USC_L_Teleport_S3.rotation;
            yield return null;
        }
    }

    void Restore_USCL_Layer_To_USCL_S3()
    {
        if (!USC_L_Rigidbody) return;
        int l = LayerMask.NameToLayer(layerName_USC_L);
        if (l == -1) return;
        SetLayerRecursively(USC_L_Rigidbody.gameObject, l);
    }

    // -------------------------------------------------
    // S4 Detach UB.L
    // -------------------------------------------------
    IEnumerator Detach_UBL_From_UB_S4()
    {
        var t = GetHandle(UB_L_Rigidbody, UBL_ParentHandle) ?? (UB_L_Rigidbody ? UB_L_Rigidbody.transform : null);
        if (!t) yield break;

        yield return SequenceController.WaitWhilePaused();

        if (!UB_L_Rigidbody) EnsureRb(t, ref UB_L_Rigidbody, true);
        if (!UB_L_Rigidbody) yield break;

        SetCollidersEnabled(t, false);
        t.SetParent(null, true);
        yield return null;
        t.SetParent(UBL_DetachedParent_S4 ? UBL_DetachedParent_S4 : null, true);
        yield return null;

        if (UB_L_Teleport_S4)
        {
            if (zeroVelocityOnTeleport_S4)
            {
                UB_L_Rigidbody.linearVelocity = Vector3.zero;
                UB_L_Rigidbody.angularVelocity = Vector3.zero;
            }
            t.position = UB_L_Teleport_S4.position;
            t.rotation = UB_L_Teleport_S4.rotation;
            yield return null;
        }
    }

    IEnumerator RaiseSmooth(Transform t, float deltaY, float duration, AnimationCurve ease)
    {
        if (!t) yield break;
        if (duration <= 0f) duration = 0.0001f;
        if (ease == null) ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

        Physics.SyncTransforms();
        yield return SequenceController.WaitWhilePaused();
        yield return null;
        yield return new WaitForEndOfFrame();

        Vector3 start = t.position;
        Vector3 target = new Vector3(start.x, start.y + deltaY, start.z);

        float elapsed = 0f;
        while (elapsed < duration)
        {
            yield return SequenceController.WaitWhilePaused();

            elapsed += Time.deltaTime;
            float t01 = Mathf.Clamp01(elapsed / duration);
            float k = ease.Evaluate(t01);
            t.position = Vector3.LerpUnclamped(start, target, k);
            yield return null;
        }
        t.position = target;
    }

    IEnumerator RaiseWithPassengerSmooth_BySeconds(Transform cyl, Transform passenger, float upOffset, float seconds, AnimationCurve ease = null)
    {
        if (!cyl || !passenger) yield break;
        float dist = Mathf.Abs(upOffset);
        float dur = Mathf.Max(0.0001f, seconds);
        if (ease == null) ease = AnimationCurve.EaseInOut(0, 0, 1, 1);

        Physics.SyncTransforms();
        yield return SequenceController.WaitWhilePaused();
        yield return null;

        Vector3 c0 = cyl.position;
        Vector3 p0 = passenger.position;
        Vector3 c1 = new Vector3(c0.x, c0.y + dist, c0.z);
        Vector3 p1 = new Vector3(p0.x, p0.y + dist, p0.z);

        float t = 0f;
        while (t < dur)
        {
            yield return SequenceController.WaitWhilePaused();

            t += Time.deltaTime;
            float k = ease.Evaluate(Mathf.Clamp01(t / dur));
            cyl.position = Vector3.LerpUnclamped(c0, c1, k);
            passenger.position = Vector3.LerpUnclamped(p0, p1, k);
            yield return null;
        }
        cyl.position = c1;
        passenger.position = p1;
        Physics.SyncTransforms();
    }

    void Restore_UBL_Layer_To_UBL_S4()
    {
        if (!UB_L_Rigidbody) return;
        int l = LayerMask.NameToLayer(layerName_UB_L);
        if (l == -1) return;
        SetLayerRecursively(UB_L_Rigidbody.gameObject, l);
    }

    // -------------------------------------------------
    // Util
    // -------------------------------------------------
    void SetLayerRecursively(GameObject go, int layer)
    {
        if (!go) return;
        go.layer = layer;
        foreach (Transform child in go.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    Transform GetHandle(Rigidbody rb, Transform overrideT)
    {
        if (overrideT) return overrideT;
        return rb ? rb.transform : null;
    }

    void ApplyStandardRbSettings(Rigidbody rb)
    {
        if (!rb) return;
        rb.useGravity = true;
        rb.isKinematic = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.constraints =
            RigidbodyConstraints.FreezePositionX |
            RigidbodyConstraints.FreezePositionZ |
            RigidbodyConstraints.FreezeRotationX |
            RigidbodyConstraints.FreezeRotationY |
            RigidbodyConstraints.FreezeRotationZ;
    }

    Rigidbody EnsureRb(Transform target, ref Rigidbody cache, bool applyStandard = true, float? mass = null)
    {
        if (!target) return null;
        var rb = target.GetComponent<Rigidbody>();
        if (!rb)
        {
            rb = target.gameObject.AddComponent<Rigidbody>();
            rb.mass = (cache ? cache.mass : (mass ?? 1f));
        }
        cache = rb;
        if (applyStandard) ApplyStandardRbSettings(rb);
        return rb;
    }

    void SetCollidersEnabled(Transform t, bool enabled)
    {
        if (!t) return;
        foreach (var col in t.GetComponentsInChildren<Collider>(true))
            col.enabled = enabled;
    }
    void ApplyFreezeAll(Rigidbody rb)
    {
        if (!rb) return;
        rb.isKinematic = true;
        rb.useGravity = false;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        rb.constraints = RigidbodyConstraints.FreezeAll;
    }
    void ApplyCarryRbSettings(Rigidbody rb)
    {
        if (!rb) return;
        rb.useGravity = true;
        rb.isKinematic = false;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.constraints = RigidbodyConstraints.FreezeRotationX |
                         RigidbodyConstraints.FreezeRotationY |
                         RigidbodyConstraints.FreezeRotationZ;
    }
}
