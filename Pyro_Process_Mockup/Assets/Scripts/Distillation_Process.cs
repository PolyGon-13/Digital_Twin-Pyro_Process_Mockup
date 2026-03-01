using UnityEngine;
using System.Collections;

public class Distillation_Process : MonoBehaviour
{
    [Header("PLC (Heater Auto Open/Close)")]
    public Unity_PLC plc;

    [Tooltip("PLC Stop bit (자동 히터 정지)")]
    public string addr_AutoStop = "M02014";
    [Tooltip("PLC Open pulse")]
    public string addr_AutoOpenPulse = "M0201C";
    [Tooltip("PLC Close pulse")]
    public string addr_AutoClosePulse = "M0201D";
    [Min(0.02f)] public float plcPulseWidthSec = 0.12f;

    [Header("PLC Heater Done Bits (read-only)")]
    public string addr_OpenDone = "";
    public string addr_CloseDone = "";

    [Header("PLC Mode Bits")]
    public string addr_IsAuto = "";
    public string addr_SetAuto = "";
    public string addr_SetManual = "";
    public bool modeCmdIsPulse = true;

    [Header("PLC Wait Settings")]
    [Min(0.05f)] public float plcPollInterval = 0.1f;
    [Min(0.5f)] public float plcMaxWaitSec = 20f;

    [Header("Ref Scripts")]
    public GantryIKArticulation gantry;
    public DockAndAttach dock;
    public GripAndMoveTest grip;
    Transform Target;
    Transform ToolSocket;

    [Header("Objects")]
    public Transform Dry_Mock_Up;
    public Transform RDS;
    public Transform leftHeater;
    public Transform rightHeater;
    public Rigidbody RDS_CC_RB;
    public Rigidbody RDS_CB_RB;
    public Rigidbody RDS_SR_RB;

    [Header("Pick Points (잡으러 갈 곳)")]
    public Transform RDS_CC_Point;
    public Transform RDS_CB_Point;
    public Transform RDS_SR_Point;
    public Transform Basket_Carrier_Point;

    [Header("Release Points (내려놓을 곳)")]
    public Transform RDS_CC_Release_Point;
    public Transform RDS_CB_Release_Point;
    public Transform RDS_SR_Release_Point;
    public Transform Basket_Carrier_Release_Point;

    [Header("Dipstick Points")]
    public Transform Dipstick_Point;
    public Transform Dipstick_Release_Point;

    [Header("Origin Points (월드 기준 원래 자리)")]
    public Transform CC_Origin_Point;
    public Transform CB_Origin_Point;
    public Transform SR_Origin_Point;
    public Transform BC_Origin_Point;

    // 런타임에 뽑아쓰는 실제 좌표 변수들
    Vector3 rds_cc_point;
    Vector3 rds_cc_release_point;
    Vector3 rds_cb_point;
    Vector3 rds_cb_release_point;
    Vector3 rds_sr_point;
    Vector3 rds_sr_release_point;
    Vector3 basket_carrier_point;
    Vector3 basket_carrier_release_point;
    Vector3 dipstick_point;
    Vector3 dipstick_release_point;
    Vector3 cc_origin_pos;
    Vector3 cb_origin_pos;
    Vector3 sr_origin_pos;
    Vector3 bc_origin_pos;

    [Header("Basket")]
    public Rigidbody Basket_Carrier;
    public Transform UB_L;
    public Transform USC_L;

    [Header("Dip Stick")]
    public Rigidbody DipStick;

    [Header("Heavy Gripper Jaw")]
    ArticulationBody Gripper_1_Left_Jaw;
    ArticulationBody Gripper_1_Right_Jaw;
    float CC_LeftJawClosePos = 0.062f;
    float CC_RightJawClosePos = -0.062f;
    float CB_LeftJawClosePos = 0.06f;
    float CB_RightJawClosePos = -0.06f;
    float BC_LeftJawClosePos = 0.127f;
    float BC_RightJawClosePos = -0.127f;
    float SR_LeftJawClosePos = 0.062f;
    float SR_RightJawClosePos = -0.062f;

    [Header("Angular Gripper Jaw")]
    ArticulationBody AG_Left_Jaw;
    ArticulationBody AG_Right_Jaw;
    ArticulationBody AG_Center_Jaw;
    float DS_JawClosePos = 0.0019f;

    [Header("RDS Parts (for local reset)")]
    public Transform CC;
    public Transform CB;
    public Transform SR;

    [Header("Heater Timing")]
    [Tooltip("히터가 한 번 열리거나 닫히는 데 걸리는 시간(초)")]
    public float heaterOpenCloseSeconds = 44f;

    float eps = 0.01f;
    Vector3 leftClosePos = new Vector3(0f, 0f, -0.36f);
    Vector3 rightClosePos = new Vector3(0f, 0f, 0.36f);

    // 이 3개는 "RDS 안에서" 원래 자리로 돌릴 때 쓰는 로컬값 → 유지
    Vector3 cc_local_origin_pos = new Vector3(0.03558636f, 0f, -2.054258f);
    Vector3 cb_local_origin_pos = new Vector3(0.03558636f, -0.3000004f, -2.088423f);
    Vector3 sr_local_origin_pos = new Vector3(0.03558636f, 0.217f, 0.009055346f);

    bool isProcessing = false;

