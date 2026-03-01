using System.Collections;
using UnityEngine;

/// <summary>
/// 오른손 주먹(2초) 토글로 갠트리 Target을 손 위치에 동기화/해제.
/// [결합 시] 손과 '그리퍼 jaw 중점' 동기화(Heavy/A 별도) + 검지-엄지 핀치 ↔ Jaws 보간.
/// Jaws는 ArticulationBody.xDrive(target)로만 구동하며, 각 조의 유효 구간 내부로 강제 클램프.
/// 
/// H.Grip 규칙: "시작 위치 = 최대 벌림(고정), 한계값 = 닫히는 양(감소량)". 
///   - 플레이/결합 상태 변환 시 현재 xDrive.target을 openStart로 캡처
///   - closeTarget = openStart + signedDelta(인버전으로 부호 결정)
///   - 보간은 [closeTarget ↔ openStart] 사이에서만 수행(더 벌어지지 않음)
/// </summary>
public class GantryHandSync : MonoBehaviour
{
    [Header("Gantry")]
    public GantryIKArticulation gantry;
    public bool useSmoothApproach = true;
    public float approachPosEps = 0.002f;
    public float maxApproachTime = 3.0f;

    [Header("Hand Tracking (sources)")]
    public Transform rightHandProxy;
    public Transform palmTransform;
    public Transform thumbTip;
    public Transform indexTip;
    public Transform middleTip;
    public Transform ringTip;
    public Transform pinkyTip;

#if OVRPLUGIN_PRESENT || USING_XR_META
    [Header("OVR (optional)")]
    public OVRHand rightOVRHand;
#endif

    [Header("Gesture (fist) settings")]
    public float holdSeconds = 2.0f;
    public float fistPinchThreshold = 0.8f;
    public float fingertipCloseDist = 0.04f;

    [Header("Follow (sync)")]
    public bool syncPosition = true;
    public bool syncRotation = false;
    public Vector3 worldOffset = Vector3.zero;
    public float followLerp = 16f;
    public float maxFollowSpeed = 5f;
    public float handJitterSmoothing = 0.02f;

    [Header("Gripper coupling (optional)")]
    public DockAndAttach dock;
    [Tooltip("H.Grip 결합 시, 손과 붙일 jaw 중점(네가 만든 오브젝트)")]
    public Transform hGripCenter;
    [Tooltip("A.Grip 결합 시, 손과 붙일 jaw 중점(네가 만든 오브젝트)")]
    public Transform aGripCenter;
    public bool matchJawToPinch = true;

    [Header("Pinch→Jaw mapping")]
    [Tooltip("검지-엄지 '맞닿음' 거리(m) = Jaws 완전 닫힘(t=0)")]
    public float pinchClosedDist = 0.010f;
    [Tooltip("검지-엄지 '최대 벌림' 거리(m) = Jaws 최대 열림(t=1)")]
    public float pinchOpenDist = 0.120f;
    [Tooltip("jaw 타깃 보간 스무딩(초당 보간 강도)")]
    public float jawLerp = 20f;

    [Header("Jaw direction (invert open/close mapping)")]
    // 요구 반영: 기본값 — Heavy Left만 반전, Right는 정상 / Angular 3개는 정상
    public bool invertHeavyLeft = true;
    public bool invertHeavyRight = false;
    public bool invertAngularLeft = false;
    public bool invertAngularRight = false;
    public bool invertAngularCenter = false;

    [Header("H.Grip close amount (from 'open start')")]
    [Tooltip("Heavy Left가 시작열림에서 '최대로 닫힐 수 있는' 이동량(절댓값, m 또는 drive 단위)")]
    public float heavyLeftCloseMagnitude = 0.062f;   // 예: GripAndMoveTest 기준
    [Tooltip("Heavy Right가 시작열림에서 '최대로 닫힐 수 있는' 이동량(절댓값, m 또는 drive 단위)")]
    public float heavyRightCloseMagnitude = 0.062f;  // 예: GripAndMoveTest 기준

