// using UnityEngine;
// #if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
// using UnityEngine.InputSystem;
// #endif
// using Spine;
// using Spine.Unity;
// using System.Collections;
// using System.Collections.Generic;

// /// <summary>
// /// 攻撃中だけ指定のIK（手/足）を有効化し、ターゲットボーンをマウス位置へ移動させる。
// /// ・IK Mixは攻撃中に1.0へ、終了後は0へ
// /// ・Flip対応（ScaleX）
// /// ・半径クランプ＆スムージングで暴れにくく

// ///　課題　マウス追従はできた　でもガタガタ。
// ///  →アニメーションとUnityの競合でかなり難しそう・・・
// ///　時間補完ではなくトリガー補完にしたいが
// /// </summary>
// [DefaultExecutionOrder(1001)]
// public class SpineIKMouseAimer : MonoBehaviour
// {
//     public SkeletonAnimation skeletonAnimation;
//     public Camera cam;
//     ISkeletonAnimation isa;

//     [Header("IK Constraint names (Spine)")]
//     public string ikRHand = "ik_r_hand";
//     public string ikLHand = "ik_l_hand";
//     public string ikRFoot = "ik_r_foot";
//     public string ikLFoot = "ik_l_foot";

//     [Header("Reach & Smoothing")]
//     public float handRadius = 2.5f;   // 親ボーン原点からの最大到達半径（Spineローカル）
//     public float footRadius = 3.0f;
//     public float followSmooth = 18f;  // 大きいほど素早い
//     public float mixSmooth = 6f;  // IKMixのなめらかさ

//     [Header("Flip")]
//     public bool respectFlipX = true;  // 左右反転時にX方向を反転解釈

//     [Header("Debug")]
//     public bool debugLogIkMix = true;
//     public bool debugDrawGizmos = true;
//     public Transform debugMarkerPrefab; // 小さいSprite等を割当て(任意)
//     Transform debugMarkerInst;

    


//     // 内部
//     Skeleton skel;
//     readonly Dictionary<Limb, IkConstraint> iks = new();
//     readonly Dictionary<Limb, Bone> targets = new();
//     Limb? activeLimb = null;
//     float remain = 0f;          // 追従を続ける残り秒数
//     Vector2 dampVel;            // 位置スムージング用

//     public enum Limb { RHand, LHand, RFoot, LFoot }

//     void Reset()
//     {
//         if (!skeletonAnimation) skeletonAnimation = GetComponentInChildren<SkeletonAnimation>();
//     }

//     void Awake()
//     {
//         if (!skeletonAnimation) skeletonAnimation = GetComponentInChildren<SkeletonAnimation>();
//         if (!cam) cam = Camera.main;
//     }

//     void OnEnable() {
//         isa = skeletonAnimation as ISkeletonAnimation;
//         if (isa != null)
//             isa.UpdateLocal += HandleUpdateLocal;
//     }

//     void OnDisable() {
//         if (isa != null)
//             isa.UpdateLocal -= HandleUpdateLocal;
//     }


//     void Start()
//     {
//         if (!skeletonAnimation) return;
//         skel = skeletonAnimation.Skeleton;
//         // それぞれ取得（見つからなくてもスルー）
//         TryAdd(Limb.RHand, ikRHand);
//         TryAdd(Limb.LHand, ikLHand);
//         TryAdd(Limb.RFoot, ikRFoot);
//         TryAdd(Limb.LFoot, ikLFoot);
//         // 初期は全部Mix 0
//         foreach (var kv in iks) kv.Value.Mix = 0f;

//         // ★ マーカー生成（任意）
//         if (debugMarkerPrefab) debugMarkerInst = Instantiate(debugMarkerPrefab);
//     }

//     void TryAdd(Limb limb, string ikName)
//     {
//         if (string.IsNullOrEmpty(ikName)) return;
//         var ik = skel.FindIkConstraint(ikName);
//         if (ik == null) { Debug.LogWarning($"[{name}] IK not found: {ikName}"); return; }
//         iks[limb] = ik;
//         targets[limb] = ik.Target; // TargetボーンはIKから取得
//     }