    void Awake()
    {
        Target = gantry ? gantry.Target : null;
        ToolSocket = gantry ? gantry.toolSocket : null;

        AG_Left_Jaw = gantry ? gantry.Angular_Gripper_Jaw_Left : null;
        AG_Right_Jaw = gantry ? gantry.Angular_Gripper_Jaw_Right : null;
        AG_Center_Jaw = gantry ? gantry.Angular_Gripper_Jaw_Center : null;

        Gripper_1_Left_Jaw = gantry ? gantry.Heavy_Gripper_Jaw_Left : null;
        Gripper_1_Right_Jaw = gantry ? gantry.Heavy_Gripper_Jaw_Right : null;

        ReadAllPointsFromScene();
    }

    void ReadAllPointsFromScene()
    {
        if (RDS_CC_Point) rds_cc_point = RDS_CC_Point.position;
        if (RDS_CB_Point) rds_cb_point = RDS_CB_Point.position;
        if (RDS_SR_Point) rds_sr_point = RDS_SR_Point.position;
        if (Basket_Carrier_Point) basket_carrier_point = Basket_Carrier_Point.position;

        if (RDS_CC_Release_Point) rds_cc_release_point = RDS_CC_Release_Point.position;
        if (RDS_CB_Release_Point) rds_cb_release_point = RDS_CB_Release_Point.position;
        if (RDS_SR_Release_Point) rds_sr_release_point = RDS_SR_Release_Point.position;
        if (Basket_Carrier_Release_Point) basket_carrier_release_point = Basket_Carrier_Release_Point.position;

        if (Dipstick_Point) dipstick_point = Dipstick_Point.position;
        if (Dipstick_Release_Point) dipstick_release_point = Dipstick_Release_Point.position;

        if (CC_Origin_Point) cc_origin_pos = CC_Origin_Point.position;
        if (CB_Origin_Point) cb_origin_pos = CB_Origin_Point.position;
        if (SR_Origin_Point) sr_origin_pos = SR_Origin_Point.position;
        if (BC_Origin_Point) bc_origin_pos = BC_Origin_Point.position;
    }

    // ================== PLC 연결 자동 감지 ==================
    bool PlcOnline()
    {
        if (plc == null) return false;
        try
        {
            plc.WriteBool("M00000", false);
            return true;
        }
        catch
        {
            return false;
        }
    }

    IEnumerator PulseBit(string addr)
    {
        // pulse 자체도 멈춰있어야 하니까
        yield return SequenceController.WaitWhilePaused();

        if (!PlcOnline()) yield break;
        if (string.IsNullOrWhiteSpace(addr)) yield break;

        plc.WriteBool(addr, true);
        yield return new WaitForSeconds(Mathf.Max(0.02f, plcPulseWidthSec));
        plc.WriteBool(addr, false);
    }

    bool SafeRead(string addr)
    {
        if (!PlcOnline()) return false;
        if (string.IsNullOrWhiteSpace(addr)) return false;
        try { return plc.ReadBool(addr); }
        catch { return false; }
    }

    IEnumerator EnsureAutoMode()
    {
        yield return SequenceController.WaitWhilePaused();

        if (!PlcOnline() || string.IsNullOrWhiteSpace(addr_IsAuto)) yield break;

        if (SafeRead(addr_IsAuto)) yield break;

        if (!string.IsNullOrWhiteSpace(addr_SetManual)) plc.WriteBool(addr_SetManual, false);

        if (!string.IsNullOrWhiteSpace(addr_SetAuto))
        {
            if (modeCmdIsPulse)
            {
                plc.WriteBool(addr_SetAuto, true);
                yield return new WaitForSeconds(0.15f);
                plc.WriteBool(addr_SetAuto, false);
            }
            else
            {
                plc.WriteBool(addr_SetAuto, true);
            }
        }

        float t = 0f;
        while (t < 3f && !SafeRead(addr_IsAuto))
        {
            yield return SequenceController.WaitWhilePaused();
            yield return new WaitForSeconds(0.1f);
            t += 0.1f;
        }
    }

    IEnumerator WaitPlcDone(string doneAddr)
    {
        yield return SequenceController.WaitWhilePaused();

        if (!PlcOnline()) yield break;
        if (string.IsNullOrWhiteSpace(doneAddr)) yield break;

        float t = 0f;
        while (t < plcMaxWaitSec)
        {
            if (SafeRead(doneAddr)) yield break;
            yield return SequenceController.WaitWhilePaused();
            yield return new WaitForSeconds(plcPollInterval);
            t += plcPollInterval;
        }

        UILogger.Instance?.Log($"[PLC] 타임아웃: {doneAddr} 완료 신호 없음({plcMaxWaitSec:0.0}s)");
    }

    // ================== 메인 공정 ==================
    public IEnumerator Do_Distillation_Process()
    {
        yield return SequenceController.WaitWhilePaused();

        // 그리퍼 도킹
        yield return dock.Start_Dock_Heavy_Gripper();
        yield return SequenceController.WaitWhilePaused();
        yield return new WaitForSeconds(dock.delayBeforeMove);

        // 공정1
        yield return RDS_CC_Move();
        yield return BC_Move();
        yield return RDS_CC_Move_2();
        yield return SequenceController.WaitWhilePaused();
        yield return new WaitForSeconds(3f);

        // (여기 원래 LIBS 있었음)

        // 공정2
        yield return RDS_CC_Move();
        yield return BC_Move_2();
        yield return RDS_CB_Move();
        yield return RDS_SR_Move();
        yield return SequenceController.WaitWhilePaused();
        yield return new WaitForSeconds(3f);

        // 공정3 정리
        yield return RDS_SR_Move_2();
        yield return RDS_CB_Move_2();
        yield return RDS_CC_Move_2();

        UILogger.Instance?.Log("증류 공정 종료");
        isProcessing = false;
    }

