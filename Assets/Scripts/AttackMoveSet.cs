using UnityEngine;
using static SpineIKMouseAimer;

[CreateAssetMenu(menuName="Fighter/Attack Move Set", fileName="NewAttackMoveSet")]
public class AttackMoveSet : ScriptableObject {
    [Header("Punch")]
    public AttackMoveData rightHandPunch;  // 右手パンチ
    public AttackMoveData leftHandPunch;   // 左手パンチ

    [Header("Kick")]
    public AttackMoveData rightFootKick;   // 右足キック
    public AttackMoveData leftFootKick;    // 左足キック

    public AttackMoveData GetForLimb(Limb limb) {
        switch (limb) {
            case Limb.RHand: return rightHandPunch;
            case Limb.LHand: return leftHandPunch;
            case Limb.RFoot: return rightFootKick;
            case Limb.LFoot: return leftFootKick;
            default: return null;
        }
    }
}
