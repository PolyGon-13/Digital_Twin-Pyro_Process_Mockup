using UnityEngine;

#if UNITY_EDITOR
[ExecuteInEditMode]
public class PhysicsValidator : MonoBehaviour
{
    void OnValidate()
    {
        // Articulation 검사
        foreach (var ab in Object.FindObjectsByType<ArticulationBody>(FindObjectsSortMode.None))
        {
            // jointPosition / jointVelocity 길이 먼저 확인
            var jp = ab.jointPosition;
            for (int i = 0; i < jp.dofCount; i++)
            {
                if (float.IsNaN(jp[i]))
                    Debug.LogError($"[PhysicsValidator] {ab.name} jointPosition[{i}] is NaN");
            }

            var jv = ab.jointVelocity;
            for (int i = 0; i < jv.dofCount; i++)
            {
                if (float.IsNaN(jv[i]))
                    Debug.LogError($"[PhysicsValidator] {ab.name} jointVelocity[{i}] is NaN");
            }

            if (ab.mass <= 0f)
                Debug.LogWarning($"[PhysicsValidator] {ab.name} mass <= 0. PhysX can be unstable.");
        }

        // Collider 검사
        foreach (var col in Object.FindObjectsByType<Collider>(FindObjectsSortMode.None))
        {
            var ls = col.transform.lossyScale;
            if (ls.x < 0.001f || ls.y < 0.001f || ls.z < 0.001f)
            {
                Debug.LogWarning($"[PhysicsValidator] {col.name} scale too small ({ls}).");
            }

            // MeshCollider는 Convex만 허용하도록 안내
            var mc = col as MeshCollider;
            if (mc != null && !mc.convex)
            {
                Debug.LogWarning($"[PhysicsValidator] {mc.name} is MeshCollider non-convex. Use Convex for Articulation.");
            }
        }
    }
}
#endif
