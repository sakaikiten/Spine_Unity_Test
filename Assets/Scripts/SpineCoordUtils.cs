using UnityEngine;
using Spine;
using Spine.Unity;

/// <summary>
/// Spine x Unity の座標変換ユーティリティ（再利用向け）
/// 画面(Screen) → ワールド(World) → スケルトンローカル(SkeletonLocal) → 親ボーンローカル(BoneLocal)
/// の一本道を用途別に切り出し。
/// 
// /// Vector2 boneLocal = ScreenToBoneLocal(
//     cam,                // 使用するカメラ
//     skeletonAnimation,  // SpineのSkeletonAnimation（Transform取得用）
//     skel,               // SpineのSkeleton
//     tgtB.Parent ?? skel.RootBone,  // IKターゲットの親ボーン
//     GetMouseScreenPos(), // スクリーン座標
//     Vector2.zero,       // オフセット（必要に応じて）
//     OffsetSpace.SkeletonLocal, // オフセット空間の種類
//     respectFlipX        // フリップ補正をかけるか
// );

//Screen → World → Skeleton Local → Skeleton World → Bone Local　という流れ

// Screen 座標
//      Input.mousePosition みたいな「画面ピクセルベースの座標」

// World 座標（Unity）
//      transform.position などの「シーン全体のグローバル座標」

// Skeleton Local（Unity側）
//      SkeletonAnimation の transform を原点 (0,0,0) としたローカル座標
//      skelAnim.transform.InverseTransformPoint(world) の結果

//Spine Skeleton World 座標
//      Spineの Bone.WorldX / WorldY などが使っている座標系
//      Skeleton.ScaleX / ScaleY が掛かった「Spine内部の世界座標」

// Spine Bone Local 座標
//      各 Bone が持っている「ローカル」座標系 (WorldToLocal で変換)


/// </summary>
public static class SpineCoordUtils
{
    public enum OffsetSpace
    {
        SkeletonLocal, // スケルトンのローカルでオフセット
        BoneLocal      // 親ボーンのローカルでオフセット
    }
    
    //★★座標変換汎用コード
    public static Vector2 WorldToBoneLocal(SkeletonAnimation skelAnim, Bone parent, Vector3 worldPos) {
        var skel = skelAnim.Skeleton;
        var local = skelAnim.transform.InverseTransformPoint(worldPos);
        float wx = local.x * skel.ScaleX;
        float wy = local.y * skel.ScaleY;
        parent.WorldToLocal(wx, wy, out float lx, out float ly);
        return new Vector2(lx, ly);
    }



    /// <summary>カメラ→対象Transformの深度を取得（ScreenToWorldPoint用のz）</summary>
    public static float GetDepthFromCameraTo(Transform target, Camera cam)
    {
        if (!cam || !target) return 0f;
        return Mathf.Abs(cam.worldToCameraMatrix.MultiplyPoint(target.position).z);
    }

    /// <summary>スクリーン座標をスケルトンローカル（Unity Transform基準）へ</summary>
    public static Vector2 ScreenToSkeletonLocal(Camera cam, SkeletonAnimation skelAnim, Vector2 screen)
    {
        Vector3 s = new Vector3(screen.x, screen.y, GetDepthFromCameraTo(skelAnim.transform, cam));
        Vector3 world = cam.ScreenToWorldPoint(s);
        Vector3 skelLocal = skelAnim.transform.InverseTransformPoint(world);
        return new Vector2((float)skelLocal.x, (float)skelLocal.y);
    }

    /// <summary>ワールド座標をスケルトンローカルへ</summary>
    public static Vector2 WorldToSkeletonLocal(SkeletonAnimation skelAnim, Vector3 world)
    {
        Vector3 skelLocal = skelAnim.transform.InverseTransformPoint(world);
        return new Vector2((float)skelLocal.x, (float)skelLocal.y);
    }

    /// <summary>スケルトンローカル→Spine内部スケール適用（Spine.World系で扱う値へ）</summary>
    public static Vector2 ApplySkeletonScale(Skeleton skel, Vector2 skelLocal)
    {
        return new Vector2(skelLocal.x * skel.ScaleX, skelLocal.y * skel.ScaleY);
    }

    /// <summary>スケルトンローカル → 親ボーンローカル</summary>
    public static Vector2 SkeletonLocalToBoneLocal(Skeleton skel, Bone parent, Vector2 skelLocal)
    {
        float wx = skelLocal.x * skel.ScaleX;
        float wy = skelLocal.y * skel.ScaleY;
        parent.WorldToLocal(wx, wy, out float lx, out float ly);
        return new Vector2(lx, ly);
    }

