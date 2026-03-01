using UnityEngine;

public class ToolSocketFollower : MonoBehaviour
{
    public Transform source;       // 여기 toolSocket 넣어
    public Rigidbody followerRB;   // 이 오브젝트 자기 자신의 RB

    void Reset()
    {
        followerRB = GetComponent<Rigidbody>();
    }

    void LateUpdate()
    {
        if (source == null || followerRB == null) return;

        // kinematic RB니까 그냥 위치/회전 덮어써도 됨
        followerRB.MovePosition(source.position);
        followerRB.MoveRotation(source.rotation);
    }
}
