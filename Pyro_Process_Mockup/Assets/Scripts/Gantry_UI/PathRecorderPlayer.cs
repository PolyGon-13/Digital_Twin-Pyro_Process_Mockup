using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using TMPro; // ← TextMeshPro 사용 시

[RequireComponent(typeof(LineRenderer))]
public class PathRecorderPlayer : MonoBehaviour
{
    [Header("Targets")]
    public Transform detector;            // PositionDetector (ToolSocket과 같은 위치)
    public Transform replayTarget;        // (IK 없을 때만 사용)

    [Header("Gantry IK (우선 사용)")]
    public GantryIKArticulation gantryIK; // 이게 있으면 gantryIK.Target을 이동시킨다

    [Header("Recording")]
    [Range(0.01f, 0.5f)] public float samplePeriod = 0.05f;
    public bool includeRotation = false;
    public bool clearOnRecordStart = true;

    [Header("Replay")]
    public bool useRecordedTiming = true;
    public float speedScale = 1f; // useRecordedTiming=false일 때만 사용

    [Header("Export (CSV)")]
    public bool exportMillimeters = true;
    public string fileNamePrefix = "GantryPath";

    [Header("UI")]
    public Image savedIndicator;

    [Tooltip("녹화 경로 없음(Idle)일 때 표시 색상")]
    public Color savedIndicatorIdleColor = Color.white;
    [Tooltip("녹화 경로 있음(Recorded)일 때 표시 색상")]
    public Color savedIndicatorRecordedColor = Color.green;

    [Space(6)]
    [Tooltip("녹화 버튼(색/라벨 변경)")]
    public Button recordButton;
    [Tooltip("재생 버튼(색/라벨 변경)")]
    public Button replayButton;

    [Tooltip("Record 버튼의 라벨(TextMeshPro). 비우면 라벨 변경 생략")]
    public TMP_Text recordLabel;
    [Tooltip("Replay 버튼의 라벨(TextMeshPro). 비우면 라벨 변경 생략")]
    public TMP_Text replayLabel;

    [Tooltip("녹화 중 표시 텍스트")]
    public string recordActiveText = "Recording...";
    [Tooltip("재생 중 표시 텍스트")]
    public string replayActiveText = "Replaying...";

    [Tooltip("녹화/재생 중 버튼 색상")]
    public Color activeButtonColor = Color.red;

    [Header("Line Visualization (Optional)")]
    [Range(0f, 0.01f)] public float minPointDistance = 0.001f;

    // ----- 내부 상태 -----
    struct Sample { public float t; public Vector3 pos; public Quaternion rot; }
    readonly List<Sample> _samples = new();
    bool _isRecording;
    float _t, _acc;
    Coroutine _replayCo;
    LineRenderer _line;

    // UI 복귀용 기본값 저장
    Color _recordDefaultColor, _replayDefaultColor;
    string _recordIdleText = "Record";
    string _replayIdleText = "Replay";

    void Awake()
    {
        _line = GetComponent<LineRenderer>();
        _line.positionCount = 0;

        // 버튼 기본 색/라벨 보관
        if (recordButton != null)
        {
            _recordDefaultColor = recordButton.image != null ? recordButton.image.color : Color.white;
        }
        if (replayButton != null)
        {
            _replayDefaultColor = replayButton.image != null ? replayButton.image.color : Color.white;
        }
        if (recordLabel != null && !string.IsNullOrEmpty(recordLabel.text))
            _recordIdleText = recordLabel.text;
        if (replayLabel != null && !string.IsNullOrEmpty(replayLabel.text))
            _replayIdleText = replayLabel.text;

        UpdateIndicator();
    }

    void Update()
    {
        if (!_isRecording || detector == null) return;

        _acc += Time.deltaTime;
        while (_acc >= samplePeriod)
        {
            _acc -= samplePeriod;
            _t += samplePeriod;
            AppendSample(detector.position, detector.rotation, _t);
        }
    }

    // ==== Record ====
    public void OnRecordToggle()
    {
        if (!_isRecording) StartRecording();
        else StopRecording();
    }

    void StartRecording()
    {
        if (detector == null) { Debug.LogWarning("[PathRP] Detector is null."); return; }
        if (_replayCo != null) StopCoroutine(_replayCo);

        if (clearOnRecordStart)
        {
            _samples.Clear();
            _line.positionCount = 0;
            _t = 0f;
        }

        // 시작 즉시 1포인트 저장
        AppendSample(detector.position, detector.rotation, _t);
        _acc = 0f;
        _isRecording = true;
        Debug.Log("[PathRP] Recording START");

        // ▶ UI: Record 버튼 활성 표시
        SetRecordVisual(active: true);

        UpdateIndicator();
    }

    void StopRecording()
    {
        _isRecording = false;
        Debug.Log($"[PathRP] Recording STOP. Samples: {_samples.Count}");

        // ▶ UI: Record 버튼 원복
        SetRecordVisual(active: false);

        UpdateIndicator();
    }

    // ==== Replay ====
    public void OnReplay()
    {
        if (_isRecording) { Debug.LogWarning("[PathRP] Recording… cannot replay."); return; }
        if (_samples.Count < 2) { Debug.LogWarning("[PathRP] No path to replay."); return; }

        Transform t = ResolveReplayTransform();
        if (t == null) { Debug.LogWarning("[PathRP] No replay transform."); return; }

        if (_replayCo != null) StopCoroutine(_replayCo);
        _replayCo = StartCoroutine(Co_Replay(t));
    }