    /// <summary>スクリーン座標 → 親ボーンローカル（オフセットを Skeleton/Bone どちらの空間で足すか選択可能）</summary>
    public static Vector2 ScreenToBoneLocal(
        Camera cam, SkeletonAnimation skelAnim, Skeleton skel, Bone parent,
        Vector2 screen,
        Vector2 offset, OffsetSpace offsetSpace,
        bool flipXBySkeletonScaleX = false
    )
    {
        // 1) Screen → SkeletonLocal
        Vector2 skelLocal = ScreenToSkeletonLocal(cam, skelAnim, screen);

        // 2) オフセット適用
        if (offsetSpace == OffsetSpace.SkeletonLocal)
        {
            skelLocal += offset; // 画面基準に近い直感
        }

        // 3) SkeletonLocal → BoneLocal
        Vector2 boneLocal = SkeletonLocalToBoneLocal(skel, parent, skelLocal);

        if (offsetSpace == OffsetSpace.BoneLocal)
        {
            boneLocal += offset; // ボーン軸に沿ったオフセット
        }

        // 4) 左右反転（SpineのScaleXでFlipしている場合の補正）
        if (flipXBySkeletonScaleX && skel.ScaleX < 0f) boneLocal.x = -boneLocal.x;

        return boneLocal;
    }

    /// <summary>IK名から IkConstraint と Target を取得（1回見つけたら使い回し推奨）</summary>
    public static bool TryFindIk(Skeleton skel, string ikName, out IkConstraint ik, out Bone target)
    {
        ik = null; target = null;
        if (skel == null || string.IsNullOrEmpty(ikName)) return false;
        ik = skel.FindIkConstraint(ikName);
        if (ik == null) return false;
        target = ik.Target;
        return target != null;
    }

    /// <summary>半径クランプ（必要な時だけ使う）</summary>
    public static Vector2 ClampInRadius(Vector2 v, float radius)
    {
        if (radius <= 0f) return v;
        if (v.sqrMagnitude > radius * radius) return v.normalized * radius;
        return v;
    }

    // =========================
    // 角度系ユーティリティ
    // =========================

    /// <summary>
    /// ベクトルvの「ローカル角度」を[-180,180]度で返す。
    /// 0度は +X 方向。Atan2(y,x) のラッパ。
    /// </summary>
    public static float GetAngleDeg(Vector2 v) {
        if (v == Vector2.zero) return 0f;
        return Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg; // ここでラジアン→度に変換
    }

    /// <summary>
    /// 半径と角度[deg]から2Dベクトルを生成。
    /// </summary>
    public static Vector2 FromPolar(float radius, float angleDeg) {
        float rad = angleDeg * Mathf.Deg2Rad;
        return new Vector2(Mathf.Cos(rad) * radius, Mathf.Sin(rad) * radius);
    }

    /// <summary>
    /// ベクトルvの「向き（角度）」だけを [minDeg,maxDeg] の範囲にクランプする。
    /// 長さ（radius）はそのまま維持。
    /// </summary>
    public static Vector2 ClampDirectionByAngle(Vector2 v, float minDeg, float maxDeg) {
        if (v == Vector2.zero) return v;

        float angleDeg    = GetAngleDeg(v);           // もう「度」
        float clampedDeg  = Mathf.Clamp(angleDeg, minDeg, maxDeg);

        // float angle = GetAngleDeg(v);                 // [-180,180]
        // float clamped = Mathf.Clamp(angle, minDeg, maxDeg);
        float len = v.magnitude;
        // Debug.Log($"aimLocal=({v.x:F2},{v.y:F2}), angle={angleDeg:F1}°, clamped={clampedDeg:F1}°");

        // return FromPolar(len, clamped);
        return FromPolar(len, clampedDeg);
    }

    /// <summary>
    /// ベクトルvを「角度」と「半径」の両方で制限する。
    /// maxRadius &gt; 0 のときだけ半径をクランプ。maxRadius &lt;= 0 なら半径はそのまま。
    /// </summary>
    public static Vector2 ClampDirectionAndRadius(
        Vector2 v,
        float minDeg,
        float maxDeg,
        float maxRadius
    ){
        // 角度制限
        Vector2 dir = ClampDirectionByAngle(v, minDeg, maxDeg);

        if (maxRadius > 0f && dir.sqrMagnitude > maxRadius * maxRadius) {
            return dir.normalized * maxRadius;
        }
        return dir;
    }


}
