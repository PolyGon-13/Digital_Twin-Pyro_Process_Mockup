using UnityEngine;
using TMPro;
using System.Collections;

public class Temparature : MonoBehaviour
{
    public Unity_PLC plc;

    [Header("PLC 주소 (ElectroRecovery + Distillation 통합)")]
    public string addr_CurTemp_Control = "D001502";
    public string addr_CurTemp_1 = "D001506";
    public string addr_CurTemp_2 = "D001508";
    public string addr_Cur_LU = "D02002";
    public string addr_Cur_LL = "D02012";

    [Header("TextMeshPro 표시 대상 (총 5개)")]
    public TMP_Text txt_CurTemp_Control;
    public TMP_Text txt_CurTemp_1;
    public TMP_Text txt_CurTemp_2;
    public TMP_Text txt_Cur_LU;
    public TMP_Text txt_Cur_LL;

    [Header("표시 형식 설정")]
    public float scale = 1.0f;
    public string numberFormat = "0.0";
    [Min(0.05f)] public float readInterval = 0.2f;

    private Coroutine _pollLoop;

    void OnEnable()
    {
        if (_pollLoop == null)
            _pollLoop = StartCoroutine(ReadTempsLoop());
    }

    void OnDisable()
    {
        if (_pollLoop != null) StopCoroutine(_pollLoop);
        _pollLoop = null;
    }

    IEnumerator ReadTempsLoop()
    {
        // slice: 0~4 (제어용, 1, 2, LU, LL)
        int slice = 0;
        var wait = new WaitForSeconds(Mathf.Max(0.05f, readInterval));

        while (true)
        {
            if (plc == null) { yield return wait; continue; }

            switch (slice)
            {
                case 0:
                    SafeReadToTMP(addr_CurTemp_Control, txt_CurTemp_Control);
                    break;
                case 1:
                    SafeReadToTMP(addr_CurTemp_1, txt_CurTemp_1);
                    break;
                case 2:
                    SafeReadToTMP(addr_CurTemp_2, txt_CurTemp_2);
                    break;
                case 3:
                    SafeReadToTMP(addr_Cur_LU, txt_Cur_LU);
                    break;
                case 4:
                    SafeReadToTMP(addr_Cur_LL, txt_Cur_LL);
                    break;
            }

            // 다음 슬라이스로 이동 (0~4 순환)
            slice = (slice + 1) % 5;

            // 프레임 분산: 각 슬라이스 사이에 한 프레임 쉬기
            yield return null;

            // 한 바퀴 끝나면 설정 주기만큼 대기
            if (slice == 0)
                yield return wait;
        }
    }

    void SafeReadToTMP(string addr, TMP_Text target)
    {
        if (string.IsNullOrWhiteSpace(addr) || target == null) return;
        try
        {
            ushort raw = plc.ReadU16(addr);
            float val = raw * scale;
            target.text = val.ToString(string.IsNullOrEmpty(numberFormat) ? "0" : numberFormat);
        }
        catch { }
    }

    void OnValidate()
    {
        readInterval = Mathf.Max(0.05f, readInterval);
    }
}
