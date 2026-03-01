using UnityEngine;
using UnityEngine.UI;
#if TMP_PRESENT || UNITY_2018_4_OR_NEWER
using TMPro;
#endif

/// <summary>
/// 버튼을 누를 때마다
/// 1) 버튼 라벨을 Open/Close로 토글
/// 2) targetGroup(GameObject)의 활성/비활성 전환
/// 라벨은 targetGroup의 현재 활성 상태를 기준으로 자동 동기화됩니다.
/// </summary>
[RequireComponent(typeof(Button))]
public class OpenCloseToggle : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("켜고/끄고 싶었던 '화면모음' 오브젝트 (패널/그룹 루트)")]
    [SerializeField] private GameObject targetGroup;

    [Header("Label")]
    [Tooltip("버튼 라벨 - TMP를 쓰면 TMP_Text에, uGUI Text를 쓰면 Text에 할당")]
#if TMP_PRESENT || UNITY_2018_4_OR_NEWER
    [SerializeField] private TMP_Text tmpLabel;
#endif
    [SerializeField] private Text uGuiLabel;

    [Header("Texts")]
    [Tooltip("targetGroup이 비활성일 때(닫힘 상태) 버튼에 보여줄 문구")]
    [SerializeField] private string openText = "Open";
    [Tooltip("targetGroup이 활성일 때(열림 상태) 버튼에 보여줄 문구")]
    [SerializeField] private string closeText = "Close";

    private Button _button;

    private void Awake()
    {
        _button = GetComponent<Button>();

        if (targetGroup == null)
        {
            Debug.LogError("[OpenCloseToggle] targetGroup이 비어있습니다.", this);
        }

        // 버튼 클릭 이벤트 등록
        _button.onClick.AddListener(OnClick);

        // 현재 targetGroup 상태를 기준으로 라벨 동기화
        SyncLabelWithTarget();
    }

    private void OnEnable()
    {
        // 에디터에서 활성/비활성 바꿨을 수 있으니 다시 동기화
        SyncLabelWithTarget();
    }

    private void OnClick()
    {
        if (targetGroup == null) return;

        // 상태 토글
        bool nextActive = !targetGroup.activeSelf;
        targetGroup.SetActive(nextActive);

        // 라벨도 토글된 상태에 맞춰 갱신
        SetLabel(nextActive ? closeText : openText);
    }

    private void SyncLabelWithTarget()
    {
        if (targetGroup == null)
        {
            SetLabel(openText); // 안전 기본값
            return;
        }

        // target이 현재 활성(열림)이면 Close, 비활성(닫힘)이면 Open을 표기
        SetLabel(targetGroup.activeSelf ? closeText : openText);
    }

    private void SetLabel(string text)
    {
#if TMP_PRESENT || UNITY_2018_4_OR_NEWER
        if (tmpLabel != null)
        {
            tmpLabel.text = text;
            return;
        }
#endif
        if (uGuiLabel != null)
        {
            uGuiLabel.text = text;
            return;
        }

        // 라벨 컴포넌트가 하나도 연결 안 된 경우
        // 버튼 이름으로라도 표시해 둠
        gameObject.name = $"Button_{text}";
    }
}
