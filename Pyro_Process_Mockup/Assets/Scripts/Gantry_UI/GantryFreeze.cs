using UnityEngine;
using System.Collections.Generic;

[DefaultExecutionOrder(9999)]
public class GantryFreeze : MonoBehaviour
{
    private static GantryFreeze _instance;

    public static bool IsStopped => _instance != null && _instance._isStopped;

    [System.Serializable]
    public class FreezeTarget
    {
        public Transform target;

        [HideInInspector] public Vector3 pos;
        [HideInInspector] public Quaternion rot;
        [HideInInspector] public Vector3 scale;

        [HideInInspector] public Rigidbody rb;
        [HideInInspector] public ArticulationBody ab;
    }

    [Header("멈출 오브젝트들 (갠트리 루트 / ToolSocket 달린놈 등)")]
    public List<FreezeTarget> targets = new List<FreezeTarget>();

    private bool _isStopped = false;

    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }
        _instance = this;
        DontDestroyOnLoad(gameObject);

        // 캐싱
        for (int i = 0; i < targets.Count; i++)
        {
            var t = targets[i];
            if (t == null || t.target == null) continue;
            t.rb = t.target.GetComponent<Rigidbody>();
            t.ab = t.target.GetComponent<ArticulationBody>();
        }
    }

    private void LateUpdate()
    {
        if (!_isStopped) return;

        for (int i = 0; i < targets.Count; i++)
        {
            var t = targets[i];
            if (t == null || t.target == null) continue;

            // transform 되돌리기
            t.target.position = t.pos;
            t.target.rotation = t.rot;
            t.target.localScale = t.scale;

            // 물리 멈추기
            if (t.rb != null)
            {
                t.rb.linearVelocity = Vector3.zero;
                t.rb.angularVelocity = Vector3.zero;
                t.rb.isKinematic = true;
            }

            if (t.ab != null)
            {
                t.ab.linearVelocity = Vector3.zero;
                t.ab.angularVelocity = Vector3.zero;

                var drive = t.ab.xDrive;
                drive.targetVelocity = 0f;
                drive.forceLimit = 0f;
                t.ab.xDrive = drive;
            }
        }
    }

    // ===== 외부에서 쓰는 애들 =====

    public static void StopAll()
    {
        if (_instance == null) return;
        _instance.SetStop(true);
    }

    public static void ResumeAll()
    {
        if (_instance == null) return;
        _instance.SetStop(false);
    }

    public static void ToggleFromUI()
    {
        if (IsStopped) ResumeAll();
        else StopAll();
    }

    // ===== 내부 =====

    private void SetStop(bool stop)
    {
        _isStopped = stop;

        if (stop)
        {
            // 현 상태 저장 + 즉시 멈춤
            for (int i = 0; i < targets.Count; i++)
            {
                var t = targets[i];
                if (t == null || t.target == null) continue;

                t.pos = t.target.position;
                t.rot = t.target.rotation;
                t.scale = t.target.localScale;

                if (t.rb != null)
                {
                    t.rb.linearVelocity = Vector3.zero;
                    t.rb.angularVelocity = Vector3.zero;
                    t.rb.isKinematic = true;
                }

                if (t.ab != null)
                {
                    t.ab.linearVelocity = Vector3.zero;
                    t.ab.angularVelocity = Vector3.zero;

                    var drive = t.ab.xDrive;
                    drive.targetVelocity = 0f;
                    drive.forceLimit = 0f;
                    t.ab.xDrive = drive;
                }
            }

            Debug.Log("<color=red>[FREEZE]</color> targets frozen.");
        }
        else
        {
            // 풀기
            for (int i = 0; i < targets.Count; i++)
            {
                var t = targets[i];
                if (t == null) continue;

                if (t.rb != null)
                    t.rb.isKinematic = false;
            }

            Debug.Log("<color=green>[FREEZE]</color> targets resumed.");
        }
    }
}