    // ================== 세부 시퀀스 ==================
    IEnumerator RDS_CC_Move()
    {
        yield return SequenceController.WaitWhilePaused();
        ReadAllPointsFromScene();

        // 히터 open (PLC)
        yield return EnsureAutoMode();
        if (PlcOnline())
        {
            // 이 3줄이 한세트니까 맨 앞에서만 pause
            plc.WriteBool("M02014", false);
            plc.WriteBool("M0201D", false);
            plc.WriteBool("M0201C", true);
        }

        // 히터 open (Unity)
        yield return SequenceController.WaitWhilePaused();
        yield return Heater_Move(leftHeater, rightHeater, Vector3.zero, Vector3.zero, heaterOpenCloseSeconds, eps);

        // PLC 완료 기다리기
        if (PlcOnline() && !string.IsNullOrWhiteSpace(addr_OpenDone))
            yield return WaitPlcDone(addr_OpenDone);

        if (PlcOnline())
        {
            // 이것도 한세트
            plc.WriteBool("M0201C", false);
            plc.WriteBool("M02014", true);
        }

        yield return SequenceController.WaitWhilePaused();
        yield return new WaitForSeconds(dock.delayBeforeMove);

        // CC pick 위치로
        yield return SequenceController.WaitWhilePaused();
        yield return Move.MoveXZ(Target, () => gantry.SpeedXZ,
            new Vector3(rds_cc_point.x, Target.position.y, rds_cc_point.z), dock.Pos_Eps);
        yield return new WaitForSeconds(dock.delayBeforeMove);

        // 내려가서
        yield return SequenceController.WaitWhilePaused();
        yield return Move.MoveY_Down(Target, rds_cc_point.y, () => gantry.SpeedYDown, dock.Pos_Eps);
        yield return new WaitForSeconds(dock.delayBeforeMove);

        // 잡기
        yield return SequenceController.WaitWhilePaused();
        yield return Move.Close_Heavy_Gripper_Jaw(Gripper_1_Left_Jaw, Gripper_1_Right_Jaw,
            CC_LeftJawClosePos, CC_RightJawClosePos, gantry.JawPosEps, gantry.HG_Start, gantry.HG_End);
        yield return new WaitForSeconds(dock.delayBeforeMove + 0.8f);

        // 붙이기
        yield return SequenceController.WaitWhilePaused();
        SetParent_2(ToolSocket, RDS_CC_RB);
        yield return new WaitForSeconds(dock.delayBeforeMove);

        // 위로
        yield return SequenceController.WaitWhilePaused();
        yield return Move.MoveY_Up(Target, gantry.yJoint_2, () => gantry.SpeedYUp, dock.Pos_Eps);
        yield return new WaitForSeconds(dock.delayBeforeMove + 0.3f);

        // Release 위치로
        yield return SequenceController.WaitWhilePaused();
        yield return Move.MoveXZ(Target, () => gantry.SpeedXZ,
            new Vector3(rds_cc_release_point.x, Target.position.y, rds_cc_release_point.z), dock.Pos_Eps);
        yield return new WaitForSeconds(dock.delayBeforeMove);

        // 내려놓기
        yield return SequenceController.WaitWhilePaused();
        yield return Move.MoveY_Down(Target, rds_cc_release_point.y, () => gantry.SpeedYDown, dock.Pos_Eps);
        yield return new WaitForSeconds(dock.delayBeforeMove);

        // Jaw open
        yield return SequenceController.WaitWhilePaused();
        yield return Move.Open_Heavy_Gripper_Jaw(Gripper_1_Left_Jaw, Gripper_1_Right_Jaw,
            gantry.JawPosEps, gantry.HG_Start, gantry.HG_End);
        yield return new WaitForSeconds(dock.delayBeforeMove);

        // 다시 RDS 자식으로
        yield return SequenceController.WaitWhilePaused();
        SetParent_2(RDS, RDS_CC_RB);
        Move.FreezeRB(RDS_CC_RB, true, false, true);
        yield return new WaitForSeconds(dock.delayBeforeMove);
        Move.SetGravity(RDS_CC_RB, true);
        yield return new WaitForSeconds(dock.delayBeforeMove + 0.8f);
        Move.FreezeRB(RDS_CC_RB, true, true, true);
        yield return new WaitForSeconds(dock.delayBeforeMove);
        Move.SetGravity(RDS_CC_RB, false);

        // 위로
        yield return SequenceController.WaitWhilePaused();
        yield return Move.MoveY_Up(Target, gantry.yJoint_2, () => gantry.SpeedYUp, dock.Pos_Eps);
        yield return new WaitForSeconds(dock.delayBeforeMove);
    }

