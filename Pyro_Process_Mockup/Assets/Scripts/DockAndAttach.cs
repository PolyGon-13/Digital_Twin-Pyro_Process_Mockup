using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class DockAndAttach : MonoBehaviour
{
    [Header("GantryIKArticulation 공유")]
    GantryIKArticulation gantry;
    Transform Target;
    Transform ToolSocket;
    ArticulationBody yJoint;

    // 통합 속도 사용: gantry.SpeedXZ 로 매핑 (구버전 호환 목적의 래퍼)
    float moveSpeed => gantry ? gantry.SpeedXZ : 0.5f;

    [Header("Object")]
    public MonoBehaviour TargetController;
    //public GantryIKArticulation[] ikToPause;

    [Header("Heavy Gripper")]
    public GameObject heavy_gripper;
    public GameObject heavy_gripper_Station;
    public Transform heavy_gripper_DockPoint;
    Renderer[] heavy_gripper_Renderers;
    Collider[] heavy_gripper_Colliders;
    Behaviour[] heavy_gripper_Behaviours;
    GameObject[] heavy_gripper_Objects;

    [Header("Heavy Gripper (Fake)")]
    public GameObject heavy_gripper_fake;
    Renderer[] heavy_gripper_Renderers_fake;
    Collider[] heavy_gripper_Colliders_fake;
    Behaviour[] heavy_gripper_Behaviours_fake;
    GameObject[] heavy_gripper_Objects_fake;
    Vector3 heavy_gripper_homePos_fake; // 초기위치
    //Quaternion heavy_gripper_fake_homeRot; // 초기회전

    [Header("Angular Gripper")]
    public GameObject angular_gripper;
    public GameObject angular_gripper_Station;
    public Transform angular_gripper_DockPoint;
    Renderer[] angular_gripper_Renderers;
    Collider[] angular_gripper_Colliders;
    Behaviour[] angular_gripper_Behaviours;
    GameObject[] angular_gripper_Objects;

    [Header("Angular Gripper (Fake)")]
    public GameObject angular_gripper_fake;
    Renderer[] angular_gripper_Renderers_fake;
    Collider[] angular_gripper_Colliders_fake;
    Behaviour[] angular_gripper_Behaviours_fake;
    GameObject[] angular_gripper_Objects_fake;
    Vector3 angular_gripper_homePos_fake;

    string[] scriptNameKeywords = new[] { "Co ACD" };
    string changeLayerName = "NoCollision"; // 도킹 중 사용할 임시 레이어

    [Header("Status")]
    public bool busy; // 시퀀스 진행여부
    public bool Hg_Attached = false; // 그리퍼 결합여부
    public bool Ag_Attached = false;

    [Header("Move Params")]
    [HideInInspector] public float moveY_Up_Speed = 0.3f;   // (미사용: 통합 속도 gantry.SpeedYUp 사용)
    [HideInInspector] public float moveY_Down_Speed = 0.3f; // (미사용: 통합 속도 gantry.SpeedYDown 사용)
    [HideInInspector] public float Pos_Eps = 0.001f;
    //[HideInInspector] public float Ang_Eps = 0.5f;
    [HideInInspector] public float delayBeforeMove = 0.2f;
    float moveY_Offset = 1f;

    [Header("KeyBoard")]
    KeyCode Dock_HG_Key = KeyCode.Z;
    KeyCode Dock_AG_Key = KeyCode.X;

    bool _lockEeTarget;

    // 레이어 원상 복구를 위한 저장소
    readonly Dictionary<GameObject, List<(Transform t, int originalLayer)>> _savedLayersMap = new Dictionary<GameObject, List<(Transform, int)>>();

    // 읽기 전용으로 공유
    public bool Is_Hg_Attached => Hg_Attached;
    public bool Is_Ag_Attached => Ag_Attached;
    public bool IsBusy => busy;
    public float DelayBeforeMove => delayBeforeMove;

    void Awake()
    {
        gantry = GetComponentInParent<GantryIKArticulation>();

        Target = gantry ? gantry.Target : null;
        ToolSocket = gantry ? gantry.toolSocket : null;
        yJoint = gantry ? gantry.yJoint_2 : null;

        Application.runInBackground = true;

        GetGripperInfo();
    }

    void Start()
    {
        if (!Target || !ToolSocket || !TargetController || !yJoint)
            Debug.LogError("[Dock] 필수 참조 누락.");
        if (!heavy_gripper || !heavy_gripper_fake || !heavy_gripper_DockPoint)
            Debug.LogError("[Dock] heavy gripper 필수 참조 누락.");
        if (!angular_gripper || !angular_gripper_fake || !angular_gripper_DockPoint)
            Debug.LogError("[Dock] angular gripper 필수 참조 누락.");

        SetVisible(heavy_gripper, heavy_gripper_Renderers, heavy_gripper_Colliders, heavy_gripper_Behaviours, heavy_gripper_Objects, false);
        SetVisible(angular_gripper, angular_gripper_Renderers, angular_gripper_Colliders, angular_gripper_Behaviours, angular_gripper_Objects, false);

        SetVisible(heavy_gripper_fake, heavy_gripper_Renderers_fake, heavy_gripper_Colliders_fake, heavy_gripper_Behaviours_fake, heavy_gripper_Objects_fake, true);
        SetVisible(angular_gripper_fake, angular_gripper_Renderers_fake, angular_gripper_Colliders_fake, angular_gripper_Behaviours_fake, angular_gripper_Objects_fake, true);
    }

    void Update()
    {
        if (!busy && !Hg_Attached && Input.GetKeyDown(Dock_HG_Key))
        {
            StopAllCoroutines();
            StartCoroutine(Dock_Heavy_Gripper());
        }
        if (!busy && !Ag_Attached && Input.GetKeyDown(Dock_AG_Key))
        {
            StopAllCoroutines();
            StartCoroutine(Dock_Angular_Gripper());
        }
    }

    // 매 프레임마다 Update 이후에 실행
    void LateUpdate()
    {
        // 스왑 프레임 안정화를 위해 Target 위치/회전 보정
        if (_lockEeTarget && Target && ToolSocket)
        {
            Target.position = ToolSocket.position;
            Target.rotation = ToolSocket.rotation;
        }
    }

    // Heavy Gripper 결합 (외부 접근용 함수)
    public IEnumerator Start_Dock_Heavy_Gripper()
    {
        if (busy || Hg_Attached) yield break;

        StopAllCoroutines();
        yield return StartCoroutine(Dock_Heavy_Gripper());
    }

    // Heavy Gripper 결합
    IEnumerator Dock_Heavy_Gripper()
    {
        if (!heavy_gripper || !heavy_gripper_fake) yield break;

        SetBusy(true);
        SetInputEnabled(false);

        try
        {
            if (Ag_Attached)
            {
                yield return UnDock_Angular_Gripper(true);
                yield return new WaitForSeconds(delayBeforeMove);
            }

            // 도킹 목표 위치 저장
            Vector3 t = heavy_gripper_DockPoint.position;

            // XZ 이동 (통합 속도)
            yield return Move.MoveXZ(transform, () => moveSpeed, new Vector3(t.x, transform.position.y, t.z), Pos_Eps);
            yield return new WaitForSeconds(delayBeforeMove);

            // Fake Layer 변경
            ChangeLayers(heavy_gripper_fake, changeLayerName);
            ChangeLayers(heavy_gripper_Station, changeLayerName);

            // Y 아래로 이동 (통합 속도)
            yield return Move.MoveY_Down(transform, t.y, () => gantry.SpeedYDown, Pos_Eps + 0.09f);
            yield return new WaitForSeconds(delayBeforeMove + 1.8f);

            _lockEeTarget = true;

            // 바꿔치기
            SetVisible(heavy_gripper_fake, heavy_gripper_Renderers_fake, heavy_gripper_Colliders_fake, heavy_gripper_Behaviours_fake, heavy_gripper_Objects_fake, false);
            SetVisible(heavy_gripper, heavy_gripper_Renderers, heavy_gripper_Colliders, heavy_gripper_Behaviours, heavy_gripper_Objects, true);

            Hg_Attached = true;

            yield return new WaitForSeconds(delayBeforeMove);
            gantry.ReApply_Jaw_Setup(1);
            _lockEeTarget = false;

            // Y 위로 이동 (통합 속도)
            yield return Move.MoveY_Up(transform, yJoint, () => gantry.SpeedYUp, Pos_Eps);
            yield return new WaitForSeconds(delayBeforeMove);
        }
        finally
        {
            RestoreLayers(heavy_gripper_fake);
            RestoreLayers(heavy_gripper_Station);

            SetInputEnabled(true);
            SetBusy(false);
        }
    }

    // Heavy Gripper 분리
    IEnumerator Undock_Heavy_Gripper(bool isCall = false)
    {
        if (!Hg_Attached || !yJoint) yield break;

        if (!isCall)
        {
            SetBusy(true);
            SetInputEnabled(false);
        }

        try
        {
            // Y 위로 이동 (통합 속도)
            yield return Move.MoveY_Up(transform, yJoint, () => gantry.SpeedYUp, Pos_Eps);
            yield return new WaitForSeconds(delayBeforeMove);

            // XZ 이동 (통합 속도)
            yield return Move.MoveXZ(transform, () => moveSpeed, new Vector3(heavy_gripper_homePos_fake.x, transform.position.y, heavy_gripper_homePos_fake.z), Pos_Eps);
            yield return new WaitForSeconds(delayBeforeMove);

            // Y 아래로 이동 (통합 속도)
            yield return Move.MoveY_Down(transform, heavy_gripper_homePos_fake.y, () => gantry.SpeedYDown, Pos_Eps);
            yield return new WaitForSeconds(delayBeforeMove);

            // heavy_gripper_fake를 gripper의 현재 위치/회전과 동일하게 설정
            heavy_gripper_fake.transform.SetPositionAndRotation(heavy_gripper.transform.position, heavy_gripper.transform.rotation);

            // 바꿔치기
            SetVisible(heavy_gripper, heavy_gripper_Renderers, heavy_gripper_Colliders, heavy_gripper_Behaviours, heavy_gripper_Objects, false);
            SetVisible(heavy_gripper_fake, heavy_gripper_Renderers_fake, heavy_gripper_Colliders_fake, heavy_gripper_Behaviours_fake, heavy_gripper_Objects_fake, true);

            Hg_Attached = false;

            // Y 위로 이동 (통합 속도)
            yield return Move.MoveY_Up(transform, yJoint, () => gantry.SpeedYUp, Pos_Eps);
            yield return new WaitForSeconds(delayBeforeMove);
        }
        finally
        {
            if (!isCall)
            {
                SetInputEnabled(true);
                SetBusy(false);
            }
        }
    }

    // Heavy Gripper 분리 + Angular Gripper 결합 (외부 접근용 함수)
    public IEnumerator Start_Dock_Angular_Gripper()
    {
        if (busy || Ag_Attached) yield break;

        StopAllCoroutines();
        yield return StartCoroutine(Dock_Angular_Gripper());
    }

    // Heavy Gripper 분리 + Angular Gripper 장착
    IEnumerator Dock_Angular_Gripper()
    {
        if (!angular_gripper || !angular_gripper_fake) yield break;

        SetBusy(true);
        SetInputEnabled(false);

        try
        {
            // Heavy Gripper가 결합되어 있으면 해제
            if (Hg_Attached)
            {
                yield return Undock_Heavy_Gripper(true);
                yield return new WaitForSeconds(delayBeforeMove);
            }

            Vector3 a = angular_gripper_DockPoint.position;

            // XZ 이동 (통합 속도)
            yield return Move.MoveXZ(transform, () => moveSpeed, new Vector3(a.x, transform.position.y, a.z), Pos_Eps);
            yield return new WaitForSeconds(delayBeforeMove);

            ChangeLayers(angular_gripper_fake, changeLayerName);
            ChangeLayers(angular_gripper_Station, changeLayerName);

            // Y 아래로 이동 (통합 속도)
            yield return Move.MoveY_Down(transform, a.y, () => gantry.SpeedYDown, Pos_Eps + 0.09f);
            yield return new WaitForSeconds(delayBeforeMove + 1.8f);

            _lockEeTarget = true;

            SetVisible(angular_gripper_fake, angular_gripper_Renderers_fake, angular_gripper_Colliders_fake, angular_gripper_Behaviours_fake, angular_gripper_Objects_fake, false);
            SetVisible(angular_gripper, angular_gripper_Renderers, angular_gripper_Colliders, angular_gripper_Behaviours, angular_gripper_Objects, true);

            Ag_Attached = true;

            yield return new WaitForSeconds(delayBeforeMove);
            gantry.ReApply_Jaw_Setup(2);
            _lockEeTarget = false;

            // Y 위로 이동 (통합 속도)
            yield return Move.MoveY_Up(transform, yJoint, () => gantry.SpeedYUp, Pos_Eps);
            yield return new WaitForSeconds(delayBeforeMove);
        }
        finally
        {
            RestoreLayers(angular_gripper_fake);
            RestoreLayers(angular_gripper_Station);

            SetInputEnabled(true);
            SetBusy(false);
        }
    }

    // Angular Gripper 해제
    IEnumerator UnDock_Angular_Gripper(bool isCall = false)
    {
        if (!Ag_Attached || !yJoint) yield break;

        if (!isCall)
        {
            SetBusy(true);
            SetInputEnabled(false);
        }

        try
        {
            // Y 위로 이동 (통합 속도)
            yield return Move.MoveY_Up(transform, yJoint, () => gantry.SpeedYUp, Pos_Eps);
            yield return new WaitForSeconds(delayBeforeMove);

            // XZ 이동 (통합 속도)
            yield return Move.MoveXZ(transform, () => moveSpeed, new Vector3(angular_gripper_homePos_fake.x, transform.position.y, angular_gripper_homePos_fake.z), Pos_Eps);
            yield return new WaitForSeconds(delayBeforeMove);

            // Y 아래로 이동 (통합 속도)
            yield return Move.MoveY_Down(transform, angular_gripper_homePos_fake.y, () => gantry.SpeedYDown, Pos_Eps);
            yield return new WaitForSeconds(delayBeforeMove);

            // angular_gripper_fake를 gripper의 현재 위치/회전과 동일하게 설정
            angular_gripper_fake.transform.SetPositionAndRotation(angular_gripper.transform.position, angular_gripper.transform.rotation);

            // 바꿔치기
            SetVisible(angular_gripper, angular_gripper_Renderers, angular_gripper_Colliders, angular_gripper_Behaviours, angular_gripper_Objects, false);
            SetVisible(angular_gripper_fake, angular_gripper_Renderers_fake, angular_gripper_Colliders_fake, angular_gripper_Behaviours_fake, angular_gripper_Objects_fake, true);

            Ag_Attached = false;

            // Y 위로 이동 (통합 속도)
            yield return Move.MoveY_Up(transform, yJoint, () => gantry.SpeedYUp, Pos_Eps);
            yield return new WaitForSeconds(delayBeforeMove);
        }
        finally
        {
            if (!isCall)
            {
                SetInputEnabled(true);
                SetBusy(false);
            }
        }
    }

    // 그리퍼 정보 가져오기 (Renderer, Collider, Co CAD)
    void GetGripperInfo()
    {
        // Heavy Gripper의 자식들의 Renderer와 Collider 정보 가져옴
        if (heavy_gripper && (heavy_gripper_Renderers == null || heavy_gripper_Renderers.Length == 0))
            heavy_gripper_Renderers = heavy_gripper.GetComponentsInChildren<Renderer>(true); // true면 비활성 포함
        if (heavy_gripper && (heavy_gripper_Colliders == null || heavy_gripper_Colliders.Length == 0))
            heavy_gripper_Colliders = heavy_gripper.GetComponentsInChildren<Collider>(true);

        // Heavy Gripper fake의 자식들의 Renderer와 Collider 정보 가져옴
        if (heavy_gripper_fake && (heavy_gripper_Renderers_fake == null || heavy_gripper_Renderers_fake.Length == 0))
            heavy_gripper_Renderers_fake = heavy_gripper_fake.GetComponentsInChildren<Renderer>(true);
        if (heavy_gripper_fake && (heavy_gripper_Colliders_fake == null || heavy_gripper_Colliders_fake.Length == 0))
            heavy_gripper_Colliders_fake = heavy_gripper_fake.GetComponentsInChildren<Collider>(true);

        // Angular Gripper의 자식들의 Renderer와 Collider 정보 가져옴
        if (angular_gripper && (angular_gripper_Renderers == null || angular_gripper_Renderers.Length == 0))
            angular_gripper_Renderers = angular_gripper.GetComponentsInChildren<Renderer>(true);
        if (angular_gripper && (angular_gripper_Colliders == null || angular_gripper_Colliders.Length == 0))
            angular_gripper_Colliders = angular_gripper.GetComponentsInChildren<Collider>(true);

        // Angular Gripper fake의 자식들의 Renderer와 Collider 정보 가져옴
        if (angular_gripper_fake && (angular_gripper_Renderers_fake == null || angular_gripper_Renderers_fake.Length == 0))
            angular_gripper_Renderers_fake = angular_gripper_fake.GetComponentsInChildren<Renderer>(true);
        if (angular_gripper_fake && (angular_gripper_Colliders_fake == null || angular_gripper_Colliders_fake.Length == 0))
            angular_gripper_Colliders_fake = angular_gripper_fake.GetComponentsInChildren<Collider>(true);

        // Heavy Gripper Fake 그리퍼 초기 위치 (다시 돌려놓을 때 사용)
        if (heavy_gripper_fake)
        {
            heavy_gripper_homePos_fake = heavy_gripper_fake.transform.position;
            //_homeRot = gripper_fake.transform.rotation;
        }

        // Angular Gripper Fake 그리퍼 초기 위치
        if (angular_gripper_fake)
        {
            angular_gripper_homePos_fake = angular_gripper_fake.transform.position;
        }

        // Heavy Gripper의 하위 Behaviour들을 저장 (코드가 저장됨 -> Co ACD)
        if (heavy_gripper)
        {
            if (heavy_gripper_Behaviours == null || heavy_gripper_Behaviours.Length == 0)
                heavy_gripper_Behaviours = AutoCollectBehavioursByName(heavy_gripper, scriptNameKeywords);
            if (heavy_gripper_Objects == null || heavy_gripper_Objects.Length == 0)
                heavy_gripper_Objects = AutoCollectGameObjectsByName(heavy_gripper, scriptNameKeywords);
        }
        if (heavy_gripper_fake)
        {
            if (heavy_gripper_Behaviours_fake == null || heavy_gripper_Behaviours_fake.Length == 0)
                heavy_gripper_Behaviours_fake = AutoCollectBehavioursByName(heavy_gripper_fake, scriptNameKeywords);
            if (heavy_gripper_Objects_fake == null || heavy_gripper_Objects_fake.Length == 0)
                heavy_gripper_Objects_fake = AutoCollectGameObjectsByName(heavy_gripper_fake, scriptNameKeywords);
        }

        // Angular Gripper의 하위 Behaviour들을 저장
        if (angular_gripper)
        {
            if (angular_gripper_Behaviours == null || angular_gripper_Behaviours.Length == 0)
                angular_gripper_Behaviours = AutoCollectBehavioursByName(angular_gripper, scriptNameKeywords);
            if (angular_gripper_Objects == null || angular_gripper_Objects.Length == 0)
                angular_gripper_Objects = AutoCollectGameObjectsByName(angular_gripper, scriptNameKeywords);
        }
        if (angular_gripper_fake)
        {
            if (angular_gripper_Behaviours_fake == null || angular_gripper_Behaviours_fake.Length == 0)
                angular_gripper_Behaviours_fake = AutoCollectBehavioursByName(angular_gripper_fake, scriptNameKeywords);
            if (angular_gripper_Objects_fake == null || angular_gripper_Objects_fake.Length == 0)
                angular_gripper_Objects_fake = AutoCollectGameObjectsByName(angular_gripper_fake, scriptNameKeywords);
        }
    }

    // Renderer, Collider 활성화/비활성화
    void SetVisible(GameObject root, Renderer[] renderers, Collider[] colliders, Behaviour[] extraBehaviours, GameObject[] extraObjects, bool visible)
    {
        if (!root) return;

        // Renderer
        if (renderers != null)
        {
            for (int i = 0; i < renderers.Length; ++i)
                if (renderers[i]) renderers[i].enabled = visible;
        }

        // Collider
        if (colliders != null)
        {
            for (int i = 0; i < colliders.Length; ++i)
                if (colliders[i]) colliders[i].enabled = visible;
        }

        // Co ACD
        if (extraBehaviours != null)
        {
            foreach (var b in extraBehaviours)
                if (b) b.enabled = visible;
        }
        if (extraObjects != null)
        {
            foreach (var go in extraObjects)
                if (go) go.SetActive(visible);
        }
    }

    // 레이어 변경
    void ChangeLayers(GameObject root, string toLayerName)
    {
        if (!root) return;

        int layer = LayerMask.NameToLayer(toLayerName);
        if (layer < 0)
        {
            Debug.LogWarning($"[LayerSwap] Layer '{toLayerName}' not found. Skip.");
            return;
        }

        if (_savedLayersMap.TryGetValue(root, out var exists) && exists.Count > 0)
        {
            Debug.LogWarning("[LayerSwap] ChangeLayers was already called for this root. Auto-restore then re-apply.");
            RestoreLayers(root);
        }

        var list = new System.Collections.Generic.List<(Transform, int)>();
        _savedLayersMap[root] = list;

        var trs = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < trs.Length; ++i)
        {
            var tr = trs[i];
            if (!tr) continue;

            list.Add((tr, tr.gameObject.layer));
            tr.gameObject.layer = layer;
        }
    }

    // 기존 레이어로 복구
    void RestoreLayers(GameObject root)
    {
        if (!root) return;

        if (!_savedLayersMap.TryGetValue(root, out var list) || list == null || list.Count == 0)
            return;

        for (int i = 0; i < list.Count; ++i)
        {
            var (t, original) = list[i];
            if (t) t.gameObject.layer = original;
        }

        list.Clear();
        _savedLayersMap.Remove(root);
    }

    // 원하는 이름의 Behaviors 컴포넌트를 찾기
    Behaviour[] AutoCollectBehavioursByName(GameObject root, string[] keywords)
    {
        var all = root.GetComponentsInChildren<Behaviour>(true);
        var list = new List<Behaviour>();
        foreach (var b in all)
        {
            if (!b) continue;
            string typeName = b.GetType().Name;
            if (MatchesAny(typeName, keywords)) list.Add(b);
        }
        return list.ToArray();
    }

    // 원하는 이름의 GameObject 찾기
    GameObject[] AutoCollectGameObjectsByName(GameObject root, string[] keywords)
    {
        var all = root.GetComponentsInChildren<Transform>(true);
        var list = new List<GameObject>();
        foreach (var t in all)
        {
            if (!t) continue;
            if (MatchesAny(t.name, keywords)) list.Add(t.gameObject);
        }
        return list.ToArray();
    }

    // text에 keywords라는 문자열이 있는지 확인
    bool MatchesAny(string text, string[] keywords)
    {
        if (string.IsNullOrEmpty(text) || keywords == null) return false;

        for (int k = 0; k < keywords.Length; ++k)
        {
            var key = keywords[k];
            if (string.IsNullOrEmpty(key)) continue;
            if (text.IndexOf(key, System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
    }

    // MonoBehavior 컴포넌트 비활성화
    public void SetInputEnabled(bool on)
    {
        if (TargetController) TargetController.enabled = on;
    }

    // busy 변수 상태 변경 (외부 접근용 함수)
    public void SetBusy(bool value)
    {
        busy = value;
    }
}
