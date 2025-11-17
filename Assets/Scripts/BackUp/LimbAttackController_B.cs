
// using UnityEngine;
// using Spine.Unity;
// using System.Collections;
// using static SpineIKMouseAimer;

// [RequireComponent(typeof(FighterInputs))]
// public class LimbAttackController : MonoBehaviour {
//     public SkeletonAnimation skeleton;

//     // SpineIKMouseAimer 参照
//     SpineIKMouseAimer aimer;


//     [Header("Anim Names")]
//     public string punch_R = "punch_r"; // Spine側の実名に合わせて
//     public string punch_L = "punch_l";
//     public string kick_R  = "kick_r";
//     public string kick_L  = "kick_l";

//     [Header("Mapping Mode")]
//     public bool useLeadSideMapping = true; // true=前手/前足優先, false=解剖学固定

//     [Header("Lock Timing")]
//     [Tooltip("0 の場合は (toAim + hold + return) を自動で使用")]
//     public float lockDurationOverride = 0f;

//     [Header("Programmatic IK Phase (SpineIKMouseAimer に合わせる)")]
//     [Tooltip("“狙いへ移動”の所要秒数")]
//     public float toAim = 0.08f;
//     [Tooltip("“狙い保持”の所要秒数")]
//     public float hold = 0.10f;
//     [Tooltip("“元へ戻る”の所要秒数")]
//     public float @return = 0.12f;
//     [Range(0f,1f)]
//     [Tooltip("攻撃中のIK強度（1で完全IK）")]
//     public float ikMix = 1.0f;

//     FighterInputs inputs;
//     SimpleFighterController mover; // FacingRight をもらうため（任意）
//     bool locked;

//     void Awake(){
//         inputs = GetComponent<FighterInputs>();
//         mover  = GetComponent<SimpleFighterController>(); // 無ければ null でもOK
//         if (!skeleton) skeleton = GetComponentInChildren<SkeletonAnimation>();
//         aimer = GetComponent<SpineIKMouseAimer>();
//         if (!aimer) Debug.LogWarning("[LimbAttackController] SpineIKMouseAimer が見つかりません。IK制御は行われません。");
//     }

//     void Update(){
//         // 連続攻撃入力をロック
//         if (locked) { inputs.ConsumeFrameButtons(); return; }

//         // どちら向きか
//         bool facingRight = mover ? mover.FacingRight : true; // デフォルト右向き

//         // 技の決定
//         string animToPlay = null;
//         Limb? limb = null;

//         // if (useLeadSideMapping)
//         // {
//             // ① リード側優先：左クリック＝常に“前手”
//             // 左クリック＝常に前手/前足（リード側）に割り当て
//             if (inputs.RightKickPressed)
//             {          // Shift+左クリック
//                 animToPlay = facingRight ? kick_R : kick_L;
//                 limb = facingRight ? Limb.RFoot : Limb.LFoot;
//             }
//             else if (inputs.LeftKickPressed)
//             {     // Shift+右クリック
//                 animToPlay = facingRight ? kick_L : kick_R;
//                 limb = facingRight ? Limb.LFoot : Limb.RFoot;
//             }
//             else if (inputs.RightPunchPressed)
//             {   // 左クリック
//                 animToPlay = facingRight ? punch_R : punch_L;
//                 limb = facingRight ? Limb.RHand : Limb.LHand;
//             }
//             else if (inputs.LeftPunchPressed)
//             {    // 右クリック
//                 animToPlay = facingRight ? punch_L : punch_R;
//                 limb = facingRight ? Limb.LHand : Limb.RHand;
//             }

//         // }
//         // else
//         // {
//         //     // ② 解剖学固定：左クリック＝常に右（向きに左右されない）
//         //     if (inputs.RightKickPressed) animToPlay = kick_R;  // 左クリック+Shift
//         //     else if (inputs.LeftKickPressed) animToPlay = kick_L;  // 右クリック+Shift
//         //     else if (inputs.RightPunchPressed) animToPlay = punch_R; // 左クリック
//         //     else if (inputs.LeftPunchPressed) animToPlay = punch_L; // 右クリック
//         // }

//         if (!string.IsNullOrEmpty(animToPlay)) {
//             // 1) アニメ再生（Track 1：攻撃）
//             if (skeleton)
//                 skeleton.AnimationState.SetAnimation(1, animToPlay, false);

//             // 2) IKプログラム制御（狙い→保持→戻り）
//             if (aimer != null && limb.HasValue) {
//                 aimer.AimProgrammatic(limb.Value, toAim, hold, @return, ikMix);
//             }

//             // 3) 入力ロック（合計時間 or 明示オーバーライド）
//             float lockDur = (lockDurationOverride > 0f) ? lockDurationOverride : (toAim + hold + @return);
//             StartCoroutine(LockFor(lockDur));
//         }

//         // 1フレーム消費
//         inputs.ConsumeFrameButtons();
//     }

//     IEnumerator LockFor(float t){
//         locked = true;
//         yield return new WaitForSeconds(t);
//         locked = false;
//     }
// }

