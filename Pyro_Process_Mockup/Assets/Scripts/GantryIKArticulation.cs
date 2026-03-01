using UnityEngine;
using System.Collections;

public class GantryIKArticulation : MonoBehaviour
{
    // === 전역 스톱 플래그 (버튼에서 토글) ===
    // 이게 true면 이 스크립트는 그 프레임에 아무 이동도 하지 않는다.
    // 다른 스크립트에서도 이 값만 보면 됨.
    [Header("Global Stop (All Gantries)")]
    public static bool GlobalGantryStop = false;

    // === 통합 이동/조 속도 정의 ===
    [Header("Unified Motion Speeds")]
    [Tooltip("XZ 평면 이동 m/s")]
    public float speedXZ = 0.5f;

    [Tooltip("Y 위로 올림 m/s")]
    public float speedYUp = 0.3f;

    [Tooltip("Y 아래로 내림 m/s")]
    public float speedYDown = 0.3f;

    [Header("Unified Jaw Speeds (Heavy Gripper)")]
    [Tooltip("Heavy Gripper Jaw 시작 속도(스무스 곡선의 초반부)")]
    public float HG_StartSpeed = 0.1f;
    [Tooltip("Heavy Gripper Jaw 종료 속도(스무스 곡선의 말미)")]
    public float HG_EndSpeed = 0.02f;

    [Header("Unified Jaw Speeds (Angular Gripper)")]
    [Tooltip("Angular Gripper Jaw 시작 속도")]
    public float AG_StartSpeed = 0.01f;
    [Tooltip("Angular Gripper Jaw 종료 속도")]
    public float AG_EndSpeed = 0.0001f;

    [Header("Jaw Epsilon (공통)")]
    [Tooltip("조(Jaw) 목표 위치 판정 오차 허용값")]
    public float jawPosEps = 0.0005f;

    // 외부 참조용 읽기 전용 프로퍼티
    public float SpeedXZ => speedXZ;
    public float SpeedYUp => speedYUp;
    public float SpeedYDown => speedYDown;
    public float HG_Start => HG_StartSpeed;
    public float HG_End => HG_EndSpeed;
    public float AG_Start => AG_StartSpeed;
    public float AG_End => AG_EndSpeed;
    public float JawPosEps => jawPosEps;

    [Header("Target")]
    public Transform Target;
    public Transform toolSocket;

    [Header("Joint")]
    public ArticulationBody zJoint;
    public ArticulationBody xJoint;
    public ArticulationBody yJoint_2;

    [Header("Heavy Gripper Jaw")]
    public ArticulationBody Heavy_Gripper_Jaw_Left;
    public ArticulationBody Heavy_Gripper_Jaw_Right;

    [Header("Heavy Gripper Jaw_Setup")]
    DockAndAttach dock; // 그리퍼 장착여부 확인용
    Vector3 HG_Jaw_Left_Anchor_Rotation = new Vector3(0f, 0f, 180f);
    Vector3 HG_Jaw_Right_Anchor_Rotation = new Vector3(0f, 0f, 180f);
    Vector2 HG_Jaw_Left_Limit = new Vector2(-0.182f, 0.182f);
    Vector2 HG_Jaw_Right_Limit = new Vector2(-0.182f, 0.182f);
    float HG_Jaw_Open_Target = 0f;
    KeyCode HG_Jaw_Close_Key = KeyCode.K;
    KeyCode HG_Jaw_Open_Key = KeyCode.L;
    bool HG_Jaw_Left_Invert = true;
    bool HG_Jaw_Right_Invert = true;

    [Header("Angular Gripper Jaw")]
    public ArticulationBody Angular_Gripper_Jaw_Left;
    public ArticulationBody Angular_Gripper_Jaw_Right;
    public ArticulationBody Angular_Gripper_Jaw_Center;

