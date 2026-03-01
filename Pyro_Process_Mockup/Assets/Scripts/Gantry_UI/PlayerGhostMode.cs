using UnityEngine;
using UnityEngine.UI;
using Unity.XR.CoreUtils; // XROrigin

public class PlayerGhostMode : MonoBehaviour
{
    [Header("Target")]
    [SerializeField] private GameObject xrOriginRig; // XR Origin Hands (XR Rig)
    private CharacterController cc;

    [Header("Layers")]
    [SerializeField] private string normalLayerName = "Player";
    [SerializeField] private string ghostLayerName = "PlayerGhost";

    [Header("UI")]
    [SerializeField] private Button toggleButton;
    [SerializeField] private Image buttonImage;
    [SerializeField] private Color onColor = new Color(0f / 255f, 255f / 255f, 0f / 255f); // 초록
    [SerializeField] private Color offColor = Color.white;                             // 하양

    [SerializeField, Tooltip("현재 상태 표시(읽기전용)")]
    private bool isNoClip = false;

    private int normalLayer;
    private int ghostLayer;

    private void Awake()
    {
        // 레퍼런스가 비어있으면 안전하게 한 번만 검색 (신 API)
        if (xrOriginRig == null)
        {
            var origin = Object.FindFirstObjectByType<XROrigin>();   // 또는 FindAnyObjectByType<XROrigin>()
            if (origin != null) xrOriginRig = origin.gameObject;
        }

        if (xrOriginRig != null)
            cc = xrOriginRig.GetComponent<CharacterController>();

        normalLayer = LayerMask.NameToLayer(normalLayerName);
        ghostLayer = LayerMask.NameToLayer(ghostLayerName);

        if (toggleButton != null)
            toggleButton.onClick.AddListener(ToggleNoClip);
    }

    private void Start()
    {
        ApplyLayer();
        ApplyVisual();
    }

    public void ToggleNoClip()
    {
        isNoClip = !isNoClip;
        ApplyLayer();
        ApplyVisual();
    }

    private void ApplyLayer()
    {
        if (cc == null) return;
        cc.gameObject.layer = isNoClip ? ghostLayer : normalLayer;
    }

    private void ApplyVisual()
    {
        if (buttonImage != null)
            buttonImage.color = isNoClip ? onColor : offColor;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        // 에디터에서 드래그만 해도 버튼/이미지 연결되면 미리 색 업데이트
        if (Application.isPlaying == false && buttonImage != null)
            buttonImage.color = isNoClip ? onColor : offColor;
    }
#endif
}
