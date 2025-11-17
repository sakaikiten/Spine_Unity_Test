using UnityEngine;
using Spine.Unity;

public class GroundGrappleAnimator : MonoBehaviour
{
    [Header("Core / Skeleton")]
    public FighterCoreManager core;           // 同じキャラの FCM
    public SkeletonAnimation skeleton;       // このキャラの Spine SkeletonAnimation

    [Header("Fallback Animations")]
    [Tooltip("エントリーアニメが指定されていないときのフォールバック")]
    public string defaultEntryAnim = "";

    void Awake()
    {
        if (!core) core = GetComponent<FighterCoreManager>();
        if (!skeleton) skeleton = GetComponentInChildren<SkeletonAnimation>();
    }

    /// <summary>
    /// 寝技エントリーのアニメーションを再生
    /// </summary>
    /// <param name="move">寝技データ</param>
    /// <param name="isAttacker">攻め側かどうか</param>
    public void PlayEntryAnimation(GroundGrappleMoveData move, bool isAttacker)
    {
        if (skeleton == null)
        {
            Debug.LogWarning("[GroundGrappleAnimator] SkeletonAnimation が設定されていません。");
            return;
        }

        var state = skeleton.AnimationState;
        if (state == null)
        {
            Debug.LogWarning("[GroundGrappleAnimator] AnimationState が取得できません。");
            return;
        }

        string animName = null;
        float timeScale = 1f;
        float entryMixDuration = 0f;

        if (move != null)
        {
            animName = isAttacker
                ? move.attackerEntryAnimState
                : move.defenderEntryAnimState;

            timeScale = move.baseAnimSpeed <= 0f ? 1f : move.baseAnimSpeed;
            entryMixDuration = Mathf.Max(0f, move.entryMixDuration);
        }

        if (string.IsNullOrEmpty(animName))
        {
            animName = defaultEntryAnim;
        }
        if (string.IsNullOrEmpty(animName))
        {
            // フォールバックも無ければ何もしない
            return;
        }

        // スピード設定
        state.TimeScale = timeScale;

        // ★ ループ true で「拘束中もずっとこのポーズ周辺を再生」
        var entry = state.SetAnimation(0, animName, true);

        if (entryMixDuration > 0f)
        {
            entry.MixDuration = entryMixDuration;
        }

        // ★ MixDuration を move から指定（ move が null のときは 0 = デフォルト）
        float mixDuration = 0f;
        if (move != null && move.entryMixDuration > 0f)
        {
            mixDuration = move.entryMixDuration;
        }

        // 0 より大きい値が設定されていれば、明示的にミックス時間を上書き
        if (mixDuration > 0f)
        {
            entry.MixDuration = mixDuration;
        }
    }
}