    [Header("Angular closed targets (optional overrides)")]
    [Tooltip("Angular 닫힘 타깃(필요 시 지정, 미지정이면 limits/현재값으로 추정)")]
    public float angularLeftClosedTarget = 0f;
    public float angularRightClosedTarget = 0f;
    public float angularCenterClosedTarget = 0f;
    public bool useAngularExplicitClosed = false;

    [Header("Debug")]
    public bool logState = false;
    public UnityEngine.UI.Image syncLamp;
    public Color onColor = new Color(0.2f, 1f, 0.6f, 1f);
    public Color offColor = new Color(0.6f, 0.6f, 1f, 1f);

    // ---- internal state ----
    bool _synced;
    float _fistTimer;
    Vector3 _smoothedHandPos;
    Quaternion _smoothedHandRot;
    Coroutine _approachCo;

    float _tJaw; // 0(닫힘)~1(열림) 저진동 상태 보간

    // 각 조별 매핑 데이터
    struct JawMap
    {
        public ArticulationBody jaw;

        // 공통(실제 최종 클램프용)
        public float driveLo, driveHi;  // xDrive limits (정렬된 lo<=hi)

        // Heavy 전용: 시작열림/닫힘량 매핑
        public bool isHeavy;
        public float openStart;     // 플레이/결합 시 캡처되는 '최대 열림' 기준
        public float closeTarget;   // openStart + signedDelta (닫힘 최대점)
        public bool invert;         // delta부호/보간 t 뒤집기에 사용

        // Angular 전용: 기존 방식(닫힘/열림 명시)
        public float closeTargetExplicit; // t=0
        public float openTargetExplicit;  // t=1
        public bool useExplicit;          // Angular에만 true
    }

    JawMap _hgL, _hgR, _agL, _agR, _agC;

    // 결합 상태 변화 감지용 캐시
    bool _lastHg, _lastAg;

    void Reset()
    {
        holdSeconds = 2.0f;
        fistPinchThreshold = 0.8f;
        fingertipCloseDist = 0.04f;
        followLerp = 16f;
        maxFollowSpeed = 5f;
        handJitterSmoothing = 0.02f;
        approachPosEps = 0.002f;
        maxApproachTime = 3f;

        pinchClosedDist = 0.010f;
        pinchOpenDist = 0.120f;
        jawLerp = 20f;

        invertHeavyLeft = true;
        invertHeavyRight = false;
        invertAngularLeft = false;
        invertAngularRight = false;
        invertAngularCenter = false;

        heavyLeftCloseMagnitude = 0.062f;
        heavyRightCloseMagnitude = 0.062f;

        useAngularExplicitClosed = false;
    }

    void Awake()
    {
        BuildJawMaps(true);   // ▶ 시작 시 openStart 캡처
        InitJawTFromCurrent();
    }

    void OnEnable() { UpdateLamp(); }