//     // 旧/新Input両対応でマウスのスクリーン座標を取得
//     Vector3 GetMouseScreenPos()
//     {
// #if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
//         var mouse = Mouse.current;
//         if (mouse == null) return Vector3.zero;
//         Vector2 p = mouse.position.ReadValue();
//         return new Vector3(p.x, p.y, 0f);
// #else
//                 return Input.mousePosition;
// #endif
//     }

//     // 透視カメラでも正しいワールド変換ができるよう、スケルトンまでのZ深度を与える
//     float GetDepthFromCameraToSkeleton()
//     {
//         var wpos = skeletonAnimation ? skeletonAnimation.transform.position : Vector3.zero;
//         // カメラ座標系でのZの絶対値
//         return Mathf.Abs(cam.worldToCameraMatrix.MultiplyPoint(wpos).z);
//     }

//     void LateUpdate()
//     {
//         if (skel == null || cam == null) return;

//         // ※ ここでは IK.Mix の制御・ターゲットSetLocalPosition・remain 減算をしない！
//         // デバッグマーカー/Gizmosだけやる
//         if (debugMarkerInst && activeLimb.HasValue && targets.TryGetValue(activeLimb.Value, out var tb) && tb != null)
//         {
//             Vector3 world = LocalBoneToWorld(tb);
//             debugMarkerInst.position = world;
//         }
//     }



//     // 追加：デバッグトグル
// [SerializeField] bool dbgBypassPositionSmoothing = false; // trueなら補間なしで直書き
// Vector3 dbgLastMouseWorld; // Gizmo確認用

// void HandleUpdateLocal(ISkeletonAnimation _) {
//     if (skel == null || cam == null) return;

//     // ===== 1) Mix更新（指数補間）=====
//     float mixStep = 1f - Mathf.Exp(-mixSmooth * Time.deltaTime);
//     foreach (var kv in iks) {
//         bool isActive = activeLimb.HasValue && kv.Key == activeLimb.Value && remain > 0f;
//         float before = kv.Value.Mix;
//         float target = isActive ? 1f : 0f;
//         kv.Value.Mix = Mathf.Lerp(before, target, mixStep);

//         if (debugLogIkMix && isActive) {
//             Debug.Log($"[IKMix] limb={kv.Key} step={mixStep:F3} {before:F3}->{kv.Value.Mix:F3} remain={remain:F3}");
//         }
//     }

//     // ===== 2) ターゲット更新 =====
//     if (activeLimb.HasValue && remain > 0f) {
//         var limb = activeLimb.Value;
//         if (!iks.TryGetValue(limb, out var ik)) return;
//         var targetBone = targets[limb];
//         if (targetBone == null) return;

//         // A) スクリーン座標（新Input / 旧Input 両対応関数）
//         Vector3 s = GetMouseScreenPos();
//         if (s == Vector3.zero) {
//             if (debugLogIkMix) Debug.LogWarning("[IKPos] Mouse screen pos is zero.");
//             return;
//         }

//         // ★ LateUpdateと同じ深度の出し方を採用（差はここ）
//         s.z = GetDepthFromCameraToSkeleton(); // <= 以前は GetDepth2DOrtho()

//         // B) スクリーン→ワールド
//         Vector3 mouseWorld = cam.ScreenToWorldPoint(s);
//         dbgLastMouseWorld = mouseWorld; // Gizmoで視認したい場合用

//         // C) ワールド→スケルトン座標
//         Vector3 skelSpace = skeletonAnimation.transform.InverseTransformPoint(mouseWorld);

//         // D) 「スケルトンワールド」スケーリングを反映
//         float worldX = skelSpace.x * skel.ScaleX;
//         float worldY = skelSpace.y * skel.ScaleY;

//         // E) 親ローカルへ変換
//         var parent = targetBone.Parent ?? skel.RootBone;
//         parent.WorldToLocal(worldX, worldY, out float lx, out float ly);

//         // F) 位置補間または直書き
//         Vector2 cur = new Vector2(targetBone.X, targetBone.Y);
//         Vector2 tgt = new Vector2(lx, ly);

