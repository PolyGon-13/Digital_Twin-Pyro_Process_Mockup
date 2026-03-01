using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;
using System.Text;
using System.Globalization;

public class PositionMovePanel : MonoBehaviour
{
    [Header("Gantry")]
    public GantryIKArticulation gantry;

    [Header("UI - Display (Target Position to move)")]
    public TMP_Text xDisplay;
    public TMP_Text yDisplay;
    public TMP_Text zDisplay;

    [Header("UI - Buttons")]
    public Button inputToggleButton;
    public Image inputToggleLamp;
    public Button moveButton;

    [Header("UI - Keypad Panel")]
    public GameObject keypadPanel;
    public TMP_Text editingAxisLabel;
    public Button axisXButton;
    public Button axisYButton;
    public Button axisZButton;

    [Header("Formatting")]
    [Range(0, 6)] public int decimalPlaces = 3;
    public string formatUnit = "";

    [Header("Settings")]
    public float moveEps = 0.005f;
    public bool closeKeypadOnMove = true;

    [Header("Presets - Fixed (text input keeps full precision)")]
    public string orgX = "0";
    public string orgY = "0";
    public string orgZ = "0";

    // ===== 내부 상태 =====
    enum Axis { X, Y, Z }

    // 축별 편집 상태
    class EditState
    {
        public int sign = +1;
        public int intPart = 0;
        public bool intSet = false;
        public StringBuilder frac = new StringBuilder(16);

        public void Clear()
        {
            sign = +1;
            intPart = 0;
            intSet = false;
            frac.Clear();
        }

        public double ToDouble()
        {
            double v = intPart;
            double w = 0.1;
            for (int i = 0; i < frac.Length; i++)
            {
                int d = frac[i] - '0';
                v += d * w;
                w *= 0.1;
            }
            return sign * v;
        }

        public void FromDouble(double val)
        {
            Clear();
            if (val < 0) { sign = -1; val = -val; }
            intPart = Mathf.FloorToInt((float)val);
            intSet = intPart != 0;
            double fracVal = val - intPart;
            if (fracVal > 0)
            {
                string s = fracVal.ToString("F10", CultureInfo.InvariantCulture);
                int dot = s.IndexOf('.');
                if (dot >= 0 && dot + 1 < s.Length)
                {
                    for (int i = dot + 1; i < s.Length; i++)
                    {
                        char ch = s[i];
                        if (ch == '0') { frac.Append('0'); continue; }
                        if (ch >= '0' && ch <= '9') frac.Append(ch);
                    }
                    TrimTrailingZeros();
                }
            }
        }

        public void TrimTrailingZeros()
        {
            int i = frac.Length - 1;
            while (i >= 0 && frac[i] == '0') i--;
            if (i < frac.Length - 1) frac.Length = i + 1;
        }
    }

    Axis editingAxis = Axis.X;
    EditState xState = new EditState();
    EditState yState = new EditState();
    EditState zState = new EditState();

    Coroutine movingCo;

    // 버튼 색상 관리
    Color _moveBtnOrigColor;
    bool _moveBtnColorCached = false;

    void Awake()
    {
        UpdateDisplays();
        SetInputMode(false);
        HookAxisButtons();
    }

    void HookAxisButtons()
    {
        if (axisXButton) axisXButton.onClick.AddListener(() => SetEditingAxis(Axis.X));
        if (axisYButton) axisYButton.onClick.AddListener(() => SetEditingAxis(Axis.Y));
        if (axisZButton) axisZButton.onClick.AddListener(() => SetEditingAxis(Axis.Z));
    }

    void SetEditingAxis(Axis a)
    {
        editingAxis = a;
        if (editingAxisLabel)
            editingAxisLabel.text = $"Editing: {editingAxis}";
    }

    // ===== 입력 토글 =====
    public void OnToggleInput() => SetInputMode(!IsInputMode());
    bool IsInputMode() => keypadPanel && keypadPanel.activeSelf;

    void SetInputMode(bool on)
    {
        if (keypadPanel) keypadPanel.SetActive(on);
        if (inputToggleLamp)
        {
            var color = on ? new Color(0.2f, 0.8f, 0.2f, 1f)
                           : new Color(0.6f, 0.6f, 0.6f, 1f);
            inputToggleLamp.color = color;
        }
        if (on && editingAxisLabel)
            editingAxisLabel.text = $"Editing: {editingAxis}";
    }

    // ===== 키패드 입력 =====
    public void OnKeyDigit(string d) { AppendDigit(int.Parse(d)); }
    public void OnKeyDot() { }
    public void OnKeyBackspace() { Backspace(); }
    public void OnKeySign() { ToggleSign(); }
    public void OnKeyClear() { GetState().Clear(); UpdateDisplays(); }
    public void OnKeyAC() { xState.Clear(); yState.Clear(); zState.Clear(); UpdateDisplays(); }
    public void OnKeyTab() { Axis next = (Axis)(((int)editingAxis + 1) % 3); SetEditingAxis(next); }

