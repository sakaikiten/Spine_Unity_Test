using UnityEngine;
using Spine;
using Spine.Unity;

/// <summary>
/// 寝技中、相手（防御側）の3ボーンから作った二次ベジェ曲線に沿って、
/// 攻撃側の指定 IK のターゲットボーンを往復移動させるドライバ。
/// 
/// ・攻撃側にアタッチする
/// ・防御側 SkeletonAnimation と 3ボーン名を指定
/// ・FighterCoreManager.State == GroundGrapple_Attacker の間だけ動作
/// </summary>
[DefaultExecutionOrder(1002)]
public class GroundGrappleIKBezier : MonoBehaviour
{
    [Header("Attacker (self)")]
    public FighterCoreManager attackerCore;
    public SkeletonAnimation attackerSkeleton;
    public string ikName;                    // 対象 IK の名前（Spine 側で付けた名前）
    public float ikMix = 1.0f;              // 寝技中の IK Mix

    [Header("Defender (相手)")]
    public SkeletonAnimation defenderSkeleton;

    [SpineBone(dataField: "defenderSkeleton")]
    public string p0BoneName = "hip";

    [SpineBone(dataField: "defenderSkeleton")]
    public string p1BoneName = "spine";

    [SpineBone(dataField: "defenderSkeleton")]
    public string p2BoneName = "head";

    [Header("Motion")]
    public float moveDuration = 1.5f;        // 片道の時間（秒）
    public bool pingPong = true;             // 往復させる
    public AnimationCurve easeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Debug")]
    public bool drawGizmos = true;
    public Color gizmoColor = Color.magenta;
    [Range(4, 64)]
    public int gizmoSegments = 24;

    Skeleton attackerSkel;
    IkConstraint ik;
    Bone ikTargetBone;

    float time;

    void Awake()
    {
        if (!attackerCore) attackerCore = GetComponent<FighterCoreManager>();
        if (!attackerSkeleton)
            attackerSkeleton = GetComponentInChildren<SkeletonAnimation>();
    }

    void Start()
    {
        SetupIkIfNeeded();
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

    // 寝技中だけ IK ターゲットを動かす
    void HandleUpdateLocal(ISkeletonAnimation anim)
    {
        if (attackerCore == null ||
            attackerCore.State != FighterCoreManager.FighterState.GroundGrapple_Attacker)
            return;

        if (!SetupIkIfNeeded()) return;
        if (!TryGetBezierPoints(out Vector3 p0, out Vector3 p1, out Vector3 p2)) return;

        if (moveDuration <= 0.01f) moveDuration = 0.01f;

        // 時間から 0〜1 の t を作る（PingPongなら 0→1→0→1…）
        time += Time.deltaTime;
        float rawT = time / moveDuration;
        float t = pingPong ? Mathf.PingPong(rawT, 1f) : Mathf.Repeat(rawT, 1f);

        float easedT = easeCurve != null ? easeCurve.Evaluate(t) : t;

        // ベジェ上のワールド位置
        Vector3 worldPos = EvaluateQuadraticBezier(p0, p1, p2, easedT);

        // その位置を IK ターゲットボーンの「親ボーンローカル座標」に変換してセット
        SetIkTargetToWorldPosition(worldPos);
    }

    bool SetupIkIfNeeded()
    {
        if (attackerSkeleton == null) return false;

        if (attackerSkel == null)
            attackerSkel = attackerSkeleton.Skeleton;
        if (attackerSkel == null) return false;

        if (ik == null)
        {
            if (string.IsNullOrEmpty(ikName))
            {
                Debug.LogWarning("[GroundGrappleIKBezier] ikName が未設定です。");
                return false;
            }

            ik = attackerSkel.FindIkConstraint(ikName);
            if (ik == null)
            {
                Debug.LogWarning($"[GroundGrappleIKBezier] IK '{ikName}' が見つかりません。");
                return false;
            }
            ikTargetBone = ik.Target;
        }

        if (ikTargetBone == null)
        {
            Debug.LogWarning($"[GroundGrappleIKBezier] IK '{ikName}' の Target が null です。");
            return false;
        }

        // 寝技中はこの IK をフルで効かせる
        ik.Mix = ikMix;
        return true;
    }

    /// <summary>
    /// 防御側スケルトンの3ボーンからワールド座標の3点を取得
    /// </summary>
    bool TryGetBezierPoints(out Vector3 p0, out Vector3 p1, out Vector3 p2)
    {
        p0 = p1 = p2 = Vector3.zero;
        if (defenderSkeleton == null) return false;

        var skel = defenderSkeleton.Skeleton;
        if (skel == null) return false;

        var b0 = skel.FindBone(p0BoneName);
        var b1 = skel.FindBone(p1BoneName);
        var b2 = skel.FindBone(p2BoneName);

        if (b0 == null || b1 == null || b2 == null)
        {
            return false;
        }

        p0 = defenderSkeleton.transform.TransformPoint(new Vector3(b0.WorldX, b0.WorldY, 0f));
        p1 = defenderSkeleton.transform.TransformPoint(new Vector3(b1.WorldX, b1.WorldY, 0f));
        p2 = defenderSkeleton.transform.TransformPoint(new Vector3(b2.WorldX, b2.WorldY, 0f));
        return true;
    }

    /// <summary>
    /// 二次ベジェ曲線 B(t) = (1-t)^2 p0 + 2(1-t)t p1 + t^2 p2
    /// </summary>
    Vector3 EvaluateQuadraticBezier(Vector3 p0, Vector3 p1, Vector3 p2, float t)
    {
        float u = 1f - t;
        return u * u * p0 + 2f * u * t * p1 + t * t * p2;
    }

    /// <summary>
    /// ワールド座標で指定された位置に IK ターゲットボーンを移動させる。
    /// ワールド → スケルトンローカル → 親ボーンローカル、と変換。
    /// </summary>
    void SetIkTargetToWorldPosition(Vector3 worldPos)
    {
        if (attackerSkeleton == null || attackerSkel == null || ikTargetBone == null)
            return;

        // Unityワールド → Spineスケルトンローカル
        Vector3 skelSpace = attackerSkeleton.transform.InverseTransformPoint(worldPos);

        // Spine内部のスケールを考慮して「ワールド座標（Spine的）」にセット
        float wx = skelSpace.x * attackerSkel.ScaleX;
        float wy = skelSpace.y * attackerSkel.ScaleY;

        // ターゲットボーンの親ボーンローカルに変換
        var parent = ikTargetBone.Parent ?? attackerSkel.RootBone;
        parent.WorldToLocal(wx, wy, out float lx, out float ly);

        // IKターゲットボーンのローカル座標を更新
        ikTargetBone.SetLocalPosition(new Vector2(lx, ly));
    }

    void OnDrawGizmos()
    {
        if (!drawGizmos || defenderSkeleton == null) return;
        if (!TryGetBezierPoints(out Vector3 p0, out Vector3 p1, out Vector3 p2)) return;

        Gizmos.color = gizmoColor;

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
