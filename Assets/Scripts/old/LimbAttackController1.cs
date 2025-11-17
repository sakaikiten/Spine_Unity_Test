// using UnityEngine;
// using Spine.Unity;
// using System.Collections;
// using static SpineIKMouseAimer; // Limb enum を使うため

// //FCMに統一前

// [RequireComponent(typeof(FighterInputs))]
// public class LimbAttackController : MonoBehaviour
// {
//     public SkeletonAnimation skeleton;
//     SpineIKMouseAimer aimer;

//     [Header("Move Set (per limb)")]
//     public AttackMoveSet moveSet;

//     // 既存のアニメ名はそのまま活用（向きに応じてここで左右を入れ替える）
//     [Header("Anim Names")]
//     public string punch_R = "R_punch";
//     public string punch_L = "L_punch";
//     public string kick_R  = "R_kick";
//     public string kick_L  = "L_kick";

//     // （任意）ダメージを他スクリプトへ渡したい場合に参照
//     [HideInInspector] public int currentDamage = 0;

//     [Header("Mapping Mode")]
//     public bool useLeadSideMapping = true; // ← 今は無視していてOK

//     // フォールバック用（MoveData が無いとき）
//     [Header("Default IK Phase (fallback)")]
//     public float toAim = 0.08f;
//     public float hold = 0.10f;
//     public float @return = 0.12f;
//     [Range(0f, 1f)] public float ikMix = 1.0f;

//     [Header("Lock Timing (fallback)")]
//     public float lockDurationOverride = 0f;

//     FighterInputs inputs;
//     SimpleFighterController mover;
//     bool locked;

//     void Awake()
//     {
//         inputs = GetComponent<FighterInputs>();
//         mover  = GetComponent<SimpleFighterController>();
//         if (!skeleton) skeleton = GetComponentInChildren<SkeletonAnimation>();
//         aimer = GetComponent<SpineIKMouseAimer>();
//         if (!aimer) Debug.LogWarning("[LimbAttackController] SpineIKMouseAimer が見つかりません。IK制御は行われません。");
//     }

//     void Update()
//     {
//         if (locked) { inputs.ConsumeFrameButtons(); return; }

//         bool facingRight = mover ? mover.FacingRight : true;

//         // --- 入力→アニメ名 & limb 決定（アニメは左右反転、limb は固定） ---
//         string animToPlay = null;
//         Limb? limb = null;

//         if (inputs.RightKickPressed) {
//             animToPlay = facingRight ? kick_R : kick_L;
//             limb       = Limb.RFoot; // 常に「右足技」として扱う
//         }
//         else if (inputs.LeftKickPressed) {
//             animToPlay = facingRight ? kick_L : kick_R;
//             limb       = Limb.LFoot;
//         }
//         else if (inputs.RightPunchPressed) { // 左クリック＝右手パンチ（固定）
//             animToPlay = facingRight ? punch_R : punch_L;
//             limb       = Limb.RHand;
//         }
//         else if (inputs.LeftPunchPressed) {  // 右クリック＝左手パンチ（固定）
//             animToPlay = facingRight ? punch_L : punch_R;
//             limb       = Limb.LHand;
//         }

//         if (!string.IsNullOrEmpty(animToPlay) && limb.HasValue)
//         {
//             // ① アニメ再生（Track 1：攻撃）
//             if (skeleton)
//                 skeleton.AnimationState.SetAnimation(1, animToPlay, false);

//             // ② 技データ取得（肢で固定：RHand/LHand/RFoot/LFoot）
//             AttackMoveData data = (moveSet != null) ? moveSet.GetForLimb(limb.Value) : null;

//             // ③ IK 呼び出し
//             if (aimer != null)
//             {
//                 if (data != null)
//                 {
//                     // ■ メインIK（手・足）… Front/Back を向きで切り替えて、名前指定IKで制御
//                     aimer.AimFrontBackIK(
//                         data.ikFront,           // 右向き時に使う IK 名
//                         data.ikBack,            // 左向き時に使う IK 名
//                         facingRight,            // どちら向きか
//                         limb.Value,             // この技が属する Limb（半径のデフォルト決定用）
//                         toAim: data.toAim,
//                         hold: data.hold,
//                         @return: data.back,
//                         mix: data.ikMix,
//                         radius: 0f,            // 0以下なら Aimer 側の handRadius / footRadius を使用
//                         respectFlipXPerIK: true,
//                         offsetLocal: Vector2.zero,
//                         offsetInSkeletonSpace: false,
//                         toCurveOverride: data.toAimCurve,
//                         returnCurveOverride: data.returnCurve,
//                         useAngleLimit: data.limitAngle,     //角度制限
//                         minAngleDeg:   data.minAngleDeg,    //角度制限
//                         maxAngleDeg:   data.maxAngleDeg     //角度制限
//                     );

//                     // ■ スカートIK … キック時だけ、マウス位置＋オフセットへふわっと動かす
//                     if ((limb == Limb.RFoot || limb == Limb.LFoot)
//                         && data.useSkirtIK
//                         && !string.IsNullOrEmpty(data.skirtIkName))
//                     {
//                         aimer.AimIK(
//                             ikName: data.skirtIkName,
//                             limbType: Limb.RHand, // 半径は使わないのでどれでもOK
//                             toAim: data.toAim,
//                             hold:  data.hold,
//                             @return: data.back,
//                             mix:   data.skirtIkMix,
//                             radius: 0f,                  // クランプ無し
//                             respectFlipXPerIK: false,    // スカートは前後なので左右Flip無し
//                             offsetLocal: data.skirtOffsetLocal, // 「少し上へ」など
//                             offsetInSkeletonSpace: true,        // 画面基準寄りで動かす
//                             toCurveOverride: data.toAimCurve,
//                             returnCurveOverride: data.returnCurve
//                         );
//                     }

//                     // （任意）現在ダメージを公開（Hitbox側で参照したい場合）
//                     currentDamage = data.damage;
//                 }
//                 else
//                 {
//                     // フォールバック（MoveData 未設定時は、とりあえずIK無し or ログのみ）
//                     Debug.LogWarning($"[LimbAttackController] MoveSet に {limb.Value} 用の AttackMoveData が設定されていません。IK制御はスキップします。");
//                 }
//             }

//             // ④ 入力ロック（技データ優先、無ければフォールバック）
//             float lockDur;
//             if (data != null) {
//                 lockDur = (data.lockOverride > 0f)
//                     ? data.lockOverride
//                     : (data.toAim + data.hold + data.back);
//             } else {
//                 lockDur = (lockDurationOverride > 0f)
//                     ? lockDurationOverride
//                     : (toAim + hold + @return);
//             }
//             StartCoroutine(LockFor(lockDur));
//         }

//         // ← 押下フラグを1フレームで消費
//         inputs.ConsumeFrameButtons();
//     }

//     IEnumerator LockFor(float t){
//         locked = true;
//         yield return new WaitForSeconds(t);
//         locked = false;
//     }
// }