    IEnumerator RDS_CC_Move_2()
    {
        yield return SequenceController.WaitWhilePaused();
        ReadAllPointsFromScene();

        // CC 다시 잡으러
        yield return SequenceController.WaitWhilePaused();
        yield return Move.MoveXZ(Target, () => gantry.SpeedXZ,
            new Vector3(rds_cc_release_point.x, Target.position.y, rds_cc_release_point.z), dock.Pos_Eps);
        yield return new WaitForSeconds(dock.delayBeforeMove);

        yield return SequenceController.WaitWhilePaused();
        yield return Move.MoveY_Down(Target, rds_cc_release_point.y, () => gantry.SpeedYDown, dock.Pos_Eps);
        yield return new WaitForSeconds(dock.delayBeforeMove);

        yield return SequenceController.WaitWhilePaused();
        yield return Move.Close_Heavy_Gripper_Jaw(Gripper_1_Left_Jaw, Gripper_1_Right_Jaw,
            CC_LeftJawClosePos, CC_RightJawClosePos, gantry.JawPosEps, gantry.HG_Start, gantry.HG_End);
        yield return new WaitForSeconds(dock.delayBeforeMove + 0.8f);

        yield return SequenceController.WaitWhilePaused();
        SetParent_2(ToolSocket, RDS_CC_RB);
        yield return new WaitForSeconds(dock.delayBeforeMove);

        // 위로
        yield return SequenceController.WaitWhilePaused();
        yield return Move.MoveY_Up(Target, gantry.yJoint_2, () => gantry.SpeedYUp, dock.Pos_Eps);
        yield return new WaitForSeconds(dock.delayBeforeMove + 0.3f);

        // 원래 CC 자리로
        yield return SequenceController.WaitWhilePaused();
        yield return Move.MoveXZ(Target, () => gantry.SpeedXZ,
            new Vector3(cc_origin_pos.x, Target.position.y, cc_origin_pos.z), dock.Pos_Eps);
        yield return new WaitForSeconds(dock.delayBeforeMove);

        // 내려가기
        yield return SequenceController.WaitWhilePaused();
        yield return Move.MoveY_Down(Target, cc_origin_pos.y, () => gantry.SpeedYDown, dock.Pos_Eps);
        yield return new WaitForSeconds(dock.delayBeforeMove);

        // Jaw open
        yield return SequenceController.WaitWhilePaused();
        yield return Move.Open_Heavy_Gripper_Jaw(Gripper_1_Left_Jaw, Gripper_1_Right_Jaw,
            gantry.JawPosEps, gantry.HG_Start, gantry.HG_End);
        yield return new WaitForSeconds(dock.delayBeforeMove);

        yield return SequenceController.WaitWhilePaused();
        SetParent_2(RDS, RDS_CC_RB);
        yield return new WaitForSeconds(dock.delayBeforeMove);

        // 위로
        yield return SequenceController.WaitWhilePaused();
        yield return Move.MoveY_Up(Target, gantry.yJoint_2, () => gantry.SpeedYUp, dock.Pos_Eps);
        yield return new WaitForSeconds(dock.delayBeforeMove);

        // PLC 히터 닫기
        yield return EnsureAutoMode();
        if (PlcOnline())
        {
            // 닫기 세트
            plc.WriteBool("M02014", false);
            plc.WriteBool("M0201C", false);
            plc.WriteBool("M0201D", true);
        }

        // Unity 히터 닫기
        yield return SequenceController.WaitWhilePaused();
        yield return Heater_Move(leftHeater, rightHeater, leftClosePos, rightClosePos, heaterOpenCloseSeconds, eps);

        if (PlcOnline() && !string.IsNullOrWhiteSpace(addr_CloseDone))
            yield return WaitPlcDone(addr_CloseDone);

        if (PlcOnline())
        {
            plc.WriteBool("M0201D", false);
            plc.WriteBool("M02014", true);
        }

        yield return SequenceController.WaitWhilePaused();
        yield return new WaitForSeconds(dock.delayBeforeMove);

        // 히터로 가려졌던 파트 로컬 원복
        if (SR) SR.localPosition = sr_local_origin_pos;
        yield return new WaitForFixedUpdate();
        if (CB) CB.localPosition = cb_local_origin_pos;
        yield return new WaitForFixedUpdate();
        if (CC) CC.localPosition = cc_local_origin_pos;
        yield return new WaitForFixedUpdate();
    }

    IEnumerator RDS_CB_Move()
    {
        yield return SequenceController.WaitWhilePaused();
        ReadAllPointsFromScene();

        yield return SequenceController.WaitWhilePaused();
        yield return Move.MoveXZ(Target, () => gantry.SpeedXZ,
            new Vector3(rds_cb_point.x, Target.position.y, rds_cb_point.z), dock.Pos_Eps);
        yield return new WaitForSeconds(dock.delayBeforeMove);

        yield return SequenceController.WaitWhilePaused();
        yield return Move.MoveY_Down(Target, rds_cb_point.y, () => gantry.SpeedYDown, dock.Pos_Eps);
        yield return new WaitForSeconds(dock.delayBeforeMove);

        yield return SequenceController.WaitWhilePaused();
        yield return Move.Close_Heavy_Gripper_Jaw(Gripper_1_Left_Jaw, Gripper_1_Right_Jaw,
            CB_LeftJawClosePos, CB_RightJawClosePos, gantry.JawPosEps, gantry.HG_Start, gantry.HG_End);
        yield return new WaitForSeconds(dock.delayBeforeMove + 0.8f);

        yield return SequenceController.WaitWhilePaused();
        SetParent_2(ToolSocket, RDS_CB_RB);
        yield return new WaitForSeconds(dock.delayBeforeMove);

        yield return SequenceController.WaitWhilePaused();
        yield return Move.MoveY_Up(Target, gantry.yJoint_2, () => gantry.SpeedYUp, dock.Pos_Eps);
        yield return new WaitForSeconds(dock.delayBeforeMove + 0.3f);

        // CB Release 위치로
        yield return SequenceController.WaitWhilePaused();
        yield return Move.MoveXZ(Target, () => gantry.SpeedXZ,
            new Vector3(rds_cb_release_point.x, Target.position.y, rds_cb_release_point.z), dock.Pos_Eps);
        yield return new WaitForSeconds(dock.delayBeforeMove);

        yield return SequenceController.WaitWhilePaused();
        yield return Move.MoveY_Down(Target, rds_cb_release_point.y, () => gantry.SpeedYDown, dock.Pos_Eps);
        yield return new WaitForSeconds(dock.delayBeforeMove);

        yield return SequenceController.WaitWhilePaused();
        yield return Move.Open_Heavy_Gripper_Jaw(Gripper_1_Left_Jaw, Gripper_1_Right_Jaw,
            gantry.JawPosEps, gantry.HG_Start, gantry.HG_End);
        yield return new WaitForSeconds(dock.delayBeforeMove);

        yield return SequenceController.WaitWhilePaused();
        SetParent_2(RDS, RDS_CB_RB);
        Move.FreezeRB(RDS_CB_RB, true, false, true);
        yield return new WaitForSeconds(dock.delayBeforeMove);
        Move.SetGravity(RDS_CB_RB, true);
        yield return new WaitForSeconds(dock.delayBeforeMove + 0.8f);
        Move.FreezeRB(RDS_CB_RB, true, true, true);
        Move.SetGravity(RDS_CB_RB, false);

        yield return SequenceController.WaitWhilePaused();
        yield return Move.MoveY_Up(Target, gantry.yJoint_2, () => gantry.SpeedYUp, dock.Pos_Eps);
        yield return new WaitForSeconds(dock.delayBeforeMove);
    }