    [Header("Angular Gripper Jaw Setup")]
    Vector3 AG_Jaw_Left_Anchor_Rotation = new Vector3(0f, 180f, 0f);
    Vector3 AG_Jaw_Right_Anchor_Rotation = new Vector3(0f, 270f, 0f);
    Vector3 AG_Jaw_Center_Anchor_Rotation = new Vector3(0f, 0f, 0f);
    [HideInInspector] public Vector2 AG_Jaw_Left_Limit = new Vector2(-0.02f, 0.0029f);
    Vector2 AG_Jaw_Right_Limit = new Vector2(-0.02f, 0.0029f);
    Vector2 AG_Jaw_Center_Limit = new Vector2(-0.02f, 0.0029f);
    float AG_Jaw_Open_Target = 0f;
    KeyCode AG_Jaw_Close_Key = KeyCode.C;
    KeyCode AG_Jaw_Open_Key = KeyCode.V;
    bool AG_Jaw_Left_Invert = false;
    bool AG_Jaw_Right_Invert = false;
    bool AG_Jaw_Center_Invert = false;

    [Header("Calibration")]
    Vector3 originWorld;
    Vector3 zAxisW, xAxisW, yAxisW;
    bool calibrated = false;
    bool useProjectedClamp = true;

    [Header("Axis Options")]
    bool invertX = false;
    bool invertY = true;
    bool invertZ = false;
    Vector3 axisScale = Vector3.one;

    Vector2 xLimit = new Vector2(-4f, 4f);
    Vector2 yLimit = new Vector2(0f, 3f);
    Vector2 zLimit = new Vector2(-3f, 2f);

    float maxStepPerSec = 100f; // IK 타겟을 조인트로 보낼 때 초당 최대 이동량(드라이브 타겟 단위)

    Vector3 zeroLocal;
    Transform root;

    [Header("Manual Gantry Control (A/D, W/S, Q/E)")]
    KeyCode moveLeft = KeyCode.A;
    KeyCode moveRight = KeyCode.D;
    KeyCode moveForward = KeyCode.S;
    KeyCode moveBack = KeyCode.W;
    KeyCode moveDown = KeyCode.E;
    KeyCode moveUp = KeyCode.Q;

    void Awake()
    {
        root = transform;
        dock = GetComponentInChildren<DockAndAttach>();

        if (!Target || !toolSocket)
            Debug.LogError("[GantryIK] Assign Transform.");
        else if (!xJoint || !yJoint_2 || !zJoint ||
                 !Heavy_Gripper_Jaw_Left || !Heavy_Gripper_Jaw_Right ||
                 !Angular_Gripper_Jaw_Left || !Angular_Gripper_Jaw_Right || !Angular_Gripper_Jaw_Center)
            Debug.LogError("[GantryIK] Assign Articulation Body.");
    }

    void Start()
    {
        CalibrateZero();

        SetupJointDrive(ref zJoint);
        SetupJointDrive(ref xJoint);
        SetupJointDrive(ref yJoint_2);

        HG_SetupJawDrive(ref Heavy_Gripper_Jaw_Left, true);
        HG_SetupJawDrive(ref Heavy_Gripper_Jaw_Right, false);

        AG_SetupJawDrive(ref Angular_Gripper_Jaw_Left, 1);
        AG_SetupJawDrive(ref Angular_Gripper_Jaw_Right, 2);
        AG_SetupJawDrive(ref Angular_Gripper_Jaw_Center, 3);

        BuildAxes();
        CalibrateOriginFromCurrentPose();
    }

