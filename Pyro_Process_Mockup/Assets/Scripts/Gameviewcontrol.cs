using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using Unity.XR.CoreUtils;

/// <summary>
/// XR 활성 시: 입력 비간섭(헤드셋/컨트롤러가 담당)
/// XR 비활성 시:
///   - 우클릭: 카메라 시야 회전 (Yaw/Pitch)
///   - Shift + 우클릭: 몸(Origin 루트) 회전 (Yaw)
///   - 화살표(↑↓←→): 이동 (카메라 시야 기준)
/// 좌클릭은 사용하지 않으므로 UGUI 버튼 클릭과 충돌하지 않음.
/// </summary>
public class Gameviewcontrol : MonoBehaviour
{
    [Header("References")]
    public Transform yawRoot; // 몸(이동/회전용 루트, 보통 XROrigin)
    public Transform cam;     // 카메라(시야 회전용)

    [Header("Look Settings")]
    public float lookSensitivityDegPerSec = 150f;
    public bool invertY = false;
    public float minPitch = -80f;
    public float maxPitch = 80f;

    [Header("Move Settings (Arrow Keys)")]
    public float moveSpeed = 3.5f;
    public float sprintMultiplier = 1.8f;
    public bool keepOnPlane = true;
    public KeyCode sprintKey = KeyCode.LeftShift;

    float _yawLocal;   // 카메라 local yaw
    float _pitchLocal; // 카메라 local pitch
    float _yawRoot;    // 몸 yaw
    static readonly List<XRInputSubsystem> _xrInputs = new();

    void Reset()
    {
        cam = transform;
    }

    void OnEnable()
    {
        if (!cam) cam = transform;
        if (!yawRoot)
        {
            var origin = GetComponentInParent<XROrigin>();
            if (origin) yawRoot = origin.transform;
        }
        if (!yawRoot) yawRoot = cam ? cam.parent : transform;

        var camLocal = cam ? cam.localEulerAngles : Vector3.zero;
        _yawLocal = NormalizeToMinus180To180(camLocal.y);
        _pitchLocal = NormalizeToMinus180To180(camLocal.x);
        _yawRoot = NormalizeToMinus180To180(yawRoot.eulerAngles.y);
    }

    void Update()
    {
        if (IsXRActive())
        {
            ReleaseCursorIfNeeded();
            return;
        }

        HandleLook();
        HandleMove();
    }

    void HandleLook()
    {
        bool rmb = Input.GetMouseButton(1);
        bool bodyTurn = rmb && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));

        if (!rmb)
        {
            // 우클릭이 아닐 때는 커서 잠금 해제 (좌클릭 UI 클릭 방해 방지)
            ReleaseCursorIfNeeded();
            return;
        }

        LockCursorIfNeeded();

        float dt = Time.deltaTime;
        float mx = Input.GetAxis("Mouse X") * lookSensitivityDegPerSec * dt;
        float my = Input.GetAxis("Mouse Y") * lookSensitivityDegPerSec * dt;

        if (bodyTurn)
        {
            // Shift + RMB: 몸 회전 (Yaw만)
            _yawRoot += mx;
            if (yawRoot) yawRoot.rotation = Quaternion.Euler(0f, _yawRoot, 0f);
        }
        else
        {
            // RMB: 카메라 시야 회전 (Yaw/Pitch)
            _yawLocal += mx;
            _pitchLocal += (invertY ? 1f : -1f) * my;
            _pitchLocal = Mathf.Clamp(_pitchLocal, minPitch, maxPitch);
            if (cam) cam.localRotation = Quaternion.Euler(_pitchLocal, _yawLocal, 0f);
        }
    }

    void HandleMove()
    {
        int h = 0;
        if (Input.GetKey(KeyCode.RightArrow)) h += 1;
        if (Input.GetKey(KeyCode.LeftArrow)) h -= 1;

        int v = 0;
        if (Input.GetKey(KeyCode.UpArrow)) v += 1;
        if (Input.GetKey(KeyCode.DownArrow)) v -= 1;

        if (h == 0 && v == 0) return;

        float speed = moveSpeed * (Input.GetKey(sprintKey) ? sprintMultiplier : 1f);

        // 이동 기준은 카메라가 보는 방향
        Vector3 forward = cam ? cam.forward : Vector3.forward;
        Vector3 right = cam ? cam.right : Vector3.right;

        if (keepOnPlane)
        {
            forward.y = 0f; right.y = 0f;
            forward.Normalize(); right.Normalize();
        }

        Vector3 move = (forward * v + right * h) * speed * Time.deltaTime;
        if (yawRoot) yawRoot.position += move;
    }

    // ---- Helpers ----
    static bool IsXRActive()
    {
        _xrInputs.Clear();
        SubsystemManager.GetSubsystems(_xrInputs);
        foreach (var s in _xrInputs)
            if (s != null && s.running) return true;
        return false;
    }

    static float NormalizeToMinus180To180(float deg)
    {
        return Mathf.Repeat(deg + 180f, 360f) - 180f;
    }

    void LockCursorIfNeeded()
    {
        if (Cursor.lockState != CursorLockMode.Locked)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void ReleaseCursorIfNeeded()
    {
        if (Cursor.lockState != CursorLockMode.None)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
