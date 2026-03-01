using UnityEngine;
using UnityEngine.UI;
using System.Collections;

// 패널의 “H.Grip / A.Grip” 버튼 토글 + 불(램프) 표시 제어
// - H.Grip: Dock ↔ Undock Heavy Gripper
// - A.Grip: Dock ↔ Undock Angular Gripper
// - Dock 수행 중(busy)에는 버튼을 잠시 비활성화
public class GripperToggleUI : MonoBehaviour
{
    [Header("References")]
    public DockAndAttach dock;          // 씬의 DockAndAttach 컴포넌트 (필수)
    public Button hGripButton;          // H.Grip 버튼
    public Button aGripButton;          // A.Grip 버튼
    public Image hGripLight;            // H.Grip 상태 램프(버튼의 Image나 별도 아이콘)
    public Image aGripLight;            // A.Grip 상태 램프

    [Header("Colors")]
    public Color onColor = new Color(0.2f, 0.8f, 0.2f, 1f);
    public Color offColor = new Color(0.5f, 0.5f, 0.5f, 1f);
    public Color busyColor = new Color(1f, 0.8f, 0.2f, 1f); // 동작중 표시(선택)

    void Reset()
    {
        // 버튼의 그래픽을 램프로 자동 할당
        if (!hGripLight && hGripButton) hGripLight = hGripButton.targetGraphic as Image;
        if (!aGripLight && aGripButton) aGripLight = aGripButton.targetGraphic as Image;
    }

    void Update()
    {
        if (!dock) return;

        // 동작 중에는 버튼 잠금
        bool interactable = !dock.IsBusy;
        if (hGripButton) hGripButton.interactable = interactable;
        if (aGripButton) aGripButton.interactable = interactable;

        // 램프 업데이트
        UpdateLamp(hGripLight, dock.Is_Hg_Attached, interactable);
        UpdateLamp(aGripLight, dock.Is_Ag_Attached, interactable);
    }

    void UpdateLamp(Image lamp, bool attached, bool interactable)
    {
        if (!lamp) return;

        if (!interactable)
        {
            lamp.color = busyColor;  // busy면 별도색
            return;
        }

        lamp.color = attached ? onColor : offColor;
    }

    // ===== Button Events =====
    // H.Grip 버튼 OnClick
    public void OnToggleHeavyGrip()
    {
        if (!dock || dock.IsBusy) return;

        if (!dock.Is_Hg_Attached)
        {
            // Heavy 미장착 → Dock
            StartCoroutine(dock.Start_Dock_Heavy_Gripper());
        }
        else
        {
            // Heavy 장착됨 → Undock (※ bool 파라미터 전달!)
            dock.StartCoroutine("Undock_Heavy_Gripper", false);
        }
    }

    // A.Grip 버튼 OnClick
    public void OnToggleAngularGrip()
    {
        if (!dock || dock.IsBusy) return;

        if (!dock.Is_Ag_Attached)
        {
            // Angular 미장착 → Dock
            StartCoroutine(dock.Start_Dock_Angular_Gripper());
        }
        else
        {
            // Angular 장착됨 → Undock (※ bool 파라미터 전달!)
            dock.StartCoroutine("UnDock_Angular_Gripper", false);
        }
    }
}
