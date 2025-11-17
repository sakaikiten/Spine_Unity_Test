using UnityEngine;
using Spine.Unity;
using System.Collections;

//後でここを Spineのアニメーションイベント（StartActive/StopActive）に置き換えますが、まずは時間指定で当たりを通す方が確実です。

[RequireComponent(typeof(FighterInputs))]
public class SimpleAttack : MonoBehaviour {
    public SkeletonAnimation skeleton;
    public string lightAnimName = "light_punch"; // あなたのSpine名に置換
    public Hitbox lightHitbox;                   // 拳のHitboxを割当
    public float startup = 0.08f; // ため時間
    public float active  = 0.10f; // 当たり時間
    public float recovery= 0.18f; // もどし時間

    FighterInputs inputs;
    bool attacking;

    void Awake(){
        inputs = GetComponent<FighterInputs>();
        if (!skeleton) skeleton = GetComponentInChildren<SkeletonAnimation>();
        if (lightHitbox) lightHitbox.active = false;
    }

    void Update(){
        if (attacking) { inputs.ConsumeFrameButtons(); return; }
        // if (inputs.LightPressed){
        if (inputs.LeftPunchPressed){    
            StartCoroutine(DoLight());
            inputs.ConsumeFrameButtons();
        }
    }

    //コルーチン
    IEnumerator DoLight(){
        attacking = true;
        // アニメ（任意）：Spine名があれば再生
        if (skeleton && !string.IsNullOrEmpty(lightAnimName))
            skeleton.AnimationState.SetAnimation(1, lightAnimName, false); // 1番トラックで再生(0は移動)

        //yield return によってこの秒数だけ待機。
        // Startup
        yield return new WaitForSeconds(startup);
        // Active
        // if (lightHitbox) lightHitbox.active = true;
        yield return new WaitForSeconds(active);
        // if (lightHitbox) lightHitbox.active = false;
        // Recovery
        yield return new WaitForSeconds(recovery);
        attacking = false;
    }
}
