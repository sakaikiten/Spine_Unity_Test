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
//     public float mixSmooth = 18f;  // IKMixのなめらかさ

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

//         // IK Mixを補間してON/OFF、ターゲット位置を更新
//         bool facingRight = skel.ScaleX >= 0f;
//         float mixStep = 1f - Mathf.Exp(-mixSmooth * Time.deltaTime);
//         float posStep = 1f - Mathf.Exp(-followSmooth * Time.deltaTime);

//         // 残り時間が切れたら非アクティブ
//         if (activeLimb.HasValue)
//         {
//             remain -= Time.deltaTime;
//             if (remain <= 0f)
//             {
//                 // フェードアウトへ
//                 activeLimb = null;
//             }
//         }

//         foreach (var limb in iks.Keys)
//         {
//             var ik = iks[limb];
//             // 目標Mix（アクティブな肢だけ1、他は0）
//             float targetMix = (activeLimb.HasValue && limb == activeLimb.Value) ? 1f : 0f;
//             ik.Mix = Mathf.Lerp(ik.Mix, targetMix, mixStep);

//             //Debug
//             if (debugLogIkMix && limb == activeLimb)
//             {
//                 if (Time.frameCount % 15 == 0) // ログ出し過多防止
//                     Debug.Log($"[IK] {limb} Mix={ik.Mix:0.00} remain={remain:0.00}");
//             }


//             // ターゲット追従（アクティブな肢のみ）
//             if (ik.Mix > 0.001f && limb == activeLimb)
//             {
//                 var targetBone = targets[limb];

//                 if (targetBone == null) return;

//                 // 1) スクリーン → ワールド（2Dオルソ用のZ設定）
//                 Vector3 screen = GetMouseScreenPos();
//                 if (screen == Vector3.zero) return;
//                 screen.z = GetDepth2DOrtho(cam, skeletonAnimation.transform); // ★
//                 Vector3 mouseWorld = cam.ScreenToWorldPoint(screen);

//                 // 2) ワールド → スケルトン座標（Spineの“スケルトンワールド”）
//                 Vector3 skelSpace = skeletonAnimation.transform.InverseTransformPoint(mouseWorld);
//                 float worldX = skelSpace.x * skel.ScaleX;
//                 float worldY = skelSpace.y * skel.ScaleY;

//                 // 3) スケルトンワールド → 親ローカル（正攻法）
//                 var parent = targetBone.Parent ?? skel.RootBone;
//                 parent.WorldToLocal(worldX, worldY, out float localX, out float localY);

//                 // 4) IK ターゲットにそのまま代入（即追従）
//                 targetBone.SetLocalPosition(new Vector2(localX, localY));

//             }
//         }

//         // 反映
//         skel.UpdateWorldTransform(Skeleton.Physics.Update);

//         // 2) ここでデバッグマーカーを更新（activeLimb があるときだけでOK）
//         if (debugMarkerInst && activeLimb.HasValue && targets.TryGetValue(activeLimb.Value, out var tb) && tb != null)
//         {
//             // option1: 目標に置いた“はず”の世界座標を直接使う（確実に見える）
//             // debugMarkerInst.position = mouseWorld; // ← mouseWorld をスコープに出しておく

//             // option2: Bone の World を読む（この場合は UpdateWorldTransform 後に読む）
//             Vector3 world = LocalBoneToWorld(tb);
//             debugMarkerInst.position = world;
//         }


//     }

//     // LateUpdate 方式だと「アニメ適用 →（この時点で IK も解かれる）→ あとからターゲット移動」になりがち。
//     // Spine-Unity には UpdateLocal フックがあります。アニメがローカル値を適用した直後、IK を解く前に介入できるので、同フレームで IK があなたのターゲットへ解かれます。
//     // void HandleUpdateLocal(ISkeletonAnimation _) {
//     //     if (skel == null || cam == null || !activeLimb.HasValue) return;

//     //     var limb = activeLimb.Value;
//     //     if (!iks.TryGetValue(limb, out var ik)) return;
//     //     var targetBone = targets[limb];
//     //     if (targetBone == null) return;

//     //     // ★ ここが実際のターゲット更新処理
//     //     Vector3 screen = GetMouseScreenPos();
//     //     if (screen == Vector3.zero) return;
//     //     screen.z = GetDepth2DOrtho(cam, skeletonAnimation.transform);
//     //     Vector3 mouseWorld = cam.ScreenToWorldPoint(screen);

