
using UnityEngine;
using Spine;
using Spine.Unity;

[RequireComponent(typeof(FighterLocomotion))]
[RequireComponent(typeof(FighterInputs))]
[RequireComponent(typeof(LimbAttackController))]  // ★ 追加：攻撃実行役
public class FighterCoreManager : MonoBehaviour {

    public enum FighterState {
        Idle,
        Walk,
        Guard,
        Attack,
        Down,
        Grabbed,
        GroundGrapple_Attacker,// 寝技をかけている側
        GroundGrapple_Defender // 寝技をかけられている側
    }

    [Header("Refs")]
    public FighterLocomotion locomotion;
    public SkeletonAnimation skeleton;
    public FighterInputs inputs;
    public LimbAttackController limbAttack;   // ★ 追加

    [Header("Animation Names")]
    public string guardAnimationName   = "guard";
    public string downAnimationName    = "down";
    public string grabbedLoopAnimationName = "grabbed";

    [Header("Grapple / Ground")]
    public GroundGrappleAnimator groundGrappleAnimator;
    public GroundGrappleIKBezierMulti groundGrappleIKBezierMulti;

    [Header("Debug")]
    public bool debugFreezeFSM = false;   // true の間は HandleNeutral などを実行しない

    public FighterState State { get; private set; } = FighterState.Idle;

    // 状態チェックのためのショートカット
    public bool IsDown => State == FighterState.Down;
    public bool IsGroundGrappleAttacker => State == FighterState.GroundGrapple_Attacker;
    public bool IsGroundGrappleDefender => State == FighterState.GroundGrapple_Defender;


    public enum AttackKind {
        None,
        RightPunch,
        LeftPunch,
        RightKick,
        LeftKick,
    }
    public AttackKind CurrentAttackKind { get; private set; } = AttackKind.None;



    void Awake() {
        if (!locomotion) locomotion = GetComponent<FighterLocomotion>();
        if (!inputs)     inputs     = GetComponent<FighterInputs>();
        if (!skeleton)   skeleton   = locomotion ? locomotion.skeleton : GetComponentInChildren<SkeletonAnimation>();
        if (!limbAttack) limbAttack = GetComponent<LimbAttackController>(); // ★ 追加
        if (!groundGrappleAnimator) groundGrappleAnimator = GetComponent<GroundGrappleAnimator>();    
        if (!groundGrappleIKBezierMulti)
        groundGrappleIKBezierMulti = GetComponent<GroundGrappleIKBezierMulti>();
    }

    void Update() {
        if (!debugFreezeFSM)
        {
            switch (State)
            {
                case FighterState.Idle:
                case FighterState.Walk:
                    HandleNeutral();
                    break;

                case FighterState.Guard:
                    HandleGuard();
                    break;

                case FighterState.Attack:
                    // 攻撃中の細かい処理があればここに
                    break;

                case FighterState.Down:
                    // ここで起き上がり入力などを見る想定
                    break;

                case FighterState.Grabbed:
                    // 拘束中。基本動けない・攻撃不可
                    break;
            }
        }

        // 1フレーム系のボタンフラグをリセット
        if (inputs != null) {
            inputs.ConsumeFrameButtons();
        }
    }

    // ニュートラル (Idle / Walk)
    void HandleNeutral() {
        // ガード優先
        if (inputs.GuardPressed) {
            EnterGuard();
            return;
        }

        // どの攻撃ボタンが押されたかをチェック
        AttackKind requested = AttackKind.None;

        // ★ キックを先に判定する
        if (inputs.RightKickPressed)      requested = AttackKind.RightKick;
        else if (inputs.LeftKickPressed)  requested = AttackKind.LeftKick;
        else if (inputs.RightPunchPressed) requested = AttackKind.RightPunch;
        else if (inputs.LeftPunchPressed)  requested = AttackKind.LeftPunch;

        if (requested != AttackKind.None) {
            StartAttack(requested);
            return;
        }

        // 移動量に応じて Idle / Walk 切り替え（論理上の状態として）
        float moveX = inputs.Move.x;
        if (Mathf.Abs(moveX) > 0.05f)
            State = FighterState.Walk;
        else
            State = FighterState.Idle;

        locomotion.canMove = true;
        locomotion.allowAutoIdleWalkAnim = true;
    }

    void HandleGuard()
    {
        // 今はシンプルに「その場ガード」として移動禁止にしておく
        locomotion.canMove = false;
        locomotion.allowAutoIdleWalkAnim = false;

        if (skeleton && !string.IsNullOrEmpty(guardAnimationName))
        {
            var current = skeleton.AnimationState.GetCurrent(0)?.Animation?.Name;
            if (current != guardAnimationName)
                skeleton.AnimationState.SetAnimation(0, guardAnimationName, true);
        }

        // ガードボタンを離したらニュートラルへ
        if (!inputs.GuardHeld)
        {
            State = FighterState.Idle;
            locomotion.canMove = true;
            locomotion.allowAutoIdleWalkAnim = true;
        }
    }

    // ★★★★★ 攻撃開始：ここを LimbAttackController 連携に変更 ★★★★★
    //ゆくゆくはアイドル状態でのパンチと、走り状態でのパンチ、つかまれた状態でのパンチをそれぞれ処理を分けたい
    public void StartAttack(AttackKind kind) {
        if (State == FighterState.Attack ||
            State == FighterState.Down ||
            State == FighterState.Grabbed) return;

        if (!limbAttack) return;

        State = FighterState.Attack;
        CurrentAttackKind = kind;

        locomotion.canMove = false;
        locomotion.allowAutoIdleWalkAnim = false;

        // ここで直接 Spine にアニメをセットする代わりに、
        // LimbAttackController に技の実行を依頼する
        limbAttack.PlayAttack(kind, OnAttackAnimComplete);
    }

