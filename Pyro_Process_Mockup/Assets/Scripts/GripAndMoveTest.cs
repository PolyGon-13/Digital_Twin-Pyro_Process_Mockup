using UnityEngine;
using System.Collections;

public class GripAndMoveTest : MonoBehaviour
{
    [Header("Gantry / Dock 연결")]
    [SerializeField] private GantryIKArticulation gantry;
    [SerializeField] private DockAndAttach dock;

    Transform _target;
    Transform _toolSocket;
    ArticulationBody _jawL;
    ArticulationBody _jawR;
    ArticulationBody _yJoint;

    float Pos_Eps => dock ? dock.Pos_Eps : 0.01f;
    float Delay => dock ? dock.delayBeforeMove : 0.2f;

    [Header("이동할 Rigidbody 대상")]
    public Rigidbody moveRb;

    [Header("그립 / 릴리즈 포인트")]
    public Transform gripPoint;
    public Transform releasePoint;
    public Transform releaseParent;

    [Header("Jaw 설정 (Heavy Gripper)")]
    public float LeftJawClosePos = 0.062f;
    public float RightJawClosePos = -0.062f;

    [Header("물리 옵션")]
    public bool useDropGravityPulse = true;
    public bool useFreezeForStabilize = true;

    [Header("테스트 Key")]
    public KeyCode runKey = KeyCode.T;

    bool _busy;
    Transform _carriedTransform;
    bool _rbRemoved = false;

    struct RBBackup
    {
        public float mass, drag, angularDrag;
        public bool useGravity, isKinematic;
        public RigidbodyInterpolation interpolation;
        public CollisionDetectionMode collisionMode;
    }
    RBBackup? _rbBackup;

    void Reset()
    {
        gantry = GetComponentInParent<GantryIKArticulation>();
        dock = GetComponent<DockAndAttach>();
    }

    void Awake()
    {
        if (!gantry) gantry = GetComponentInParent<GantryIKArticulation>();
        if (!dock) dock = GetComponent<DockAndAttach>();

        _target = gantry ? gantry.Target : null;
        _toolSocket = gantry ? gantry.toolSocket : null;
        _jawL = gantry ? gantry.Heavy_Gripper_Jaw_Left : null;
        _jawR = gantry ? gantry.Heavy_Gripper_Jaw_Right : null;
        _yJoint = gantry ? gantry.yJoint_2 : null;
    }

    void Update()
    {
        if (!_busy && Input.GetKeyDown(runKey))
            StartCoroutine(RunOnce());
    }

