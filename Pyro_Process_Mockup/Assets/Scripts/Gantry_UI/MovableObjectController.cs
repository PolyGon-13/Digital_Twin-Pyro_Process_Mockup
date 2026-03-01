using System;
using System.Collections.Generic;
using UnityEngine;

public class MovableObjectController : MonoBehaviour
{
    [Serializable]
    public class MovableItem
    {
        [Tooltip("관리할 실제 오브젝트")]
        public Transform target;

        // 초기값 저장용
        [HideInInspector] public Transform originalParent;
        [HideInInspector] public int originalSiblingIndex;
        [HideInInspector] public Vector3 originalLocalPos;
        [HideInInspector] public Quaternion originalLocalRot;
        [HideInInspector] public Vector3 originalLocalScale;
        [HideInInspector] public int originalLayer;

        // Rigidbody 관련
        [HideInInspector] public bool hasRigidbody;
        [HideInInspector] public bool rb_useGravity;
        [HideInInspector] public bool rb_isKinematic;
        [HideInInspector] public RigidbodyConstraints rb_constraints;
        [HideInInspector] public RigidbodyInterpolation rb_interpolation;
        [HideInInspector] public CollisionDetectionMode rb_collisionMode;
    }

    [Header("여기에 관리할 오브젝트들을 추가하세요")]
    public List<MovableItem> movableObjects = new List<MovableItem>();

    private void Awake()
    {
        CaptureOriginalStates();
    }

    private void CaptureOriginalStates()
    {
        for (int i = 0; i < movableObjects.Count; i++)
        {
            MovableItem item = movableObjects[i];
            if (item == null || item.target == null)
                continue;

            Transform tr = item.target;

            item.originalParent = tr.parent;
            item.originalSiblingIndex = tr.GetSiblingIndex();

            item.originalLocalPos = tr.localPosition;
            item.originalLocalRot = tr.localRotation;
            item.originalLocalScale = tr.localScale;

            item.originalLayer = tr.gameObject.layer;

            Rigidbody rb = tr.GetComponent<Rigidbody>();
            if (rb != null)
            {
                item.hasRigidbody = true;
                item.rb_useGravity = rb.useGravity;
                item.rb_isKinematic = rb.isKinematic;
                item.rb_constraints = rb.constraints;
                item.rb_interpolation = rb.interpolation;
                item.rb_collisionMode = rb.collisionDetectionMode;
            }
            else
            {
                item.hasRigidbody = false;
            }
        }
    }

    public void ResetObjects()
    {
        for (int i = 0; i < movableObjects.Count; i++)
        {
            MovableItem item = movableObjects[i];
            if (item == null || item.target == null)
                continue;

            Transform tr = item.target;

            tr.SetParent(item.originalParent);

            if (item.originalParent != null)
            {
                tr.SetSiblingIndex(item.originalSiblingIndex);
            }

            tr.localPosition = item.originalLocalPos;
            tr.localRotation = item.originalLocalRot;
            tr.localScale = item.originalLocalScale;

            tr.gameObject.layer = item.originalLayer;

            Rigidbody rb = tr.GetComponent<Rigidbody>();
            if (rb != null && item.hasRigidbody)
            {
                rb.useGravity = item.rb_useGravity;
                rb.isKinematic = item.rb_isKinematic;
                rb.constraints = item.rb_constraints;
                rb.interpolation = item.rb_interpolation;
                rb.collisionDetectionMode = item.rb_collisionMode;

#if UNITY_6000_0_OR_NEWER
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
#else
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
#endif
            }
        }
    }

    public void FreezeAll()
    {
        for (int i = 0; i < movableObjects.Count; i++)
        {
            MovableItem item = movableObjects[i];
            if (item == null || item.target == null)
                continue;

            Rigidbody rb = item.target.GetComponent<Rigidbody>();
            if (rb != null)
            {
#if UNITY_6000_0_OR_NEWER
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
#else
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
#endif
                rb.constraints = RigidbodyConstraints.FreezePosition | RigidbodyConstraints.FreezeRotation;
            }
        }
    }

    public void UnfreezeAll()
    {
        for (int i = 0; i < movableObjects.Count; i++)
        {
            MovableItem item = movableObjects[i];
            if (item == null || item.target == null)
                continue;

            Rigidbody rb = item.target.GetComponent<Rigidbody>();
            if (rb != null && item.hasRigidbody)
            {
                rb.constraints = item.rb_constraints;
            }
        }
    }

#if UNITY_EDITOR
    [ContextMenu("Reset Objects (Runtime Only)")]
    private void _EditorResetObjects()
    {
        ResetObjects();
    }

    [ContextMenu("Freeze All (Runtime Only)")]
    private void _EditorFreezeAll()
    {
        FreezeAll();
    }

    [ContextMenu("Unfreeze All (Runtime Only)")]
    private void _EditorUnfreezeAll()
    {
        UnfreezeAll();
    }
#endif
}
