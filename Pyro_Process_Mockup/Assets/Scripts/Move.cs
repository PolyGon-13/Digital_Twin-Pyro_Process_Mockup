using UnityEngine;
using System;
using System.Collections;

public static class Move
{
    public sealed class Ctx
    {
        public Transform Self;
        public Func<float> MoveSpeed;
        public Func<float> PosEps;
        public Func<ArticulationBody> YJoint;
    }

    static float Eps(Ctx ctx)
    {
        float v = (ctx != null && ctx.PosEps != null) ? ctx.PosEps() : 0.01f;
        return Mathf.Max(1e-4f, v);
    }

    public static IEnumerator MoveTo_Target(Ctx ctx, Vector3 target)
    {
        if (ctx == null || !ctx.Self) yield break;

        float pe = Eps(ctx);
        Func<float> sp = ctx.MoveSpeed ?? (() => 0.5f);

        while (Vector3.Distance(ctx.Self.position, target) > pe)
        {
            yield return new WaitForFixedUpdate();
            float step = Mathf.Max(0f, sp()) * Time.fixedDeltaTime;
            ctx.Self.position = Vector3.MoveTowards(ctx.Self.position, target, step);
        }
    }

    public static IEnumerator MoveXZ(Ctx ctx, Vector3 targetXZ)
    {
        if (ctx == null || !ctx.Self) yield break;

        float pe = Eps(ctx);
        Func<float> sp = ctx.MoveSpeed ?? (() => 0.5f);
        Vector3 goal = new Vector3(targetXZ.x, targetXZ.y, targetXZ.z); // 변경

        while (Vector3.Distance(ctx.Self.position, goal) > pe)
        {
            yield return new WaitForFixedUpdate();
            ctx.Self.position = Vector3.MoveTowards(ctx.Self.position, goal, sp() * Time.fixedDeltaTime);
        }
    }

    public static IEnumerator MoveY_Down(Ctx ctx, float targetY, Func<float> downSpeed)
    {
        if (ctx == null || !ctx.Self) yield break;
        if (downSpeed == null) yield break;

        float pe = Eps(ctx);

        Vector3 cur = new Vector3(ctx.Self.position.x, ctx.Self.position.y, ctx.Self.position.z);
        Vector3 goal = new Vector3(cur.x, targetY, cur.z);

        while (Vector3.Distance(ctx.Self.position, goal) > pe)
        {
            // yield return Tick();
            yield return new WaitForFixedUpdate();
            // float step = downSpeed() * Dt();
            float step = downSpeed() * Time.fixedDeltaTime;
            ctx.Self.position = Vector3.MoveTowards(ctx.Self.position, goal, step);
        }
    }

    public static IEnumerator MoveY_Up(Ctx ctx, Func<float> upSpeed)
    {
        if (ctx == null || !ctx.Self) yield break;
        if (ctx.YJoint == null) yield break;
        if (upSpeed == null) yield break;

        var yj = ctx.YJoint();
        if (!yj) yield break;

        float pe = Eps(ctx);

        float cur = (yj.jointPosition.dofCount > 0) ? (float)yj.jointPosition[0] : 0f;
        var yd = yj.xDrive;

        float top = Mathf.Min(yd.lowerLimit, yd.upperLimit);     // ���� 0�� ����
        float delta = top - cur;                                  // ����Ʈ �Ÿ�(���� ����)
        if (Mathf.Abs(delta) <= 1e-4f) yield break;

        float targetY = ctx.Self.position.y - delta;              // �� ��ȣ ���� ����

        while (true)
        {
            float dist = Mathf.Abs(ctx.Self.position.y - targetY);
            if (dist <= pe) yield break;

            yield return new WaitForFixedUpdate();

            float step = Mathf.Max(0f, upSpeed()) * Time.fixedDeltaTime;

            if (dist <= step)
            {
                var p = ctx.Self.position;
                ctx.Self.position = new Vector3(p.x, targetY, p.z);
                yield return new WaitForFixedUpdate();
                yield break;
            }

            var p2 = ctx.Self.position;
            float newY = Mathf.MoveTowards(p2.y, targetY, step);
            ctx.Self.position = new Vector3(p2.x, newY, p2.z);
        }
    }