    public IEnumerator RunOnce()
    {
        if (_busy) yield break;
        if (!_target || !_toolSocket || !_jawL || !_jawR || !_yJoint || !moveRb || !gripPoint || !releasePoint)
        {
            Debug.LogWarning("[GripAndMoveTest] 필수 참조 누락");
            yield break;
        }

        _busy = true;
        if (dock) { dock.SetBusy(true); dock.SetInputEnabled(false); }

        // 1) XZ 이동 (픽업 위치)
        yield return Move.MoveXZ(_target, () => gantry.SpeedXZ,
            new Vector3(gripPoint.position.x, _target.position.y, gripPoint.position.z), Pos_Eps);
        yield return new WaitForSeconds(Delay);

        // 2) Y Down
        yield return Move.MoveY_Down(_target, gripPoint.position.y, () => gantry.SpeedYDown, Pos_Eps);
        yield return new WaitForSeconds(Delay);

        // 3) Jaw Close
        yield return Move.Close_Heavy_Gripper_Jaw(
            _jawL, _jawR,
            LeftJawClosePos, RightJawClosePos,
            gantry.JawPosEps, gantry.HG_Start, gantry.HG_End);
        yield return new WaitForSeconds(Delay + 0.8f);

        // 4) ToolSocket에 부착 + Rigidbody 제거 + Collider Off
        _carriedTransform = moveRb.transform;
        SetParent_2(_toolSocket, _carriedTransform);
        RemoveRBAndDisableAllColliders(_carriedTransform, ref moveRb);
        _rbRemoved = true;
        yield return new WaitForSeconds(Delay);

        // 5) Y Up
        yield return Move.MoveY_Up(_target, _yJoint, () => gantry.SpeedYUp, Pos_Eps);
        yield return new WaitForSeconds(Delay + 0.3f);

        // 6) XZ 이동 (릴리즈 위치)
        yield return Move.MoveXZ(_target, () => gantry.SpeedXZ,
            new Vector3(releasePoint.position.x, _target.position.y, releasePoint.position.z), Pos_Eps);
        yield return new WaitForSeconds(Delay);

        // 7) Y Down
        yield return Move.MoveY_Down(_target, releasePoint.position.y, () => gantry.SpeedYDown, Pos_Eps);
        yield return new WaitForSeconds(Delay);

        // 8) Jaw Open
        yield return Move.Open_Heavy_Gripper_Jaw(
            _jawL, _jawR,
            gantry.JawPosEps, gantry.HG_Start, gantry.HG_End);
        yield return new WaitForSeconds(Delay);

        // 9) Rigidbody 복구 + 부모 복원
        if (releaseParent) SetParent_2(releaseParent, _carriedTransform);
        RestoreRBAndEnableAllColliders(_carriedTransform, out moveRb);

        // 10) Y Up (복귀)
        yield return Move.MoveY_Up(_target, _yJoint, () => gantry.SpeedYUp, Pos_Eps);
        yield return new WaitForSeconds(Delay);

        if (dock) { dock.SetInputEnabled(true); dock.SetBusy(false); }
        _busy = false;
    }

    // ---------- Helpers ----------

    void SetParent_2(Transform newParent, Transform childRoot)
    {
        if (!newParent || !childRoot) return;
        childRoot.SetParent(newParent, true);
    }

    void RemoveRBAndDisableAllColliders(Transform root, ref Rigidbody rb)
    {
        if (!root) return;
        var colliders = root.GetComponentsInChildren<Collider>(true);
        foreach (var col in colliders) col.enabled = false;

        if (rb)
        {
            _rbBackup = new RBBackup
            {
                mass = rb.mass,
                drag = rb.linearDamping,
                angularDrag = rb.angularDamping,
                useGravity = rb.useGravity,
                isKinematic = rb.isKinematic,
                interpolation = rb.interpolation,
                collisionMode = rb.collisionDetectionMode
            };
            Destroy(rb);
            rb = null;
        }

        var extraRBs = root.GetComponentsInChildren<Rigidbody>(true);
        foreach (var r in extraRBs) Destroy(r);
    }

    void RestoreRBAndEnableAllColliders(Transform root, out Rigidbody newRb)
    {
        newRb = null;
        if (!root) return;

        var colliders = root.GetComponentsInChildren<Collider>(true);
        foreach (var col in colliders) col.enabled = true;

        newRb = root.gameObject.AddComponent<Rigidbody>();

        if (_rbBackup.HasValue)
        {
            var b = _rbBackup.Value;
            newRb.mass = Mathf.Max(0.0001f, b.mass);
            newRb.linearDamping = b.drag;
            newRb.angularDamping = b.angularDrag;
        }
        else
        {
            newRb.mass = 1f;
            newRb.linearDamping = 0f;
            newRb.angularDamping = 0.05f;
        }

        newRb.interpolation = RigidbodyInterpolation.Interpolate;
        newRb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        newRb.useGravity = true;
        newRb.isKinematic = false;

        newRb.constraints =
            RigidbodyConstraints.FreezePositionX |
            RigidbodyConstraints.FreezePositionZ |
            RigidbodyConstraints.FreezeRotationX |
            RigidbodyConstraints.FreezeRotationY |
            RigidbodyConstraints.FreezeRotationZ;

        _rbRemoved = false;
    }
}
