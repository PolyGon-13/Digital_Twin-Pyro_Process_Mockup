using UnityEngine;
using System.Collections;

public class LIBS : MonoBehaviour
{
    [Header("Ref Scripts / Objects")]
    public GantryIKArticulation gantry;
    public DockAndAttach dock;

    [Header("Scene Roots")]
    public Transform Dry_Mock_Up;
    public Transform ToolSocket;

    [Header("Dipstick")]
    public Rigidbody DipStick;

    [Header("Dipstick Points")]
    public Transform Dipstick_Point;
    public Transform Dipstick_Release_Point;
    public Transform ER_Release_Point;

    [Header("A.Grip (Transform 기반)")]
    public Transform JawL;
    public Transform JawR;
    public Transform JawC;

    Vector3 jawL_open_pos, jawR_open_pos, jawC_open_pos;
    Quaternion jawL_open_rot, jawR_open_rot, jawC_open_rot;

    public float jawCloseOffset = 0.02f;
    public float jawCCloseOffset = 0.0f;
    public float jawMoveTime = 0.25f;

    Transform _target;
    Transform _yJointDummy;
    Vector3 _dipPos;
    Vector3 _dipReleasePos;
    Vector3 _erReleasePos;

    bool _isRunning = false;

    bool _isCarryingDipstick = false;
    Transform _carriedTransform = null;

    Rigidbody _carriedRb = null;
    bool _rbWasKinematic = false;
    RigidbodyConstraints _rbOldConstraints = RigidbodyConstraints.None;

    Vector3 _offsetFromTool = Vector3.zero;
    Quaternion _initialRot = Quaternion.identity;

    void Awake()
    {
        if (gantry)
        {
            _target = gantry.Target;
            _yJointDummy = gantry.yJoint_2 ? gantry.yJoint_2.transform : null;
            if (!ToolSocket && gantry.toolSocket)
                ToolSocket = gantry.toolSocket;
        }

        ReadPoints();
        CacheJawOpenPose();
    }

    void Update()
    {
        // 들고 있는 동안: 잡힌 시점의 위치/회전만 유지
        if (_isCarryingDipstick && _carriedTransform != null && ToolSocket != null)
        {
            _carriedTransform.position = ToolSocket.position + _offsetFromTool;
            _carriedTransform.rotation = _initialRot;
        }
    }

    void ReadPoints()
    {
        if (Dipstick_Point) _dipPos = Dipstick_Point.position;
        if (Dipstick_Release_Point) _dipReleasePos = Dipstick_Release_Point.position;
        if (ER_Release_Point) _erReleasePos = ER_Release_Point.position;
    }

    float PosEps => dock ? dock.Pos_Eps : 0.01f;
    float Delay => dock ? dock.delayBeforeMove : 0.2f;

    void CacheJawOpenPose()
    {
        if (JawL)
        {
            jawL_open_pos = JawL.localPosition;
            jawL_open_rot = JawL.localRotation;
        }
        if (JawR)
        {
            jawR_open_pos = JawR.localPosition;
            jawR_open_rot = JawR.localRotation;
        }
        if (JawC)
        {
            jawC_open_pos = JawC.localPosition;
            jawC_open_rot = JawC.localRotation;
        }
    }

    public IEnumerator Do_LIBS_Process()
    {
        if (_isRunning) yield break;
        _isRunning = true;

        if (dock != null)
        {
            yield return SequenceController.WaitWhilePaused();
            yield return dock.Start_Dock_Angular_Gripper();
            yield return SequenceController.WaitWhilePaused();
            yield return new WaitForSeconds(Delay);
        }

        yield return SequenceController.WaitWhilePaused();
        yield return Dipstick_GripAndMove_TransformJaw();
        yield return SequenceController.WaitWhilePaused();
        yield return new WaitForSeconds(Delay);

        if (dock != null)
        {
            yield return SequenceController.WaitWhilePaused();
            yield return dock.Start_Dock_Heavy_Gripper();
            yield return SequenceController.WaitWhilePaused();
            yield return new WaitForSeconds(Delay);
        }

        _isRunning = false;
    }