    IEnumerator RDS_CB_Move_2()
    {
        yield return SequenceController.WaitWhilePaused();
        ReadAllPointsFromScene();

        // 릴리즈 자리에서 다시 집어와서 원위치로
        yield return SequenceController.WaitWhilePaused();
        yield return Move.MoveXZ(Target, () => gantry.SpeedXZ,
            new Vector3(rds_cb_release_point.x, Target.position.y, rds_cb_release_point.z), dock.Pos_Eps);
        yield return new WaitForSeconds(dock.delayBeforeMove);

        yield return SequenceController.WaitWhilePaused();
        yield return Move.MoveY_Down(Target, rds_cb_release_point.y, () => gantry.SpeedYDown, dock.Pos_Eps);
        yield return new WaitForSeconds(dock.delayBeforeMove);

        yield return SequenceController.WaitWhilePaused();
        yield return Move.Close_Heavy_Gripper_Jaw(Gripper_1_Left_Jaw, Gripper_1_Right_Jaw,
            CB_LeftJawClosePos, CB_RightJawClosePos, gantry.JawPosEps, gantry.HG_Start, gantry.HG_End);
        yield return new WaitForSeconds(dock.delayBeforeMove + 0.8f);

        yield return SequenceController.WaitWhilePaused();
        SetParent_2(ToolSocket, RDS_CB_RB);
        yield return new WaitForSeconds(dock.delayBeforeMove);

        yield return SequenceController.WaitWhilePaused();
        yield return Move.MoveY_Up(Target, gantry.yJoint_2, () => gantry.SpeedYUp, dock.Pos_Eps);
        yield return new WaitForSeconds(dock.delayBeforeMove + 0.3f);

        // 월드 원래 자리로
        yield return SequenceController.WaitWhilePaused();
        yield return Move.MoveXZ(Target, () => gantry.SpeedXZ,
            new Vector3(cb_origin_pos.x, Target.position.y, cb_origin_pos.z), dock.Pos_Eps);
        yield return new WaitForSeconds(dock.delayBeforeMove);

        yield return SequenceController.WaitWhilePaused();
        yield return Move.MoveY_Down(Target, cb_origin_pos.y, () => gantry.SpeedYDown, dock.Pos_Eps);
        yield return new WaitForSeconds(dock.delayBeforeMove);

        yield return SequenceController.WaitWhilePaused();
        yield return Move.Open_Heavy_Gripper_Jaw(Gripper_1_Left_Jaw, Gripper_1_Right_Jaw,
            gantry.JawPosEps, gantry.HG_Start, gantry.HG_End);
        yield return new WaitForSeconds(dock.delayBeforeMove);

        yield return SequenceController.WaitWhilePaused();
        SetParent_2(RDS, RDS_CB_RB);
        yield return new WaitForSeconds(dock.delayBeforeMove);

        Move.FreezeRB(RDS_CB_RB, true, false, true);
        yield return new WaitForSeconds(dock.delayBeforeMove);
        Move.SetGravity(RDS_CB_RB, true);
        yield return new WaitForSeconds(dock.delayBeforeMove + 0.8f);
        Move.FreezeRB(RDS_CB_RB, true, true, true);
        Move.SetGravity(RDS_CB_RB, false);

        yield return SequenceController.WaitWhilePaused();
        yield return Move.MoveY_Up(Target, gantry.yJoint_2, () => gantry.SpeedYUp, dock.Pos_Eps);
        yield return new WaitForSeconds(dock.delayBeforeMove);
    }