//     //     Vector3 skelSpace = skeletonAnimation.transform.InverseTransformPoint(mouseWorld);
//     //     float worldX = skelSpace.x * skel.ScaleX;
//     //     float worldY = skelSpace.y * skel.ScaleY;

//     //     var parent = targetBone.Parent ?? skel.RootBone;
//     //     parent.WorldToLocal(worldX, worldY, out float localX, out float localY);
//     //     targetBone.SetLocalPosition(new Vector2(localX, localY));

//     //     // IK Mix をフェードイン
//     //     float mixStep = 1f - Mathf.Exp(-mixSmooth * Time.deltaTime);
//     //     ik.Mix = Mathf.Lerp(ik.Mix, (limb == activeLimb) ? 1f : 0f, mixStep);
//     // }

//     void HandleUpdateLocal(ISkeletonAnimation _) {
//         if (skel == null || cam == null) return;

//         // 1) まず全IKのMixを一括で減衰（非アクティブは0へ）
//         float mixStep = 1f - Mathf.Exp(-mixSmooth * Time.deltaTime);
//         foreach (var kv in iks) {
//             bool isActive = activeLimb.HasValue && kv.Key == activeLimb.Value && remain > 0f;
//             kv.Value.Mix = Mathf.Lerp(kv.Value.Mix, isActive ? 1f : 0f, mixStep);
//         }

//         // 2) アクティブな肢があれば、そのターゲットだけ更新
//         if (activeLimb.HasValue && remain > 0f) {
//             var limb = activeLimb.Value;
//             if (!iks.TryGetValue(limb, out var ik)) return;
//             var targetBone = targets[limb];
//             if (targetBone == null) return;

//             Vector3 s = GetMouseScreenPos();
//             if (s == Vector3.zero) return;
//             s.z = GetDepth2DOrtho(cam, skeletonAnimation.transform);
//             Vector3 mouseWorld = cam.ScreenToWorldPoint(s);

//             Vector3 skelSpace = skeletonAnimation.transform.InverseTransformPoint(mouseWorld);
//             float worldX = skelSpace.x * skel.ScaleX;
//             float worldY = skelSpace.y * skel.ScaleY;

//             var parent = targetBone.Parent ?? skel.RootBone;
//             parent.WorldToLocal(worldX, worldY, out float lx, out float ly);
//             targetBone.SetLocalPosition(new Vector2(lx, ly));
//         }

//         // 3) 残り時間をここで減らす（ここでやると評価順のズレが出にくい）
//         if (activeLimb.HasValue) {
//             remain -= Time.deltaTime;
//             if (remain <= 0f) activeLimb = null; // 明示的に解除
//         }
//     }




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
//     Vector3 LocalBoneToWorld(Bone bone) {
//         // bone.WorldX/Y は「スケルトン座標（スケール適用後）」なので、
//         // TransformPoint に渡す前にスケールを打ち消す
//         float sx = Mathf.Approximately(skel.ScaleX, 0f) ? 1f : skel.ScaleX;
//         float sy = Mathf.Approximately(skel.ScaleY, 0f) ? 1f : skel.ScaleY;

//         var local = new Vector3(bone.WorldX / sx, bone.WorldY / sy, 0f);
//         return skeletonAnimation.transform.TransformPoint(local);
//     }

//     void OnDrawGizmos() {
//         if (!debugDrawGizmos || skel == null) return;

//         // マウス世界座標
//         Vector3 screen = GetMouseScreenPos();
//         if (cam) {
//             screen.z = GetDepthFromCameraToSkeleton();
//             Vector3 mouseWorld = cam.ScreenToWorldPoint(screen);

//             Gizmos.DrawWireSphere(mouseWorld, 0.05f);
//         }

//         // アクティブ肢のターゲットボーン
//         if (activeLimb.HasValue && targets.TryGetValue(activeLimb.Value, out var tb) && tb != null) {
//             Vector3 w = LocalBoneToWorld(tb);
//             Gizmos.DrawSphere(w, 0.06f);
//             // 到達半径の可視化（親ボーン原点を中心に円）
//             var parent = tb.Parent ?? skel.RootBone;
//             Vector3 parentW = LocalBoneToWorld(parent);
//             float r = (activeLimb == Limb.RHand || activeLimb == Limb.LHand) ? handRadius : footRadius;
//             DrawWireCircle(parentW, r * Mathf.Abs(skel.ScaleX)); // 簡易円描画（下の関数を追加）
//             Gizmos.DrawLine(parentW, w);
//         }
//     }

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