    void Update()
    {
        if (!Target) return;

        // === STOP이 켜져 있으면 이 프레임에서는 아무것도 안 함 ===
        // (가장 우선시)
        if (GlobalGantryStop)
        {
            // 여기서 굳이 드라이브 타겟을 건드릴 필요는 없다.
            // 현재 target 값 그대로 두면 "그 시점 자리"에 멈춘 상태가 된다.
            // 필요하면 여기서 조도 막아둔다.
            return;
        }

        // 키보드 수동 이동 (Target 직접 이동)
        KeyBoardGantryMove();

        // IK 업데이트 (Target → Joint 드라이브 타겟)
        float gantryStep = maxStepPerSec * Time.deltaTime;
        UpdateGantryIK(gantryStep);

        // 워크스페이스 제한
        if (useProjectedClamp) ClampTargetToGantryLimitsProjected();
        else ClampTargetToGantryLimitsLocal();

        // 수동 Heavy Jaw 조작 (통합 속도 사용)
        if (dock && dock.Is_Hg_Attached)
        {
            float jawStep = Mathf.Max(0f, HG_StartSpeed) * Time.deltaTime;
            if (Input.GetKey(HG_Jaw_Close_Key)) Heavy_Gripper_Jaw_Close(jawStep);
            else if (Input.GetKey(HG_Jaw_Open_Key)) Heavy_Gripper_Jaw_Open(jawStep);
        }

        // 수동 Angular Jaw 조작 (통합 속도 사용)
        if (dock && dock.Is_Ag_Attached)
        {
            if (Input.GetKey(AG_Jaw_Close_Key))
                StartCoroutine(Move.Close_Angular_Gripper_Jaw(
                    Angular_Gripper_Jaw_Left, Angular_Gripper_Jaw_Right, Angular_Gripper_Jaw_Center,
                    AG_Jaw_Left_Limit.y, jawPosEps, AG_StartSpeed, AG_EndSpeed));
            else if (Input.GetKey(AG_Jaw_Open_Key))
                StartCoroutine(Move.Open_Angular_Gripper_Jaw(
                    Angular_Gripper_Jaw_Left, Angular_Gripper_Jaw_Right, Angular_Gripper_Jaw_Center,
                    jawPosEps, AG_StartSpeed, AG_EndSpeed));
        }

        // 축/원점 재설정
        if (Input.GetKeyDown(KeyCode.R))
        {
            BuildAxes();
            CalibrateOriginFromCurrentPose();
        }
    }

    // ========= 외부에서 스톱 토글용 =========
    public static void SetGantryStop(bool stop)
    {
        GlobalGantryStop = stop;
    }

    public static bool IsGantryStopped()
    {
        return GlobalGantryStop;
    }

    // ========= 초기화/세팅 =========

    void CalibrateZero()
    {
        // ToolSocket(툴 좌표)를 root(Gantry) 좌표 기준 원점으로 환산
        zeroLocal = root.InverseTransformPoint(toolSocket.position);
    }

    void SetupJointDrive(ref ArticulationBody j)
    {
        if (j == null) return;

        var d = j.xDrive;
        if (d.stiffness < 1f) d.stiffness = 100000f;
        if (d.damping < 1f) d.damping = 10000f;
        if (d.forceLimit < 1f) d.forceLimit = float.PositiveInfinity;
        j.xDrive = d;
        j.useGravity = false;
    }

    public void ReApply_Jaw_Setup(int num)
    {
        switch (num)
        {
            case 1:
                HG_SetupJawDrive(ref Heavy_Gripper_Jaw_Left, true);
                HG_SetupJawDrive(ref Heavy_Gripper_Jaw_Right, false);
                break;
            case 2:
                AG_SetupJawDrive(ref Angular_Gripper_Jaw_Left, 1);
                AG_SetupJawDrive(ref Angular_Gripper_Jaw_Right, 2);
                AG_SetupJawDrive(ref Angular_Gripper_Jaw_Center, 3);
                break;
            default:
                return;
        }
    }

    // Heavy Gripper Jaw Setup
    void HG_SetupJawDrive(ref ArticulationBody j, bool isLeft)
    {
        if (j == null) return;

        var d = j.xDrive;
        if (d.stiffness < 1f) d.stiffness = 100000f;
        if (d.damping < 1f) d.damping = 10000f;
        if (d.forceLimit < 1f) d.forceLimit = float.PositiveInfinity;

        if (isLeft)
        {
            j.anchorRotation = Quaternion.Euler(HG_Jaw_Left_Anchor_Rotation);
            d.lowerLimit = HG_Jaw_Left_Limit.x;
            d.upperLimit = HG_Jaw_Left_Limit.y;
        }
        else
        {
            j.anchorRotation = Quaternion.Euler(HG_Jaw_Right_Anchor_Rotation);
            d.lowerLimit = HG_Jaw_Right_Limit.x;
            d.upperLimit = HG_Jaw_Right_Limit.y;
        }
        d.target = Mathf.Clamp(HG_Jaw_Open_Target, d.lowerLimit, d.upperLimit);

        j.xDrive = d;
        j.useGravity = false;
    }

