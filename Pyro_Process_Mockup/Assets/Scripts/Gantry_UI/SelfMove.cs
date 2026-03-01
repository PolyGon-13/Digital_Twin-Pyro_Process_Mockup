using UnityEngine;
using System.Collections;

/// <summary>
/// UI 버튼(길게 누르기)으로 WASD/QE와 동일한 수동 이동을 수행.
/// - GantryIKArticulation의 "통합 속도"를 사용해 축/방향별로 속도 자동 선택:
///   * X/Z 이동: gantry.SpeedXZ
///   * Y 이동  : gantry.SpeedYUp (위), gantry.SpeedYDown (아래)
/// - EventTrigger로 PointerDown/PointerUp만 연결하면 됨
/// </summary>
public class SelfMove : MonoBehaviour
{
    [Header("References")]
    public GantryIKArticulation gantry;  // 씬의 GantryIKArticulation 드래그

    [Header("Behavior")]
    [Tooltip("Gantry 통합 속도 사용 여부 (해제 시 1m/s 기준 + 배율만 적용)")]
    public bool useGantrySpeed = true;

    [Tooltip("추가 배율(=1이면 통합 속도 그대로)")]
    public float speedMultiplier = 1f;

    // 내부 상태
    private Vector3 holdDir = Vector3.zero;
    private int holdRequests = 0;   // 여러 버튼이 동시에 눌려도 안전하게
    private Coroutine moveRoutine;

    void OnDisable()
    {
        StopHold();
    }

    // ===== UI에서 호출할 함수들 =====
    // X+
    public void Start_X_Pos() => StartHold(new Vector3(+1, 0, 0));
    public void Stop_X_Pos() => StopHold();
    // X-
    public void Start_X_Neg() => StartHold(new Vector3(-1, 0, 0));
    public void Stop_X_Neg() => StopHold();
    // Z+
    public void Start_Z_Pos() => StartHold(new Vector3(0, 0, +1));
    public void Stop_Z_Pos() => StopHold();
    // Z-
    public void Start_Z_Neg() => StartHold(new Vector3(0, 0, -1));
    public void Stop_Z_Neg() => StopHold();
    // Y+ (상)
    public void Start_Y_Pos() => StartHold(new Vector3(0, +1, 0));
    public void Stop_Y_Pos() => StopHold();
    // Y- (하)
    public void Start_Y_Neg() => StartHold(new Vector3(0, -1, 0));
    public void Stop_Y_Neg() => StopHold();

    // ===== 내부 구현 =====
    private void StartHold(Vector3 dir)
    {
        holdDir = dir.normalized;
        holdRequests++;
        if (moveRoutine == null)
            moveRoutine = StartCoroutine(HoldMoveLoop());
    }

    private void StopHold()
    {
        holdRequests = Mathf.Max(0, holdRequests - 1);
        if (holdRequests == 0 && moveRoutine != null)
        {
            StopCoroutine(moveRoutine);
            moveRoutine = null;
            holdDir = Vector3.zero;
        }
    }

    private IEnumerator HoldMoveLoop()
    {
        while (holdRequests > 0)
        {
            if (gantry && gantry.Target)
            {
                float baseSpd = 1f;

                if (useGantrySpeed)
                {
                    // 우선순위: Y방향 성분이 가장 크면 Y속도, 아니면 XZ속도
                    float ax = Mathf.Abs(holdDir.x);
                    float ay = Mathf.Abs(holdDir.y);
                    float az = Mathf.Abs(holdDir.z);

                    if (ay > ax && ay > az) // Y가 우세
                        baseSpd = (holdDir.y >= 0f) ? gantry.SpeedYUp : gantry.SpeedYDown;
                    else
                        baseSpd = gantry.SpeedXZ;
                }

                float spd = Mathf.Max(0f, baseSpd) * Mathf.Max(0f, speedMultiplier);
                gantry.Target.position += holdDir * spd * Time.deltaTime;
            }
            yield return null;
        }
        moveRoutine = null;
    }
}