    void Update()
    {
        if (!gantry || !gantry.Target) return;
        if (!HasValidHandSource()) return;

        // 0) 결합 상태 변화 감지 → jawMap 재계산(heavy는 openStart 재캡처) + t 재초기화
        bool hg = dock && dock.Is_Hg_Attached;
        bool ag = dock && dock.Is_Ag_Attached;
        if (hg != _lastHg || ag != _lastAg)
        {
            _lastHg = hg; _lastAg = ag;
            BuildJawMaps(true);
            InitJawTFromCurrent();
        }

        // 1) 주먹 유지 토글
        bool fist = IsRightFist();
        if (fist)
        {
            _fistTimer += Time.unscaledDeltaTime;
            if (_fistTimer >= holdSeconds)
            {
                _fistTimer = 0f;
                ToggleSync();
            }
        }
        else _fistTimer = 0f;

        // 2) 동기화 추적
        if (_synced)
        {
            Vector3 handPos; Quaternion handRot;
            GetRightHandPose(out handPos, out handRot);
            ApplyLowPass(ref _smoothedHandPos, handPos, handJitterSmoothing);
            _smoothedHandRot = Quaternion.Slerp(_smoothedHandRot, handRot, Time.deltaTime * followLerp);

            Transform center = GetAttachedCenter(hg, ag);

            Vector3 goal;
            if ((hg || ag) && center && gantry.toolSocket)
            {
                // toolSocket을 center에 맞추도록 보정
                Vector3 centerDelta = gantry.toolSocket.position - center.position;
                goal = _smoothedHandPos + worldOffset + centerDelta;
            }
            else goal = _smoothedHandPos + worldOffset;

            Vector3 next = goal;
            if (maxFollowSpeed > 0f)
            {
                Vector3 from = gantry.Target.position;
                float maxStep = maxFollowSpeed * Time.deltaTime;
                next = Vector3.MoveTowards(from, goal, maxStep);
            }
            gantry.Target.position = Vector3.Lerp(gantry.Target.position, next, Time.deltaTime * followLerp);
            if (syncRotation)
                gantry.Target.rotation = Quaternion.Slerp(gantry.Target.rotation, _smoothedHandRot, Time.deltaTime * followLerp);

            // 3) 핀치 ↔ Jaws
            if ((hg || ag) && matchJawToPinch)
                UpdateJawsFromPinch(hg, ag);
        }
    }

    void ToggleSync()
    {
        if (!_synced)
        {
            if (_approachCo != null) StopCoroutine(_approachCo);
            _approachCo = StartCoroutine(Co_ApproachThenSyncOn());
        }
        else
        {
            _synced = false;
            if (logState) Debug.Log("[GantryHandSync] Sync OFF");
            UpdateLamp();
        }
    }

    IEnumerator Co_ApproachThenSyncOn()
    {
        Vector3 handPos; Quaternion handRot;
        GetRightHandPose(out handPos, out handRot);
        _smoothedHandPos = handPos; _smoothedHandRot = handRot;

        bool hg = dock && dock.Is_Hg_Attached;
        bool ag = dock && dock.Is_Ag_Attached;
        Transform center = GetAttachedCenter(hg, ag);

        Vector3 goal = handPos + worldOffset;
        if ((hg || ag) && center && gantry.toolSocket)
        {
            Vector3 centerDelta = gantry.toolSocket.position - center.position;
            goal += centerDelta;
        }

        if (useSmoothApproach)
        {
            bool approached = false;
            float t0 = Time.time;

            bool kickedMove = false;
            try
            {
                var ctx = new Move.Ctx
                {
                    Self = gantry.Target,
                    // ▼ 변경 ①: 통합 속도 사용 (Y는 Up/Down, 그 외 XZ)
                    MoveSpeed = () =>
                    {
                        if (gantry == null || gantry.Target == null) return 0.5f;
                        float dy = goal.y - gantry.Target.position.y;
                        return (Mathf.Abs(dy) > 1e-4f)
                            ? (dy > 0 ? gantry.SpeedYUp : gantry.SpeedYDown)
                            : gantry.SpeedXZ;
                    },
                    PosEps = () => approachPosEps,
                    YJoint = () => gantry.yJoint_2
                };
                StartCoroutine(Move.MoveTo_Target(ctx, goal));
                kickedMove = true;
            }
            catch { }

            while (Time.time - t0 < maxApproachTime)
            {
                if (!kickedMove)
                {
                    var from = gantry.Target.position;
                    // ▼ 변경 ②: 통합 속도 사용 (Y는 Up/Down, 그 외 XZ)
                    float dy = goal.y - from.y;
                    float step = (Mathf.Abs(dy) > 1e-4f)
                        ? ((dy > 0 ? gantry.SpeedYUp : gantry.SpeedYDown) * Time.deltaTime)
                        : (gantry.SpeedXZ * Time.deltaTime);
                    gantry.Target.position = Vector3.MoveTowards(from, goal, step);
                }

                float dist = Vector3.Distance(gantry.Target.position, goal);
                if (dist <= Mathf.Max(approachPosEps, 0.001f)) { approached = true; break; }
                yield return null;
            }
            if (!approached && logState) Debug.Log("[GantryHandSync] Approach timed out; syncing anyway.");
        }
        else
        {
            gantry.Target.position = goal;
            if (syncRotation) gantry.Target.rotation = handRot;
        }

        _synced = true;
        if (logState) Debug.Log("[GantryHandSync] Sync ON");
        UpdateLamp();
        _approachCo = null;

        // 동기화 직후에도 현재 조 상태로 t를 초기화
        InitJawTFromCurrent();
    }

