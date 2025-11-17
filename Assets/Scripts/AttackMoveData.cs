// AttackMoveData.cs
using UnityEngine;
using static SpineIKMouseAimer;

[CreateAssetMenu(menuName="Fighter/Attack Move Data", fileName="NewAttackMove")]
public class AttackMoveData : ScriptableObject {

    [Header("IK Constraint Names (向きで切替)")]
    [Tooltip("右向き時に操作するIK名（例: arm_front_hand_IK）")]
    public string ikFront; // 右向き時に“手前側(Front)”のIKを操作
    [Tooltip("左向き時に操作するIK名（例: arm_back_hand_IK）")]
    public string ikBack;  // 左向き時に“奥側(Back)” のIKを操作

    [Header("Secondary IK (Skirt)")]
    public bool useSkirtIK = false;             // キック技などで有効にする

    [Tooltip("スカート端のIK名（左右反転なし・1点のみ）")]
    public string skirtIkName;
    [Tooltip("マウス狙い点の親ローカルに足すオフセット。少し上へなら (0, +0.2) など")]
    public Vector2 skirtOffsetLocal = new Vector2(0f, 0.2f);
    [Range(0,1)]
    public float skirtIkMix = 0.8f;             // 主IKより弱めにすると“ふわっ”と見える

    [Header("Timings / Mix")]
    public float toAim = 0.08f;
    public float hold  = 0.10f;
    public float back  = 0.12f;
    [Range(0, 1)] public float ikMix = 1.0f;
    
    [Header("Angle Limit (deg, local X=0)")]
    public bool limitAngle = false;
    public float minAngleDeg = 30f;
    public float maxAngleDeg = 120f;


    [Header("Curves / Reach")]
    public AnimationCurve toAimCurve  = AnimationCurve.EaseInOut(0,0,1,1);
    public AnimationCurve returnCurve = AnimationCurve.EaseInOut(0,0,1,1);
    public float handRadius = 2.5f;
    public float footRadius = 3.0f;

    [Header("Combat")]
    public int damage = 10;

    [Header("Lock")]
    public float lockOverride = 0f; // <=0 なら toAim+hold+back
}
