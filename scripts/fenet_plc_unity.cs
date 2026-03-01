using UnityEngine;
using UnityEngine.UI;
using System;
using System.Collections;
using System.Collections.Generic;
using VagabondK.Protocols.Channels;
using VagabondK.Protocols.LSElectric;
using VagabondK.Protocols.LSElectric.FEnet;

using LsDeviceType = VagabondK.Protocols.LSElectric.DeviceType;
using LsDataType = VagabondK.Protocols.LSElectric.DataType;

public class Unity_PLC : MonoBehaviour
{
    [Header("TCP")]
    public string plcIp = "192.168.0.111";
    int plcPort = 2004;
    int connectTimeoutMs = 5000; // TCP 연결 시도 타임아웃

    TcpChannel _channel; // FEnet 프레임을 실제 TCP 소켓으로 송수신하는 전송 레이어 객체
    FEnetClient _xgt; // LS ELECTRIC FEnet 프로토콜 클라이언트

    float pollInterval = 0.2f; // 폴링 간격

    readonly string[] MutexMListRaw = new[]
    {
        "M00050","M00060","M00062","M00064","M00070","M00080",
        "M00090","M00092","M00094","M00100","M00102","M00104","M00106","M00108"
    }; // 서로 동시에 ON 되면 안되는 주소 (화면전환)
    HashSet<string> _mutexSet; // 상호배타 그룹을 O(1)로 조회하기 위한 집합
    List<string> _mutexRecentOn; // 최근 ON된 주소 한 개 추적

    public bool ReadBool(string addr) => (bool)Read(addr);
    public ushort ReadU16(string addr) => (ushort)Read(addr);
    public void WriteBool(string addr, bool v) => Write(addr, v);

    void Awake()
    {
        Application.runInBackground = true; // 백그라운드에서도 코루틴 사용 가능
    }

    void Start()
    {
        _channel = new TcpChannel(plcIp, plcPort, connectTimeoutMs); // TCP 전송 채널 생성
        _mutexSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase); // 상호배타 주소 집합용 HashSet 생성
        foreach (var m in MutexMListRaw) _mutexSet.Add(NormalizeBit(m)); // 원본 문자열 배열을 정규화해서 추가
        _mutexRecentOn = new List<string>(1); // 크기가 1인 리스트 생성 (최근 ON된 주소 추적)

