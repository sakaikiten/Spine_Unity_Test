using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Spine;
using Spine.Unity;

[DisallowMultipleComponent]
public class SpineSkinController : MonoBehaviour {
    [Header("Target")]
    public SkeletonAnimation skeletonAnimation;   // or SkeletonGraphicも可
    [Header("Auto Apply on Start")]
    public string[] initialSkins;

    [Header("Optional Presets")]
    public List<SkinPreset> presets;

    Skeleton _skeleton;
    SkeletonData _data;

    // 合成スキンのキャッシュ（頻繁な再合成を避ける）
    readonly Dictionary<string, Skin> _cache = new();

    public System.Action<IReadOnlyList<string>> OnSkinsApplied;

    void Awake() {
        if (!skeletonAnimation) {
            Debug.LogError("[SpineSkinController] SkeletonAnimation not set.");
            enabled = false; return;
        }
        _skeleton = skeletonAnimation.Skeleton;
        _data = _skeleton.Data;

        if (initialSkins != null && initialSkins.Length > 0)
            ApplySkins(initialSkins);
    }

    public void ApplyPreset(SkinPreset preset) {
        if (preset == null) return;
        ApplySkins(preset.skinNames);
    }

    public void ApplySkins(IEnumerable<string> skinNames) {
        var names = (skinNames ?? Enumerable.Empty<string>())
                    .Where(n => !string.IsNullOrEmpty(n))
                    .Distinct()
                    .OrderBy(n => n)                 // 衝突時の再現性確保用：キーはソート版
                    .ToArray();

        string key = string.Join("+", names);
        if (!_cache.TryGetValue(key, out var combined)) {
            combined = new Skin($"combined:{key}");
            foreach (var n in names) {
                var s = _data.FindSkin(n);
                if (s != null) combined.AddSkin(s);
                else Debug.LogWarning($"[SpineSkinController] Skin not found: {n}");
            }
            _cache[key] = combined;
        }

        _skeleton.SetSkin(combined);
        RefreshPose();

        OnSkinsApplied?.Invoke(names);
    }

    public void ClearSkins() {
        // 空スキン（何も表示しない）
        var empty = new Skin("empty");
        _skeleton.SetSkin(empty);
        RefreshPose();
        OnSkinsApplied?.Invoke(System.Array.Empty<string>());
    }

    public void RefreshPose() {
        _skeleton.SetSlotsToSetupPose();
        skeletonAnimation.AnimationState.Apply(_skeleton);
        skeletonAnimation.LateUpdate();
    }
}
