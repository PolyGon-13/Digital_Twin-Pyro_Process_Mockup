using System.Collections;
using UnityEngine;

public class RackModule : MonoBehaviour
{
    [Header("PLC")]
    public Unity_PLC plc;                  // Unity_PLC 컴포넌트를 드래그해서 할당

    [Header("PLC Bit Addresses")]
    public string addrDetach1 = "P00092";  // 탈착1
    public string addrDetach2 = "P00093";  // 탈착2
    public string addrDetach3 = "P00094";  // 탈착3

    [Header("UI - 불 켜진 이미지 오브젝트")]
    public GameObject lampDetach1On;       // ON일 때 보일 오브젝트(이미지)
    public GameObject lampDetach2On;
    public GameObject lampDetach3On;

    [Header("Polling")]
    [Tooltip("PLC 폴링 주기(초). 너무 빠르면 부하가 커짐")]
    [Range(0.05f, 1f)]
    public float pollInterval = 0.2f;

    Coroutine _pollCo;

    void OnEnable() => StartPolling();
    void OnDisable() => StopPolling();

    public void StartPolling()
    {
        if (_pollCo == null) _pollCo = StartCoroutine(PollLoop());
    }

    public void StopPolling()
    {
        if (_pollCo != null)
        {
            StopCoroutine(_pollCo);
            _pollCo = null;
        }
    }

    IEnumerator PollLoop()
    {
        while (true)
        {
            UpdateLamp(lampDetach1On, SafeRead(addrDetach1));
            UpdateLamp(lampDetach2On, SafeRead(addrDetach2));
            UpdateLamp(lampDetach3On, SafeRead(addrDetach3));

            yield return new WaitForSeconds(pollInterval);
        }
    }

    bool SafeRead(string addr)
    {
        if (plc == null || string.IsNullOrWhiteSpace(addr)) return false;
        try
        {
            return plc.ReadBool(addr); // Unity_PLC에서 비트 읽기
        }
        catch (System.Exception ex)
        {
            // 읽기 실패하면 안전하게 꺼진 상태로 처리(로그만 남김)
            Debug.LogWarning($"[HoldModule] Read failed {addr}: {ex.Message}");
            return false;
        }
    }

    void UpdateLamp(GameObject onImageObject, bool isOn)
    {
        if (onImageObject == null) return;
        if (onImageObject.activeSelf != isOn)
            onImageObject.SetActive(isOn);
    }

    // 필요하면 버튼 등으로 1회 갱신하고 싶을 때 호출
    public void RefreshOnce()
    {
        UpdateLamp(lampDetach1On, SafeRead(addrDetach1));
        UpdateLamp(lampDetach2On, SafeRead(addrDetach2));
        UpdateLamp(lampDetach3On, SafeRead(addrDetach3));
    }
}