    // ---------------- Pinch → Jaw control ----------------

    void BuildJawMaps(bool recaptureHeavyOpenStart)
    {
        // 공통: 드라이브 리밋 확보
        (float lo, float hi) GetLimits(ArticulationBody j)
        {
            var d = j.xDrive; float a = Mathf.Min(d.lowerLimit, d.upperLimit), b = Mathf.Max(d.lowerLimit, d.upperLimit);
            return (a, b);
        }

        // Heavy: 시작열림 캡처 + 닫힘량 적용
        _hgL = new JawMap { jaw = gantry ? gantry.Heavy_Gripper_Jaw_Left : null, invert = invertHeavyLeft, isHeavy = true };
        _hgR = new JawMap { jaw = gantry ? gantry.Heavy_Gripper_Jaw_Right : null, invert = invertHeavyRight, isHeavy = true };

        if (_hgL.jaw)
        {
            var lim = GetLimits(_hgL.jaw); _hgL.driveLo = lim.lo; _hgL.driveHi = lim.hi;
            var d = _hgL.jaw.xDrive;
            if (recaptureHeavyOpenStart) _hgL.openStart = d.target; // ▶ 시작열림 갱신
            float signedDelta = (_hgL.invert ? +heavyLeftCloseMagnitude : -heavyLeftCloseMagnitude);
            _hgL.closeTarget = Mathf.Clamp(_hgL.openStart + signedDelta, _hgL.driveLo, _hgL.driveHi);
        }
        if (_hgR.jaw)
        {
            var lim = GetLimits(_hgR.jaw); _hgR.driveLo = lim.lo; _hgR.driveHi = lim.hi;
            var d = _hgR.jaw.xDrive;
            if (recaptureHeavyOpenStart) _hgR.openStart = d.target; // ▶ 시작열림 갱신
            float signedDelta = (_hgR.invert ? +heavyRightCloseMagnitude : -heavyRightCloseMagnitude);
            _hgR.closeTarget = Mathf.Clamp(_hgR.openStart + signedDelta, _hgR.driveLo, _hgR.driveHi);
        }

        // Angular: 기존 방식(닫힘 명시 or 리밋/현재값으로 추정)
        _agL = BuildAngularMap(gantry ? gantry.Angular_Gripper_Jaw_Left : null, invertAngularLeft, angularLeftClosedTarget, useAngularExplicitClosed);
        _agR = BuildAngularMap(gantry ? gantry.Angular_Gripper_Jaw_Right : null, invertAngularRight, angularRightClosedTarget, useAngularExplicitClosed);
        _agC = BuildAngularMap(gantry ? gantry.Angular_Gripper_Jaw_Center : null, invertAngularCenter, angularCenterClosedTarget, useAngularExplicitClosed);
    }

