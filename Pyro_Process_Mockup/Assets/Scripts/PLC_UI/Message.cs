using System;
using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;

public class Message : MonoBehaviour
{
    public enum DisplayMode { DirectString, StringTableIndex }
    public enum MessageSet { Set1, Set2, Set3, Custom }   // 화면별로 1/2/3 중 선택
    public enum EncodingMode { Ascii_2BytesPerWord, Utf16_1CharPerWord }
    public enum LengthUnit { Bytes, Chars }

    [Header("Mode")]
    [Tooltip("DirectString: D영역에서 문자열 직접 읽기\nStringTableIndex: D영역의 번호로 문자열표에서 선택")]
    public DisplayMode mode = DisplayMode.StringTableIndex;

    [Header("Set Selection (화면별로 1/2/3 중 선택)")]
    [Tooltip("메시지창 1= D000008 + Table1, 메시지창 2= D000009 + Table2, 메시지창 3= D000010 + Table3")]
    public MessageSet set = MessageSet.Set1;

    [Header("PLC & Target")]
    public Unity_PLC plc;
    public TMP_Text targetText;

    [Header("Addresses")]
    [Tooltip("메시지창1의 인덱스 주소")]
    public string addressSet1 = "D000008";
    [Tooltip("메시지창2의 인덱스 주소")]
    public string addressSet2 = "D000009";
    [Tooltip("메시지창3의 인덱스 주소")]
    public string addressSet3 = "D000010";
    [Tooltip("커스텀 인덱스 주소 (set=Custom일 때 사용)")]
    public string addressCustom = "D000008";

    [Header("String Tables (XP 번호와 동일한 인덱스, 빈칸 유지)")]
    [Tooltip("true면 내부에 내장된 기본 테이블을 사용하고, false면 아래 table1/2/3 직렬화 값을 사용합니다.")]
    public bool useBuiltinTables = true;

    [TextArea(1, 4)] public string[] table1; // 직렬화(원하면 여기 채워 사용)
    [TextArea(1, 4)] public string[] table2;
    [TextArea(1, 4)] public string[] table3;
    [TextArea(1, 4)] public string[] tableCustom; // 임시/테스트용

    // ---- 내장 기본 테이블 (0-based, 빈칸은 "")
    static readonly string[] DEFAULT_TABLE1 = new string[] {
        /*0*/ "운전대기중....",
        /*1*/ "운전준비완료....",
        /*2*/ "자동운전중....",
        /*3*/ "임시정지중....",
        /*4*/ "",
        /*5*/ "수동운전중....",
        /*6*/ "",
        /*7*/ "",
        /*8*/ "",
        /*9*/ "",
        /*10*/ "비상정지 알람....",
        /*11*/ "",
        /*12*/ "",
        /*13*/ "",
        /*14*/ "",
        /*15*/ "",
        /*16*/ "",
        /*17*/ "",
        /*18*/ "",
        /*19*/ "",
        /*20*/ "",
        /*21*/ "STEP1알람",
        /*22*/ "STEP2알람",
        /*23*/ "STEP3알람"
    };

    static readonly string[] DEFAULT_TABLE2 = new string[] {
        /*0*/ "운전대기중....",
        /*1*/ "운전준비완료....",
        /*2*/ "자동운전중....",
        /*3*/ "전해확보위치중....",
        /*4*/ "임시정지중....",
        /*5*/ "수동운전중....",
        /*6*/ "",
        /*7*/ "",
        /*8*/ "",
        /*9*/ "",
        /*10*/ "비상정지 알람....",
        /*11*/ "전복복구알람....",
        /*12*/ "Z축 상승리미트알람....",
        /*13*/ "Z축 하강리미트알람....",
        /*14*/ "CLMAP전압검출알람....",
        /*15*/ "CLMAP전류검출알람....",
        /*16*/ "PS1전압검출알람....",
        /*17*/ "PS1전류검출알람....",
        /*18*/ "RECEIVER열 알람탐지알람....",
        /*19*/ "RECEIVER회전알람....",
        /*20*/ "RECEIVER전진후진알람....",
        /*21*/ "Y축알람",
        /*22*/ "X축알람",
        /*23*/ "Z축알람",
        /*24*/ "R축알람",
        /*25*/ "G축알람"
    };

