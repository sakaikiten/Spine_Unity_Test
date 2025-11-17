using UnityEngine;
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem;
#endif
using Spine;
using Spine.Unity;
using System.Collections.Generic;
using static SpineCoordUtils;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 任意の IK を「名前で」指定して、
/// ・指定時間でマウス方向へ移動
/// ・保持
/// ・指定時間で元位置へ戻す
/// を複数同時に制御する Aimer。
///
/// ・Limb 固定版 / Front-Back 版 / スカート版 をすべて統合。
/// ・IK 名をキーに辞書登録して、攻撃時間中だけ IkConstraint.Mix を上げて完全コード制御。
/// ・攻撃中の IK ターゲット位置と IK 名を Gizmo で表示。
/// </summary>
[DefaultExecutionOrder(1001)]
public class SpineIKMouseAimer : MonoBehaviour {
    public SkeletonAnimation skeletonAnimation;
    public Camera cam;

    [Header("Reach & Flip (default)")]
    public float handRadius = 2.5f;
    public float footRadius = 3.0f;
    public bool respectFlipX = true;

    [Header("Motion Curve (default)")]
    public AnimationCurve defaultToAimCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);
    public AnimationCurve defaultReturnCurve = AnimationCurve.EaseInOut(0, 0, 1, 1);

    [Header("Debug")]
    public bool debugLog = false;
    public bool debugDrawGizmos = true;

    public enum Limb { RHand, LHand, RFoot, LFoot }

    // 3フェーズ状態
    class IkState {
        public enum Phase { Idle, ToAim, Hold, Return }
        public Phase phase = Phase.Idle;
        public Vector2 savedLocal;
        public float t;
        public float toAimDur, holdDur, returnDur;
    }

    // IKごとの設定
    class IkConfig {
        public Limb limbType;              // 半径のデフォルト判定用
        public float radius = -1f;         // >0 なら優先。<=0 なら handRadius/footRadius を使用、<=0 ならクランプなし
        public bool respectFlipX = true;

        // オフセット（どの空間で足すか）
        public Vector2 offsetLocal = Vector2.zero;
        public bool offsetInSkeletonSpace = false; // true: スケルトンローカル空間で足す, false: 親ボーンローカルで足す

        // カーブ・Mix
        public AnimationCurve toCurve;
        public AnimationCurve returnCurve;
        public float mix = 1.0f;

        // ここから追加：角度制限関連
        public bool useAngleLimit = false;
        public float minAngleDeg = -180f;
        public float maxAngleDeg = 180f;
    }

    // 1 IK エントリ
    class IkEntry {
        public IkConstraint ik;
        public Bone target;
        public IkState state = new IkState();
        public IkConfig cfg = new IkConfig();
    }

    Skeleton skel;

    // IK 名で管理（複数同時OK）
    readonly Dictionary<string, IkEntry> iksByName = new();

    void Awake() {
        if (!skeletonAnimation) skeletonAnimation = GetComponentInChildren<SkeletonAnimation>();
        if (!cam) cam = Camera.main;
    }

    void Start() {
        if (!skeletonAnimation) return;
        skel = skeletonAnimation.Skeleton;
    }

    void OnEnable() {
        if (skeletonAnimation is ISkeletonAnimation isa)
            isa.UpdateLocal += HandleUpdateLocal;
    }

    void OnDisable() {
        if (skeletonAnimation is ISkeletonAnimation isa)
            isa.UpdateLocal -= HandleUpdateLocal;
    }

    // --- 入力：IK発火API ---------------------------------------------------

    /// <summary>
    /// 任意の IK 名を「3フェーズ」でマウス方向へ動かす。
    /// Limb は「手か足か」でデフォルト半径を決めるために使う。
    /// radius &lt;= 0 の場合は handRadius / footRadius を使用。
    /// respectFlipX: true なら Skeleton.ScaleX で左右反転。
    /// offsetLocal: offsetInSkeletonSpace=false なら親ボーンローカル、true ならスケルトンローカルで足す。
    /// </summary>
    public void AimIK(
        string ikName,
        Limb limbType,
        float toAim = 0.08f,
        float hold = 0.12f,
        float @return = 0.10f,
        float mix = 1.0f,
        float radius = -1f,
        bool respectFlipXPerIK = true,
        Vector2 offsetLocal = default,
        bool offsetInSkeletonSpace = false,
        AnimationCurve toCurveOverride = null,
        AnimationCurve returnCurveOverride = null,
        bool useAngleLimit = false,      //角度クランプするか
        float minAngleDeg = -180f,       //クランプ角度
        float maxAngleDeg = 180f         //クランプ角度
    ) {
        if (skel == null || string.IsNullOrEmpty(ikName)) return;

        if (!iksByName.TryGetValue(ikName, out var entry)) {
            // 初回は IK を検索して登録
            var ik = skel.FindIkConstraint(ikName);
            if (ik == null) {
                Debug.LogWarning($"[SpineIKMouseAimer] IK '{ikName}' が見つかりません。");
                return;
            }
            entry = new IkEntry {
                ik = ik,
                target = ik.Target
            };
            iksByName[ikName] = entry;
        }

        if (entry.target == null) {
            Debug.LogWarning($"[SpineIKMouseAimer] IK '{ikName}' の Target が null です。");
            return;
        }

        // 設定を詰める
        var cfg = entry.cfg;
        cfg.limbType = limbType;
        cfg.radius = radius;
        cfg.respectFlipX = respectFlipXPerIK;
        cfg.offsetLocal = offsetLocal;
        cfg.offsetInSkeletonSpace = offsetInSkeletonSpace;
        cfg.mix = mix;
        cfg.toCurve = toCurveOverride ?? defaultToAimCurve;
        cfg.returnCurve = returnCurveOverride ?? defaultReturnCurve;
        cfg.useAngleLimit = useAngleLimit;
        cfg.minAngleDeg   = minAngleDeg;
        cfg.maxAngleDeg   = maxAngleDeg;

        // 状態をリセット
        var st = entry.state;
        st.savedLocal = new Vector2(entry.target.X, entry.target.Y);
        st.t = 0f;
        st.toAimDur = Mathf.Max(0.0001f, toAim);
        st.holdDur = Mathf.Max(0f, hold);
        st.returnDur = Mathf.Max(0.0001f, @return);
        st.phase = IkState.Phase.ToAim;

        // 攻撃中は Mix を上げる
        entry.ik.Mix = mix;

        if (debugLog)
            Debug.Log($"[AimIK] Start '{ikName}' limb={limbType} mix={mix}");
    }

    /// <summary>
    /// Front/Back の IK 名を向きで切り替えたいとき用のヘルパ。
    /// （中身は単に AimIK に丸投げ）
    /// </summary>
    public void AimFrontBackIK(
        string ikFront, string ikBack, bool facingRight,
        Limb limbType,
        float toAim, float hold, float @return,
        float mix,
        float radius = -1f,
        bool respectFlipXPerIK = true,
        Vector2 offsetLocal = default,
        bool offsetInSkeletonSpace = false,
        AnimationCurve toCurveOverride = null,
        AnimationCurve returnCurveOverride = null,
        bool useAngleLimit = false,
        float minAngleDeg = -180f,
        float maxAngleDeg =  180f
    ) {
        string ikName = facingRight ? ikFront : ikBack;
        AimIK(ikName, limbType, toAim, hold, @return, mix, radius, respectFlipXPerIK,
              offsetLocal, offsetInSkeletonSpace, toCurveOverride, returnCurveOverride,useAngleLimit,minAngleDeg,maxAngleDeg);
    }

    // --- マウス座標ユーティリティ -----------------------------------------

    Vector3 GetMouseScreenPos() {
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
        var mouse = Mouse.current;
        if (mouse == null) return Vector3.zero;
        Vector2 p = mouse.position.ReadValue();
        return new Vector3(p.x, p.y, 0f);
#else
        return Input.mousePosition;
#endif
    }

    float GetDepthFromCameraToSkeleton() {
        var wpos = skeletonAnimation ? skeletonAnimation.transform.position : Vector3.zero;
        return Mathf.Abs(cam.worldToCameraMatrix.MultiplyPoint(wpos).z);
    }

    // --- メイン更新 --------------------------------------------------------

    void HandleUpdateLocal(ISkeletonAnimation _) {
        if (skel == null || cam == null || skeletonAnimation == null) return;

        if (iksByName.Count == 0) return;

        // このフレームのマウス座標 → スケルトンローカルを一度だけ計算
        Vector3 s = GetMouseScreenPos();
        if (s == Vector3.zero) return;
        s.z = GetDepthFromCameraToSkeleton();
        Vector3 mouseWorld = cam.ScreenToWorldPoint(s);
        Vector3 skelSpace = skeletonAnimation.transform.InverseTransformPoint(mouseWorld);
        Vector2 skelLocal = new(skelSpace.x, skelSpace.y);

        List<string> finished = null;

        foreach (var kv in iksByName) {
            string ikName = kv.Key;
            var entry = kv.Value;
            var st = entry.state;
            var cfg = entry.cfg;
            var tgtB = entry.target;
            var ik = entry.ik;

            if (tgtB == null) continue;
            if (st.phase == IkState.Phase.Idle) continue;

            // A) スケルトンローカル → 親ボーンローカル
            Vector2 skelForThis = skelLocal;
            if (cfg.offsetInSkeletonSpace && cfg.offsetLocal != Vector2.zero) {
                skelForThis += cfg.offsetLocal;
            }

            float wx = skelForThis.x * skel.ScaleX;
            float wy = skelForThis.y * skel.ScaleY;

            var parent = tgtB.Parent ?? skel.RootBone;
            parent.WorldToLocal(wx, wy, out float lx, out float ly);
            Vector2 aimLocal = new(lx, ly);

            if (!cfg.offsetInSkeletonSpace && cfg.offsetLocal != Vector2.zero) {
                // 親ボーンローカルでオフセットを足したい場合
                aimLocal += cfg.offsetLocal;
            }

            // 半径クランプ
            float rMax = cfg.radius;
            if (rMax <= 0f) {
                // Limb からデフォルト半径
                bool isHand = (cfg.limbType == Limb.RHand || cfg.limbType == Limb.LHand);
                rMax = isHand ? handRadius : footRadius;
            }
            if (rMax > 0f && aimLocal.sqrMagnitude > rMax * rMax) {
                aimLocal = aimLocal.normalized * rMax;
            }

            // FlipX
            if (cfg.respectFlipX && skel.ScaleX < 0f)
            {
                aimLocal.x = -aimLocal.x;
            }
            
            // --- ★角度クランプ（ここで扇形に制限） ---
            if (cfg.useAngleLimit) {
                aimLocal = ClampDirectionByAngle(aimLocal, cfg.minAngleDeg, cfg.maxAngleDeg);
            }

            // B) フェーズ進行
            switch (st.phase) {
                case IkState.Phase.ToAim:
                    st.t += Time.deltaTime / st.toAimDur;
                    tgtB.SetLocalPosition(Vector2.Lerp(st.savedLocal, aimLocal, cfg.toCurve.Evaluate(Mathf.Clamp01(st.t))));
                    if (st.t >= 1f) {
                        st.t = 0f;
                        st.phase = (st.holdDur > 0f) ? IkState.Phase.Hold : IkState.Phase.Return;
                    }
                    break;

                case IkState.Phase.Hold:
                    tgtB.SetLocalPosition(aimLocal);
                    st.t += Time.deltaTime;
                    if (st.t >= st.holdDur) {
                        st.t = 0f;
                        st.phase = IkState.Phase.Return;
                    }
                    break;

                case IkState.Phase.Return:
                    st.t += Time.deltaTime / st.returnDur;
                    tgtB.SetLocalPosition(Vector2.Lerp(aimLocal, st.savedLocal, cfg.returnCurve.Evaluate(Mathf.Clamp01(st.t))));
                    if (st.t >= 1f) {
                        ik.Mix = 0f;
                        st.phase = IkState.Phase.Idle;
                        if (finished == null) finished = new List<string>();
                        finished.Add(ikName);
                    }
                    break;
            }

            if (debugLog)
                Debug.Log($"[IK {ikName}] {st.phase} t={st.t:F2} tgt=({tgtB.X:F2},{tgtB.Y:F2})");
        }

        // 攻撃終了した IK は状態を残すが、次フレームからは Idle でスキップされる。
        // 必要なら finished で何かフックしても良い（今は未使用）。
    }

    // --- Gizmos -------------------------------------------------------------

    void OnDrawGizmos() {
        if (!debugDrawGizmos || skeletonAnimation == null) return;
        if (iksByName == null || iksByName.Count == 0) return;

        foreach (var kv in iksByName) {
            string ikName = kv.Key;
            var entry = kv.Value;
            var st = entry.state;
            var tgtB = entry.target;
            if (tgtB == null) continue;
            if (st.phase == IkState.Phase.Idle) continue; // 攻撃中のみ描画

            Vector3 world = LocalBoneToWorld(tgtB);
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(world, 0.05f);

#if UNITY_EDITOR
            // ターゲット名ラベル
            var style = new GUIStyle(EditorStyles.boldLabel);
            style.normal.textColor = Color.yellow;
            Handles.Label(world + Vector3.up * 0.05f, ikName, style);
#endif
        }
    }

    Vector3 LocalBoneToWorld(Bone bone) {
        if (skeletonAnimation == null || skel == null) return Vector3.zero;
        float sx = Mathf.Approximately(skel.ScaleX, 0f) ? 1f : skel.ScaleX;
        float sy = Mathf.Approximately(skel.ScaleY, 0f) ? 1f : skel.ScaleY;
        var local = new Vector3(bone.WorldX / sx, bone.WorldY / sy, 0f);
        return skeletonAnimation.transform.TransformPoint(local);
    }
}
