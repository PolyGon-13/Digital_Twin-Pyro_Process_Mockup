using UnityEngine;
using System.Collections.Generic;

[DisallowMultipleComponent]
public class GantryBlockDetector : MonoBehaviour
{
    [Header("Target Objects (Colliders to Watch)")]
    [Tooltip("이 오브젝트들과 그 자식들의 Collider를 전부 감시함")]
    public List<GameObject> targetRoots = new List<GameObject>();

    [Header("Blocked When Touching These Layers")]
    [Tooltip("여러 레이어를 지정할 수 있음 — 어느 하나라도 닿으면 Blocked = true")]
    public List<LayerMask> blockedLayerGroups = new List<LayerMask>();

    [Header("Debounce / Hysteresis (sec)")]
    public float engageDelay = 0.03f;
    public float releaseDelay = 0.05f;

    [Header("Tint")]
    public Renderer[] tintRenderers;
    public Color normalColor = Color.white;
    public Color blockedColor = Color.red;

    [Header("Tint Advanced")]
    [Tooltip("PropertyBlock이 안 먹는 경우 강제로 머티리얼 인스턴스를 건드림(성능 약간 손해)")]
    public bool useMaterialInstance = false;   // 기본 false 권장
    [Tooltip("강제로 쓸 컬러 프로퍼티명(비워두면 자동으로 _BaseColor/_Color 탐색)")]
    public string overrideColorProperty = "";  // 선택

    public bool IsBlocked { get; private set; }

    // 내부 상태
    private readonly HashSet<Collider> _monitoredColliders = new HashSet<Collider>();
    private readonly HashSet<Collider> _blockedContacts = new HashSet<Collider>();
    private float _engageTimer;
    private float _releaseTimer;
    private MaterialPropertyBlock _mpb;
    private Dictionary<Renderer, Material[]> _originalMats;

    void Awake()
    {
        RegisterAllTargetColliders();
        _mpb = new MaterialPropertyBlock();
        _originalMats = new Dictionary<Renderer, Material[]>();
        ApplyTint(false);
    }

    void RegisterAllTargetColliders()
    {
        _monitoredColliders.Clear();

        foreach (var root in targetRoots)
        {
            if (root == null) continue;
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            foreach (var c in colliders)
            {
                if (!_monitoredColliders.Contains(c))
                    _monitoredColliders.Add(c);
            }
        }

        // 프록시 자동 부착
        foreach (var c in _monitoredColliders)
        {
            if (c == null) continue;
            var proxy = c.gameObject.GetComponent<GantryBlockProxy>();
            if (!proxy)
                proxy = c.gameObject.AddComponent<GantryBlockProxy>();
            proxy.Init(this);
        }
    }

    void FixedUpdate()
    {
        float dt = Time.fixedDeltaTime;
        bool hasContact = _blockedContacts.Count > 0;

        if (hasContact)
        {
            _releaseTimer = 0f;
            _engageTimer += dt;
            if (!IsBlocked && _engageTimer >= engageDelay)
                SetBlocked(true);
        }
        else
        {
            _engageTimer = 0f;
            _releaseTimer += dt;
            if (IsBlocked && _releaseTimer >= releaseDelay)
                SetBlocked(false);
        }
    }

    void SetBlocked(bool blocked)
    {
        IsBlocked = blocked;
        ApplyTint(blocked);
    }

    // ---------- 색상 처리 ----------
    void ApplyTint(bool blocked)
    {
        if (tintRenderers == null) return;
        Color c = blocked ? blockedColor : normalColor;

        foreach (var r in tintRenderers)
        {
            SetRendererColor(r, c);
        }
    }

    string ResolveColorProperty(Renderer r)
    {
        if (!string.IsNullOrEmpty(overrideColorProperty))
            return overrideColorProperty;

        var mats = r.sharedMaterials;
        for (int i = 0; i < mats.Length; i++)
        {
            var m = mats[i];
            if (!m) continue;
            if (m.HasProperty("_BaseColor")) return "_BaseColor";
            if (m.HasProperty("_Color")) return "_Color";
        }
        return null;
    }

    void SetRendererColor(Renderer r, Color c)
    {
        if (!r) return;

        // 1) MaterialPropertyBlock 시도
        if (!useMaterialInstance)
        {
            if (_mpb == null) _mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(_mpb);

            string prop = ResolveColorProperty(r);
            if (!string.IsNullOrEmpty(prop))
            {
                _mpb.SetColor(prop, c);
                _mpb.SetColor("_BaseColor", c);
                _mpb.SetColor("_Color", c);
                r.SetPropertyBlock(_mpb);
                return;
            }
            else
            {
                _mpb.SetColor("_EmissionColor", c);
                r.SetPropertyBlock(_mpb);
                r.sharedMaterial?.EnableKeyword("_EMISSION");
                return;
            }
        }

        // 2) PropertyBlock이 안 먹는 경우 직접 머티리얼 수정
        if (!_originalMats.ContainsKey(r))
        {
            _originalMats[r] = r.sharedMaterials;
            var inst = r.materials;
            r.materials = inst;
        }

        var matsInst = r.materials;
        for (int i = 0; i < matsInst.Length; i++)
        {
            var m = matsInst[i];
            if (!m) continue;

            string prop = ResolveColorProperty(r);
            if (!string.IsNullOrEmpty(prop))
            {
                m.SetColor(prop, c);
                if (prop != "_BaseColor") m.SetColor("_BaseColor", c);
                if (prop != "_Color") m.SetColor("_Color", c);
            }
            else
            {
                m.SetColor("_EmissionColor", c);
                m.EnableKeyword("_EMISSION");
            }
        }
    }

    void OnDisable()
    {
        if (!useMaterialInstance || _originalMats == null) return;
        foreach (var kv in _originalMats)
        {
            var r = kv.Key;
            if (!r) continue;
            r.sharedMaterials = kv.Value;
        }
        _originalMats.Clear();
    }

    // ---------- 프록시에서 호출 ----------
    public void OnProxyContactEnter(Collider other)
    {
        if (IsBlockedLayer(other))
            _blockedContacts.Add(other);
    }

    public void OnProxyContactExit(Collider other)
    {
        _blockedContacts.Remove(other);
    }

    bool IsBlockedLayer(Collider c)
    {
        int otherLayer = 1 << c.gameObject.layer;
        foreach (var mask in blockedLayerGroups)
        {
            if ((mask.value & otherLayer) != 0)
                return true;
        }
        return false;
    }
}


// =================== 콜백 전달용 프록시 ===================
public class GantryBlockProxy : MonoBehaviour
{
    private GantryBlockDetector _parent;

    public void Init(GantryBlockDetector parent)
    {
        _parent = parent;
    }

    void OnCollisionEnter(Collision other)
    {
        _parent?.OnProxyContactEnter(other.collider);
    }

    void OnCollisionExit(Collision other)
    {
        _parent?.OnProxyContactExit(other.collider);
    }

    void OnTriggerEnter(Collider other)
    {
        _parent?.OnProxyContactEnter(other);
    }

    void OnTriggerExit(Collider other)
    {
        _parent?.OnProxyContactExit(other);
    }
}