    static readonly string[] DEFAULT_TABLE3 = new string[] {
        /*0*/ "운전대기중....",
        /*1*/ "운전준비완료....",
        /*2*/ "자동운전중....",
        /*3*/ "임시정지중....",
        /*4*/ "",
        /*5*/ "수동운전중....",
        /*6*/ "",
        /*7*/ "",
        /*8*/ "",
        /*9*/ "",
        /*10*/ "비상정지 알람....",
        /*11*/ "",
        /*12*/ "",
        /*13*/ "",
        /*14*/ "",
        /*15*/ "",
        /*16*/ "",
        /*17*/ "",
        /*18*/ "",
        /*19*/ "",
        /*20*/ "",
        /*21*/ "Y축알람",
        /*22*/ "X축알람",
        /*23*/ "Z축알람",
        /*24*/ "R축알람",
        /*25*/ "G축알람"
    };

    [Tooltip("XP 문자열표 인덱스가 0부터 시작이므로 기본값 = false")]
    public bool oneBasedIndex = false;
    [Tooltip("인덱스가 0/범위 밖이면 빈 문자열로 표시")]
    public bool blankWhenOutOfRange = true;

    [Header("DirectString 모드 옵션")]
    [Min(1)] public int wordCount = 32;
    public EncodingMode encoding = EncodingMode.Utf16_1CharPerWord; // 한글이면 보통 이게 맞음
    public bool trimAtNull = true;
    public bool swapBytesInWord = false;

    [Header("Framing (DirectString 전용)")]
    public bool hasLengthPrefix = false;
    public LengthUnit lengthUnit = LengthUnit.Chars;
    [Min(0)] public int startWordOffset = 0;

    [Header("Polling")]
    [Min(0.05f)] public float pollIntervalSec = 0.2f;
    public bool updateOnlyOnChange = true;

    private string _lastShown;

    void Reset() { targetText = GetComponent<TMP_Text>(); }
    void Awake() { if (!targetText) targetText = GetComponent<TMP_Text>(); }
    void OnEnable() { StartCoroutine(PollLoop()); }

    IEnumerator PollLoop()
    {
        while (true)
        {
            string s = null;
            try
            {
                s = (mode == DisplayMode.StringTableIndex) ? ReadByIndexOnce() : ReadStringOnce();
            }
            catch (Exception ex) { Debug.LogWarning($"[Message] Read error: {ex.Message}"); }

            if (!updateOnlyOnChange || _lastShown != s)
            {
                if (targetText) targetText.text = s ?? string.Empty;
                _lastShown = s;
            }
            yield return new WaitForSeconds(pollIntervalSec);
        }
    }

    public void RefreshOnce()
    {
        var s = (mode == DisplayMode.StringTableIndex) ? ReadByIndexOnce() : ReadStringOnce();
        if (targetText) targetText.text = s ?? string.Empty;
        _lastShown = s;
    }

    // ===== StringTableIndex 모드 =====
    public string ReadByIndexOnce()
    {
        if (plc == null) throw new InvalidOperationException("Unity_PLC is null.");

        // 1) 주소 선택
        string addr = set switch
        {
            MessageSet.Set1 => addressSet1,
            MessageSet.Set2 => addressSet2,
            MessageSet.Set3 => addressSet3,
            MessageSet.Custom => addressCustom,
            _ => addressSet1
        };
        addr = NormalizeD(addr);
        if (!TryParseD(addr, out uint baseIndex)) throw new ArgumentException($"Invalid D address: {addr}");

        // 2) 인덱스 읽기
        ushort raw = ReadWord(baseIndex);

        // 3) 테이블 선택
        var table = GetActiveTable();

        int idx = oneBasedIndex ? raw - 1 : raw; // 기본 0-based
        if (table == null || table.Length == 0) return string.Empty;

        if (idx < 0 || idx >= table.Length)
            return blankWhenOutOfRange ? string.Empty : $"[#{raw}]";

        return table[idx] ?? string.Empty;
    }

