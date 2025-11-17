using UnityEngine;
using Spine.Unity;

[RequireComponent(typeof(FighterInputs))]
[RequireComponent(typeof(Rigidbody2D))]
public class SimpleFighterController : MonoBehaviour {
    [Header("Refs")]
    public SkeletonAnimation skeleton;

    Rigidbody2D rb;
    FighterInputs inputs;

    [Header("Settings")]
    public float moveSpeed = 4f;
    public string idleAnimationName = "idle";  // あなたのSpineの名前に合わせて
    public string walkAnimationName = "walk";  // 同上

    bool facingRight = true;
    public bool FacingRight => facingRight;

    void Awake() {
        inputs = GetComponent<FighterInputs>();
        rb = GetComponent<Rigidbody2D>();
        if (!skeleton) skeleton = GetComponentInChildren<SkeletonAnimation>();
    }

    void Start() {
        // 起動時はidleループ
        if (skeleton && !string.IsNullOrEmpty(idleAnimationName))
            skeleton.AnimationState.SetAnimation(0, idleAnimationName, true);
    }

    void Update() {
        // 左右の向き反転（x入力の符号で決定）
        if (inputs.Move.x > 0.05f) facingRight = true;
        else if (inputs.Move.x < -0.05f) facingRight = false;

        // 見た目をフリップ
        transform.localScale = new Vector3(facingRight ? 1f : -1f, 1f, 1f);

        // アニメ切替（idle/walk だけ）
        bool walking = Mathf.Abs(inputs.Move.x) > 0.05f;
        //　現在のアニメーションを取得
        var current = skeleton.AnimationState.GetCurrent(0)?.Animation?.Name;
        //　skeleton が存在する＋want（再生したいアニメ名）が空でない＋今の current と違う（＝同じアニメを何度もセットしない）でアニメを切り替える
        string want = walking ? walkAnimationName : idleAnimationName;
        if (skeleton && !string.IsNullOrEmpty(want) && current != want)
            skeleton.AnimationState.SetAnimation(0, want, true);
    }

    void FixedUpdate() {
        // 水平移動のみ（重力は使う場合に Project Settings で有効）
        rb.linearVelocity = new Vector2(inputs.Move.x * moveSpeed, rb.linearVelocity.y);
    }
}