    // Angular Gripper Jaw Setup
    void AG_SetupJawDrive(ref ArticulationBody j, int num)
    {
        if (j == null) return;

        Vector3 anchorEuler;
        Vector2 limit;

        switch (num)
        {
            case 1: anchorEuler = AG_Jaw_Left_Anchor_Rotation; limit = AG_Jaw_Left_Limit; break;
            case 2: anchorEuler = AG_Jaw_Right_Anchor_Rotation; limit = AG_Jaw_Right_Limit; break;
            case 3: anchorEuler = AG_Jaw_Center_Anchor_Rotation; limit = AG_Jaw_Center_Limit; break;
            default: return;
        }

        var d = j.xDrive;
        if (d.stiffness < 1f) d.stiffness = 100000f;
        if (d.damping < 1f) d.damping = 10000f;
        if (d.forceLimit < 1f) d.forceLimit = float.PositiveInfinity;

        j.anchorRotation = Quaternion.Euler(anchorEuler);
        d.lowerLimit = limit.x;
        d.upperLimit = limit.y;
        d.target = Mathf.Clamp(d.target, d.lowerLimit, d.upperLimit);

        j.xDrive = d;
        j.useGravity = false;
    }

    // ========= 축/원점/클램프 =========

    void BuildAxes()
    {
        zAxisW = (zJoint) ? (zJoint.transform.rotation * zJoint.anchorRotation * Vector3.right).normalized : Vector3.forward;
        xAxisW = (xJoint) ? (xJoint.transform.rotation * xJoint.anchorRotation * Vector3.right).normalized : Vector3.right;
        yAxisW = (yJoint_2) ? (yJoint_2.transform.rotation * yJoint_2.anchorRotation * Vector3.right).normalized : Vector3.up;
    }

    void CalibrateOriginFromCurrentPose()
    {
        if (!zJoint || !xJoint || !yJoint_2 || !toolSocket)
        {
            calibrated = false;
            return;
        }

        float zPos = GetJointPosMeters(zJoint);
        float xPos = GetJointPosMeters(xJoint);
        float yPos = GetJointPosMeters(yJoint_2);

        // 현재 ToolSocket에서 각 축 성분을 빼서 Gantry 원점 추정
        originWorld = toolSocket.position - (zAxisW * zPos) - (xAxisW * xPos) - (yAxisW * yPos);
        calibrated = true;
    }

    // ========= IK / 수동 이동 =========

    void SetJointTarget(ArticulationBody j, float wanted, float maxDelta)
    {
        var d = j.xDrive;
        float curr = d.target;
        float next = Mathf.MoveTowards(curr, wanted, maxDelta);
        d.target = next;
        j.xDrive = d;
    }

    void KeyBoardGantryMove()
    {
        float dx = 0f; if (Input.GetKey(moveLeft)) dx += 1f; if (Input.GetKey(moveRight)) dx -= 1f;
        float dz = 0f; if (Input.GetKey(moveForward)) dz += 1f; if (Input.GetKey(moveBack)) dz -= 1f;
        float dy = 0f; if (Input.GetKey(moveDown)) dy -= 1f; if (Input.GetKey(moveUp)) dy += 1f;

        if (invertX) dx = -dx;
        if (invertZ) dz = -dz;
        if (invertY) dy = -dy;

        Vector3 delta = new Vector3(dx, dy, dz);
        if (delta.sqrMagnitude > 1e-6f && Target)
        {
            Target.position += delta.normalized * Mathf.Max(0f, speedXZ) * Time.deltaTime;
        }
    }

