using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class GripToggleButton : MonoBehaviour
{
    [Header("Refs")]
    public GantryIKArticulation gantry;       // H, A 둘 다 여기서 가져옴
    public Button gripButton;
    public TMP_Text gripLabel;

    [Header("(선택) 현재 어떤 그리퍼가 꽂혔는지 판정용")]
    // GripperToggleUI -> dock -> Is_Ag_Attached / Is_Hg_Attached 봐서 판단
    public GripperToggleUI gripperToggleUI;
    public DockAndAttach dockOverride; // 위가 없으면 이걸 직접 넣어도 됨

    [Header("H.Grip Close Amount")]
    public bool copyClosePosFromGripAndMoveTest = false;
    public GripAndMoveTest closePosSource;
    public float leftJawClosePos = 0.062f;
    public float rightJawClosePos = -0.062f;

    [Header("UI Style")]
    public string labelWhenOpen = "Grip";
    public string labelWhenGripped = "Gripped";
    public Color grippedColor = new Color(0.2f, 0.8f, 0.4f, 1f);

    bool _gripped = false;   // true면 "지금 조여있는 상태"
    bool _busy = false;
    Color _origColor;

    void Awake()
    {
        if (!gripButton) gripButton = GetComponent<Button>();
        if (gripButton && gripButton.image) _origColor = gripButton.image.color;

        // 처음 켜질 때 A.Grip이 꽂혀있으면 => 이미 gripped 상태로 시작
        if (IsAngularAttachedRightNow())
            _gripped = true;

        ApplyVisuals();
    }

    void OnEnable()
    {
        // 다시 활성화될 때도 A.Grip이면 gripped로 맞춰둠
        if (IsAngularAttachedRightNow())
            _gripped = true;

        ApplyVisuals();
    }

    public void OnClickToggle()
    {
        if (_busy) return;

        bool isA = IsAngularAttachedRightNow();
        if (!isA && !gantry) return;

        StartCoroutine(isA ? Toggle_A_Grip_Co() : Toggle_H_Grip_Co());
    }

    // =============== H.GRIP (그대로) ===============
    IEnumerator Toggle_H_Grip_Co()
    {
        _busy = true;
        SetInteractable(false);

        if (copyClosePosFromGripAndMoveTest && closePosSource)
        {
            leftJawClosePos = closePosSource.LeftJawClosePos;
            rightJawClosePos = closePosSource.RightJawClosePos;
        }

        var jawL = gantry.Heavy_Gripper_Jaw_Left;
        var jawR = gantry.Heavy_Gripper_Jaw_Right;

        if (!jawL || !jawR)
        {
            Debug.LogWarning("[GripToggleButton] Heavy gripper jaws not assigned in GantryIKArticulation.");
            SetInteractable(true);
            _busy = false;
            yield break;
        }

        float eps = gantry.JawPosEps;
        float v0 = gantry.HG_Start;
        float v1 = gantry.HG_End;

        if (!_gripped)
        {
            // 닫기
            yield return Move.Close_Heavy_Gripper_Jaw(jawL, jawR,
                leftJawClosePos, rightJawClosePos, eps, v0, v1);
            _gripped = true;
        }
        else
        {
            // 열기
            yield return Move.Open_Heavy_Gripper_Jaw(jawL, jawR, eps, v0, v1);
            _gripped = false;
        }

        ApplyVisuals();
        SetInteractable(true);
        _busy = false;
    }

    // =============== A.GRIP (gantry 값 사용) ===============
    IEnumerator Toggle_A_Grip_Co()
    {
        _busy = true;
        SetInteractable(false);

        // gantry에 다 있음
        var jawL = gantry.Angular_Gripper_Jaw_Left;
        var jawR = gantry.Angular_Gripper_Jaw_Right;
        var jawC = gantry.Angular_Gripper_Jaw_Center;

        if (!jawL || !jawR || !jawC)
        {
            Debug.LogWarning("[GripToggleButton] A.Grip attached but Angular jaws are not assigned in GantryIKArticulation.");
            SetInteractable(true);
            _busy = false;
            yield break;
        }

        float eps = gantry.JawPosEps;
        float v0 = gantry.AG_Start;
        float v1 = gantry.AG_End;
        // 닫을 때 목표값: 지금 GantryIKArticulation에서 수동으로 닫을 때 쓰는 값이 이거였음
        float closeTarget = gantry.AG_Jaw_Left_Limit.y; // 왼쪽 limit의 위쪽이 닫힘 쪽으로 세팅돼 있음 :contentReference[oaicite:1]{index=1}

        if (_gripped)
        {
            // 지금은 조여있는 상태 -> 버튼 누르면 "펴기"
            yield return Move.Open_Angular_Gripper_Jaw(
                jawL, jawR, jawC,
                eps,
                v0, v1
            );
            _gripped = false;
        }
        else
        {
            // 지금은 펴진 상태 -> 버튼 누르면 "다시 grip(닫기)"
            yield return Move.Close_Angular_Gripper_Jaw(
                jawL, jawR, jawC,
                closeTarget,
                eps,
                v0, v1
            );
            _gripped = true;
        }

        ApplyVisuals();
        SetInteractable(true);
        _busy = false;
    }

    // 지금 이 순간 A.Grip이 꽂혀있는지 판단
    bool IsAngularAttachedRightNow()
    {
        if (gripperToggleUI && gripperToggleUI.dock)
            return gripperToggleUI.dock.Is_Ag_Attached;

        if (dockOverride)
            return dockOverride.Is_Ag_Attached;

        // Dock을 못 받으면 H.Grip으로 본다
        return false;
    }

    void SetInteractable(bool on)
    {
        if (gripButton) gripButton.interactable = on;
    }

    void ApplyVisuals()
    {
        if (gripLabel)
            gripLabel.text = _gripped ? labelWhenGripped : labelWhenOpen;

        if (gripButton && gripButton.image)
            gripButton.image.color = _gripped ? grippedColor : _origColor;
    }
}
