using System.Collections.Generic;
using UnityEngine;
using Spine;
using Spine.Unity;

/// <summary>
/// 寝技中、GroundGrappleMoveData に定義された複数のベジェIKトラックに従って、
/// 攻撃側の IK ターゲットボーンを動かすドライバ。
/// 
/// ・攻撃側にアタッチする
/// ・BeginGrapple(move, defenderSkeleton) が呼ばれてから動作開始
/// ・FighterCoreManager.State == GroundGrapple_Attacker の間だけ動作
/// ・Spine の UpdateLocal タイミングで IK を書き換える
/// </summary>
[DefaultExecutionOrder(1002)]
public class GroundGrappleIKBezierMulti : MonoBehaviour
{
    [Header("Attacker (self)")]
    public FighterCoreManager attackerCore;
    public SkeletonAnimation attackerSkeleton;

    [Header("Defender (相手)")]
    public SkeletonAnimation defenderSkeleton;

    [Header("Current Move")]
    public GroundGrappleMoveData currentMove;

    [Header("Debug")]
    public bool drawGizmos = true;
    public Color gizmoColor = Color.magenta;
    [Range(4, 64)]
    public int gizmoSegments = 24;

    Skeleton attackerSkel;

    class TrackRuntime
    {
        public GroundGrappleMoveData.BezierIKTrack data;
        public IkConstraint ik;
        public Bone ikTargetBone;
        public float time; // 経過時間（位相オフセット込み）
    }

    readonly List<TrackRuntime> tracks = new();

    void Awake()
    {
        if (!attackerCore) attackerCore = GetComponent<FighterCoreManager>();
        if (!attackerSkeleton)
            attackerSkeleton = GetComponentInChildren<SkeletonAnimation>();
    }

    void OnEnable()
    {
        if (attackerSkeleton is ISkeletonAnimation isa)
            isa.UpdateLocal += HandleUpdateLocal;
    }

    void OnDisable()
    {
        if (attackerSkeleton is ISkeletonAnimation isa)
            isa.UpdateLocal -= HandleUpdateLocal;
    }

    /// <summary>
    /// 寝技開始時に FCM から呼ぶ。
    /// Move と defenderSkeleton をセットして全トラックを初期化。
    /// </summary>
    public void BeginGrapple(GroundGrappleMoveData move, SkeletonAnimation defenderSkel)
    {
        currentMove = move;
        defenderSkeleton = defenderSkel;

        tracks.Clear();

        if (attackerSkeleton == null || defenderSkeleton == null || move == null)
        {
            Debug.LogWarning("[GroundGrappleIKBezierMulti] BeginGrapple: セットアップ不足");
            return;
        }

        attackerSkel = attackerSkeleton.Skeleton;
        if (attackerSkel == null) return;

        var ikList = attackerSkel.IkConstraints;

        if (move.bezierIKTracks == null || move.bezierIKTracks.Length == 0)
            return;

        foreach (var b in move.bezierIKTracks)
        {
            if (string.IsNullOrEmpty(b.ikName))
                continue;

            // IK を名前で探す
            IkConstraint ik = null;
            for (int i = 0; i < ikList.Count; i++)
            {
                var candidate = ikList.Items[i];
                if (candidate.Data.Name == b.ikName)
                {
                    ik = candidate;
                    break;
                }
            }

            if (ik == null)
            {
                Debug.LogWarning($"[GroundGrappleIKBezierMulti] IK '{b.ikName}' が見つかりません。");
                continue;
            }

            var targetBone = ik.Target;
            if (targetBone == null)
            {
                Debug.LogWarning($"[GroundGrappleIKBezierMulti] IK '{b.ikName}' の Target が null です。");
                continue;
            }

            // 初期位相として timeOffset を設定
            float startTime = Mathf.Max(0f, b.timeOffset);

            // ランタイムトラック作成
            tracks.Add(new TrackRuntime
            {
                data = b,
                ik = ik,
                ikTargetBone = targetBone,
                time = startTime
            });

            // 寝技中はこの IK を指定 Mix で効かせる
            ik.Mix = b.ikMix;
        }
    }

