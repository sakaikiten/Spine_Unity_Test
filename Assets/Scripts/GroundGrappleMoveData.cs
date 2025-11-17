using UnityEngine;

// GroundGrappleMoveData：個々の寝技（袈裟固めA・横四方固めB…）の設定

[CreateAssetMenu(menuName = "Grapple/Ground Grapple Move")]
public class GroundGrappleMoveData : ScriptableObject
{
    [Header("ID / Name")]
    [Tooltip("内部ID（スクリプトから参照したい場合など）")]
    public string moveId;

    [Tooltip("インスペクタ上でわかりやすい表示名")]
    public string displayName = "Kesa Gatame 1";

    [Header("Entry Positioning")]
    [Tooltip("相手スケルトン側のエントリーボーン名（例: hip, chest, head など）")]
    public string targetEntryBoneName = "hip";

    [Tooltip("相手ボーンのワールド位置から見た攻撃側の理想オフセット（ユニット）")]
    public Vector2 attackerOffsetFromTarget = new Vector2(-0.6f, 0.0f);

    [Tooltip("向きに応じて X オフセットを自動反転するか（true 推奨）")]
    public bool mirrorOffsetByFacing = true;

    [Header("Base Anim Settings (後で拡張)")]
    [Tooltip("攻撃側のエントリー用アニメーションステート名（Animator など）")]
    public string attackerEntryAnimState;

    [Tooltip("受け側のエントリー用アニメーションステート名")]
    public string defenderEntryAnimState;

    [Tooltip("アニメーションの基準速度（1.0 = 等速）")]
    public float baseAnimSpeed = 1.0f;

    [Tooltip("前のアニメからエントリーアニメへクロスフェードする時間（秒）。0なら即切り替え。")]
    public float entryMixDuration = 0.15f;


    [System.Serializable]
    public class BezierIKTrack
    {
        [Tooltip("攻撃側の IK 名（Spine の IK Constraint 名）")]
        public string ikName;

        [Tooltip("この IK の Mix（効き具合）")]
        [Range(0f, 1f)]
        public float ikMix = 1.0f;

        [Header("Defender Bones (3 points for quadratic Bezier)")]
        [Tooltip("ベジェ開始点 P0 に使う防御側ボーン名")]
        public string p0BoneName = "hip";

        [Tooltip("ベジェ制御点 P1 に使う防御側ボーン名")]
        public string p1BoneName = "spine";

        [Tooltip("ベジェ終点 P2 に使う防御側ボーン名")]
        public string p2BoneName = "head";

        [Header("Motion")]
        [Tooltip("片道（0→1）の時間（秒）")]
        public float moveDuration = 1.5f;

        [Tooltip("0→1→0…で往復させるなら true")]
        public bool pingPong = true;

        [Tooltip("0〜1 の時間に対する進捗カーブ")]
        public AnimationCurve easeCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

        [Tooltip("アニメ開始時の位相オフセット（秒）。IKごとに動きのタイミングをズラす用。")]
        public float timeOffset = 0f;
    }

    [Header("Bezier IK Tracks (攻撃側IK→防御側3ボーン)")]
    public BezierIKTrack[] bezierIKTracks;

    [Header("Transition Settings")]
    [Tooltip("この寝技から遷移しうる候補たち")]
    public GroundGrappleTransitionSlot[] transitions;

    public class GroundGrappleTransitionSlot
    {
        [Tooltip("遷移先の寝技")]
        public GroundGrappleMoveData nextMove;

        [Tooltip("重み。これらの合計から確率を計算します")]
        public float weight = 1f;

        [Tooltip("この遷移先が有効になる条件")]
        public GroundGrappleTransitionCondition condition;
    }

    public class GroundGrappleTransitionCondition
    {
        [Header("時間条件")]
        [Tooltip("この寝技に入ってからの経過時間が min〜max の間ならOK（秒）")]
        public bool useElapsedTimeRange = false;
        public float minElapsedTime = 0f;
        public float maxElapsedTime = 9999f;

        [Header("攻撃側 HP 条件（0〜1で指定）")]
        public bool useAttackerHpRange = false;
        public float attackerHpMin = 0f;
        public float attackerHpMax = 1f;

        [Header("防御側 HP 条件（0〜1で指定）")]
        public bool useDefenderHpRange = false;
        public float defenderHpMin = 0f;
        public float defenderHpMax = 1f;

        [Header("デバフレベルなどの簡易条件")]
        public bool useAttackerDebuffMin = false;
        public int attackerDebuffMin = 0;

        public bool useDefenderDebuffMin = false;
        public int defenderDebuffMin = 0;

        // 実際のチェック処理は ScriptableObject 内に書かず、
        // ランタイムクラス側から呼んでもらう想定にしておく。
        // （FCM のフィールド名に依存するため）
    }


}

