using UnityEngine;
using Spine.Unity;

[RequireComponent(typeof(FighterInputs))]
[RequireComponent(typeof(Rigidbody2D))]
public class FighterLocomotion : MonoBehaviour {
    [Header("Refs")]
    public SkeletonAnimation skeleton;

    Rigidbody2D rb;
    FighterInputs inputs;

    [Header("Settings")]
    public float moveSpeed = 4f;
    public string idleAnimationName = "idle";
    public string walkAnimationName = "walk";

    bool facingRight = true;
    public bool FacingRight => facingRight;

    [Header("Control Flags (FCM から制御)")]
    public bool canMove = true;              // false の間は横移動しない
    public bool allowAutoIdleWalkAnim = true; // false の間は idle/walk 自動切替しない

    void Awake() {
        inputs = GetComponent<FighterInputs>();
        rb = GetComponent<Rigidbody2D>();
        if (!skeleton) skeleton = GetComponentInChildren<SkeletonAnimation>();
    }

    void Start() {
        // 起動時は idle ループ
        if (skeleton && !string.IsNullOrEmpty(idleAnimationName))
            skeleton.AnimationState.SetAnimation(0, idleAnimationName, true);
    }

    void Update() {
        // 向き反転（移動禁止中でも向きは変えておいてOK）
        if (inputs.Move.x > 0.05f) facingRight = true;
        else if (inputs.Move.x < -0.05f) facingRight = false;

        transform.localScale = new Vector3(facingRight ? 1f : -1f, 1f, 1f);

        // idle/walk の自動アニメ切替が禁止されているならここで終わり
        if (!allowAutoIdleWalkAnim || skeleton == null) return;

        bool walking = Mathf.Abs(inputs.Move.x) > 0.05f && canMove;
        var current = skeleton.AnimationState.GetCurrent(0)?.Animation?.Name;

        string want = walking ? walkAnimationName : idleAnimationName;
        if (!string.IsNullOrEmpty(want) && current != want)
            skeleton.AnimationState.SetAnimation(0, want, true);
    }

    void FixedUpdate() {
        if (!canMove) {
            // 移動禁止中は横だけ止める（縦方向は重力などに任せる）
            rb.linearVelocity = new Vector2(0f, rb.linearVelocity.y);
            return;
        }

        rb.linearVelocity = new Vector2(inputs.Move.x * moveSpeed, rb.linearVelocity.y);
    }
}