    JawMap BuildAngularMap(ArticulationBody jaw, bool invert, float explicitClosed, bool useExplicit)
    {
        var map = new JawMap { jaw = jaw, invert = invert, isHeavy = false, useExplicit = useExplicit };
        if (!jaw) return map;
        var d = jaw.xDrive;
        map.driveLo = Mathf.Min(d.lowerLimit, d.upperLimit);
        map.driveHi = Mathf.Max(d.lowerLimit, d.upperLimit);

        if (useExplicit)
        {
            float close = Mathf.Clamp(explicitClosed, map.driveLo, map.driveHi);
            // 열림은 닫힘에서 더 먼 리밋
            float distLo = Mathf.Abs(close - map.driveLo);
            float distHi = Mathf.Abs(map.driveHi - close);
            float open = (distHi >= distLo) ? map.driveHi : map.driveLo;
            map.closeTargetExplicit = close;
            map.openTargetExplicit = open;
        }
        else
        {
            float cur = d.target;
            float distLo = Mathf.Abs(cur - map.driveLo);
            float distHi = Mathf.Abs(map.driveHi - cur);
            float close = (distLo <= distHi) ? map.driveLo : map.driveHi;
            float open = (close == map.driveLo) ? map.driveHi : map.driveLo;
            map.closeTargetExplicit = close;
            map.openTargetExplicit = open;
        }
        return map;
    }

    void UpdateJawsFromPinch(bool hgAttached, bool agAttached)
    {
        if (!thumbTip || !indexTip) return;

        float pinch = Vector3.Distance(indexTip.position, thumbTip.position);
        // 0 = 닫힘(맞닿음), 1 = 열림(최대 벌림)
        float t = Mathf.InverseLerp(pinchClosedDist, pinchOpenDist, pinch);
        _tJaw = Mathf.Lerp(_tJaw, t, 1f - Mathf.Exp(-jawLerp * Time.deltaTime));

        if (hgAttached)
        {
            SetHeavyJaw(_hgL, _tJaw);
            SetHeavyJaw(_hgR, _tJaw);
        }
        if (agAttached)
        {
            SetAngularJaw(_agL, _tJaw);
            SetAngularJaw(_agR, _tJaw);
            SetAngularJaw(_agC, _tJaw);
        }
    }

    // Heavy: [closeTarget ↔ openStart] 선형보간(더 벌어지지 않음)
    void SetHeavyJaw(JawMap m, float t01)
    {
        if (m.jaw == null) return;

        float t = Mathf.Clamp01(m.invert ? (1f - t01) : t01);
        float wanted = Mathf.Lerp(m.closeTarget, m.openStart, t); // t=0(닫힘) → close, t=1(열림) → openStart

        // 유효 구간 및 드라이브 리밋으로 클램프
        float loSeg = Mathf.Min(m.closeTarget, m.openStart);
        float hiSeg = Mathf.Max(m.closeTarget, m.openStart);
        wanted = Mathf.Clamp(wanted, loSeg, hiSeg);
        wanted = Mathf.Clamp(wanted, m.driveLo, m.driveHi);

        var d = m.jaw.xDrive;
        d.target = wanted;
        m.jaw.xDrive = d;
    }

    // Angular: [closeExplicit ↔ openExplicit] 보간(기존 방식)
    void SetAngularJaw(JawMap m, float t01)
    {
        if (m.jaw == null) return;
        float t = Mathf.Clamp01(m.invert ? (1f - t01) : t01);
        float wanted = Mathf.Lerp(m.closeTargetExplicit, m.openTargetExplicit, t);
        wanted = Mathf.Clamp(wanted, m.driveLo, m.driveHi);
        var d = m.jaw.xDrive;
        d.target = wanted;
        m.jaw.xDrive = d;
    }

    // 현재 조 위치(xDrive.target)를 기준으로 t를 역보간해 초기화(초기 튐 방지)
    void InitJawTFromCurrent()
    {
        float sum = 0f; int cnt = 0;

        AccumHeavyT(_hgL, ref sum, ref cnt);
        AccumHeavyT(_hgR, ref sum, ref cnt);
        AccumAngularT(_agL, ref sum, ref cnt);
        AccumAngularT(_agR, ref sum, ref cnt);
        AccumAngularT(_agC, ref sum, ref cnt);

        if (cnt > 0) _tJaw = Mathf.Clamp01(sum / cnt);
    }

