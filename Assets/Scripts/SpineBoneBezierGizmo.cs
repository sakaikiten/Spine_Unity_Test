using UnityEngine;
using Spine.Unity;

/// <summary>
/// 防御側スケルトンの3ボーンを結ぶ二次ベジェ曲線を
/// Gizmo で可視化し、任意の Transform をその上で往復移動させるテスター。
/// （のちに IK ターゲット用ロジックに差し替え予定）
/// </summary>
public class SpineBoneBezierGizmo : MonoBehaviour
{
    [Header("Target Skeleton (Defender)")]
    public SkeletonAnimation targetSkeleton;

    [SpineBone(dataField: "targetSkeleton")]
    public string p0BoneName = "target_manko_entry";

    [SpineBone(dataField: "targetSkeleton")]
    public string p1BoneName = "target_manko_bejyeCurve";

    [SpineBone(dataField: "targetSkeleton")]
    public string p2BoneName = "target_portio";

    [Header("Gizmo")]
    public Color gizmoColor = Color.cyan;
    [Range(4, 64)]
    public int segments = 24;  // 曲線を何分割するか

    [Header("Debug Follower (テスト用)")]
    public Transform follower;     // ベジェ上を動かすオブジェクト（後で IK ターゲットに置き換え）
    public float moveDuration = 1.5f;   // 片道の時間（秒）
    public bool pingPong = true;        // 往復させるかどうか
    public AnimationCurve easeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    float time;

    void Update()
    {
        if (follower == null || targetSkeleton == null)
            return;

        // 時間から t を計算
        if (moveDuration <= 0.01f) moveDuration = 0.01f;

        time += Time.deltaTime;
        float rawT = (time / moveDuration);

        float t;
        if (pingPong)
        {
            // 0→1→0→1… の PingPong
            t = Mathf.PingPong(rawT, 1f);
        }
        else
        {
            // 0→1→0→1… だと困るならループ系にしてもOK
            t = Mathf.Repeat(rawT, 1f);
        }

        // イージングカーブを適用
        float easedT = easeCurve != null ? easeCurve.Evaluate(t) : t;

        // ベジェ上の座標を計算して follower を動かす
        if (TryGetBezierPoints(out Vector3 p0, out Vector3 p1, out Vector3 p2))
        {
            Vector3 pos = EvaluateQuadraticBezier(p0, p1, p2, easedT);
            follower.position = pos;
        }
    }

    void OnDrawGizmos()
    {
        if (targetSkeleton == null)
            return;

        if (!TryGetBezierPoints(out Vector3 p0, out Vector3 p1, out Vector3 p2))
            return;

        Gizmos.color = gizmoColor;

        Vector3 prev = p0;
        for (int i = 1; i <= segments; i++)
        {
            float t = (float)i / segments;
            Vector3 curr = EvaluateQuadraticBezier(p0, p1, p2, t);
            Gizmos.DrawLine(prev, curr);
            prev = curr;
        }

        // 制御点も軽く表示（小さな球）
        Gizmos.DrawSphere(p0, 0.02f);
        Gizmos.DrawSphere(p1, 0.02f);
        Gizmos.DrawSphere(p2, 0.02f);
    }

    /// <summary>
    /// 二次ベジェ曲線 B(t) = (1-t)^2 * p0 + 2(1-t)t * p1 + t^2 * p2
    /// </summary>
    Vector3 EvaluateQuadraticBezier(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        float u = 1f - t;
        return u * u * p0 + 2f * u * t * p1 + t * t * p2;
    }

    /// <summary>
    /// Spine の3ボーンからワールド座標の3点を取得
    /// </summary>
    bool TryGetBezierPoints(out Vector3 p0, out Vector3 p1, out Vector3 p2)
    {
        p0 = p1 = p2 = Vector3.zero;
        if (targetSkeleton == null) return false;

        var skel = targetSkeleton.Skeleton;
        if (skel == null) return false;

        var b0 = skel.FindBone(p0BoneName);
        var b1 = skel.FindBone(p1BoneName);
        var b2 = skel.FindBone(p2BoneName);

        if (b0 == null || b1 == null || b2 == null)
        {
            // どれか1つでも見つからなければ失敗
            return false;
        }

        // Spineのworld座標 → Unity world座標
        p0 = targetSkeleton.transform.TransformPoint(new Vector3(b0.WorldX, b0.WorldY, 0f));
        p1 = targetSkeleton.transform.TransformPoint(new Vector3(b1.WorldX, b1.WorldY, 0f));
        p2 = targetSkeleton.transform.TransformPoint(new Vector3(b2.WorldX, b2.WorldY, 0f));

        return true;
    }
}