    // ★ GetAttackAnimationName は LimbAttackController 側に移したので不要になった
    // string GetAttackAnimationName(AttackKind kind) { ... } は削除

    // ★ LimbAttackController から攻撃完了コールバックを受ける
    void OnAttackAnimComplete() {
        if (State != FighterState.Attack) return;

        CurrentAttackKind = AttackKind.None;
        State = FighterState.Idle;
        locomotion.canMove = true;
        locomotion.allowAutoIdleWalkAnim = true;
    }

    // ダウンに入る
    public void EnterDown() {
        State = FighterState.Down;
        locomotion.canMove = false;
        locomotion.allowAutoIdleWalkAnim = false;

        if (skeleton && !string.IsNullOrEmpty(downAnimationName)) {
            skeleton.AnimationState.SetAnimation(0, downAnimationName, true);
        }
    }

    // 起き上がり（とりあえず即復帰用のAPI）
    public void StandUp() {
        State = FighterState.Idle;
        locomotion.canMove = true;
        locomotion.allowAutoIdleWalkAnim = true;
    }

    // 拘束開始（投げられている側など）
    public void EnterGrabbed() {
        State = FighterState.Grabbed;
        locomotion.canMove = false;
        locomotion.allowAutoIdleWalkAnim = false;

        if (skeleton && !string.IsNullOrEmpty(grabbedLoopAnimationName)) {
            skeleton.AnimationState.SetAnimation(0, grabbedLoopAnimationName, true);
        }
    }

    // 拘束解除
    public void ReleaseGrabbed() {
        State = FighterState.Idle;
        locomotion.canMove = true;
        locomotion.allowAutoIdleWalkAnim = true;
    }

    // 防御状態に入る
    void EnterGuard()
    {
        State = FighterState.Guard;
        locomotion.canMove = false;
        locomotion.allowAutoIdleWalkAnim = false;
    }

    // ==== 寝技用の状態遷移API ====

    // 旧API：Moveデータなし版（既存コードとの互換用）
    public void EnterGroundGrappleAsAttacker(FighterCoreManager defender) {
        // 中身は新バージョンに丸投げ（move = null）
        EnterGroundGrappleAsAttacker(defender, null);
    }

    // 新API：寝技データを渡せる版
    public void EnterGroundGrappleAsAttacker(
        FighterCoreManager defender,
        GroundGrappleMoveData move
    ) {
        // 自分をアタッカー状態に
        State = FighterState.GroundGrapple_Attacker;
        locomotion.canMove = false;
        locomotion.allowAutoIdleWalkAnim = false;

        // 相手をディフェンダー状態に（同じ move を渡す）
        if (defender != null) {
            defender.EnterGroundGrappleAsDefender(move);
        }

        // ★ 攻め側アニメ再生（あれば）
        if (groundGrappleAnimator != null) {
            groundGrappleAnimator.PlayEntryAnimation(move, true);
        }
        // ★ IK ベジェを開始
        if (groundGrappleIKBezierMulti != null && defender != null && defender.skeleton != null) {
            groundGrappleIKBezierMulti.BeginGrapple(move, defender.skeleton);
        }
    }


    // 旧API：Move データなし版（内部からも呼ばれているかもなので残す）
    public void EnterGroundGrappleAsDefender() {
        EnterGroundGrappleAsDefender(null);
    }

    // 新API：Move データ付き
    public void EnterGroundGrappleAsDefender(GroundGrappleMoveData move) {
        State = FighterState.GroundGrapple_Defender;
        locomotion.canMove = false;
        locomotion.allowAutoIdleWalkAnim = false;

        // ★ 受け側アニメ再生（あれば）
        if (groundGrappleAnimator != null) {
            groundGrappleAnimator.PlayEntryAnimation(move, false);
        }
    }


    // 寝技を終了する（とりあえず全員 Down に戻す例）
    public void ExitGroundGrappleToDown() {
        State = FighterState.Down;
        locomotion.canMove = false;
        locomotion.allowAutoIdleWalkAnim = false;

        if (skeleton && !string.IsNullOrEmpty(downAnimationName)) {
            skeleton.AnimationState.SetAnimation(0, downAnimationName, true);
        }
    }

    

    // ★ デバッグ用に、任意のステートへ強制遷移するAPI
    public void ForceStateDebug(FighterState target) {
        switch (target) {
            case FighterState.Idle:
                // ニュートラル状態へ戻す
                CurrentAttackKind = AttackKind.None;
                State = FighterState.Idle;
                locomotion.canMove = true;
                locomotion.allowAutoIdleWalkAnim = true;

                if (skeleton && !string.IsNullOrEmpty(locomotion.idleAnimationName)) {
                    skeleton.AnimationState.SetAnimation(0, locomotion.idleAnimationName, true);
                }
                break;

            case FighterState.Walk:
                // ちょっと歩き状態にしておきたい場合（実際には Move 入力がないと止まる）
                CurrentAttackKind = AttackKind.None;
                State = FighterState.Walk;
                locomotion.canMove = true;
                locomotion.allowAutoIdleWalkAnim = true;
                break;

            case FighterState.Guard:
                EnterGuard();
                break;

            case FighterState.Attack:
                // 適当な攻撃を強制再生（ここでは右パンチ）
                StartAttack(AttackKind.RightPunch);
                break;

            case FighterState.Down:
                EnterDown();
                break;

            case FighterState.Grabbed:
                EnterGrabbed();
                break;

            case FighterState.GroundGrapple_Attacker:
                EnterGroundGrappleAsAttacker(null); // 相手なしで自分だけアタッカー状態にするなら
                break;

            case FighterState.GroundGrapple_Defender:
                EnterGroundGrappleAsDefender();
                break;
        }
    }


}

