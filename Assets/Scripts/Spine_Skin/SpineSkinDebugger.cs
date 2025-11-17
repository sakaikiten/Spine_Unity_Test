
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine.InputSystem.UI;
#endif

[System.Serializable]
public class PlayerSkinSettings {
    public List<bool> initialOn = new List<bool>();
}

public class SpineSkinDebugger : MonoBehaviour {

    [Header("Controllers to Drive (複数プレイヤー用)")]
    public SpineSkinController[] controllers;  // プレイヤーA/B…をここに登録
    public int selectedIndex = 0;              // 現在操作中のプレイヤー

    // 互換用：昔の単体用フィールド（controllers が空のときだけ使う）
    [Header("Legacy (単体用。controllersが空のときのみ使用)")]
    public SpineSkinController controller;

    [Header("Available Skin Names")]
    public string[] availableSkins = {
        "body/female01",
        "body/male01",
        "body_parts/penis",
        "body_parts/penis_mosaic",
        "cloth_sailor/sailor_skirt",
        "cloth_sailor/sailor_top"
    };

    // [Header("Initial ON states (same length as availableSkins)")]
    // public bool[] initialOn;
    [Header("Initial ON states per player (same length as controllers)")]
    // public bool[][] initialOnPerPlayer;
    public List<PlayerSkinSettings> initialOnPerPlayer = new List<PlayerSkinSettings>();


    [Header("UI")]
    public Vector2 panelOffset = new Vector2(16, -16);
    public string panelTitle = "Skins";
    public int fontSize = 8;

    // 各コントローラごとのアクティブスキン
    List<string>[] _perControllerActives;
    Transform _panelRoot;
    Text _targetLabel;

    void Awake() {
        // 旧フィールドからのフォールバック
        if ((controllers == null || controllers.Length == 0) && controller != null) {
            controllers = new SpineSkinController[] { controller };
        }

        if (controllers == null || controllers.Length == 0) {
            Debug.LogError("[SpineSkinDebugUI] No controllers assigned.");
            enabled = false;
            return;
        }

        if (selectedIndex < 0 || selectedIndex >= controllers.Length)
            selectedIndex = 0;

        // if (initialOn == null || initialOn.Length != availableSkins.Length)
        //     initialOn = new bool[availableSkins.Length];
        if (initialOnPerPlayer == null || initialOnPerPlayer.Count != controllers.Length) {
            Debug.LogError("[SpineSkinDebugUI] initialOnPerPlayer must have the same length as controllers.");
            enabled = false;
            return;
        }

        // // 各プレイヤーごとのスキンONリストを準備
        // _perControllerActives = new List<string>[controllers.Length];
        // for (int i = 0; i < controllers.Length; i++) {
        //     _perControllerActives[i] = new List<string>();
        //     for (int s = 0; s < availableSkins.Length; s++) {
        //         if (initialOn[s]) {
        //             _perControllerActives[i].Add(availableSkins[s]);
        //         }
        //     }
        //     controllers[i].ApplySkins(_perControllerActives[i]);
        // }

        // 各プレイヤーごとのスキンONリストを準備
        _perControllerActives = new List<string>[controllers.Length];
        for (int i = 0; i < controllers.Length; i++) {
            _perControllerActives[i] = new List<string>();
            if (initialOnPerPlayer[i].initialOn == null || initialOnPerPlayer[i].initialOn.Count != availableSkins.Length) {
                Debug.LogError($"[SpineSkinDebugUI] initialOnPerPlayer[{i}] must have the same length as availableSkins.");
                enabled = false;
                return;
            }
            for (int s = 0; s < availableSkins.Length; s++) {
                if (initialOnPerPlayer[i].initialOn[s]) {
                    _perControllerActives[i].Add(availableSkins[s]);
                }
            }
            controllers[i].ApplySkins(_perControllerActives[i]);
        }

        EnsureEventSystem();
        BuildUI();
        RefreshUIForCurrentTarget();
    }

