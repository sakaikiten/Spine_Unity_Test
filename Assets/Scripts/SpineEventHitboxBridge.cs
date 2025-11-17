// using UnityEngine;
// using Spine.Unity;
// using System.Collections.Generic;

// public class SpineEventHitboxBridge : MonoBehaviour {
//     [Header("Refs")]
//     public SkeletonAnimation skeleton;

//     [Header("Limb Hitboxes")]
//     public Hitbox HB_RHand, HB_LHand, HB_RFoot, HB_LFoot;

//     Dictionary<string, Hitbox> map;

//     void Awake() {
//         if (!skeleton) skeleton = GetComponentInChildren<SkeletonAnimation>();
//         map = new Dictionary<string, Hitbox> {
//             {"RHand", HB_RHand},
//             {"LHand", HB_LHand},
//             {"RFoot", HB_RFoot},
//             {"LFoot", HB_LFoot}
//         };
//         skeleton.AnimationState.Event += OnSpineEvent;
//         // 念のため全OFF
//         foreach (var kv in map) if (kv.Value) kv.Value.active = false;
//     }

// // スクリプト削除時にイベント登録を解除しておきます。
// // （これを忘れると、削除後もイベントが飛んでエラーになる可能性があります）
//     void OnDestroy() {
//         if (skeleton) skeleton.AnimationState.Event -= OnSpineEvent;
//     }

//     void OnSpineEvent(Spine.TrackEntry entry, Spine.Event e) {

//         // 例: HB_ON_RHand / HB_OFF_LFoot
//         string name = e.Data.Name; // "HB_ON_RHand"
//         if (!name.StartsWith("HB_")) return;

//         bool turnOn = name.Contains("_ON_");
//         string limb = name.EndsWith("RHand") ? "RHand" :
//                       name.EndsWith("LHand") ? "LHand" :
//                       name.EndsWith("RFoot") ? "RFoot" :
//                       name.EndsWith("LFoot") ? "LFoot" : null;


//         if (limb != null && map.TryGetValue(limb, out var hb) && hb) {
//             hb.active = turnOn;
//         }
//     }
// }

using UnityEngine;
using Spine.Unity;
using System.Collections.Generic;

public class SpineEventHitboxBridge : MonoBehaviour {
    [Header("Refs")]
    public SkeletonAnimation skeleton;

    [Header("Limb Hitboxes")]
    public Hitbox HB_RHand, HB_LHand, HB_RFoot, HB_LFoot;

    Dictionary<string, Hitbox> map;

    void Awake() {
        //skeleton が未設定なら、子オブジェクトから自動で探します。
        if (!skeleton) skeleton = GetComponentInChildren<SkeletonAnimation>();
        // イベント名（Spine上で打つ名前）と対応するHitboxを登録。
        // "HB_RHand" イベントが来たら HB_RHand のHitboxを操作する、という対応表。
        map = new Dictionary<string, Hitbox> {
            {"HB_RHand", HB_RHand},
            {"HB_LHand", HB_LHand},
            {"HB_RKick", HB_RFoot},
            {"HB_LKick", HB_LFoot}
        };
        //Spineのアニメーション再生中にイベントが発生したら、OnSpineEvent() が呼ばれるように登録
        skeleton.AnimationState.Event += OnSpineEvent;

        // シーン開始時に安全のため、すべてのHitboxを無効化。
        foreach (var kv in map)
            if (kv.Value) kv.Value.active = false;
    }

    //オブジェクトが破棄されるときにイベント購読を解除。
    //解除しないと、Spineからのイベント発火時に「参照が切れたオブジェクト」を呼び出そうとしてエラーが出ることがあり
    void OnDestroy() {
        if (skeleton)
            skeleton.AnimationState.Event -= OnSpineEvent;
    }

    void OnSpineEvent(Spine.TrackEntry entry, Spine.Event e) {
        // 例: HB_RHand (float=1.0) or HB_RHand (float=0.0)
        string name = e.Data.Name;

        //辞書に存在しない場合（例：他のイベント）は無視。
        if (!map.TryGetValue(name, out var hb) || hb == null)
            return;

        // float値でON/OFFを判断（1.0以上→ON、0.5未満→OFF）
        bool turnOn = e.Float >= 0.5f;
        hb.active = turnOn;
    }
}
