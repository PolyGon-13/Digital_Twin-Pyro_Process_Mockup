using UnityEngine;

public class AutoDoor : MonoBehaviour
{
    [Header("누굴 기준으로 거리 측정할지 (보통 XR 카메라)")]
    public Transform player;                     // XR Origin의 Main Camera를 드래그

    [Header("각도 (로컬 Y)")]
    public float closedAngleY = 0f;              // 닫힘 각도
    public float openAngleY = 90f;             // 열림 각도 (반대방향이면 -90)

    [Header("거리 (히스테리시스 적용)")]
    public float openDistance = 2.0f;           // 이 거리 이내로 들어오면 OPEN
    public float closeDistance = 2.5f;           // 이 거리 밖으로 나가면 CLOSE

    [Header("회전 속도")]
    public float rotateSpeedDegPerSec = 180f;    // 초당 회전(도)

    [Header("기타")]
    public bool useLocalY = true;                // 로컬 Y축 기준(일반적)
    public bool drawGizmos = true;

    float _targetY;
    bool _isOpen;

    void Reset()
    {
        // 현재 각도를 닫힘으로 설정하고 열림=닫힘+90도로 자동 초기화
        closedAngleY = transform.localEulerAngles.y;
        openAngleY = closedAngleY + 90f;
    }

    void Awake()
    {
        if (player == null && Camera.main != null) player = Camera.main.transform;
        _targetY = closedAngleY;
        _isOpen = false;
    }

    void Update()
    {
        if (player != null)
        {
            float d = Vector3.Distance(player.position, transform.position);

            // 히스테리시스: 덜 달그락거리게
            if (!_isOpen && d <= openDistance)
            {
                _isOpen = true;
                _targetY = openAngleY;
            }
            else if (_isOpen && d >= closeDistance)
            {
                _isOpen = false;
                _targetY = closedAngleY;
            }
        }

        // 현재 Y 읽어서 목표 각도로 부드럽게 회전
        float currentY = useLocalY ? transform.localEulerAngles.y : transform.eulerAngles.y;
        float nextY = Mathf.MoveTowardsAngle(currentY, _targetY, rotateSpeedDegPerSec * Time.deltaTime);

        if (useLocalY)
        {
            Vector3 e = transform.localEulerAngles; e.y = nextY; transform.localEulerAngles = e;
        }
        else
        {
            Vector3 e = transform.eulerAngles; e.y = nextY; transform.eulerAngles = e;
        }
    }

    void OnDrawGizmosSelected()
    {
        if (!drawGizmos) return;
        Gizmos.color = new Color(0f, 1f, 0f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, openDistance);
        Gizmos.color = new Color(1f, 0.5f, 0f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, closeDistance);
    }
}