    // Gripper1 Jaw Open
    public static IEnumerator Open_Heavy_Gripper_Jaw(ArticulationBody abLeft, ArticulationBody abRight,
        float jawPos_Eps, float jawStartSpeed, float jawEndSpeed)
    {
        // ��ǥ ��ġ
        float lTargetVal = 0f;
        float rTargetVal = 0f;

        // ���� ��ġ
        float lCur = abLeft ? abLeft.xDrive.target : 0f;
        float rCur = abRight ? abRight.xDrive.target : 0f;

        float progressed = 0f; // ������ �Ÿ� ����

        // �̵��ؾ� �� �Ÿ� (����/������ �� �� ū �Ÿ�)
        float maxTotal = Mathf.Max(0.0001f, Mathf.Max(
            abLeft ? Mathf.Abs(lTargetVal - lCur) : 0f,
            abRight ? Mathf.Abs(rTargetVal - rCur) : 0f));

        int safety = 0;
        while (true)
        {
            // ���� �Ϸ�Ǿ����� Ȯ��
            bool lDone = (abLeft == null) || (Mathf.Abs(abLeft.xDrive.target - lTargetVal) <= jawPos_Eps);
            bool rDone = (abRight == null) || (Mathf.Abs(abRight.xDrive.target - rTargetVal) <= jawPos_Eps);
            if (lDone && rDone) break;

            float t = Mathf.Clamp01(progressed / maxTotal); // �󸶳� ���ȴ��� 0~1�� ������ �˷���
            float v = Mathf.Lerp(jawStartSpeed, jawEndSpeed, t); // ������� ���� �ӵ��� ���� ������
            float step = v * Time.deltaTime;

            if (abLeft && !lDone)
            {
                var dl = abLeft.xDrive;
                dl.target = Mathf.MoveTowards(dl.target, lTargetVal, step); // dl.target�� lTargetVal������ step��ŭ �̵�
                abLeft.xDrive = dl;
            }
            if (abRight && !rDone)
            {
                var dr = abRight.xDrive;
                dr.target = Mathf.MoveTowards(dr.target, rTargetVal, step);
                abRight.xDrive = dr;
            }

            progressed += step;
            yield return null; // �� ������ ���
            if (++safety > 2000) break; // 2000�������� �ݺ�
        }
        yield break; // �ڷ�ƾ ��� ����
    }

    // Gripper1 Jaw Close
    public static IEnumerator Close_Heavy_Gripper_Jaw(ArticulationBody _abLeft, ArticulationBody _abRight,
        float leftTargetPos, float rightTargetPos, float jawPos_Eps, float jawStartSpeed, float jawEndSpeed)
    {
        if (_abLeft == null && _abRight == null) yield break;

        float lTgt = leftTargetPos;
        float rTgt = rightTargetPos;

        // ���ѹ��� ����
        if (_abLeft)
        {
            var dl = _abLeft.xDrive;
            lTgt = Mathf.Clamp(lTgt, dl.lowerLimit, dl.upperLimit);
        }
        if (_abRight)
        {
            var dr = _abRight.xDrive;
            rTgt = Mathf.Clamp(rTgt, dr.lowerLimit, dr.upperLimit);
        }

        // ���� ��ġ
        float lCur = _abLeft ? _abLeft.xDrive.target : 0f;
        float rCur = _abRight ? _abRight.xDrive.target : 0f;

        float progressed = 0f; // ������ �Ÿ� ����

        // �̵��ؾ� �� �Ÿ�
        float maxTotal = Mathf.Max(0.0001f, Mathf.Max(
            _abLeft ? Mathf.Abs(lTgt - lCur) : 0f,
            _abRight ? Mathf.Abs(rTgt - rCur) : 0f));

        int safety = 0;
        while (true)
        {
            bool lDone = (_abLeft == null) || (Mathf.Abs(_abLeft.xDrive.target - lTgt) <= jawPos_Eps);
            bool rDone = (_abRight == null) || (Mathf.Abs(_abRight.xDrive.target - rTgt) <= jawPos_Eps);
            if (lDone && rDone) break;

            float t = Mathf.Clamp01(progressed / maxTotal);
            float v = Mathf.Lerp(jawStartSpeed, jawEndSpeed, t);
            float step = v * Time.deltaTime;

            if (_abLeft && !lDone)
            {
                var dl = _abLeft.xDrive;
                dl.target = Mathf.MoveTowards(dl.target, lTgt, step);
                _abLeft.xDrive = dl;
            }
            if (_abRight && !rDone)
            {
                var dr = _abRight.xDrive;
                dr.target = Mathf.MoveTowards(dr.target, rTgt, step);
                _abRight.xDrive = dr;
            }

            progressed += step;
            yield return null;
            if (++safety > 2000) break;
        }
        yield break;
    }

