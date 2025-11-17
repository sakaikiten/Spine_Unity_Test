using UnityEngine;
using Spine.Unity;
using System.Collections;
using static SpineIKMouseAimer; // Limb enum

[RequireComponent(typeof(FighterLocomotion))] // または SimpleFighterController
public class LimbAttackController : MonoBehaviour {
    public SkeletonAnimation skeleton;
    SpineIKMouseAimer aimer;

    public AttackMoveSet moveSet;

    [Header("Anim Names")]
    public string punch_R = "R_punch";
    public string punch_L = "L_punch";
    public string kick_R  = "R_kick";
    public string kick_L  = "L_kick";

    [HideInInspector] public int currentDamage = 0;

    [Header("Default IK Phase (fallback)")]
    public float toAim = 0.08f;
    public float hold = 0.10f;
    public float @return = 0.12f;
    [Range(0f, 1f)] public float ikMix = 1.0f;

    [Header("Lock Timing (fallback)")]
    public float lockDurationOverride = 0f;

    FighterLocomotion mover;  // 以前 SimpleFighterController だった部分
    bool locked;
    public bool IsLocked => locked; // FCMが参照したければ使う

    void Awake() {
        mover = GetComponent<FighterLocomotion>(); // or SimpleFighterController
        if (!skeleton) skeleton = GetComponentInChildren<SkeletonAnimation>();
        aimer = GetComponent<SpineIKMouseAimer>();
        if (!aimer) Debug.LogWarning("[LimbAttackController] SpineIKMouseAimer が見つかりません。IK制御は行われません。");
    }

    // ★★ ここが「FCMから呼ぶための入口」 ★★
    // AttackKind は FighterCoreManager 側に定義している enum を想定
public void PlayAttack(FighterCoreManager.AttackKind kind, System.Action onFinished) {
    if (locked) return;

    bool facingRight = mover ? mover.FacingRight : true;

    string animToPlay = null;
    Limb? limb = null;


    switch (kind) {
        case FighterCoreManager.AttackKind.RightKick:
                Debug.Log("R_kick");
            animToPlay = facingRight ? kick_R : kick_L;
            limb       = Limb.RFoot;
            break;
        case FighterCoreManager.AttackKind.LeftKick:
            animToPlay = facingRight ? kick_L : kick_R;
            limb       = Limb.LFoot;
            break;
        case FighterCoreManager.AttackKind.RightPunch:
            animToPlay = facingRight ? punch_R : punch_L;
            limb       = Limb.RHand;
            break;
        case FighterCoreManager.AttackKind.LeftPunch:
            animToPlay = facingRight ? punch_L : punch_R;
            limb       = Limb.LHand;
            break;
        default:
            return;
    }

        if (string.IsNullOrEmpty(animToPlay) || !limb.HasValue)
            return;

        // ① アニメ再生（Track 1：攻撃）
        if (skeleton)
            skeleton.AnimationState.SetAnimation(1, animToPlay, false);

        // ② 技データ取得（Limb ごとの AttackMoveData）
        AttackMoveData data = (moveSet != null) ? moveSet.GetForLimb(limb.Value) : null;

        // ③ IK 呼び出し
        if (aimer != null) {
            if (data != null) {
                aimer.AimFrontBackIK(
                    data.ikFront,
                    data.ikBack,
                    facingRight,
                    limb.Value,
                    toAim:    data.toAim,
                    hold:     data.hold,
                    @return:  data.back,
                    mix:      data.ikMix,
                    radius:   0f,
                    respectFlipXPerIK: true,
                    offsetLocal:           Vector2.zero,
                    offsetInSkeletonSpace: false,
                    toCurveOverride:       data.toAimCurve,
                    returnCurveOverride:   data.returnCurve,
                    useAngleLimit: data.limitAngle,
                    minAngleDeg:   data.minAngleDeg,
                    maxAngleDeg:   data.maxAngleDeg
                );

                // スカートIKなども、今まで通りここで limb を見て分岐
                if ((limb == Limb.RFoot || limb == Limb.LFoot)
                    && data.useSkirtIK
                    && !string.IsNullOrEmpty(data.skirtIkName))
                {
                    aimer.AimIK(
                        ikName: data.skirtIkName,
                        limbType: Limb.RHand,
                        toAim: data.toAim,
                        hold:  data.hold,
                        @return: data.back,
                        mix:   data.skirtIkMix,
                        radius: 0f,
                        respectFlipXPerIK: false,
                        offsetLocal: data.skirtOffsetLocal,
                        offsetInSkeletonSpace: true,
                        toCurveOverride:     data.toAimCurve,
                        returnCurveOverride: data.returnCurve
                    );
                }

                currentDamage = data.damage;
            } else {
                Debug.LogWarning($"[LimbAttackController] MoveSet に {limb.Value} 用の AttackMoveData が設定されていません。IK制御はスキップします。");
            }
        }

        // ④ ロック時間決定＆開始
        float lockDur;
        if (data != null) {
            lockDur = (data.lockOverride > 0f)
                ? data.lockOverride
                : (data.toAim + data.hold + data.back);
        } else {
            lockDur = (lockDurationOverride > 0f)
                ? lockDurationOverride
                : (toAim + hold + @return);
        }
        StartCoroutine(LockForAndCallback(lockDur, onFinished));
    }

    IEnumerator LockForAndCallback(float t, System.Action onFinished) {
        locked = true;
        yield return new WaitForSeconds(t);
        locked = false;
        onFinished?.Invoke();
    }
}