    void AppendDigit(int digit)
    {
        var st = GetState();
        if (!st.intSet)
        {
            st.intPart = digit;
            st.intSet = true;
        }
        else
        {
            st.frac.Append((char)('0' + digit));
        }
        UpdateDisplays();
    }

    void Backspace()
    {
        var st = GetState();
        if (st.frac.Length > 0)
        {
            st.frac.Length -= 1;
        }
        else if (st.intSet)
        {
            st.intSet = false;
            st.intPart = 0;
        }
        UpdateDisplays();
    }

    void ToggleSign()
    {
        var st = GetState();
        st.sign = -st.sign;
        UpdateDisplays();
    }

    EditState GetState()
    {
        switch (editingAxis)
        {
            case Axis.X: return xState;
            case Axis.Y: return yState;
            default: return zState;
        }
    }

    // ===== 표시 갱신 =====
    void UpdateDisplays()
    {
        if (xDisplay) xDisplay.text = "X: " + FormatNum(xState.ToDouble());
        if (yDisplay) yDisplay.text = "Y: " + FormatNum(yState.ToDouble());
        if (zDisplay) zDisplay.text = "Z: " + FormatNum(zState.ToDouble());
    }
    string FormatNum(double v) => v.ToString("F" + decimalPlaces, CultureInfo.InvariantCulture) + formatUnit;

    // ===== 프리셋 =====
    public void PresetFromTransform(Transform t)
    {
        if (!t) return;
        Vector3 p = t.position;
        xState.FromDouble(p.x);
        yState.FromDouble(p.y);
        zState.FromDouble(p.z);
        UpdateDisplays();
    }

    public void PresetFromToolSocket()
    {
        if (!gantry || !gantry.toolSocket) return;
        PresetFromTransform(gantry.toolSocket);
    }

    public void OnPresetORG()
    {
        if (!double.TryParse(orgX, NumberStyles.Float, CultureInfo.InvariantCulture, out var x)) x = 0;
        if (!double.TryParse(orgY, NumberStyles.Float, CultureInfo.InvariantCulture, out var y)) y = 0;
        if (!double.TryParse(orgZ, NumberStyles.Float, CultureInfo.InvariantCulture, out var z)) z = 0;

        xState.FromDouble(x);
        yState.FromDouble(y);
        zState.FromDouble(z);

        UpdateDisplays();
    }

    // ===== Move (Y-Up → XZ → Y-Down) =====
    public void OnMove()
    {
        if (!gantry || !gantry.Target) return;

        Vector3 goal = new Vector3(
            (float)xState.ToDouble(),
            (float)yState.ToDouble(),
            (float)zState.ToDouble()
        );

        if (movingCo != null) StopCoroutine(movingCo);
        movingCo = StartCoroutine(MoveSequence(goal));

        if (closeKeypadOnMove) SetInputMode(false);
    }

    IEnumerator MoveSequence(Vector3 goal)
    {
        // 버튼 색/상태 변경
        SetMoveButtonBusy(true);

        // 공통 Ctx 구성
        var ctx = new Move.Ctx
        {
            Self = gantry.Target,
            PosEps = () => moveEps,
            YJoint = () => gantry.yJoint_2
        };

        // ① Y 최상단까지 올림
        yield return Move.MoveY_Up(ctx, () => gantry.SpeedYUp); // Y 상한 계산 후 상승 :contentReference[oaicite:4]{index=4}

        // ② XZ 평면 이동 (현재 Y 유지)
        Vector3 topPlaneGoal = new Vector3(goal.x, gantry.Target.position.y, goal.z);
        yield return Move.MoveXZ(ctx, topPlaneGoal); // XZ 이동 유틸 사용 :contentReference[oaicite:5]{index=5}

        // ③ 목표 Y까지 하강
        yield return Move.MoveY_Down(ctx, goal.y, () => gantry.SpeedYDown); // 하강 유틸 사용 :contentReference[oaicite:6]{index=6}

        SetMoveButtonBusy(false);
        movingCo = null;
    }

    void SetMoveButtonBusy(bool busy)
    {
        if (!moveButton) return;

        if (!_moveBtnColorCached)
        {
            _moveBtnColorCached = true;
            // Button.image는 2022/6000에서도 존재
            _moveBtnOrigColor = moveButton.image ? moveButton.image.color : Color.white;
        }

        moveButton.interactable = !busy;

        if (moveButton.image)
        {
            // 이동 중: 주황빛, 완료 후: 원래 색 복구
            moveButton.image.color = busy ? new Color(1f, 0.75f, 0.2f, 1f) : _moveBtnOrigColor;
        }
    }
}