    public static IEnumerator Close_Angular_Gripper_Jaw(ArticulationBody _abLeft, ArticulationBody _abRight, ArticulationBody _abCenter, float TargetPos, float jawPos_Eps, float jawStartSpeed, float jawEndSpeed)
    {
        if (_abLeft == null && _abRight == null && _abCenter == null) yield break;

        float lTgt = _abLeft ? Mathf.Clamp(TargetPos, _abLeft.xDrive.lowerLimit, _abLeft.xDrive.upperLimit) : 0f;
        float rTgt = _abRight ? Mathf.Clamp(TargetPos, _abRight.xDrive.lowerLimit, _abRight.xDrive.upperLimit) : 0f;
        float cTgt = _abCenter ? Mathf.Clamp(TargetPos, _abCenter.xDrive.lowerLimit, _abCenter.xDrive.upperLimit) : 0f;

        float lCur = _abLeft ? _abLeft.xDrive.target : 0f;
        float rCur = _abRight ? _abRight.xDrive.target : 0f;
        float cCur = _abCenter ? _abCenter.xDrive.target : 0f;

        float maxTotal = Mathf.Max(
            Mathf.Max(_abLeft ? Mathf.Abs(lTgt - lCur) : 0f, _abRight ? Mathf.Abs(rTgt - rCur) : 0f),
            _abCenter ? Mathf.Abs(cTgt - cCur) : 0f);

        if (maxTotal < 1e-6f)
        {
            if (_abLeft) { var d = _abLeft.xDrive; d.target = lTgt; _abLeft.xDrive = d; }
            if (_abRight) { var d = _abRight.xDrive; d.target = rTgt; _abRight.xDrive = d; }
            if (_abCenter) { var d = _abCenter.xDrive; d.target = cTgt; _abCenter.xDrive = d; }
            yield break;
        }

        float progressed = 0f;
        int safety = 0;

        while (true)
        {
            bool lDone = (_abLeft == null) || Mathf.Abs(_abLeft.xDrive.target - lTgt) <= jawPos_Eps;
            bool rDone = (_abRight == null) || Mathf.Abs(_abRight.xDrive.target - rTgt) <= jawPos_Eps;
            bool cDone = (_abCenter == null) || Mathf.Abs(_abCenter.xDrive.target - cTgt) <= jawPos_Eps;
            if (lDone && rDone && cDone) break;

            float t = Mathf.Clamp01(progressed / maxTotal);
            float v = Mathf.Lerp(jawStartSpeed, jawEndSpeed, t);
            float step = v * Time.deltaTime;

            if (_abLeft && !lDone)
            {
                var d = _abLeft.xDrive;
                d.target = Mathf.MoveTowards(d.target, lTgt, step);
                _abLeft.xDrive = d;
            }
            if (_abRight && !rDone)
            {
                var d = _abRight.xDrive;
                d.target = Mathf.MoveTowards(d.target, rTgt, step);
                _abRight.xDrive = d;
            }
            if (_abCenter && !cDone)
            {
                var d = _abCenter.xDrive;
                d.target = Mathf.MoveTowards(d.target, cTgt, step);
                _abCenter.xDrive = d;
            }

            progressed += step;
            yield return null;

            if (++safety > 2000) break;
        }
    }