    void AccumHeavyT(JawMap m, ref float sum, ref int cnt)
    {
        if (m.jaw == null) return;
        var d = m.jaw.xDrive;
        float cur = Mathf.Clamp(d.target, Mathf.Min(m.closeTarget, m.openStart), Mathf.Max(m.closeTarget, m.openStart));
        float t = Mathf.InverseLerp(m.closeTarget, m.openStart, cur); // t=0 close, t=1 open
        if (m.invert) t = 1f - t;
        sum += t; cnt++;
    }

    void AccumAngularT(JawMap m, ref float sum, ref int cnt)
    {
        if (m.jaw == null) return;
        var d = m.jaw.xDrive;
        float cur = Mathf.Clamp(d.target, Mathf.Min(m.closeTargetExplicit, m.openTargetExplicit), Mathf.Max(m.closeTargetExplicit, m.openTargetExplicit));
        float t = Mathf.InverseLerp(m.closeTargetExplicit, m.openTargetExplicit, cur);
        if (m.invert) t = 1f - t;
        sum += t; cnt++;
    }

    // ---------------- helpers ----------------
    Transform GetAttachedCenter(bool hg, bool ag)
    {
        if (hg && hGripCenter) return hGripCenter;
        if (ag && aGripCenter) return aGripCenter;
        return null;
    }

    bool HasValidHandSource()
    {
#if OVRPLUGIN_PRESENT || USING_XR_META
        if (rightOVRHand != null && rightOVRHand.IsTracked) return true;
#endif
        return rightHandProxy != null && rightHandProxy.gameObject.activeInHierarchy;
    }

    void GetRightHandPose(out Vector3 pos, out Quaternion rot)
    {
#if OVRPLUGIN_PRESENT || USING_XR_META
        if (rightOVRHand != null && rightOVRHand.IsTracked)
        {
            if (rightHandProxy != null) { pos = rightHandProxy.position; rot = rightHandProxy.rotation; return; }
            pos = rightOVRHand.transform.position; rot = rightOVRHand.transform.rotation; return;
        }
#endif
        if (rightHandProxy != null) { pos = rightHandProxy.position; rot = rightHandProxy.rotation; return; }
        pos = Vector3.zero; rot = Quaternion.identity;
    }

    bool IsRightFist()
    {
#if OVRPLUGIN_PRESENT || USING_XR_META
        if (rightOVRHand != null && rightOVRHand.IsTracked)
        {
            float idx = rightOVRHand.GetFingerPinchStrength(OVRHand.HandFinger.Index);
            float mid = rightOVRHand.GetFingerPinchStrength(OVRHand.HandFinger.Middle);
            float rng = rightOVRHand.GetFingerPinchStrength(OVRHand.HandFinger.Ring);
            float pky = rightOVRHand.GetFingerPinchStrength(OVRHand.HandFinger.Pinky);
            return (idx >= fistPinchThreshold && mid >= fistPinchThreshold &&
                    rng >= fistPinchThreshold && pky >= fistPinchThreshold);
        }
#endif
        if (!palmTransform || !(thumbTip && indexTip && middleTip && ringTip && pinkyTip)) return false;

        float d =
            (Vector3.Distance(palmTransform.position, thumbTip.position) +
             Vector3.Distance(palmTransform.position, indexTip.position) +
             Vector3.Distance(palmTransform.position, middleTip.position) +
             Vector3.Distance(palmTransform.position, ringTip.position) +
             Vector3.Distance(palmTransform.position, pinkyTip.position)) / 5f;

        return d <= fingertipCloseDist;
    }

    static void ApplyLowPass(ref Vector3 smoothed, Vector3 current, float tau)
    {
        if (tau <= 0f) { smoothed = current; return; }
        float a = Time.deltaTime / (tau + Time.deltaTime);
        smoothed = Vector3.Lerp(smoothed, current, a);
    }

    void UpdateLamp()
    {
        if (syncLamp) syncLamp.color = _synced ? onColor : offColor;
    }
}
