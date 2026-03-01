using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// POS_Catalog
/// - 별도의 패널에 부착
/// - 각 버튼을 누르면, 연결된 Transform의 현재 world position(x,y,z)을
///   PositionMovePanel에 자동 입력(표시는 F{decimalPlaces}, 내부값은 풀프리시전 유지).
/// - 이동은 PositionMovePanel의 Move 버튼을 사용 (옵션으로 자동 이동 가능).
/// </summary>
public class POS_Catalog : MonoBehaviour
{
    [Header("Target Position Panel (필수)")]
    [Tooltip("XYZ 표시/키패드/이동 로직을 가진 PositionMovePanel을 연결하세요.")]
    public PositionMovePanel positionPanel;

    [Header("동작 옵션")]
    [Tooltip("버튼을 누르면 좌표를 입력만 하고, 자동 이동은 하지 않습니다. 체크하면 입력 직후 이동까지 실행합니다.")]
    public bool autoMoveAfterSelect = false;

    [System.Serializable]
    public class Entry
    {
        [Tooltip("인스펙터/디버깅용 라벨 (선택).")]
        public string label;

        [Tooltip("이 좌표를 선택하는 UI 버튼.")]
        public Button button;

        [Tooltip("버튼을 누르는 시점에 이 Transform의 world position을 읽어옵니다.")]
        public Transform target;
    }

    [Header("포지션 엔트리 목록 (원하는 만큼 추가하세요)")]
    [Tooltip("UB.L, USC.L, UB.U, USC.U, UB.D, USC.D, ER_UB, ER_USC, ER_UB.D, ER_USC.D, " +
             "R.UB.L, R.USC.L, R.UB.U, R.USC.U, RDS.CC, RDS.CB, RDS.SR, " +
             "A.L, A.UB.U, A.USC.U, A.UB.A, A.USC.A, Dipstick 등 23개 버튼을 등록하고, 필요시 더 추가 가능합니다.")]
    public List<Entry> entries = new List<Entry>();

    // --- 편의: 초기 세팅 보조 (선택 사용) ------------------------------
    // 프로젝트에서 고정된 이름을 쓰면, 인스펙터에서 label만 보고도 매핑 상태를 쉽게 확인할 수 있습니다.
    // 예: label = "UB.L", "USC.L", ... "Dipstick"
    // -------------------------------------------------------------------

    private void Awake()
    {
        if (!positionPanel)
        {
            Debug.LogWarning("[POS_Catalog] PositionMovePanel이 연결되지 않았습니다. 좌표 입력이 동작하지 않습니다.", this);
        }

        // 각 버튼에 클릭 리스너 연결
        for (int i = 0; i < entries.Count; i++)
        {
            int captured = i; // 클로저 캡처 주의
            var e = entries[captured];

            if (e == null) continue;

            if (!e.button)
            {
                Debug.LogWarning($"[POS_Catalog] Entry[{captured}] '{e.label}'에 버튼이 비어있습니다.", this);
                continue;
            }

            e.button.onClick.AddListener(() => OnEntryClicked(captured));
        }
    }

    /// <summary>
    /// 인덱스로 엔트리를 선택(버튼 클릭과 동일).
    /// UI 이벤트에서 직접 연결하고 싶을 때도 사용할 수 있습니다.
    /// </summary>
    public void OnEntryClicked(int index)
    {
        if (!IsValidIndex(index))
        {
            Debug.LogWarning($"[POS_Catalog] 잘못된 index: {index}", this);
            return;
        }

        var e = entries[index];
        ApplyTransformPosition(e);
    }

    /// <summary>
    /// 임의의 Transform을 전달받아 해당 시점의 위치를 PositionMovePanel에 입력.
    /// (UnityEvent에서 직접 Transform을 끌어다 연결해 쓸 수도 있습니다.)
    /// </summary>
    public void ApplyFromTransform(Transform t)
    {
        if (!t)
        {
            Debug.LogWarning("[POS_Catalog] 전달된 Transform이 null 입니다.", this);
            return;
        }

        if (!positionPanel)
        {
            Debug.LogWarning("[POS_Catalog] PositionMovePanel이 연결되지 않아 좌표를 입력할 수 없습니다.", this);
            return;
        }

        positionPanel.PresetFromTransform(t);

        if (autoMoveAfterSelect)
        {
            positionPanel.OnMove();
        }
    }

    // === 내부구현 ===
    private bool IsValidIndex(int idx) => (idx >= 0 && idx < entries.Count && entries[idx] != null);

    private void ApplyTransformPosition(Entry e)
    {
        if (!positionPanel)
        {
            Debug.LogWarning("[POS_Catalog] PositionMovePanel이 연결되지 않아 좌표를 입력할 수 없습니다.", this);
            return;
        }

        if (!e.target)
        {
            Debug.LogWarning($"[POS_Catalog] '{e.label}'의 target Transform이 비어있습니다.", this);
            return;
        }

        // 버튼을 누른 '그 시점'의 world position을 읽어 PositionMovePanel에 세팅
        positionPanel.PresetFromTransform(e.target);

        if (autoMoveAfterSelect)
        {
            positionPanel.OnMove();
        }
    }
}