    void UpdateGantryIK(float step)
    {
        if (!Target || !root) return;

        // Target의 월드좌표를 Gantry 로컬로 변환
        Vector3 targetLocal = root.InverseTransformPoint(Target.position);

        // zeroLocal 기준 오프셋
        Vector3 delta = targetLocal - zeroLocal;

        float zTarget = delta.z * (invertZ ? -1f : 1f) * axisScale.x;
        float xTarget = delta.x * (invertX ? -1f : 1f) * axisScale.y;
        float yTarget = delta.y * (invertY ? -1f : 1f) * axisScale.z;

        // 이동 범위 제한
        zTarget = Mathf.Clamp(zTarget, zLimit.x, zLimit.y);
        xTarget = Mathf.Clamp(xTarget, xLimit.x, xLimit.y);
        yTarget = Mathf.Clamp(yTarget, yLimit.x, yLimit.y);

        SetJointTarget(zJoint, zTarget, step);
        SetJointTarget(xJoint, xTarget, step);
        SetJointTarget(yJoint_2, yTarget, step);
    }

    void ClampTargetToGantryLimitsLocal()
    {
        if (!Target || !root) return;

        Vector3 local = root.InverseTransformPoint(Target.position);
        Vector3 d = local - zeroLocal;

        float zCmd = Mathf.Clamp(d.z * (invertZ ? -1f : 1f) * axisScale.x, zLimit.x, zLimit.y);
        float xCmd = Mathf.Clamp(d.x * (invertX ? -1f : 1f) * axisScale.y, xLimit.x, xLimit.y);
        float yCmd = Mathf.Clamp(d.y * (invertY ? -1f : 1f) * axisScale.z, yLimit.x, yLimit.y);

        float sx = (axisScale.x != 0f) ? (zCmd / axisScale.x) : 0f; // 주의: x/y/z축 스케일 매핑
        float sy = (axisScale.z != 0f) ? (yCmd / axisScale.z) : 0f;
        float sz = (axisScale.x != 0f) ? (zCmd / axisScale.x) : 0f;

        float newLocalX = (invertX ? -(xCmd / axisScale.y) : (xCmd / axisScale.y));
        float newLocalY = (invertY ? -(yCmd / axisScale.z) : (yCmd / axisScale.z));
        float newLocalZ = (invertZ ? -(zCmd / axisScale.x) : (zCmd / axisScale.x));

        Vector3 clampedLocal = zeroLocal + new Vector3(newLocalX, newLocalY, newLocalZ);
        Target.position = root.TransformPoint(clampedLocal);
    }

    void ClampTargetToGantryLimitsProjected()
    {
        if (!calibrated || !zJoint || !xJoint || !yJoint_2 || !Target) return;

        var z = zJoint.xDrive;
        var x = xJoint.xDrive;
        var y = yJoint_2.xDrive;

        float zMin = Mathf.Min(z.lowerLimit, z.upperLimit);
        float zMax = Mathf.Max(z.lowerLimit, z.upperLimit);
        float xMin = Mathf.Min(x.lowerLimit, x.upperLimit);
        float xMax = Mathf.Max(x.lowerLimit, x.upperLimit);
        float yMin = Mathf.Min(y.lowerLimit, y.upperLimit);
        float yMax = Mathf.Max(y.lowerLimit, y.upperLimit);

        Vector3 o = calibrated ? originWorld : Vector3.zero;

        // Target 위치를 축으로 투영
        Vector3 rel = Target.position - o;
        float sz = Vector3.Dot(rel, zAxisW);
        float sx = Vector3.Dot(rel, xAxisW);
        float sy = Vector3.Dot(rel, yAxisW);

        // 범위 클램프
        sz = Mathf.Clamp(sz, zMin, zMax);
        sx = Mathf.Clamp(sx, xMin, xMax);
        sy = Mathf.Clamp(sy, yMin, yMax);

        // 재합성
        Vector3 pClamped = o + zAxisW * sz + xAxisW * sx + yAxisW * sy;
        Target.position = pClamped;
    }

    float GetJointPosMeters(ArticulationBody j)
    {
        return (j != null && j.jointPosition.dofCount > 0) ? (float)j.jointPosition[0] : 0f;
    }

    // ========= Jaw 수동 동작(키) =========