//         Vector2 outPos;
//         if (dbgBypassPositionSmoothing) {
//             outPos = tgt; // 補間を切って挙動確認
//         } else {
//             float posStep = 1f - Mathf.Exp(-followSmooth * Time.deltaTime);
//             outPos = Vector2.Lerp(cur, tgt, posStep);
//             if (debugLogIkMix) {
//                 Debug.Log($"[IKPos] limb={limb} posStep={posStep:F3} cur=({cur.x:F2},{cur.y:F2}) tgt=({tgt.x:F2},{tgt.y:F2}) out=({outPos.x:F2},{outPos.y:F2})");
//             }
//         }

//         targetBone.SetLocalPosition(outPos);

//         // 追加：変換の全ログ（必要なときだけでもOK）
//         if (debugLogIkMix) {
//             Debug.Log(
//                 $"[Trace] limb={limb}\n" +
//                 $"  Screen=({s.x:F1},{s.y:F1},{s.z:F2})  World=({mouseWorld.x:F3},{mouseWorld.y:F3},{mouseWorld.z:F3})\n" +
//                 $"  SkelSpace=({skelSpace.x:F3},{skelSpace.y:F3})  WorldXY=({worldX:F3},{worldY:F3})\n" +
//                 $"  Local(lx,ly)=({lx:F3},{ly:F3})  Bone.XY=({targetBone.X:F3},{targetBone.Y:F3})"
//             );
//         }
//     }

//     // ===== 3) 残り時間の更新（ここだけで減算！）=====
//     if (activeLimb.HasValue) {
//         remain -= Time.deltaTime;
//         if (remain <= 0f) activeLimb = null;
//     }
// }




//     /// <summary> 指定の手足を duration 秒だけマウス追従させる。既存のものは置き換え。 </summary>
//     public void Aim(Limb limb, float duration)
//     {
//         if (!iks.ContainsKey(limb)) return; // 該当IKがない
//         activeLimb = limb;
//         remain = Mathf.Max(0.02f, duration);
//     }


//     // 2D（Orthographic）カメラで ScreenToWorldPoint する際の z を計算
//     float GetDepth2DOrtho(Camera c, Transform skeleton)
//     {
//         if (!c) return 0f;
//         if (c.orthographic)
//             return (skeleton.position - c.transform.position).z; // 例: カメラz=-10, 骨z=0 → 10
//         // 透視の場合は従来ヘルパでもOK（念のため）
//         var wpos = skeleton.position;
//         return Mathf.Abs(c.worldToCameraMatrix.MultiplyPoint(wpos).z);
//     }


//     //ここからデバッグ
//     // Bone(ローカル)→ワールド変換のヘルパ
//     Vector3 LocalBoneToWorld(Bone bone)
//     {
//         // bone.WorldX/Y は「スケルトン座標（スケール適用後）」なので、
//         // TransformPoint に渡す前にスケールを打ち消す
//         float sx = Mathf.Approximately(skel.ScaleX, 0f) ? 1f : skel.ScaleX;
//         float sy = Mathf.Approximately(skel.ScaleY, 0f) ? 1f : skel.ScaleY;

//         var local = new Vector3(bone.WorldX / sx, bone.WorldY / sy, 0f);
//         return skeletonAnimation.transform.TransformPoint(local);
//     }
    
// void OnDrawGizmos() {
//     if (!debugDrawGizmos || skel == null || cam == null) return;

//     // 直近に計算したマウスのワールド座標を可視化
//     Gizmos.DrawWireSphere(dbgLastMouseWorld, 0.06f);

//     if (activeLimb.HasValue && targets.TryGetValue(activeLimb.Value, out var tb) && tb != null) {
//         Vector3 w = LocalBoneToWorld(tb);
//         Gizmos.DrawSphere(w, 0.06f);
//     }
// }


//     // 平面の簡易円
//     void DrawWireCircle(Vector3 center, float radius, int seg=32){
//         Vector3 prev = center + Vector3.right * radius;
//         for (int i=1;i<=seg;i++){
//             float a = (float)i/seg * Mathf.PI*2;
//             Vector3 next = center + new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0f) * radius;
//             Gizmos.DrawLine(prev, next);
//             prev = next;
//         }
//     }

// }