    string[] GetActiveTable()
    {
        if (!useBuiltinTables)
        {
            return set switch
            {
                MessageSet.Set1 => table1,
                MessageSet.Set2 => table2,
                MessageSet.Set3 => table3,
                MessageSet.Custom => tableCustom,
                _ => table1
            };
        }
        // 내장 기본으로 선택
        return set switch
        {
            MessageSet.Set1 => DEFAULT_TABLE1,
            MessageSet.Set2 => DEFAULT_TABLE2,
            MessageSet.Set3 => DEFAULT_TABLE3,
            MessageSet.Custom => tableCustom, // 커스텀은 사용자가 직접 넣을 수 있게 남김
            _ => DEFAULT_TABLE1
        };
    }

    // ===== DirectString 모드 =====
    public string ReadStringOnce()
    {
        if (plc == null) throw new InvalidOperationException("Unity_PLC is null.");

        // DirectString 모드에서도 선택한 Set의 주소를 사용
        string addr = set switch
        {
            MessageSet.Set1 => addressSet1,
            MessageSet.Set2 => addressSet2,
            MessageSet.Set3 => addressSet3,
            MessageSet.Custom => addressCustom,
            _ => addressSet1
        };
        addr = NormalizeD(addr);
        if (!TryParseD(addr, out uint baseIndex)) throw new ArgumentException($"Invalid D address: {addr}");

        int dataStartOffset = startWordOffset;
        int wordsToRead;

        int effectiveChars = -1, effectiveBytes = -1;
        if (hasLengthPrefix)
        {
            ushort lenWord = ReadWord(baseIndex);
            dataStartOffset += 1;

            if (encoding == EncodingMode.Utf16_1CharPerWord || lengthUnit == LengthUnit.Chars)
                effectiveChars = lenWord;
            else
                effectiveBytes = lenWord;
        }

        if (encoding == EncodingMode.Utf16_1CharPerWord)
        {
            int maxChars = wordCount;
            if (effectiveChars >= 0) maxChars = Mathf.Min(maxChars, effectiveChars);
            wordsToRead = Mathf.Clamp(maxChars, 0, wordCount);

            var chars = new char[wordsToRead];
            for (int i = 0; i < wordsToRead; i++)
            {
                ushort w = ReadWord(baseIndex + (uint)(dataStartOffset + i));
                if (swapBytesInWord) w = SwapBytes(w);
                char ch = (char)w;
                if (trimAtNull && ch == '\0') return new string(chars, 0, i);
                chars[i] = ch;
            }
            return new string(chars);
        }
        else // ASCII (2 bytes per word)
        {
            int maxBytes = wordCount * 2;
            if (effectiveBytes >= 0) maxBytes = Mathf.Min(maxBytes, effectiveBytes);
            wordsToRead = Mathf.CeilToInt(maxBytes / 2f);

            var bytes = new byte[wordsToRead * 2];
            int bi = 0;
            for (int i = 0; i < wordsToRead; i++)
            {
                ushort w = ReadWord(baseIndex + (uint)(dataStartOffset + i));
                if (swapBytesInWord) w = SwapBytes(w);
                bytes[bi++] = (byte)(w & 0xFF);
                bytes[bi++] = (byte)((w >> 8) & 0xFF);
            }

            int len = bytes.Length;
            if (trimAtNull)
            {
                int z = Array.IndexOf<byte>(bytes, 0);
                if (z >= 0) len = z;
            }
            return Encoding.ASCII.GetString(bytes, 0, len);
        }
    }

    // ===== 내부 유틸 =====
    string NormalizeD(string addr)
    {
        addr = (addr ?? "").Trim().ToUpperInvariant();
        if (!addr.StartsWith("D")) addr = "D" + addr;
        return addr;
    }

    bool TryParseD(string addr, out uint index)
    {
        index = 0;
        if (string.IsNullOrEmpty(addr) || addr[0] != 'D') return false;
        return uint.TryParse(addr.Substring(1), out index);
    }

    // Unity_PLC.ReadU16을 사용해 한 워드 읽기
    ushort ReadWord(uint dIndex)
    {
        return plc.ReadU16($"D{dIndex:D6}");
    }

    static ushort SwapBytes(ushort w) => (ushort)(((w & 0xFF) << 8) | ((w >> 8) & 0xFF));
}