    public static IEnumerator Open_Angular_Gripper_Jaw(ArticulationBody abLeft, ArticulationBody abRight, ArticulationBody abCenter, float jawPos_Eps, float jawStartSpeed, float jawEndSpeed)
    {
        if (abLeft == null && abRight == null && abCenter == null) yield break;

        // left, right, center 모두 제한범위가 동일하므로 left를 사용
        float lTgt = abLeft ? abLeft.xDrive.lowerLimit : 0f;
        float rTgt = abRight ? abRight.xDrive.lowerLimit : 0f;
        float cTgt = abCenter ? abCenter.xDrive.lowerLimit : 0f;

        // 현재 target(출발점)
        float lCur = abLeft ? abLeft.xDrive.target : 0f;
        float rCur = abRight ? abRight.xDrive.target : 0f;
        float cCur = abCenter ? abCenter.xDrive.target : 0f;

        // 가장 멀리 이동해야 하는 거리(감속 보간 분모)
        float maxTotal = Mathf.Max(
            Mathf.Max(abLeft ? Mathf.Abs(lTgt - lCur) : 0f,
                    abRight ? Mathf.Abs(rTgt - rCur) : 0f),
                    abCenter ? Mathf.Abs(cTgt - cCur) : 0f);

        if (maxTotal < 1e-6f)
        {
            if (abLeft)
            {
                var d = abLeft.xDrive;
                d.target = lTgt;
                abLeft.xDrive = d;
            }
            if (abRight)
            {
                var d = abRight.xDrive;
                d.target = rTgt;
                abRight.xDrive = d;
            }
            if (abCenter)
            {
                var d = abCenter.xDrive;
                d.target = cTgt;
                abCenter.xDrive = d;
            }
            yield break;
        }

        float progressed = 0f;
        int safety = 0;

        while (true)
        {
            bool lDone = (abLeft == null) || Mathf.Abs(abLeft.xDrive.target - lTgt) <= jawPos_Eps;
            bool rDone = (abRight == null) || Mathf.Abs(abRight.xDrive.target - rTgt) <= jawPos_Eps;
            bool cDone = (abCenter == null) || Mathf.Abs(abCenter.xDrive.target - cTgt) <= jawPos_Eps;
            if (lDone && rDone && cDone) break;

            float t = Mathf.Clamp01(progressed / Mathf.Max(0.0001f, maxTotal));
            float v = Mathf.Lerp(jawStartSpeed, jawEndSpeed, t);
            float step = v * Time.deltaTime;

            if (abLeft && !lDone)
            {
                var d = abLeft.xDrive;
                d.target = Mathf.MoveTowards(d.target, lTgt, step);
                abLeft.xDrive = d;
            }
            if (abRight && !rDone)
            {
                var d = abRight.xDrive;
                d.target = Mathf.MoveTowards(d.target, rTgt, step);
                abRight.xDrive = d;
            }
            if (abCenter && !cDone)
            {
                var d = abCenter.xDrive;
                d.target = Mathf.MoveTowards(d.target, cTgt, step);
                abCenter.xDrive = d;
            }

            progressed += step;
            yield return null;
            if (++safety > 2000) break;
        }
    }

    // Rigidbody�� freeze position�� X,Y,Z ����
    public static void FreezeRB(Rigidbody rb, bool x, bool y, bool z)
    {
        if (!rb) return;

        RigidbodyConstraints constraints = rb.constraints;

        if (x) constraints |= RigidbodyConstraints.FreezePositionX;
        else constraints &= ~RigidbodyConstraints.FreezePositionX;

        // Y축
        if (y) constraints |= RigidbodyConstraints.FreezePositionY;
        else constraints &= ~RigidbodyConstraints.FreezePositionY;

        // Z축
        if (z) constraints |= RigidbodyConstraints.FreezePositionZ;
        else constraints &= ~RigidbodyConstraints.FreezePositionZ;

        rb.constraints = constraints;
    }

    public static void SetGravity(Rigidbody rb, bool useGravity)
    {
        if (!rb) return;
        rb.useGravity = useGravity;
    }

    /*
                        // Rigidbody�� freeze position�� Y,Z ����
                        // �⺻ freeze position�� X,Y,Z�̰� �� ���̾� �� �浹�� ���� �������� ����
                        public static void UnfreezeUpperYZ(Rigidbody upperRb)
                        {

                            int upperLayer = LayerMask.NameToLayer("Upper");
                            int facilityLayer = LayerMask.NameToLayer("FacillityGeneral");

                            if (upperLayer != -1 && facilityLayer != -1)
                            {
                                Physics.IgnoreLayerCollision(upperLayer, facilityLayer, true); // �� ���̾� �� �浹���� ON
                            }


                            if (upperRb)
                            {
                                upperRb.constraints &= ~(RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezePositionZ);
                            }

                            if (upperLayer != -1 && facilityLayer != -1)
                            {
                                Physics.IgnoreLayerCollision(upperLayer, facilityLayer, false);
                            }
                        }
                    */

    public static IEnumerator MoveTo_Target(Transform self, Func<float> moveSpeed, Vector3 target, float eps)
    {
        var ctx = new Ctx { Self = self, MoveSpeed = moveSpeed, PosEps = () => eps };
        return MoveTo_Target(ctx, target);
    }
    public static IEnumerator MoveXZ(Transform self, Func<float> moveSpeed, Vector3 targetXZ, float eps)
    {
        var ctx = new Ctx { Self = self, MoveSpeed = moveSpeed, PosEps = () => eps };
        return MoveXZ(ctx, targetXZ);
    }
    public static IEnumerator MoveY_Down(Transform self, float targetY, Func<float> downSpeed, float eps)
    {
        var ctx = new Ctx { Self = self, PosEps = () => eps };
        return MoveY_Down(ctx, targetY, downSpeed);
    }
    public static IEnumerator MoveY_Up(Transform self, ArticulationBody y_joint, Func<float> upSpeed, float eps)
    {
        var ctx = new Ctx { Self = self, PosEps = () => eps, YJoint = () => y_joint };
        return MoveY_Up(ctx, upSpeed);
    }
}