    void EnsureEventSystem() {
#if UNITY_2023_1_OR_NEWER
        if (!Object.FindFirstObjectByType<EventSystem>()) {
#else
        if (!Object.FindObjectOfType<EventSystem>()) {
#endif
            var es = new GameObject("EventSystem", typeof(EventSystem));
#if ENABLE_INPUT_SYSTEM && !ENABLE_LEGACY_INPUT_MANAGER
            es.AddComponent<InputSystemUIInputModule>();
#else
            es.AddComponent<StandaloneInputModule>();
#endif
            DontDestroyOnLoad(es);
        }
    }

    void BuildUI() {
        var canvasGO = new GameObject("SpineSkinDebugUI", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        DontDestroyOnLoad(canvasGO);
        var canvas = canvasGO.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280, 720);

        var panel = CreateUI("Panel", canvasGO.transform, out RectTransform prt);
        _panelRoot = panel.transform;

        var img = panel.AddComponent<Image>();
        img.color = new Color(0, 0, 0, 0.6f);
        prt.anchorMin = new Vector2(0, 1);
        prt.anchorMax = new Vector2(0, 1);
        prt.pivot = new Vector2(0, 1);
        prt.anchoredPosition = panelOffset;
        prt.sizeDelta = new Vector2(260, 40 + 34 * (availableSkins.Length + 3)); // ターゲット行を1行追加

        // タイトル
        var title = CreateText(panel.transform, panelTitle, fontSize + 2, FontStyle.Bold);
        var trt = title.GetComponent<RectTransform>();
        trt.anchorMin = new Vector2(0, 1);
        trt.anchorMax = new Vector2(1, 1);
        trt.pivot = new Vector2(0.5f, 1);
        trt.anchoredPosition = new Vector2(0, -10);
        trt.sizeDelta = new Vector2(-20, 30);

        // ターゲット選択行
        BuildTargetSelector(panel.transform);

        float y = -80f; // ターゲット行の下からトグル開始

        // スキントグル群
        for (int i = 0; i < availableSkins.Length; i++) {
            string sName = availableSkins[i];

            var row = CreateUI($"Toggle_{sName}", panel.transform, out RectTransform rrt);
            rrt.anchorMin = new Vector2(0, 1);
            rrt.anchorMax = new Vector2(1, 1);
            rrt.pivot = new Vector2(0, 1);
            rrt.anchoredPosition = new Vector2(10, y);
            rrt.sizeDelta = new Vector2(-20, 28);

            var tgl = row.AddComponent<Toggle>();

            // 現在選択中プレイヤーの状態に合わせて初期ON/OFF
            bool initOn = _perControllerActives[selectedIndex].Contains(sName);
            tgl.isOn = initOn;

            var bg = CreateImage(row.transform, "Background", new Vector2(14, -14), new Vector2(20, 20));
            var ck = CreateImage(bg.transform, "Checkmark", Vector2.zero, new Vector2(20, 20));
            tgl.graphic = ck.GetComponent<Image>();
            tgl.targetGraphic = bg.GetComponent<Image>();

            var label = CreateText(row.transform, sName, fontSize, FontStyle.Normal);
            var lrt = label.GetComponent<RectTransform>();
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(40, 0);
            lrt.offsetMax = Vector2.zero;

            // トグル変更時：現在選択中プレイヤーのリストを更新
            tgl.onValueChanged.AddListener(isOn => {
                var list = _perControllerActives[selectedIndex];
                if (isOn) {
                    if (!list.Contains(sName)) list.Add(sName);
                } else {
                    list.Remove(sName);
                }
                controllers[selectedIndex].ApplySkins(list);
            });

            y -= 34f;
        }

        // All / None / Reset ボタン
        MakeButton(panel.transform, "All", new Vector2(10, y), () => {
            var list = _perControllerActives[selectedIndex];
            list.Clear();
            list.AddRange(availableSkins);
            controllers[selectedIndex].ApplySkins(list);
            RefreshToggleVisuals();
        });

        MakeButton(panel.transform, "None", new Vector2(90, y), () => {
            var list = _perControllerActives[selectedIndex];
            list.Clear();
            controllers[selectedIndex].ApplySkins(list);
            RefreshToggleVisuals();
        });

        MakeButton(panel.transform, "Reset", new Vector2(170, y), () => {
            controllers[selectedIndex].RefreshPose();
            // RefreshPose後も、今のスキン構成を反映し直したければ↓を有効に
            controllers[selectedIndex].ApplySkins(_perControllerActives[selectedIndex]);
        });
    }

    void BuildTargetSelector(Transform panel) {
        // ラベル
        _targetLabel = CreateText(panel, "", fontSize, FontStyle.Normal);
        var lrt = _targetLabel.GetComponent<RectTransform>();
        lrt.anchorMin = new Vector2(0, 1);
        lrt.anchorMax = new Vector2(1, 1);
        lrt.pivot = new Vector2(0.5f, 1);
        lrt.anchoredPosition = new Vector2(0, -40);
        lrt.sizeDelta = new Vector2(-80, 24); // 左右ボタン分少し狭める
        _targetLabel.alignment = TextAnchor.MiddleCenter;

        // Prevボタン
        var prev = CreateUI("Btn_TargetPrev", panel, out RectTransform prPrev);
        prPrev.anchorMin = new Vector2(0, 1);
        prPrev.anchorMax = new Vector2(0, 1);
        prPrev.pivot = new Vector2(0, 1);
        prPrev.anchoredPosition = new Vector2(10, -40);
        prPrev.sizeDelta = new Vector2(24, 24);
        var imgPrev = prev.AddComponent<Image>();
        imgPrev.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);
        var btnPrev = prev.AddComponent<Button>();
        btnPrev.onClick.AddListener(() => ChangeTarget(-1));
        var txtPrev = CreateText(prev.transform, "<", fontSize, FontStyle.Bold);
        txtPrev.alignment = TextAnchor.MiddleCenter;
        var trPrev = txtPrev.GetComponent<RectTransform>();
        trPrev.anchorMin = Vector2.zero;
        trPrev.anchorMax = Vector2.one;
        trPrev.offsetMin = trPrev.offsetMax = Vector2.zero;

        // Nextボタン
        var next = CreateUI("Btn_TargetNext", panel, out RectTransform prNext);
        prNext.anchorMin = new Vector2(0, 1);
        prNext.anchorMax = new Vector2(0, 1);
        prNext.pivot = new Vector2(0, 1);
        prNext.anchoredPosition = new Vector2(226, -40); // パネル幅260を想定
        prNext.sizeDelta = new Vector2(24, 24);
        var imgNext = next.AddComponent<Image>();
        imgNext.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);
        var btnNext = next.AddComponent<Button>();
        btnNext.onClick.AddListener(() => ChangeTarget(+1));
        var txtNext = CreateText(next.transform, ">", fontSize, FontStyle.Bold);
        txtNext.alignment = TextAnchor.MiddleCenter;
        var trNext = txtNext.GetComponent<RectTransform>();
        trNext.anchorMin = Vector2.zero;
        trNext.anchorMax = Vector2.one;
        trNext.offsetMin = trNext.offsetMax = Vector2.zero;
    }

    void ChangeTarget(int delta) {
        if (controllers == null || controllers.Length == 0) return;
        selectedIndex = (selectedIndex + delta + controllers.Length) % controllers.Length;
        RefreshUIForCurrentTarget();
    }

    void RefreshUIForCurrentTarget() {
        if (controllers == null || controllers.Length == 0) return;
        if (selectedIndex < 0 || selectedIndex >= controllers.Length)
            selectedIndex = 0;

        // ラベル更新
        if (_targetLabel != null) {
            string name = controllers[selectedIndex] != null
                ? controllers[selectedIndex].gameObject.name
                : "null";
            _targetLabel.text = $"Target: {selectedIndex + 1}/{controllers.Length} ({name})";
        }

        // スキンを反映し、トグルも更新
        controllers[selectedIndex].ApplySkins(_perControllerActives[selectedIndex]);
        RefreshToggleVisuals();
    }

    void RefreshToggleVisuals() {
        if (_panelRoot == null || _perControllerActives == null) return;
        var list = _perControllerActives[selectedIndex];

        foreach (var t in _panelRoot.GetComponentsInChildren<Toggle>(true)) {
            var label = t.GetComponentInChildren<Text>();
            if (label == null) continue;
            string sName = label.text;
            bool on = list.Contains(sName);
            t.SetIsOnWithoutNotify(on);
        }
    }

    // ---- UI helpers (built-in Text/Font; adjust to TMP if 好み) ----
    static GameObject CreateUI(string name, Transform parent, out RectTransform rt) {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        rt = go.GetComponent<RectTransform>();
        return go;
    }

    static GameObject CreateImage(Transform parent, string name, Vector2 pos, Vector2 size) {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        return go;
    }

    static Text CreateText(Transform parent, string text, int size, FontStyle style) {
        var go = new GameObject("Text", typeof(RectTransform), typeof(Text));
        go.transform.SetParent(parent, false);
        var t = go.GetComponent<Text>();
        t.text = text;
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); // 新UnityはArial.ttf非推奨
        t.fontSize = size;
        t.fontStyle = style;
        t.alignment = TextAnchor.MiddleLeft;
        t.color = Color.white;
        return t;
    }

    void MakeButton(Transform parent, string label, Vector2 pos, System.Action onClick) {
        var go = CreateUI($"Btn_{label}", parent, out RectTransform rt);
        rt.anchorMin = new Vector2(0, 1);
        rt.anchorMax = new Vector2(0, 1);
        rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(70, 28);
        var img = go.AddComponent<Image>();
        img.color = new Color(0.2f, 0.2f, 0.2f, 0.9f);
        var btn = go.AddComponent<Button>();
        btn.onClick.AddListener(() => onClick?.Invoke());
        var txt = CreateText(go.transform, label, fontSize, FontStyle.Normal);
        txt.alignment = TextAnchor.MiddleCenter;
        var trt = txt.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
    }
}
