using UnityEngine;

[RequireComponent(typeof(FighterInputs))]
[RequireComponent(typeof(FighterCoreManager))]
public class AIFighterController : MonoBehaviour {
    public Transform target;           // 狙う相手（プレイヤーの Transform）
    public float approachDistance = 2f; // この距離まで近づいたら攻撃
    public float walkSpeed = 1f;        // 近づくときの歩き入力の強さ (0〜1)
    public float attackInterval = 1.5f; // 何秒ごとに攻撃してよいか

    FighterInputs inputs;
    FighterCoreManager core;
    float attackTimer;

    void Awake() {
        inputs = GetComponent<FighterInputs>();
        core   = GetComponent<FighterCoreManager>();
    }

    void Update() {
        if (!target) {
            // ターゲットがいなければ何もしない
            inputs.Move = Vector2.zero;
            return;
        }

        // 自分とターゲットの距離・方向を求める
        float dx = target.position.x - transform.position.x;
        float absDx = Mathf.Abs(dx);
        float dir = Mathf.Sign(dx); // +1: 右に敵, -1: 左に敵

        // まず移動入力
        if (core.State == FighterCoreManager.FighterState.Idle ||
            core.State == FighterCoreManager.FighterState.Walk) {

            if (absDx > approachDistance) {
                // まだ遠い → 近づく
                inputs.Move = new Vector2(dir * walkSpeed, 0f);
            } else {
                // だいたい十分な距離 → 足を止める
                inputs.Move = Vector2.zero;
            }
        } else {
            // それ以外の状態（ダウン中・攻撃中など）は自力移動しない
            inputs.Move = Vector2.zero;
        }

        // 攻撃のクールダウン管理
        attackTimer -= Time.deltaTime;
        if (attackTimer <= 0f &&
            absDx <= approachDistance + 0.5f && // 近づいたら殴る
            (core.State == FighterCoreManager.FighterState.Idle ||
             core.State == FighterCoreManager.FighterState.Walk)) {

            // とりあえず右パンチだけ
            inputs.RightPunchPressed = true;

            attackTimer = attackInterval;
        }
    }
}