    Transform ResolveReplayTransform()
    {
        if (gantryIK != null && gantryIK.Target != null)
            return gantryIK.Target;                   // ✅ IK 우선
        if (replayTarget != null) return replayTarget;
        return detector; // 최후의 보루
    }

    System.Collections.IEnumerator Co_Replay(Transform tMove)
    {
        Debug.Log("[PathRP] Replay START (IK first)");

        // ▶ UI: Replay 버튼 활성 표시
        SetReplayVisual(active: true);

        // 1) 시작점으로 'Target' 순간이동 (갠트리 루트는 절대 이동 X)
        var first = _samples[0];
        tMove.position = first.pos;
        if (includeRotation) tMove.rotation = first.rot;

        // 2) 샘플 구간 따라 보간 이동 (IK가 조인트를 구동)
        for (int i = 0; i < _samples.Count - 1; i++)
        {
            var a = _samples[i];
            var b = _samples[i + 1];

            float segDur = useRecordedTiming
                ? Mathf.Max(0.0001f, b.t - a.t)
                : Mathf.Max(0.0001f, (b.pos - a.pos).magnitude / Mathf.Max(0.0001f, speedScale));

            float t = 0f;
            while (t < segDur)
            {
                float u = t / segDur;
                tMove.position = Vector3.Lerp(a.pos, b.pos, u);
                if (includeRotation) tMove.rotation = Quaternion.Slerp(a.rot, b.rot, u);
                t += Time.deltaTime;
                yield return null;
            }
            tMove.position = b.pos;
            if (includeRotation) tMove.rotation = b.rot;
        }

        Debug.Log("[PathRP] Replay DONE");
        _replayCo = null;

        // ▶ UI: Replay 버튼 원복
        SetReplayVisual(active: false);
    }

    // ==== Save ====
    public void OnSave()
    {
        if (_samples.Count < 2) { Debug.LogWarning("[PathRP] No path to save."); return; }

        string dir = Path.Combine(Application.dataPath, "Paths"); // 사용자가 바꿨던 위치 유지
        Directory.CreateDirectory(dir);
        string stamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        string path = Path.Combine(dir, $"{fileNamePrefix}_{stamp}.csv");

        try
        {
            using var sw = new StreamWriter(path);
            var ci = CultureInfo.InvariantCulture;

            if (includeRotation)
                sw.WriteLine("Index;T_s;X;Y;Z;Qx;Qy;Qz;Qw;Unit_Pos");
            else
                sw.WriteLine("Index;T_s;X;Y;Z;Unit_Pos");

            for (int i = 0; i < _samples.Count; i++)
            {
                var s = _samples[i];
                Vector3 p = s.pos; string unit = "m";
                if (exportMillimeters) { p *= 1000f; unit = "mm"; }

                if (includeRotation)
                {
                    var q = s.rot;
                    sw.WriteLine(string.Format(ci,
                        "{0};{1:F4};{2:F6};{3:F6};{4:F6};{5:F6};{6:F6};{7:F6};{8:F6};{9}",
                        i, s.t, p.x, p.y, p.z, q.x, q.y, q.z, q.w, unit));
                }
                else
                {
                    sw.WriteLine(string.Format(ci,
                        "{0};{1:F4};{2:F6};{3:F6};{4:F6};{5}",
                        i, s.t, p.x, p.y, p.z, unit));
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError($"[PathRP] CSV save failed: {ex.Message}");
            return;
        }

        Debug.Log($"[PathRP] CSV saved.");
        UpdateIndicator();
    }

    // ==== utils ====
    void AppendSample(Vector3 worldPos, Quaternion worldRot, float tSec)
    {
        bool addForLine = true;
        if (_line.positionCount > 0 && minPointDistance > 0f)
        {
            Vector3 last = _line.GetPosition(_line.positionCount - 1);
            addForLine = (worldPos - last).sqrMagnitude >= (minPointDistance * minPointDistance);
        }

        _samples.Add(new Sample { t = tSec, pos = worldPos, rot = worldRot });

        if (addForLine)
        {
            _line.positionCount++;
            _line.SetPosition(_line.positionCount - 1, worldPos);
        }
    }

    void UpdateIndicator()
    {
        if (savedIndicator == null) return;

        bool hasPath = _samples.Count >= 2;

        // 항상 보이게
        savedIndicator.enabled = true;

        // 경로 있으면 초록, 없으면 흰색
        savedIndicator.color = hasPath
            ? savedIndicatorRecordedColor
            : savedIndicatorIdleColor;
    }


    public void ClearPath()
    {
        if (_isRecording) { Debug.LogWarning("[PathRP] Can't clear while recording."); return; }
        _samples.Clear();
        _line.positionCount = 0;
        _t = 0f;
        UpdateIndicator();
        Debug.Log("[PathRP] Path cleared.");
    }

    // ---- UI 헬퍼 ----
    void SetRecordVisual(bool active)
    {
        if (recordButton != null && recordButton.image != null)
            recordButton.image.color = active ? activeButtonColor : _recordDefaultColor;

        if (recordLabel != null)
            recordLabel.text = active ? recordActiveText : _recordIdleText;
    }

    void SetReplayVisual(bool active)
    {
        if (replayButton != null && replayButton.image != null)
            replayButton.image.color = active ? activeButtonColor : _replayDefaultColor;

        if (replayLabel != null)
            replayLabel.text = active ? replayActiveText : _replayIdleText;
    }
}