    IEnumerator RDS_SR_Move()
    {
        yield return SequenceController.WaitWhilePaused();
        ReadAllPointsFromScene();

        yield return SequenceController.WaitWhilePaused();
        yield return Move.MoveXZ(Target, () => gantry.SpeedXZ,
            new Vector3(rds_sr_point.x, Target.position.y, rds_sr_point.z), dock.Pos_Eps);
        yield return new WaitForSeconds(dock.delayBeforeMove);

        yield return SequenceController.WaitWhilePaused();
        yield return Move.MoveY_Down(Target, rds_sr_point.y, () => gantry.SpeedYDown, dock.Pos_Eps);
        yield return new WaitForSeconds(dock.delayBeforeMove);

        yield return SequenceController.WaitWhilePaused();
        yield return Move.Close_Heavy_Gripper_Jaw(Gripper_1_Left_Jaw, Gripper_1_Right_Jaw,
            SR_LeftJawClosePos, SR_RightJawClosePos, gantry.JawPosEps, gantry.HG_Start, gantry.HG_End);
        yield return new WaitForSeconds(dock.delayBeforeMove + 0.8f);

        yield return SequenceController.WaitWhilePaused();
        SetParent_2(ToolSocket, RDS_SR_RB);
        yield return new WaitForSeconds(dock.delayBeforeMove);

        yield return SequenceController.WaitWhilePaused();
        yield return Move.MoveY_Up(Target, gantry.yJoint_2, () => gantry.SpeedYUp, dock.Pos_Eps);
        yield return new WaitForSeconds(dock.delayBeforeMove + 0.3f);

        // SR release
        yield return SequenceController.WaitWhilePaused();
        yield return Move.MoveXZ(Target, () => gantry.SpeedXZ,
            new Vector3(rds_sr_release_point.x, Target.position.y, rds_sr_release_point.z), dock.Pos_Eps);
        yield return new WaitForSeconds(dock.delayBeforeMove);

        yield return SequenceController.WaitWhilePaused();
        yield return Move.MoveY_Down(Target, rds_sr_release_point.y, () => gantry.SpeedYDown, dock.Pos_Eps);
        yield return new WaitForSeconds(dock.delayBeforeMove);

        yield return SequenceController.WaitWhilePaused();
        yield return Move.Open_Heavy_Gripper_Jaw(Gripper_1_Left_Jaw, Gripper_1_Right_Jaw,
            gantry.JawPosEps, gantry.HG_Start, gantry.HG_End);
        yield return new WaitForSeconds(dock.delayBeforeMove);

        yield return SequenceController.WaitWhilePaused();
        SetParent_2(RDS, RDS_SR_RB);
        yield return new WaitForSeconds(dock.delayBeforeMove);

        Move.FreezeRB(RDS_SR_RB, true, false, true);
        yield return new WaitForSeconds(dock.delayBeforeMove);
        Move.SetGravity(RDS_SR_RB, true);
        yield return new WaitForSeconds(dock.delayBeforeMove + 0.8f);
        Move.FreezeRB(RDS_SR_RB, true, true, true);
        Move.SetGravity(RDS_SR_RB, false);

        yield return SequenceController.WaitWhilePaused();
        yield return Move.MoveY_Up(Target, gantry.yJoint_2, () => gantry.SpeedYUp, dock.Pos_Eps);
        yield return new WaitForSeconds(dock.delayBeforeMove);
    }

    IEnumerator RDS_SR_Move_2()
    {
        yield return SequenceController.WaitWhilePaused();
        ReadAllPointsFromScene();

        // SR release 자리에서 다시 잡기
        yield return SequenceController.WaitWhilePaused();
        yield return Move.MoveXZ(Target, () => gantry.SpeedXZ,
            new Vector3(rds_sr_release_point.x, Target.position.y, rds_sr_release_point.z), dock.Pos_Eps);
        yield return new WaitForSeconds(dock.delayBeforeMove);

        yield return SequenceController.WaitWhilePaused();
        yield return Move.MoveY_Down(Target, rds_sr_release_point.y, () => gantry.SpeedYDown, dock.Pos_Eps);
        yield return new WaitForSeconds(dock.delayBeforeMove);

        yield return SequenceController.WaitWhilePaused();
        yield return Move.Close_Heavy_Gripper_Jaw(Gripper_1_Left_Jaw, Gripper_1_Right_Jaw,
            SR_LeftJawClosePos, SR_RightJawClosePos, gantry.JawPosEps, gantry.HG_Start, gantry.HG_End);
        yield return new WaitForSeconds(dock.delayBeforeMove + 0.8f);

        yield return SequenceController.WaitWhilePaused();
        SetParent_2(ToolSocket, RDS_SR_RB);
        yield return new WaitForSeconds(dock.delayBeforeMove);

        yield return SequenceController.WaitWhilePaused();
        yield return Move.MoveY_Up(Target, gantry.yJoint_2, () => gantry.SpeedYUp, dock.Pos_Eps);
        yield return new WaitForSeconds(dock.delayBeforeMove + 0.3f);

        // SR 원래 위치
        yield return SequenceController.WaitWhilePaused();
        yield return Move.MoveXZ(Target, () => gantry.SpeedXZ,
            new Vector3(sr_origin_pos.x, Target.position.y, sr_origin_pos.z), dock.Pos_Eps);
        yield return new WaitForSeconds(dock.delayBeforeMove);

        yield return SequenceController.WaitWhilePaused();
        yield return Move.MoveY_Down(Target, sr_origin_pos.y, () => gantry.SpeedYDown, dock.Pos_Eps);
        yield return new WaitForSeconds(dock.delayBeforeMove);

        yield return SequenceController.WaitWhilePaused();
        yield return Move.Open_Heavy_Gripper_Jaw(Gripper_1_Left_Jaw, Gripper_1_Right_Jaw,
            gantry.JawPosEps, gantry.HG_Start, gantry.HG_End);
        yield return new WaitForSeconds(dock.delayBeforeMove);

        yield return SequenceController.WaitWhilePaused();
        SetParent_2(RDS, RDS_SR_RB);
        yield return new WaitForSeconds(dock.delayBeforeMove);

        Move.FreezeRB(RDS_SR_RB, true, false, true);
        yield return new WaitForSeconds(dock.delayBeforeMove);
        Move.SetGravity(RDS_SR_RB, true);
        yield return new WaitForSeconds(dock.delayBeforeMove + 0.8f);
        Move.FreezeRB(RDS_SR_RB, true, true, true);
        Move.SetGravity(RDS_SR_RB, false);

        yield return SequenceController.WaitWhilePaused();
        yield return Move.MoveY_Up(Target, gantry.yJoint_2, () => gantry.SpeedYUp, dock.Pos_Eps);
        yield return new WaitForSeconds(dock.delayBeforeMove);
    }