    void Heavy_Gripper_Jaw_Close(float step)
    {
        if (Heavy_Gripper_Jaw_Left)
        {
            var d = Heavy_Gripper_Jaw_Left.xDrive;
            float closeTgt = HG_Jaw_Left_Invert ? d.lowerLimit : d.upperLimit;
            d.target = Mathf.MoveTowards(d.target, closeTgt, step);
            Heavy_Gripper_Jaw_Left.xDrive = d;
        }
        if (Heavy_Gripper_Jaw_Right)
        {
            var d = Heavy_Gripper_Jaw_Right.xDrive;
            float closeTgt = HG_Jaw_Right_Invert ? d.upperLimit : d.lowerLimit;
            d.target = Mathf.MoveTowards(d.target, closeTgt, step);
            Heavy_Gripper_Jaw_Right.xDrive = d;
        }
    }

    void Heavy_Gripper_Jaw_Open(float step)
    {
        if (Heavy_Gripper_Jaw_Left)
        {
            var d = Heavy_Gripper_Jaw_Left.xDrive;
            float openTgt = Mathf.Clamp(HG_Jaw_Open_Target, d.lowerLimit, d.upperLimit);
            d.target = Mathf.MoveTowards(d.target, openTgt, step);
            Heavy_Gripper_Jaw_Left.xDrive = d;
        }
        if (Heavy_Gripper_Jaw_Right)
        {
            var d = Heavy_Gripper_Jaw_Right.xDrive;
            float openTgt = Mathf.Clamp(HG_Jaw_Open_Target, d.lowerLimit, d.upperLimit);
            d.target = Mathf.MoveTowards(d.target, openTgt, step);
            Heavy_Gripper_Jaw_Right.xDrive = d;
        }
    }

    void Angular_Gripper_Jaw_Close(float step)
    {
        if (Angular_Gripper_Jaw_Left)
        {
            var d = Angular_Gripper_Jaw_Left.xDrive;
            float closeTgt = AG_Jaw_Left_Invert ? d.lowerLimit : d.upperLimit;
            d.target = Mathf.MoveTowards(d.target, closeTgt, step);
            Angular_Gripper_Jaw_Left.xDrive = d;
        }
        if (Angular_Gripper_Jaw_Right)
        {
            var d = Angular_Gripper_Jaw_Right.xDrive;
            float closeTgt = AG_Jaw_Right_Invert ? d.lowerLimit : d.upperLimit;
            d.target = Mathf.MoveTowards(d.target, closeTgt, step);
            Angular_Gripper_Jaw_Right.xDrive = d;
        }
        if (Angular_Gripper_Jaw_Center)
        {
            var d = Angular_Gripper_Jaw_Center.xDrive;
            float closeTgt = AG_Jaw_Center_Invert ? d.lowerLimit : d.upperLimit;
            d.target = Mathf.MoveTowards(d.target, closeTgt, step);
            Angular_Gripper_Jaw_Center.xDrive = d;
        }
    }

    void Angular_Gripper_Jaw_Open(float step)
    {
        if (Angular_Gripper_Jaw_Left)
        {
            var d = Angular_Gripper_Jaw_Left.xDrive;
            float openTgt = Mathf.Clamp(AG_Jaw_Open_Target, d.lowerLimit, d.upperLimit);
            d.target = Mathf.MoveTowards(d.target, openTgt, step);
            Angular_Gripper_Jaw_Left.xDrive = d;
        }
        if (Angular_Gripper_Jaw_Right)
        {
            var d = Angular_Gripper_Jaw_Right.xDrive;
            float openTgt = Mathf.Clamp(AG_Jaw_Open_Target, d.lowerLimit, d.upperLimit);
            d.target = Mathf.MoveTowards(d.target, openTgt, step);
            Angular_Gripper_Jaw_Right.xDrive = d;
        }
        if (Angular_Gripper_Jaw_Center)
        {
            var d = Angular_Gripper_Jaw_Center.xDrive;
            float openTgt = Mathf.Clamp(AG_Jaw_Open_Target, d.lowerLimit, d.upperLimit);
            d.target = Mathf.MoveTowards(d.target, openTgt, step);
            Angular_Gripper_Jaw_Center.xDrive = d;
        }
    }
}