    IEnumerator Dipstick_GripAndMove_TransformJaw()
    {
        ReadPoints();
        yield return SequenceController.WaitWhilePaused();
        yield return null;

        if (_target == null)
        {
            Debug.LogWarning("[LIBS] target 없음");
            yield break;
        }

        Transform carried = DipStick ? DipStick.transform : (Dipstick_Point ? Dipstick_Point : null);

        // 1. 픽업 XZ
        yield return SequenceController.WaitWhilePaused();
        yield return Move.MoveXZ(_target, () => gantry.SpeedXZ,
            new Vector3(_dipPos.x, _target.position.y, _dipPos.z), PosEps);
        yield return SequenceController.WaitWhilePaused();
        yield return new WaitForSeconds(Delay);

        // 2. Jaw Open
        yield return SequenceController.WaitWhilePaused();
        yield return OpenAG_Transform();
        yield return SequenceController.WaitWhilePaused();
        yield return new WaitForSeconds(Delay);

        // 3. Y Down
        yield return SequenceController.WaitWhilePaused();
        yield return Move.MoveY_Down(_target, _dipPos.y, () => gantry.SpeedYDown, PosEps);
        yield return SequenceController.WaitWhilePaused();
        yield return new WaitForSeconds(Delay);

        // 4. A.Grip 닫기
        yield return SequenceController.WaitWhilePaused();
        yield return CloseAG_Transform();
        yield return SequenceController.WaitWhilePaused();
        yield return new WaitForSeconds(Delay);

        // 5. 들고 다니기 시작
        if (carried != null && ToolSocket != null)
        {
            _carriedTransform = carried;
            _isCarryingDipstick = true;

            _offsetFromTool = _carriedTransform.position - ToolSocket.position;
            _initialRot = _carriedTransform.rotation;

            _carriedRb = carried.GetComponent<Rigidbody>();
            if (_carriedRb != null)
            {
                _rbWasKinematic = _carriedRb.isKinematic;
                _rbOldConstraints = _carriedRb.constraints;

                _carriedRb.isKinematic = true;
                _carriedRb.constraints = RigidbodyConstraints.None;
            }
        }
        yield return SequenceController.WaitWhilePaused();
        yield return new WaitForSeconds(Delay);

        // 6. Y Up
        yield return SequenceController.WaitWhilePaused();
        yield return Move.MoveY_Up(_target, gantry.yJoint_2, () => gantry.SpeedYUp, PosEps);
        yield return SequenceController.WaitWhilePaused();
        yield return new WaitForSeconds(Delay);

        // 7. 릴리즈 XZ
        yield return SequenceController.WaitWhilePaused();
        yield return Move.MoveXZ(_target, () => gantry.SpeedXZ,
            new Vector3(_dipReleasePos.x, _target.position.y, _dipReleasePos.z), PosEps);
        yield return SequenceController.WaitWhilePaused();
        yield return new WaitForSeconds(Delay);

        // 8. Y Down
        yield return SequenceController.WaitWhilePaused();
        yield return Move.MoveY_Down(_target, _dipReleasePos.y, () => gantry.SpeedYDown, PosEps);
        yield return SequenceController.WaitWhilePaused();
        yield return new WaitForSeconds(Delay);

        // 9. Y Up
        yield return SequenceController.WaitWhilePaused();
        yield return Move.MoveY_Up(_target, gantry.yJoint_2, () => gantry.SpeedYUp, PosEps);
        yield return SequenceController.WaitWhilePaused();
        yield return new WaitForSeconds(Delay);

        // 10. ER 쪽 XZ 이동
        if (ER_Release_Point != null)
        {
            yield return SequenceController.WaitWhilePaused();
            yield return Move.MoveXZ(_target, () => gantry.SpeedXZ,
                new Vector3(_erReleasePos.x, _target.position.y, _erReleasePos.z), PosEps);
            yield return SequenceController.WaitWhilePaused();
            yield return new WaitForSeconds(Delay);
        }

        // 11. Y Down
        if (ER_Release_Point != null)
        {
            yield return SequenceController.WaitWhilePaused();
            yield return Move.MoveY_Down(_target, _erReleasePos.y, () => gantry.SpeedYDown, PosEps);
            yield return SequenceController.WaitWhilePaused();
            yield return new WaitForSeconds(Delay);
        }

        // 12. A.Grip 열기
        yield return SequenceController.WaitWhilePaused();
        yield return OpenAG_Transform();
        yield return SequenceController.WaitWhilePaused();
        yield return new WaitForSeconds(Delay);

        // 13. 들고 다니기 종료
        _isCarryingDipstick = false;
        _carriedTransform = null;
        _offsetFromTool = Vector3.zero;

        if (_carriedRb != null)
        {
            _carriedRb.isKinematic = _rbWasKinematic;
            _carriedRb.constraints = _rbOldConstraints;
            _carriedRb = null;
        }
        yield return SequenceController.WaitWhilePaused();
        yield return new WaitForSeconds(Delay);

        // 14. Y Up 복귀
        yield return SequenceController.WaitWhilePaused();
        yield return Move.MoveY_Up(_target, gantry.yJoint_2, () => gantry.SpeedYUp, PosEps);
        yield return SequenceController.WaitWhilePaused();
        yield return new WaitForSeconds(Delay);
    }

    IEnumerator CloseAG_Transform()
    {
        float t = 0f;
        while (t < jawMoveTime)
        {
            yield return SequenceController.WaitWhilePaused();
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / jawMoveTime);

            if (JawL)
            {
                Vector3 targetPos = jawL_open_pos + new Vector3(-jawCloseOffset, 0f, 0f);
                JawL.localPosition = Vector3.Lerp(jawL_open_pos, targetPos, k);
            }
            if (JawR)
            {
                Vector3 targetPos = jawR_open_pos + new Vector3(0f, 0f, +jawCloseOffset);
                JawR.localPosition = Vector3.Lerp(jawR_open_pos, targetPos, k);
            }
            if (JawC)
            {
                Vector3 targetPos = jawC_open_pos + new Vector3(+jawCloseOffset, 0f, 0f);
                JawC.localPosition = Vector3.Lerp(jawC_open_pos, targetPos, k);
            }

            yield return null;
        }
    }

    IEnumerator OpenAG_Transform()
    {
        float t = 0f;
        Vector3 startL = JawL ? JawL.localPosition : Vector3.zero;
        Vector3 startR = JawR ? JawR.localPosition : Vector3.zero;
        Vector3 startC = JawC ? JawC.localPosition : Vector3.zero;

        while (t < jawMoveTime)
        {
            yield return SequenceController.WaitWhilePaused();
            t += Time.deltaTime;
            float k = Mathf.Clamp01(t / jawMoveTime);

            if (JawL)
                JawL.localPosition = Vector3.Lerp(startL, jawL_open_pos, k);
            if (JawR)
                JawR.localPosition = Vector3.Lerp(startR, jawR_open_pos, k);
            if (JawC)
                JawC.localPosition = Vector3.Lerp(startC, jawC_open_pos, k);

            yield return null;
        }
    }
}