    IEnumerator BC_Move()
    {
        yield return SequenceController.WaitWhilePaused();
        ReadAllPointsFromScene();

        yield return SequenceController.WaitWhilePaused();
        SetParent_4(Basket_Carrier, UB_L, USC_L);

        yield return SequenceController.WaitWhilePaused();
        yield return Move.MoveXZ(Target, () => gantry.SpeedXZ,
            new Vector3(basket_carrier_point.x, Target.position.y, basket_carrier_point.z), dock.Pos_Eps);
        yield return new WaitForSeconds(dock.delayBeforeMove);

        yield return SequenceController.WaitWhilePaused();
        yield return Move.MoveY_Down(Target, basket_carrier_point.y, () => gantry.SpeedYDown, dock.Pos_Eps);
        yield return new WaitForSeconds(dock.delayBeforeMove);

        yield return SequenceController.WaitWhilePaused();
        yield return Move.Close_Heavy_Gripper_Jaw(Gripper_1_Left_Jaw, Gripper_1_Right_Jaw,
            BC_LeftJawClosePos, BC_RightJawClosePos, gantry.JawPosEps, gantry.HG_Start, gantry.HG_End);
        yield return new WaitForSeconds(dock.delayBeforeMove + 0.8f);

        yield return SequenceController.WaitWhilePaused();
        Move.FreezeRB(Basket_Carrier, true, false, true);
        yield return new WaitForSeconds(dock.delayBeforeMove + 0.8f);

        yield return SequenceController.WaitWhilePaused();
        yield return Move.MoveY_Up(Target, gantry.yJoint_2, () => gantry.SpeedYUp, dock.Pos_Eps);
        yield return new WaitForSeconds(dock.delayBeforeMove + 0.3f);

        Move.FreezeRB(Basket_Carrier, false, false, false);

        yield return SequenceController.WaitWhilePaused();
        yield return Move.MoveXZ(Target, () => gantry.SpeedXZ,
            new Vector3(basket_carrier_release_point.x, Target.position.y, basket_carrier_release_point.z), dock.Pos_Eps);
        yield return new WaitForSeconds(dock.delayBeforeMove);

        Move.FreezeRB(Basket_Carrier, true, false, true);
        IgnoreCollision(11, 21, false);
        yield return new WaitForSeconds(dock.delayBeforeMove);

        yield return SequenceController.WaitWhilePaused();
        yield return Move.MoveY_Down(Target, basket_carrier_release_point.y, () => gantry.SpeedYDown, dock.Pos_Eps);
        yield return new WaitForSeconds(dock.delayBeforeMove);

        yield return SequenceController.WaitWhilePaused();
        yield return Move.Open_Heavy_Gripper_Jaw(Gripper_1_Left_Jaw, Gripper_1_Right_Jaw,
            gantry.JawPosEps, gantry.HG_Start, gantry.HG_End);
        yield return new WaitForSeconds(dock.delayBeforeMove);

        Move.FreezeRB(Basket_Carrier, true, true, true);
        yield return new WaitForSeconds(dock.delayBeforeMove);

        yield return SequenceController.WaitWhilePaused();
        yield return Move.MoveY_Up(Target, gantry.yJoint_2, () => gantry.SpeedYUp, dock.Pos_Eps);
        yield return new WaitForSeconds(dock.delayBeforeMove);
    }

    IEnumerator BC_Move_2()
    {
        yield return SequenceController.WaitWhilePaused();
        ReadAllPointsFromScene();

        // 릴리즈 자리에서 다시 집기
        yield return SequenceController.WaitWhilePaused();
        yield return Move.MoveXZ(Target, () => gantry.SpeedXZ,
            new Vector3(basket_carrier_release_point.x, Target.position.y, basket_carrier_release_point.z), dock.Pos_Eps);
        yield return new WaitForSeconds(dock.delayBeforeMove);

        yield return SequenceController.WaitWhilePaused();
        yield return Move.MoveY_Down(Target, basket_carrier_release_point.y, () => gantry.SpeedYDown, dock.Pos_Eps);
        yield return new WaitForSeconds(dock.delayBeforeMove);

        yield return SequenceController.WaitWhilePaused();
        yield return Move.Close_Heavy_Gripper_Jaw(Gripper_1_Left_Jaw, Gripper_1_Right_Jaw,
            BC_LeftJawClosePos, BC_RightJawClosePos, gantry.JawPosEps, gantry.HG_Start, gantry.HG_End);
        yield return new WaitForSeconds(dock.delayBeforeMove + 0.8f);

        IgnoreCollision(11, 21, true);
        Move.FreezeRB(Basket_Carrier, true, false, true);
        yield return new WaitForSeconds(dock.delayBeforeMove + 0.8f);

        yield return SequenceController.WaitWhilePaused();
        yield return Move.MoveY_Up(Target, gantry.yJoint_2, () => gantry.SpeedYUp, dock.Pos_Eps);
        yield return new WaitForSeconds(dock.delayBeforeMove + 0.3f);

        Move.FreezeRB(Basket_Carrier, false, false, false);
        IgnoreCollision(11, 21, false);

        // 원래 BC 자리로
        yield return SequenceController.WaitWhilePaused();
        yield return Move.MoveXZ(Target, () => gantry.SpeedXZ,
            new Vector3(bc_origin_pos.x, Target.position.y, bc_origin_pos.z), dock.Pos_Eps);
        yield return new WaitForSeconds(dock.delayBeforeMove);

        Move.FreezeRB(Basket_Carrier, true, false, true);
        yield return new WaitForSeconds(dock.delayBeforeMove);

        yield return SequenceController.WaitWhilePaused();
        yield return Move.MoveY_Down(Target, bc_origin_pos.y, () => gantry.SpeedYDown, dock.Pos_Eps);
        yield return new WaitForSeconds(dock.delayBeforeMove);

        yield return SequenceController.WaitWhilePaused();
        yield return Move.Open_Heavy_Gripper_Jaw(Gripper_1_Left_Jaw, Gripper_1_Right_Jaw,
            gantry.JawPosEps, gantry.HG_Start, gantry.HG_End);
        yield return new WaitForSeconds(dock.delayBeforeMove);

        Move.SetGravity(Basket_Carrier, true);
        yield return new WaitForSeconds(dock.delayBeforeMove);
        Move.FreezeRB(Basket_Carrier, true, true, true);
        yield return new WaitForSeconds(dock.delayBeforeMove);
        Move.SetGravity(Basket_Carrier, false);

        yield return SequenceController.WaitWhilePaused();
        yield return Move.MoveY_Up(Target, gantry.yJoint_2, () => gantry.SpeedYUp, dock.Pos_Eps);
        yield return new WaitForSeconds(dock.delayBeforeMove);
    }