    // 寝技中だけ IK ターゲットを動かす
    void HandleUpdateLocal(ISkeletonAnimation anim)
    {
        if (attackerCore == null ||
            attackerCore.State != FighterCoreManager.FighterState.GroundGrapple_Attacker)
            return;

        if (currentMove == null || defenderSkeleton == null)
            return;

        if (tracks.Count == 0)
            return;

        var defSkel = defenderSkeleton.Skeleton;
        if (defSkel == null) return;

        // 防御側のワールド変換を最新に
        defenderSkeleton.Skeleton.UpdateWorldTransform(Skeleton.Physics.Update);

        foreach (var t in tracks)
        {
            var b = t.data;
            var ik = t.ik;
            var ikTargetBone = t.ikTargetBone;

            if (ik == null || ikTargetBone == null)
                continue;

            if (b.moveDuration <= 0.01f) b.moveDuration = 0.01f;

            // 対象ボーン3つからベジェの制御点取得
            if (!TryGetBezierPoints(defSkel, out Vector3 p0, out Vector3 p1, out Vector3 p2, b))
                continue;

            // 時間進行
            t.time += Time.deltaTime;
            float rawT = t.time / b.moveDuration;

            float t01;
            if (b.pingPong)
                t01 = Mathf.PingPong(rawT, 1f);
            else
                t01 = Mathf.Repeat(rawT, 1f);

            float easedT = b.easeCurve != null ? b.easeCurve.Evaluate(t01) : t01;

            // ベジェ上のワールド位置
            Vector3 worldPos = EvaluateQuadraticBezier(p0, p1, p2, easedT);

            // その位置を IK ターゲットボーンのローカル座標に変換してセット
            SetIkTargetToWorldPosition(worldPos, ikTargetBone);
        }

        // 攻撃側スケルトンもワールド更新
        attackerSkel.UpdateWorldTransform(Skeleton.Physics.Update);
    }

    bool TryGetBezierPoints(Skeleton defSkel, out Vector3 p0, out Vector3 p1, out Vector3 p2,
                            GroundGrappleMoveData.BezierIKTrack data)
    {
        p0 = p1 = p2 = Vector3.zero;
        if (defenderSkeleton == null || defSkel == null) return false;

        var b0 = defSkel.FindBone(data.p0BoneName);
        var b1 = defSkel.FindBone(data.p1BoneName);
        var b2 = defSkel.FindBone(data.p2BoneName);

        if (b0 == null || b1 == null || b2 == null)
            return false;

        p0 = defenderSkeleton.transform.TransformPoint(new Vector3(b0.WorldX, b0.WorldY, 0f));
        p1 = defenderSkeleton.transform.TransformPoint(new Vector3(b1.WorldX, b1.WorldY, 0f));
        p2 = defenderSkeleton.transform.TransformPoint(new Vector3(b2.WorldX, b2.WorldY, 0f));
        return true;
    }

    Vector3 EvaluateQuadraticBezier(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        float u = 1f - t;
        return u * u * p0 + 2f * u * t * p1 + t * t * p2;
    }

    void SetIkTargetToWorldPosition(Vector3 worldPos, Bone ikTargetBone)
    {
        if (attackerSkeleton == null || attackerSkel == null || ikTargetBone == null)
            return;

        // Unityワールド → Spineスケルトンローカル
        Vector3 skelSpace = attackerSkeleton.transform.InverseTransformPoint(worldPos);

        float wx = skelSpace.x * attackerSkel.ScaleX;
        float wy = skelSpace.y * attackerSkel.ScaleY;

        var parent = ikTargetBone.Parent ?? attackerSkel.RootBone;
        parent.WorldToLocal(wx, wy, out float lx, out float ly);

        ikTargetBone.SetLocalPosition(new Vector2(lx, ly));
    }

    void OnDrawGizmos()
    {
        if (!drawGizmos || defenderSkeleton == null || currentMove == null) return;

        var defSkel = defenderSkeleton.Skeleton;
        if (defSkel == null) return;

        Gizmos.color = gizmoColor;

        if (currentMove.bezierIKTracks == null) return;

        foreach (var track in currentMove.bezierIKTracks)
        {
            if (!TryGetBezierPoints(defSkel, out Vector3 p0, out Vector3 p1, out Vector3 p2, track))
                continue;

            Vector3 prev = p0;
            for (int i = 1; i <= gizmoSegments; i++)
            {
                float t = (float)i / gizmoSegments;
                Vector3 curr = EvaluateQuadraticBezier(p0, p1, p2, t);
                Gizmos.DrawLine(prev, curr);
                prev = curr;
            }

            Gizmos.DrawSphere(p0, 0.02f);
            Gizmos.DrawSphere(p1, 0.02f);
            Gizmos.DrawSphere(p2, 0.02f);
        }
    }
}
