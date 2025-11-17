using UnityEngine;
using Spine.Unity;

// AIGroundGrappleEntry は「ダウン検知 → 接近 → FCMへ寝技開始を依頼」だけ担当
// 移動は Rigidbody2D を直接いじる代わりに、接近中だけ FighterLocomotion.enabled = false にして競合を避ける
// 到達したら
// rb の横速度をゼロにして
// Locomotion を元に戻し
// .selfCore.EnterGroundGrappleAsAttacker(targetCore, entryMove); を呼ぶ
// という流れです。

[RequireComponent(typeof(FighterCoreManager))]
[RequireComponent(typeof(FighterLocomotion))]
[RequireComponent(typeof(Rigidbody2D))]
public class AIGroundGrappleEntry : MonoBehaviour
{
    [Header("Self / Target")]
    public FighterCoreManager selfCore;          // 自分（寝技をかける側）
    public FighterLocomotion selfLocomotion;     // 自分の移動コンポーネント
    public FighterCoreManager targetCore;        // 相手（寝技をかけられる側）

    [Header("Target Skeleton / Bone")]
    public SkeletonAnimation targetSkeleton;     // 相手の SkeletonAnimation
    // [SpineBone(dataField: "targetSkeleton")]
    //public string targetEntryBoneName = "hip";   // 寝技エントリーポイントにしたいボーン名

    // 既存の targetEntryBoneName は、
    // 「データが無いときのフォールバック」としてだけ使うようにする
    [Spine.Unity.SpineBone(dataField: "targetSkeleton")]
    public string targetEntryBoneName = "hip";


    [Header("Grapple Move Data")]
    public GroundGrappleMoveData entryMove;

    [Header("Approach Settings")]
    public float approachSpeed = 3f;             // 接近速度
    public float stopDistanceX = 0.12f;          // X距離がこの値以下なら到達扱い

    Rigidbody2D rb;

    enum EntryState
    {
        Idle,           // 何もしない
        WaitingForDown, // 相手がダウンするのを待っている
        Approaching,    // 寝技エントリーボーンに接近中
        Reached         // 到達、ここから寝技状態へ移行
    }

    [SerializeField]
    EntryState state = EntryState.WaitingForDown;

    bool locomotionTemporarilyDisabled = false;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (!selfCore)        selfCore        = GetComponent<FighterCoreManager>();
        if (!selfLocomotion)  selfLocomotion  = GetComponent<FighterLocomotion>();
    }

    void Update()
    {
        if (state == EntryState.WaitingForDown)
        {
            // 相手がダウンしたら接近開始
            if (targetCore != null && targetCore.IsDown)
            {
                state = EntryState.Approaching;
            }
        }
    }

    void FixedUpdate()
    {
        switch (state)
        {
            case EntryState.Approaching:
                HandleApproach();
                break;

            default:
                // 接近中でないときは、Locomotion を元の状態に戻しておく
                RestoreLocomotionIfNeeded();
                break;
        }
    }

    void HandleApproach()
    {
        if (targetCore == null || targetSkeleton == null)
        {
            state = EntryState.Idle;
            return;
        }

        // ★ FighterLocomotion と競合しないよう、接近中だけ一時的に無効化
        if (!locomotionTemporarilyDisabled)
        {
            selfLocomotion.enabled = false;
            locomotionTemporarilyDisabled = true;
        }

        Vector3 entryWorldPos = GetTargetEntryWorldPos();
        Vector2 selfPos = rb.position;

        float dx = entryWorldPos.x - selfPos.x;

        // 十分近ければ到達
        if (Mathf.Abs(dx) <= stopDistanceX)
        {
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);

            // Locomotion を戻す（この後は寝技側ロジックで canMove を止める）
            RestoreLocomotionIfNeeded();

            state = EntryState.Reached;

            // ここで寝技状態へ移行（FCM に頼む）
            // selfCore.EnterGroundGrappleAsAttacker(targetCore);
            selfCore.EnterGroundGrappleAsAttacker(targetCore, entryMove);
            return;
        }

        // まだ遠いので X 方向に移動
        float dir = Mathf.Sign(dx); // -1 or +1

        rb.linearVelocity = new Vector2(dir * approachSpeed, rb.linearVelocity.y);

        // 向きも合わせておく（Locomotion を止めているので自分でスケールを反転）
        if (dir > 0.05f)
        {
            transform.localScale = new Vector3(1f, 1f, 1f);
        }
        else if (dir < -0.05f)
        {
            transform.localScale = new Vector3(-1f, 1f, 1f);
        }
    }

    void RestoreLocomotionIfNeeded()
    {
        if (locomotionTemporarilyDisabled)
        {
            selfLocomotion.enabled = true;
            locomotionTemporarilyDisabled = false;
        }
    }

    /// <summary>
    /// 相手スケルトンの指定ボーンのワールド座標を取得
    /// </summary>
    Vector3 GetTargetEntryWorldPos()
    {
        var skeleton = targetSkeleton.Skeleton;
        if (skeleton == null)
            return targetSkeleton.transform.position;

        // どのボーンを使うか
        string boneName;

        if (entryMove != null && !string.IsNullOrEmpty(entryMove.targetEntryBoneName))
        {
            boneName = entryMove.targetEntryBoneName;
        }
        else
        {
            boneName = targetEntryBoneName; // 旧フィールド（保険）
        }

        var bone = skeleton.FindBone(boneName);
        if (bone == null)
        {
            Debug.LogWarning($"[AIGroundGrappleEntry] Bone '{boneName}' not found.");
            return targetSkeleton.transform.position;
        }

        // Spine ボーンのワールド位置
        Vector3 baseWorldPos = targetSkeleton.transform.TransformPoint(
            new Vector3(bone.WorldX, bone.WorldY, 0f)
        );

        // ここから攻撃側の理想オフセットを加える
        if (entryMove != null)
        {
            Vector2 offset = entryMove.attackerOffsetFromTarget;

            // 攻撃側の向きに合わせて X オフセットを反転するかどうか
            if (entryMove.mirrorOffsetByFacing)
            {
                // localScale.x で左右判定（右向き = 1, 左向き = -1 前提）
                float facingSign = Mathf.Sign(transform.localScale.x);
                offset.x *= facingSign;
            }

            baseWorldPos += new Vector3(offset.x, offset.y, 0f);
        }

        return baseWorldPos;
    }

}
