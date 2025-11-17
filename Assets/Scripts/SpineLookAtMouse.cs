using UnityEngine;
using UnityEngine.InputSystem;           // 新Input System
using Spine;
using Spine.Unity;

/// <summary>
/// マウス位置へ「瞳ボーンは位置で」「頭ボーンは回転で」追従させる。
/// ・Scene/Game 両方でOK（Camera必須）
/// ・左右反転(ScaleX)に自動対応
/// ・可動域制限、スムージング、デッドゾーン付き
/// マウスへ：瞳=位置追従（楕円クランプ）、頭=回転追従（角度クランプ）
/// ・原点は Root ではなく「eyeCenter/headBone のローカル座標系」
/// ・左右反転は WorldToLocal が吸収（手動反転不要）
/// ・Spine 4.2+ 対応
/// </summary>
/// 

[DefaultExecutionOrder(1000)] // なるべくLateUpdateの後に
public class SpineLookAtMouse : MonoBehaviour {
    [Header("Refs")]
    public SkeletonAnimation skeletonAnimation;
    public Camera cam;

    [Header("Bones (Spine names)")]
    [SpineBone(dataField: "skeletonAnimation")] public string eyeCenterBoneName = "eye_center_ctrl"; 
    [SpineBone(dataField: "skeletonAnimation")] public string eyePupilBoneName  = "eye_pupil_ctrl";
    [SpineBone(dataField: "skeletonAnimation")] public string headBoneName      = "head_front_Ctrl";

    [Header("Eye (position follow)")]
    public float eyeRadiusX = 5f;        // 楕円のX半径
    public float eyeRadiusY = 5f;        // 楕円のY半径
    public float eyeSmooth = 20f;       // 追従速度
    // 数値が大きいほどマウスをすぐ追う（反応が速い）。
    // 5〜10 → ゆっくり、 15〜30 → 素早く追う。目より小さくすると「遅れてついてくる」感じにできる。
    public float eyeDead = 0.1f;      // デッドゾーン
    //デッドゾーン半径。マウスがキャラ中心付近にあるときに動きを止めてガタつきを防ぐ。
    // 0.1〜0.3くらい。小さいと常に小刻みに動く。大きいと中央で止まる範囲が広がる。

    [Header("Head (position follow)")]
    public float headRadiusX = 2f;
    public float headRadiusY = 2f;
    public float headSmooth  = 10f;
    public float headDead    = 0.2f;

    [Header("Enable/Disable during gameplay")]
    public bool enableFollow = true;

    // internals
    Bone eyeCenter, eyeBone, headBone;
    bool ready;

    void Reset() {
        if (!skeletonAnimation) skeletonAnimation = GetComponentInChildren<SkeletonAnimation>();
    }

    void Awake() {
        if (!skeletonAnimation) skeletonAnimation = GetComponentInChildren<SkeletonAnimation>();
        if (!cam) cam = Camera.main;
    }

    void Start() {
        if (!skeletonAnimation || skeletonAnimation.Skeleton == null) return;
        if (!string.IsNullOrEmpty(eyeCenterBoneName))
            eyeCenter = skeletonAnimation.Skeleton.FindBone(eyeCenterBoneName);
        if (!string.IsNullOrEmpty(eyePupilBoneName))
            eyeBone = skeletonAnimation.Skeleton.FindBone(eyePupilBoneName);
        if (!string.IsNullOrEmpty(headBoneName))
            headBone = skeletonAnimation.Skeleton.FindBone(headBoneName);
        ready = (eyeCenter != null);
    }

    void LateUpdate() {
        if (!ready || !enableFollow || cam == null) return;

        // --- 1) マウスのスクリーン→ワールド（Zを必ず指定） ---
        Vector3 scr = Mouse.current != null ? (Vector3)Mouse.current.position.ReadValue() : (Vector3)Input.mousePosition;
        // 対象（このコンポーネントの Transform）までの深度を使う
        float depth = cam.WorldToScreenPoint(transform.position).z;
        scr.z = depth;
        Vector3 world = cam.ScreenToWorldPoint(scr);

        // --- 2) ワールド→スケルトン空間 ---
        Vector3 skel = skeletonAnimation.transform.InverseTransformPoint(world);
        // Spineはスケールを持つので補正
        skel.x *= skeletonAnimation.Skeleton.ScaleX;
        skel.y *= skeletonAnimation.Skeleton.ScaleY;

        // ===================== Eye: 位置追従（eyeCenter基準） =====================
        if (eyeCenter != null && eyeBone != null) {
            // マウス位置を eyeCenter の「ローカル座標」に変換
            float lx, ly;
            eyeCenter.WorldToLocal(skel.x, skel.y, out lx, out ly); // ←原点ズレ/反転を自動解決

            // デッドゾーン
            Vector2 p = new Vector2(lx, ly);
            float mag = p.magnitude;
            if (mag < eyeDead) p = Vector2.zero;

            // 楕円クランプ（x^2/rx^2 + y^2/ry^2 <= 1）
            float v = (p.x * p.x) / (eyeRadiusX * eyeRadiusX) + (p.y * p.y) / (eyeRadiusY * eyeRadiusY);
            if (v > 1f) {
                float s = 1f / Mathf.Sqrt(v);
                p = new Vector2(p.x * s, p.y * s);
            }

            // スムージング
            float t = 1f - Mathf.Exp(-eyeSmooth * Time.deltaTime);
            float nx = Mathf.Lerp(eyeBone.X, p.x, t);
            float ny = Mathf.Lerp(eyeBone.Y, p.y, t);

            // eyeBone は eyeCenter の子（想定）。ローカル座標で配置
            eyeBone.SetLocalPosition(new Vector2(nx, ny));
        }

        // ===================== Head:  =====================
        // --- Head: 座標追従（目と同じロジック） ---
        if (eyeCenter != null && headBone != null) {
            float hx, hy;
            eyeCenter.WorldToLocal(skel.x, skel.y, out hx, out hy);
            Vector2 p = new Vector2(hx, hy);
            if (p.magnitude < headDead) p = Vector2.zero;

            float v = (p.x * p.x) / (headRadiusX * headRadiusX) + (p.y * p.y) / (headRadiusY * headRadiusY);
            if (v > 1f) p *= 1f / Mathf.Sqrt(v);

            float t = 1f - Mathf.Exp(-headSmooth * Time.deltaTime);
            float nx = Mathf.Lerp(headBone.X, p.x, t);
            float ny = Mathf.Lerp(headBone.Y, p.y, t);
            headBone.SetLocalPosition(new Vector2(nx, ny));
        }

        // --- 3) 反映（Spine 4.2+） ---
        skeletonAnimation.Skeleton.UpdateWorldTransform(Spine.Skeleton.Physics.Update);
    }

    public void SetEnabled(bool enabled) => enableFollow = enabled;
    public void EnableForSeconds(float seconds) {
        if (!gameObject.activeInHierarchy) { enableFollow = true; return; }
        StartCoroutine(CoEnableFor(seconds));
    }
    System.Collections.IEnumerator CoEnableFor(float sec) {
        enableFollow = true;
        yield return new WaitForSeconds(sec);
        enableFollow = false;
    }
}