        Connect();
        Start_Test();
    }

    public void Start_Test()
    {
        StartCoroutine(Test_PLC());
    }

    IEnumerator Test_PLC()
    {
        if (_xgt == null) Connect();

        yield break;
    }

    // 특정 주소의 값이 True가 될 때까지 기다리기
    IEnumerator WatchBitUntilTrue(string addr)
    {
        while (true)
        {
            bool v = ReadBool(addr);

            if (v)
            {
                yield break;
            }
            yield return new WaitForSeconds(pollInterval);
        }
    }

    // PLC 연결
    void Connect()
    {
        try
        {
            _channel.Write(Array.Empty<byte>()); // 채널이 사용 가능한 상태인지 확인
            _xgt = new FEnetClient(_channel); // FEnet 프로토콜 처리하는 클라이언트 객체 생성
            _xgt.Timeout = 3000; // FEnet 요청/응답 타임아웃
            _xgt.UseHexBitIndex = false; // 비트 주소 해석 방식
            Debug.Log("[PLC] Connected");
        }
        catch (Exception e)
        {
            Debug.LogError("[PLC] Connect Error: " + e.Message);
            throw; // 발생한 예외를 상위 호출자에게 전달
        }
    }

    // PLC 연결 해제
    void Disconnect()
    {
        try { _xgt?.Dispose(); } catch (Exception ex) { Debug.LogWarning("[PLC] XGT Dispose warn: " + ex.Message); }
        try { _channel?.Close(); } catch (Exception ex) { Debug.LogWarning("[PLC] Channel Close warn: " + ex.Message); }
        Debug.Log("[PLC] Disconnected");
    }

    void OnDestroy()
    {
        try { _xgt?.Dispose(); } catch (Exception ex) { Debug.LogWarning("[PLC] XGT Dispose warn: " + ex.Message); }
        try { _channel?.Dispose(); } catch (Exception ex) { Debug.LogWarning("[PLC] Channel Dispose warn: " + ex.Message); }
    }

    // PLC 주소 문자열을 표준형식으로 바꿈
    static string NormalizeBit(string addr)
    {
        addr = addr.Trim().ToUpperInvariant(); // Trim : 앞뒤 공백 제거, ToUpperInvariant : 모두 대문자 변환

        if (string.IsNullOrEmpty(addr) || addr.Length < 2)
            throw new ArgumentException("bit address");
        char head = addr[0]; // 주소 맨 앞 1글자
        if (!IsBitArea(addr))
            throw new NotSupportedException("not bit area: " + head);
        // ArgumentException : 메서드에 잘못된 인자를 전달했을 때 던져지는 예외 클래스 (함수에 넘겨진 값이 조건에 맞지 않음을 의미)

        var body = addr.Substring(1); // 주소 맨 앞 1글자 제거
        var wordPart = body.Substring(0, body.Length - 1); // 마지막 1글자를 제외한 부분 추출
        var bitHex = body.Substring(body.Length - 1, 1); // 마지막 1글자 추출
        int word = int.Parse(wordPart); // 문자열을 정수형으로 변환 (0201 -> 201)
        return head + word.ToString("D5") + bitHex; // ToString("D5") : 5자리 0채움으로 변환 (201 -> 00201)
    }

    static (LsDeviceType dt, uint globalBit, int word, int bit) ParseBit(string addr)
    {
        string normalized = NormalizeBit(addr);
        char head = normalized[0];
        var body = normalized.Substring(1);
        var wordPart = body.Substring(0, body.Length - 1);
        var bitHex = body.Substring(body.Length - 1, 1);
        int word = int.Parse(wordPart);
        int bit = Convert.ToInt32(bitHex, 16);
        uint global = (uint)(word * 16 + bit);

        LsDeviceType dt = head switch
        {
            'M' => LsDeviceType.M,
            'P' => LsDeviceType.P,
            'L' => LsDeviceType.L,
            'T' => LsDeviceType.T,
            _ => throw new NotSupportedException("Device type not mapped: " + head),
        }; // VagabondK.Protocols.LSElectric.DeviceType 값으로 변환

        return (dt, global, word, bit);
    }

    static (LsDeviceType dt, uint index) ParseWord(string addr)
    {
        addr = addr.Trim().ToUpperInvariant();
        if (string.IsNullOrEmpty(addr) || addr.Length < 2) 
            throw new ArgumentException("word address");
        char head = addr[0]; // 첫 글자
        if (!IsWordArea(addr))
            throw new NotSupportedException("not word area: " + head);
        
        var body = addr.Substring(1); // 영역 문자 제외한 나머지
        uint idx = uint.Parse(body); // 문자열을 부호 없는 정수로 변환

        LsDeviceType dt = head switch
        {
            'D' => LsDeviceType.D,
            'U' => LsDeviceType.U,
            'T' => LsDeviceType.T,
            _ => throw new NotSupportedException("Device type not mapped: " + head),
        };

        return (dt, idx);
    }

    // 데이터 타입이 Bit인지 확인
    static bool IsBitArea(string addr)
    {
        addr = addr.Trim().ToUpperInvariant();
        if (IsWordArea(addr)) return false;

        char head = addr[0];
        if (head == 'M' || head == 'P' || head == 'L' || head == 'D' || head == 'U' || head == 'T') return true;

        return false;
    }

    // 데이터 타입이 Word인지 확인
    static bool IsWordArea(string addr)
    {
        if (string.IsNullOrWhiteSpace(addr)) return false;

        addr = addr.Trim().ToUpperInvariant();
        char head = addr[0];

        if (head == 'D' || head == 'U' || head == 'T')
            return !addr.Contains('.');

        return false;
    }

    static (LsDeviceType dt, LsDataType ty, uint index) ParseAddress(string addr)
    {
        if (string.IsNullOrWhiteSpace(addr))
            throw new ArgumentException("address");

        addr = addr.Trim().ToUpperInvariant();
        char head = addr[0];

        if (IsBitArea(addr))
        {
            var p = ParseBit(addr);
            return (p.dt, LsDataType.Bit, p.globalBit);
        }
        else if (IsWordArea(addr))
        {
            var p = ParseWord(addr);
            return (p.dt, LsDataType.Word, p.index);
        }
        else
        {
            throw new NotSupportedException("Not Supported Area");
        }
    }

    // Bit 쓰기
    void WriteBit(string addr, bool on)
    {
        var p = ParseBit(addr);
        var dv = new DeviceVariable(p.dt, LsDataType.Word, (uint)p.word);

        var map = _xgt.Read(dv);
        ushort w = map[dv].UnsignedWordValue;

        ushort mask = (ushort)(1 << p.bit); // 1을 왼쪽으로 p.bit만큼 민 값
        ushort nw = on ? (ushort)(w | mask) : (ushort)(w & ~mask);
        // w | mask : w 그대로 유지하되 mask에서만 1인 부분도 1로 바꿈
        // ~mask : mask 반전 (0->1, 1->0)
        // w & ~mask : w와 mask 둘 다 1인 비트만 1

        if (nw != w) _xgt.Write(dv, nw); // 값이 변한 경우에만 쓰기 전송
    }

    // 읽기
    public object Read(string addr)
    {
        try
        {
            var (dt, ty, index) = ParseAddress(addr);
            var dv = new DeviceVariable(dt, ty, index);
            var map = _xgt.Read(dv);
            // FEnetClient.cs의 아래 메서드 사용
            // public IReadOnlyDictionary<DeviceVariable, DeviceValue> Read(params DeviceVariable[] variables);
            var v = map[dv];

            if (ty == LsDataType.Bit)
            {
                bool bv = (bool)v;
                Debug.Log($"[PLC][READ][Bit] {addr} = {bv}");
                return bv;
            }
            else if (ty == LsDataType.Word)
            {
                ushort wv = v.UnsignedWordValue;
                Debug.Log($"[PLC][READ][Word] {addr} = {wv}");
                return wv;
            }
            throw new NotSupportedException("datatype");
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PLC][READ][ERROR] {addr} : {ex.Message}");
            throw;
        }
    }

    // 쓰기
    public void Write(string addr, object value)
    {
        try
        {
            var (dt, ty, index) = ParseAddress(addr);

            if (ty == LsDataType.Bit)
            {
                bool b = value is bool bb ? bb : Convert.ToBoolean(value);
                // value가 bool형이면 bb에 넣기

                if (dt == LsDeviceType.M)
                {
                    var mb = NormalizeBit(addr); // 정규화

                    if (b && _mutexSet.Contains(mb)) // ON으로 바꾸는 동작이고, 입력 주소가 상호배타 그룹에 속하는 경우
                    {
                        string prev = (_mutexRecentOn.Count > 0) ? _mutexRecentOn[0] : null; // 이전에 ON된 주소 가져옴

                        if (!string.IsNullOrEmpty(prev) && !string.Equals(prev, mb, StringComparison.OrdinalIgnoreCase)) // 올바른 주소이고, 현재 ON하려는 주소와 다른 경우
                        {
                            WriteBit(prev, false);
                            //Debug.Log($"[PLC][WRITE][Mutex] {prev} <= OFF");
                        }
                        WriteBit(mb, true);

                        if (_mutexRecentOn.Count == 0) _mutexRecentOn.Add(mb);
                        else _mutexRecentOn[0] = mb;
                        //Debug.Log($"[PLC][WRITE][Bit][M] {addr} <= {b} (Mutex recent ON = {mb})");
                        return;
                    }
                    else // OFF 동작이거나, 상호배타 그룹에 속하지 않는 경우
                    {
                        WriteBit(mb, b);

                        if (!b && _mutexSet.Contains(mb)) // OFF 동작이고, 상호배타 그룹에 속해 있는 경우
                        {
                            if (_mutexRecentOn.Count > 0 && string.Equals(_mutexRecentOn[0], mb, StringComparison.OrdinalIgnoreCase))
                                _mutexRecentOn.Clear(); // 최근 ON 동작 주소가 자기 자신일 경우 목록을 비움
                        }
                        //Debug.Log($"[PLC][WRITE][Bit][M] {addr} <= {b}");
                        return;
                    }
                }
                else if (dt == LsDeviceType.P)
                {
                    WriteBit(addr, b);
                    //Debug.Log($"[PLC][WRITE][Bit][P] {addr} <= {b}");
                    return;
                }
                throw new NotSupportedException($"Write not allowed for this bit area: {dt}");
            }
            else if (ty == LsDataType.Word)
            {
                ushort w = value switch
                {
                    ushort u => u,
                    short s  => unchecked((ushort)s),
                    int i    => checked((ushort)i),
                    uint ui  => checked((ushort)ui),
                    string str => (ushort)(
                        str.StartsWith("0x", StringComparison.OrdinalIgnoreCase)
                        ? Convert.ToUInt16(str.Substring(2), 16)
                        : Convert.ToUInt16(str, 10)
                    ),
                    _ => Convert.ToUInt16(value)
                };

                var dv = new DeviceVariable(dt, LsDataType.Word, index);
                _xgt.Write(dv, w);

                //Debug.Log($"[PLC][WRITE][Word] {addr} <= {w}");
                return;
            }
            else
            {
                throw new NotSupportedException("datatype");
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PLC][WRITE][ERROR] {addr} : {ex.Message}");
            throw;
        }
    }
}