    // ===== 유니티 히터 이동 (초 기반) =====
    IEnumerator Heater_Move(Transform a, Transform b, Vector3 left_target_pos, Vector3 right_target_pos, float seconds, float eps)
    {
        yield return SequenceController.WaitWhilePaused();

        if (!a || !b) yield break;

        if (seconds <= 0f)
        {
            a.localPosition = left_target_pos;
            b.localPosition = right_target_pos;
            yield break;
        }

        Vector3 aStart = a.localPosition;
        Vector3 bStart = b.localPosition;

        float t = 0f;
        while (t < 1f)
        {
            yield return SequenceController.WaitWhilePaused();

            t += Time.deltaTime / seconds;
            float u = Mathf.Clamp01(t);

            a.localPosition = Vector3.Lerp(aStart, left_target_pos, u);
            b.localPosition = Vector3.Lerp(bStart, right_target_pos, u);

            bool aDone = (a.localPosition - left_target_pos).sqrMagnitude <= eps * eps;
            bool bDone = (b.localPosition - right_target_pos).sqrMagnitude <= eps * eps;
            if (aDone && bDone) break;

            yield return null;
        }

        a.localPosition = left_target_pos;
        b.localPosition = right_target_pos;
    }

    // ===== 단일 공정 진입점 (LIBS는 제거) =====
    IEnumerator Run_BC_Load()
    {
        isProcessing = true;

        yield return SequenceController.WaitWhilePaused();
        yield return dock.Start_Dock_Heavy_Gripper();
        yield return new WaitForSeconds(dock.delayBeforeMove);

        yield return RDS_CC_Move();
        yield return BC_Move();
        yield return RDS_CC_Move_2();

        UILogger.Instance?.Log("증류 공정 진행(BC 로딩 완료)");
        isProcessing = false;
    }

    IEnumerator Run_BC_Unload()
    {
        isProcessing = true;

        yield return SequenceController.WaitWhilePaused();
        yield return dock.Start_Dock_Heavy_Gripper();
        yield return new WaitForSeconds(dock.delayBeforeMove);

        yield return RDS_CC_Move();
        yield return BC_Move_2();
        yield return RDS_CB_Move();
        yield return RDS_SR_Move();

        UILogger.Instance?.Log("BC 언로딩 및 내부 이송 완료");
        isProcessing = false;
    }

    IEnumerator Run_Collect_And_Finish()
    {
        isProcessing = true;

        yield return SequenceController.WaitWhilePaused();
        yield return dock.Start_Dock_Heavy_Gripper();
        yield return new WaitForSeconds(dock.delayBeforeMove);

        yield return RDS_SR_Move_2();
        yield return RDS_CB_Move_2();
        yield return RDS_CC_Move_2();

        UILogger.Instance?.Log("증류 공정 종료(물질 수거 완료)");
        isProcessing = false;
    }

    // ===== 헬퍼 =====
    void SetParent_2(Transform root, Rigidbody child)
    {
        if (!root || !child) return;
        child.transform.SetParent(root, true);
    }

    void SetParent_2(Transform root, Rigidbody[] children)
    {
        if (!root || children == null) return;
        foreach (var c in children)
        {
            if (c) c.transform.SetParent(root, true);
        }
    }

    void SetParent_2(Transform root, Transform child)
    {
        if (!root || !child) return;
        child.SetParent(root, true);
    }

    void SetParent_4(Rigidbody parent, Transform child1, Transform child2)
    {
        if (parent == null) return;

        if (child1)
        {
            Rigidbody rb = child1.GetComponent<Rigidbody>();
            if (rb) Destroy(rb);
            child1.SetParent(parent.transform);
        }

        if (child2)
        {
            Rigidbody rb = child2.GetComponent<Rigidbody>();
            if (rb) Destroy(rb);
            child2.SetParent(parent.transform);
        }

        int bcLayer = LayerMask.NameToLayer("BC");
        if (bcLayer >= 0)
        {
            foreach (Transform t in parent.GetComponentsInChildren<Transform>(true))
                t.gameObject.layer = bcLayer;
        }
    }

    void IgnoreCollision(int layer1, int layer2, bool on)
    {
        if (layer1 < 0 || layer2 < 0) return;
        Physics.IgnoreLayerCollision(layer1, layer2, on);
    }

}
